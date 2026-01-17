using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using MechanicScope.Core;
using MechanicScope.Data;

namespace MechanicScope.UI
{
    /// <summary>
    /// Displays a summary when a procedure is completed.
    /// Shows statistics, allows notes, and logs the repair.
    /// </summary>
    public class CompletionSummaryUI : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private ProcedureRunner procedureRunner;
        [SerializeField] private DataManager dataManager;
        [SerializeField] private EngineModelLoader modelLoader;

        [Header("Header")]
        [SerializeField] private TextMeshProUGUI titleText;
        [SerializeField] private TextMeshProUGUI subtitleText;
        [SerializeField] private Image checkmarkIcon;

        [Header("Statistics")]
        [SerializeField] private TextMeshProUGUI totalStepsText;
        [SerializeField] private TextMeshProUGUI durationText;
        [SerializeField] private TextMeshProUGUI engineNameText;
        [SerializeField] private TextMeshProUGUI dateText;

        [Header("Historical Comparison")]
        [SerializeField] private GameObject comparisonPanel;
        [SerializeField] private TextMeshProUGUI previousTimeText;
        [SerializeField] private TextMeshProUGUI averageTimeText;
        [SerializeField] private TextMeshProUGUI timesCompletedText;
        [SerializeField] private Image performanceIndicator;
        [SerializeField] private TextMeshProUGUI performanceText;

        [Header("Notes")]
        [SerializeField] private TMP_InputField notesInput;
        [SerializeField] private TextMeshProUGUI notesHintText;

        [Header("Rating")]
        [SerializeField] private GameObject ratingPanel;
        [SerializeField] private Button[] ratingStars;
        [SerializeField] private TextMeshProUGUI ratingLabel;

        [Header("Reinstall Notes")]
        [SerializeField] private GameObject reinstallNotesPanel;
        [SerializeField] private TextMeshProUGUI reinstallNotesText;
        [SerializeField] private Button toggleReinstallNotesButton;

        [Header("Actions")]
        [SerializeField] private Button doneButton;
        [SerializeField] private Button repeatButton;
        [SerializeField] private Button shareButton;

        [Header("Animation")]
        [SerializeField] private Animator completionAnimator;
        [SerializeField] private float animationDelay = 0.3f;

        [Header("Colors")]
        [SerializeField] private Color fasterColor = new Color(0.3f, 0.69f, 0.31f);
        [SerializeField] private Color slowerColor = new Color(0.96f, 0.26f, 0.21f);
        [SerializeField] private Color neutralColor = new Color(0.5f, 0.5f, 0.5f);

        // Events
        public event Action OnDone;
        public event Action OnRepeat;
        public event Action OnShare;

        // State
        private DateTime startTime;
        private DateTime endTime;
        private int selectedRating;
        private Procedure currentProcedure;
        private RepairStatistics previousStats;

        private void Start()
        {
            SetupButtons();

            if (procedureRunner != null)
            {
                procedureRunner.OnProcedureLoaded += OnProcedureLoaded;
                procedureRunner.OnProcedureCompleted += OnProcedureCompleted;
            }

            Hide();
        }

        private void OnDestroy()
        {
            if (procedureRunner != null)
            {
                procedureRunner.OnProcedureLoaded -= OnProcedureLoaded;
                procedureRunner.OnProcedureCompleted -= OnProcedureCompleted;
            }
        }

        private void SetupButtons()
        {
            if (doneButton != null)
                doneButton.onClick.AddListener(OnDoneClicked);
            if (repeatButton != null)
                repeatButton.onClick.AddListener(OnRepeatClicked);
            if (shareButton != null)
                shareButton.onClick.AddListener(OnShareClicked);
            if (toggleReinstallNotesButton != null)
                toggleReinstallNotesButton.onClick.AddListener(ToggleReinstallNotes);

            // Setup rating stars
            if (ratingStars != null)
            {
                for (int i = 0; i < ratingStars.Length; i++)
                {
                    int rating = i + 1;
                    ratingStars[i].onClick.AddListener(() => SetRating(rating));
                }
            }
        }

