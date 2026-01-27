using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using MechanicScope.Core;

namespace MechanicScope.UI
{
    /// <summary>
    /// Main UI controller that manages app modes and screen transitions.
    /// </summary>
    public class MainUIController : MonoBehaviour
    {
        [Header("Core References")]
        [SerializeField] private ARAlignment arAlignment;
        [SerializeField] private EngineModelLoader modelLoader;
        [SerializeField] private ProcedureRunner procedureRunner;
        [SerializeField] private PartDatabase partDatabase;
        [SerializeField] private ProgressTracker progressTracker;

        [Header("UI Components")]
        [SerializeField] private EngineSelectionUI engineSelection;
        [SerializeField] private ProcedureSelectionUI procedureSelection;
        [SerializeField] private ProcedureCardUI procedureCard;
        [SerializeField] private PartInfoPopup partInfoPopup;
        [SerializeField] private AlignmentControlsUI alignmentControls;

        [Header("Screens")]
        [SerializeField] private GameObject splashScreen;
        [SerializeField] private GameObject engineSelectionScreen;
        [SerializeField] private GameObject alignmentScreen;
        [SerializeField] private GameObject procedureSelectionScreen;
        [SerializeField] private GameObject procedureActiveScreen;
        [SerializeField] private GameObject partInspectorScreen;
        [SerializeField] private GameObject settingsScreen;
        [SerializeField] private GameObject completionScreen;

        [Header("Header UI")]
        [SerializeField] private GameObject header;
        [SerializeField] private Button menuButton;
        [SerializeField] private Button settingsButton;
        [SerializeField] private TextMeshProUGUI headerTitle;

        [Header("Settings")]
        [SerializeField] private float splashDuration = 2f;

        // Events
        public event Action<AppMode> OnModeChanged;
        public event Action<string> OnEngineSelected;
        public event Action<string> OnProcedureSelected;

        // Properties
        public AppMode CurrentMode { get; private set; } = AppMode.Splash;
        public string SelectedEngineId { get; private set; }
        public string SelectedProcedureId { get; private set; }

        private void Start()
        {
            InitializeUI();
            SubscribeToEvents();

            // Show splash screen initially
            SetMode(AppMode.Splash);
            Invoke(nameof(OnSplashComplete), splashDuration);
        }

        private void OnDestroy()
        {
            // Cancel any pending invokes to prevent memory leaks and null references
            CancelInvoke(nameof(OnSplashComplete));
            UnsubscribeFromEvents();
        }

        private void InitializeUI()
        {
            // Setup header buttons
            if (menuButton != null)
                menuButton.onClick.AddListener(OnMenuClicked);
            if (settingsButton != null)
                settingsButton.onClick.AddListener(OnSettingsClicked);

            // Hide all screens initially
            HideAllScreens();
        }

        private void SubscribeToEvents()
        {
            if (modelLoader != null)
            {
                modelLoader.OnModelLoaded += OnModelLoaded;
                modelLoader.OnLoadError += OnModelLoadError;
            }

            if (procedureRunner != null)
            {
                procedureRunner.OnProcedureLoaded += OnProcedureLoaded;
                procedureRunner.OnProcedureCompleted += OnProcedureCompleted;
            }

            if (arAlignment != null)
            {
                arAlignment.OnAlignmentLocked += OnAlignmentLocked;
            }

            if (engineSelection != null)
            {
                engineSelection.OnEngineSelected += HandleEngineSelected;
            }

            if (procedureSelection != null)
            {
                procedureSelection.OnProcedureSelected += HandleProcedureSelected;
            }

            if (partInfoPopup != null)
            {
                partInfoPopup.OnDetailsRequested += OnPartDetailsRequested;
            }
        }

