# Mechanic Scope

AR-powered engine repair assistant for mobile devices (iOS/Android). Overlays 3D engine models on live camera feed and provides guided repair procedures with dependency-aware step sequencing.

## Tech Stack

- **Engine:** Unity 2022.3 LTS
- **AR:** AR Foundation 5.x + ARCore/ARKit
- **Rendering:** Universal Render Pipeline (URP) 14.x
- **Language:** C# (.NET Standard 2.1)
- **Database:** SQLite 3.x (local persistence)
- **3D Models:** GLB/glTF via glTFast (com.unity.cloud.gltfast)
- **Data Format:** JSON for procedures and configuration

## Project Structure

```
Assets/
├── Scripts/
│   ├── Core/           # AR alignment, procedure runner, part database, progress tracking
│   ├── Data/           # SQLite wrapper, repositories (PartRepository, ProgressRepository)
│   ├── UI/             # MainUIController, selection screens, procedure cards, popups
│   ├── Voice/          # Voice command manager, platform-specific recognizers
│   ├── Accessibility/  # Screen reader support, high contrast, large touch targets
│   ├── Performance/    # LOD manager, asset optimizer, performance monitor
│   └── Editor/         # Procedure editor window (Unity Editor tools)
├── Shaders/            # PartHighlight.shader for component highlighting
├── Tests/
│   └── EditMode/       # Edit mode tests (ProcedureRunner, PartDatabase, EngineModelLoader,
│                       # plus validation of the shipped engine/procedure data)
├── Resources/          # DefaultPartsData.json
└── StreamingAssets/
    └── Engines/        # Engine models and procedures (e.g., gm_ls_gen4/)

Tools/
└── HeadlessTests/      # Runs Assets/Tests/EditMode via `dotnet test`, no Unity install needed
```

## Key Files

| File | Purpose |
|------|---------|
| `Scripts/Core/AppInitializer.cs` | App startup, singleton initialization |
| `Scripts/Core/ARAlignment.cs` | AR session management, model alignment |
| `Scripts/Core/ProcedureRunner.cs` | Procedure loading and step sequencing |
| `Scripts/Core/PartDatabase.cs` | Part information lookup |
| `Scripts/Core/ProgressTracker.cs` | Session progress and history |
| `Scripts/Data/DataManager.cs` | Singleton data coordinator |
| `Scripts/UI/MainUIController.cs` | UI orchestration |

## Commands

```bash
# Run the edit-mode suite headlessly (needs the .NET 8 SDK, not Unity).
# Compiles the real Core sources + real tests against a UnityEngine shim in Tools/HeadlessTests.
# Exits non-zero if anything fails to compile or any test fails.
./run_tests.sh

# In Unity (authoritative for rendering/AR/coroutine behaviour):
#   Window > General > Test Runner > EditMode > Run All
```

Tests live in `Assets/Tests/EditMode` and are picked up automatically by both runners.
See `Tools/HeadlessTests/README.md` for what the shim does and does not cover.

## Coding Conventions

- **Classes/Methods:** PascalCase (`ARAlignment`, `LoadProcedure()`)
- **Private fields:** camelCase (`currentState`, `procedureCache`)
- **Constants:** UPPER_SNAKE_CASE
- **Braces:** Allman style (opening brace on new line)
- **Indentation:** 4 spaces
- **Line length:** 120 characters max
- **Documentation:** XML doc comments for public APIs

## Design Patterns

- **Singleton:** `DataManager.Instance`, `AppInitializer`
- **Repository:** `PartRepository`, `ProgressRepository` for data access
- **Event-Driven:** C# events (`OnStepCompleted`, `OnTrackingStateChanged`)
- **State Machine:** `ARAlignment.AlignmentState` enum

## Unity Conventions

- Use `[SerializeField]` for Inspector-exposed private fields
- Cache component references in `Awake()` or `Start()`
- Prefer `async/await` or coroutines for async operations
- Subscribe to events in `Start()`, unsubscribe in `OnDestroy()`

## Core Data Structures

```csharp
// Procedure (loaded from JSON)
public class Procedure {
    public string id, name, description, engineId;
    public string estimatedTime, difficulty;
    public string[] tools;
    public ProcedureStep[] steps;
}

// Procedure step with dependencies
public class ProcedureStep {
    public int id;
    public string action, details, partId;
    public int[] requires;  // Step IDs that must complete first
    public string[] tools, warnings;
    public TorqueSpec torqueSpec;
    public StepMedia media;
}

// AR alignment states
public enum AlignmentState { Uninitialized, Loading, Aligning, Locked, Paused }
public enum AppMode { ModelSelection, Alignment, ProcedureSelection, ProcedureActive, PartInspection }
```

## Engine/Procedure File Formats

Engine manifest (`engine.json`):
```json
{
    "id": "gm_ls_gen4",
    "name": "GM LS Gen IV",
    "modelFile": "gm_ls_gen4.glb",
    "partMappings": [{ "nodeNameInModel": "Alternator_Mesh", "partId": "alternator_gm_ls" }]
}
```

Procedure file (`procedures/*.json`):
```json
{
    "id": "oil_change",
    "name": "Oil and Filter Change",
    "engineId": "gm_ls_gen4",
    "difficulty": "beginner",
    "steps": [
        { "id": 1, "action": "Position drain pan", "partId": "oil_pan", "requires": [] },
        { "id": 2, "action": "Remove drain plug", "requires": [1], "torqueSpec": { "value": 18, "unit": "ft-lbs" } }
    ]
}
```

## Important Notes

- **Offline-first:** No cloud, no accounts, no analytics - all data stored locally
- **Platforms:** iOS 14+ (ARKit), Android 8.0+ (ARCore)
- **Permissions:** Camera (AR), Microphone (voice), Storage (imports)
- **Current version:** 0.3.0 (see Development Status below for what's working vs. stubbed)

## Development Status

| Module | Status | Notes |
|--------|--------|-------|
| AR alignment & touch controls | Working | Production-quality gesture handling |
| Procedure runner (dependency resolution) | Working | Full graph-based step sequencing |
| Part highlighting shader | Working | URP Fresnel + outline + pulse animation |
| Performance monitor | Working | FPS, memory, battery tracking |
| Accessibility (haptics, text scaling) | Working | Native iOS/Android haptics |
| SQLite database & repositories | **Opt-in** | Compiles, but no provider is bound. Disabled by default; app uses the JSON layer. See `Docs/SQLITE_SETUP.md` |
| 3D model loading | Real, unexercised | Loads GLB via glTFast; no `.glb` is bundled, so nothing loads out of the box |
| Voice commands | Partial | Command registration/dispatch works. `EditorVoiceRecognizer` only simulates input, and the iOS/Android recognizers `DllImport` a native speech plugin that is not in the repo, so no real recognition happens on any platform |
| LOD manager (mesh simplification) | **Stub** | Returns input unchanged |
| App initializer (AR systems) | **Stub** | Logs and yields null |

The whole assembly is compile-checked on every `./run_tests.sh` run, so this table describes
runtime completeness, not build health.

## Documentation

- `SPEC_SHEET.md` - Technical specification and architecture
- `Docs/ADDING_ENGINES.md` - Engine model import guide
- `Docs/PROCEDURE_FORMAT.md` - Procedure JSON specification
- `Docs/UNITY_SETUP.md` - Development environment setup
