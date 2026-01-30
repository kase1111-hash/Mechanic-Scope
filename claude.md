# Mechanic Scope

AR-powered engine repair assistant for mobile devices (iOS/Android). Overlays 3D engine models on live camera feed and provides guided repair procedures with dependency-aware step sequencing.

## Tech Stack

- **Engine:** Unity 2022.3 LTS
- **AR:** AR Foundation 5.x + ARCore/ARKit
- **Rendering:** Universal Render Pipeline (URP) 14.x
- **Language:** C# (.NET Standard 2.1)
- **Database:** SQLite 3.x (local persistence)
- **Async:** UniTask
- **3D Models:** GLB (preferred), FBX, OBJ via GLTFUtility
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
│   ├── Editor/         # Edit mode tests
│   └── Runtime/        # Play mode tests (Core, Data, Voice, Integration, Performance)
├── Resources/          # DefaultPartsData.json
└── StreamingAssets/
    └── Engines/        # Engine models and procedures (e.g., gm_ls_gen4/)
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
# Run all tests
./run_tests.sh

# In Unity: Window > General > Test Runner
```

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
- Prefer `async/await` with UniTask over coroutines
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
- **Current version:** 0.3.0 (Phase 3 - Polish and Advanced Features complete)

## Documentation

- `SPEC_SHEET.md` - Technical specification and architecture
- `Docs/CONTRIBUTING.md` - Contribution guidelines
- `Docs/ADDING_ENGINES.md` - Engine model import guide
- `Docs/PROCEDURE_FORMAT.md` - Procedure JSON specification
- `SECURITY.md` - Privacy policy and vulnerability reporting
