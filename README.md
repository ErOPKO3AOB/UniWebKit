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
2. Откройте `Window → UniWeb API Settings` или `Project Settings → UniWeb API`. Оба окна используют единственный конфиг `Assets/UniWebKit/Resources/UniWebSettings.asset` и создают его при отсутствии. Старый конфиг из `Assets/Resources/UniWebSettings.asset` автоматически переносится с сохранением GUID, если нового ещё нет. Если существуют оба, сначала оставьте нужные настройки в едином конфиге и удалите старый: два одноимённых ресурса создают неоднозначность при загрузке.
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

Тип устройства доступен через `UniWebBridge.Envir` или `IEnvirProvider` в VContainer: `IsMobile`, `IsDesktop`, `DeviceType`, `IsReady`. Реализованы GamePush, PluginYG2, VKGames и Unity-fallback. [Описание Envir](Documentation~/Environment.md).

## Локализация

`UniWebBridge.Localization` / `ILocalizationProvider` предоставляет текущий `Language`,
`IsReady`, `SetLanguage("en")` и событие `LanguageChanged`. Поддерживаются GamePush,
PluginYG2, VKGames и локальный fallback. Коды языков — обычные строки, без enum SDK.
Модуль подключается к любой системе переводов игры.
[Настройка и примеры](Documentation~/Localization.md).

## Таблицы лидеров

`ILeaderboardProvider` / `UniWebBridge.Leaderboards`: запись счёта, получение топа
и результата текущего игрока. Подготовлены общий контракт, модели, базовый класс
и безопасная заглушка; адаптеры платформ будут добавлены позже.
[Описание и примеры](Documentation~/Leaderboards.md).

## Платформа, профиль и пауза

`UniWebBridge.Platform`, `.Profile`, `.Game`, `.Pause` предоставляют готовность SDK,
профиль игрока, игровой lifecycle и состояние паузы. Есть контракты, базовые классы
и заглушки; для VK реализованы инициализация, профиль и пауза. Игровой lifecycle
VK остаётся неподдерживаемым. Аналитика в пакет не добавляется.
[Описание и примеры](Documentation~/Platform.md).

## Реклама VK Games

Реклама VK Games также доступна через `IAdvProvider`: JavaScript-мост входит в пакет,
WebGL-шаблон остаётся в проекте. [Подключение и ограничения](Documentation~/VKGamesAds.md).

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
- [x] **Абстракция лидербордов (`ILeaderboardProvider`)** — Запись счёта, топ и результат текущего игрока; базовый класс и безопасная заглушка. Адаптеры платформ пока не реализованы.
- [x] **Внутриигровые покупки (`IInAppProvider`)** — Каталог, восстановление покупок, явная консумация и опциональные подписки. Провайдеры GamePush и PluginYG2 (с автоматической консумацией SDK); для остальных площадок — безопасная заглушка.
- [ ] **Новые платформы из коробки** — типа GameDistribution.

## Лицензия
MIT

## Сохранения игрока

Модуль IPlayerProvider / UniWebBridge.Player предоставляет типизированные данные, локальный кэш, адаптеры платформ и явное подтверждение облачной записи. Поддерживаются GamePush, PluginYG2 и прямой VK Bridge. Расширения UniTask находятся в отдельной необязательной сборке; у ядра нет новой зависимости. Настройка, привязка нативных полей, разрешение конфликтов и примеры async API приведены в [документации сохранений](Documentation~/Player.md).
