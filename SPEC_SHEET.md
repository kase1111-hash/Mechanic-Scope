# Mechanic Scope - Technical Specification Sheet

## Overview

**Project:** AR-Powered Engine Repair Assistant
**Platform:** iOS / Android (Unity + AR Foundation)
**Architecture:** Local-first, offline-capable mobile application
**License:** MIT

---

## 1. System Requirements

### 1.1 Target Devices
| Platform | Minimum Requirements |
|----------|---------------------|
| iOS | iPhone 8+ with ARKit support, iOS 14+ |
| Android | ARCore-compatible device, Android 8.0+ |
| RAM | 3GB minimum, 4GB recommended |
| Storage | 500MB base app + user models |

### 1.2 Development Environment
| Tool | Version | Purpose |
|------|---------|---------|
| Unity | 2022.3 LTS | Game engine / AR runtime |
| AR Foundation | 5.x | Cross-platform AR abstraction |
| Universal Render Pipeline (URP) | 14.x | Mobile-optimized rendering |
| SQLite | 3.x | Local database |
| C# | .NET Standard 2.1 | Application logic |

---

## 2. Core Components

### 2.1 AR Layer (`ARAlignment.cs`)

**Responsibility:** Handle camera feed, AR session management, and 3D model alignment to real-world engine.

#### Public Interface
```csharp
public class ARAlignment : MonoBehaviour
{
    // Events
    public event Action<bool> OnTrackingStateChanged;
    public event Action<Pose> OnModelPoseUpdated;

    // Properties
    public bool IsTracking { get; }
    public Pose CurrentModelPose { get; }
    public float AlignmentConfidence { get; }

    // Methods
    public void LoadEngineModel(string modelPath);
    public void SetManualAlignment(Pose pose);
    public void ResetAlignment();
    public void LockAlignment();
    public void UnlockAlignment();
    public Vector3 ScreenToModelPoint(Vector2 screenPosition);
    public string GetPartAtScreenPosition(Vector2 screenPosition);
}
```

#### Implementation Details
- Use AR Foundation's `ARSession` and `ARSessionOrigin`
- Implement manual alignment via touch gestures:
  - **Single finger drag:** Rotate model
  - **Two finger pinch:** Scale model
  - **Two finger drag:** Translate model
- Raycasting for part detection (mesh colliders on model parts)
- Store alignment transform per engine model in PlayerPrefs

#### State Machine
```
States: Uninitialized → Loading → Aligning → Locked → Paused
Transitions:
  - Uninitialized → Loading: LoadEngineModel() called
  - Loading → Aligning: Model loaded successfully
  - Aligning → Locked: LockAlignment() called
  - Locked → Aligning: UnlockAlignment() called
  - Any → Paused: App backgrounded
  - Paused → Previous: App foregrounded
```

---

### 2.2 Procedure Engine (`ProcedureRunner.cs`)

**Responsibility:** Load, parse, and execute procedure graphs with dependency resolution.

#### Public Interface
```csharp
public class ProcedureRunner : MonoBehaviour
{
    // Events
    public event Action<Procedure> OnProcedureLoaded;
    public event Action<ProcedureStep> OnStepActivated;
    public event Action<ProcedureStep> OnStepCompleted;
    public event Action OnProcedureCompleted;

    // Properties
    public Procedure CurrentProcedure { get; }
    public List<ProcedureStep> AvailableSteps { get; }  // Steps with satisfied dependencies
    public List<ProcedureStep> CompletedSteps { get; }
    public float ProgressPercentage { get; }

    // Methods
    public void LoadProcedure(string procedureId, string engineId);
    public void CompleteStep(int stepId);
    public void UncompleteStep(int stepId);
    public void ResetProcedure();
    public List<string> GetHighlightedParts();  // Parts involved in available steps
}
```

#### Dependency Resolution Algorithm
```
function GetAvailableSteps(procedure, completedStepIds):
    availableSteps = []
    for step in procedure.steps:
        if step.id in completedStepIds:
            continue
        if step.requires is empty:
            availableSteps.add(step)
        else if all(reqId in completedStepIds for reqId in step.requires):
            availableSteps.add(step)
    return availableSteps
```

