#!/usr/bin/env python3
"""Generate a stand-in GLB for the GM LS Gen IV engine.

The real engine model is not shipped (licensing - see README "Engine Models"). Without *some*
model, engine.json points at a file that does not exist and nothing downstream of model loading
(alignment, part tapping, highlighting) can be exercised. This script writes a deliberately crude,
blocky V8 whose nodes are named exactly after the `nodeNameInModel` entries in engine.json, so
the full vertical slice runs end to end until a real model replaces it.

Positions are approximate and for layout only - do not use this model to locate real parts.

Pure standard library (no numpy / Blender), so it runs anywhere Python 3.8+ does:

    python3 Tools/StandInModel/generate_standin_glb.py

Output: Assets/StreamingAssets/Engines/gm_ls_gen4/gm_ls_gen4.glb

Coordinate system is glTF's: metres, +Y up, +Z toward the front of the engine (the accessory
drive), +X toward the passenger side. glTFast converts to Unity's left-handed space on import.
"""

import json
import math
import os
import struct
import sys

REPO_ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
ENGINE_DIR = os.path.join(REPO_ROOT, "Assets", "StreamingAssets", "Engines", "gm_ls_gen4")
OUTPUT_PATH = os.path.join(ENGINE_DIR, "gm_ls_gen4.glb")


# --- Geometry primitives -------------------------------------------------------------------
# Each returns (positions, normals, indices) with flat normals (vertices duplicated per face) so
# the blocks shade crisply without any smoothing groups.

def box(center, size):
    cx, cy, cz = center
    hx, hy, hz = size[0] / 2, size[1] / 2, size[2] / 2
    faces = [
        ((1, 0, 0), [(hx, -hy, -hz), (hx, hy, -hz), (hx, hy, hz), (hx, -hy, hz)]),
        ((-1, 0, 0), [(-hx, -hy, hz), (-hx, hy, hz), (-hx, hy, -hz), (-hx, -hy, -hz)]),
        ((0, 1, 0), [(-hx, hy, -hz), (-hx, hy, hz), (hx, hy, hz), (hx, hy, -hz)]),
        ((0, -1, 0), [(-hx, -hy, hz), (-hx, -hy, -hz), (hx, -hy, -hz), (hx, -hy, hz)]),
        ((0, 0, 1), [(-hx, -hy, hz), (hx, -hy, hz), (hx, hy, hz), (-hx, hy, hz)]),
        ((0, 0, -1), [(hx, -hy, -hz), (-hx, -hy, -hz), (-hx, hy, -hz), (hx, hy, -hz)]),
    ]
    positions, normals, indices = [], [], []
    for normal, corners in faces:
        base = len(positions)
        for x, y, z in corners:
            positions.append((cx + x, cy + y, cz + z))
            normals.append(normal)
        indices += [base, base + 1, base + 2, base, base + 2, base + 3]
    return positions, normals, indices


def cylinder(center, radius, length, axis, segments=16):
    """Closed cylinder along 'x', 'y' or 'z'."""
    def orient(a, b, h):
        # (a, b) lie in the cap plane, h runs along the axis.
        if axis == "x":
            return (h, a, b)
        if axis == "y":
            return (a, h, b)
        return (a, b, h)

    cx, cy, cz = center
    half = length / 2
    positions, normals, indices = [], [], []

    def add(p, n):
        positions.append((cx + p[0], cy + p[1], cz + p[2]))
        normals.append(n)
        return len(positions) - 1

    for i in range(segments):
        t0 = 2 * math.pi * i / segments
        t1 = 2 * math.pi * (i + 1) / segments
        tm = (t0 + t1) / 2
        a0, b0 = radius * math.cos(t0), radius * math.sin(t0)
        a1, b1 = radius * math.cos(t1), radius * math.sin(t1)
        n = orient(math.cos(tm), math.sin(tm), 0)
        v = [add(orient(a0, b0, -half), n), add(orient(a1, b1, -half), n),
             add(orient(a1, b1, half), n), add(orient(a0, b0, half), n)]
        indices += [v[0], v[2], v[1], v[0], v[3], v[2]]
        for sign in (-1, 1):
            cn = orient(0, 0, sign)
            c = add(orient(0, 0, sign * half), cn)
            p0 = add(orient(a0, b0, sign * half), cn)
            p1 = add(orient(a1, b1, sign * half), cn)
            indices += [c, p0, p1] if sign > 0 else [c, p1, p0]
    return positions, normals, indices


