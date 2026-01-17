using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace MechanicScope.Core
{
    /// <summary>
    /// Loads, parses, and executes procedure graphs with dependency resolution.
    /// Handles step sequencing, progress tracking, and part highlighting.
    /// </summary>
    public class ProcedureRunner : MonoBehaviour
    {
        [Header("Dependencies")]
        [SerializeField] private EngineModelLoader modelLoader;
        [SerializeField] private ProgressTracker progressTracker;

        // Events
        public event Action<Procedure> OnProcedureLoaded;
        public event Action<ProcedureStep> OnStepActivated;
        public event Action<ProcedureStep> OnStepCompleted;
        public event Action<ProcedureStep> OnStepUncompleted;
        public event Action OnProcedureCompleted;
        public event Action<List<string>> OnHighlightPartsChanged;
        public event Action<string> OnLoadError;

        // Properties
        public Procedure CurrentProcedure { get; private set; }
        public List<ProcedureStep> AvailableSteps { get; private set; } = new List<ProcedureStep>();
        public List<ProcedureStep> CompletedSteps { get; private set; } = new List<ProcedureStep>();
        public ProcedureStep ActiveStep { get; private set; }
        public bool IsLoaded => CurrentProcedure != null;

        public float ProgressPercentage
        {
            get
            {
                if (CurrentProcedure == null || CurrentProcedure.steps == null || CurrentProcedure.steps.Length == 0)
                    return 0f;
                return (float)CompletedSteps.Count / CurrentProcedure.steps.Length * 100f;
            }
        }

        private HashSet<int> completedStepIds = new HashSet<int>();
        private Dictionary<string, List<Procedure>> procedureCache = new Dictionary<string, List<Procedure>>();

        /// <summary>
        /// Gets all available procedures for an engine.
        /// </summary>
        public List<Procedure> GetProceduresForEngine(string engineId)
        {
            if (procedureCache.ContainsKey(engineId))
            {
                return procedureCache[engineId];
            }

            List<Procedure> procedures = new List<Procedure>();

            // Check persistent data path
            string userProceduresPath = Path.Combine(Application.persistentDataPath, "engines", engineId, "procedures");
            LoadProceduresFromDirectory(userProceduresPath, procedures);

            // Check streaming assets for bundled procedures
            string bundledProceduresPath = Path.Combine(Application.streamingAssetsPath, "Engines", engineId, "procedures");
            LoadProceduresFromDirectory(bundledProceduresPath, procedures);

            // Also check global procedures directory
            string globalProceduresPath = Path.Combine(Application.streamingAssetsPath, "Procedures", engineId);
            LoadProceduresFromDirectory(globalProceduresPath, procedures);

            procedureCache[engineId] = procedures;
            return procedures;
        }

        private void LoadProceduresFromDirectory(string path, List<Procedure> procedures)
        {
            if (!Directory.Exists(path)) return;

            string[] files = Directory.GetFiles(path, "*.json");
            foreach (string file in files)
            {
                try
                {
                    string json = File.ReadAllText(file);
                    Procedure procedure = JsonUtility.FromJson<Procedure>(json);
                    if (procedure != null && !string.IsNullOrEmpty(procedure.id))
                    {
                        procedure.FilePath = file;
                        procedures.Add(procedure);
                    }
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"Failed to load procedure from {file}: {e.Message}");
                }
            }
        }

        /// <summary>
        /// Loads a procedure by ID for a specific engine.
        /// </summary>
        public void LoadProcedure(string procedureId, string engineId)
        {
            List<Procedure> procedures = GetProceduresForEngine(engineId);
            Procedure procedure = procedures.Find(p => p.id == procedureId);

            if (procedure == null)
            {
                OnLoadError?.Invoke($"Procedure '{procedureId}' not found for engine '{engineId}'");
                return;
            }

            LoadProcedure(procedure, engineId);
        }

        /// <summary>
        /// Loads a procedure directly.
        /// </summary>
        public void LoadProcedure(Procedure procedure, string engineId)
        {
            CurrentProcedure = procedure;
            completedStepIds.Clear();
            CompletedSteps.Clear();

            // Load saved progress if available
            if (progressTracker != null)
            {
                List<int> savedProgress = progressTracker.LoadProgress(procedure.id, engineId);
                foreach (int stepId in savedProgress)
                {
                    completedStepIds.Add(stepId);
                    ProcedureStep step = FindStepById(stepId);
                    if (step != null)
                    {
                        CompletedSteps.Add(step);
                    }
                }
            }

            UpdateAvailableSteps();
            OnProcedureLoaded?.Invoke(procedure);

            // Set first available step as active
            if (AvailableSteps.Count > 0)
            {
                SetActiveStep(AvailableSteps[0]);
            }
        }

        /// <summary>
        /// Loads a procedure from a JSON string.
        /// </summary>
        public void LoadProcedureFromJson(string json, string engineId)
        {
            try
            {
                Procedure procedure = JsonUtility.FromJson<Procedure>(json);
                if (procedure != null)
                {
                    LoadProcedure(procedure, engineId);
                }
            }
            catch (Exception e)
            {
                OnLoadError?.Invoke($"Failed to parse procedure JSON: {e.Message}");
            }
        }

        /// <summary>
        /// Updates the list of available steps based on completed dependencies.
        /// </summary>
        private void UpdateAvailableSteps()
        {
            AvailableSteps.Clear();

            if (CurrentProcedure == null || CurrentProcedure.steps == null) return;

            foreach (ProcedureStep step in CurrentProcedure.steps)
            {
                // Skip already completed steps
                if (completedStepIds.Contains(step.id)) continue;

                // Check if all dependencies are satisfied
                bool dependenciesMet = true;
                if (step.requires != null && step.requires.Length > 0)
                {
                    foreach (int requiredId in step.requires)
                    {
                        if (!completedStepIds.Contains(requiredId))
                        {
                            dependenciesMet = false;
                            break;
                        }
                    }
                }

                if (dependenciesMet)
                {
                    AvailableSteps.Add(step);
                }
            }

            // Update part highlighting
            UpdatePartHighlights();
        }

        /// <summary>
        /// Marks a step as completed.
        /// </summary>
        public void CompleteStep(int stepId)
        {
            if (CurrentProcedure == null) return;

            ProcedureStep step = FindStepById(stepId);
            if (step == null) return;

            // Check if step is available (dependencies met)
            if (!AvailableSteps.Contains(step))
            {
                Debug.LogWarning($"Cannot complete step {stepId}: dependencies not met");
                return;
            }

            completedStepIds.Add(stepId);
            CompletedSteps.Add(step);
            UpdateAvailableSteps();

            // Save progress
            if (progressTracker != null)
            {
                progressTracker.SaveProgress(CurrentProcedure.id, CurrentProcedure.engineId, completedStepIds.ToList());
            }

            OnStepCompleted?.Invoke(step);

            // Check if procedure is complete
            if (CurrentProcedure.steps != null && completedStepIds.Count >= CurrentProcedure.steps.Length)
            {
                OnProcedureCompleted?.Invoke();
            }
            else if (AvailableSteps.Count > 0)
            {
                // Activate next available step
                SetActiveStep(AvailableSteps[0]);
            }
        }

        /// <summary>
        /// Marks a step as not completed (undo).
        /// </summary>
        public void UncompleteStep(int stepId)
        {
            if (CurrentProcedure == null) return;
            if (!completedStepIds.Contains(stepId)) return;

            ProcedureStep step = FindStepById(stepId);
            if (step == null) return;

            // Check if any completed step depends on this one
            foreach (int completedId in completedStepIds)
            {
                if (completedId == stepId) continue;
                ProcedureStep completedStep = FindStepById(completedId);
                if (completedStep?.requires != null && completedStep.requires.Contains(stepId))
                {
                    Debug.LogWarning($"Cannot uncomplete step {stepId}: step {completedId} depends on it");
                    return;
                }
            }

            completedStepIds.Remove(stepId);
            CompletedSteps.Remove(step);
            UpdateAvailableSteps();

            // Save progress
            if (progressTracker != null)
            {
                progressTracker.SaveProgress(CurrentProcedure.id, CurrentProcedure.engineId, completedStepIds.ToList());
            }

            OnStepUncompleted?.Invoke(step);
            SetActiveStep(step);
        }

        /// <summary>
        /// Sets the currently active step (for display purposes).
        /// </summary>
        public void SetActiveStep(ProcedureStep step)
        {
            if (step == null || completedStepIds.Contains(step.id)) return;

            ActiveStep = step;
            UpdatePartHighlights();
            OnStepActivated?.Invoke(step);
        }

        /// <summary>
        /// Sets active step by ID.
        /// </summary>
        public void SetActiveStep(int stepId)
        {
            ProcedureStep step = FindStepById(stepId);
            if (step != null)
            {
                SetActiveStep(step);
            }
        }

        /// <summary>
        /// Advances to the next available step.
        /// </summary>
        public void NextStep()
        {
            if (ActiveStep == null || AvailableSteps.Count == 0) return;

            int currentIndex = AvailableSteps.IndexOf(ActiveStep);
            if (currentIndex < AvailableSteps.Count - 1)
            {
                SetActiveStep(AvailableSteps[currentIndex + 1]);
            }
        }

        /// <summary>
        /// Goes to the previous available step.
        /// </summary>
        public void PreviousStep()
        {
            if (ActiveStep == null || AvailableSteps.Count == 0) return;

            int currentIndex = AvailableSteps.IndexOf(ActiveStep);
            if (currentIndex > 0)
            {
                SetActiveStep(AvailableSteps[currentIndex - 1]);
            }
        }

        /// <summary>
        /// Resets all progress on the current procedure.
        /// </summary>
        public void ResetProcedure()
        {
            if (CurrentProcedure == null) return;

            completedStepIds.Clear();
            CompletedSteps.Clear();
            UpdateAvailableSteps();

            // Clear saved progress
            if (progressTracker != null)
            {
                progressTracker.ClearProgress(CurrentProcedure.id, CurrentProcedure.engineId);
            }

            if (AvailableSteps.Count > 0)
            {
                SetActiveStep(AvailableSteps[0]);
            }
        }

        /// <summary>
        /// Unloads the current procedure.
        /// </summary>
        public void UnloadProcedure()
        {
            CurrentProcedure = null;
            ActiveStep = null;
            completedStepIds.Clear();
            CompletedSteps.Clear();
            AvailableSteps.Clear();

            if (modelLoader != null)
            {
                modelLoader.ClearHighlights();
            }
        }

        /// <summary>
        /// Gets the list of part IDs that should be highlighted for available steps.
        /// </summary>
        public List<string> GetHighlightedParts()
        {
            List<string> parts = new List<string>();

            // Highlight active step's part more prominently
            if (ActiveStep != null && !string.IsNullOrEmpty(ActiveStep.partId))
            {
                parts.Add(ActiveStep.partId);
            }

            return parts;
        }

        /// <summary>
        /// Gets all parts involved in currently available steps.
        /// </summary>
        public List<string> GetAllAvailableStepParts()
        {
            List<string> parts = new List<string>();

            foreach (var step in AvailableSteps)
            {
                if (!string.IsNullOrEmpty(step.partId) && !parts.Contains(step.partId))
                {
                    parts.Add(step.partId);
                }
            }

            return parts;
        }

        private void UpdatePartHighlights()
        {
            List<string> highlightParts = GetHighlightedParts();
            OnHighlightPartsChanged?.Invoke(highlightParts);

            if (modelLoader != null)
            {
                modelLoader.HighlightParts(highlightParts);
            }
        }

        private ProcedureStep FindStepById(int stepId)
        {
            if (CurrentProcedure == null || CurrentProcedure.steps == null) return null;
            return CurrentProcedure.steps.FirstOrDefault(s => s.id == stepId);
        }

        /// <summary>
        /// Gets step information by ID.
        /// </summary>
        public ProcedureStep GetStep(int stepId)
        {
            return FindStepById(stepId);
        }

        /// <summary>
        /// Checks if a step is completed.
        /// </summary>
        public bool IsStepCompleted(int stepId)
        {
            return completedStepIds.Contains(stepId);
        }

        /// <summary>
        /// Checks if a step is currently available (dependencies met, not completed).
        /// </summary>
        public bool IsStepAvailable(int stepId)
        {
            return AvailableSteps.Any(s => s.id == stepId);
        }

        /// <summary>
        /// Gets the dependency status for a step.
        /// </summary>
        public StepStatus GetStepStatus(int stepId)
        {
            if (completedStepIds.Contains(stepId))
                return StepStatus.Completed;
            if (AvailableSteps.Any(s => s.id == stepId))
                return StepStatus.Available;
            return StepStatus.Blocked;
        }

        /// <summary>
        /// Clears the procedure cache, forcing reload on next access.
        /// </summary>
        public void ClearCache()
        {
            procedureCache.Clear();
        }
    }

    public enum StepStatus
    {
        Blocked,
        Available,
        Completed
    }

    /// <summary>
    /// Procedure data structure matching procedure.json format.
    /// </summary>
    [Serializable]
    public class Procedure
    {
        public string id;
        public string name;
        public string description;
        public string engineId;
        public string estimatedTime;
        public string difficulty;
        public string[] tools;
        public ProcedureStep[] steps;
        public string reinstallNotes;

        // Runtime properties
        [NonSerialized] public string FilePath;
    }

    /// <summary>
    /// Individual step within a procedure.
    /// </summary>
    [Serializable]
    public class ProcedureStep
    {
        public int id;
        public string action;
        public string details;
        public string partId;
        public string[] tools;
        public string[] warnings;
        public int[] requires;
        public TorqueSpec torqueSpec;
        public StepMedia media;
    }

    [Serializable]
    public class TorqueSpec
    {
        public float value;
        public string unit;
        public string note;

        public override string ToString()
        {
            string result = $"{value} {unit}";
            if (!string.IsNullOrEmpty(note))
            {
                result += $" ({note})";
            }
            return result;
        }
    }

    [Serializable]
    public class StepMedia
    {
        public string image;
        public string video;
    }
}
