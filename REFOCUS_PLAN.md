# Mechanic-Scope Refocus Plan

This plan restructures development around one goal: **a single working vertical slice** — open the app, select an engine, load a real 3D model in AR, align it, run a procedure with dependency-aware steps, and complete it. Everything else is secondary.

---

## Current State

| Layer | Status | Files | Notes |
|-------|--------|-------|-------|
| AR alignment | Real | `ARAlignment.cs` (492 lines) | Touch controls, raycasting, state machine — production-quality |
| Procedure engine | Real | `ProcedureRunner.cs` (578 lines) | Dependency resolution, step sequencing — production-quality |
| 3D model loading | **Stub** | `EngineModelLoader.cs` (585 lines) | `LoadGLBModel()` at line 199 creates cubes. This is the blocker. |
| Part database | Real | `PartDatabase.cs` + `PartRepository.cs` | Two implementations (JSON Phase 1, SQLite Phase 2) — both work |
| Progress tracking | Real | `ProgressTracker.cs` + `ProgressRepository.cs` | Two implementations — both work |
| SQLite layer | Real | `SQLiteDatabase.cs`, `DataManager.cs` | Migrations, transactions — production-quality |
| UI navigation | Real | `MainUIController.cs` (429 lines) | 8-mode state machine, event wiring — complete |
| Voice commands | **Broken** | `VoiceCommandManager.cs`, `VoiceRecognizers.cs` | Platform recognizers exist as `#if` stubs with native DLL imports but no native plugin binaries |
| LOD manager | **Stub** | `LODManager.cs` | `SimplifyMesh()` returns input unchanged |
| Accessibility | Real | 4 files | Native haptics, text scaling — works but premature |
| App Store tooling | Real | 3 files | Build configs, screenshots — premature |
| Performance monitor | Real | `PerformanceMonitor.cs` | FPS/memory/battery — works but premature |
| Asset optimizer | Real | `AssetOptimizer.cs` | Texture/mesh optimization — works but no assets to optimize |
| Procedure editor | Real | `ProcedureEditorWindow.cs` | In-editor tool — functional |
| Highlight shader | Real | `PartHighlight.shader` | URP Fresnel + outline + pulse — production-ready |
| Unity project | **Missing** | No `ProjectSettings/`, `Packages/manifest.json`, `.meta` files | Never opened in Unity |

**Bottom line:** ~60% of the code is real and production-quality. The project is blocked by one critical gap (3D model loading) and one infrastructure gap (no Unity project files).

---

## Phase 0: Project Infrastructure (Do First)

**Goal:** Make this a real Unity project that compiles.

### 0.1 — Create Unity Project Shell

Create a Unity 2022.3 LTS project and copy existing scripts into it, or initialize Unity project files around the existing structure.

**Required artifacts:**
- `ProjectSettings/ProjectSettings.asset` — URP, ARCore/ARKit, IL2CPP
- `ProjectSettings/TagManager.asset` — any custom tags/layers
- `ProjectSettings/InputManager.asset` — touch input
- `Packages/manifest.json` — declare all dependencies:
  ```json
  {
    "dependencies": {
      "com.unity.xr.arfoundation": "5.1.0",
      "com.unity.xr.arcore": "5.1.0",
      "com.unity.xr.arkit": "5.1.0",
      "com.unity.render-pipelines.universal": "14.0.8",
      "com.unity.textmeshpro": "3.0.6",
      "com.unity.test-framework": "1.3.9",
      "com.cysharp.unitask": "https://github.com/Cysharp/UniTask.git?path=src/UniTask/Assets/Plugins/UniTask"
    }
  }
  ```
- `.meta` files — auto-generated when Unity imports the project

### 0.2 — Add GLTFUtility

This is the **single most important dependency**. It enables 3D model loading — the core feature.

