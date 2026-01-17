using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using MechanicScope.Core;

namespace MechanicScope.UI
{
    /// <summary>
    /// UI controls for aligning the 3D model to the AR camera view.
    /// </summary>
    public class AlignmentControlsUI : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private ARAlignment arAlignment;

        [Header("UI Elements")]
        [SerializeField] private GameObject controlsPanel;
        [SerializeField] private TextMeshProUGUI instructionText;
        [SerializeField] private TextMeshProUGUI statusText;

        [Header("Buttons")]
        [SerializeField] private Button lockButton;
        [SerializeField] private Button resetButton;
        [SerializeField] private Button doneButton;

        [Header("Mode Controls")]
        [SerializeField] private Toggle rotateToggle;
        [SerializeField] private Toggle scaleToggle;
        [SerializeField] private Toggle translateToggle;

        [Header("Fine Adjustment")]
        [SerializeField] private Slider xPositionSlider;
        [SerializeField] private Slider yPositionSlider;
        [SerializeField] private Slider zPositionSlider;
        [SerializeField] private Slider scaleSlider;

        [Header("Instructions")]
        [SerializeField] private string rotateInstruction = "Drag with one finger to rotate the model";
        [SerializeField] private string scaleInstruction = "Pinch with two fingers to scale";
        [SerializeField] private string translateInstruction = "Drag with two fingers to move";
        [SerializeField] private string lockedInstruction = "Alignment locked. Tap Unlock to adjust.";

        [Header("Settings")]
        [SerializeField] private float fineAdjustRange = 0.5f;
        [SerializeField] private float scaleRange = 2f;

        // Events
        public event Action OnAlignmentConfirmed;
        public event Action OnAlignmentReset;

        // Properties
        public bool IsLocked => arAlignment?.IsAlignmentLocked ?? false;

        private Vector3 initialPosition;
        private float initialScale;

        private void Start()
        {
            // Setup button listeners
            if (lockButton != null)
                lockButton.onClick.AddListener(OnLockClicked);
            if (resetButton != null)
                resetButton.onClick.AddListener(OnResetClicked);
            if (doneButton != null)
                doneButton.onClick.AddListener(OnDoneClicked);

            // Setup sliders
            SetupSliders();

            // Subscribe to AR events
            if (arAlignment != null)
            {
                arAlignment.OnAlignmentLocked += OnAlignmentLocked;
                arAlignment.OnAlignmentUnlocked += OnAlignmentUnlocked;
                arAlignment.OnModelPoseUpdated += OnModelPoseUpdated;
            }

            UpdateUI();
        }

        private void OnDestroy()
        {
            if (arAlignment != null)
            {
                arAlignment.OnAlignmentLocked -= OnAlignmentLocked;
                arAlignment.OnAlignmentUnlocked -= OnAlignmentUnlocked;
                arAlignment.OnModelPoseUpdated -= OnModelPoseUpdated;
            }
        }

        private void SetupSliders()
        {
            if (xPositionSlider != null)
            {
                xPositionSlider.minValue = -fineAdjustRange;
                xPositionSlider.maxValue = fineAdjustRange;
                xPositionSlider.onValueChanged.AddListener(OnXPositionChanged);
            }

            if (yPositionSlider != null)
            {
                yPositionSlider.minValue = -fineAdjustRange;
                yPositionSlider.maxValue = fineAdjustRange;
                yPositionSlider.onValueChanged.AddListener(OnYPositionChanged);
            }

            if (zPositionSlider != null)
            {
                zPositionSlider.minValue = -fineAdjustRange;
                zPositionSlider.maxValue = fineAdjustRange;
                zPositionSlider.onValueChanged.AddListener(OnZPositionChanged);
            }

            if (scaleSlider != null)
            {
                scaleSlider.minValue = 0.1f;
                scaleSlider.maxValue = scaleRange;
                scaleSlider.value = 1f;
                scaleSlider.onValueChanged.AddListener(OnScaleChanged);
            }
        }

        private void OnXPositionChanged(float value)
        {
            if (arAlignment?.CurrentModel == null || IsLocked) return;
            Vector3 pos = arAlignment.CurrentModel.transform.position;
            pos.x = initialPosition.x + value;
            arAlignment.CurrentModel.transform.position = pos;
        }

