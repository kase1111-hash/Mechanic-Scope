using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Profiling;

namespace MechanicScope.Performance
{
    /// <summary>
    /// Monitors and reports performance metrics.
    /// Provides real-time stats and performance warnings.
    /// </summary>
    public class PerformanceMonitor : MonoBehaviour
    {
        public static PerformanceMonitor Instance { get; private set; }

        [Header("Monitoring Settings")]
        [SerializeField] private bool enableMonitoring = true;
        [SerializeField] private float updateInterval = 0.5f;
        [SerializeField] private int historySize = 120; // 60 seconds at 0.5s interval

        [Header("Warning Thresholds")]
        [SerializeField] private float lowFPSThreshold = 30f;
        [SerializeField] private float highMemoryThresholdMB = 512f;
        [SerializeField] private float highBatteryDrainThreshold = 0.02f; // 2% per minute

        [Header("Debug Display")]
        [SerializeField] private bool showDebugOverlay = false;
        [SerializeField] private KeyCode toggleOverlayKey = KeyCode.F1;

        // Events
        public event Action<PerformanceWarning> OnPerformanceWarning;

        // Current metrics
        public float CurrentFPS { get; private set; }
        public float AverageFPS { get; private set; }
        public long UsedMemoryMB { get; private set; }
        public long TotalMemoryMB { get; private set; }
        public float BatteryLevel { get; private set; }
        public float CPUUsage { get; private set; }
        public int DrawCalls { get; private set; }
        public int Triangles { get; private set; }

        private List<float> fpsHistory = new List<float>();
        private List<long> memoryHistory = new List<long>();
        private float lastUpdateTime;
        private int frameCount;
        private float frameTimer;
        private float lastBatteryLevel;
        private float lastBatteryCheckTime;

        // GC tracking
        private long lastGCMemory;
        private int gcCount;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void Update()
        {
            if (!enableMonitoring) return;

            UpdateFrameMetrics();

            if (Time.unscaledTime - lastUpdateTime >= updateInterval)
            {
                lastUpdateTime = Time.unscaledTime;
                UpdateAllMetrics();
                CheckForWarnings();
            }

            #if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (Input.GetKeyDown(toggleOverlayKey))
            {
                showDebugOverlay = !showDebugOverlay;
            }
            #endif
        }

        private void OnGUI()
        {
            if (!showDebugOverlay) return;

            DrawDebugOverlay();
        }

        private void UpdateFrameMetrics()
        {
            frameCount++;
            frameTimer += Time.unscaledDeltaTime;
        }

        private void UpdateAllMetrics()
        {
            // FPS
            CurrentFPS = frameCount / frameTimer;
            frameCount = 0;
            frameTimer = 0;

            // Add to history
            fpsHistory.Add(CurrentFPS);
            if (fpsHistory.Count > historySize)
            {
                fpsHistory.RemoveAt(0);
            }

            // Calculate average
            float sum = 0;
            foreach (float fps in fpsHistory)
            {
                sum += fps;
            }
            AverageFPS = sum / fpsHistory.Count;

            // Memory
            UsedMemoryMB = Profiler.GetTotalAllocatedMemoryLong() / (1024 * 1024);
            TotalMemoryMB = SystemInfo.systemMemorySize;

            memoryHistory.Add(UsedMemoryMB);
            if (memoryHistory.Count > historySize)
            {
                memoryHistory.RemoveAt(0);
            }

            // GC tracking
            long currentGCMemory = GC.GetTotalMemory(false);
            if (currentGCMemory < lastGCMemory)
            {
                gcCount++;
            }
            lastGCMemory = currentGCMemory;

            // Battery
            BatteryLevel = SystemInfo.batteryLevel;
            if (BatteryLevel >= 0 && lastBatteryCheckTime > 0)
            {
                float timeDelta = Time.unscaledTime - lastBatteryCheckTime;
                if (timeDelta > 0)
                {
                    float batteryDrain = lastBatteryLevel - BatteryLevel;
                    // Normalize to per-minute drain
                    // (handled in warning check)
                }
            }
            lastBatteryLevel = BatteryLevel;
            lastBatteryCheckTime = Time.unscaledTime;

            // Rendering stats (only available in editor or development builds)
            #if UNITY_EDITOR
            // These would come from Unity's rendering stats
            #endif
        }

        private void CheckForWarnings()
        {
            // Low FPS warning
            if (CurrentFPS < lowFPSThreshold && CurrentFPS > 0)
            {
                OnPerformanceWarning?.Invoke(new PerformanceWarning
                {
                    type = WarningType.LowFrameRate,
                    message = $"Low frame rate: {CurrentFPS:F1} FPS",
                    severity = CurrentFPS < 20 ? WarningSeverity.Critical : WarningSeverity.Warning,
                    value = CurrentFPS
                });
            }

            // High memory warning
            if (UsedMemoryMB > highMemoryThresholdMB)
            {
                OnPerformanceWarning?.Invoke(new PerformanceWarning
                {
                    type = WarningType.HighMemory,
                    message = $"High memory usage: {UsedMemoryMB} MB",
                    severity = UsedMemoryMB > highMemoryThresholdMB * 1.5f ? WarningSeverity.Critical : WarningSeverity.Warning,
                    value = UsedMemoryMB
                });
            }

            // Memory leak detection (simple heuristic)
            if (memoryHistory.Count >= 10)
            {
                bool isIncreasing = true;
                for (int i = 1; i < 10; i++)
                {
                    if (memoryHistory[memoryHistory.Count - i] <= memoryHistory[memoryHistory.Count - i - 1])
                    {
                        isIncreasing = false;
                        break;
                    }
                }

                if (isIncreasing)
                {
                    OnPerformanceWarning?.Invoke(new PerformanceWarning
                    {
                        type = WarningType.MemoryLeak,
                        message = "Possible memory leak detected",
                        severity = WarningSeverity.Warning,
                        value = UsedMemoryMB
                    });
                }
            }
        }

