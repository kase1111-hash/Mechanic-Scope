# Enabling the SQLite data layer

The SQLite layer (`Assets/Scripts/Data/`) is **opt-in and disabled by default**. This document
explains why, and what you need to do to turn it on.

## Current state

Mechanic Scope has two data layers:

| Layer | Files | Status |
|-------|-------|--------|
| **Phase 1 — JSON** | `Core/PartDatabase.cs`, `Core/ProgressTracker.cs` | **Active.** What the app uses today. No native dependencies. |
| **Phase 2 — SQLite** | `Data/SQLiteDatabase.cs`, `Data/PartRepository.cs`, `Data/ProgressRepository.cs`, `Data/DataManager.cs` | Written, compiles, but has no provider bound. Kept for a future migration. |

With SQLite disabled, `DataManager.Initialize()` logs a notice and returns without creating
repositories. `DataManager.Parts` and `DataManager.Progress` stay `null`, which is exactly the
condition the UI already checks before falling back to the JSON store — so the app runs normally.

## Why it is disabled

The wrapper was originally written against `Mono.Data.Sqlite`. That type ships only with Unity's
legacy **.NET Framework** API compatibility level. This project targets **.NET Standard**
(`ProjectSettings/ProjectSettings.asset` → `apiCompatibilityLevel: 6`) with IL2CPP and managed
stripping on iOS and Android, where `Mono.Data.Sqlite` does not exist.

Because every script under `Assets/Scripts` belongs to one assembly (`MechanicScope.asmdef`), an
unresolvable type in that one file failed the compilation of **the entire assembly** — the app would
not build at all. The reference now lives behind the `MECHANICSCOPE_SQLITE` compile symbol, so the
default project compiles cleanly.

Everything else in the wrapper is written against the ADO.NET base types in `System.Data.Common`,
which *are* part of .NET Standard. Only `SQLiteDatabase.CreateConnection()` is conditional.

## Enabling it

You need two things: a provider assembly, and the native SQLite binaries for each target platform.

### 1. Choose a provider

| Option | Notes |
|--------|-------|
| **`sqlite-net` + `SQLitePCLRaw`** | Most common Unity choice. Ships native `sqlite3` binaries for iOS, Android, macOS and Windows. Works under IL2CPP. |
| **`Microsoft.Data.Sqlite`** | Also builds on SQLitePCLRaw. Heavier, but a standard ADO.NET provider, so it drops straight into `CreateConnection()`. |
| **`Mono.Data.Sqlite`** | Only if you switch API Compatibility Level to *.NET Framework*. Not recommended: it is unsupported on IL2CPP/mobile and increases build size. |

Install via [NuGetForUnity](https://github.com/GlitchEnzo/NuGetForUnity) or by placing the managed
DLLs under `Assets/Plugins/` and the native libraries under the matching
`Assets/Plugins/Android/libs/<abi>/` and `Assets/Plugins/iOS/` folders.

### 2. Point `CreateConnection()` at it

Edit the guarded branch in `Assets/Scripts/Data/SQLiteDatabase.cs`:

```csharp
private static DbConnection CreateConnection(string connectionString)
{
#if MECHANICSCOPE_SQLITE
    return new Microsoft.Data.Sqlite.SqliteConnection(connectionString);
#else
    throw new NotSupportedException(...);
#endif
}
```

Note that the connection string format differs between providers. The current string is
`URI=file:{path}` (the Mono.Data.Sqlite form); `Microsoft.Data.Sqlite` expects
`Data Source={path}`. Update it to match whichever provider you bind.

### 3. Define the compile symbol

**Edit → Project Settings → Player → Other Settings → Scripting Define Symbols**, add:

```
MECHANICSCOPE_SQLITE
```

Add it for every platform you build (it is set per build target).

### 4. Verify

Run the test suite — it compiles the data layer either way:

```bash
./run_tests.sh
```

Then run the app and confirm the log line `DataManager initialized successfully` appears instead of
the "SQLite layer is not enabled" notice.

## Migration note

The two layers are not synchronised. Turning SQLite on gives you empty repositories; it does not
import existing JSON progress from `ProgressTracker`. Write a one-time migration before switching
the app over to the SQLite store, or users will appear to lose their repair history.
