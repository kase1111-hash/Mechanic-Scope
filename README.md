# Mechanic Scope

**AR-Powered Engine Repair Assistant for Shade Tree Mechanics**

Point your phone at an engine, see what you're looking at, and follow step-by-step removal sequences. No more guessing which bolt comes out first or which connector you forgot to unplug.

---

## What It Does

Mechanic Scope overlays part identification and procedural guidance directly onto a live camera view of your engine. Load a 3D model that matches your engine, align it to your view, and the app walks you through repairs with highlighted parts and sequenced steps.

**Built for the driveway, not the dealership.** This is a tool for home mechanics who want visual confirmation before they start unbolting things.

---

## Before You Start

**Protect your phone.** Engines are greasy. Put your phone in a ziplock bag before you start. The touchscreen still works through the plastic, and you won't ruin a $1000 device with brake fluid.

**You need an engine model.** Mechanic Scope doesn't ship with proprietary CAD data. You'll import a 3D model (.glb, .fbx, .obj) that matches your engine. See [Engine Models](#engine-models) for sources.

**This is guidance, not gospel.** Always verify torque specs, fluid capacities, and safety procedures against your service manual. Mechanic Scope helps you see — it doesn't replace knowing what you're doing.

---

## Core Features

| Feature | Status | Description |
|---------|--------|-------------|
| **Part Overlay** | Working | Tap any visible component to see its name and basic specs |
| **Procedure Mode** | Working | Select a task (e.g., "Replace alternator") and follow highlighted removal steps |
| **Progress Tracking** | Working | Check off steps as you go; pick up where you left off |
| **Offline Operation** | Working | All procedure data stored locally — no cell signal required |
| **Voice Commands** | Planned | Hands-free control (say "next step" or "what is this part") — not yet functional |

> **Note:** 3D model loading currently uses placeholder geometry. Real GLB model rendering via glTFast is implemented but requires a user-supplied `.glb` file to function. See [Development Status](#development-status) for details.

---

## How It Works

1. **Load your engine model** — Select the model matching your vehicle from your imported library
2. **Align the overlay** — Point your camera at the engine and adjust until the 3D model snaps to the real geometry
3. **Choose a procedure** — Pick the repair task from the procedure list
4. **Follow the sequence** — Parts highlight in removal order; connectors and fasteners are called out
5. **Mark complete** — Log the repair for your records

---

## System Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                      MOBILE DEVICE                          │
├─────────────────────────────────────────────────────────────┤
│  ┌───────────────┐  ┌───────────────┐  ┌───────────────┐   │
│  │   AR Layer    │  │  Procedure    │  │   Part Info   │   │
│  │  (ARKit/Core) │  │    Engine     │  │   Database    │   │
│  └───────┬───────┘  └───────┬───────┘  └───────┬───────┘   │
│          │                  │                  │            │
│          └──────────────────┼──────────────────┘            │
│                             │                               │
│                    ┌────────▼────────┐                      │
│                    │  Local SQLite   │                      │
│                    │  + JSON Graphs  │                      │
│                    └─────────────────┘                      │
└─────────────────────────────────────────────────────────────┘
```

**Key design decisions:**

- **Local-first** — All data lives on device. No accounts, no cloud dependency, no subscriptions.
- **Model-driven procedures** — Each engine model bundles its own procedure graphs. The app doesn't guess; it knows what's connected to what because you told it which engine you have.
- **Graph-based sequences** — Procedures aren't flat checklists. They're dependency graphs: "Remove X before Y" with conditional branches for engine variants.

---

## Tech Stack

| Layer | Technology | Purpose |
|-------|------------|---------|
| AR Runtime | Unity 2022.3 LTS + AR Foundation 5.1.5 | Cross-platform AR (iOS/Android) |
| 3D Rendering | Unity URP 14.0.11 | Lightweight rendering for mobile |
| 3D Model Loading | glTFast 6.6.0 | Runtime GLB/glTF import |
| Procedure Data | JSON + SQLite | Portable, offline-capable storage |
| Speech (planned) | On-device STT | Platform-native (not yet implemented) |
| Part Recognition | YOLOv8 (future) | Visual identification without model alignment |

---

## Project Structure

```
Mechanic-Scope/
├── Assets/
│   ├── Scripts/
│   │   ├── Core/              # AR alignment, procedure runner, part database, model loading
│   │   ├── Data/              # SQLite wrapper, repositories (parts, progress)
│   │   ├── UI/                # Screen controllers, popups, procedure cards
│   │   ├── Voice/             # Voice command manager (planned, not yet functional)
│   │   ├── Accessibility/     # Screen reader, haptics, text scaling
│   │   ├── Performance/       # FPS monitoring, LOD manager, asset optimizer
│   │   ├── Utils/             # Media loader utility
│   │   └── Editor/            # Scene setup tool, procedure editor window
│   ├── Shaders/               # URP part highlight shader
│   ├── Resources/             # DefaultPartsData.json (bundled part data)
│   ├── StreamingAssets/
│   │   └── Engines/           # Engine models + procedure JSON files
│   └── Tests/
│       └── EditMode/          # Edit mode tests + shipped-data validation
├── Docs/
│   ├── ADDING_ENGINES.md      # Engine model import guide
│   ├── PROCEDURE_FORMAT.md    # Procedure JSON specification
│   └── UNITY_SETUP.md         # Development environment setup
├── Tools/
│   └── HeadlessTests/         # Runs the test suite without Unity (see its README)
├── Packages/
│   └── manifest.json          # Unity package dependencies
├── run_tests.sh               # Test entry point
└── README.md
```

---

## Running the Tests

The suite runs headlessly — no Unity installation required. It needs the .NET 8 SDK
(`sudo apt-get install -y dotnet-sdk-8.0`, or see [dotnet.microsoft.com](https://dotnet.microsoft.com/download/dotnet/8.0)):

```bash
./run_tests.sh
```

This compiles the real sources in `Assets/Scripts/Core` and the real tests in
`Assets/Tests/EditMode` against a minimal `UnityEngine` shim, then runs them. It exits non-zero if
anything fails to compile or any test fails, so it is safe to use as a CI or pre-commit gate.

Coverage includes procedure dependency resolution and step sequencing, JSON parsing, the part
database, engine-ID path-traversal validation, and integrity of the bundled engine and procedure
data (no dependency cycles, no dangling step references, every referenced part resolvable).

Rendering, AR and coroutine timing are outside what the shim can verify — run those in the Editor
via **Window > General > Test Runner**. See
[`Tools/HeadlessTests/README.md`](Tools/HeadlessTests/README.md) for the full boundary.

---

## Engine Models

Mechanic Scope requires 3D engine models that you provide. Options include:

| Source | Notes |
|--------|-------|
| **Your own CAD** | If you have access to OEM or aftermarket CAD files |
| **Photogrammetry** | Scan your own engine with a photogrammetry app |
| **Community models** | Check GrabCAD, Sketchfab (verify licensing) |
| **Simplified models** | Hand-model key components for procedure guidance |

The app doesn't need manufacturer-perfect CAD. A simplified model with correct component positions works fine for overlay alignment and procedure guidance.

**Supported formats:** .glb / .gltf (via glTFast)

---

## Procedure Format

Procedures are JSON graphs defining removal sequences. Example:

```json
{
  "procedure": "replace_alternator",
  "engine": "gm_ls_gen4",
  "steps": [
    {
      "id": 1,
      "action": "Disconnect negative battery terminal",
      "part": "battery_negative",
      "tools": ["10mm socket"],
      "warnings": ["Always disconnect battery first"]
    },
    {
      "id": 2,
      "action": "Remove serpentine belt",
      "part": "serpentine_belt",
      "requires": [1],
      "tools": ["15mm wrench or belt tool"]
    },
    {
      "id": 3,
      "action": "Disconnect alternator electrical connector",
      "part": "alternator_connector",
      "requires": [1]
    },
    {
      "id": 4,
      "action": "Remove alternator mounting bolts",
      "part": "alternator",
      "requires": [2, 3],
      "tools": ["13mm socket"],
      "torque_spec": "37 ft-lbs on reinstall"
    }
  ]
}
```

The `requires` field creates the dependency graph. Steps 2 and 3 can happen in either order (both only require step 1), but step 4 requires both 2 and 3 to be complete.

---

## Development Status

### What Works
- [x] AR session management and camera feed
- [x] Manual model alignment (rotate, scale, translate via touch gestures)
- [x] Dependency-aware procedure engine with graph-based step resolution
- [x] Part tap-to-identify via raycasting
- [x] SQLite-backed part database with full-text search
- [x] Progress persistence and repair history logging
- [x] Part highlighting shader (URP Fresnel + outline)
- [x] Performance monitoring (FPS, memory, battery)
- [x] Accessibility features (native haptics, text scaling, high contrast)

### What Needs Work
- [ ] **3D model loading** — currently generates placeholder geometry; glTFast is integrated but no `.glb` file is bundled
- [ ] **Voice commands** — code references platform recognizers that are not yet implemented
- [ ] **LOD manager** — mesh simplification is a stub (returns input unchanged)
- [ ] **App initialization** — `InitializeARSystems()` is a placeholder

### Future Consideration
- [ ] ML-based part recognition (reduce reliance on manual alignment)
- [ ] OBD-II integration for diagnostic context
- [ ] Multi-device progress sync
- [ ] Community procedure marketplace

---

## Licensing

Mechanic Scope is released under the **MIT License**.

**What this covers:** The application code, UI, procedure engine, and documentation.

**What this does not cover:** Any 3D engine models, OEM service data, or manufacturer trademarks. Users are responsible for ensuring they have appropriate rights to any models or data they import.

---

## Contributing

1. Fork the repository
2. Create a feature branch: `git checkout -b feature/your-feature`
3. Commit changes with clear messages
4. Open a pull request with a description of what you've added

See `Docs/UNITY_SETUP.md` for development environment setup.

---

*Mechanic Scope is an independent project and is not affiliated with any vehicle manufacturer.*
