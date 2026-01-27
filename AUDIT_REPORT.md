# Software Audit Report: Mechanic-Scope

**Date:** January 27, 2026
**Auditor:** Claude (Automated Code Audit)
**Version:** 0.3.0 (Phase 3 - Polish)
**Scope:** Correctness, security, and fitness for purpose

---

## Executive Summary

Mechanic-Scope is an AR-powered engine repair assistant for DIY mechanics. The software demonstrates a well-architected foundation with proper separation of concerns, event-driven communication, and security considerations. However, several issues were identified that affect correctness and fitness for purpose.

**Overall Assessment:** The codebase is **structurally sound** but has **functionality gaps** that prevent it from fully meeting its stated purpose.

| Category | Rating | Notes |
|----------|--------|-------|
| Architecture | Good | Clean separation, event-driven design |
| Code Quality | Good | Consistent style, proper documentation |
| Security | Good | Path traversal protection, parameterized SQL |
| Test Coverage | Moderate | Unit tests exist but integration coverage is limited |
| Fitness for Purpose | Needs Work | Core AR/model loading features are placeholders |

---

## Critical Findings

### 1. Model Loading is Placeholder-Only (CRITICAL)

**Files:** `Assets/Scripts/Core/EngineModelLoader.cs:199-243`

The GLB and OBJ model loading methods contain only placeholder implementations that create primitive cubes instead of actual 3D models:

```csharp
private IEnumerator LoadGLBModel(string path, EngineManifest manifest)
{
    // Note: Actual GLB loading requires GLTFUtility or similar plugin
    // This is a placeholder that creates a simple cube as demonstration
    Debug.Log($"Loading GLB model from: {path}");

    // In production, use GLTFUtility:
    // GameObject model = Siccity.GLTFUtility.Importer.LoadFromFile(path);

    // Placeholder: Create a simple object structure for testing
    GameObject model = CreatePlaceholderModel(manifest);  // <-- Always called
    ...
}
```

**Impact:** The core AR visualization feature does not function. Users cannot view actual engine models.

**Recommendation:** Integrate GLTFUtility or TriLib for actual model loading before release.

---

### 2. Repair Start Time is Hardcoded (HIGH)

**File:** `Assets/Scripts/UI/MainUIController.cs:323`

```csharp
RepairLog log = new RepairLog
{
    ProcedureId = procedureRunner.CurrentProcedure.id,
    EngineName = modelLoader?.CurrentEngine?.name ?? "Unknown",
    StartedAt = DateTime.Now.AddHours(-1), // Approximate  <-- HARDCODED
    CompletedAt = DateTime.Now,
    Notes = ""
};
```

**Impact:** Repair history duration metrics are inaccurate (always ~1 hour regardless of actual time).

**Recommendation:** Track actual start time when procedure is loaded in `OnProcedureLoaded`.

---

### 3. DateTime Parsing Without Error Handling (MEDIUM)

**File:** `Assets/Scripts/Core/ProgressTracker.cs:181-190`

```csharp
return entries
    .OrderByDescending(e => e.completedAt)
    .Select(e => new RepairLog
    {
        ...
        StartedAt = DateTime.Parse(e.startedAt),  // Can throw FormatException
        CompletedAt = DateTime.Parse(e.completedAt),  // Can throw FormatException
        ...
    })
    .ToList();
```

**Impact:** Corrupted or manually edited progress files will crash the app.

**Recommendation:** Use `DateTime.TryParse` with fallback values, as done in `ProgressRepository.cs:263`.

---

## Correctness Issues

### 4. Procedure Completion Check May Fire Prematurely (MEDIUM)

**File:** `Assets/Scripts/Core/ProcedureRunner.cs:272-276`

```csharp
if (CurrentProcedure.steps != null && completedStepIds.Count >= CurrentProcedure.steps.Length)
{
    OnProcedureCompleted?.Invoke();
}
```

**Issue:** Uses count comparison without verifying all specific step IDs are completed. If step IDs are non-sequential (e.g., 1, 2, 5, 10), this could incorrectly mark procedures complete.

**Recommendation:** Change to explicit check:
```csharp
if (CurrentProcedure.steps.All(s => completedStepIds.Contains(s.id)))
```

---

### 5. Static Raycast List Potential Concurrency Issue (LOW)

**File:** `Assets/Scripts/Core/ARAlignment.cs:55`

```csharp
private static readonly List<ARRaycastHit> raycastHits = new List<ARRaycastHit>();
```

**Issue:** Static list shared across all instances could cause issues in edge cases.

**Impact:** Low - typically only one ARAlignment instance exists.

---

### 6. Touch Tap Detection Uses Magic Number (LOW)

**File:** `Assets/Scripts/Core/ARAlignment.cs:404`

```csharp
if (touch.phase == TouchPhase.Ended && touch.deltaTime < 0.2f)
```

**Recommendation:** Make tap duration threshold configurable via SerializeField.

---

### 7. Voice Command Registration Uses Object Array (LOW)

**File:** `Assets/Scripts/Voice/VoiceCommandManager.cs:220-249`

```csharp
public void RegisterCommand(params object[] args)
{
    List<string> phrases = new List<string>();
    Action action = null;
    string description = "";

    foreach (var arg in args)
    {
        if (arg is string s)
        ...
```

**Issue:** Type-unsafe API prone to runtime errors. No compile-time checking.

**Recommendation:** Use strongly-typed overloads or a builder pattern.

---

## Security Analysis

### Positive Security Findings

