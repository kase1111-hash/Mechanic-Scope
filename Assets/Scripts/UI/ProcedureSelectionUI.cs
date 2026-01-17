using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using MechanicScope.Core;

namespace MechanicScope.UI
{
    /// <summary>
    /// UI for selecting a procedure from the available list for an engine.
    /// </summary>
    public class ProcedureSelectionUI : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private ProcedureRunner procedureRunner;
        [SerializeField] private ProgressTracker progressTracker;

        [Header("UI Elements")]
        [SerializeField] private Transform listContainer;
        [SerializeField] private GameObject procedureItemPrefab;
        [SerializeField] private TextMeshProUGUI emptyStateText;
        [SerializeField] private TextMeshProUGUI engineNameText;
        [SerializeField] private GameObject loadingIndicator;

        [Header("Filter/Sort")]
        [SerializeField] private TMP_Dropdown difficultyFilter;
        [SerializeField] private TMP_Dropdown sortDropdown;

        [Header("Settings")]
        [SerializeField] private string emptyMessage = "No procedures available for this engine.";

        // Events
        public event Action<string> OnProcedureSelected;

        // Properties
        public string CurrentEngineId { get; private set; }

        private List<GameObject> spawnedItems = new List<GameObject>();
        private List<Procedure> currentProcedures = new List<Procedure>();
        private string currentDifficultyFilter = "all";
        private string currentSort = "name";

        private void Start()
        {
            if (difficultyFilter != null)
            {
                difficultyFilter.onValueChanged.AddListener(OnDifficultyFilterChanged);
            }

            if (sortDropdown != null)
            {
                sortDropdown.onValueChanged.AddListener(OnSortChanged);
            }
        }

        /// <summary>
        /// Loads procedures for the specified engine.
        /// </summary>
        public void LoadProcedures(string engineId)
        {
            CurrentEngineId = engineId;
            ClearList();

            if (procedureRunner == null || string.IsNullOrEmpty(engineId))
            {
                ShowEmptyState(true);
                return;
            }

            ShowLoading(true);

            currentProcedures = procedureRunner.GetProceduresForEngine(engineId);

            ShowLoading(false);

            if (currentProcedures == null || currentProcedures.Count == 0)
            {
                ShowEmptyState(true);
                return;
            }

            ShowEmptyState(false);
            ApplyFilterAndSort();
        }

        private void ApplyFilterAndSort()
        {
            ClearList();

            IEnumerable<Procedure> filtered = currentProcedures;

            // Apply difficulty filter
            if (currentDifficultyFilter != "all")
            {
                filtered = System.Linq.Enumerable.Where(filtered,
                    p => p.difficulty?.ToLower() == currentDifficultyFilter);
            }

            // Apply sort
            List<Procedure> sorted = new List<Procedure>(filtered);
            switch (currentSort)
            {
                case "name":
                    sorted.Sort((a, b) => string.Compare(a.name, b.name));
                    break;
                case "difficulty":
                    sorted.Sort((a, b) => GetDifficultyOrder(a.difficulty).CompareTo(GetDifficultyOrder(b.difficulty)));
                    break;
                case "progress":
                    sorted.Sort((a, b) => GetProgress(b).CompareTo(GetProgress(a)));
                    break;
            }

            foreach (Procedure procedure in sorted)
            {
                CreateProcedureItem(procedure);
            }
        }

        private int GetDifficultyOrder(string difficulty)
        {
            switch (difficulty?.ToLower())
            {
                case "beginner": return 0;
                case "easy": return 1;
                case "intermediate": return 2;
                case "advanced": return 3;
                case "expert": return 4;
                default: return 5;
            }
        }

        private float GetProgress(Procedure procedure)
        {
            if (progressTracker == null || procedure.steps == null) return 0;
            return progressTracker.GetProgressPercentage(procedure.id, CurrentEngineId, procedure.steps.Length);
        }

        private void ClearList()
        {
            foreach (GameObject item in spawnedItems)
            {
                Destroy(item);
            }
            spawnedItems.Clear();
        }

        private void CreateProcedureItem(Procedure procedure)
        {
            if (procedureItemPrefab == null || listContainer == null) return;

            GameObject item = Instantiate(procedureItemPrefab, listContainer);
            spawnedItems.Add(item);

            // Setup item UI
            ProcedureListItem listItem = item.GetComponent<ProcedureListItem>();
            if (listItem != null)
            {
                float progress = GetProgress(procedure);
                listItem.Setup(procedure, progress, OnItemSelected);
            }
            else
            {
                // Fallback setup
                SetupItemFallback(item, procedure);
            }
        }

