using System;
using System.Collections;
using UnityEngine;
using MechanicScope.Data;
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
        [SerializeField] private AccessibilityManager accessibilityManagerPrefab;

        [Header("Optional Systems")]
        [SerializeField] private bool enableVoiceCommands = true;

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

                // Step 4: Initialize accessibility
                CurrentState = InitializationState.InitializingAccessibility;
                OnInitializationProgress?.Invoke(0.6f);
                yield return InitializeAccessibility();
                progress = 0.7f;

                // Step 5: Initialize voice commands
                if (enableVoiceCommands)
                {
                    CurrentState = InitializationState.InitializingVoice;
                    OnInitializationProgress?.Invoke(0.8f);
                    yield return InitializeVoiceCommands();
                }
                progress = 0.85f;

                // Step 6: Load initial content
                CurrentState = InitializationState.LoadingContent;
                OnInitializationProgress?.Invoke(0.9f);
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

            if (Camera.main == null)
            {
                Debug.LogWarning("Main camera not found. AR may not function properly.");
            }

            // Wait for AR session to be ready (up to 5 seconds)
            var arSession = FindFirstObjectByType<UnityEngine.XR.ARFoundation.ARSession>();
            if (arSession != null)
            {
                float timeout = 5f;
                float elapsed = 0f;
                while (UnityEngine.XR.ARFoundation.ARSession.state < UnityEngine.XR.ARFoundation.ARSessionState.CheckingAvailability
                       && elapsed < timeout)
                {
                    elapsed += Time.deltaTime;
                    yield return null;
                }
                Debug.Log($"[AppInitializer] AR session state: {UnityEngine.XR.ARFoundation.ARSession.state}");
            }
            else
            {
                Debug.LogWarning("[AppInitializer] No ARSession found in scene.");
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

            // Trigger engine list scan so the selection UI is populated
            var modelLoader = FindFirstObjectByType<EngineModelLoader>();
            if (modelLoader != null)
            {
                modelLoader.RefreshEngineList();
                Debug.Log($"[AppInitializer] Found {modelLoader.AvailableEngines.Count} engines.");
            }

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
