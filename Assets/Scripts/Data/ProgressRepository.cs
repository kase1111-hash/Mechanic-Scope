using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using MechanicScope.Core;

namespace MechanicScope.Data
{
    /// <summary>
    /// SQLite-based repository for progress tracking, repair history, and preferences.
    /// </summary>
    public class ProgressRepository : IDisposable
    {
        private SQLiteDatabase db;
        private bool isDisposed;

        public bool IsInitialized => db != null;

        /// <summary>
        /// Initializes the progress repository with the database at the given path.
        /// </summary>
        public void Initialize(string databasePath)
        {
            db = new SQLiteDatabase(databasePath);
            RunMigrations();
        }

        private void RunMigrations()
        {
            var migrator = new DatabaseMigrator(db);

            migrator.AddMigration(1, "Create progress tables", database =>
            {
                // Procedure progress table
                database.ExecuteNonQuery(@"
                    CREATE TABLE IF NOT EXISTS procedure_progress (
                        id INTEGER PRIMARY KEY AUTOINCREMENT,
                        procedure_id TEXT NOT NULL,
                        engine_id TEXT NOT NULL,
                        completed_steps TEXT,
                        started_at TEXT,
                        last_updated TEXT DEFAULT CURRENT_TIMESTAMP,
                        UNIQUE(procedure_id, engine_id)
                    )
                ");

                // Repair history table
                database.ExecuteNonQuery(@"
                    CREATE TABLE IF NOT EXISTS repair_history (
                        id TEXT PRIMARY KEY,
                        procedure_id TEXT NOT NULL,
                        procedure_name TEXT,
                        engine_id TEXT,
                        engine_name TEXT,
                        started_at TEXT NOT NULL,
                        completed_at TEXT NOT NULL,
                        duration_minutes INTEGER,
                        notes TEXT,
                        rating INTEGER
                    )
                ");

                // Preferences table
                database.ExecuteNonQuery(@"
                    CREATE TABLE IF NOT EXISTS preferences (
                        key TEXT PRIMARY KEY,
                        value TEXT,
                        updated_at TEXT DEFAULT CURRENT_TIMESTAMP
                    )
                ");

                // Create indexes
                database.ExecuteNonQuery(
                    "CREATE INDEX IF NOT EXISTS idx_repair_history_engine ON repair_history(engine_id)"
                );
                database.ExecuteNonQuery(
                    "CREATE INDEX IF NOT EXISTS idx_repair_history_date ON repair_history(completed_at DESC)"
                );
            });

            migrator.AddMigration(2, "Add statistics table", database =>
            {
                database.ExecuteNonQuery(@"
                    CREATE TABLE IF NOT EXISTS repair_statistics (
                        id INTEGER PRIMARY KEY AUTOINCREMENT,
                        procedure_id TEXT NOT NULL,
                        engine_id TEXT NOT NULL,
                        times_completed INTEGER DEFAULT 0,
                        total_duration_minutes INTEGER DEFAULT 0,
                        avg_duration_minutes REAL DEFAULT 0,
                        last_completed_at TEXT,
                        UNIQUE(procedure_id, engine_id)
                    )
                ");
            });

            migrator.RunMigrations();
        }

        #region Progress Methods

        /// <summary>
        /// Saves progress for a procedure.
        /// </summary>
        public void SaveProgress(string procedureId, string engineId, List<int> completedStepIds)
        {
            string stepsJson = completedStepIds != null && completedStepIds.Count > 0
                ? string.Join(",", completedStepIds)
                : "";

            db.ExecuteNonQuery(
                @"INSERT INTO procedure_progress (procedure_id, engine_id, completed_steps, last_updated)
                  VALUES (@procedureId, @engineId, @steps, CURRENT_TIMESTAMP)
                  ON CONFLICT(procedure_id, engine_id) DO UPDATE SET
                      completed_steps = @steps,
                      last_updated = CURRENT_TIMESTAMP",
                new Dictionary<string, object>
                {
                    { "@procedureId", procedureId },
                    { "@engineId", engineId },
                    { "@steps", stepsJson }
                }
            );
        }

        /// <summary>
        /// Loads saved progress for a procedure.
        /// </summary>
        public List<int> LoadProgress(string procedureId, string engineId)
        {
            var row = db.ExecuteQuerySingle(
                "SELECT completed_steps FROM procedure_progress WHERE procedure_id = @procedureId AND engine_id = @engineId",
                new Dictionary<string, object>
                {
                    { "@procedureId", procedureId },
                    { "@engineId", engineId }
                }
            );

            if (row == null) return new List<int>();

            string stepsString = row["completed_steps"]?.ToString();
            if (string.IsNullOrEmpty(stepsString)) return new List<int>();

            var result = new List<int>();
            foreach (var s in stepsString.Split(','))
            {
                if (!string.IsNullOrEmpty(s) && int.TryParse(s.Trim(), out int stepId))
                {
                    result.Add(stepId);
                }
            }
            return result;
        }

        /// <summary>
        /// Clears progress for a procedure.
        /// </summary>
        public void ClearProgress(string procedureId, string engineId)
        {
            db.ExecuteNonQuery(
                "DELETE FROM procedure_progress WHERE procedure_id = @procedureId AND engine_id = @engineId",
                new Dictionary<string, object>
                {
                    { "@procedureId", procedureId },
                    { "@engineId", engineId }
                }
            );
        }

        /// <summary>
        /// Checks if there's any saved progress for a procedure.
        /// </summary>
        public bool HasProgress(string procedureId, string engineId)
        {
            var result = db.ExecuteScalar(
                @"SELECT COUNT(*) FROM procedure_progress
                  WHERE procedure_id = @procedureId AND engine_id = @engineId
                  AND completed_steps IS NOT NULL AND completed_steps != ''",
                new Dictionary<string, object>
                {
                    { "@procedureId", procedureId },
                    { "@engineId", engineId }
                }
            );
            return Convert.ToInt32(result) > 0;
        }

        /// <summary>
        /// Gets all procedures with saved progress.
        /// </summary>
        public List<ProgressSummary> GetAllProgress()
        {
            var rows = db.ExecuteQuery(
                "SELECT * FROM procedure_progress WHERE completed_steps IS NOT NULL AND completed_steps != '' ORDER BY last_updated DESC"
            );

            return rows.Select(r => new ProgressSummary
            {
                ProcedureId = r["procedure_id"]?.ToString(),
                EngineId = r["engine_id"]?.ToString(),
                CompletedStepCount = (r["completed_steps"]?.ToString() ?? "").Split(',').Where(s => !string.IsNullOrEmpty(s)).Count(),
                LastUpdated = DateTime.TryParse(r["last_updated"]?.ToString(), out var dt) ? dt : DateTime.MinValue
            }).ToList();
        }

        #endregion

        #region Repair History Methods

        /// <summary>
        /// Logs a completed repair.
        /// </summary>
        public void LogCompletedRepair(RepairLogEntry entry)
        {
            int durationMinutes = (int)(entry.CompletedAt - entry.StartedAt).TotalMinutes;

            db.ExecuteNonQuery(
                @"INSERT INTO repair_history (id, procedure_id, procedure_name, engine_id, engine_name,
                  started_at, completed_at, duration_minutes, notes, rating)
                  VALUES (@id, @procedureId, @procedureName, @engineId, @engineName,
                  @startedAt, @completedAt, @duration, @notes, @rating)",
                new Dictionary<string, object>
                {
                    { "@id", entry.Id ?? Guid.NewGuid().ToString() },
                    { "@procedureId", entry.ProcedureId },
                    { "@procedureName", entry.ProcedureName },
                    { "@engineId", entry.EngineId },
                    { "@engineName", entry.EngineName },
                    { "@startedAt", entry.StartedAt.ToString("o") },
                    { "@completedAt", entry.CompletedAt.ToString("o") },
                    { "@duration", durationMinutes },
                    { "@notes", entry.Notes },
                    { "@rating", entry.Rating }
                }
            );

            // Update statistics
            UpdateStatistics(entry.ProcedureId, entry.EngineId, durationMinutes, entry.CompletedAt);
        }

        /// <summary>
        /// Gets repair history, optionally filtered by engine.
        /// </summary>
        public List<RepairLogEntry> GetRepairHistory(string engineId = null, int limit = 50)
        {
            string sql = "SELECT * FROM repair_history";
            var parameters = new Dictionary<string, object>();

            if (!string.IsNullOrEmpty(engineId))
            {
                sql += " WHERE engine_id = @engineId";
                parameters["@engineId"] = engineId;
            }

            sql += " ORDER BY completed_at DESC LIMIT @limit";
            parameters["@limit"] = limit;

            var rows = db.ExecuteQuery(sql, parameters);

            return rows.Select(r => new RepairLogEntry
            {
                Id = r["id"]?.ToString(),
                ProcedureId = r["procedure_id"]?.ToString(),
                ProcedureName = r["procedure_name"]?.ToString(),
                EngineId = r["engine_id"]?.ToString(),
                EngineName = r["engine_name"]?.ToString(),
                StartedAt = DateTime.TryParse(r["started_at"]?.ToString(), out var start) ? start : DateTime.MinValue,
                CompletedAt = DateTime.TryParse(r["completed_at"]?.ToString(), out var end) ? end : DateTime.MinValue,
                DurationMinutes = Convert.ToInt32(r["duration_minutes"] ?? 0),
                Notes = r["notes"]?.ToString(),
                Rating = r["rating"] != null ? Convert.ToInt32(r["rating"]) : (int?)null
            }).ToList();
        }

        /// <summary>
        /// Deletes a repair history entry.
        /// </summary>
        public void DeleteRepairLog(string logId)
        {
            db.ExecuteNonQuery(
                "DELETE FROM repair_history WHERE id = @id",
                new Dictionary<string, object> { { "@id", logId } }
            );
        }

        /// <summary>
        /// Gets repair statistics for a procedure.
        /// </summary>
        public RepairStatistics GetStatistics(string procedureId, string engineId)
        {
            var row = db.ExecuteQuerySingle(
                "SELECT * FROM repair_statistics WHERE procedure_id = @procedureId AND engine_id = @engineId",
                new Dictionary<string, object>
                {
                    { "@procedureId", procedureId },
                    { "@engineId", engineId }
                }
            );

            if (row == null) return null;

            return new RepairStatistics
            {
                ProcedureId = row["procedure_id"]?.ToString(),
                EngineId = row["engine_id"]?.ToString(),
                TimesCompleted = Convert.ToInt32(row["times_completed"] ?? 0),
                TotalDurationMinutes = Convert.ToInt32(row["total_duration_minutes"] ?? 0),
                AverageDurationMinutes = Convert.ToSingle(row["avg_duration_minutes"] ?? 0),
                LastCompletedAt = DateTime.TryParse(row["last_completed_at"]?.ToString(), out var dt) ? dt : (DateTime?)null
            };
        }

        private void UpdateStatistics(string procedureId, string engineId, int durationMinutes, DateTime completedAt)
        {
            db.ExecuteNonQuery(
                @"INSERT INTO repair_statistics (procedure_id, engine_id, times_completed, total_duration_minutes, avg_duration_minutes, last_completed_at)
                  VALUES (@procedureId, @engineId, 1, @duration, @duration, @completedAt)
                  ON CONFLICT(procedure_id, engine_id) DO UPDATE SET
                      times_completed = times_completed + 1,
                      total_duration_minutes = total_duration_minutes + @duration,
                      avg_duration_minutes = (total_duration_minutes + @duration) / (times_completed + 1),
                      last_completed_at = @completedAt",
                new Dictionary<string, object>
                {
                    { "@procedureId", procedureId },
                    { "@engineId", engineId },
                    { "@duration", durationMinutes },
                    { "@completedAt", completedAt.ToString("o") }
                }
            );
        }

        #endregion

        #region Preferences Methods

        /// <summary>
        /// Sets a user preference.
        /// </summary>
        public void SetPreference(string key, string value)
        {
            db.ExecuteNonQuery(
                @"INSERT INTO preferences (key, value, updated_at)
                  VALUES (@key, @value, CURRENT_TIMESTAMP)
                  ON CONFLICT(key) DO UPDATE SET
                      value = @value,
                      updated_at = CURRENT_TIMESTAMP",
                new Dictionary<string, object>
                {
                    { "@key", key },
                    { "@value", value }
                }
            );
        }

        /// <summary>
        /// Gets a user preference.
        /// </summary>
        public string GetPreference(string key, string defaultValue = null)
        {
            var result = db.ExecuteScalar(
                "SELECT value FROM preferences WHERE key = @key",
                new Dictionary<string, object> { { "@key", key } }
            );

            return result?.ToString() ?? defaultValue;
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
        /// Gets a float preference.
        /// </summary>
        public float GetPreferenceFloat(string key, float defaultValue = 0f)
        {
            string value = GetPreference(key);
            if (string.IsNullOrEmpty(value)) return defaultValue;
            return float.TryParse(value, out float result) ? result : defaultValue;
        }

        /// <summary>
        /// Deletes a preference.
        /// </summary>
        public void DeletePreference(string key)
        {
            db.ExecuteNonQuery(
                "DELETE FROM preferences WHERE key = @key",
                new Dictionary<string, object> { { "@key", key } }
            );
        }

        /// <summary>
        /// Gets all preferences as a dictionary.
        /// </summary>
        public Dictionary<string, string> GetAllPreferences()
        {
            var rows = db.ExecuteQuery("SELECT key, value FROM preferences");
            return rows.ToDictionary(
                r => r["key"]?.ToString() ?? "",
                r => r["value"]?.ToString()
            );
        }

        #endregion

        /// <summary>
        /// Clears all data from the repository.
        /// </summary>
        public void ClearAllData()
        {
            db.ExecuteNonQuery("DELETE FROM procedure_progress");
            db.ExecuteNonQuery("DELETE FROM repair_history");
            db.ExecuteNonQuery("DELETE FROM repair_statistics");
            db.ExecuteNonQuery("DELETE FROM preferences");
        }

        public void Dispose()
        {
            if (isDisposed) return;

            db?.Dispose();
            db = null;
            isDisposed = true;
        }
    }

    #region Data Classes

    public class ProgressSummary
    {
        public string ProcedureId { get; set; }
        public string EngineId { get; set; }
        public int CompletedStepCount { get; set; }
        public DateTime LastUpdated { get; set; }
    }

    public class RepairLogEntry
    {
        public string Id { get; set; }
        public string ProcedureId { get; set; }
        public string ProcedureName { get; set; }
        public string EngineId { get; set; }
        public string EngineName { get; set; }
        public DateTime StartedAt { get; set; }
        public DateTime CompletedAt { get; set; }
        public int DurationMinutes { get; set; }
        public string Notes { get; set; }
        public int? Rating { get; set; }
    }

    public class RepairStatistics
    {
        public string ProcedureId { get; set; }
        public string EngineId { get; set; }
        public int TimesCompleted { get; set; }
        public int TotalDurationMinutes { get; set; }
        public float AverageDurationMinutes { get; set; }
        public DateTime? LastCompletedAt { get; set; }
    }

    #endregion
}
