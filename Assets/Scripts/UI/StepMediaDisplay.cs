using System;
using UnityEngine;
using UnityEngine.UI;
using MechanicScope.Core;
using MechanicScope.Utils;

namespace MechanicScope.UI
{
    /// <summary>
    /// UI component for displaying step media (images) in the procedure card.
    /// </summary>
    public class StepMediaDisplay : MonoBehaviour
    {
        [Header("UI Elements")]
        [SerializeField] private GameObject mediaContainer;
        [SerializeField] private RawImage imageDisplay;
        [SerializeField] private AspectRatioFitter aspectFitter;
        [SerializeField] private Button expandButton;
        [SerializeField] private Button closeExpandedButton;
        [SerializeField] private GameObject loadingIndicator;
        [SerializeField] private GameObject errorIndicator;

        [Header("Expanded View")]
        [SerializeField] private GameObject expandedView;
        [SerializeField] private RawImage expandedImage;
        [SerializeField] private Button expandedCloseButton;

        [Header("Settings")]
        [SerializeField] private float thumbnailHeight = 150f;
        [SerializeField] private bool autoHideOnNoMedia = true;

        // Events
        public event Action OnImageExpanded;
        public event Action OnImageCollapsed;

        // State
        private string currentEngineId;
        private string currentProcedureId;
        private ProcedureStep currentStep;
        private Texture2D currentTexture;
        private bool isLoading;

        private void Awake()
        {
            SetupButtons();
        }

        private void Start()
        {
            if (autoHideOnNoMedia)
            {
                Hide();
            }
        }

        private void SetupButtons()
        {
            if (expandButton != null)
            {
                expandButton.onClick.AddListener(ExpandImage);
            }

            if (closeExpandedButton != null)
            {
                closeExpandedButton.onClick.AddListener(CollapseImage);
            }

            if (expandedCloseButton != null)
            {
                expandedCloseButton.onClick.AddListener(CollapseImage);
            }
        }

        /// <summary>
        /// Sets the current engine and procedure context.
        /// </summary>
        public void SetContext(string engineId, string procedureId)
        {
            currentEngineId = engineId;
            currentProcedureId = procedureId;
        }

        /// <summary>
        /// Displays media for a procedure step.
        /// </summary>
        public void DisplayStepMedia(ProcedureStep step)
        {
            currentStep = step;

            // Clear previous state
            ClearDisplay();

            // Check if step has media
            if (step?.media?.image == null)
            {
                if (autoHideOnNoMedia)
                {
                    Hide();
                }
                return;
            }

            Show();
            ShowLoading(true);

            // Load the image
            var mediaLoader = StepMediaLoader.Instance;
            if (mediaLoader != null)
            {
                mediaLoader.LoadStepImage(currentEngineId, currentProcedureId, step.media.image, OnImageLoaded);
            }
            else
            {
                // Fallback: try loading directly
                LoadImageFallback(step.media.image);
            }
        }

        private void OnImageLoaded(Texture2D texture)
        {
            ShowLoading(false);

            if (texture == null)
            {
                ShowError(true);
                return;
            }

            currentTexture = texture;
            DisplayTexture(texture);
        }

        private void DisplayTexture(Texture2D texture)
        {
            if (imageDisplay != null)
            {
                imageDisplay.texture = texture;
                imageDisplay.gameObject.SetActive(true);

                // Update aspect ratio
                if (aspectFitter != null && texture.width > 0)
                {
                    aspectFitter.aspectRatio = (float)texture.width / texture.height;
                }
            }

            if (expandedImage != null)
            {
                expandedImage.texture = texture;
            }

            ShowError(false);
        }

        private void LoadImageFallback(string imagePath)
        {
            // Simple fallback for when StepMediaLoader isn't available
            StartCoroutine(LoadImageCoroutine(imagePath));
        }

        private System.Collections.IEnumerator LoadImageCoroutine(string path)
        {
            string fullPath = System.IO.Path.Combine(
                Application.streamingAssetsPath, "Engines", currentEngineId, "procedures", "media", path
            );

            using (var request = UnityEngine.Networking.UnityWebRequestTexture.GetTexture(fullPath))
            {
                yield return request.SendWebRequest();

                ShowLoading(false);

                if (request.result == UnityEngine.Networking.UnityWebRequest.Result.Success)
                {
                    var texture = UnityEngine.Networking.DownloadHandlerTexture.GetContent(request);
                    currentTexture = texture;
                    DisplayTexture(texture);
                }
                else
                {
                    ShowError(true);
                }
            }
        }

        /// <summary>
        /// Expands the image to full screen view.
        /// </summary>
        public void ExpandImage()
        {
            if (currentTexture == null || expandedView == null) return;

            expandedView.SetActive(true);
            OnImageExpanded?.Invoke();
        }

        /// <summary>
        /// Collapses the expanded view.
        /// </summary>
        public void CollapseImage()
        {
            if (expandedView != null)
            {
                expandedView.SetActive(false);
            }
            OnImageCollapsed?.Invoke();
        }

        /// <summary>
        /// Clears the current display.
        /// </summary>
        public void ClearDisplay()
        {
            if (imageDisplay != null)
            {
                imageDisplay.texture = null;
                imageDisplay.gameObject.SetActive(false);
            }

            if (expandedImage != null)
            {
                expandedImage.texture = null;
            }

            if (expandedView != null)
            {
                expandedView.SetActive(false);
            }

            ShowError(false);
            ShowLoading(false);

            currentTexture = null;
        }

        /// <summary>
        /// Shows the media container.
        /// </summary>
        public void Show()
        {
            if (mediaContainer != null)
            {
                mediaContainer.SetActive(true);
            }
            gameObject.SetActive(true);
        }

        /// <summary>
        /// Hides the media container.
        /// </summary>
        public void Hide()
        {
            if (mediaContainer != null)
            {
                mediaContainer.SetActive(false);
            }

            if (expandedView != null)
            {
                expandedView.SetActive(false);
            }
        }

        private void ShowLoading(bool show)
        {
            isLoading = show;
            if (loadingIndicator != null)
            {
                loadingIndicator.SetActive(show);
            }
        }

        private void ShowError(bool show)
        {
            if (errorIndicator != null)
            {
                errorIndicator.SetActive(show);
            }
        }

        /// <summary>
        /// Checks if media is currently being displayed.
        /// </summary>
        public bool HasMedia => currentTexture != null;

        /// <summary>
        /// Checks if media is currently loading.
        /// </summary>
        public bool IsLoading => isLoading;

        /// <summary>
        /// Gets the current texture.
        /// </summary>
        public Texture2D CurrentTexture => currentTexture;
    }
}
