# Managed localization checks

Requires .NET SDK 11. Run from the package root:

```powershell
dotnet run --project Tests~/Localization/Localization.Tests.csproj -p:LocalizationDefines=Localization_yg
dotnet run --project Tests~/Localization/Localization.Tests.csproj
```

Runs actual localization sources against managed Unity/SDK doubles. Checks startup,
normalization, SDK mappings and external changes, missing optional modules, disposal,
and replacement in the static bridge. Does not call platform services.
Unity Editor and WebGL integration remain separate checks.
