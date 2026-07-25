// Minimal UnityEngine stand-in used ONLY by the headless test harness.
//
// This is never compiled into the Unity project (it lives outside Assets/). It exists so the
// real gameplay sources under Assets/Scripts/Core can be compiled and executed by `dotnet test`
// without the Unity Editor.
//
// Scope rule: shim only what the code under test touches, and keep the observable behaviour
// identical to Unity's for the paths the tests exercise. Anything rendering-related is a no-op
// container that stores values, because no test asserts on rendering.

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;

namespace UnityEngine
{
    // === Attributes (compile-time only in Unity too) ===

    [AttributeUsage(AttributeTargets.Field)]
    public class SerializeField : Attribute { }

    [AttributeUsage(AttributeTargets.Field)]
    public class HideInInspector : Attribute { }

    [AttributeUsage(AttributeTargets.Field)]
    public class HeaderAttribute : Attribute
    {
        public readonly string header;
        public HeaderAttribute(string header) { this.header = header; }
    }

    [AttributeUsage(AttributeTargets.Field)]
    public class TooltipAttribute : Attribute
    {
        public readonly string tooltip;
        public TooltipAttribute(string tooltip) { this.tooltip = tooltip; }
    }

    [AttributeUsage(AttributeTargets.Field)]
    public class RangeAttribute : Attribute
    {
        public readonly float min, max;
        public RangeAttribute(float min, float max) { this.min = min; this.max = max; }
    }

    [AttributeUsage(AttributeTargets.Field)]
    public class TextAreaAttribute : Attribute
    {
        public TextAreaAttribute() { }
        public TextAreaAttribute(int minLines, int maxLines) { }
    }

    // === Math / value types ===

    public static class Mathf
    {
        public const float PI = (float)Math.PI;
        public const float Infinity = float.PositiveInfinity;
        public const float Epsilon = float.Epsilon;

        public static float Clamp(float v, float min, float max) => v < min ? min : (v > max ? max : v);
        public static float Clamp01(float v) => Clamp(v, 0f, 1f);
        public static float Lerp(float a, float b, float t) => a + (b - a) * Clamp01(t);
        public static float Abs(float v) => Math.Abs(v);
        public static float Min(float a, float b) => Math.Min(a, b);
        public static float Max(float a, float b) => Math.Max(a, b);
        public static float Sin(float v) => (float)Math.Sin(v);
        public static float Cos(float v) => (float)Math.Cos(v);
        public static float Sqrt(float v) => (float)Math.Sqrt(v);
        public static float Pow(float f, float p) => (float)Math.Pow(f, p);
        public static float Round(float v) => (float)Math.Round(v);
        public static float Floor(float v) => (float)Math.Floor(v);
        public static float Ceil(float v) => (float)Math.Ceiling(v);
        public static int RoundToInt(float v) => (int)Math.Round(v);
        public static int FloorToInt(float v) => (int)Math.Floor(v);
        public static int CeilToInt(float v) => (int)Math.Ceiling(v);
        public static bool Approximately(float a, float b) => Math.Abs(b - a) < 1e-6f;
    }

    public struct Vector2
    {
        public float x, y;
        public Vector2(float x, float y) { this.x = x; this.y = y; }
        public static Vector2 zero => new Vector2(0f, 0f);
        public static Vector2 one => new Vector2(1f, 1f);
        public override string ToString() => $"({x}, {y})";
    }

    public struct Vector3
    {
        public float x, y, z;
        public Vector3(float x, float y, float z) { this.x = x; this.y = y; this.z = z; }
        public Vector3(float x, float y) : this(x, y, 0f) { }

        public static Vector3 zero => new Vector3(0f, 0f, 0f);
        public static Vector3 one => new Vector3(1f, 1f, 1f);
        public static Vector3 up => new Vector3(0f, 1f, 0f);
        public static Vector3 down => new Vector3(0f, -1f, 0f);
        public static Vector3 left => new Vector3(-1f, 0f, 0f);
        public static Vector3 right => new Vector3(1f, 0f, 0f);
        public static Vector3 forward => new Vector3(0f, 0f, 1f);
        public static Vector3 back => new Vector3(0f, 0f, -1f);

        public float magnitude => Mathf.Sqrt(x * x + y * y + z * z);
        public float sqrMagnitude => x * x + y * y + z * z;

