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
- **Voice Commands**: Hands-free control with on-device speech recognition
  - "Next step" / "Done" - complete current step
  - "Go back" - undo last step
  - "What is this" - part identification mode
  - "Show/hide details" - toggle step card
  - Platform-specific recognizers (iOS SFSpeechRecognizer, Android SpeechRecognizer)
  - Audio feedback for command recognition
- **Procedure Editor**: Desktop tool for authoring procedures
  - Visual dependency graph editor
  - Step validation and error checking
  - Media attachment support
  - Export/import functionality
- **Step Media Support**: Images and videos in procedure steps
  - Inline image display in step cards
  - Video playback support
  - Media loader with caching
- **Procedure Sharing**: Export/import procedures for community sharing
  - JSON export with media bundling
  - Import validation and conflict resolution
- **Alignment Assistance**: Visual guides for model alignment
  - Alignment confidence indicator
  - Guide overlays for positioning
- **Accessibility Features**
  - Screen reader support (VoiceOver/TalkBack)
  - Adjustable font sizes
  - High contrast mode
  - Accessible button and text components
- **Performance Optimization**
  - LOD (Level of Detail) manager for complex models
  - Asset optimizer for mobile performance
  - Performance monitoring and metrics
- **App Store Preparation**
  - Build configuration management
  - Screenshot capture utility
  - App store metadata configuration
- **Comprehensive Test Suite**
  - End-to-end integration tests
  - Accessibility tests
  - Performance benchmarks
  - Voice command tests

### Changed
- Improved UI responsiveness on lower-end devices
- Enhanced part highlighting shader for better visibility
- Optimized model loading pipeline

### Fixed
- Memory leak when switching between engine models
- Touch gesture conflicts during alignment
- Progress not saving on app background

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
- **3D Model Loading**
  - GLB format support (via GLTFUtility)
  - FBX and OBJ format support
  - Runtime model import
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
| 0.3.0 | 2026-01-17 | Phase 3: Polish & Advanced Features |
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
