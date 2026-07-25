// Additional UnityEngine types needed to compile the UI, Voice, Performance and Accessibility
// modules headlessly. Same rule as the rest of the shim: match Unity's shape, do nothing at
// runtime, and report "unavailable" for anything that needs real hardware.

using System;
using System.Collections.Generic;

namespace UnityEngine
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    public class RequireComponent : Attribute
    {
        public RequireComponent(Type requiredComponent) { }
        public RequireComponent(Type requiredComponent, Type requiredComponent2) { }
    }

    [AttributeUsage(AttributeTargets.Class)]
    public class AddComponentMenu : Attribute
    {
        public AddComponentMenu(string menuName) { }
    }

    [AttributeUsage(AttributeTargets.Class)]
    public class DisallowMultipleComponent : Attribute { }

    [AttributeUsage(AttributeTargets.Class)]
    public class ExecuteInEditMode : Attribute { }

    public struct Rect
    {
        public float x, y, width, height;
        public Rect(float x, float y, float width, float height)
        {
            this.x = x; this.y = y; this.width = width; this.height = height;
        }
        public static Rect zero => new Rect(0f, 0f, 0f, 0f);
        public Vector2 position => new Vector2(x, y);
        public Vector2 size => new Vector2(width, height);
        public Vector2 center => new Vector2(x + width / 2f, y + height / 2f);
        public bool Contains(Vector2 point) =>
            point.x >= x && point.x < x + width && point.y >= y && point.y < y + height;
    }

    public struct Bounds
    {
        public Vector3 center { get; set; }
        public Vector3 extents { get; set; }

        public Bounds(Vector3 center, Vector3 size)
        {
            this.center = center;
            extents = size * 0.5f;
        }

        public Vector3 size { get => extents * 2f; set => extents = value * 0.5f; }
        public Vector3 min => center - extents;
        public Vector3 max => center + extents;

        public void Encapsulate(Vector3 point) { }
        public void Encapsulate(Bounds bounds) { }
        public bool Contains(Vector3 point) => false;
    }

    public struct Pose
    {
        public Vector3 position;
        public Quaternion rotation;
        public Pose(Vector3 position, Quaternion rotation)
        {
            this.position = position; this.rotation = rotation;
        }
        public static Pose identity => new Pose(Vector3.zero, Quaternion.identity);
    }

    public struct Ray
    {
        public Vector3 origin { get; set; }
        public Vector3 direction { get; set; }
        public Ray(Vector3 origin, Vector3 direction) { this.origin = origin; this.direction = direction; }
    }

    public struct RaycastHit
    {
        public Vector3 point { get; set; }
        public Vector3 normal { get; set; }
        public float distance { get; set; }
        public Collider collider { get; set; }
        public Transform transform { get; set; }
    }

    public static class Physics
    {
        public static bool Raycast(Ray ray, out RaycastHit hitInfo, float maxDistance = Mathf.Infinity)
        {
            hitInfo = default;
            return false;
        }

        public static bool Raycast(Vector3 origin, Vector3 direction, out RaycastHit hitInfo,
                                   float maxDistance = Mathf.Infinity)
        {
            hitInfo = default;
            return false;
        }
    }

    public class RectTransform : Transform
    {
        public Vector2 anchoredPosition { get; set; }
        public Vector2 sizeDelta { get; set; }
        public Vector2 anchorMin { get; set; }
        public Vector2 anchorMax { get; set; }
        public Vector2 pivot { get; set; } = new Vector2(0.5f, 0.5f);
        public Vector2 offsetMin { get; set; }
        public Vector2 offsetMax { get; set; }
        public Rect rect => new Rect(0f, 0f, sizeDelta.x, sizeDelta.y);

        public void SetSizeWithCurrentAnchors(Axis axis, float size) { }
        public enum Axis { Horizontal, Vertical }
    }

    public class Canvas : Behaviour
    {
        public enum RenderMode { ScreenSpaceOverlay, ScreenSpaceCamera, WorldSpace }

        public RenderMode renderMode { get; set; }
        public Camera worldCamera { get; set; }
        public int sortingOrder { get; set; }
        public float scaleFactor => 1f;
    }

    public class CanvasGroup : Behaviour
    {
        public float alpha { get; set; } = 1f;
        public bool interactable { get; set; } = true;
        public bool blocksRaycasts { get; set; } = true;
        public bool ignoreParentGroups { get; set; }
    }

    public enum FilterMode { Point, Bilinear, Trilinear }
    public enum TextureWrapMode { Repeat, Clamp, Mirror, MirrorOnce }
    public enum TextureFormat { Alpha8, RGB24, RGBA32, ARGB32, BGRA32, RGB565, DXT1, DXT5 }

    public class Sprite : Object
    {
        public Rect rect { get; private set; }
        public Texture2D texture { get; private set; }
        public Vector2 pivot { get; private set; }

        public static Sprite Create(Texture2D texture, Rect rect, Vector2 pivot,
                                    float pixelsPerUnit = 100f)
        {
            return new Sprite { texture = texture, rect = rect, pivot = pivot };
        }
    }

    public class AudioClip : Object
    {
        public int samples => 0;
        public int channels => 1;
        public int frequency => 44100;
        public float length => 0f;
        public bool GetData(float[] data, int offsetSamples) => false;
    }

    public class AudioSource : Behaviour
    {
        public AudioClip clip { get; set; }
        public float volume { get; set; } = 1f;
        public float pitch { get; set; } = 1f;
        public bool loop { get; set; }
        public bool playOnAwake { get; set; } = true;
        public bool isPlaying => false;

        public void Play() { }
        public void Stop() { }
        public void Pause() { }
        public void PlayOneShot(AudioClip clip, float volumeScale = 1f) { }
    }

    public class Animator : Behaviour
    {
        public float speed { get; set; } = 1f;
        public void SetTrigger(string name) { }
        public void SetBool(string name, bool value) { }
        public void SetFloat(string name, float value) { }
        public void SetInteger(string name, int value) { }
        public void Play(string stateName) { }
    }

    public class LineRenderer : Renderer
    {
        public int positionCount { get; set; }
        public float startWidth { get; set; }
        public float endWidth { get; set; }
        public Color startColor { get; set; }
        public Color endColor { get; set; }
        public bool loop { get; set; }
        public bool useWorldSpace { get; set; } = true;

        public void SetPosition(int index, Vector3 position) { }
        public Vector3 GetPosition(int index) => Vector3.zero;
        public void SetPositions(Vector3[] positions) { }
    }

    public class Camera : Behaviour
    {
        public static Camera main => null;
        public static Camera current => null;
        public static int allCamerasCount => 0;

        public float fieldOfView { get; set; } = 60f;
        public float nearClipPlane { get; set; } = 0.3f;
        public float farClipPlane { get; set; } = 1000f;
        public Color backgroundColor { get; set; }

        public Ray ScreenPointToRay(Vector3 position) => new Ray(Vector3.zero, Vector3.forward);
        public Vector3 ScreenToWorldPoint(Vector3 position) => Vector3.zero;
        public Vector3 WorldToScreenPoint(Vector3 position) => Vector3.zero;
        public Vector3 WorldToViewportPoint(Vector3 position) => Vector3.zero;
        public Vector3 ViewportToWorldPoint(Vector3 position) => Vector3.zero;
    }

    public class GUIStyleState
    {
        public Color textColor { get; set; }
        public Texture2D background { get; set; }
    }

    public class GUIStyle
    {
        public int fontSize { get; set; }
        public GUIStyleState normal { get; set; } = new GUIStyleState();
        public GUIStyleState hover { get; set; } = new GUIStyleState();
        public GUIStyle() { }
        public GUIStyle(GUIStyle other) { }
    }

    public class GUIContent
    {
        public string text { get; set; }
        public GUIContent() { }
        public GUIContent(string text) { this.text = text; }
    }

    /// <summary>IMGUI draws nothing headlessly; these exist so OnGUI methods compile.</summary>
    public static class GUI
    {
        public static Color color { get; set; } = Color.white;
        public static GUIStyle skin { get; set; }

        public static void Label(Rect position, string text) { }
        public static void Label(Rect position, string text, GUIStyle style) { }
        public static void Box(Rect position, string text) { }
        public static void Box(Rect position, string text, GUIStyle style) { }
        public static bool Button(Rect position, string text) => false;
        public static void DrawTexture(Rect position, Texture image) { }
    }

    public static class GUILayout
    {
        public static void Label(string text) { }
        public static bool Button(string text) => false;
    }

    public static class GUIUtility
    {
        public static string systemCopyBuffer { get; set; } = "";
        public static Vector2 ScreenToGUIPoint(Vector2 screenPoint) => screenPoint;
        public static Vector2 GUIToScreenPoint(Vector2 guiPoint) => guiPoint;
    }

    public static class RectTransformUtility
    {
        public static bool ScreenPointToLocalPointInRectangle(
            RectTransform rect, Vector2 screenPoint, Camera cam, out Vector2 localPoint)
        {
            localPoint = Vector2.zero;
            return false;
        }

        public static bool RectangleContainsScreenPoint(RectTransform rect, Vector2 screenPoint, Camera cam) => false;
    }

    // === Input ===

    public enum TouchPhase { Began, Moved, Stationary, Ended, Canceled }

    public struct Touch
    {
        public int fingerId { get; set; }
        public Vector2 position { get; set; }
        public Vector2 deltaPosition { get; set; }
        public float deltaTime { get; set; }
        public int tapCount { get; set; }
        public TouchPhase phase { get; set; }
    }

    public enum KeyCode
    {
        None = 0, Backspace = 8, Tab = 9, Return = 13, Escape = 27, Space = 32,
        Alpha0 = 48, Alpha1, Alpha2, Alpha3, Alpha4, Alpha5, Alpha6, Alpha7, Alpha8, Alpha9,
        A = 97, B, C, D, E, F, G, H, I, J, K, L, M, N, O, P, Q, R, S, T, U, V, W, X, Y, Z,
        UpArrow = 273, DownArrow = 274, RightArrow = 275, LeftArrow = 276,
        F1 = 282, F2 = 283, F3 = 284, F4 = 285, F5 = 286, F6 = 287,
        F7 = 288, F8 = 289, F9 = 290, F10 = 291, F11 = 292, F12 = 293,
        LeftShift = 304, RightShift = 303, LeftControl = 306, RightControl = 305
    }

    /// <summary>
    /// No input device exists headlessly, so every query reports "nothing happened".
    /// </summary>
    public static class Input
    {
        public static int touchCount => 0;
        public static Touch[] touches => new Touch[0];
        public static Touch GetTouch(int index) => default;

        public static Vector3 mousePosition => Vector3.zero;
        public static bool GetMouseButton(int button) => false;
        public static bool GetMouseButtonDown(int button) => false;
        public static bool GetMouseButtonUp(int button) => false;

        public static bool GetKey(KeyCode key) => false;
        public static bool GetKeyDown(KeyCode key) => false;
        public static bool GetKeyUp(KeyCode key) => false;

        public static float GetAxis(string axisName) => 0f;
        public static bool touchSupported => false;
        public static Gyroscope gyro => null;
    }

    public class Gyroscope
    {
        public bool enabled { get; set; }
        public Vector3 rotationRateUnbiased => Vector3.zero;
        public Quaternion attitude => Quaternion.identity;
    }

    public static class Screen
    {
        public static int width => 1080;
        public static int height => 1920;
        public static float dpi => 320f;
        public static bool sleepTimeout { get; set; }
        public static ScreenOrientation orientation { get; set; }
    }

    public enum ScreenOrientation { Portrait, PortraitUpsideDown, LandscapeLeft, LandscapeRight, AutoRotation }

    public static class SystemInfo
    {
        public static string deviceModel => "HeadlessTestRunner";
        public static string deviceName => "headless";
        public static string operatingSystem => Environment.OSVersion.ToString();
        public static int systemMemorySize => 4096;
        public static int graphicsMemorySize => 1024;
        public static int processorCount => Environment.ProcessorCount;
        public static bool supportsGyroscope => false;
        public static bool supportsAccelerometer => false;
        public static bool supportsVibration => false;
        public static float batteryLevel => -1f;
        public static BatteryStatus batteryStatus => BatteryStatus.Unknown;
    }

    public enum BatteryStatus { Unknown, Charging, Discharging, NotCharging, Full }

    public static class Handheld
    {
        public static void Vibrate() { }
    }

    public static class Microphone
    {
        public static string[] devices => new string[0];

        public static AudioClip Start(string deviceName, bool loop, int lengthSec, int frequency) => null;
        public static void End(string deviceName) { }
        public static bool IsRecording(string deviceName) => false;
        public static int GetPosition(string deviceName) => 0;
        public static void GetDeviceCaps(string deviceName, out int minFreq, out int maxFreq)
        {
            minFreq = 0;
            maxFreq = 0;
        }
    }

    public static class QualitySettings
    {
        public static int vSyncCount { get; set; }
        public static int GetQualityLevel() => 0;
        public static void SetQualityLevel(int index) { }
    }
}