        private void UnsubscribeFromEvents()
        {
            if (modelLoader != null)
            {
                modelLoader.OnModelLoaded -= OnModelLoaded;
                modelLoader.OnLoadError -= OnModelLoadError;
            }

            if (procedureRunner != null)
            {
                procedureRunner.OnProcedureLoaded -= OnProcedureLoaded;
                procedureRunner.OnProcedureCompleted -= OnProcedureCompleted;
            }

            if (arAlignment != null)
            {
                arAlignment.OnAlignmentLocked -= OnAlignmentLocked;
            }

            if (engineSelection != null)
            {
                engineSelection.OnEngineSelected -= HandleEngineSelected;
            }

            if (procedureSelection != null)
            {
                procedureSelection.OnProcedureSelected -= HandleProcedureSelected;
            }

            if (partInfoPopup != null)
            {
                partInfoPopup.OnDetailsRequested -= OnPartDetailsRequested;
            }
        }

        private void OnSplashComplete()
        {
            SetMode(AppMode.ModelSelection);
        }

        /// <summary>
        /// Sets the current app mode and updates UI accordingly.
        /// </summary>
        public void SetMode(AppMode mode)
        {
            AppMode previousMode = CurrentMode;
            CurrentMode = mode;

            HideAllScreens();
            UpdateHeader(mode);

            switch (mode)
            {
                case AppMode.Splash:
                    ShowScreen(splashScreen);
                    SetHeaderVisible(false);
                    break;

                case AppMode.ModelSelection:
                    ShowScreen(engineSelectionScreen);
                    SetHeaderVisible(true);
                    SetHeaderTitle("Select Engine");
                    engineSelection?.RefreshList();
                    break;

                case AppMode.Alignment:
                    ShowScreen(alignmentScreen);
                    SetHeaderVisible(true);
                    SetHeaderTitle("Align Model");
                    alignmentControls?.Show();
                    break;

                case AppMode.ProcedureSelection:
                    ShowScreen(procedureSelectionScreen);
                    SetHeaderVisible(true);
                    SetHeaderTitle("Select Procedure");
                    procedureSelection?.LoadProcedures(SelectedEngineId);
                    break;

                case AppMode.ProcedureActive:
                    ShowScreen(procedureActiveScreen);
                    SetHeaderVisible(true);
                    SetHeaderTitle(procedureRunner?.CurrentProcedure?.name ?? "Procedure");
                    procedureCard?.Show();
                    break;

                case AppMode.PartInspection:
                    ShowScreen(partInspectorScreen);
                    SetHeaderVisible(true);
                    SetHeaderTitle("Part Details");
                    break;

                case AppMode.Settings:
                    ShowScreen(settingsScreen);
                    SetHeaderVisible(true);
                    SetHeaderTitle("Settings");
                    break;

                case AppMode.Completion:
                    ShowScreen(completionScreen);
                    SetHeaderVisible(true);
                    SetHeaderTitle("Complete!");
                    break;
            }

            OnModeChanged?.Invoke(mode);
        }

        private void HideAllScreens()
        {
            HideScreen(splashScreen);
            HideScreen(engineSelectionScreen);
            HideScreen(alignmentScreen);
            HideScreen(procedureSelectionScreen);
            HideScreen(procedureActiveScreen);
            HideScreen(partInspectorScreen);
            HideScreen(settingsScreen);
            HideScreen(completionScreen);
        }

        private void ShowScreen(GameObject screen)
        {
            if (screen != null)
                screen.SetActive(true);
        }

        private void HideScreen(GameObject screen)
        {
            if (screen != null)
                screen.SetActive(false);
        }

        private void UpdateHeader(AppMode mode)
        {
            bool showHeader = mode != AppMode.Splash;
            SetHeaderVisible(showHeader);
        }

        private void SetHeaderVisible(bool visible)
        {
            if (header != null)
                header.SetActive(visible);
        }

        private void SetHeaderTitle(string title)
        {
            if (headerTitle != null)
                headerTitle.text = title;
        }

