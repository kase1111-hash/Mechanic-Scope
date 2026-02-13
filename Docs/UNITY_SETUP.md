# Unity Project Setup Guide

## Prerequisites

- **Unity 2022.3 LTS** (2022.3.x)
- iOS: Xcode 14+ for iOS builds
- Android: Android SDK with API 26+

## Quick Start

### 1. Open the Project in Unity

Open the `/home/user/Mechanic-Scope` folder in Unity Hub. On first open, Unity will:
- Resolve packages from `Packages/manifest.json` (AR Foundation, glTFast, URP, etc.)
- Generate `.meta` files for all assets
- Compile all scripts in `Assets/Scripts/`

### 2. Build the Main Scene

The project includes an automated scene builder. After compilation:

1. Go to menu: **MechanicScope > Setup Main Scene**
2. This creates `Assets/Scenes/MainScene.unity` with:
   - AR Session + AR Session Origin + AR Camera
   - All manager GameObjects (AppInitializer, EngineModelLoader, ProcedureRunner, etc.)
   - UI Canvas with all 8 screen panels, header, and navigation wiring
   - All SerializeField references pre-wired between components

### 3. Assign Materials

After scene setup, two references need manual assignment on the **EngineModelLoader** component:

| Field | Create As |
|-------|-----------|
| `defaultMaterial` | URP/Lit material, gray (#808080), smoothness 0.5 |
| `highlightMaterial` | URP/Lit material, orange (#FF6B35), emission enabled |

Or use the included `Assets/Shaders/PartHighlight.shader` for the highlight material.

### 4. Add a Real 3D Engine Model

Place a `.glb` file in `Assets/StreamingAssets/Engines/gm_ls_gen4/`:

```
StreamingAssets/Engines/gm_ls_gen4/
├── engine.json          (already exists)
├── gm_ls_gen4.glb       (YOU ADD THIS)
└── procedures/
    ├── oil_change.json          (already exists)
    └── replace_alternator.json  (already exists)
```

The model's mesh node names must match the `partMappings[].nodeNameInModel` entries in `engine.json`. If your model uses different names, update `engine.json` accordingly.

**Where to source a model:**
- [Sketchfab](https://sketchfab.com/search?q=v8+engine&type=models) — search "V8 engine" or "LS engine"
- [TurboSquid](https://www.turbosquid.com/) — search for engine models, export as GLB
- Create one in Blender and export as `.glb`

### 5. Configure XR

Go to **Edit > Project Settings > XR Plug-in Management**:

- **Android tab:** Enable **ARCore**
- **iOS tab:** Enable **ARKit**

### 6. Build and Deploy

**Android:**
1. File > Build Settings > Android > Switch Platform
2. Player Settings > Other Settings: Min API Level = 26
3. Build and Run on ARCore-compatible device

**iOS:**
1. File > Build Settings > iOS > Switch Platform
2. Player Settings > Other Settings:
   - Camera Usage Description: "Required for AR engine overlay"
   - Microphone Usage Description: "Optional for voice commands"
   - Require ARKit support: checked
3. Build > Open in Xcode > Run on device

## Project Structure

```
Assets/
├── Scripts/
│   ├── MechanicScope.asmdef        # Main assembly (references glTFast, AR Foundation)
│   ├── Core/                        # AR, procedures, parts, progress, model loading
│   ├── Data/                        # SQLite, repositories, data manager
│   ├── UI/                          # Screen controllers and UI components
│   ├── Voice/                       # Voice commands (scaffolded, not yet functional)
│   ├── Accessibility/               # Text scaling, haptics, screen reader support
│   ├── Performance/                 # FPS monitoring, asset optimization, LOD (partial)
│   ├── Utils/                       # Media loading
│   └── Editor/                      # Scene setup tool, procedure editor
├── Resources/
│   └── DefaultPartsData.json        # Bundled parts database
├── Shaders/
│   └── PartHighlight.shader         # URP highlight shader
└── StreamingAssets/
    └── Engines/gm_ls_gen4/          # Bundled engine + procedures
```

## Packages (auto-resolved)

| Package | Version | Purpose |
|---------|---------|---------|
| AR Foundation | 5.1.5 | AR abstraction layer |
| ARCore XR Plugin | 5.1.5 | Android AR |
| ARKit XR Plugin | 5.1.5 | iOS AR |
| glTFast | 6.6.0 | Runtime GLB/glTF model loading |
| URP | 14.0.11 | Rendering pipeline |
| TextMeshPro | 3.0.9 | UI text rendering |
| Input System | 1.8.2 | Touch input |

## App Flow

```
Splash (2s) → Engine Selection → [Load GLB] → AR Alignment →
Procedure Selection → Step-by-Step Repair → Completion Summary
```

## Troubleshooting

**"Scripts won't compile"**
- Ensure Unity 2022.3 LTS. Newer versions may need package updates.
- Check Package Manager for missing/failed packages.

**"Model doesn't load"**
- Verify the `.glb` file is in `StreamingAssets/Engines/{engineId}/`
- Check Console for glTFast error messages
- Ensure `engine.json` `modelFile` matches the actual filename

**"Parts not detected on tap"**
- MeshColliders are auto-generated on model load
- Verify `engine.json` `partMappings[].nodeNameInModel` matches model node names
- Check raycast layer masks in AR Camera settings

**"UI screens not switching"**
- Re-run **MechanicScope > Setup Main Scene** to rebuild wiring
- Check that all SerializeField references are assigned in Inspector