- Add [GLTFUtility](https://github.com/Siccity/GLTFUtility) via Unity Package Manager or as a folder in `Assets/Plugins/`
- Alternative: [glTFast](https://github.com/atteneder/glTFast) (Unity's official, better maintained)

### 0.3 — Acquire a Real 3D Model

The project bundles a `gm_ls_gen4.glb` reference in `engine.json` but no actual `.glb` file exists.

**Options:**
- Source a GM LS engine model from Sketchfab, TurboSquid, or CGTrader (search "LS engine" or "V8 engine")
- Ensure the model has named meshes matching the 10 part mappings in `engine.json` (Alternator_Mesh, Battery_Terminal_Mesh, etc.)
- If mesh names don't match, update `engine.json` `partMappings[].nodeNameInModel` to match the model's hierarchy

### 0.4 — Verify Compilation

Open in Unity 2022.3 LTS. Fix any compilation errors. Expect:
- Missing assembly references (add `.asmdef` references)
- Platform-specific `#if` blocks may need adjustment
- `Mono.Data.Sqlite` may need explicit plugin import on newer Unity versions

**Exit criteria:** Project compiles with zero errors in Unity Editor.

---

## Phase 1: The Vertical Slice (Core Priority)

**Goal:** One complete workflow end-to-end on a real device.

### 1.1 — Fix EngineModelLoader.cs (The Blocker)

Replace the placeholder at lines 199-223 with real GLTFUtility loading.

**Current code (stub):**
```csharp
private IEnumerator LoadGLBModel(string path, EngineManifest manifest)
{
    // Placeholder: creates cubes
    GameObject model = CreatePlaceholderModel(manifest);
    ...
}
```

**Target code:**
```csharp
private IEnumerator LoadGLBModel(string path, EngineManifest manifest)
{
    GameObject model = null;
    bool loaded = false;

    Siccity.GLTFUtility.Importer.ImportGLBAsync(path, new ImportSettings(), (result, clips) => {
        model = result;
        loaded = true;
    });

    while (!loaded) yield return null;

    if (model == null)
    {
        OnLoadError?.Invoke($"Failed to load GLB from {path}");
        yield break;
    }

    ApplyPartMappings(model, manifest);
    // ... rest of existing setup code
}
```

Also fix `LoadOBJModel()` at lines 226-243 (same pattern) or remove OBJ support and standardize on GLB.

**Key detail:** The existing `ApplyPartMappings()` method already walks the model hierarchy, finds nodes by name, and attaches `PartIdentifier` components. This code is real and doesn't need changes — it just needs a real model to walk.

### 1.2 — Build the Scene

Create a single Unity scene (`MainScene.unity`) with:
- `ARSession` + `ARSessionOrigin` (AR Foundation standard setup)
- `ARCamera` with `ARCameraManager` + `ARCameraBackground`
- `ARRaycastManager` on the session origin
- `MainUIController` GameObject with references to all UI screens
- `AppInitializer` GameObject
- `ARAlignment` component on the AR session origin
- `EngineModelLoader`, `ProcedureRunner`, `PartDatabase`, `ProgressTracker` on manager GameObjects
- Canvas with 8 screen panels (one per AppMode)
- `EventSystem` for touch input

### 1.3 — Wire UI to Core Systems

`MainUIController.cs` already has the event wiring in code (lines 86-154). The task is to assign the serialized field references in the Unity Inspector:

**Required references (from MainUIController):**
```
[SerializeField] ARAlignment arAlignment
[SerializeField] EngineModelLoader modelLoader
[SerializeField] ProcedureRunner procedureRunner
[SerializeField] PartDatabase partDatabase
[SerializeField] ProgressTracker progressTracker
[SerializeField] 8x screen GameObjects
[SerializeField] UI component references
```

### 1.4 — Test the Vertical Slice

**Test on device (Android or iOS):**
1. Launch → Splash → Engine Selection (GM LS Gen IV appears)
2. Select engine → Model loads (real 3D model, not cubes)
3. Touch to rotate/scale/position → Lock alignment
4. Select "Oil Change" procedure
5. Step through 7 steps with dependency enforcement
6. Complete → Summary screen

**Exit criteria:** This flow works on a physical device with a real 3D model visible in AR.

---

## Phase 2: Stabilize What Exists

**Goal:** Make the working slice reliable and testable.

### 2.1 — Write Real Tests

Delete all existing mock-only tests. Replace with integration tests that instantiate real components:

**Priority test targets:**
| Component | What to Test |
|-----------|-------------|
| `ProcedureRunner` | Load JSON → resolve dependencies → complete steps in order → block invalid steps → fire events |
| `PartDatabase` | Import JSON → search by name → filter by engine → filter by category |
| `ProgressTracker` | Save progress → reload → verify steps → clear → verify empty |
| `EngineModelLoader` | Load manifest → validate part mappings → error on missing file |
| `MainUIController` | Mode transitions → event flow → back navigation |

**Test approach:** Use Unity Test Framework's play-mode tests. Instantiate real MonoBehaviours on test GameObjects. No mocks except for AR hardware.

### 2.2 — Consolidate Data Layer

The codebase has two parallel persistence systems:
- **Phase 1:** `PartDatabase.cs` + `ProgressTracker.cs` (JSON files)
- **Phase 2:** `PartRepository.cs` + `ProgressRepository.cs` (SQLite via `DataManager.cs`)

**Decision:** Pick one and delete the other. Recommendation: **keep SQLite (Phase 2)**. It's more robust, has migrations, FTS5 search, and is already fully implemented. Then:

1. Update `ProcedureRunner` to use `DataManager.Instance.Progress` instead of `ProgressTracker`
2. Update part lookups to use `DataManager.Instance.Parts` instead of `PartDatabase`
3. Delete `PartDatabase.cs` and `ProgressTracker.cs`
4. Update `MainUIController` references accordingly

### 2.3 — Clean Up AppInitializer.cs

Current init methods are empty stubs. Replace with real initialization:

```csharp
private IEnumerator InitializeARSystems()
{
    // Check ARSession.state, wait for ready
    while (ARSession.state < ARSessionState.Ready)
        yield return null;
}
```

Wire it to actually call `DataManager.Instance.Initialize()` and verify the database is ready before transitioning out of splash.

---

## Phase 3: Selective Feature Re-enablement

**Goal:** Add back features that directly support the core workflow. One at a time. Each must be tested before moving on.

### 3.1 — Part Highlighting (High Value, Low Effort)

Already have `PartHighlight.shader` and `HighlightController.cs`. Wire them:
- `ProcedureRunner.OnHighlightPartsChanged` → `EngineModelLoader.HighlightParts()` (this wiring exists in code)
- Assign highlight material to `EngineModelLoader.highlightMaterial` in Inspector
- Verify: tap a part in AR → `PartInfoPopup` shows part details

### 3.2 — Repair History and Progress Resume (High Value, Low Effort)

Already fully implemented in `ProgressRepository`. Wire:
- On procedure complete → `ProgressRepository.LogCompletedRepair()`
- On app reopen → `ProgressRepository.LoadProgress()` → resume from last step
- Show progress percentage on `ProcedureSelectionUI` cards

### 3.3 — Voice Commands (High Value, High Effort)

The `VoiceCommandManager` and command registration are real. The gap is native speech recognition.

**Pragmatic path:**
- **For Editor testing:** `EditorVoiceRecognizer` already works (keyboard-simulated)
- **For Android:** The `AndroidVoiceRecognizer` in `VoiceRecognizers.cs` uses `android.speech.SpeechRecognizer` via JNI — this should work if the native Android SpeechRecognizer API is available. Test on device.
- **For iOS:** The `IOSVoiceRecognizer` declares `DllImport` for native functions (`_StartSpeechRecognition`, etc.). This requires a native iOS plugin (`.mm` file implementing the Speech framework bridge). Write or source one.

**If too much effort:** Defer voice entirely. The app works without it — it's a hands-free convenience, not a requirement.

### 3.4 — Accessibility (Medium Value, Already Done)

`AccessibilityManager.cs` is real. Re-enable after the core slice works:
- Text scaling via `AccessibleText` components
- High contrast mode for outdoor/bright conditions (relevant for mechanics)
- Haptic feedback on step completion

---

## What to Cut (Permanently or Indefinitely)

### Cut Now — Remove from Codebase

| File/Module | Reason |
|---|---|
| `Assets/Scripts/Performance/LODManager.cs` | `SimplifyMesh()` returns input unchanged. Dead code. If LOD is needed later, use Unity's built-in LOD Group with pre-authored LOD meshes instead. |
| `Assets/Scripts/AppStore/` (3 files) | Build configurations, screenshot capture, app store metadata. Premature — no shipping build exists. Re-add when preparing for app store submission. |
| `Assets/Tests/` (all 9 files) | Every test mocks its target. Zero production code is tested. These provide false confidence. Replace with real tests in Phase 2.1. |
| `SECURITY.md` | Vulnerability reporting policy for a project with no users. Re-add at v1.0. |
| `CONTRIBUTING.md` | PR guidelines for a project with no contributors. Re-add when open-sourcing. |

### Defer — Keep in Code, Disable in UI

| File/Module | Reason | Re-enable When |
|---|---|---|
| `Assets/Scripts/Voice/` (3 files) | Needs native plugin work. Don't delete — the manager and command registration are solid. | Native plugins are built or sourced |
| `Assets/Scripts/Accessibility/` (4 files) | Real code, but irrelevant until the app runs. | After Phase 1 vertical slice works on device |
| `Assets/Scripts/Performance/AssetOptimizer.cs` | Real code, but no assets to optimize yet. | Model loading is working and performance profiling shows bottlenecks |
| `Assets/Scripts/Performance/PerformanceMonitor.cs` | Real code, useful for debugging but not user-facing. | During device performance testing |
| `Assets/Scripts/Core/ProcedureSharing.cs` | Export/import is a community feature. | After multiple engines and procedures exist |
| `Assets/Scripts/Editor/ProcedureEditorWindow.cs` | Useful tool, but manual JSON editing works fine. | When non-developers need to author procedures |

---

## Milestone Definitions

### M0: It Compiles (Phase 0)
- Unity project opens without errors
- All scripts compile
- GLTFUtility is importable
- A test `.glb` file loads in a blank scene via `Importer.LoadFromFile()`

### M1: The Vertical Slice (Phase 1)
- On a physical device: select engine → load real 3D model in AR → align → run oil change procedure → complete
- Part tapping works (raycast → `PartInfoPopup`)
- No crashes during the full flow

### M2: It's Testable (Phase 2)
- Real integration tests pass for ProcedureRunner, PartRepository, ProgressRepository
- Single data layer (SQLite only)
- AppInitializer performs real initialization

### M3: It's Useful (Phase 3)
- Part highlighting shows which part the current step references
- Progress persists across app restarts
- At least 2 engines with 2+ procedures each
- Voice commands work on at least one platform (or consciously deferred)

---

## Effort Estimates by Phase

| Phase | Scope | Depends On |
|-------|-------|------------|
| **Phase 0** | Unity project setup, GLTFUtility integration, source a 3D model | Nothing — start here |
| **Phase 1.1** | Replace placeholder in `EngineModelLoader.cs` (~50 lines of code change) | Phase 0 |
| **Phase 1.2** | Build scene, assign references in Inspector | Phase 0 |
| **Phase 1.3** | Wire UI (references already coded — Inspector work) | Phase 1.1 + 1.2 |
| **Phase 1.4** | Device testing and bug fixes | Phase 1.1-1.3 |
| **Phase 2.1** | Write 15-20 real integration tests | Phase 1 |
| **Phase 2.2** | Delete Phase 1 data layer, update references (~4 files) | Phase 1 |
| **Phase 2.3** | Fix AppInitializer (~30 lines) | Phase 1 |
| **Phase 3.1** | Wire highlighting (Inspector + verify) | Phase 1 |
| **Phase 3.2** | Wire progress resume (already coded, verify flow) | Phase 2.2 |
| **Phase 3.3** | Voice: test Android, build iOS plugin or defer | Phase 1 |
| **Phase 3.4** | Re-enable accessibility (Inspector + verify) | Phase 1 |

---

## Decision Log

| Decision | Rationale |
|---|---|
| **GLB over FBX/OBJ** | GLTFUtility is free, well-maintained, and GLB is the standard for runtime 3D on mobile. FBX requires TriLib ($). OBJ lacks animations and PBR materials. |
| **SQLite over JSON files** | Already implemented with migrations, FTS5 search, and transactions. JSON files don't scale and lack query capability. |
| **Cut LODManager** | Mesh simplification at runtime is a solved problem (pre-author LOD meshes in Blender). The stub implementation provides zero value. |
| **Defer voice** | High effort (native plugins), medium value. The app's primary interaction is touch, and voice is additive. |
| **Delete all tests** | Tests that mock everything validate nothing. Starting fresh with real integration tests is faster than fixing mock tests. |
| **One engine first** | Prove the flow works with one engine (GM LS Gen IV) before adding more. The engine import system (`EngineImporter.cs`) already supports adding more later. |

---

## Summary

The project's architecture and most of its code are solid. The problem is not quality — it's sequencing. Peripheral features were built before the core feature works. The refocus is straightforward:

1. **Phase 0:** Make it a real Unity project
2. **Phase 1:** Fix the one placeholder that blocks everything (3D model loading)
3. **Phase 2:** Replace fake tests with real ones, consolidate the data layer
4. **Phase 3:** Re-enable features one at a time, each tested before the next

The codebase does not need a rewrite. It needs a 50-line code change in `EngineModelLoader.cs`, a real `.glb` file, and a Unity project shell. Everything else is wiring.
