# Leaderboard contract checks

Requires .NET SDK 11. From the package root:

```powershell
dotnet run --project Tests~/Leaderboards/Leaderboards.Tests.csproj
```

Checks the actual contract, safe fallback, snapshot ownership, numeric precision and
replacement/disposal in the static bridge. Other bridge features use small test doubles.
No platform SDKs or service calls are involved.