        public static Vector3 operator +(Vector3 a, Vector3 b) => new Vector3(a.x + b.x, a.y + b.y, a.z + b.z);
        public static Vector3 operator -(Vector3 a, Vector3 b) => new Vector3(a.x - b.x, a.y - b.y, a.z - b.z);
        public static Vector3 operator *(Vector3 a, float d) => new Vector3(a.x * d, a.y * d, a.z * d);
        public static Vector3 operator *(float d, Vector3 a) => a * d;
        public static Vector3 operator /(Vector3 a, float d) => new Vector3(a.x / d, a.y / d, a.z / d);

        public static float Distance(Vector3 a, Vector3 b) => (a - b).magnitude;
        public static Vector3 Lerp(Vector3 a, Vector3 b, float t) =>
            new Vector3(Mathf.Lerp(a.x, b.x, t), Mathf.Lerp(a.y, b.y, t), Mathf.Lerp(a.z, b.z, t));

        public override string ToString() => $"({x}, {y}, {z})";
    }

    public struct Quaternion
    {
        public float x, y, z, w;
        public Quaternion(float x, float y, float z, float w) { this.x = x; this.y = y; this.z = z; this.w = w; }
        public static Quaternion identity => new Quaternion(0f, 0f, 0f, 1f);
        public static Quaternion Euler(float x, float y, float z) => identity;
        public static Quaternion Euler(Vector3 euler) => identity;
        public Vector3 eulerAngles { get => Vector3.zero; set { } }
    }

    public struct Color
    {
        public float r, g, b, a;
        public Color(float r, float g, float b, float a) { this.r = r; this.g = g; this.b = b; this.a = a; }
        public Color(float r, float g, float b) : this(r, g, b, 1f) { }

        public static Color white => new Color(1f, 1f, 1f, 1f);
        public static Color black => new Color(0f, 0f, 0f, 1f);
        public static Color clear => new Color(0f, 0f, 0f, 0f);
        public static Color red => new Color(1f, 0f, 0f, 1f);
        public static Color green => new Color(0f, 1f, 0f, 1f);
        public static Color blue => new Color(0f, 0f, 1f, 1f);
        public static Color yellow => new Color(1f, 0.92f, 0.016f, 1f);
        public static Color gray => new Color(0.5f, 0.5f, 0.5f, 1f);

        public override string ToString() => $"RGBA({r}, {g}, {b}, {a})";
    }

    // === Object model ===

    public class Object
    {
        private static int nextInstanceId = 1;
        private readonly int instanceId = System.Threading.Interlocked.Increment(ref nextInstanceId);

        public string name { get; set; } = "";
        internal bool destroyed;

        public int GetInstanceID() => instanceId;

        public static void Destroy(Object obj) => DestroyImmediate(obj);

        public static void DestroyImmediate(Object obj)
        {
            if (obj == null || obj.destroyed) return;
            obj.destroyed = true;

            if (obj is GameObject go)
            {
                go.NotifyDestroyed();
            }
        }

        // Unity overloads == so a destroyed object compares equal to null. The code under test
        // relies on this (`if (highlightController != null)`), so the shim reproduces it.
        public static bool operator ==(Object a, Object b)
        {
            bool aNull = ReferenceEquals(a, null) || a.destroyed;
            bool bNull = ReferenceEquals(b, null) || b.destroyed;
            if (aNull && bNull) return true;
            if (aNull || bNull) return false;
            return ReferenceEquals(a, b);
        }

        public static bool operator !=(Object a, Object b) => !(a == b);
        public override bool Equals(object other) => ReferenceEquals(this, other);
        public override int GetHashCode() => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(this);
        public override string ToString() => name;
    }

    public class Component : Object
    {
        public GameObject gameObject { get; internal set; }
        public Transform transform => gameObject.transform;

        public T GetComponent<T>() where T : Component => gameObject.GetComponent<T>();
        public T[] GetComponents<T>() where T : Component => gameObject.GetComponents<T>();
        public T GetComponentInChildren<T>() where T : Component => gameObject.GetComponentInChildren<T>();
        public T[] GetComponentsInChildren<T>() where T : Component => gameObject.GetComponentsInChildren<T>();
        public T AddComponent<T>() where T : Component, new() => gameObject.AddComponent<T>();
    }

    public class Behaviour : Component
    {
        public bool enabled { get; set; } = true;
        public bool isActiveAndEnabled => enabled && !destroyed;
    }