---

### 2.3 Part Database (`PartDatabase.cs`)

**Responsibility:** Store and retrieve part metadata, specifications, and cross-references.

#### Public Interface
```csharp
public class PartDatabase : MonoBehaviour
{
    // Methods
    public PartInfo GetPart(string partId);
    public List<PartInfo> SearchParts(string query);
    public List<PartInfo> GetPartsForEngine(string engineId);
    public void ImportPartData(string jsonPath);
}

public class PartInfo
{
    public string Id { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }
    public string Category { get; set; }  // "electrical", "mechanical", "fluid", etc.
    public Dictionary<string, string> Specs { get; set; }  // "torque": "37 ft-lbs"
    public List<string> CrossReferences { get; set; }  // OEM part numbers
    public string ImagePath { get; set; }
}
```

#### SQLite Schema
```sql
CREATE TABLE parts (
    id TEXT PRIMARY KEY,
    name TEXT NOT NULL,
    description TEXT,
    category TEXT,
    image_path TEXT
);

CREATE TABLE part_specs (
    part_id TEXT,
    spec_key TEXT,
    spec_value TEXT,
    FOREIGN KEY (part_id) REFERENCES parts(id),
    PRIMARY KEY (part_id, spec_key)
);

CREATE TABLE part_cross_refs (
    part_id TEXT,
    ref_type TEXT,  -- "oem", "aftermarket", "interchange"
    ref_value TEXT,
    FOREIGN KEY (part_id) REFERENCES parts(id)
);

CREATE TABLE engine_parts (
    engine_id TEXT,
    part_id TEXT,
    model_node_name TEXT,  -- Name in 3D model hierarchy
    FOREIGN KEY (part_id) REFERENCES parts(id),
    PRIMARY KEY (engine_id, part_id)
);
```

---

### 2.4 Progress Tracker (`ProgressTracker.cs`)

**Responsibility:** Persist procedure progress, repair history, and user preferences.

#### Public Interface
```csharp
public class ProgressTracker : MonoBehaviour
{
    // Events
    public event Action<string, int> OnProgressUpdated;  // procedureId, completedCount

    // Methods
    public void SaveProgress(string procedureId, string engineId, List<int> completedStepIds);
    public List<int> LoadProgress(string procedureId, string engineId);
    public void ClearProgress(string procedureId, string engineId);

    public void LogCompletedRepair(RepairLog log);
    public List<RepairLog> GetRepairHistory(string engineId = null);

    public void SetPreference(string key, string value);
    public string GetPreference(string key, string defaultValue = null);
}

public class RepairLog
{
    public string Id { get; set; }
    public string ProcedureId { get; set; }
    public string EngineName { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime CompletedAt { get; set; }
    public string Notes { get; set; }
}
```

#### SQLite Schema
```sql
CREATE TABLE procedure_progress (
    procedure_id TEXT,
    engine_id TEXT,
    completed_steps TEXT,  -- JSON array of step IDs
    last_updated DATETIME,
    PRIMARY KEY (procedure_id, engine_id)
);

CREATE TABLE repair_history (
    id TEXT PRIMARY KEY,
    procedure_id TEXT,
    engine_name TEXT,
    started_at DATETIME,
    completed_at DATETIME,
    notes TEXT
);

CREATE TABLE preferences (
    key TEXT PRIMARY KEY,
    value TEXT
);
```

---

## 3. Data Models

### 3.1 Engine Model Manifest (`engine.json`)
```json
{
    "id": "gm_ls_gen4",
    "name": "GM LS Gen IV (L76/L77/LS3)",
    "manufacturer": "General Motors",
    "years": "2007-2017",
    "modelFile": "gm_ls_gen4.glb",
    "thumbnail": "gm_ls_gen4_thumb.png",
    "partMappings": [
        {
            "nodeNameInModel": "Alternator_Mesh",
            "partId": "alternator_gm_ls"
        },
        {
            "nodeNameInModel": "BatteryNeg_Mesh",
            "partId": "battery_negative"
        }
    ],
    "defaultAlignment": {
        "position": [0, 0, 0.5],
        "rotation": [0, 180, 0],
        "scale": [1, 1, 1]
    }
}
```

