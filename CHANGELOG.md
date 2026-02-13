# Changelog

All notable changes to Mechanic Scope are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

---

## [Unreleased]

### Planned
- ML-based part recognition (YOLOv8)
- OBD-II Bluetooth integration
- Multi-device progress sync
- Community procedure marketplace

---

## [0.3.0] - 2026-01-17

### Added
- **Voice Commands** *(scaffolded — not yet functional)*: Command manager and recognizer interfaces exist, but platform-specific recognizer classes (iOS/Android) are not implemented. See `Assets/Scripts/Voice/`.
- **Procedure Editor**: Unity Editor window for authoring procedures (`Window > MechanicScope > Procedure Editor`)
  - Step editing and validation
  - Basic dependency configuration
- **Step Media Support** *(scaffolded)*: Data model and media loader utility exist but UI display is not wired up
- **Accessibility Features**
  - Screen reader support (VoiceOver/TalkBack)
  - Adjustable font sizes
  - High contrast mode
  - Native haptics (iOS DllImport, Android JNI)
- **Performance Monitoring**
  - FPS, memory, and battery tracking via Unity Profiler APIs
  - LOD manager *(mesh simplification is a stub — returns input unchanged)*
  - Asset optimizer
- **Test Suite** *(mock-based only)*
  - Tests for procedure runner, part database, voice commands, performance, accessibility
  - All tests use mock objects; no integration tests against real Unity components

### Changed
- Enhanced part highlighting shader (Fresnel + outline + pulse animation)

### Known Issues
- Voice command recognizers reference classes that don't exist — feature is non-functional
- LOD `SimplifyMesh()` is a no-op placeholder
- Test suite tests mock objects, not production code paths

---

## [0.2.0] - 2026-01-15

### Added
- **Dependency-Aware Procedure Engine**
  - Graph-based step resolution
  - Parallel step support
  - Automatic step unlocking based on dependencies
- **SQLite Integration**
  - Parts database with full-text search
  - Progress persistence across sessions
  - Repair history logging
- **Engine Model Library**
  - Import custom engine models
  - Engine selection screen
  - Delete and manage imported models
- **Procedure Selection Screen**
  - Browse available procedures per engine
  - Difficulty and time estimates
  - Procedure search and filtering
- **Part Highlighting**
  - Shader-based highlighting for active parts
  - Pulsing animation for current step parts
  - Configurable highlight colors
- **Repair History**
  - Log completed repairs with timestamps
  - Add notes to completed repairs
  - View repair history per engine
- **Settings Screen**
  - User preferences storage
  - Highlight color customization
  - Voice command toggle
- **Alignment Persistence**
  - Save alignment per engine model
  - Restore previous alignment on load

### Changed
- Procedure display now shows dependency graph visualization
- Improved part database query performance
- Enhanced model alignment accuracy

### Fixed
- Steps not unlocking when dependencies completed
- Database migration issues on app update
- Incorrect progress percentage calculation

---

## [0.1.0] - 2026-01-13

### Added
- **AR Foundation Integration**
  - AR session management
  - Camera feed with model overlay
  - Pause/resume AR tracking
- **3D Model Loading** *(placeholder — loads cubes instead of real models)*
  - glTFast package integrated for GLB/glTF support
  - Loader scaffolded but falls back to placeholder geometry when no `.glb` file is present
- **Manual Alignment Controls**
  - Single finger drag to rotate
  - Two finger pinch to scale
  - Two finger drag to translate
  - Alignment lock/unlock
- **Part Detection**
  - Mesh collider generation
  - Tap-to-identify via raycasting
  - Part name display popup
- **JSON Procedure Loading**
  - Procedure file parsing
  - Step data extraction
  - Tool and warning display
- **Linear Procedure Display**
  - Step-by-step UI cards
  - Mark step complete
  - Progress indicator
- **Basic Part Database**
  - Hardcoded part data
  - Part specifications display
  - Cross-reference information
- **Sample Engine Data**
  - GM LS Gen IV sample model
  - Oil change procedure
  - Alternator replacement procedure
- **Project Documentation**
  - README with feature overview
  - Technical specification sheet
  - Unity setup guide

### Technical
- Unity 2022.3 LTS project setup
- AR Foundation 5.x integration
- Universal Render Pipeline configuration
- TextMeshPro UI components
- Assembly definition files

---

## [0.0.1] - 2026-01-10

### Added
- Initial project structure
- MIT License
- Basic README

---

## Version History Summary

| Version | Date | Milestone |
|---------|------|-----------|
| 0.3.0 | 2026-01-17 | Phase 3: Polish & Advanced Features (partial — see known issues) |
| 0.2.0 | 2026-01-15 | Phase 2: Core Experience |
| 0.1.0 | 2026-01-13 | Phase 1: Foundation |
| 0.0.1 | 2026-01-10 | Initial Commit |

---

## Release Notes Format

Each release includes:
- **Added**: New features
- **Changed**: Changes to existing functionality
- **Deprecated**: Features to be removed in future versions
- **Removed**: Features removed in this version
- **Fixed**: Bug fixes
- **Security**: Security-related changes

---

[Unreleased]: https://github.com/kase1111-hash/Mechanic-Scope/compare/v0.3.0...HEAD
[0.3.0]: https://github.com/kase1111-hash/Mechanic-Scope/compare/v0.2.0...v0.3.0
[0.2.0]: https://github.com/kase1111-hash/Mechanic-Scope/compare/v0.1.0...v0.2.0
[0.1.0]: https://github.com/kase1111-hash/Mechanic-Scope/compare/v0.0.1...v0.1.0
[0.0.1]: https://github.com/kase1111-hash/Mechanic-Scope/releases/tag/v0.0.1
