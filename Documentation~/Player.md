# Сохранения и данные игрока

Модуль предоставляет IPlayerProvider через UniWebBridge.Player или VContainer. Он хранит значения в памяти, а Load/Save выполняют ввод-вывод. Сначала дождитесь Load; Get до загрузки вернёт значение по умолчанию, Set — false. Все вызовы выполняются в главном потоке Unity.

## Быстрый пример с UniTask

Установите UniTask 2.x в проект. Отдельная сборка UniWebKit.UniTask автоматически предоставляет LoadAsync и SaveAsync; ядро пакета не зависит от UniTask. В собственной игровой asmdef добавьте ссылку на UniWebKit.UniTask для использования этих расширений.

~~~csharp
using UniWebKit;

var player = UniWebBridge.Player;
var loaded = await player.LoadAsync();
if (!loaded.IsSuccess)
{
    // При Conflict сначала выберите локальные или платформенные данные.
    return;
}

player.Set("unlockedLevels", 3);
player.Set("RotSens", 0.5f);
player.Set("DISABLE_ADV", true);
player.Set("Language", "ru");

int checkpoint = player.Get("unlockedLevels", 0);
float sensitivity = player.Get("RotSens", 0.5f);
bool adsDisabled = player.Get("DISABLE_ADV", false);

var saved = await player.SaveAsync();
if (saved.IsFallback)
    UnityEngine.Debug.LogWarning(saved.CloudError);
~~~

Без UniTask доступны те же операции через callbacks, возвращающие отменяемый PlayerStorageOperation:

~~~csharp
player.Load(result =>
{
    if (!result.IsSuccess) return;
    player.Set("unlockedLevels", 3);
    player.Save(saved => UnityEngine.Debug.Log(saved.Location));
});
~~~

Операция может завершиться синхронно. Cancel/Dispose отменяет ожидание, но не откатывает уже отправленную серверу запись. Dispose провайдера завершает его текущую операцию отменой. Не вызывайте Load/Save параллельно на одном провайдере: получите Busy. Менять значения через Set во время Save можно; новые изменения останутся несохранёнными до следующего Save.

## Типы

- string, int, long, float, double, decimal, bool и enum.
- Одномерные массивы, List<T> и Serializable DTO с полями, поддерживаемыми JsonUtility.
- Примитивные числа кодируются инвариантными строками: long и decimal не теряют точность при прохождении через JavaScript/JSON.
- Тип читается явно. Get<long> не подменяет сохранённый int; TryGet сообщает об отсутствии или несовместимом типе.
- Для объектов Get возвращает десериализованную копию. Изменённый объект нужно передать обратно в Set.
- NaN и Infinity отклоняются.
- Стандартный JSON-сериализатор не поддерживает Dictionary, HashSet, DateTime, Guid, свойства вместо полей, вложенные контейнеры и циклические DTO. Set возвращает false для неподдерживаемой структуры.
- Для других форматов задайте PlayerStorageOptions.Serializer, реализующий IPlayerDataSerializer. Например, адаптер Newtonsoft.Json может поддерживать словари и даты без зависимости ядра UniWebKit от Newtonsoft.

~~~csharp
[System.Serializable]
public class Progress
{
    public int level;
    public string[] completedChallenges;
}

player.Set("progress", new Progress
{
    level = 3,
    completedChallenges = new[] { "first_jump" }
});
var progress = player.Get<Progress>("progress");
~~~

DeleteKey и DeleteAll изменяют только данные этого модуля. Для записи изменений нужен Save. PlayerPrefs.DeleteAll и сброс чужих SDK модуль не вызывает.

## Локальное и облачное сохранение

PlayerStorageResult различает IsSuccess, Location, IsCloudConfirmed, IsLocalSaved и CloudError.

| Режим | Поведение |
|---|---|
| Preferred | Сначала записывает локальный кэш, затем использует платформу. При ошибке платформы успешный локальный фолбэк имеет Location.Local, IsFallback=true и CloudError. |
| LocalOnly | Работает только с локальным кэшем. |
| CloudOnly | Требует подтверждения облачной операции. Локальный кэш не превращает ошибку облака в успех. |

Для выдачи оплаченной награды используйте подтверждённый результат:

~~~csharp
// Сначала отметьте чек обработанным и начислите награду в данных игрока.
var saved = await player.SaveAsync(PlayerStorageMode.CloudOnly);
if (saved.IsSuccess && saved.IsCloudConfirmed)
{
    // Теперь можно консумировать покупку.
}
~~~

Это подтверждение сохранения, а не серверная транзакция покупки. За уникальность чека и выдачу награды отвечает игра. Не считайте локальный фолбэк подтверждённым облаком.

Кэш хранится через Unity PlayerPrefs: в WebGL Unity использует браузерное хранилище. Ключ кэша включает провайдера, профиль и CloudKey. При смене профиля старые значения перестают быть доступны через Get/Set; вызовите Load для нового профиля. Гостевые данные могут переноситься самим SDK; идентичность платформенного хранилища подтверждается текущей SDK-сессией, а не старым owner внутри документа.

## Конфликты и ошибки

После неудачной отправки кэш сохраняет признак pending. Если при следующей загрузке платформа содержит другую ревизию, Load вернёт Conflict и сохранит локальную копию. Выбор должен быть явным:

~~~csharp
// Принять платформенные данные:
await player.LoadAsync(conflictResolution: PlayerStorageConflictResolution.UseCloud);

