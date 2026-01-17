using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using MechanicScope.Core;

namespace MechanicScope.UI
{
    /// <summary>
    /// UI component for displaying the current procedure step.
    /// Supports collapsed and expanded states.
    /// </summary>
    public class ProcedureCardUI : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private ProcedureRunner procedureRunner;

        [Header("Card Elements")]
        [SerializeField] private GameObject cardPanel;
        [SerializeField] private TextMeshProUGUI stepNumberText;
        [SerializeField] private TextMeshProUGUI actionText;
        [SerializeField] private TextMeshProUGUI detailsText;
        [SerializeField] private TextMeshProUGUI toolsText;
        [SerializeField] private TextMeshProUGUI warningsText;
        [SerializeField] private TextMeshProUGUI torqueText;

        [Header("Buttons")]
        [SerializeField] private Button completeButton;
        [SerializeField] private Button detailsButton;
        [SerializeField] private Button previousButton;
        [SerializeField] private Button nextButton;
        [SerializeField] private Button collapseButton;

        [Header("Panels")]
        [SerializeField] private GameObject expandedPanel;
        [SerializeField] private GameObject warningsPanel;
        [SerializeField] private GameObject torquePanel;
        [SerializeField] private GameObject toolsPanel;

        [Header("Progress")]
        [SerializeField] private Slider progressSlider;
        [SerializeField] private TextMeshProUGUI progressText;

        [Header("Swipe Settings")]
        [SerializeField] private float swipeThreshold = 50f;

        // Events
        public event Action OnStepCompleted;
        public event Action OnCardExpanded;
        public event Action OnCardCollapsed;

        // Properties
        public bool IsExpanded { get; private set; }
        public ProcedureStep CurrentStep { get; private set; }

        private Vector2 swipeStartPosition;
        private bool isSwiping;

        private void Start()
        {
            // Subscribe to procedure events
            if (procedureRunner != null)
            {
                procedureRunner.OnProcedureLoaded += OnProcedureLoaded;
                procedureRunner.OnStepActivated += OnStepActivated;
                procedureRunner.OnStepCompleted += OnStepCompletedHandler;
                procedureRunner.OnProcedureCompleted += OnProcedureCompleted;
            }

            // Setup button listeners
            if (completeButton != null)
                completeButton.onClick.AddListener(OnCompleteClicked);
            if (detailsButton != null)
                detailsButton.onClick.AddListener(ToggleExpanded);
            if (previousButton != null)
                previousButton.onClick.AddListener(OnPreviousClicked);
            if (nextButton != null)
                nextButton.onClick.AddListener(OnNextClicked);
            if (collapseButton != null)
                collapseButton.onClick.AddListener(Collapse);

            // Start collapsed
            SetExpanded(false);
            Hide();
        }

        private void OnDestroy()
        {
            if (procedureRunner != null)
            {
                procedureRunner.OnProcedureLoaded -= OnProcedureLoaded;
                procedureRunner.OnStepActivated -= OnStepActivated;
                procedureRunner.OnStepCompleted -= OnStepCompletedHandler;
                procedureRunner.OnProcedureCompleted -= OnProcedureCompleted;
            }
        }

        private void Update()
        {
            HandleSwipeInput();
        }

        private void HandleSwipeInput()
        {
            if (Input.touchCount == 1)
            {
                Touch touch = Input.GetTouch(0);

                // Check if touch is within card bounds
                if (cardPanel == null || !RectTransformUtility.RectangleContainsScreenPoint(
                    cardPanel.GetComponent<RectTransform>(),
                    touch.position,
                    null))
                {
                    return;
                }

                switch (touch.phase)
                {
                    case TouchPhase.Began:
                        swipeStartPosition = touch.position;
                        isSwiping = true;
                        break;

                    case TouchPhase.Ended:
                        if (isSwiping)
                        {
                            float swipeDistance = touch.position.x - swipeStartPosition.x;
                            if (Mathf.Abs(swipeDistance) > swipeThreshold)
                            {
                                if (swipeDistance > 0)
                                {
                                    OnPreviousClicked();
                                }
                                else
                                {
                                    OnNextClicked();
                                }
                            }
                        }
                        isSwiping = false;
                        break;

                    case TouchPhase.Canceled:
                        isSwiping = false;
                        break;
                }
            }
        }

        private void OnProcedureLoaded(Procedure procedure)
        {
            Show();
            UpdateProgress();
        }

        private void OnStepActivated(ProcedureStep step)
        {
            CurrentStep = step;
            UpdateStepDisplay();
            UpdateNavigationButtons();
        }