        private void SetupItemFallback(GameObject item, Procedure procedure)
        {
            TextMeshProUGUI[] texts = item.GetComponentsInChildren<TextMeshProUGUI>();
            foreach (var text in texts)
            {
                string lowerName = text.name.ToLower();
                if (lowerName.Contains("name") || lowerName.Contains("title"))
                {
                    text.text = procedure.name;
                }
                else if (lowerName.Contains("description") || lowerName.Contains("desc"))
                {
                    text.text = procedure.description ?? "";
                }
                else if (lowerName.Contains("difficulty"))
                {
                    text.text = procedure.difficulty ?? "";
                }
                else if (lowerName.Contains("time"))
                {
                    text.text = procedure.estimatedTime ?? "";
                }
            }

            // Setup progress bar if present
            Slider progressSlider = item.GetComponentInChildren<Slider>();
            if (progressSlider != null)
            {
                float progress = GetProgress(procedure);
                progressSlider.value = progress / 100f;
            }

            // Setup button
            Button button = item.GetComponent<Button>();
            if (button == null)
            {
                button = item.GetComponentInChildren<Button>();
            }

            if (button != null)
            {
                string procedureId = procedure.id;
                button.onClick.AddListener(() => OnItemSelected(procedureId));
            }
        }

        private void OnItemSelected(string procedureId)
        {
            OnProcedureSelected?.Invoke(procedureId);
        }

        private void OnDifficultyFilterChanged(int index)
        {
            switch (index)
            {
                case 0: currentDifficultyFilter = "all"; break;
                case 1: currentDifficultyFilter = "beginner"; break;
                case 2: currentDifficultyFilter = "intermediate"; break;
                case 3: currentDifficultyFilter = "advanced"; break;
            }
            ApplyFilterAndSort();
        }

        private void OnSortChanged(int index)
        {
            switch (index)
            {
                case 0: currentSort = "name"; break;
                case 1: currentSort = "difficulty"; break;
                case 2: currentSort = "progress"; break;
            }
            ApplyFilterAndSort();
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
    /// Component for individual procedure list items.
    /// </summary>
    public class ProcedureListItem : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI nameText;
        [SerializeField] private TextMeshProUGUI descriptionText;
        [SerializeField] private TextMeshProUGUI difficultyText;
        [SerializeField] private TextMeshProUGUI timeText;
        [SerializeField] private TextMeshProUGUI toolsText;
        [SerializeField] private Slider progressSlider;
        [SerializeField] private TextMeshProUGUI progressText;
        [SerializeField] private Image difficultyIcon;
        [SerializeField] private Button selectButton;
        [SerializeField] private GameObject inProgressBadge;

        [Header("Difficulty Colors")]
        [SerializeField] private Color beginnerColor = new Color(0.3f, 0.8f, 0.3f);
        [SerializeField] private Color intermediateColor = new Color(0.9f, 0.7f, 0.2f);
        [SerializeField] private Color advancedColor = new Color(0.9f, 0.3f, 0.3f);

        private string procedureId;
        private Action<string> onSelected;

        public void Setup(Procedure procedure, float progress, Action<string> selectCallback)
        {
            procedureId = procedure.id;
            onSelected = selectCallback;

            if (nameText != null)
                nameText.text = procedure.name;

            if (descriptionText != null)
                descriptionText.text = procedure.description ?? "";

            if (difficultyText != null)
            {
                difficultyText.text = FormatDifficulty(procedure.difficulty);
                difficultyText.color = GetDifficultyColor(procedure.difficulty);
            }

            if (timeText != null)
                timeText.text = procedure.estimatedTime ?? "";

            if (toolsText != null && procedure.tools != null)
            {
                toolsText.text = string.Join(", ", procedure.tools);
            }

            if (progressSlider != null)
            {
                progressSlider.value = progress / 100f;
                progressSlider.gameObject.SetActive(progress > 0);
            }

            if (progressText != null)
            {
                progressText.text = $"{Mathf.RoundToInt(progress)}%";
                progressText.gameObject.SetActive(progress > 0);
            }

            if (inProgressBadge != null)
            {
                inProgressBadge.SetActive(progress > 0 && progress < 100);
            }

            if (selectButton != null)
            {
                selectButton.onClick.AddListener(OnSelectClicked);
            }
            else
            {
                Button itemButton = GetComponent<Button>();
                if (itemButton == null)
                {
                    itemButton = gameObject.AddComponent<Button>();
                }
                itemButton.onClick.AddListener(OnSelectClicked);
            }
        }

        private string FormatDifficulty(string difficulty)
        {
            if (string.IsNullOrEmpty(difficulty)) return "";
            return char.ToUpper(difficulty[0]) + difficulty.Substring(1).ToLower();
        }

        private Color GetDifficultyColor(string difficulty)
        {
            switch (difficulty?.ToLower())
            {
                case "beginner":
                case "easy":
                    return beginnerColor;
                case "intermediate":
                    return intermediateColor;
                case "advanced":
                case "expert":
                    return advancedColor;
                default:
                    return Color.white;
            }
        }

        private void OnSelectClicked()
        {
            onSelected?.Invoke(procedureId);
        }
    }
}