        private void OnProcedureLoaded(Procedure procedure)
        {
            currentProcedure = procedure;
            startTime = DateTime.Now;

            // Load previous statistics
            if (dataManager?.Progress != null)
            {
                previousStats = dataManager.Progress.GetStatistics(procedure.id, procedure.engineId);
            }
        }

        private void OnProcedureCompleted()
        {
            endTime = DateTime.Now;
            Show();
        }

        /// <summary>
        /// Shows the completion summary with current data.
        /// </summary>
        public void Show()
        {
            gameObject.SetActive(true);
            PopulateSummary();

            if (completionAnimator != null)
            {
                completionAnimator.SetTrigger("Show");
            }
        }

        /// <summary>
        /// Hides the completion summary.
        /// </summary>
        public void Hide()
        {
            gameObject.SetActive(false);
        }

        private void PopulateSummary()
        {
            if (currentProcedure == null) return;

            // Header
            if (titleText != null)
                titleText.text = "Procedure Complete!";
            if (subtitleText != null)
                subtitleText.text = currentProcedure.name;

            // Statistics
            int totalSteps = currentProcedure.steps?.Length ?? 0;
            if (totalStepsText != null)
                totalStepsText.text = $"{totalSteps} steps completed";

            TimeSpan duration = endTime - startTime;
            if (durationText != null)
                durationText.text = FormatDuration(duration);

            if (engineNameText != null && modelLoader?.CurrentEngine != null)
                engineNameText.text = modelLoader.CurrentEngine.name;

            if (dateText != null)
                dateText.text = DateTime.Now.ToString("MMM dd, yyyy h:mm tt");

            // Historical comparison
            PopulateComparison(duration);

            // Reinstall notes
            if (reinstallNotesPanel != null && reinstallNotesText != null)
            {
                bool hasNotes = !string.IsNullOrEmpty(currentProcedure.reinstallNotes);
                reinstallNotesPanel.SetActive(false);  // Start collapsed
                reinstallNotesText.text = currentProcedure.reinstallNotes ?? "";

                if (toggleReinstallNotesButton != null)
                    toggleReinstallNotesButton.gameObject.SetActive(hasNotes);
            }

            // Reset notes and rating
            if (notesInput != null)
                notesInput.text = "";
            selectedRating = 0;
            UpdateRatingDisplay();
        }

        private void PopulateComparison(TimeSpan currentDuration)
        {
            if (comparisonPanel == null) return;

            if (previousStats == null || previousStats.TimesCompleted == 0)
            {
                // First time completing this procedure
                comparisonPanel.SetActive(false);
                return;
            }

            comparisonPanel.SetActive(true);

            int currentMinutes = (int)currentDuration.TotalMinutes;
            int avgMinutes = (int)previousStats.AverageDurationMinutes;

            if (timesCompletedText != null)
                timesCompletedText.text = $"Times completed: {previousStats.TimesCompleted + 1}";

            if (averageTimeText != null)
                averageTimeText.text = $"Average time: {FormatDuration(TimeSpan.FromMinutes(avgMinutes))}";

            if (previousStats.LastCompletedAt.HasValue && previousTimeText != null)
            {
                previousTimeText.text = $"Last completed: {previousStats.LastCompletedAt.Value:MMM dd}";
            }

            // Performance comparison
            if (performanceIndicator != null && performanceText != null)
            {
                float difference = currentMinutes - avgMinutes;
                float percentDiff = avgMinutes > 0 ? (difference / avgMinutes) * 100 : 0;

                if (Math.Abs(percentDiff) < 10)
                {
                    performanceIndicator.color = neutralColor;
                    performanceText.text = "On pace";
                }
                else if (percentDiff < 0)
                {
                    performanceIndicator.color = fasterColor;
                    performanceText.text = $"{Math.Abs((int)percentDiff)}% faster";
                }
                else
                {
                    performanceIndicator.color = slowerColor;
                    performanceText.text = $"{(int)percentDiff}% slower";
                }
            }
        }

