# Envir: тип устройства

Модуль находится в `Runtime/Features/Environment`. `IEnvirProvider` предоставляет `DeviceType`, `IsMobile`, `IsDesktop`, `IsReady` и стандартный `IsSupported`.

```csharp
var envir = UniWebBridge.Envir;
if (envir.IsReady)
{
    mobileControls.SetActive(envir.IsMobile);
    desktopControls.SetActive(envir.IsDesktop);
}
```

Через VContainer получите `IEnvirProvider` после `RegisterUniWeb()`. Регистрация следует Last-in Wins: сначала `UnityEnvirProvider`, затем адаптер выбранного SDK. Прежние вызовы `UniWebBridge.Initialize(ads, inApps)` сохраняют совместимость; третий необязательный параметр — `envir`.

- `Mobile` включает телефоны и планшеты; `Desktop` обозначает ПК.
- `Unknown` не считается ПК: оба флага возвращают `false`. TV/консоли без подходящей классификации также остаются `Unknown`.
- GamePush использует `GP_Device.IsMobile()`/`IsDesktop()` после `GP_Init.isReady`. В Editor учитывается штатная эмуляция устройства GamePush.
- PluginYG2 использует общий `YG2.envir`, включая признак планшета, под `UNIWEB_PLUGIN_YG2` и `EnvirData_yg`. Это не привязка к конкретной площадке. Без модуля EnvirData остаётся Unity-fallback.
- Unity-fallback распознаёт Desktop/Handheld в Editor и нативных приложениях. В WebGL без готовых данных SDK возвращает `Unknown`: цель сборки WebGL не определяет устройство браузера.
- `IsSupported` означает наличие функциональности провайдера; `IsReady` — готовность классификации. До готовности SDK провайдер может предоставлять fallback, но `IsReady` остаётся `false`.
- Данные читаются при обращении. Событий SDK, подписок, фоновых опросов и игровых объектов модуль не создаёт.

Язык, браузер и прочие данные в первую версию не включены.
