# Adding Engine Models to Mechanic Scope

This guide explains how to add new engine models to Mechanic Scope, from obtaining 3D models to configuring part mappings and procedures.

---

## Table of Contents

- [Overview](#overview)
- [Engine Model Requirements](#engine-model-requirements)
- [Obtaining Engine Models](#obtaining-engine-models)
- [File Structure](#file-structure)
- [Engine Manifest (engine.json)](#engine-manifest-enginejson)
- [Part Mappings](#part-mappings)
- [Adding Procedures](#adding-procedures)
- [Testing Your Engine](#testing-your-engine)
- [Troubleshooting](#troubleshooting)

---

## Overview

Each engine in Mechanic Scope consists of:

1. **3D Model** - A GLB, FBX, or OBJ file representing the engine
2. **Engine Manifest** - A JSON file describing the engine and mapping model nodes to parts
3. **Procedures** - JSON files defining repair/maintenance tasks
4. **Thumbnail** - An optional preview image for the engine selection screen

---

## Engine Model Requirements

### Supported Formats

| Format | Extension | Notes |
|--------|-----------|-------|
| glTF Binary | `.glb` | **Recommended** - Best compatibility and file size |
| FBX | `.fbx` | Good support, larger files |
| OBJ | `.obj` | Basic support, no animations |

### Model Guidelines

- **Scale**: Model should be approximately real-world scale (1 unit = 1 meter)
- **Orientation**: Front of engine facing +Z axis, top facing +Y axis
- **Polygon Count**: Target 50,000-200,000 triangles for mobile performance
- **Materials**: Use PBR materials; avoid complex shaders
- **Naming**: Name each part/component mesh descriptively (e.g., `Alternator_Mesh`, `OilFilter_Mesh`)

### Part Organization

For part identification to work, individual components must be **separate meshes** with unique names:

```
Engine (root)
├── Block_Mesh
├── Head_Left_Mesh
├── Head_Right_Mesh
├── Alternator_Mesh
├── Starter_Mesh
├── OilPan_Mesh
├── OilFilter_Mesh
├── SerpentineBelt_Mesh
├── Intake_Mesh
├── Exhaust_Left_Mesh
├── Exhaust_Right_Mesh
└── ...
```

---

## Obtaining Engine Models

### Option 1: Create Your Own

Use 3D modeling software (Blender, Maya, etc.) to model the engine. Focus on major components rather than perfect accuracy.

### Option 2: Photogrammetry

Scan your actual engine using photogrammetry apps:
- Polycam (iOS/Android)
- RealityScan (iOS/Android)
- Meshroom (Desktop, free)

**Tips for scanning:**
- Clean the engine first
- Use consistent lighting
- Take 50-100 overlapping photos
- Process and clean up the mesh in Blender

### Option 3: Community Models

Check these sources (verify licensing before use):

| Source | URL | Notes |
|--------|-----|-------|
| GrabCAD | grabcad.com | Engineering CAD models |
| Sketchfab | sketchfab.com | 3D models with various licenses |
| TurboSquid | turbosquid.com | Commercial and free models |
| CGTrader | cgtrader.com | Commercial and free models |

**Important**: Respect model licenses. Many models are for personal use only.

### Option 4: Simplified Models

You don't need photo-realistic accuracy. A simplified model with correctly positioned major components works well for overlay guidance.

---

## File Structure

Engine data is stored in `Assets/StreamingAssets/Engines/`:

```
Assets/StreamingAssets/Engines/
└── [engine_id]/
    ├── engine.json          # Engine manifest (required)
    ├── [model_file].glb     # 3D model (required)
    ├── thumbnail.png        # Preview image (optional)
    └── procedures/          # Procedure JSON files
        ├── oil_change.json
        ├── replace_alternator.json
        └── ...
```

### Naming Convention

Use lowercase with underscores for the engine ID:
- `gm_ls_gen4`
- `honda_k24`
- `ford_coyote_5l`

---

## Engine Manifest (engine.json)

The engine manifest describes the engine and maps 3D model nodes to part IDs.

### Required Fields

```json
{
    "id": "gm_ls_gen4",
    "name": "GM LS Gen IV",
    "manufacturer": "General Motors",
    "modelFile": "gm_ls_gen4.glb",
    "partMappings": []
}
```

### Complete Example

```json
{
    "id": "gm_ls_gen4",
    "name": "GM LS Gen IV (L76/L77/LS3)",
    "manufacturer": "General Motors",
    "years": "2007-2017",
    "description": "6.0L/6.2L V8 engine used in Corvette, Camaro, and truck applications",
    "modelFile": "gm_ls_gen4.glb",
    "thumbnail": "thumbnail.png",
    "partMappings": [
        {
            "nodeNameInModel": "Alternator_Mesh",
            "partId": "alternator_gm_ls"
        },
        {
            "nodeNameInModel": "Starter_Mesh",
            "partId": "starter_gm_ls"
        },
        {
            "nodeNameInModel": "OilFilter_Mesh",
            "partId": "oil_filter_gm_ls"
        },
        {
            "nodeNameInModel": "SerpentineBelt_Mesh",
            "partId": "serpentine_belt"
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

### Field Reference

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `id` | string | Yes | Unique identifier (lowercase, underscores) |
| `name` | string | Yes | Display name |
| `manufacturer` | string | Yes | Engine manufacturer |
| `modelFile` | string | Yes | 3D model filename |
| `partMappings` | array | Yes | Maps model nodes to part IDs |
| `years` | string | No | Production years |
| `description` | string | No | Detailed description |
| `thumbnail` | string | No | Preview image filename |
| `defaultAlignment` | object | No | Initial alignment transform |

---

## Part Mappings

Part mappings connect the 3D model mesh names to part IDs in the parts database.

### Finding Node Names

1. Open your model in Blender or Unity
2. Look at the object hierarchy
3. Note the exact names of each mesh

### Mapping Structure

```json
{
    "nodeNameInModel": "Alternator_Mesh",  // Exact name in 3D model
    "partId": "alternator_gm_ls"           // ID in parts database
}
```

### Creating New Part IDs

If a part doesn't exist in the database, you can:

1. Use an existing generic part ID (e.g., `alternator`, `oil_filter`)
2. Create a new entry in `Assets/Resources/DefaultPartsData.json`:

```json
{
    "id": "alternator_gm_ls",
    "name": "Alternator - GM LS",
    "description": "145-amp alternator for LS engines",
    "category": "electrical",
    "specifications": {
        "output": "145 amps",
        "voltage": "14.2V"
    },
    "crossReferences": [
        "AC Delco 334-2529",
        "Denso 210-0578"
    ]
}
```

---

## Adding Procedures

Each engine can have multiple procedures stored in the `procedures/` subfolder.

### Procedure File Structure

```json
{
    "id": "replace_alternator",
    "name": "Replace Alternator",
    "description": "Remove and replace the alternator",
    "engineId": "gm_ls_gen4",
    "estimatedTime": "45-60 minutes",
    "difficulty": "intermediate",
    "tools": ["10mm socket", "13mm socket", "15mm wrench"],
    "steps": [
        {
            "id": 1,
            "action": "Disconnect negative battery terminal",
            "partId": "battery_negative",
            "requires": [],
            "tools": ["10mm socket"],
            "warnings": ["Always disconnect battery first"]
        }
    ]
}
```

See [PROCEDURE_FORMAT.md](./PROCEDURE_FORMAT.md) for complete procedure specification.

---

## Testing Your Engine

### In-Editor Testing

1. Open Unity and load the project
2. Open the main scene
3. Enter Play mode
4. Select your engine from the engine list
5. Verify:
   - Model loads correctly
   - Part tap-to-identify works
   - Procedures load and display

### On-Device Testing

1. Build for your target platform (iOS/Android)
2. Install on a test device
3. Test AR alignment with a real engine (or printed reference)
4. Verify all procedures work correctly

### Common Issues

| Issue | Cause | Solution |
|-------|-------|----------|
| Model doesn't load | Wrong path or format | Check `modelFile` path in manifest |
| Parts not detected | Mesh names don't match | Verify `nodeNameInModel` exactly matches |
| Model too big/small | Scale issue | Adjust `defaultAlignment.scale` |
| Wrong orientation | Rotation issue | Adjust `defaultAlignment.rotation` |

---

## Troubleshooting

### Model Loading Issues

**"Failed to load model"**
- Verify the GLB/FBX file is not corrupted
- Try re-exporting from your 3D software
- Check Unity console for detailed error

**Model appears but parts aren't selectable**
- Ensure each part is a separate mesh object
- Verify mesh colliders are generated (check EngineModelLoader settings)
- Check that `nodeNameInModel` exactly matches the mesh name

### Part Mapping Issues

**Tap on part shows wrong name or nothing**
- Open model in Unity and verify hierarchy names
- Check for typos in `nodeNameInModel`
- Verify `partId` exists in parts database

### Performance Issues

**Model loads slowly or causes lag**
- Reduce polygon count (decimate in Blender)
- Simplify materials
- Remove unnecessary detail meshes
- Target 50,000-200,000 triangles

### Alignment Issues

**Model appears in wrong position**
- Adjust `defaultAlignment.position` in engine.json
- Check model origin point in your 3D software
- Verify model scale is approximately 1 unit = 1 meter

---

## Best Practices

1. **Start simple**: Add a basic model with key components first
2. **Test frequently**: Verify each part mapping works before adding more
3. **Document your work**: Add descriptions and notes for future reference
4. **Share with community**: Consider contributing your engine data back to the project

---

## Contributing Engine Data

If you'd like to share your engine model and procedures:

1. Ensure you have rights to distribute the model
2. Test thoroughly on at least one device
3. Include comprehensive part mappings
4. Add 2-3 common procedures
5. Submit a Pull Request following [CONTRIBUTING.md](./CONTRIBUTING.md)

---

## Related Documentation

- [PROCEDURE_FORMAT.md](./PROCEDURE_FORMAT.md) - Detailed procedure specification
- [UNITY_SETUP.md](./UNITY_SETUP.md) - Development environment setup
- [CONTRIBUTING.md](./CONTRIBUTING.md) - Contribution guidelines