    /// <summary>
    /// Coroutine handle. The harness drives coroutines synchronously (see StartCoroutine).
    /// </summary>
    public class Coroutine
    {
        internal IEnumerator Routine;
        internal bool Stopped;
    }

    public class YieldInstruction { }

    public class WaitForSeconds : YieldInstruction
    {
        public readonly float seconds;
        public WaitForSeconds(float seconds) { this.seconds = seconds; }
    }

    public class WaitForEndOfFrame : YieldInstruction { }
    public class WaitForFixedUpdate : YieldInstruction { }

    public class WaitUntil : YieldInstruction
    {
        public readonly Func<bool> predicate;
        public WaitUntil(Func<bool> predicate) { this.predicate = predicate; }
    }

    public class MonoBehaviour : Behaviour
    {
        /// <summary>
        /// Starts a coroutine. Unity resumes these across frames; in the harness there are no
        /// frames, so the routine is advanced only to its first yield and then parked. Tests must
        /// not depend on coroutine bodies completing — none of the current tests do. Advancing to
        /// the first yield matches Unity, which runs a coroutine's prologue synchronously.
        /// </summary>
        public Coroutine StartCoroutine(IEnumerator routine)
        {
            var coroutine = new Coroutine { Routine = routine };
            if (routine != null)
            {
                try
                {
                    routine.MoveNext();
                }
                catch (Exception e)
                {
                    Debug.LogError($"Coroutine threw: {e.Message}");
                }
            }
            return coroutine;
        }

        public Coroutine StartCoroutine(string methodName) => new Coroutine();

        public void StopCoroutine(Coroutine coroutine)
        {
            if (coroutine != null) coroutine.Stopped = true;
        }

        public void StopCoroutine(IEnumerator routine) { }
        public void StopAllCoroutines() { }

        public void Invoke(string methodName, float time) { }
        public void CancelInvoke() { }
    }

    public class Transform : Component, IEnumerable
    {
        private readonly List<Transform> children = new List<Transform>();

        public Transform parent { get; private set; }
        public int childCount => children.Count;

        public Vector3 position { get; set; }
        public Vector3 localPosition { get; set; }
        public Vector3 localScale { get; set; } = Vector3.one;
        public Vector3 localEulerAngles { get; set; }
        public Vector3 eulerAngles { get; set; }
        public Quaternion rotation { get; set; } = Quaternion.identity;
        public Quaternion localRotation { get; set; } = Quaternion.identity;

        public void SetParent(Transform newParent) => SetParent(newParent, true);

        public void SetParent(Transform newParent, bool worldPositionStays)
        {
            parent?.children.Remove(this);
            parent = newParent;
            newParent?.children.Add(this);
        }

        public Transform GetChild(int index) => children[index];

        public Transform Find(string childName) => children.Find(c => c.name == childName);

        // Unity's Transform enumerates its direct children.
        public IEnumerator GetEnumerator() => children.GetEnumerator();

        internal IEnumerable<Transform> SelfAndDescendants()
        {
            yield return this;
            foreach (Transform child in children)
            {
                foreach (Transform t in child.SelfAndDescendants())
                {
                    yield return t;
                }
            }
        }
    }

    public class GameObject : Object
    {
        private readonly List<Component> components = new List<Component>();

        public Transform transform { get; }
        public bool activeSelf { get; private set; } = true;
        public bool activeInHierarchy => activeSelf;
        public string tag { get; set; } = "Untagged";
        public int layer { get; set; }

        public GameObject() : this("GameObject") { }

        public GameObject(string name)
        {
            this.name = name;
            transform = new Transform { gameObject = this, name = name };
            components.Add(transform);
        }

        public void SetActive(bool value) => activeSelf = value;

        public T AddComponent<T>() where T : Component, new()
        {
            var component = new T { gameObject = this, name = typeof(T).Name };
            components.Add(component);

            // Unity invokes Awake() the moment a component is added (also in edit-mode tests).
            InvokeLifecycle(component, "Awake");
            return component;
        }

        public T GetComponent<T>() where T : Component
        {
            foreach (Component c in components)
            {
                if (c is T typed) return typed;
            }
            return null;
        }

        public T[] GetComponents<T>() where T : Component
        {
            var found = new List<T>();
            foreach (Component c in components)
            {
                if (c is T typed) found.Add(typed);
            }
            return found.ToArray();
        }

