using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using MechanicScope.Core;

namespace MechanicScope.UI
{
    /// <summary>
    /// Popup UI for displaying part information when tapped in AR view.
    /// </summary>
    public class PartInfoPopup : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private ARAlignment arAlignment;
        [SerializeField] private PartDatabase partDatabase;

        [Header("Popup Elements")]
        [SerializeField] private GameObject popupPanel;
        [SerializeField] private RectTransform popupRect;
        [SerializeField] private TextMeshProUGUI partNameText;
        [SerializeField] private TextMeshProUGUI categoryText;
        [SerializeField] private TextMeshProUGUI descriptionText;
        [SerializeField] private TextMeshProUGUI specsText;
        [SerializeField] private Image categoryIcon;
        [SerializeField] private Button closeButton;
        [SerializeField] private Button detailsButton;

        [Header("Category Icons")]
        [SerializeField] private Sprite electricalIcon;
        [SerializeField] private Sprite mechanicalIcon;
        [SerializeField] private Sprite fluidIcon;
        [SerializeField] private Sprite defaultIcon;

        [Header("Settings")]
        [SerializeField] private Vector2 popupOffset = new Vector2(0, 50);
        [SerializeField] private float autoHideDelay = 5f;
        [SerializeField] private bool followTapPosition = true;

        // Events
        public event Action<PartInfo> OnPartSelected;
        public event Action<PartInfo> OnDetailsRequested;
        public event Action OnPopupClosed;

        // Properties
        public bool IsVisible => popupPanel != null && popupPanel.activeSelf;
        public PartInfo CurrentPart { get; private set; }
        public string CurrentPartId { get; private set; }

        private float hideTimer;
        private bool isTimerActive;
        private Camera mainCamera;

        private void Start()
        {
            mainCamera = Camera.main;

            // Subscribe to AR part tap events
            if (arAlignment != null)
            {
                arAlignment.OnPartTapped += OnPartTapped;
            }

            // Setup buttons
            if (closeButton != null)
                closeButton.onClick.AddListener(Hide);
            if (detailsButton != null)
                detailsButton.onClick.AddListener(OnDetailsClicked);

            Hide();
        }

        private void OnDestroy()
        {
            if (arAlignment != null)
            {
                arAlignment.OnPartTapped -= OnPartTapped;
            }
        }

        private void Update()
        {
            if (isTimerActive && autoHideDelay > 0)
            {
                hideTimer -= Time.deltaTime;
                if (hideTimer <= 0)
                {
                    Hide();
                }
            }

            // Hide on tap outside popup
            if (IsVisible && Input.touchCount > 0)
            {
                Touch touch = Input.GetTouch(0);
                if (touch.phase == TouchPhase.Began)
                {
                    if (!IsPointerOverPopup(touch.position))
                    {
                        // Don't hide immediately - let the AR system check for part tap first
                    }
                }
            }
        }

        private void OnPartTapped(string nodeNameOrPartId)
        {
            // Try to find part info by ID first, then by node name
            PartInfo part = partDatabase?.GetPart(nodeNameOrPartId);

            if (part == null)
            {
                // Try searching for part by node name in part mappings
                // For Phase 1, just display the node name
                ShowForUnknownPart(nodeNameOrPartId);
                return;
            }

            ShowForPart(part, Input.touchCount > 0 ? Input.GetTouch(0).position : (Vector2)Input.mousePosition);
        }

        /// <summary>
        /// Shows popup for a known part.
        /// </summary>
        public void ShowForPart(PartInfo part, Vector2 screenPosition)
        {
            if (part == null) return;

            CurrentPart = part;
            CurrentPartId = part.Id;

            // Update UI
            if (partNameText != null)
                partNameText.text = part.Name;

            if (categoryText != null)
            {
                categoryText.text = part.Category ?? "Unknown";
                categoryText.gameObject.SetActive(!string.IsNullOrEmpty(part.Category));
            }

            if (descriptionText != null)
            {
                descriptionText.text = part.Description ?? "";
                descriptionText.gameObject.SetActive(!string.IsNullOrEmpty(part.Description));
            }

            if (specsText != null)
            {
                if (part.Specs != null && part.Specs.Count > 0)
                {
                    specsText.text = part.GetFormattedSpecs();
                    specsText.gameObject.SetActive(true);
                }
                else
                {
                    specsText.gameObject.SetActive(false);
                }
            }

            UpdateCategoryIcon(part.Category);

            if (detailsButton != null)
                detailsButton.gameObject.SetActive(true);

            PositionPopup(screenPosition);
            Show();

            OnPartSelected?.Invoke(part);
        }