### 3.2 Procedure Graph (`procedure.json`)
```json
{
    "id": "replace_alternator",
    "name": "Replace Alternator",
    "description": "Remove and replace the alternator",
    "engineId": "gm_ls_gen4",
    "estimatedTime": "45-60 minutes",
    "difficulty": "intermediate",
    "tools": [
        "10mm socket",
        "13mm socket",
        "15mm wrench or belt tool",
        "Torque wrench"
    ],
    "steps": [
        {
            "id": 1,
            "action": "Disconnect negative battery terminal",
            "details": "Loosen the 10mm nut and pull the cable off the terminal. Tuck it away from the battery to prevent accidental contact.",
            "partId": "battery_negative",
            "tools": ["10mm socket"],
            "warnings": ["Always disconnect battery first to prevent electrical shorts"],
            "requires": [],
            "media": {
                "image": "step1_battery.png",
                "video": null
            }
        },
        {
            "id": 2,
            "action": "Remove serpentine belt",
            "details": "Use a 15mm wrench on the tensioner pulley. Rotate clockwise to release tension, then slip the belt off the alternator pulley.",
            "partId": "serpentine_belt",
            "tools": ["15mm wrench or belt tool"],
            "warnings": [],
            "requires": [1],
            "media": null
        },
        {
            "id": 3,
            "action": "Disconnect alternator electrical connector",
            "details": "Press the release tab and pull the connector straight off. There's also a small wire with a push-on connector for the charge indicator.",
            "partId": "alternator_connector",
            "tools": [],
            "warnings": [],
            "requires": [1],
            "media": null
        },
        {
            "id": 4,
            "action": "Remove alternator mounting bolts",
            "details": "Remove the two 13mm bolts holding the alternator to the bracket. Support the alternator as you remove the final bolt.",
            "partId": "alternator",
            "tools": ["13mm socket"],
            "warnings": [],
            "requires": [2, 3],
            "torqueSpec": {
                "value": 37,
                "unit": "ft-lbs",
                "note": "Apply on reinstall"
            },
            "media": null
        },
        {
            "id": 5,
            "action": "Remove alternator from engine bay",
            "details": "Lift and maneuver the alternator out. May need to rotate it to clear the radiator hose.",
            "partId": "alternator",
            "tools": [],
            "warnings": [],
            "requires": [4],
            "media": null
        }
    ],
    "reinstallNotes": "Installation is reverse of removal. Ensure belt is properly seated on all pulleys before releasing tensioner."
}
```

### 3.3 UI State Model
```csharp
public enum AppMode
{
    ModelSelection,     // Browsing/importing engine models
    Alignment,          // Aligning 3D model to camera view
    ProcedureSelection, // Choosing a repair procedure
    ProcedureActive,    // Following a procedure
    PartInspection      // Viewing part details
}

public class UIState
{
    public AppMode CurrentMode { get; set; }
    public string SelectedEngineId { get; set; }
    public string SelectedProcedureId { get; set; }
    public string SelectedPartId { get; set; }
    public bool IsVoiceEnabled { get; set; }
    public bool IsOverlayVisible { get; set; }
}
```

---

## 4. User Interface Specifications

### 4.1 Screen Flow
```
┌─────────────┐     ┌─────────────┐     ┌─────────────┐
│   Splash    │────▶│   Engine    │────▶│  Alignment  │
│   Screen    │     │  Selection  │     │    View     │
└─────────────┘     └─────────────┘     └──────┬──────┘
                                               │
                    ┌──────────────────────────┘
                    ▼
            ┌─────────────┐     ┌─────────────┐
            │  Procedure  │────▶│  Procedure  │
            │  Selection  │     │   Active    │
            └─────────────┘     └──────┬──────┘
                                       │
                    ┌──────────────────┤
                    ▼                  ▼
            ┌─────────────┐     ┌─────────────┐
            │    Part     │     │  Completion │
            │  Inspector  │     │   Summary   │
            └─────────────┘     └─────────────┘
```