        public T GetComponentInChildren<T>() where T : Component
        {
            T[] all = GetComponentsInChildren<T>();
            return all.Length > 0 ? all[0] : null;
        }

        public T[] GetComponentsInChildren<T>() where T : Component
        {
            var found = new List<T>();
            foreach (Transform t in transform.SelfAndDescendants())
            {
                found.AddRange(t.gameObject.GetComponents<T>());
            }
            return found.ToArray();
        }

        internal void NotifyDestroyed()
        {
            foreach (Component c in components)
            {
                InvokeLifecycle(c, "OnDestroy");
                c.destroyed = true;
            }
        }

        private static void InvokeLifecycle(Component component, string methodName)
        {
            var method = component.GetType().GetMethod(
                methodName,
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.Public |
                System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.DeclaredOnly);

            if (method != null && method.GetParameters().Length == 0)
            {
                try
                {
                    method.Invoke(component, null);
                }
                catch (System.Reflection.TargetInvocationException e)
                {
                    Debug.LogError($"{methodName}() threw: {e.InnerException?.Message}");
                }
            }
        }

        public static GameObject Find(string name) => null;
        public static GameObject CreatePrimitive(PrimitiveType type) => new GameObject(type.ToString());
    }

    public enum PrimitiveType { Sphere, Capsule, Cylinder, Cube, Plane, Quad }

    // === Rendering placeholders ===
    //
    // No test asserts on rendering behaviour, so these store values and do nothing else.

    public class Shader : Object
    {
        private static readonly Dictionary<string, int> propertyIds = new Dictionary<string, int>();
        private static int nextPropertyId = 1;

        public static Shader Find(string name) => null;

        public static int PropertyToID(string name)
        {
            lock (propertyIds)
            {
                if (!propertyIds.TryGetValue(name, out int id))
                {
                    id = nextPropertyId++;
                    propertyIds[name] = id;
                }
                return id;
            }
        }
    }

    public class Texture : Object { }
    public class Texture2D : Texture
    {
        public Texture2D(int width, int height) { }
    }

    public class Material : Object
    {
        private readonly Dictionary<int, float> floats = new Dictionary<int, float>();
        private readonly Dictionary<int, Color> colors = new Dictionary<int, Color>();

        public Material() { }
        public Material(Shader shader) { }
        public Material(Material source) { }

        public Shader shader { get; set; }
        public Color color { get; set; } = Color.white;
        public Texture mainTexture { get; set; }

        public void SetFloat(int nameId, float value) => floats[nameId] = value;
        public void SetFloat(string name, float value) => floats[Shader.PropertyToID(name)] = value;
        public float GetFloat(int nameId) => floats.TryGetValue(nameId, out float v) ? v : 0f;
        public float GetFloat(string name) => GetFloat(Shader.PropertyToID(name));

        public void SetColor(int nameId, Color value) => colors[nameId] = value;
        public void SetColor(string name, Color value) => colors[Shader.PropertyToID(name)] = value;
        public Color GetColor(int nameId) => colors.TryGetValue(nameId, out Color v) ? v : Color.white;
        public Color GetColor(string name) => GetColor(Shader.PropertyToID(name));

        public void SetTexture(int nameId, Texture value) { }
        public void SetTexture(string name, Texture value) { }
        public bool HasProperty(int nameId) => true;
        public bool HasProperty(string name) => true;
        public void EnableKeyword(string keyword) { }
        public void DisableKeyword(string keyword) { }
        public void CopyPropertiesFromMaterial(Material source) { }
    }

    public class Renderer : Component
    {
        private Material[] materialArray = new Material[0];

        public Material material
        {
            get => materialArray.Length > 0 ? materialArray[0] : null;
            set => materialArray = new[] { value };
        }

        public Material sharedMaterial { get => material; set => material = value; }

        public Material[] materials { get => materialArray; set => materialArray = value ?? new Material[0]; }
        public Material[] sharedMaterials { get => materials; set => materials = value; }

        public bool enabled { get; set; } = true;
    }

    public class MeshRenderer : Renderer { }
    public class SkinnedMeshRenderer : Renderer { }

    public class Mesh : Object { }

    public class MeshFilter : Component
    {
        public Mesh mesh { get; set; }
        public Mesh sharedMesh { get; set; }
    }

    public class Collider : Component
    {
        public bool enabled { get; set; } = true;
        public bool isTrigger { get; set; }
    }

