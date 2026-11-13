# Платформа, профиль и пауза

В ядре появились независимые от SDK контракты, базовые классы и безопасные
заглушки. Все вызовы и callbacks выполняются в главном потоке Unity; callbacks
могут завершиться синхронно. Аналитика в эти модули не входит.

| Контракт / Static API | Содержимое | VK Bridge |
| --- | --- | --- |
| `IPlatformProvider` / `UniWebBridge.Platform` | `IsReady`, `IsBusy`, `Initialized`, `Initialize` | `VKWebAppInit` |
| `IProfileProvider` / `UniWebBridge.Profile` | `CurrentProfile`, `IsReady`, `IsBusy`, `ProfileChanged`, `Refresh` | `VKWebAppGetUserInfo` |
| `IGameProvider` / `UniWebBridge.Game` | `GameReady`, `GameplayStart`, `GameplayStop` | Безопасная заглушка, `IsSupported = false` |
| `IPauseProvider` / `UniWebBridge.Pause` | `IsPaused`, `PauseChanged`, `SetPaused` | Скрытие/возврат приложения и вкладки, реклама и окно оплаты |

Готовность SDK отделена от `IEnvirProvider.IsReady`: первая означает завершение
инициализации, вторая — готовность классификации устройства. Создание статического
моста (`UniWebBridge.IsInitialized`) также не означает готовность внешнего SDK.

## Инициализация

```csharp
var platform = UniWebBridge.Platform;
platform.Initialize(
    onReady: () => UnityEngine.Debug.Log("SDK готов"),
    onError: error => UnityEngine.Debug.LogWarning(error));
```

VK-провайдер начинает инициализацию при создании. Параллельные вызовы `Initialize`
ожидают одну операцию; после успеха `Initialized` вызывается один раз, а
`IsReady` уже равен `true`. Повторный вызов после успеха сразу завершает callback.
Ошибка не превращается в успех: её можно обработать и повторить `Initialize`.
В Editor VK не вызывается, операция возвращает `Unsupported`.

`IsSupported` у платформы/профиля VK означает возможность обращения к мосту в
WebGL, а не подтверждение успешной авторизации или доступности удалённого сервиса.
Успех определяется результатом операции. Для других SDK пока используются
заглушки; здесь не добавлены их реализации.

## Профиль

`ProfileInfo` — неизменяемый снимок со строковыми `Id`, `Name`, `AvatarUrl`.
До первой загрузки `CurrentProfile = null`. Идентификатор не выводится из имени.

```csharp
var profile = UniWebBridge.Profile;
profile.ProfileChanged += current =>
{
    // Сравните current.Id с ранее сохранённым ID и обновите игровые сервисы/UI.
};
profile.Refresh(onSuccess: current => UnityEngine.Debug.Log(current.Name));
```

VK-провайдер сам запрашивает профиль при создании и обновляет его при возврате
в приложение/видимую вкладку. Можно вызвать `Refresh` явно. Событие вызывается
при первом результате и изменении ID/имени/аватара; одинаковые данные не вызывают
его повторно. Сохраняемый профиль при ошибке обновления остаётся последним
полученным снимком, а ошибка передаётся через callback явного `Refresh`.
Параллельный `Refresh` возвращает `Busy`; при первоначальной автозагрузке
подпишитесь на `ProfileChanged` и примените `CurrentProfile`, если `IsReady` уже равен `true`.

У проверенного VK Bridge нет отдельного события смены аккаунта: изменение
обнаруживается сравнением ответов `GetUserInfo`, а не выдуманным SDK-событием.
Профиль предназначен для клиентской логики и UI, не заменяет серверную проверку
подписи/платежа. При смене пользователя игра должна заново загрузить его прогресс
и прекратить обработку операций предыдущей сессии.

## Пауза

```csharp
var pause = UniWebBridge.Pause;
pause.PauseChanged += paused => UnityEngine.Time.timeScale = paused ? 0f : 1f;
UnityEngine.Time.timeScale = pause.IsPaused ? 0f : 1f;

pause.SetPaused(true);  // Например, открыто меню игры.
pause.SetPaused(false); // Убирает только ручную причину.
```

Итоговое состояние — OR ручной и платформенной причин. Возврат вкладки не
снимает паузу скрытого VK-приложения; закрытие рекламы не снимает паузу меню.
Повторные одинаковые состояния не публикуются. Провайдер не меняет
`Time.timeScale` или звук: этим управляет игровой контроллер.

Когда создан VK-провайдер паузы (в DI — когда он разрешён контейнером),
VK-адаптер рекламы передаёт управление звуком игровому контроллеру и не пытается
восстановить его поверх другой причины паузы. При использовании только рекламы
сохраняется прежнее поведение локального приглушения/восстановления звука.

Без SDK доступен `LocalPauseProvider`, поддерживающий ручную причину.
`UnsupportedPauseProvider` также предусмотрен для полностью отключённой функции.

## Game lifecycle

`GameProviderBase` содержит безопасные виртуальные методы. Для VK используется
`UnsupportedGameProvider`: в проверенном контракте VK Bridge нет эквивалентов
`GameReady/GameplayStart/GameplayStop`. Мост не подменяет их аналитическими
событиями или повторным `VKWebAppInit`.

## Подключение и освобождение

Static Bootstrapper выбирает VK-провайдеры автоматически. В VContainer получите
эти интерфейсы после `RegisterUniWeb()`. Собственные реализации регистрируются
после него; для ручного Static API доступны новые необязательные аргументы:

```csharp
UniWebBridge.Initialize(platform: myPlatform, profile: myProfile, pause: myPause, game: myGame);
```

Старые вызовы `Initialize` совместимы. Замена освобождает прежние disposable
объекты; передача того же экземпляра его сохраняет. `Dispose` удаляет подписки,
завершает ожидающие callbacks ошибкой `Disposed` и отсоединяет поздние ответы.
Временные ошибки описывает общий `UniWebError`; типы и коды SDK наружу не выходят.

Источники: [контракт VK Bridge](https://github.com/VKCOM/vk-bridge/blob/master/packages/core/src/types/data.ts),
[официальная обёртка lifecycle и профиля](https://github.com/VKCOM/vk-mini-apps-api/blob/master/src/index.ts).