namespace UnityEngine.SceneManagement
{
    public struct Scene
    {
        public string name { get; set; }
        public int buildIndex { get; set; }
        public bool isLoaded { get; set; }
        public bool IsValid() => true;
    }

    public enum LoadSceneMode { Single, Additive }

    public static class SceneManager
    {
        public static Scene GetActiveScene() => new Scene { name = "HeadlessTestScene", isLoaded = true };
        public static int sceneCount => 1;
        public static void LoadScene(string sceneName) { }
        public static void LoadScene(int buildIndex) { }
        public static void LoadScene(string sceneName, LoadSceneMode mode) { }
    }
}

namespace UnityEngine.Profiling
{
    public static class Profiler
    {
        public static long GetTotalAllocatedMemoryLong() => 0L;
        public static long GetTotalReservedMemoryLong() => 0L;
        public static long GetTotalUnusedReservedMemoryLong() => 0L;
        public static long GetMonoUsedSizeLong() => 0L;
        public static long GetMonoHeapSizeLong() => 0L;
        public static void BeginSample(string name) { }
        public static void EndSample() { }
    }
}

namespace UnityEngine.Networking
{
    public class DownloadHandler : IDisposable
    {
        public byte[] data => new byte[0];
        public string text => "";
        public void Dispose() { }
    }

