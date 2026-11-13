# Settings asset checks

Run from the package root with .NET SDK 11:

```powershell
dotnet run --project Tests~/Settings/Settings.Tests.csproj
```

Uses actual settings and editor utility sources with an in-memory AssetDatabase.
Checks single creation, Create-menu selection, existing values, legacy moves with
GUID preservation, conflicting assets and move failure.