def merge(*parts):
    positions, normals, indices = [], [], []
    for p, n, i in parts:
        base = len(positions)
        positions += p
        normals += n
        indices += [base + k for k in i]
    return positions, normals, indices


# --- Engine layout -------------------------------------------------------------------------
# (node name, colour RGB, geometry). Names marked "mapped" must match engine.json exactly.

GREY = (0.35, 0.36, 0.38)
DARK = (0.12, 0.12, 0.13)
ALU = (0.70, 0.71, 0.73)
BLACK_PLASTIC = (0.08, 0.08, 0.09)
RUBBER = (0.05, 0.05, 0.05)

PARTS = [
    # Unmapped context geometry: block and heads give the parts something to sit on.
    ("Engine_Block", GREY, box((0, 0.20, -0.05), (0.46, 0.34, 0.62))),
    ("Cylinder_Head_Left", GREY, box((-0.24, 0.40, -0.05), (0.16, 0.14, 0.58))),
    ("Cylinder_Head_Right", GREY, box((0.24, 0.40, -0.05), (0.16, 0.14, 0.58))),
    ("Battery", DARK, box((0.62, 0.36, 0.28), (0.18, 0.18, 0.26))),

    # Mapped parts.
    ("Valve_Cover_Mesh", BLACK_PLASTIC, merge(
        box((-0.26, 0.51, -0.05), (0.15, 0.08, 0.56)),
        box((0.26, 0.51, -0.05), (0.15, 0.08, 0.56)))),
    ("Intake_Manifold_Mesh", BLACK_PLASTIC, box((0, 0.53, -0.07), (0.30, 0.14, 0.50))),
    ("Throttle_Body_Mesh", ALU, cylinder((0, 0.54, 0.24), 0.05, 0.08, "z")),
    ("Oil_Pan_Mesh", DARK, box((0, -0.03, -0.05), (0.40, 0.12, 0.60))),
    ("Oil_Filter_Mesh", (0.85, 0.45, 0.10), cylinder((-0.25, 0.00, 0.10), 0.045, 0.12, "y")),
    ("Water_Pump_Mesh", ALU, merge(
        box((0, 0.26, 0.29), (0.20, 0.16, 0.06)),
        cylinder((0, 0.26, 0.34), 0.05, 0.04, "z"))),
    ("Alternator_Mesh", ALU, cylinder((0.20, 0.44, 0.26), 0.075, 0.14, "z")),
    ("Alternator_Connector", (0.10, 0.10, 0.12), box((0.28, 0.44, 0.20), (0.03, 0.03, 0.04))),
    ("Tensioner_Pulley_Mesh", DARK, cylinder((-0.14, 0.38, 0.34), 0.035, 0.03, "z")),
    ("Serpentine_Belt_Mesh", RUBBER, merge(
        box((0.03, 0.52, 0.355), (0.40, 0.012, 0.022)),     # top run
        box((0.03, 0.14, 0.355), (0.40, 0.012, 0.022)),     # bottom run
        box((-0.17, 0.33, 0.355), (0.012, 0.38, 0.022)),    # left run
        box((0.23, 0.33, 0.355), (0.012, 0.38, 0.022)))),   # right run
    ("Starter_Motor_Mesh", DARK, cylinder((0.25, 0.06, -0.25), 0.05, 0.18, "z")),
    ("Battery_Negative_Terminal", (0.10, 0.10, 0.10),
     cylinder((0.58, 0.47, 0.20), 0.012, 0.03, "y")),
]


# --- GLB writer ----------------------------------------------------------------------------

def pad4(data, fill):
    return data + fill * ((4 - len(data) % 4) % 4)


