# Unity Project Setup Guide

This document explains how to set up the Mechanic Scope Unity project from the provided scripts.

## Prerequisites

- **Unity 2022.3 LTS** or newer
- **AR Foundation 5.x** package
- **Universal Render Pipeline (URP)** 14.x
- **TextMeshPro** (included with Unity)
- iOS: Xcode 14+ for iOS builds
- Android: Android SDK with ARCore support

## Initial Setup

### 1. Create New Unity Project

1. Open Unity Hub
2. Create new project using **3D (URP)** template
3. Name it `MechanicScope`

### 2. Install Required Packages

Open **Window > Package Manager** and install:

```
com.unity.xr.arfoundation (5.x)
com.unity.xr.arcore (Android)
com.unity.xr.arkit (iOS)
com.unity.textmeshpro
```

### 3. Import Project Files

Copy the following folders from this repository into your Unity project:

```
Assets/
├── Scripts/          → Copy to Assets/Scripts/
├── StreamingAssets/  → Copy to Assets/StreamingAssets/
└── Resources/        → Copy to Assets/Resources/
```

### 4. Configure Build Settings

#### iOS
1. Go to **File > Build Settings**
2. Select **iOS** platform
3. In **Player Settings > Other Settings**:
   - Set **Camera Usage Description**: "Required for AR engine overlay"
   - Set **Microphone Usage Description**: "Optional for voice commands"
   - Enable **Requires ARKit support**

#### Android
1. Select **Android** platform
2. In **Player Settings > Other Settings**:
   - Set minimum API Level to **Android 8.0 (API 26)**
   - Enable **ARCore Supported**
3. In **XR Plug-in Management**:
   - Enable **ARCore**

---

## Scene Setup

### 1. Create Main Scene

Create a new scene called `MainScene` in `Assets/Scenes/`.

### 2. AR Session Setup

1. **Delete** the default Main Camera
2. Create **XR > AR Session** (creates AR Session GameObject)
3. Create **XR > AR Session Origin** (creates AR Session Origin with AR Camera)

Your hierarchy should look like:
```
AR Session
AR Session Origin
  └── AR Camera
```

### 3. Add AR Components

Select **AR Session Origin** and add:
- `AR Raycast Manager` component

### 4. Create Game Manager

1. Create empty GameObject named `GameManager`
2. Add the following components:
   - `ARAlignment`
   - `EngineModelLoader`
   - `ProcedureRunner`
   - `PartDatabase`
   - `ProgressTracker`
   - `MainUIController`

### 5. Configure Component References

#### ARAlignment
| Field | Reference |
|-------|-----------|
| AR Session | AR Session |
| AR Session Origin | AR Session Origin |
| Raycast Manager | AR Session Origin (ARRaycastManager) |
| AR Camera | AR Camera |

#### EngineModelLoader
| Field | Reference |
|-------|-----------|
| Default Material | Create a new URP/Lit material |
| Highlight Material | Create orange/yellow emissive material |

#### ProcedureRunner
| Field | Reference |
|-------|-----------|
| Model Loader | GameManager (EngineModelLoader) |
| Progress Tracker | GameManager (ProgressTracker) |

#### PartDatabase
| Field | Reference |
|-------|-----------|
| Default Parts Data | DefaultPartsData (from Resources) |

---

## UI Setup

### 1. Create Canvas

1. Create **UI > Canvas**
2. Set **Render Mode** to `Screen Space - Overlay`
3. Add **Canvas Scaler** with:
   - UI Scale Mode: `Scale With Screen Size`
   - Reference Resolution: `1080 x 1920`
   - Match: `0.5`

### 2. Create UI Panels

Create the following panels as children of Canvas:

```
Canvas
├── Header
│   ├── MenuButton
│   ├── TitleText
│   └── SettingsButton
├── SplashScreen
├── EngineSelectionScreen
├── AlignmentScreen
│   └── AlignmentControls
├── ProcedureSelectionScreen
├── ProcedureActiveScreen
│   └── ProcedureCard
├── PartInspectorScreen
├── SettingsScreen
└── CompletionScreen
```

### 3. Header Setup

```
Header (Panel)
├── MenuButton (Button with Image)
├── TitleText (TextMeshPro)
└── SettingsButton (Button with Image)

Anchor: Top, Stretch horizontal
Height: 100px
Background: #1E1E1E (dark gray)
```

### 4. Procedure Card Setup

```
ProcedureCard (Panel)
├── StepNumberText (TMP)
├── ActionText (TMP)
├── ExpandedPanel
│   ├── DetailsText (TMP)
│   ├── ToolsPanel
│   │   └── ToolsText (TMP)
│   ├── WarningsPanel
│   │   └── WarningsText (TMP)
│   └── TorquePanel
│       └── TorqueText (TMP)
├── ProgressSlider (Slider)
├── ButtonsContainer
│   ├── PreviousButton
│   ├── CompleteButton
│   └── NextButton
└── CollapseButton

Anchor: Bottom, Stretch horizontal
Background: #2D2D2D
Padding: 20px
```

### 5. Part Info Popup Setup

```
PartInfoPopup (Panel)
├── PartNameText (TMP)
├── CategoryText (TMP)
├── CategoryIcon (Image)
├── DescriptionText (TMP)
├── SpecsText (TMP)
├── CloseButton
└── DetailsButton

Position: Floating (set via script)
Background: #2D2D2D with rounded corners
```