### 4.2 Main AR View Layout
```
┌────────────────────────────────────────────┐
│ [≡]                              [🎤] [⚙️] │  ← Header (48dp)
├────────────────────────────────────────────┤
│                                            │
│                                            │
│            AR CAMERA FEED                  │
│         + 3D MODEL OVERLAY                 │
│                                            │
│                                            │
│    ╭─────────╮                             │
│    │ Part ID │  ← Tap-to-identify popup    │
│    ╰─────────╯                             │
│                                            │
├────────────────────────────────────────────┤
│ ┌────────────────────────────────────────┐ │
│ │ Step 2 of 5                            │ │  ← Procedure Card
│ │ Remove serpentine belt                 │ │     (collapsible)
│ │ Tools: 15mm wrench                     │ │
│ │                                        │ │
│ │ [Details]           [✓ Mark Complete]  │ │
│ └────────────────────────────────────────┘ │
└────────────────────────────────────────────┘
```

### 4.3 UI Components

#### Procedure Card
- **Collapsed:** Step number, action title, complete button
- **Expanded:** Full details, tools, warnings, torque specs, media
- **Swipe gestures:** Swipe left/right to navigate available steps

#### Part Info Popup
- Appears on tap in AR view
- Shows: Part name, category icon, key specs
- Tap to expand to full Part Inspector screen

#### Step Dependency Visualization
- Show procedure as node graph in overview mode
- Completed steps: Filled circles
- Available steps: Highlighted circles with pulse animation
- Blocked steps: Faded circles with lock icon

### 4.4 Color Palette
| Element | Color | Hex |
|---------|-------|-----|
| Primary (interactive) | Safety Orange | #FF6B35 |
| Secondary | Steel Blue | #4A90A4 |
| Success | Green | #4CAF50 |
| Warning | Amber | #FFC107 |
| Danger | Red | #F44336 |
| Background | Dark Gray | #1E1E1E |
| Surface | Medium Gray | #2D2D2D |
| Text Primary | White | #FFFFFF |
| Text Secondary | Light Gray | #B0B0B0 |

---

## 5. Voice Command Module (Optional)

### 5.1 Supported Commands
| Phrase | Action |
|--------|--------|
| "Next step" / "Done" | Complete current step, advance |
| "What is this" | Enter part identification mode |
| "Go back" | Uncomplete last step |
| "Show details" | Expand current step card |
| "Hide details" | Collapse step card |
| "What tools do I need" | Read current step tools |
| "Read warnings" | Read current step warnings |
| "Stop listening" | Disable voice commands |

### 5.2 Implementation
- Use on-device speech recognition (no network required)
- iOS: `SFSpeechRecognizer`
- Android: `SpeechRecognizer` API
- Unity wrapper: Custom native plugin or Whisper.cpp integration
- Wake word optional (can use always-listening or push-to-talk)

---

## 6. File System Structure

```
Application.persistentDataPath/
├── engines/
│   ├── gm_ls_gen4/
│   │   ├── engine.json          # Engine manifest
│   │   ├── model.glb            # 3D model
│   │   ├── thumbnail.png        # Preview image
│   │   └── procedures/
│   │       ├── replace_alternator.json
│   │       ├── replace_starter.json
│   │       └── ...
│   └── honda_k24/
│       └── ...
├── database/
│   ├── parts.sqlite             # Part information database
│   └── progress.sqlite          # User progress database
├── cache/
│   └── model_cache/             # Processed model data
└── preferences.json             # User settings
```

---

## 7. Implementation Phases

### Phase 1: Foundation (MVP)
**Goal:** Working AR alignment with static model and linear procedure display

#### Tasks
1. [ ] Unity project setup with AR Foundation
2. [ ] Basic AR session management (tracking, pause/resume)
3. [ ] GLB model loader with runtime import
4. [ ] Manual alignment controls (translate, rotate, scale)
5. [ ] Mesh collider generation for part detection
6. [ ] Tap-to-identify part display (raycast → part name)
7. [ ] JSON procedure loader
8. [ ] Linear procedure display UI (ignore dependencies)
9. [ ] Step completion (tap to mark done)
10. [ ] Basic part database with hardcoded data

