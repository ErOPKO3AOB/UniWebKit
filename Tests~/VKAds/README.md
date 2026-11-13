# VK advertisement checks

Run from the package root:

```powershell
dotnet run --project Tests~/VKAds/VKAds.Tests.csproj
node Tests~/VKAds/bridge.test.cjs
```

The C# checks require .NET SDK 11. They compile the actual provider with Editor
guards and use reflection to deliver native callback sequences to its pending
operation. Unity audio/logging and the AOT attribute use small test doubles.

The JavaScript checks require Node 12+. They run the actual `.jslib` after replacing
only Emscripten `makeDynCall` macros with direct calls. They simulate SDK startup,
ad inventory, success/failure, concurrent requests and SDK script loading.

For a real Emscripten link check, use Unity's bundled toolchain and its `.emscripten`
configuration, with the output under `obj`:

```text
emcc Tests~/VKAds/link-smoke.c --js-library Runtime/Providers/VKGames/Plugins/WebGL/UniWebVKBridge.jslib -O2 -sENVIRONMENT=node -o Tests~/VKAds/obj/link-smoke.js
node Tests~/VKAds/obj/link-smoke.js
```

This checks native function signatures and JS library dependencies. It is not an
IL2CPP game build or a test of real VK advertisements.