def build_gltf(parts):
    blob = bytearray()
    buffer_views, accessors, meshes, materials, nodes = [], [], [], [], []

    def add_view(data, target):
        nonlocal blob
        blob = bytearray(pad4(bytes(blob), b"\x00"))
        buffer_views.append({"buffer": 0, "byteOffset": len(blob), "byteLength": len(data),
                             "target": target})
        blob += data
        return len(buffer_views) - 1

    for name, colour, (positions, normals, indices) in parts:
        if len(positions) > 0xFFFF:
            raise ValueError(f"{name}: too many vertices for UNSIGNED_SHORT indices")

        pos_view = add_view(b"".join(struct.pack("<3f", *p) for p in positions), 34962)
        nrm_view = add_view(b"".join(struct.pack("<3f", *n) for n in normals), 34962)
        idx_view = add_view(struct.pack(f"<{len(indices)}H", *indices), 34963)

        mins = [min(p[k] for p in positions) for k in range(3)]
        maxs = [max(p[k] for p in positions) for k in range(3)]
        accessors += [
            {"bufferView": pos_view, "componentType": 5126, "count": len(positions),
             "type": "VEC3", "min": mins, "max": maxs},
            {"bufferView": nrm_view, "componentType": 5126, "count": len(normals), "type": "VEC3"},
            {"bufferView": idx_view, "componentType": 5123, "count": len(indices),
             "type": "SCALAR"},
        ]
        a = len(accessors) - 3

        materials.append({
            "name": f"{name}_Mat",
            "pbrMetallicRoughness": {"baseColorFactor": [*colour, 1.0], "metallicFactor": 0.2,
                                     "roughnessFactor": 0.7},
        })
        meshes.append({"name": name, "primitives": [{
            "attributes": {"POSITION": a, "NORMAL": a + 1}, "indices": a + 2,
            "material": len(materials) - 1}]})
        nodes.append({"name": name, "mesh": len(meshes) - 1})

    root = {"name": "GM_LS_Gen4_StandIn", "children": list(range(1, len(nodes) + 1))}
    gltf = {
        "asset": {"version": "2.0",
                  "generator": "Mechanic-Scope Tools/StandInModel/generate_standin_glb.py"},
        "scene": 0,
        "scenes": [{"name": "Scene", "nodes": [0]}],
        # Children are offset by one because the root is node 0.
        "nodes": [root] + nodes,
        "meshes": meshes,
        "materials": materials,
        "accessors": accessors,
        "bufferViews": buffer_views,
        "buffers": [{"byteLength": 0}],
    }
    blob = pad4(bytes(blob), b"\x00")
    gltf["buffers"][0]["byteLength"] = len(blob)
    return gltf, blob


def write_glb(gltf, blob, path):
    json_chunk = pad4(json.dumps(gltf, separators=(",", ":")).encode("utf-8"), b" ")
    total = 12 + 8 + len(json_chunk) + 8 + len(blob)
    with open(path, "wb") as f:
        f.write(struct.pack("<4sII", b"glTF", 2, total))
        f.write(struct.pack("<I4s", len(json_chunk), b"JSON"))
        f.write(json_chunk)
        f.write(struct.pack("<I4s", len(blob), b"BIN\x00"))
        f.write(blob)


def check_against_manifest():
    """Fail loudly if engine.json maps a node this model does not provide."""
    with open(os.path.join(ENGINE_DIR, "engine.json"), encoding="utf-8") as f:
        manifest = json.load(f)
    names = {name for name, _, _ in PARTS}
    missing = [m["nodeNameInModel"] for m in manifest["partMappings"]
               if m["nodeNameInModel"] not in names]
    if missing:
        sys.exit(f"engine.json maps nodes the stand-in does not define: {', '.join(missing)}")


def main():
    check_against_manifest()
    gltf, blob = build_gltf(PARTS)
    write_glb(gltf, blob, OUTPUT_PATH)
    print(f"Wrote {os.path.relpath(OUTPUT_PATH, REPO_ROOT)} "
          f"({os.path.getsize(OUTPUT_PATH)} bytes, {len(PARTS)} parts)")


if __name__ == "__main__":
    main()