// Или оставить локальные, затем явно отправить их:
await player.LoadAsync(conflictResolution: PlayerStorageConflictResolution.UseLocal);
await player.SaveAsync();
~~~

Не выбирайте UseLocal автоматически при каждом запуске: это может заменить более свежий прогресс другого устройства. Изменение данных во время Load также не приводит к их тихой перезаписи. Повреждённый локальный документ не заменяется пустым сохранением. Для игр со своим интерфейсом конфликта используйте результат Load и отдельное подтверждение выбора игроком.

На одном ключе браузерный мост упорядочивает запросы, включая поздние ответы после таймаутов. Это не заменяет поддержку конфликтов и атомарных транзакций на сервере; одновременную игру с нескольких устройств проверяйте отдельно.

## GamePush

Выберите GamePush и включите UNIWEB_GAMEPUSH. Создайте в панели поле игрока CloudKey, по умолчанию UniWebKitSave, типа string с пустым значением по умолчанию. В нём хранится весь переносимый документ, поэтому отдельное поле панели для каждой настройки не требуется.

В WebGL запись ожидает Promise gp.player.sync({ storage: "cloud" }) и проверяет состояние после синхронизации. У запроса свой ID; посторонний autosave не завершает его ожидание. Таймауты работают по времени Unity, без Task.Delay и потоков.

Load читает готовое состояние SDK после player.ready. Оно может происходить из локального хранилища GamePush, поэтому IsCloudConfirmed=false, а Load в режиме CloudOnly возвращает Unsupported. Для обычной загрузки используйте Preferred; режим CloudOnly поддерживается при Save.

GamePush задаёт типы полей в панели: number, string и boolean. См. [состояние игрока](https://docs.gamepush.com/docs/player/) и [облачные сохранения](https://docs.gamepush.com/docs/player/cloud-saves/).

Для лидербордов или ранее существовавших полей добавьте PlayerStorage.GamePushFields в настройках:

| Key | PlatformField | Type |
|---|---|---|
| unlockedLevels | unlockedLevels | Int32 |
| RotSens | RotSens | Single |
| DISABLE_ADV | DISABLE_ADV | Boolean |

При отсутствии переносимого документа Int32, Single, Boolean и String инициализируются из указанных нативных полей. Сохранение записывает документ и привязанные поля перед общей синхронизацией. Поля необходимо создать в панели с соответствующими типами. Int64, Double, Decimal, Enum и JSON привязываются к string для сохранения точности.

Удаление переносимого ключа не сбрасывает нативное поле: для сброса задайте ему явное значение через Set. Это не позволяет DeleteAll случайно удалить чужие данные или постоянные права игрока.

В Editor и native API используется GP_Player; без адресного подтверждения Sync результат имеет Location.Platform, IsCloudConfirmed=false. CloudOnly в таком окружении не выдаёт ложный успех.

## PluginYG2

Выберите PluginYG2 и включите UNIWEB_PLUGIN_YG2. Для стандартного адаптера нужны модули Storage и RedefinePlayerPrefs: документ помещается в строковое значение существующего SavesYG через публичный API RedefineYG.PlayerPrefs. SDK и SavesYG модуль не изменяет, partial-класс из другой сборки не создаёт.

Вызов YG2.SaveProgress не возвращает подтверждение записи в облако. Поэтому результат — Location.Platform и IsCloudConfirmed=false. CloudOnly возвращает Unsupported. Это относится к контракту установленной версии PluginYG2; не следует считать возврат из SaveProgress облачным ACK.

Платформу внутри PluginYG2 выбирает сам SDK. Адаптер не привязан к API Яндекса. Если нужных модулей нет, доступен локальный фолбэк. Существующие особенности компиляции YG2 относительно asmdef остаются такими же, как у рекламного и платёжного провайдеров.

Исходный API: [PluginYG2](https://github.com/JustPlay-Max/Unity-PluginYG-2).

## VK Games / VK Bridge

Выберите VKGames. Адаптер не требует C#-пакета VK; WebGL-шаблон должен предоставить и инициализировать window.vkBridge. Он обращается к VKWebAppStorageGet и VKWebAppStorageSet, где ключи и значения строковые. Документ сериализуется целиком; числа не передаются как JavaScript Number.

См. [официальные методы VK Bridge storage](https://github.com/VKCOM/vk-mini-apps-api/blob/master/docs/classes/vkminiappapi.md#storage-methods). Это API VK Bridge, а не облачные сохранения настольного лаунчера VK Play. При использовании VK через GamePush выбирайте GamePush.

После Set выполняется Get и сравнение полного значения: отклонённая или усечённая запись не считается подтверждённой. Учитывайте квоту размера значения и лимиты запросов VK для своего приложения; модуль не обрезает данные и не разбивает большой документ на неатомарные части. При превышении лимита сохранится локальный кэш, а CloudOnly завершится ошибкой.

## Своя площадка

Реализуйте IPlayerStorageBackend и зарегистрируйте CustomPlayerProvider в bootstrapper/DI. Backend читает/пишет непрозрачный строковый документ, сообщает текущий ProfileId и честно указывает CanConfirmCloudWrites/Location. Успешный callback с cloudConfirmed=true допустим только после фактического подтверждения записи.

В бизнес-логике игры используются только IPlayerProvider, типы данных и результаты операций; условные директивы площадок там не нужны.