    public class MeshCollider : Collider
    {
        public Mesh sharedMesh { get; set; }
        public bool convex { get; set; }
    }

    public class BoxCollider : Collider { }

    public class TextAsset : Object
    {
        public string text { get; }
        public TextAsset() { text = ""; }
        public TextAsset(string text) { this.text = text; }
    }

    // === Engine services ===

    public static class Application
    {
        private static string persistent;
        private static string assets;

        /// <summary>
        /// Writable scratch directory owned by the test host, so code that writes save files
        /// (ProgressTracker) never touches the real project tree.
        /// </summary>
        public static string persistentDataPath
        {
            get
            {
                if (persistent == null)
                {
                    persistent = Path.Combine(Path.GetTempPath(), "MechanicScopeHeadlessTests", "persistent");
                    Directory.CreateDirectory(persistent);
                }
                return persistent;
            }
            set => persistent = value;
        }

        /// <summary>
        /// The project's Assets folder — matching what Unity reports in the Editor. Located by
        /// walking up from the test binary until an Assets/ directory turns up, so tests that read
        /// shipped data files behave identically here and in the Unity Editor.
        /// </summary>
        public static string dataPath
        {
            get
            {
                if (assets == null)
                {
                    var dir = new DirectoryInfo(AppContext.BaseDirectory);
                    while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "Assets")))
                    {
                        dir = dir.Parent;
                    }

                    if (dir == null)
                    {
                        throw new DirectoryNotFoundException(
                            "Headless shim could not locate the project's Assets/ folder above " +
                            AppContext.BaseDirectory);
                    }
                    assets = Path.Combine(dir.FullName, "Assets");
                }
                return assets;
            }
            set => assets = value;
        }

        public static string streamingAssetsPath => Path.Combine(dataPath, "StreamingAssets");
        public static bool isEditor => true;
        public static bool isPlaying => false;
        public static string version => "0.3.0";
        public static RuntimePlatform platform => RuntimePlatform.LinuxEditor;
    }

    public enum RuntimePlatform
    {
        OSXEditor, OSXPlayer, WindowsPlayer, WindowsEditor,
        IPhonePlayer, Android, LinuxPlayer, LinuxEditor
    }

    public static class Time
    {
        public static float time => 0f;
        public static float deltaTime => 0.016f;
        public static float unscaledDeltaTime => 0.016f;
        public static float realtimeSinceStartup => 0f;
        public static int frameCount => 0;
    }

    public static class Debug
    {
        /// <summary>Captured log lines, so a test could assert on them if it ever needs to.</summary>
        public static readonly List<string> LogLines = new List<string>();

        public static void Log(object message) => Record("LOG", message);
        public static void LogWarning(object message) => Record("WARN", message);
        public static void LogError(object message) => Record("ERROR", message);
        public static void LogException(Exception e) => Record("EXCEPTION", e?.Message);
        public static void Log(object message, Object context) => Record("LOG", message);
        public static void LogWarning(object message, Object context) => Record("WARN", message);
        public static void LogError(object message, Object context) => Record("ERROR", message);
        public static void Assert(bool condition) { }
        public static void Assert(bool condition, string message) { }

        private static void Record(string level, object message)
        {
            lock (LogLines)
            {
                LogLines.Add($"[{level}] {message}");
            }
        }
    }

    public static class PlayerPrefs
    {
        private static readonly Dictionary<string, object> store = new Dictionary<string, object>();

        public static void SetInt(string key, int value) => store[key] = value;
        public static void SetFloat(string key, float value) => store[key] = value;
        public static void SetString(string key, string value) => store[key] = value;

        public static int GetInt(string key, int def = 0) => store.TryGetValue(key, out object v) ? (int)v : def;
        public static float GetFloat(string key, float def = 0f) => store.TryGetValue(key, out object v) ? (float)v : def;
        public static string GetString(string key, string def = "") => store.TryGetValue(key, out object v) ? (string)v : def;

        public static bool HasKey(string key) => store.ContainsKey(key);
        public static void DeleteKey(string key) => store.Remove(key);
        public static void DeleteAll() => store.Clear();
        public static void Save() { }
    }

    public static class Resources
    {
        public static T Load<T>(string path) where T : Object => null;
        public static Object Load(string path) => null;
        public static void UnloadUnusedAssets() { }
    }
}