        private void DrawDebugOverlay()
        {
            GUIStyle style = new GUIStyle();
            style.fontSize = 14;
            style.normal.textColor = Color.white;

            GUIStyle bgStyle = new GUIStyle();
            bgStyle.normal.background = MakeTexture(1, 1, new Color(0, 0, 0, 0.7f));

            float width = 220;
            float height = 180;
            float x = 10;
            float y = 10;

            GUI.Box(new Rect(x, y, width, height), "", bgStyle);

            y += 5;
            x += 5;

            // FPS
            Color fpsColor = CurrentFPS >= 55 ? Color.green : CurrentFPS >= 30 ? Color.yellow : Color.red;
            style.normal.textColor = fpsColor;
            GUI.Label(new Rect(x, y, width, 20), $"FPS: {CurrentFPS:F1} (avg: {AverageFPS:F1})", style);
            y += 20;

            // Memory
            style.normal.textColor = UsedMemoryMB < highMemoryThresholdMB ? Color.white : Color.yellow;
            GUI.Label(new Rect(x, y, width, 20), $"Memory: {UsedMemoryMB} MB / {TotalMemoryMB} MB", style);
            y += 20;

            // GC
            style.normal.textColor = Color.white;
            GUI.Label(new Rect(x, y, width, 20), $"GC Collections: {gcCount}", style);
            y += 20;

            // Battery
            if (BatteryLevel >= 0)
            {
                Color batteryColor = BatteryLevel > 0.2f ? Color.white : Color.red;
                style.normal.textColor = batteryColor;
                GUI.Label(new Rect(x, y, width, 20), $"Battery: {BatteryLevel * 100:F0}%", style);
                y += 20;
            }

            // LOD info
            if (LODManager.Instance != null)
            {
                var stats = LODManager.Instance.GetStats();
                style.normal.textColor = Color.white;
                GUI.Label(new Rect(x, y, width, 20), $"LOD Bias: {stats.lodBias:F2}", style);
                y += 20;
                GUI.Label(new Rect(x, y, width, 20), $"Managed Objects: {stats.managedObjects}", style);
                y += 20;
            }

            // Device info
            style.normal.textColor = Color.gray;
            GUI.Label(new Rect(x, y, width, 20), $"{SystemInfo.deviceModel}", style);
        }

        private Texture2D MakeTexture(int width, int height, Color color)
        {
            Color[] pixels = new Color[width * height];
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = color;
            }

            Texture2D texture = new Texture2D(width, height);
            texture.SetPixels(pixels);
            texture.Apply();
            return texture;
        }

        /// <summary>
        /// Gets a performance report.
        /// </summary>
        public PerformanceReport GetReport()
        {
            float minFPS = float.MaxValue;
            float maxFPS = 0;

            foreach (float fps in fpsHistory)
            {
                if (fps < minFPS) minFPS = fps;
                if (fps > maxFPS) maxFPS = fps;
            }

            long minMemory = long.MaxValue;
            long maxMemory = 0;

            foreach (long mem in memoryHistory)
            {
                if (mem < minMemory) minMemory = mem;
                if (mem > maxMemory) maxMemory = mem;
            }

            return new PerformanceReport
            {
                currentFPS = CurrentFPS,
                averageFPS = AverageFPS,
                minFPS = minFPS == float.MaxValue ? 0 : minFPS,
                maxFPS = maxFPS,
                currentMemoryMB = UsedMemoryMB,
                peakMemoryMB = maxMemory,
                gcCollections = gcCount,
                batteryLevel = BatteryLevel,
                timestamp = DateTime.Now
            };
        }

        /// <summary>
        /// Enables or disables the debug overlay.
        /// </summary>
        public void SetDebugOverlay(bool enabled)
        {
            showDebugOverlay = enabled;
        }

        /// <summary>
        /// Resets performance history.
        /// </summary>
        public void ResetHistory()
        {
            fpsHistory.Clear();
            memoryHistory.Clear();
            gcCount = 0;
        }
    }

    public enum WarningType
    {
        LowFrameRate,
        HighMemory,
        MemoryLeak,
        HighBatteryDrain,
        ThermalThrottling
    }

    public enum WarningSeverity
    {
        Info,
        Warning,
        Critical
    }

    public struct PerformanceWarning
    {
        public WarningType type;
        public string message;
        public WarningSeverity severity;
        public float value;
    }

    public struct PerformanceReport
    {
        public float currentFPS;
        public float averageFPS;
        public float minFPS;
        public float maxFPS;
        public long currentMemoryMB;
        public long peakMemoryMB;
        public int gcCollections;
        public float batteryLevel;
        public DateTime timestamp;
    }
}