### 6. Engine Selection Screen

```
EngineSelectionScreen (Panel)
├── ScrollView
│   └── Content (Vertical Layout Group)
│       └── [EngineListItem prefab instances]
├── EmptyStateText (TMP)
├── ImportButton
└── LoadingIndicator

Full screen panel
```

### 7. Create Prefabs

Create these prefabs in `Assets/Prefabs/UI/`:

#### EngineListItem Prefab
```
EngineListItem (Button)
├── Thumbnail (Image)
├── InfoContainer
│   ├── NameText (TMP)
│   ├── ManufacturerText (TMP)
│   └── YearsText (TMP)
├── DeleteButton (optional)
└── BundledBadge

Size: Full width, 120px height
```

#### ProcedureListItem Prefab
```
ProcedureListItem (Button)
├── NameText (TMP)
├── DescriptionText (TMP)
├── DifficultyText (TMP)
├── TimeText (TMP)
├── ProgressSlider
├── InProgressBadge
└── ChevronIcon

Size: Full width, 150px height
```

### 8. Add UI Components

Add these scripts to appropriate UI GameObjects:

| Script | Attach To |
|--------|-----------|
| `MainUIController` | GameManager |
| `ProcedureCardUI` | ProcedureCard |
| `PartInfoPopup` | PartInfoPopup |
| `EngineSelectionUI` | EngineSelectionScreen |
| `ProcedureSelectionUI` | ProcedureSelectionScreen |
| `AlignmentControlsUI` | AlignmentControls |

### 9. Connect UI References

For `MainUIController`, connect all screen references, UI components, and core system references.

---

## Materials Setup

### 1. Default Model Material

Create `Materials/ModelDefault.mat`:
- Shader: Universal Render Pipeline/Lit
- Base Color: Gray (#808080)
- Smoothness: 0.5

### 2. Highlight Material

Create `Materials/ModelHighlight.mat`:
- Shader: Universal Render Pipeline/Lit
- Base Color: Safety Orange (#FF6B35)
- Emission: Enabled, Orange (#FF6B35), Intensity 0.5

---

## Color Scheme

Use these colors for UI consistency:

| Element | Hex Code |
|---------|----------|
| Primary (buttons) | #FF6B35 |
| Secondary | #4A90A4 |
| Success | #4CAF50 |
| Warning | #FFC107 |
| Danger | #F44336 |
| Background | #1E1E1E |
| Surface | #2D2D2D |
| Text Primary | #FFFFFF |
| Text Secondary | #B0B0B0 |

---

## Testing

### Editor Testing

1. The AR camera won't work in editor, but UI navigation will
2. For model loading, use the placeholder model generation
3. Test procedure flow with the sample GM LS Gen IV procedures

### Device Testing

1. Build to device
2. Point camera at any surface
3. The app will display placeholder cubes for model parts
4. Test full procedure flow with touch controls

---

## Adding GLB Loading Support

The current implementation uses placeholder models. For actual GLB loading:

### Option 1: GLTFUtility (Recommended for open source)
1. Download from: https://github.com/Siccity/GLTFUtility
2. Import the unitypackage
3. In `EngineModelLoader.cs`, replace placeholder model creation with:
```csharp
GameObject model = Siccity.GLTFUtility.Importer.LoadFromFile(path);
```

### Option 2: TriLib (Commercial, supports more formats)
1. Purchase from Unity Asset Store
2. Follow TriLib documentation for runtime loading

---

## Folder Structure Reference

Final project structure:
```
Assets/
├── Materials/
│   ├── ModelDefault.mat
│   └── ModelHighlight.mat
├── Prefabs/
│   ├── AR/
│   └── UI/
│       ├── EngineListItem.prefab
│       └── ProcedureListItem.prefab
├── Resources/
│   └── DefaultPartsData.json
├── Scenes/
│   └── MainScene.unity
├── Scripts/
│   ├── Core/
│   │   ├── ARAlignment.cs
│   │   ├── EngineModelLoader.cs
│   │   ├── PartDatabase.cs
│   │   ├── ProcedureRunner.cs
│   │   └── ProgressTracker.cs
│   └── UI/
│       ├── AlignmentControlsUI.cs
│       ├── EngineSelectionUI.cs
│       ├── MainUIController.cs
│       ├── PartInfoPopup.cs
│       ├── ProcedureCardUI.cs
│       └── ProcedureSelectionUI.cs
├── StreamingAssets/
│   └── Engines/
│       └── gm_ls_gen4/
│           ├── engine.json
│           └── procedures/
│               ├── replace_alternator.json
│               └── oil_change.json
└── Plugins/
    └── [GLTFUtility or TriLib]
```

---

## Troubleshooting

### "AR Session not tracking"
- Ensure good lighting
- Move device to scan environment
- Check AR Foundation is properly installed

### "Model not loading"
- Verify path to StreamingAssets
- Check engine.json format is valid
- Look for errors in Console

### "Touch not working on model"
- Verify MeshColliders are generated
- Check AR Camera has Physics Raycaster
- Ensure model layer is raycastable

### "UI not responding"
- Check Canvas has GraphicRaycaster
- Verify EventSystem exists in scene
- Check button onClick events are wired

---

*For additional help, see the main README or open an issue on GitHub.*