#### Deliverables
- APK/IPA that loads a model, aligns it, and walks through a procedure
- Single test engine model with 1-2 procedures

### Phase 2: Core Experience
**Goal:** Full dependency graph support, persistence, multiple models

#### Tasks
1. [ ] Dependency resolution in procedure engine
2. [ ] SQLite integration for parts database
3. [ ] Progress persistence (save/load step completion)
4. [ ] Engine model library management (import, delete, select)
5. [ ] Procedure selection screen
6. [ ] Part highlighting (shader-based) for active steps
7. [ ] Repair history logging
8. [ ] Settings screen (preferences)
9. [ ] Alignment persistence per engine model

#### Deliverables
- Feature-complete app with multiple engine support
- Real procedure data for 2-3 engine models

### Phase 3: Polish
**Goal:** Voice commands, editor tool, quality of life improvements

#### Tasks
1. [ ] Voice command integration
2. [ ] Desktop procedure editor (separate Unity project or Electron app)
3. [ ] Step media support (images, optional video)
4. [ ] Procedure export/import for sharing
5. [ ] Improved alignment assistance (visual guides)
6. [ ] Performance optimization
7. [ ] Accessibility improvements
8. [ ] App store preparation

#### Deliverables
- Production-ready app
- Procedure editor for community contribution

---

## 8. Testing Strategy

### 8.1 Unit Tests
- Procedure dependency resolution
- Part database queries
- Progress save/load
- JSON parsing

### 8.2 Integration Tests
- AR session lifecycle
- Model loading pipeline
- Database migrations

### 8.3 Manual Test Cases
| Test Case | Steps | Expected Result |
|-----------|-------|-----------------|
| Model Alignment | Load model, use gestures to align | Model follows finger input smoothly |
| Part Detection | Tap on model part | Correct part name shown |
| Procedure Flow | Complete steps with dependencies | Steps unlock correctly |
| Progress Persistence | Complete steps, close app, reopen | Progress restored |
| Offline Operation | Enable airplane mode, use app | All features work |

---

## 9. Performance Targets

| Metric | Target |
|--------|--------|
| App launch to AR ready | < 3 seconds |
| Model load time (10MB GLB) | < 2 seconds |
| AR frame rate | 60 FPS |
| Part tap response | < 100ms |
| Memory usage | < 500MB |
| Battery drain (active use) | < 15%/hour |

---

## 10. Security & Privacy

- **No network required:** All data stored locally
- **No user accounts:** No PII collected
- **No analytics:** No tracking or telemetry
- **Model data:** User-provided, user-responsible for licensing
- **Permissions required:**
  - Camera (AR functionality)
  - Microphone (voice commands - optional)
  - Storage (model import)

---

## 11. Known Limitations & Future Work

### Current Limitations
- Requires manual model alignment (no automatic recognition)
- No real-time part detection without model
- Single-user (no cloud sync between devices)

### Future Enhancements (Post-MVP)
- ML-based part recognition (YOLOv8 or similar)
- OBD-II Bluetooth integration for diagnostic context
- Multi-device progress sync (optional cloud feature)
- Community procedure marketplace
- Video overlay for step demonstrations

---

## Appendix A: Third-Party Dependencies

| Package | Version | License | Purpose |
|---------|---------|---------|---------|
| AR Foundation | 5.x | Unity EULA | AR abstraction |
| GLTFUtility | Latest | MIT | GLB import |
| SQLite4Unity3d | Latest | Public Domain | SQLite wrapper |
| UniTask | Latest | MIT | Async/await support |
| DOTween | Latest | Free version | UI animations |

---

## Appendix B: Glossary

| Term | Definition |
|------|------------|
| **Procedure** | A complete repair task with ordered steps |
| **Step** | A single action within a procedure |
| **Dependency** | A step that must be completed before another |
| **Part** | A component on the engine (alternator, belt, etc.) |
| **Alignment** | The process of matching the 3D model to the camera view |
| **Overlay** | The 3D model rendered on top of the camera feed |

---

*Spec Version: 1.0*
*Last Updated: 2026-01-17*