        // Event handlers
        private void HandleEngineSelected(string engineId)
        {
            SelectedEngineId = engineId;
            OnEngineSelected?.Invoke(engineId);

            // Load the engine model
            modelLoader?.LoadEngine(engineId);
        }

        private void OnModelLoaded(GameObject model, EngineManifest manifest)
        {
            // Set the model in AR alignment
            arAlignment?.SetEngineModel(model, manifest.id);

            // Transition to alignment mode
            SetMode(AppMode.Alignment);
        }

        private void OnModelLoadError(string error)
        {
            Debug.LogError($"Failed to load model: {error}");
            // Show error UI
        }

        private void OnAlignmentLocked()
        {
            // After alignment is locked, show procedure selection
            SetMode(AppMode.ProcedureSelection);
        }

        private void HandleProcedureSelected(string procedureId)
        {
            SelectedProcedureId = procedureId;
            OnProcedureSelected?.Invoke(procedureId);

            // Load the procedure
            procedureRunner?.LoadProcedure(procedureId, SelectedEngineId);
        }

        private void OnProcedureLoaded(Procedure procedure)
        {
            SetMode(AppMode.ProcedureActive);
        }

        private void OnProcedureCompleted()
        {
            // Log the completed repair
            if (progressTracker != null && procedureRunner?.CurrentProcedure != null)
            {
                RepairLog log = new RepairLog
                {
                    ProcedureId = procedureRunner.CurrentProcedure.id,
                    EngineName = modelLoader?.CurrentEngine?.name ?? "Unknown",
                    StartedAt = DateTime.Now.AddHours(-1), // Approximate
                    CompletedAt = DateTime.Now,
                    Notes = ""
                };
                progressTracker.LogCompletedRepair(log);
            }

            SetMode(AppMode.Completion);
        }

        private void OnPartDetailsRequested(PartInfo part)
        {
            // Navigate to full part inspector
            SetMode(AppMode.PartInspection);
        }

        private void OnMenuClicked()
        {
            // Show navigation menu
            // For Phase 1, just go back to engine selection
            if (CurrentMode != AppMode.ModelSelection)
            {
                SetMode(AppMode.ModelSelection);
            }
        }

        private void OnSettingsClicked()
        {
            SetMode(AppMode.Settings);
        }

        // Public navigation methods
        public void GoBack()
        {
            switch (CurrentMode)
            {
                case AppMode.Alignment:
                    SetMode(AppMode.ModelSelection);
                    break;
                case AppMode.ProcedureSelection:
                    SetMode(AppMode.Alignment);
                    arAlignment?.UnlockAlignment();
                    break;
                case AppMode.ProcedureActive:
                    SetMode(AppMode.ProcedureSelection);
                    break;
                case AppMode.PartInspection:
                    SetMode(AppMode.ProcedureActive);
                    break;
                case AppMode.Settings:
                    SetMode(AppMode.ModelSelection);
                    break;
                case AppMode.Completion:
                    SetMode(AppMode.ProcedureSelection);
                    break;
            }
        }

        public void GoToEngineSelection()
        {
            SetMode(AppMode.ModelSelection);
        }

        public void GoToProcedureSelection()
        {
            if (!string.IsNullOrEmpty(SelectedEngineId))
            {
                SetMode(AppMode.ProcedureSelection);
            }
        }

        public void GoToSettings()
        {
            SetMode(AppMode.Settings);
        }

        public void StartNewRepair()
        {
            procedureRunner?.ResetProcedure();
            SetMode(AppMode.ProcedureActive);
        }

        public void FinishAndReturn()
        {
            procedureRunner?.UnloadProcedure();
            SetMode(AppMode.ProcedureSelection);
        }
    }

    /// <summary>
    /// Application mode enumeration.
    /// </summary>
    public enum AppMode
    {
        Splash,
        ModelSelection,
        Alignment,
        ProcedureSelection,
        ProcedureActive,
        PartInspection,
        Settings,
        Completion
    }
}
