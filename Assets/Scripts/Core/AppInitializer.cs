using System;
using System.Collections;
using UnityEngine;
using MechanicScope.Data;
using MechanicScope.Performance;
using MechanicScope.Accessibility;
using MechanicScope.Voice;

namespace MechanicScope.Core
{
    /// <summary>
    /// Main application initializer.
    /// Handles startup sequence, dependency initialization, and system checks.
    /// </summary>
    public class AppInitializer : MonoBehaviour
    {
        public static AppInitializer Instance { get; private set; }

        [Header("Core Systems")]
        [SerializeField] private bool initializeOnAwake = true;
        [SerializeField] private bool showSplashScreen = true;
        [SerializeField] private float minimumSplashTime = 2f;

        [Header("Dependencies")]
        [SerializeField] private DataManager dataManagerPrefab;
        [SerializeField] private LODManager lodManagerPrefab;
        [SerializeField] private PerformanceMonitor performanceMonitorPrefab;
        [SerializeField] private AccessibilityManager accessibilityManagerPrefab;

        [Header("Optional Systems")]
        [SerializeField] private bool enableVoiceCommands = true;
        [SerializeField] private bool enablePerformanceMonitoring = true;

        // Events
        public event Action OnInitializationStarted;
        public event Action<float> OnInitializationProgress;
        public event Action OnInitializationCompleted;
        public event Action<string> OnInitializationFailed;

        // State
        public bool IsInitialized { get; private set; }
        public bool IsInitializing { get; private set; }
        public InitializationState CurrentState { get; private set; }

        public enum InitializationState
        {
            NotStarted,
            CheckingRequirements,
            InitializingData,
            InitializingAR,
            InitializingPerformance,
            InitializingAccessibility,
            InitializingVoice,
            LoadingContent,
            Ready,
            Failed
        }

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);

