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
- VKGames использует `vk_platform` из параметров запуска: `desktop_*` — ПК, поддерживаемые `mobile_*` — телефоны и планшеты. Приоритет у ответа `VKWebAppGetLaunchParams`, затем у параметров URL, затем у браузерных данных. iPadOS с desktop User-Agent учитывается как планшет; TV, консоли и нераспознанные устройства остаются `Unknown`. В Editor и нативных приложениях используется Unity-fallback.
- Unity-fallback распознаёт Desktop/Handheld в Editor и нативных приложениях. В WebGL без готовых данных SDK возвращает `Unknown`: цель сборки WebGL не определяет устройство браузера.
- `IsSupported` означает наличие функциональности провайдера; `IsReady` — готовность классификации. GamePush и PluginYG2 ожидают готовности SDK. У VKGames известная классификация из URL или браузера доступна сразу, независимо от `IPlatformProvider.IsReady`; ответ SDK может её уточнить.
- Данные читаются при обращении. VKGames использует общий объект `VKGamesRuntime`: запрашивает параметры запуска при регистрации и возвращении в игру, без отдельного покадрового опроса окружения.

Язык доступен через отдельный [модуль локализации](Localization.md). Тип браузера и прочие данные в контракт не включены.