        private void OnStepCompletedHandler(ProcedureStep step)
        {
            UpdateProgress();
            OnStepCompleted?.Invoke();
        }

        private void OnProcedureCompleted()
        {
            // Show completion state
            if (stepNumberText != null)
                stepNumberText.text = "Complete!";
            if (actionText != null)
                actionText.text = "All steps completed";
            if (completeButton != null)
                completeButton.interactable = false;

            // Could show completion summary or celebration UI here
        }

        private void UpdateStepDisplay()
        {
            if (CurrentStep == null) return;

            // Step number
            if (stepNumberText != null && procedureRunner?.CurrentProcedure != null)
            {
                int totalSteps = procedureRunner.CurrentProcedure.steps?.Length ?? 0;
                int currentIndex = Array.FindIndex(procedureRunner.CurrentProcedure.steps, s => s.id == CurrentStep.id) + 1;
                stepNumberText.text = $"Step {currentIndex} of {totalSteps}";
            }

            // Action text
            if (actionText != null)
                actionText.text = CurrentStep.action;

            // Details
            if (detailsText != null)
            {
                detailsText.text = CurrentStep.details ?? "";
                detailsText.gameObject.SetActive(!string.IsNullOrEmpty(CurrentStep.details));
            }

            // Tools
            if (toolsText != null && toolsPanel != null)
            {
                if (CurrentStep.tools != null && CurrentStep.tools.Length > 0)
                {
                    toolsText.text = "Tools: " + string.Join(", ", CurrentStep.tools);
                    toolsPanel.SetActive(true);
                }
                else
                {
                    toolsPanel.SetActive(false);
                }
            }

            // Warnings
            if (warningsText != null && warningsPanel != null)
            {
                if (CurrentStep.warnings != null && CurrentStep.warnings.Length > 0)
                {
                    warningsText.text = string.Join("\n", CurrentStep.warnings);
                    warningsPanel.SetActive(true);
                }
                else
                {
                    warningsPanel.SetActive(false);
                }
            }

            // Torque spec
            if (torqueText != null && torquePanel != null)
            {
                if (CurrentStep.torqueSpec != null)
                {
                    torqueText.text = CurrentStep.torqueSpec.ToString();
                    torquePanel.SetActive(true);
                }
                else
                {
                    torquePanel.SetActive(false);
                }
            }

            // Complete button state
            if (completeButton != null)
            {
                completeButton.interactable = procedureRunner?.IsStepAvailable(CurrentStep.id) ?? false;
            }
        }

        private void UpdateProgress()
        {
            if (procedureRunner == null) return;

            float progress = procedureRunner.ProgressPercentage;

            if (progressSlider != null)
                progressSlider.value = progress / 100f;

            if (progressText != null)
                progressText.text = $"{Mathf.RoundToInt(progress)}%";
        }

        private void UpdateNavigationButtons()
        {
            if (procedureRunner == null) return;

            var availableSteps = procedureRunner.AvailableSteps;
            int currentIndex = availableSteps.IndexOf(CurrentStep);

            if (previousButton != null)
                previousButton.interactable = currentIndex > 0;

            if (nextButton != null)
                nextButton.interactable = currentIndex < availableSteps.Count - 1;
        }

        private void OnCompleteClicked()
        {
            if (CurrentStep != null && procedureRunner != null)
            {
                procedureRunner.CompleteStep(CurrentStep.id);
            }
        }

        private void OnPreviousClicked()
        {
            procedureRunner?.PreviousStep();
        }

        private void OnNextClicked()
        {
            procedureRunner?.NextStep();
        }

        public void ToggleExpanded()
        {
            SetExpanded(!IsExpanded);
        }

        public void Expand()
        {
            SetExpanded(true);
        }

        public void Collapse()
        {
            SetExpanded(false);
        }

        private void SetExpanded(bool expanded)
        {
            IsExpanded = expanded;

            if (expandedPanel != null)
                expandedPanel.SetActive(expanded);

            if (expanded)
                OnCardExpanded?.Invoke();
            else
                OnCardCollapsed?.Invoke();
        }

        public void Show()
        {
            if (cardPanel != null)
                cardPanel.SetActive(true);
        }

        public void Hide()
        {
            if (cardPanel != null)
                cardPanel.SetActive(false);
        }

        /// <summary>
        /// Manually set the step to display (for external control).
        /// </summary>
        public void DisplayStep(ProcedureStep step)
        {
            CurrentStep = step;
            UpdateStepDisplay();
            UpdateNavigationButtons();
            Show();
        }
    }
}
