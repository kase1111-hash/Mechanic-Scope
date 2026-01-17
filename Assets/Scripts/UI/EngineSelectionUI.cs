using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using MechanicScope.Core;

namespace MechanicScope.UI
{
    /// <summary>
    /// UI for selecting an engine model from the available list.
    /// </summary>
    public class EngineSelectionUI : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private EngineModelLoader modelLoader;

        [Header("UI Elements")]
        [SerializeField] private Transform listContainer;
        [SerializeField] private GameObject engineItemPrefab;
        [SerializeField] private TextMeshProUGUI emptyStateText;
        [SerializeField] private Button importButton;
        [SerializeField] private GameObject loadingIndicator;

        [Header("Settings")]
        [SerializeField] private string emptyMessage = "No engines imported.\nTap '+' to add an engine model.";

        // Events
        public event Action<string> OnEngineSelected;
        public event Action OnImportRequested;

        private List<GameObject> spawnedItems = new List<GameObject>();

        private void Start()
        {
            if (importButton != null)
            {
                importButton.onClick.AddListener(OnImportClicked);
            }

            if (modelLoader != null)
            {
                modelLoader.OnEngineListUpdated += OnEngineListUpdated;
            }

            RefreshList();
        }

        private void OnDestroy()
        {
            if (modelLoader != null)
            {
                modelLoader.OnEngineListUpdated -= OnEngineListUpdated;
            }
        }

        private void OnEngineListUpdated(EngineManifest manifest)
        {
            RefreshList();
        }

        /// <summary>
        /// Refreshes the engine list from the model loader.
        /// </summary>
        public void RefreshList()
        {
            ClearList();

            if (modelLoader == null)
            {
                ShowEmptyState(true);
                return;
            }

            modelLoader.RefreshEngineList();
            List<EngineManifest> engines = modelLoader.AvailableEngines;

            if (engines == null || engines.Count == 0)
            {
                ShowEmptyState(true);
                return;
            }

            ShowEmptyState(false);

            foreach (EngineManifest engine in engines)
            {
                CreateEngineItem(engine);
            }
        }

        private void ClearList()
        {
            foreach (GameObject item in spawnedItems)
            {
                Destroy(item);
            }
            spawnedItems.Clear();
        }

        private void CreateEngineItem(EngineManifest engine)
        {
            if (engineItemPrefab == null || listContainer == null) return;

            GameObject item = Instantiate(engineItemPrefab, listContainer);
            spawnedItems.Add(item);

            // Setup item UI
            EngineListItem listItem = item.GetComponent<EngineListItem>();
            if (listItem != null)
            {
                listItem.Setup(engine, OnItemSelected, OnItemDeleteRequested);
            }
            else
            {
                // Fallback: Find UI components directly
                SetupItemFallback(item, engine);
            }
        }

        private void SetupItemFallback(GameObject item, EngineManifest engine)
        {
            // Try to find text components
            TextMeshProUGUI[] texts = item.GetComponentsInChildren<TextMeshProUGUI>();
            foreach (var text in texts)
            {
                if (text.name.ToLower().Contains("name") || text.name.ToLower().Contains("title"))
                {
                    text.text = engine.name;
                }
                else if (text.name.ToLower().Contains("manufacturer"))
                {
                    text.text = engine.manufacturer ?? "";
                }
                else if (text.name.ToLower().Contains("year"))
                {
                    text.text = engine.years ?? "";
                }
            }

            // Setup button
            Button button = item.GetComponent<Button>();
            if (button == null)
            {
                button = item.GetComponentInChildren<Button>();
            }

            if (button != null)
            {
                string engineId = engine.id;
                button.onClick.AddListener(() => OnItemSelected(engineId));
            }
        }

        private void OnItemSelected(string engineId)
        {
            OnEngineSelected?.Invoke(engineId);
        }

        private void OnItemDeleteRequested(string engineId)
        {
            // Show confirmation dialog, then delete
            modelLoader?.DeleteEngine(engineId);
            RefreshList();
        }

        private void OnImportClicked()
        {
            OnImportRequested?.Invoke();
            // In a real implementation, this would open a file picker
            // For Phase 1, we can show instructions or use a simple path input
            ShowImportInstructions();
        }

        private void ShowImportInstructions()
        {
            Debug.Log("To import an engine:\n" +
                      "1. Place your .glb model file in a folder\n" +
                      "2. Create an engine.json manifest file\n" +
                      "3. Copy the folder to: " + Application.persistentDataPath + "/engines/");
        }

        private void ShowEmptyState(bool show)
        {
            if (emptyStateText != null)
            {
                emptyStateText.gameObject.SetActive(show);
                if (show)
                {
                    emptyStateText.text = emptyMessage;
                }
            }

            if (listContainer != null)
            {
                listContainer.gameObject.SetActive(!show);
            }
        }

        public void ShowLoading(bool show)
        {
            if (loadingIndicator != null)
            {
                loadingIndicator.SetActive(show);
            }
        }
    }

    /// <summary>
    /// Component for individual engine list items.
    /// </summary>
    public class EngineListItem : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI nameText;
        [SerializeField] private TextMeshProUGUI manufacturerText;
        [SerializeField] private TextMeshProUGUI yearsText;
        [SerializeField] private Image thumbnailImage;
        [SerializeField] private Button selectButton;
        [SerializeField] private Button deleteButton;
        [SerializeField] private GameObject bundledBadge;

        private string engineId;
        private Action<string> onSelected;
        private Action<string> onDeleteRequested;

        public void Setup(EngineManifest engine, Action<string> selectCallback, Action<string> deleteCallback)
        {
            engineId = engine.id;
            onSelected = selectCallback;
            onDeleteRequested = deleteCallback;

            if (nameText != null)
                nameText.text = engine.name;

            if (manufacturerText != null)
                manufacturerText.text = engine.manufacturer ?? "";

            if (yearsText != null)
                yearsText.text = engine.years ?? "";

            if (bundledBadge != null)
                bundledBadge.SetActive(engine.IsBundled);

            if (deleteButton != null)
            {
                deleteButton.gameObject.SetActive(!engine.IsBundled);
                deleteButton.onClick.AddListener(OnDeleteClicked);
            }

            if (selectButton != null)
            {
                selectButton.onClick.AddListener(OnSelectClicked);
            }
            else
            {
                // Make the whole item clickable
                Button itemButton = GetComponent<Button>();
                if (itemButton == null)
                {
                    itemButton = gameObject.AddComponent<Button>();
                }
                itemButton.onClick.AddListener(OnSelectClicked);
            }

            // Load thumbnail if available
            // LoadThumbnail(engine);
        }

        private void OnSelectClicked()
        {
            onSelected?.Invoke(engineId);
        }

        private void OnDeleteClicked()
        {
            onDeleteRequested?.Invoke(engineId);
        }
    }
}