1. **Path Traversal Protection** (`EngineModelLoader.cs:508-530`)
   - Engine IDs are validated against path traversal patterns (`..`, `/`, `\`)
   - Invalid characters are checked before file operations

2. **Parameterized SQL Queries** (`PartRepository.cs`, `ProgressRepository.cs`)
   - All database queries use parameterized statements
   - No SQL injection vulnerabilities detected

3. **Input Validation**
   - JSON parsing is wrapped in try-catch blocks
   - Null checks throughout the codebase

### Security Recommendations

1. **Add Database Encryption**
   - Parts and progress databases contain user data
   - Consider SQLCipher for sensitive installations

2. **Voice Command Confirmation**
   - Destructive operations (reset, delete) should require confirmation
   - Current implementation executes immediately on recognition

---

## Test Coverage Analysis

### Current Test Suite

| Test File | Tests | Coverage Area |
|-----------|-------|--------------|
| `ProcedureRunnerTests.cs` | 10 | Procedure loading, step dependencies |
| `EngineModelTests.cs` | ~5 | Engine manifest parsing |
| `PartDatabaseTests.cs` | ~8 | Part storage and retrieval |
| `ProgressTrackerTests.cs` | ~6 | Progress persistence |
| `VoiceCommandTests.cs` | ~8 | Command recognition |
| `EndToEndTests.cs` | 12 | Integration workflows |
| `AccessibilityTests.cs` | ~3 | Accessibility features |
| `PerformanceTests.cs` | ~3 | Performance metrics |

### Test Coverage Gaps

1. **AR Alignment** - No tests for touch gesture handling
2. **Model Loading** - No tests for actual GLB/OBJ loading (only placeholders)
3. **Error Recovery** - Limited tests for corrupted data scenarios
4. **Edge Cases** - Circular dependencies in procedures not tested
5. **UI State Machine** - MainUIController transitions not tested

### Test Quality Issues

- `EndToEndTests.cs` uses separate `TestProcedure` class instead of actual `Procedure` class
- Some tests use different field names (`dependencies` vs `requires`) than production code
- Integration tests don't test actual AR functionality

---

## Fitness for Purpose Assessment

### Stated Purpose
> AR-powered engine repair assistant that overlays part identification and procedural guidance onto a live camera view of an engine.

### Feature Completeness

| Feature | Status | Notes |
|---------|--------|-------|
| AR session management | Partial | Framework present, but limited tracking |
| 3D model overlay | Not Functional | Only placeholders rendered |
| Part identification via tap | Functional | Raycast-based detection works |
| Procedure step sequencing | Functional | Dependency resolution works |
| Progress persistence | Functional | JSON and SQLite implementations |
| Voice commands | Functional | Platform abstraction in place |
| Offline operation | Functional | Local-first architecture |
| Multi-engine support | Functional | Engine discovery and loading |

### Key Gaps Preventing Release

1. **Model Loading**: Cannot display actual 3D engine models
2. **AR Tracking Feedback**: No visual indicators for poor tracking
3. **Error Recovery UI**: Errors logged but not displayed to users
4. **Onboarding**: No first-run tutorial or help system

---

## Architectural Strengths

1. **Clean Separation of Concerns**
   - Core, Data, UI, Voice, Performance layers are well-separated
   - Components communicate via events, reducing coupling

2. **Dual Persistence Strategy**
   - Phase 1 JSON storage works immediately
   - Phase 2 SQLite migration path prepared with full FTS5 search

3. **Platform Abstraction**
   - `IVoiceRecognizer` interface allows platform-specific implementations
   - AR Foundation abstracts ARCore/ARKit differences

4. **Event-Driven Architecture**
   - Components subscribe to events rather than polling
   - Loose coupling enables independent testing

---

## Recommendations Summary

### Must Fix Before Release (P0)

1. Implement actual GLB/OBJ model loading
2. Track actual repair start time
3. Add DateTime parsing error handling

### Should Fix (P1)

4. Add procedure step ID uniqueness validation
5. Improve test coverage for AR and model loading
6. Add user-facing error messages
7. Add onboarding/tutorial flow

### Nice to Have (P2)

8. Make tap threshold configurable
9. Add database encryption option
10. Add voice command confirmation for destructive actions
11. Refactor voice command registration to type-safe API

---

## Files Reviewed

```
Assets/Scripts/Core/
  - ProcedureRunner.cs (577 lines)
  - ARAlignment.cs (491 lines)
  - EngineModelLoader.cs (584 lines)
  - PartDatabase.cs (405 lines)
  - ProgressTracker.cs (385 lines)
  - AppInitializer.cs (341 lines)

Assets/Scripts/Data/
  - DataManager.cs (250 lines)
  - PartRepository.cs (480 lines)
  - ProgressRepository.cs (476 lines)

Assets/Scripts/UI/
  - MainUIController.cs (426 lines)

Assets/Scripts/Voice/
  - VoiceCommandManager.cs (554 lines)

Assets/Tests/Runtime/
  - ProcedureRunnerTests.cs (192 lines)
  - EndToEndTests.cs (399 lines)
```

---

## Conclusion

Mechanic-Scope demonstrates solid software engineering fundamentals with a well-organized codebase, appropriate use of design patterns, and good security practices. The architecture supports the stated goals of an offline-first AR repair assistant.

However, the **critical gap** is that the core differentiating feature (3D model visualization) is not implemented. The model loading code only creates placeholder cubes, meaning the AR experience cannot function as intended.

**Recommendation:** Address the critical model loading gap and the high-priority datetime issues before any public release. The remaining issues are lower priority but should be tracked in a backlog.

---

*This audit was conducted through static code analysis. Runtime testing on actual devices may reveal additional issues.*
