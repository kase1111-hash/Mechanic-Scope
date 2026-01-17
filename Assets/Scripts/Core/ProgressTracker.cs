using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace MechanicScope.Core
{
    /// <summary>
    /// Persists procedure progress, repair history, and user preferences.
    /// Phase 1 uses JSON file storage; Phase 2 will migrate to SQLite.
    /// </summary>
    public class ProgressTracker : MonoBehaviour
    {
        // Events
        public event Action<string, int> OnProgressUpdated; // procedureId, completedCount
        public event Action<RepairLog> OnRepairLogged;
        public event Action OnDataLoaded;

        // Properties
        public bool IsLoaded { get; private set; }

        private ProgressData progressData;
        private string dataFilePath;

        private void Awake()
        {
            dataFilePath = Path.Combine(Application.persistentDataPath, "progress.json");
            LoadData();
        }

        private void LoadData()
        {
            if (File.Exists(dataFilePath))
            {
                try
                {
                    string json = File.ReadAllText(dataFilePath);
                    progressData = JsonUtility.FromJson<ProgressData>(json);
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"Failed to load progress data: {e.Message}");
                    progressData = new ProgressData();
                }
            }
            else
            {
                progressData = new ProgressData();
            }

            // Initialize collections if null
            if (progressData.procedureProgress == null)
                progressData.procedureProgress = new List<ProcedureProgressEntry>();
            if (progressData.repairHistory == null)
                progressData.repairHistory = new List<RepairLogEntry>();
            if (progressData.preferences == null)
                progressData.preferences = new List<PreferenceEntry>();

            IsLoaded = true;
            OnDataLoaded?.Invoke();
        }

        private void SaveData()
        {
            try
            {
                string json = JsonUtility.ToJson(progressData, true);
                File.WriteAllText(dataFilePath, json);
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to save progress data: {e.Message}");
            }
        }

        /// <summary>
        /// Saves progress for a procedure.
        /// </summary>
        public void SaveProgress(string procedureId, string engineId, List<int> completedStepIds)
        {
            string key = GetProgressKey(procedureId, engineId);

            ProcedureProgressEntry entry = progressData.procedureProgress.Find(p => p.key == key);
            if (entry == null)
            {
                entry = new ProcedureProgressEntry { key = key };
                progressData.procedureProgress.Add(entry);
            }

            entry.procedureId = procedureId;
            entry.engineId = engineId;
            entry.completedSteps = completedStepIds.ToArray();
            entry.lastUpdated = DateTime.Now.ToString("o");

            SaveData();
            OnProgressUpdated?.Invoke(procedureId, completedStepIds.Count);
        }

        /// <summary>
        /// Loads saved progress for a procedure.
        /// </summary>
        public List<int> LoadProgress(string procedureId, string engineId)
        {
            string key = GetProgressKey(procedureId, engineId);
            ProcedureProgressEntry entry = progressData.procedureProgress.Find(p => p.key == key);

            if (entry?.completedSteps != null)
            {
                return entry.completedSteps.ToList();
            }

            return new List<int>();
        }

        /// <summary>
        /// Clears progress for a procedure.
        /// </summary>
        public void ClearProgress(string procedureId, string engineId)
        {
            string key = GetProgressKey(procedureId, engineId);
            progressData.procedureProgress.RemoveAll(p => p.key == key);
            SaveData();
            OnProgressUpdated?.Invoke(procedureId, 0);
        }

        /// <summary>
        /// Checks if there's any saved progress for a procedure.
        /// </summary>
        public bool HasProgress(string procedureId, string engineId)
        {
            string key = GetProgressKey(procedureId, engineId);
            ProcedureProgressEntry entry = progressData.procedureProgress.Find(p => p.key == key);
            return entry?.completedSteps != null && entry.completedSteps.Length > 0;
        }

        /// <summary>
        /// Gets progress percentage for a procedure.
        /// </summary>
        public float GetProgressPercentage(string procedureId, string engineId, int totalSteps)
        {
            if (totalSteps <= 0) return 0f;

            List<int> completed = LoadProgress(procedureId, engineId);
            return (float)completed.Count / totalSteps * 100f;
        }

        /// <summary>
        /// Logs a completed repair.
        /// </summary>
        public void LogCompletedRepair(RepairLog log)
        {
            RepairLogEntry entry = new RepairLogEntry
            {
                id = log.Id ?? Guid.NewGuid().ToString(),
                procedureId = log.ProcedureId,
                engineName = log.EngineName,
                startedAt = log.StartedAt.ToString("o"),
                completedAt = log.CompletedAt.ToString("o"),
                notes = log.Notes
            };

            progressData.repairHistory.Add(entry);
            SaveData();
            OnRepairLogged?.Invoke(log);
        }

        /// <summary>
        /// Gets repair history, optionally filtered by engine.
        /// </summary>
        public List<RepairLog> GetRepairHistory(string engineName = null)
        {
            IEnumerable<RepairLogEntry> entries = progressData.repairHistory;

            if (!string.IsNullOrEmpty(engineName))
            {
                entries = entries.Where(e => e.engineName == engineName);
            }

            return entries
                .OrderByDescending(e => e.completedAt)
                .Select(e => new RepairLog
                {
                    Id = e.id,
                    ProcedureId = e.procedureId,
                    EngineName = e.engineName,
                    StartedAt = DateTime.Parse(e.startedAt),
                    CompletedAt = DateTime.Parse(e.completedAt),
                    Notes = e.notes
                })
                .ToList();
        }

        /// <summary>
        /// Deletes a repair log entry.
        /// </summary>
        public void DeleteRepairLog(string logId)
        {
            progressData.repairHistory.RemoveAll(e => e.id == logId);
            SaveData();
        }

        /// <summary>
        /// Sets a user preference.
        /// </summary>
        public void SetPreference(string key, string value)
        {
            PreferenceEntry entry = progressData.preferences.Find(p => p.key == key);
            if (entry == null)
            {
                entry = new PreferenceEntry { key = key };
                progressData.preferences.Add(entry);
            }
            entry.value = value;
            SaveData();
        }

        /// <summary>
        /// Gets a user preference.
        /// </summary>
        public string GetPreference(string key, string defaultValue = null)
        {
            PreferenceEntry entry = progressData.preferences.Find(p => p.key == key);
            return entry?.value ?? defaultValue;
        }

        /// <summary>
        /// Gets a boolean preference.
        /// </summary>
        public bool GetPreferenceBool(string key, bool defaultValue = false)
        {
            string value = GetPreference(key);
            if (string.IsNullOrEmpty(value)) return defaultValue;
            return value.ToLower() == "true" || value == "1";
        }

        /// <summary>
        /// Gets an integer preference.
        /// </summary>
        public int GetPreferenceInt(string key, int defaultValue = 0)
        {
            string value = GetPreference(key);
            if (string.IsNullOrEmpty(value)) return defaultValue;
            return int.TryParse(value, out int result) ? result : defaultValue;
        }

        /// <summary>
        /// Deletes a preference.
        /// </summary>
        public void DeletePreference(string key)
        {
            progressData.preferences.RemoveAll(p => p.key == key);
            SaveData();
        }

        /// <summary>
        /// Clears all data (progress, history, preferences).
        /// </summary>
        public void ClearAllData()
        {
            progressData = new ProgressData
            {
                procedureProgress = new List<ProcedureProgressEntry>(),
                repairHistory = new List<RepairLogEntry>(),
                preferences = new List<PreferenceEntry>()
            };
            SaveData();
        }

        /// <summary>
        /// Exports all data to JSON string.
        /// </summary>
        public string ExportData()
        {
            return JsonUtility.ToJson(progressData, true);
        }

        /// <summary>
        /// Imports data from JSON string.
        /// </summary>
        public void ImportData(string json)
        {
            try
            {
                ProgressData imported = JsonUtility.FromJson<ProgressData>(json);
                if (imported != null)
                {
                    // Merge with existing data
                    foreach (var entry in imported.procedureProgress ?? new List<ProcedureProgressEntry>())
                    {
                        if (!progressData.procedureProgress.Any(p => p.key == entry.key))
                        {
                            progressData.procedureProgress.Add(entry);
                        }
                    }

                    foreach (var entry in imported.repairHistory ?? new List<RepairLogEntry>())
                    {
                        if (!progressData.repairHistory.Any(r => r.id == entry.id))
                        {
                            progressData.repairHistory.Add(entry);
                        }
                    }

                    SaveData();
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to import data: {e.Message}");
            }
        }

        /// <summary>
        /// Gets all procedures with saved progress.
        /// </summary>
        public List<(string procedureId, string engineId, int completedCount, DateTime lastUpdated)> GetAllProgress()
        {
            return progressData.procedureProgress
                .Select(p => (
                    p.procedureId,
                    p.engineId,
                    p.completedSteps?.Length ?? 0,
                    string.IsNullOrEmpty(p.lastUpdated) ? DateTime.MinValue : DateTime.Parse(p.lastUpdated)
                ))
                .OrderByDescending(p => p.Item4)
                .ToList();
        }

        private string GetProgressKey(string procedureId, string engineId)
        {
            return $"{engineId}_{procedureId}";
        }
    }

    /// <summary>
    /// Repair log entry for completed repairs.
    /// </summary>
    public class RepairLog
    {
        public string Id { get; set; }
        public string ProcedureId { get; set; }
        public string EngineName { get; set; }
        public DateTime StartedAt { get; set; }
        public DateTime CompletedAt { get; set; }
        public string Notes { get; set; }
    }

    // JSON serialization structures
    [Serializable]
    public class ProgressData
    {
        public List<ProcedureProgressEntry> procedureProgress;
        public List<RepairLogEntry> repairHistory;
        public List<PreferenceEntry> preferences;
    }

    [Serializable]
    public class ProcedureProgressEntry
    {
        public string key;
        public string procedureId;
        public string engineId;
        public int[] completedSteps;
        public string lastUpdated;
    }

    [Serializable]
    public class RepairLogEntry
    {
        public string id;
        public string procedureId;
        public string engineName;
        public string startedAt;
        public string completedAt;
        public string notes;
    }

    [Serializable]
    public class PreferenceEntry
    {
        public string key;
        public string value;
    }
}
