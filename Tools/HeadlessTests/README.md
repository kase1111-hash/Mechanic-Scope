# Headless test harness

Runs the project's edit-mode test suite without installing Unity.

```bash
./run_tests.sh          # from the repository root
dotnet test Tools/HeadlessTests   # equivalent
```

Requires the .NET 8 SDK (`sudo apt-get install -y dotnet-sdk-8.0`).

## How it works

The harness is a plain .NET 8 NUnit project that compiles **the real production sources** from
`Assets/Scripts/Core` together with **the real tests** from `Assets/Tests/EditMode`, linking them
against a minimal `UnityEngine` stand-in in `UnityShim/`.

Nothing is duplicated. The tests and the code under test are the same files Unity compiles, so the
harness cannot drift from the project — if a test is added or a source file changes, this runner
picks it up automatically.

The project lives outside `Assets/`, so Unity never compiles it and it cannot affect a real build.

```
Tools/HeadlessTests/
├── MechanicScope.HeadlessTests.csproj   # lists the sources + tests to compile
├── UnityShim/
│   ├── UnityEngineShim.cs               # GameObject, MonoBehaviour, Transform, Debug, ...
│   ├── JsonUtilityShim.cs               # JsonUtility with Unity's field-binding semantics
│   └── GltfastShim.cs                   # glTFast placeholder (no model loading headlessly)
└── README.md
```

## What the shim guarantees

The shim reproduces the Unity behaviour the tested code actually depends on:

- `AddComponent<T>()` invokes `Awake()` immediately, and `DestroyImmediate` invokes `OnDestroy()`.
- `Object`'s `==` operator treats a destroyed object as equal to `null`, like Unity's.
- `JsonUtility` binds to **fields** (not properties), honours `[SerializeField]` and
  `[NonSerialized]`, ignores unknown keys, leaves absent keys at their default, and throws
  `ArgumentException` on malformed JSON.
- `Application.dataPath` / `streamingAssetsPath` resolve to the project's real `Assets/` folder, as
  they do in the Unity Editor, so tests that read shipped data files behave identically in both.
- `Application.persistentDataPath` points at a temp directory, so tests never write into the repo.

## What it does not cover

This is a fast check of engine-independent logic — procedure sequencing, JSON parsing, part lookup,
path-traversal validation, and shipped-data integrity. It is **not** a replacement for running the
suite in Unity.

Deliberately out of scope:

- **Rendering.** Materials, shaders and renderers are value-holding stubs; nothing draws.
- **Coroutine timing.** `StartCoroutine` runs the body up to its first `yield` and parks it, because
  the harness has no frame loop. No current test depends on a coroutine completing.
- **AR, glTF model loading, and anything in `Assets/Scripts/UI`, `Voice`, `Performance`, or
  `Accessibility`** — none of which have edit-mode tests today.

Unity remains the authority on Unity-specific behaviour:
**Window > General > Test Runner > EditMode > Run All**.

## Adding tests

Write them in `Assets/Tests/EditMode` as normal NUnit tests — the harness compiles that whole
directory, so no registration step is needed. If a new test touches a Unity API the shim does not
implement yet, the build fails with a clear `CS1061`/`CS0246`; add the member to `UnityShim/`,
keeping its behaviour faithful to Unity's.