        private string FormatDuration(TimeSpan duration)
        {
            if (duration.TotalHours >= 1)
            {
                return $"{(int)duration.TotalHours}h {duration.Minutes}m";
            }
            else if (duration.TotalMinutes >= 1)
            {
                return $"{(int)duration.TotalMinutes} minutes";
            }
            else
            {
                return $"{duration.Seconds} seconds";
            }
        }

        private void SetRating(int rating)
        {
            selectedRating = rating;
            UpdateRatingDisplay();
        }

        private void UpdateRatingDisplay()
        {
            if (ratingStars == null) return;

            for (int i = 0; i < ratingStars.Length; i++)
            {
                Image starImage = ratingStars[i].GetComponent<Image>();
                if (starImage != null)
                {
                    starImage.color = i < selectedRating ? Color.yellow : Color.gray;
                }
            }

            if (ratingLabel != null)
            {
                string[] labels = { "Rate this repair", "Poor", "Fair", "Good", "Great", "Excellent" };
                ratingLabel.text = labels[selectedRating];
            }
        }

        private void ToggleReinstallNotes()
        {
            if (reinstallNotesPanel != null)
            {
                reinstallNotesPanel.SetActive(!reinstallNotesPanel.activeSelf);
            }
        }

        private void OnDoneClicked()
        {
            SaveRepairLog();
            Hide();
            OnDone?.Invoke();
        }

        private void OnRepeatClicked()
        {
            SaveRepairLog();
            Hide();
            OnRepeat?.Invoke();
        }

        private void OnShareClicked()
        {
            // Generate shareable summary
            string summary = GenerateSummaryText();

            // Copy to clipboard
            GUIUtility.systemCopyBuffer = summary;

            // On mobile, could use native share dialog
            Debug.Log("Summary copied to clipboard:\n" + summary);

            OnShare?.Invoke();
        }

        private void SaveRepairLog()
        {
            if (dataManager?.Progress == null || currentProcedure == null) return;

            var entry = new RepairLogEntry
            {
                Id = Guid.NewGuid().ToString(),
                ProcedureId = currentProcedure.id,
                ProcedureName = currentProcedure.name,
                EngineId = currentProcedure.engineId,
                EngineName = modelLoader?.CurrentEngine?.name ?? "Unknown",
                StartedAt = startTime,
                CompletedAt = endTime,
                DurationMinutes = (int)(endTime - startTime).TotalMinutes,
                Notes = notesInput?.text,
                Rating = selectedRating > 0 ? selectedRating : (int?)null
            };

            dataManager.Progress.LogCompletedRepair(entry);
            Debug.Log($"Repair logged: {entry.ProcedureName} in {entry.DurationMinutes} minutes");
        }

        private string GenerateSummaryText()
        {
            TimeSpan duration = endTime - startTime;
            int totalSteps = currentProcedure?.steps?.Length ?? 0;

            string summary = $"Completed: {currentProcedure?.name ?? "Procedure"}\n";
            summary += $"Engine: {modelLoader?.CurrentEngine?.name ?? "Unknown"}\n";
            summary += $"Steps: {totalSteps}\n";
            summary += $"Time: {FormatDuration(duration)}\n";
            summary += $"Date: {DateTime.Now:MMM dd, yyyy}\n";

            if (!string.IsNullOrEmpty(notesInput?.text))
            {
                summary += $"\nNotes: {notesInput.text}\n";
            }

            summary += "\n#MechanicScope";

            return summary;
        }

        /// <summary>
        /// Manually shows completion for a procedure (for testing).
        /// </summary>
        public void ShowForProcedure(Procedure procedure, DateTime started, DateTime completed)
        {
            currentProcedure = procedure;
            startTime = started;
            endTime = completed;
            Show();
        }
    }
}
