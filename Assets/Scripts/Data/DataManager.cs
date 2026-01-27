using System;
using System.IO;
using UnityEngine;

namespace MechanicScope.Data
{
    /// <summary>
    /// Central data manager that initializes and provides access to all repositories.
    /// Handles database initialization, migration, and lifecycle management.
    /// </summary>
    public class DataManager : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField] private bool initializeOnAwake = true;
        [SerializeField] private bool importDefaultPartsOnFirstRun = true;
        [SerializeField] private TextAsset defaultPartsData;

        public static DataManager Instance { get; private set; }

        // Repositories
        public PartRepository Parts { get; private set; }
        public ProgressRepository Progress { get; private set; }

        // Events
        public event Action OnInitialized;
        public event Action<string> OnError;

        // Properties
        public bool IsInitialized { get; private set; }
        public string DatabaseDirectory => Path.Combine(Application.persistentDataPath, "database");

        private void Awake()
        {
            // Singleton pattern
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            if (initializeOnAwake)
            {
                Initialize();
            }
        }

        private void OnDestroy()
        {
            Shutdown();
            if (Instance == this)
            {
                Instance = null;
            }
        }

        /// <summary>
        /// Initializes all data repositories.
        /// </summary>
        public void Initialize()
        {
            if (IsInitialized) return;

            try
            {
                // Ensure database directory exists
                if (!Directory.Exists(DatabaseDirectory))
                {
                    Directory.CreateDirectory(DatabaseDirectory);
                }

                // Initialize repositories
                string partsDbPath = Path.Combine(DatabaseDirectory, "parts.db");
                string progressDbPath = Path.Combine(DatabaseDirectory, "progress.db");

                bool isFirstRun = !File.Exists(partsDbPath);

                Parts = new PartRepository();
                Parts.Initialize(partsDbPath);

                Progress = new ProgressRepository();
                Progress.Initialize(progressDbPath);

                // Import default parts data on first run
                if (isFirstRun && importDefaultPartsOnFirstRun)
                {
                    ImportDefaultParts();
                }

                IsInitialized = true;
                Debug.Log("DataManager initialized successfully");
                OnInitialized?.Invoke();
            }
            catch (Exception e)
            {
                Debug.LogError($"DataManager initialization failed: {e.Message}");
                OnError?.Invoke(e.Message);
            }
        }

        /// <summary>
        /// Shuts down all repositories and releases resources.
        /// </summary>
        public void Shutdown()
        {
            Parts?.Dispose();
            Parts = null;

            Progress?.Dispose();
            Progress = null;

            IsInitialized = false;
        }

        /// <summary>
        /// Imports default parts data from the bundled JSON file.
        /// </summary>
        public void ImportDefaultParts()
        {
            if (defaultPartsData == null)
            {
                // Try to load from Resources
                defaultPartsData = Resources.Load<TextAsset>("DefaultPartsData");
            }

            if (defaultPartsData != null && Parts != null)
            {
                Parts.ImportFromJson(defaultPartsData.text);
                Debug.Log("Imported default parts data");
            }
        }

        /// <summary>
        /// Resets all data to initial state.
        /// </summary>
        public void ResetAllData()
        {
            Shutdown();

            // Delete database files
            string partsDbPath = Path.Combine(DatabaseDirectory, "parts.db");
            string progressDbPath = Path.Combine(DatabaseDirectory, "progress.db");

            if (File.Exists(partsDbPath))
            {
                File.Delete(partsDbPath);
            }

            if (File.Exists(progressDbPath))
            {
                File.Delete(progressDbPath);
            }

            // Reinitialize
            Initialize();
        }

        /// <summary>
        /// Exports all data to a backup file.
        /// </summary>
        /// <returns>Backup path on success, null on failure</returns>
        public string ExportBackup()
        {
            try
            {
                string backupDir = Path.Combine(Application.persistentDataPath, "backups");
                if (!Directory.Exists(backupDir))
                {
                    Directory.CreateDirectory(backupDir);
                }

                string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                string backupPath = Path.Combine(backupDir, $"backup_{timestamp}");
                Directory.CreateDirectory(backupPath);

                // Copy database files
                string partsDbPath = Path.Combine(DatabaseDirectory, "parts.db");
                string progressDbPath = Path.Combine(DatabaseDirectory, "progress.db");
                bool anyFilesCopied = false;

                if (File.Exists(partsDbPath))
                {
                    string destPath = Path.Combine(backupPath, "parts.db");
                    File.Copy(partsDbPath, destPath);
                    // Verify the copy succeeded
                    if (!File.Exists(destPath))
                    {
                        throw new IOException($"Failed to copy parts.db to backup");
                    }
                    anyFilesCopied = true;
                }

                if (File.Exists(progressDbPath))
                {
                    string destPath = Path.Combine(backupPath, "progress.db");
                    File.Copy(progressDbPath, destPath);
                    // Verify the copy succeeded
                    if (!File.Exists(destPath))
                    {
                        throw new IOException($"Failed to copy progress.db to backup");
                    }
                    anyFilesCopied = true;
                }

                if (!anyFilesCopied)
                {
                    // No files to backup - clean up empty directory
                    Directory.Delete(backupPath);
                    Debug.LogWarning("No database files found to backup");
                    OnError?.Invoke("No database files found to backup");
                    return null;
                }

                Debug.Log($"Backup created at: {backupPath}");
                return backupPath;
            }
            catch (Exception e)
            {
                Debug.LogError($"Backup failed: {e.Message}");
                OnError?.Invoke($"Backup failed: {e.Message}");
                return null;
            }
        }

        /// <summary>
        /// Restores data from a backup.
        /// </summary>
        public void RestoreBackup(string backupPath)
        {
            if (!Directory.Exists(backupPath))
            {
                OnError?.Invoke("Backup path does not exist");
                return;
            }

            Shutdown();

            // Copy backup files to database directory
            string partsBackup = Path.Combine(backupPath, "parts.db");
            string progressBackup = Path.Combine(backupPath, "progress.db");

            if (File.Exists(partsBackup))
            {
                File.Copy(partsBackup, Path.Combine(DatabaseDirectory, "parts.db"), true);
            }

            if (File.Exists(progressBackup))
            {
                File.Copy(progressBackup, Path.Combine(DatabaseDirectory, "progress.db"), true);
            }

            Initialize();
            Debug.Log("Backup restored successfully");
        }

        /// <summary>
        /// Gets database statistics for debugging.
        /// </summary>
        public DatabaseStats GetStats()
        {
            return new DatabaseStats
            {
                PartCount = Parts?.GetPartCount() ?? 0,
                CategoryCount = Parts?.GetCategories().Count ?? 0,
                InProgressProcedures = Progress?.GetAllProgress().Count ?? 0,
                CompletedRepairs = Progress?.GetRepairHistory(limit: int.MaxValue).Count ?? 0
            };
        }
    }

    public class DatabaseStats
    {
        public int PartCount { get; set; }
        public int CategoryCount { get; set; }
        public int InProgressProcedures { get; set; }
        public int CompletedRepairs { get; set; }

        public override string ToString()
        {
            return $"Parts: {PartCount}, Categories: {CategoryCount}, In Progress: {InProgressProcedures}, Completed: {CompletedRepairs}";
        }
    }
}