                if (initializeOnAwake)
                {
                    StartCoroutine(InitializeAsync());
                }
            }
            else
            {
                Destroy(gameObject);
            }
        }

        /// <summary>
        /// Starts the initialization process.
        /// </summary>
        public void Initialize()
        {
            if (!IsInitializing && !IsInitialized)
            {
                StartCoroutine(InitializeAsync());
            }
        }

        private IEnumerator InitializeAsync()
        {
            IsInitializing = true;
            OnInitializationStarted?.Invoke();

            float startTime = Time.time;
            float progress = 0f;

            try
            {
                // Step 1: Check requirements
                CurrentState = InitializationState.CheckingRequirements;
                OnInitializationProgress?.Invoke(0.1f);
                yield return CheckSystemRequirements();
                progress = 0.15f;

                // Step 2: Initialize data layer
                CurrentState = InitializationState.InitializingData;
                OnInitializationProgress?.Invoke(0.2f);
                yield return InitializeDataLayer();
                progress = 0.35f;

                // Step 3: Initialize AR systems
                CurrentState = InitializationState.InitializingAR;
                OnInitializationProgress?.Invoke(0.4f);
                yield return InitializeARSystems();
                progress = 0.5f;

                // Step 4: Initialize performance systems
                if (enablePerformanceMonitoring)
                {
                    CurrentState = InitializationState.InitializingPerformance;
                    OnInitializationProgress?.Invoke(0.55f);
                    yield return InitializePerformanceSystems();
                }
                progress = 0.65f;

                // Step 5: Initialize accessibility
                CurrentState = InitializationState.InitializingAccessibility;
                OnInitializationProgress?.Invoke(0.7f);
                yield return InitializeAccessibility();
                progress = 0.8f;

                // Step 6: Initialize voice commands
                if (enableVoiceCommands)
                {
                    CurrentState = InitializationState.InitializingVoice;
                    OnInitializationProgress?.Invoke(0.85f);
                    yield return InitializeVoiceCommands();
                }
                progress = 0.9f;

                // Step 7: Load initial content
                CurrentState = InitializationState.LoadingContent;
                OnInitializationProgress?.Invoke(0.95f);
                yield return LoadInitialContent();

                // Ensure minimum splash time
                if (showSplashScreen)
                {
                    float elapsed = Time.time - startTime;
                    if (elapsed < minimumSplashTime)
                    {
                        yield return new WaitForSeconds(minimumSplashTime - elapsed);
                    }
                }

                // Complete
                CurrentState = InitializationState.Ready;
                IsInitialized = true;
                IsInitializing = false;
                OnInitializationProgress?.Invoke(1f);
                OnInitializationCompleted?.Invoke();

                Debug.Log("[AppInitializer] Initialization completed successfully");
            }
            catch (Exception e)
            {
                CurrentState = InitializationState.Failed;
                IsInitializing = false;
                OnInitializationFailed?.Invoke(e.Message);
                Debug.LogError($"[AppInitializer] Initialization failed: {e.Message}");
            }
        }

        private IEnumerator CheckSystemRequirements()
        {
            Debug.Log("[AppInitializer] Checking system requirements...");

            // Check AR support
            #if UNITY_IOS
            if (!UnityEngine.XR.ARSubsystems.LoaderUtility.GetActiveLoader()?.GetLoadedSubsystem<UnityEngine.XR.ARSubsystems.XRSessionSubsystem>()?.running ?? true)
            {
                Debug.LogWarning("AR may not be fully supported on this device");
            }
            #elif UNITY_ANDROID
            // Android AR check
            #endif

            // Check device capabilities
            if (SystemInfo.systemMemorySize < 2048)
            {
                Debug.LogWarning("Low memory device detected. Performance may be affected.");
            }

            if (!SystemInfo.supportsGyroscope)
            {
                Debug.LogWarning("Gyroscope not available. AR tracking may be limited.");
            }

            yield return null;
        }

        private IEnumerator InitializeDataLayer()
        {
            Debug.Log("[AppInitializer] Initializing data layer...");

            // Create DataManager if not exists
            if (DataManager.Instance == null)
            {
                if (dataManagerPrefab != null)
                {
                    Instantiate(dataManagerPrefab);
                }
                else
                {
                    GameObject go = new GameObject("DataManager");
                    go.AddComponent<DataManager>();
                }
            }

            // Wait for initialization
            yield return new WaitUntil(() => DataManager.Instance != null);
            yield return null;
        }

        private IEnumerator InitializeARSystems()
        {
            Debug.Log("[AppInitializer] Initializing AR systems...");

            // AR systems are typically set up in scene
            // Just ensure camera is available
            if (Camera.main == null)
            {
                Debug.LogWarning("Main camera not found. AR may not function properly.");
            }

            yield return null;
        }

        private IEnumerator InitializePerformanceSystems()
        {
            Debug.Log("[AppInitializer] Initializing performance systems...");

            // Create LODManager if not exists
            if (LODManager.Instance == null)
            {
                if (lodManagerPrefab != null)
                {
                    Instantiate(lodManagerPrefab);
                }
                else
                {
                    GameObject go = new GameObject("LODManager");
                    go.AddComponent<LODManager>();
                }
            }

            // Create PerformanceMonitor if not exists
            if (PerformanceMonitor.Instance == null)
            {
                if (performanceMonitorPrefab != null)
                {
                    Instantiate(performanceMonitorPrefab);
                }
                else
                {
                    GameObject go = new GameObject("PerformanceMonitor");
                    go.AddComponent<PerformanceMonitor>();
                }
            }

            yield return null;
        }

        private IEnumerator InitializeAccessibility()
        {
            Debug.Log("[AppInitializer] Initializing accessibility...");

            // Create AccessibilityManager if not exists
            if (AccessibilityManager.Instance == null)
            {
                if (accessibilityManagerPrefab != null)
                {
                    Instantiate(accessibilityManagerPrefab);
                }
                else
                {
                    GameObject go = new GameObject("AccessibilityManager");
                    go.AddComponent<AccessibilityManager>();
                }
            }

            yield return null;
        }

        private IEnumerator InitializeVoiceCommands()
        {
            Debug.Log("[AppInitializer] Initializing voice commands...");

            // Voice command systems are typically set up in scene
            // Just verify components exist

            VoiceCommandManager voiceManager = FindFirstObjectByType<VoiceCommandManager>();
            if (voiceManager == null)
            {
                Debug.Log("VoiceCommandManager not found. Voice commands disabled.");
            }

            yield return null;
        }

        private IEnumerator LoadInitialContent()
        {
            Debug.Log("[AppInitializer] Loading initial content...");

            // Pre-load any required assets
            // Load engine list, etc.

            yield return null;
        }

        /// <summary>
        /// Gets a human-readable status message.
        /// </summary>
        public string GetStatusMessage()
        {
            return CurrentState switch
            {
                InitializationState.NotStarted => "Preparing to start...",
                InitializationState.CheckingRequirements => "Checking device requirements...",
                InitializationState.InitializingData => "Loading data...",
                InitializationState.InitializingAR => "Setting up AR...",
                InitializationState.InitializingPerformance => "Optimizing performance...",
                InitializationState.InitializingAccessibility => "Configuring accessibility...",
                InitializationState.InitializingVoice => "Setting up voice commands...",
                InitializationState.LoadingContent => "Loading content...",
                InitializationState.Ready => "Ready",
                InitializationState.Failed => "Initialization failed",
                _ => "Loading..."
            };
        }
    }
}
