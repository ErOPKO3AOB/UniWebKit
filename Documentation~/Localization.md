# Локализация: выбор и смена языка

`ILocalizationProvider` управляет языком игры через единый контракт, без типов SDK.
Доступ: `UniWebBridge.Localization` или DI после `builder.RegisterUniWeb()`.

```csharp
var localization = UniWebBridge.Localization;
string language = localization.Language; // "ru", "en", "pt-BR"
bool accepted = localization.SetLanguage("en");
```

| Член | Назначение |
| --- | --- |
| `IsSupported` | Функция доступна; локальный fallback также поддерживает выбор языка |
| `IsReady` | Начальный язык получен и можно переключать язык |
| `Language` | Текущий код языка; до готовности SDK — предварительный системный язык |
| `LanguageChanged` | Уведомление о готовности SDK и последующих изменениях языка |
| `SetLanguage(code)` | `true`: запрос принят. `false`: неверный/неподдерживаемый код, SDK ещё не готов или провайдер освобождён |
| `Dispose()` | Отписка от SDK, остановка ожидания и освобождение подписчиков |

Пробелы по краям удаляются, `_` заменяется на `-`, регистр нормализуется:
`PT_br` → `pt-BR`, `zh_hant` → `zh-Hant`. Общий API принимает языковые теги,
а не закрытый enum конкретной площадки. Он проверяет форму кода, а не наличие перевода в игре.
Повторная установка текущего языка не вызывает событие повторно.

## Обновление UI

Подпишитесь и сразу примените текущий язык, если провайдер уже готов:

```csharp
using UnityEngine;
using UniWebKit;

public class GameLanguage : MonoBehaviour
{
    private ILocalizationProvider _localization;

    private void OnEnable()
    {
        _localization = UniWebBridge.Localization;
        _localization.LanguageChanged += Apply;
        if (_localization.IsReady) Apply(_localization.Language);
    }

    private void OnDisable() => _localization.LanguageChanged -= Apply;

    private void Apply(string language)
    {
        // Выберите переводы игры и обновите UI.
        // Если нужного перевода нет: сначала базовый язык, затем язык по умолчанию.
        Debug.Log(language);
    }
}
```

Через VContainer внедрите `ILocalizationProvider` вместо обращения к статическому мосту.
Вызовы и события работают в главном потоке Unity. Событие может прийти синхронно
из `SetLanguage`. `true` не означает подтверждение удалённой операции;
фактическую смену отражает `LanguageChanged`. У SDK нет общего события ошибки смены языка.

## Провайдеры

- **GamePush:** начальный язык через `GP_Language.Current()`, переключение через
  `GP_Language.Change`, внешние изменения через `OnChangeLanguage`. Поддерживаются
  `en`, `ru`, `tr`, `fr`, `it`, `de`, `es`, `zh`, `pt`, `ko`, `ja`, `ar`, `hi`, `id`.
  Иные коды, включая региональные, отклоняются: адаптер не подменяет их английским.
- **PluginYG2:** модуль `Localization_yg`, `YG2.lang`, `SwitchLanguage` и
  `onSwitchLang`. Работает с выбранной в PluginYG2 площадкой. Стратегию начального
  определения и сохранения языка задаёт сам SDK; UniWebKit её не переопределяет.
  Без модуля Localization используется локальное поведение.
- **VKGames:** `vk_language` из `VKWebAppGetLaunchParams`, затем из URL запуска,
  затем `navigator.languages` / `navigator.language`; если язык неизвестен — `en`.
  Коды VK `ua` и `kz` приводятся к `uk` и `kk`. Начальный язык доступен сразу;
  поздний ответ SDK или возвращение в игру может уточнить его через `LanguageChanged`.
  `SetLanguage` меняет язык только внутри игры: после явного выбора ответ SDK его
  не перезаписывает. В Editor и нативных приложениях начальный язык берётся из Unity.
- **Local:** язык `Application.systemLanguage` (неизвестный → `en`) и переключение
  в памяти. Используется для Custom и как безопасное поведение по умолчанию.
  В WebGL системный язык Unity не заменяет определение языка SDK площадки.

Адаптеры GamePush и PluginYG2 дожидаются готовности SDK, затем подписываются и публикуют начальный язык.
Временный объект ожидания уничтожается после готовности или `Dispose`; постоянного
покадрового опроса нет. Это учитывает сброс событий при старте PluginYG2 в Editor.
Если SDK не готов, ожидание продолжается до его готовности или освобождения провайдера.

Модуль не переводит тексты, не хранит таблицы, не требует TextMeshPro или Unity Localization.
Подключите словари, Unity Localization или другую систему в обработчике `LanguageChanged`.
Список переводов игры и выбор резервного языка принадлежат игре. Local и VKGames
не сохраняют выбор между запусками; если нужно, используйте систему прогресса игры.

## Своя площадка

Наследуйте `LocalizationProviderBase` или реализуйте `ILocalizationProvider`.
Базовый класс предоставляет нормализацию, локальное переключение и событие;
адаптер SDK вызывает защищённый `ApplyLanguage` при получении фактического языка.
Для отложенной готовности передайте `isReady: false` в базовый конструктор.

```csharp
UniWebBridge.Initialize(localization: new LocalLocalizationProvider("en"));
```

При ручной сборке отключите `AutoInitializeStaticAPI`. Замена провайдера в мосте
освобождает старый экземпляр; повторная передача того же экземпляра его сохраняет.
В DI зарегистрируйте свой `ILocalizationProvider` после `RegisterUniWeb()`.

Источники: [PluginYG2 Localization](https://max-games.ru/plugin-yg/doc/lang/),
[GamePush Unity SDK](https://github.com/GamePushService/GamePush-Unity-plugin#gp_language).
Параметры VK сверены с [типами VK Bridge](https://github.com/VKCOM/vk-bridge/blob/master/packages/core/src/types/data.ts)
и [значениями платформ и языков](https://github.com/VKCOM/vk-bridge/blob/master/packages/core/src/types/data/values.ts).
Сигнатуры также сверены с установленными локально исходниками SDK.
