# Platform/profile/pause/IAP checks

Requires .NET SDK 11. From the package root:

```powershell
dotnet run --project Tests~/Platform/Platform.Tests.csproj
```

Compiles actual contracts, base classes, VK providers and PluginYG2 IAP adapter
against managed Unity/SDK doubles. Covers initialization, profile comparison,
overlapping pause reasons, snapshot events, Pending orders, disposal and static
bridge replacement. Native replies are simulated; no real account or payment is used.

The VK JavaScript scenarios and Emscripten link check live under `Tests~/VKAds`.
