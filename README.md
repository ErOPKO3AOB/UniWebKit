# UniWebKit — Unified WebGL SDK Bridge

Легковесный модульный мост для интеграции WebGL-платформ (PluginYG2, GamePush и др.) в Unity. Позволяет писать игровую логику один раз и переключать целевые площадки без переписывания кода.

## Преимущества

* **Никакого Vendor Lock-in:** Игра общается только с единым контрактом в неймспейсе `UniWebKit`. Смена SDK сводится к переключению настройки в конфиге.
* **Безопасность (Shadow Fallbacks):** Если выбранная платформа не поддерживает какую-то фичу, плагин автоматически подставляет безопасную заглушку (`IsSupported = false`). Никаких `NullReferenceException` и лишних проверок на `null`.
* **Две архитектуры из коробки:** Полная поддержка Dependency Injection (**VContainer**) и классического **Static API**.
* **Чистая сборка (Zero Overhead):** Неактивные SDK полностью вырезаются на этапе компиляции через Scripting Define Symbols, не раздувая финальный WebGL-билд.
* **Умная работа с колбэками:** Глобальные события сторонних SDK (например, в PluginYG2) автоматически оборачиваются в безопасные одноразовые подписки без утечек памяти.

---

## Установка и настройка

1. Установите пакет через **Package Manager** (`Add package from git URL...`):
   ```text
   [https://github.com/ErOPKO3AOB/UniWebKit.git](https://github.com/ErOPKO3AOB/UniWebKit.git)
   ```
2. Создайте конфигурационный файл настроек `UniWebSettings` и поместите его в папку `Resources` (`Assets/Resources/UniWebSettings.asset`).
3. Выберите в инспекторе целевую платформу (`PluginYG2`, `GamePush`, `Custom`) и архитектуру (`VContainer` или `Static`).

---

## Инициализация

### Вариант 1: VContainer (DI)
Зарегистрируйте сервисы одной строкой в вашем корневом `LifetimeScope`. Конфиг подгрузится из `Resources` автоматически:

```csharp
using UniWebKit;
using VContainer;
using VContainer.Unity;

public class GameLifetimeScope : LifetimeScope
{
    protected override void Configure(IContainerBuilder builder)
    {
        builder.RegisterUniWeb();
    }
}
```

### Вариант 2: Static API
Если в конфиге включен флаг `AutoInitializeStaticAPI`, мост `UniWebBridge` инициализируется сам до загрузки первой сцены (`BeforeSceneLoad`). Никакого дополнительного кода для старта писать не нужно.

---

## Использование в коде

Единый интерфейс `IAdvProvider` работает одинаково независимо от выбранной архитектуры.

**Через VContainer:**
```csharp
using UniWebKit;
using UnityEngine;
using VContainer;

public class AdsController : MonoBehaviour
{
    [Inject] private readonly IAdvProvider _ads;

    public void ShowInterstitial()
    {
        if (!_ads.IsSupported || !_ads.IsInterstitialReady) return;

        _ads.ShowInterstitial(
            onOpened: () => Time.timeScale = 0f,
            onClosed: success => Time.timeScale = 1f
        );
    }

    public void ShowRewarded()
    {
        _ads.ShowRewarded(
            onReward: () => Debug.Log("Игрок получил награду!"),
            onOpened: () => Time.timeScale = 0f,
            onClosed: success => Time.timeScale = 1f,
            placementId: "double_coins"
        );
    }
}
```

**Через Static API:**
```csharp
using UniWebKit;

// Вызов из любого места игры
if (UniWebBridge.Ads.IsRewardedReady)
{
    UniWebBridge.Ads.ShowRewarded(onReward: GiveCoins);
}

// Управление стики-баннерами
UniWebBridge.Ads.ShowStickyBanner();
```

**Внутриигровые покупки:**

`UniWebBridge.InApps` (или `IInAppProvider` через VContainer) предоставляет каталог, покупки игрока, обычную покупку, явную консумацию и опциональные подписки. Реализованы адаптеры GamePush и PluginYG2; для остальных площадок предусмотрена безопасная заглушка. Подробности, ограничения SDK, восстановление и порядок выдачи награды описаны в [документации покупок](Documentation~/InAppPurchases.md).

GamePush поддерживает ручную консумацию: игра сначала начисляет и сохраняет награду, затем вызывает `Consume`. PluginYG2 сам запускает консумацию при покупке и возвращает `ConsumeMode.Automatic`: повторная консумация новой покупки не требуется. Постоянные права и награды сохраняет игровая система прогресса.

---

## Окружение

Тип устройства доступен через `UniWebBridge.Envir` или `IEnvirProvider` в VContainer: `IsMobile`, `IsDesktop`, `DeviceType`, `IsReady`. Реализованы GamePush, PluginYG2 и Unity-fallback. [Описание Envir](Documentation~/Environment.md).

## Создание кастомного провайдера

Если нужной платформы еще нет из коробки, вы можете легко подключить свой SDK, не меняя исходники пакета:

1. Унаследуйтесь от базового класса `AdvProviderBase` (переопределять нужно только те методы, которые реально есть у вашей платформы):

```csharp
using System;
using UniWebKit;

public class CustomVKAdvProvider : AdvProviderBase
{
    public override bool IsInterstitialReady => true;

    public override void ShowInterstitial(Action onOpened = null, Action<bool> onClosed = null, Action<string> onError = null)
    {
        // Логика вызова вашего SDK
        onOpened?.Invoke();
        onClosed?.Invoke(true);
    }
}
```

2. Подключите провайдер в проект:
   * **Для VContainer:** зарегистрируйте ваш класс *после* вызова `RegisterUniWeb()` (контейнер перезапишет дефолтную заглушку по правилу Last-in Wins):
     ```csharp
     builder.RegisterUniWeb();
     builder.Register<CustomVKAdvProvider>(Lifetime.Singleton).As<IAdvProvider>();
     ```
   * **Для Static API:** снимите галочку `AutoInitializeStaticAPI` в настройках и передайте провайдер вручную при старте игры:
     ```csharp
     UniWebBridge.Initialize(new CustomVKAdvProvider());
     ```

---

## Roadmap (В разработке)

Пакет активно развивается. Архитектура теневых фолбэков (`IUniWebFeature`) спроектирована под масштабирование без риска крашей на платформах с урезанным API. В ближайших обновлениях планируются:

- [x] **Реклама (`IAdvProvider`)** — Interstitial, Rewarded, Sticky-баннеры, проверка AdBlock.
- [ ] **Сохранения и профиль (`IPlayerProvider`)** — Облачные сохранения с автоматическим фолбэком в локальный `PlayerPrefs` / `IndexedDB`, если у площадки нет своего облака.
- [ ] **Таблицы лидеров (`ILeaderboardProvider`)** — Запись рекордов и получение таблиц (с безопасным отключением UI через `IsSupported` на платформах без лидербордов).
- [x] **Внутриигровые покупки (`IInAppProvider`)** — Каталог, восстановление покупок, явная консумация и опциональные подписки. Провайдеры GamePush и PluginYG2 (с автоматической консумацией SDK); для остальных площадок — безопасная заглушка.
- [ ] **Новые платформы из коробки** — типа GameDistribution.

## Лицензия
MIT