    public class DownloadHandlerBuffer : DownloadHandler { }

    public class DownloadHandlerTexture : DownloadHandler
    {
        public Texture2D texture => null;
        public static Texture2D GetContent(UnityWebRequest request) => null;
    }

    public static class UnityWebRequestTexture
    {
        public static UnityWebRequest GetTexture(string uri) => new UnityWebRequest(uri);
    }

    public class UploadHandler : IDisposable
    {
        public void Dispose() { }
    }

    public class UnityWebRequestAsyncOperation : YieldInstruction
    {
        public bool isDone => true;
        public float progress => 1f;
    }

    /// <summary>
    /// The project uses UnityWebRequest only to read local files via file:// URIs. Headlessly this
    /// always reports a connection error rather than pretending to have fetched anything.
    /// </summary>
    public class UnityWebRequest : IDisposable
    {
        public enum Result { InProgress, Success, ConnectionError, ProtocolError, DataProcessingError }

        public string url { get; set; }
        public string error => "UnityWebRequest is not available in the headless test harness";
        public Result result => Result.ConnectionError;
        public bool isDone => true;
        public long responseCode => 0;
        public DownloadHandler downloadHandler { get; set; } = new DownloadHandlerBuffer();
        public UploadHandler uploadHandler { get; set; }

        public UnityWebRequest() { }
        public UnityWebRequest(string url) { this.url = url; }

        public static UnityWebRequest Get(string uri) => new UnityWebRequest(uri);
        public static UnityWebRequest Post(string uri, string postData) => new UnityWebRequest(uri);

        public UnityWebRequestAsyncOperation SendWebRequest() => new UnityWebRequestAsyncOperation();
        public void Abort() { }
        public void Dispose() { }
    }
}