        /// <summary>
        /// Shows popup for an unknown part (just node name).
        /// </summary>
        public void ShowForUnknownPart(string nodeName)
        {
            CurrentPart = null;
            CurrentPartId = nodeName;

            // Format node name for display (replace underscores, capitalize)
            string displayName = FormatNodeName(nodeName);

            if (partNameText != null)
                partNameText.text = displayName;

            if (categoryText != null)
                categoryText.gameObject.SetActive(false);

            if (descriptionText != null)
            {
                descriptionText.text = "Part not in database";
                descriptionText.gameObject.SetActive(true);
            }

            if (specsText != null)
                specsText.gameObject.SetActive(false);

            if (categoryIcon != null)
                categoryIcon.sprite = defaultIcon;

            if (detailsButton != null)
                detailsButton.gameObject.SetActive(false);

            Vector2 position = Input.touchCount > 0 ? Input.GetTouch(0).position : (Vector2)Input.mousePosition;
            PositionPopup(position);
            Show();
        }

        /// <summary>
        /// Shows popup for a part by ID.
        /// </summary>
        public void ShowForPartId(string partId, Vector2 screenPosition)
        {
            PartInfo part = partDatabase?.GetPart(partId);
            if (part != null)
            {
                ShowForPart(part, screenPosition);
            }
            else
            {
                ShowForUnknownPart(partId);
            }
        }

        private void PositionPopup(Vector2 screenPosition)
        {
            if (!followTapPosition || popupRect == null) return;

            // Position popup near tap location, but keep within screen bounds
            Vector2 targetPosition = screenPosition + popupOffset;

            // Get screen bounds
            float screenWidth = Screen.width;
            float screenHeight = Screen.height;

            // Get popup size
            float popupWidth = popupRect.rect.width;
            float popupHeight = popupRect.rect.height;

            // Clamp to screen bounds
            targetPosition.x = Mathf.Clamp(targetPosition.x, popupWidth / 2, screenWidth - popupWidth / 2);
            targetPosition.y = Mathf.Clamp(targetPosition.y, popupHeight / 2, screenHeight - popupHeight / 2);

            popupRect.position = targetPosition;
        }

        private void UpdateCategoryIcon(string category)
        {
            if (categoryIcon == null) return;

            Sprite icon = defaultIcon;

            if (!string.IsNullOrEmpty(category))
            {
                switch (category.ToLower())
                {
                    case "electrical":
                        icon = electricalIcon ?? defaultIcon;
                        break;
                    case "mechanical":
                        icon = mechanicalIcon ?? defaultIcon;
                        break;
                    case "fluid":
                        icon = fluidIcon ?? defaultIcon;
                        break;
                }
            }

            categoryIcon.sprite = icon;
            categoryIcon.gameObject.SetActive(icon != null);
        }

        private string FormatNodeName(string nodeName)
        {
            if (string.IsNullOrEmpty(nodeName)) return "Unknown Part";

            // Remove common suffixes
            string name = nodeName
                .Replace("_Mesh", "")
                .Replace("_mesh", "")
                .Replace("_geo", "")
                .Replace("_GEO", "");

            // Replace underscores with spaces
            name = name.Replace("_", " ");

            // Capitalize first letter of each word
            var words = name.Split(' ');
            for (int i = 0; i < words.Length; i++)
            {
                if (words[i].Length > 0)
                {
                    words[i] = char.ToUpper(words[i][0]) + words[i].Substring(1).ToLower();
                }
            }

            return string.Join(" ", words);
        }

        private bool IsPointerOverPopup(Vector2 screenPosition)
        {
            if (popupRect == null) return false;

            return RectTransformUtility.RectangleContainsScreenPoint(popupRect, screenPosition, null);
        }

        private void OnDetailsClicked()
        {
            if (CurrentPart != null)
            {
                OnDetailsRequested?.Invoke(CurrentPart);
            }
        }

        public void Show()
        {
            if (popupPanel != null)
            {
                popupPanel.SetActive(true);
            }

            // Reset auto-hide timer
            hideTimer = autoHideDelay;
            isTimerActive = autoHideDelay > 0;
        }

        public void Hide()
        {
            if (popupPanel != null)
            {
                popupPanel.SetActive(false);
            }

            isTimerActive = false;
            CurrentPart = null;
            CurrentPartId = null;

            OnPopupClosed?.Invoke();
        }

        /// <summary>
        /// Stops the auto-hide timer (for when user is interacting).
        /// </summary>
        public void PauseAutoHide()
        {
            isTimerActive = false;
        }

        /// <summary>
        /// Resumes the auto-hide timer.
        /// </summary>
        public void ResumeAutoHide()
        {
            if (autoHideDelay > 0 && IsVisible)
            {
                hideTimer = autoHideDelay;
                isTimerActive = true;
            }
        }
    }
}
