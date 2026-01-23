# Procedure Format Specification

This document defines the JSON format for repair and maintenance procedures in Mechanic Scope.

---

## Table of Contents

- [Overview](#overview)
- [File Location](#file-location)
- [Complete Example](#complete-example)
- [Schema Reference](#schema-reference)
- [Step Dependencies](#step-dependencies)
- [Media Support](#media-support)
- [Torque Specifications](#torque-specifications)
- [Conditional Steps](#conditional-steps)
- [Best Practices](#best-practices)
- [Validation](#validation)

---

## Overview

Procedures in Mechanic Scope are **dependency graphs**, not simple linear checklists. Each step can depend on one or more previous steps, allowing for:

- Parallel work paths (e.g., disconnect electrical AND remove belt before removing alternator)
- Flexible completion order where dependencies allow
- Clear visualization of what's blocked and why

---

## File Location

Procedure files are stored in the engine's procedure folder:

```
Assets/StreamingAssets/Engines/[engine_id]/procedures/
├── oil_change.json
├── replace_alternator.json
├── replace_starter.json
└── ...
```

**Naming Convention**: Use lowercase with underscores (e.g., `replace_water_pump.json`)

---

## Complete Example

```json
{
    "id": "replace_alternator",
    "name": "Replace Alternator",
    "description": "Remove and replace the alternator on a GM LS Gen IV engine",
    "engineId": "gm_ls_gen4",
    "version": "1.0",
    "author": "Community",
    "estimatedTime": "45-60 minutes",
    "difficulty": "intermediate",
    "tools": [
        "10mm socket",
        "13mm socket",
        "15mm wrench or serpentine belt tool",
        "Torque wrench",
        "Ratchet with extensions"
    ],
    "parts": [
        {
            "name": "Alternator",
            "partNumber": "AC Delco 334-2529",
            "quantity": 1,
            "required": true
        }
    ],
    "steps": [
        {
            "id": 1,
            "action": "Disconnect negative battery terminal",
            "details": "Loosen the 10mm nut securing the negative cable to the battery. Pull the cable off and tuck it away from the battery to prevent accidental contact.",
            "partId": "battery_negative",
            "requires": [],
            "tools": ["10mm socket"],
            "warnings": [
                "Always disconnect the battery before working on electrical components",
                "Ensure the key is out of the ignition"
            ],
            "media": {
                "image": "step1_battery.png"
            }
        },
        {
            "id": 2,
            "action": "Remove serpentine belt",
            "details": "Locate the belt tensioner pulley. Use a 15mm wrench or belt tool to rotate the tensioner clockwise (releases tension). While holding, slip the belt off the alternator pulley. Slowly release the tensioner.",
            "partId": "serpentine_belt",
            "requires": [1],
            "tools": ["15mm wrench or serpentine belt tool"],
            "tips": [
                "Take a photo of the belt routing before removal",
                "Check belt for cracks or wear while it's off"
            ]
        },
        {
            "id": 3,
            "action": "Disconnect alternator electrical connector",
            "details": "Locate the main electrical connector on the back of the alternator. Press the release tab and pull the connector straight off. There's also a smaller push-on connector for the charge indicator - pull it straight off.",
            "partId": "alternator_connector",
            "requires": [1]
        },
        {
            "id": 4,
            "action": "Remove alternator mounting bolts",
            "details": "Remove the two 13mm bolts securing the alternator to the mounting bracket. There's one on top and one on the bottom. Support the alternator as you remove the final bolt.",
            "partId": "alternator",
            "requires": [2, 3],
            "tools": ["13mm socket", "Ratchet with extensions"],
            "torqueSpec": {
                "value": 37,
                "unit": "ft-lbs",
                "note": "Apply on reinstall"
            }
        },
        {
            "id": 5,
            "action": "Remove alternator from engine bay",
            "details": "Carefully lift and maneuver the alternator out of the engine bay. You may need to rotate it to clear the radiator hose and other components.",
            "partId": "alternator",
            "requires": [4],
            "tips": [
                "Note the alternator orientation for reinstallation"
            ]
        }
    ],
    "reinstallNotes": "Installation is the reverse of removal. Ensure the serpentine belt is properly seated on all pulleys before releasing the tensioner. Torque mounting bolts to 37 ft-lbs.",
    "finalChecks": [
        "Verify belt is properly routed on all pulleys",
        "Reconnect battery and verify charging system operation",
        "Check for any warning lights"
    ]
}
```

---

## Schema Reference

### Root Object

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `id` | string | Yes | Unique procedure identifier |
| `name` | string | Yes | Human-readable procedure name |
| `description` | string | No | Detailed description |
| `engineId` | string | Yes | ID of the engine this procedure applies to |
| `version` | string | No | Procedure version (e.g., "1.0") |
| `author` | string | No | Procedure author/contributor |
| `estimatedTime` | string | No | Time estimate (e.g., "45-60 minutes") |
| `difficulty` | string | No | "beginner", "intermediate", "advanced" |
| `tools` | array | No | List of required tools |
| `parts` | array | No | List of parts needed (see Parts Object) |
| `steps` | array | Yes | Ordered array of step objects |
| `reinstallNotes` | string | No | Notes for reinstallation |
| `finalChecks` | array | No | List of final verification steps |

### Parts Object

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `name` | string | Yes | Part name |
| `partNumber` | string | No | OEM or aftermarket part number |
| `quantity` | number | No | Number needed (default: 1) |
| `required` | boolean | No | Whether part is required or optional |

### Step Object

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `id` | number | Yes | Unique step ID (positive integer) |
| `action` | string | Yes | Short action description (shown in UI) |
| `details` | string | No | Detailed instructions |
| `partId` | string | No | Part ID for highlighting in AR |
| `requires` | array | Yes | Array of step IDs that must complete first |
| `tools` | array | No | Tools needed for this step |
| `warnings` | array | No | Safety warnings (displayed prominently) |
| `tips` | array | No | Helpful tips (less prominent than warnings) |
| `torqueSpec` | object | No | Torque specification (see Torque Object) |
| `media` | object | No | Images or video for this step |
| `condition` | object | No | Conditional logic (see Conditional Steps) |

### Torque Object

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `value` | number | Yes | Torque value |
| `unit` | string | Yes | "ft-lbs", "nm", "in-lbs" |
| `note` | string | No | Additional context |
| `sequence` | string | No | Tightening sequence if applicable |

### Media Object

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `image` | string | No | Image filename (relative to procedure folder) |
| `video` | string | No | Video filename or URL |
| `caption` | string | No | Media description |

---

## Step Dependencies

### How Dependencies Work

The `requires` array lists step IDs that must be completed before a step becomes available:

```json
{
    "id": 4,
    "action": "Remove alternator mounting bolts",
    "requires": [2, 3]
}
```

Step 4 only becomes available after **both** steps 2 and 3 are complete.

### Dependency Patterns

**Linear (A → B → C)**
```json
{"id": 1, "requires": []},
{"id": 2, "requires": [1]},
{"id": 3, "requires": [2]}
```

**Parallel then Converge (A → B, A → C, B+C → D)**
```json
{"id": 1, "requires": []},
{"id": 2, "requires": [1]},
{"id": 3, "requires": [1]},
{"id": 4, "requires": [2, 3]}
```

**Fork (A → B, A → C with no convergence)**
```json
{"id": 1, "requires": []},
{"id": 2, "requires": [1]},
{"id": 3, "requires": [1]}
```

### Dependency Visualization

```
Step 1: Disconnect Battery
    ├──→ Step 2: Remove Belt
    │         │
    │         ↓
    └──→ Step 3: Disconnect Connector
              │
              ↓
         Step 4: Remove Bolts (requires 2 AND 3)
              │
              ↓
         Step 5: Remove Alternator
```

---

## Media Support

### Image Files

Place images in the procedure's folder:

```
procedures/
├── replace_alternator.json
├── step1_battery.png
├── step2_belt_routing.png
└── step4_bolt_locations.png
```

Reference in the step:

```json
{
    "id": 1,
    "media": {
        "image": "step1_battery.png",
        "caption": "Battery negative terminal location"
    }
}
```

### Image Guidelines

- Format: PNG or JPG
- Resolution: 1280x720 recommended
- Size: Under 500KB per image
- Content: Clear, well-lit photos focusing on the relevant area

### Video Support

Videos can be local files or URLs:

```json
{
    "media": {
        "video": "step2_belt_removal.mp4",
        "caption": "Belt removal technique"
    }
}
```

Or external:

```json
{
    "media": {
        "video": "https://example.com/videos/belt_removal.mp4"
    }
}
```

---

## Torque Specifications

### Basic Torque Spec

```json
{
    "torqueSpec": {
        "value": 37,
        "unit": "ft-lbs"
    }
}
```

### Torque with Notes

```json
{
    "torqueSpec": {
        "value": 18,
        "unit": "ft-lbs",
        "note": "Apply to new bolts only; reused bolts torque to 15 ft-lbs"
    }
}
```

### Torque Sequence

For multi-bolt patterns:

```json
{
    "torqueSpec": {
        "value": 22,
        "unit": "ft-lbs",
        "sequence": "Tighten in star pattern: 1-3-5-2-4. First pass to 11 ft-lbs, second pass to 22 ft-lbs."
    }
}
```

### Supported Units

| Unit | Description |
|------|-------------|
| `ft-lbs` | Foot-pounds (US standard) |
| `nm` | Newton-meters (metric) |
| `in-lbs` | Inch-pounds (small fasteners) |

---

## Conditional Steps

For procedures with variations (e.g., different model years), use conditional steps:

```json
{
    "id": 6,
    "action": "Remove additional bracket (2010+ models only)",
    "details": "2010 and later models have an additional support bracket...",
    "requires": [5],
    "condition": {
        "type": "year",
        "operator": ">=",
        "value": 2010,
        "description": "Only for 2010+ models"
    }
}
```

### Condition Types

| Type | Description | Example |
|------|-------------|---------|
| `year` | Model year comparison | `{"type": "year", "operator": ">=", "value": 2010}` |
| `variant` | Engine variant | `{"type": "variant", "value": "supercharged"}` |
| `optional` | User choice | `{"type": "optional", "description": "If replacing tensioner"}` |

Users can skip conditional steps if the condition doesn't apply.

---

## Best Practices

### Writing Clear Steps

1. **Action**: Start with a verb (Remove, Disconnect, Loosen, Install)
2. **Details**: Explain the how, not just the what
3. **Specificity**: Include bolt sizes, locations, directions

**Good:**
```json
{
    "action": "Remove upper intake manifold bolts",
    "details": "Remove the eight 8mm bolts securing the upper intake to the lower. Start from the outside corners and work inward to prevent warping."
}
```

**Bad:**
```json
{
    "action": "Take off the intake",
    "details": "Remove the bolts."
}
```

### Safety Warnings

Always include warnings for:
- Electrical hazards
- Hot components
- Fluid hazards (fuel, coolant, oil)
- Heavy parts
- Pressurized systems
- Moving parts

```json
{
    "warnings": [
        "Engine must be cool before starting this procedure",
        "Fuel system is pressurized - relieve pressure before disconnecting fuel lines"
    ]
}
```

### Dependency Design

- Avoid unnecessary dependencies (don't block steps that can actually be done in parallel)
- Test the dependency graph by walking through the procedure
- Consider both the "by the book" order and what a mechanic might actually do

### Tool Specifications

Be specific about tools:

**Good:**
- "15mm combination wrench"
- "3/8\" drive torque wrench (10-80 ft-lbs range)"
- "Serpentine belt tool or 15mm wrench"

**Bad:**
- "wrench"
- "socket"
- "some tool"

---

## Validation

### Required Validation Rules

1. **Unique Step IDs**: No duplicate `id` values
2. **Valid Dependencies**: All IDs in `requires` must exist as step IDs
3. **No Circular Dependencies**: Steps cannot depend on themselves (directly or indirectly)
4. **At Least One Starting Step**: At least one step must have `requires: []`

### JSON Schema

A JSON Schema for validation is available at:
```
Assets/Editor/ProcedureSchema.json
```

### Validation in Unity

The Procedure Editor window validates procedures before saving:

1. **Window → Mechanic Scope → Procedure Editor**
2. Load or create a procedure
3. Click **Validate** to check for errors

### Common Validation Errors

| Error | Cause | Fix |
|-------|-------|-----|
| "Duplicate step ID" | Two steps have same ID | Assign unique IDs |
| "Unknown dependency" | Step requires non-existent ID | Check step ID exists |
| "Circular dependency" | A → B → A | Restructure dependencies |
| "No starting step" | All steps have dependencies | Add a step with empty requires |
| "Missing required field" | Step missing id, action, or requires | Add missing field |

---

## Exporting and Sharing

Procedures can be exported for sharing:

1. In the app, go to **Settings → Export Procedure**
2. Select the procedure to export
3. Choose export format (JSON or compressed archive with media)

To import:
1. **Settings → Import Procedure**
2. Select the procedure file
3. Choose target engine (or create new)

### Community Sharing Format

When sharing procedures with the community, include:
- The procedure JSON file
- Any referenced images/media
- A brief README with:
  - Engine model/year this was tested on
  - Any special notes or variations
  - Your contact for questions

---

## Related Documentation

- [ADDING_ENGINES.md](./ADDING_ENGINES.md) - How to add engine models
- [CONTRIBUTING.md](./CONTRIBUTING.md) - Contribution guidelines
- [SPEC_SHEET.md](../SPEC_SHEET.md) - Full technical specification