        private void OnYPositionChanged(float value)
        {
            if (arAlignment?.CurrentModel == null || IsLocked) return;
            Vector3 pos = arAlignment.CurrentModel.transform.position;
            pos.y = initialPosition.y + value;
            arAlignment.CurrentModel.transform.position = pos;
        }

        private void OnZPositionChanged(float value)
        {
            if (arAlignment?.CurrentModel == null || IsLocked) return;
            Vector3 pos = arAlignment.CurrentModel.transform.position;
            pos.z = initialPosition.z + value;
            arAlignment.CurrentModel.transform.position = pos;
        }

        private void OnScaleChanged(float value)
        {
            if (arAlignment?.CurrentModel == null || IsLocked) return;
            arAlignment.CurrentModel.transform.localScale = Vector3.one * value;
        }

        private void OnLockClicked()
        {
            if (arAlignment == null) return;

            if (IsLocked)
            {
                arAlignment.UnlockAlignment();
            }
            else
            {
                arAlignment.LockAlignment();
            }
        }

        private void OnResetClicked()
        {
            arAlignment?.ResetAlignment();
            ResetSliders();
            OnAlignmentReset?.Invoke();
        }

        private void OnDoneClicked()
        {
            if (!IsLocked)
            {
                arAlignment?.LockAlignment();
            }
            OnAlignmentConfirmed?.Invoke();
        }

        private void OnAlignmentLocked()
        {
            UpdateUI();
        }

        private void OnAlignmentUnlocked()
        {
            CaptureInitialValues();
            UpdateUI();
        }

        private void OnModelPoseUpdated(Pose pose)
        {
            // Update sliders to reflect current position (optional)
        }

        private void CaptureInitialValues()
        {
            if (arAlignment?.CurrentModel != null)
            {
                initialPosition = arAlignment.CurrentModel.transform.position;
                initialScale = arAlignment.CurrentModel.transform.localScale.x;
            }
        }

        private void ResetSliders()
        {
            if (xPositionSlider != null) xPositionSlider.value = 0;
            if (yPositionSlider != null) yPositionSlider.value = 0;
            if (zPositionSlider != null) zPositionSlider.value = 0;
            if (scaleSlider != null) scaleSlider.value = 1f;
            CaptureInitialValues();
        }

        private void UpdateUI()
        {
            bool locked = IsLocked;

            // Update lock button text
            if (lockButton != null)
            {
                TextMeshProUGUI buttonText = lockButton.GetComponentInChildren<TextMeshProUGUI>();
                if (buttonText != null)
                {
                    buttonText.text = locked ? "Unlock" : "Lock";
                }
            }

            // Update instruction text
            if (instructionText != null)
            {
                instructionText.text = locked ? lockedInstruction : rotateInstruction;
            }

            // Update status
            if (statusText != null)
            {
                statusText.text = locked ? "Locked" : "Adjusting";
            }

            // Enable/disable controls
            if (xPositionSlider != null) xPositionSlider.interactable = !locked;
            if (yPositionSlider != null) yPositionSlider.interactable = !locked;
            if (zPositionSlider != null) zPositionSlider.interactable = !locked;
            if (scaleSlider != null) scaleSlider.interactable = !locked;

            // Done button only enabled when locked
            if (doneButton != null)
            {
                doneButton.interactable = locked;
            }
        }

        public void Show()
        {
            if (controlsPanel != null)
            {
                controlsPanel.SetActive(true);
            }
            CaptureInitialValues();
            ResetSliders();
            UpdateUI();
        }

        public void Hide()
        {
            if (controlsPanel != null)
            {
                controlsPanel.SetActive(false);
            }
        }

        /// <summary>
        /// Sets the instruction text based on current interaction mode.
        /// </summary>
        public void SetInstruction(string instruction)
        {
            if (instructionText != null)
            {
                instructionText.text = instruction;
            }
        }

        /// <summary>
        /// Shows a temporary status message.
        /// </summary>
        public void ShowStatus(string status, float duration = 2f)
        {
            if (statusText != null)
            {
                statusText.text = status;
                if (duration > 0)
                {
                    CancelInvoke(nameof(ClearStatus));
                    Invoke(nameof(ClearStatus), duration);
                }
            }
        }

        private void ClearStatus()
        {
            UpdateUI();
        }
    }
}
