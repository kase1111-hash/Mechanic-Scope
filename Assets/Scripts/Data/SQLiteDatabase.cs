using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace MechanicScope.Data
{
    /// <summary>
    /// Lightweight SQLite wrapper for Unity.
    /// Uses Mono.Data.Sqlite which is included in Unity.
    /// </summary>
    public class SQLiteDatabase : IDisposable
    {
        private Mono.Data.Sqlite.SqliteConnection connection;
        private bool isDisposed;

        public string DatabasePath { get; private set; }
        public bool IsConnected => connection != null && connection.State == System.Data.ConnectionState.Open;

        /// <summary>
        /// Opens or creates a SQLite database at the specified path.
        /// </summary>
        public SQLiteDatabase(string databasePath)
        {
            DatabasePath = databasePath;

            // Ensure directory exists
            string directory = Path.GetDirectoryName(databasePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            string connectionString = $"URI=file:{databasePath}";
            connection = new Mono.Data.Sqlite.SqliteConnection(connectionString);
            connection.Open();
        }

        /// <summary>
        /// Executes a non-query SQL command (INSERT, UPDATE, DELETE, CREATE).
        /// </summary>
        public int ExecuteNonQuery(string sql, Dictionary<string, object> parameters = null)
        {
            using (var command = connection.CreateCommand())
            {
                command.CommandText = sql;
                AddParameters(command, parameters);
                return command.ExecuteNonQuery();
            }
        }

        /// <summary>
        /// Executes a scalar query and returns the first column of the first row.
        /// </summary>
        public object ExecuteScalar(string sql, Dictionary<string, object> parameters = null)
        {
            using (var command = connection.CreateCommand())
            {
                command.CommandText = sql;
                AddParameters(command, parameters);
                return command.ExecuteScalar();
            }
        }

        /// <summary>
        /// Executes a query and returns results as a list of dictionaries.
        /// </summary>
        public List<Dictionary<string, object>> ExecuteQuery(string sql, Dictionary<string, object> parameters = null)
        {
            var results = new List<Dictionary<string, object>>();

            using (var command = connection.CreateCommand())
            {
                command.CommandText = sql;
                AddParameters(command, parameters);

                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var row = new Dictionary<string, object>();
                        for (int i = 0; i < reader.FieldCount; i++)
                        {
                            string columnName = reader.GetName(i);
                            object value = reader.IsDBNull(i) ? null : reader.GetValue(i);
                            row[columnName] = value;
                        }
                        results.Add(row);
                    }
                }
            }

            return results;
        }

        /// <summary>
        /// Executes a query and returns the first result, or null if none.
        /// </summary>
        public Dictionary<string, object> ExecuteQuerySingle(string sql, Dictionary<string, object> parameters = null)
        {
            var results = ExecuteQuery(sql, parameters);
            return results.Count > 0 ? results[0] : null;
        }

        /// <summary>
        /// Checks if a table exists in the database.
        /// </summary>
        public bool TableExists(string tableName)
        {
            var result = ExecuteScalar(
                "SELECT name FROM sqlite_master WHERE type='table' AND name=@name",
                new Dictionary<string, object> { { "@name", tableName } }
            );
            return result != null;
        }

        /// <summary>
        /// Begins a transaction for batch operations.
        /// </summary>
        public SQLiteTransaction BeginTransaction()
        {
            return new SQLiteTransaction(connection.BeginTransaction());
        }

        /// <summary>
        /// Gets the last inserted row ID.
        /// </summary>
        public long GetLastInsertRowId()
        {
            return (long)ExecuteScalar("SELECT last_insert_rowid()");
        }

        private void AddParameters(Mono.Data.Sqlite.SqliteCommand command, Dictionary<string, object> parameters)
        {
            if (parameters == null) return;

            foreach (var param in parameters)
            {
                var sqlParam = command.CreateParameter();
                sqlParam.ParameterName = param.Key;
                sqlParam.Value = param.Value ?? DBNull.Value;
                command.Parameters.Add(sqlParam);
            }
        }

        public void Dispose()
        {
            if (isDisposed) return;

            if (connection != null)
            {
                connection.Close();
                connection.Dispose();
                connection = null;
            }

            isDisposed = true;
        }

        ~SQLiteDatabase()
        {
            Dispose();
        }
    }

    /// <summary>
    /// Wrapper for SQLite transactions.
    /// </summary>
    public class SQLiteTransaction : IDisposable
    {
        private Mono.Data.Sqlite.SqliteTransaction transaction;
        private bool isCompleted;

        internal SQLiteTransaction(Mono.Data.Sqlite.SqliteTransaction transaction)
        {
            this.transaction = transaction;
        }

        public void Commit()
        {
            if (!isCompleted)
            {
                transaction.Commit();
                isCompleted = true;
            }
        }

        public void Rollback()
        {
            if (!isCompleted)
            {
                transaction.Rollback();
                isCompleted = true;
            }
        }

        public void Dispose()
        {
            if (!isCompleted)
            {
                Rollback();
            }
            transaction?.Dispose();
        }
    }

    /// <summary>
    /// Database migration system for schema updates.
    /// </summary>
    public class DatabaseMigrator
    {
        private readonly SQLiteDatabase db;
        private readonly List<Migration> migrations = new List<Migration>();

        public DatabaseMigrator(SQLiteDatabase database)
        {
            db = database;
            EnsureMigrationTable();
        }

        private void EnsureMigrationTable()
        {
            db.ExecuteNonQuery(@"
                CREATE TABLE IF NOT EXISTS __migrations (
                    version INTEGER PRIMARY KEY,
                    applied_at TEXT NOT NULL
                )
            ");
        }

        public void AddMigration(int version, string description, Action<SQLiteDatabase> migrate)
        {
            migrations.Add(new Migration { Version = version, Description = description, Migrate = migrate });
        }

        public void RunMigrations()
        {
            int currentVersion = GetCurrentVersion();
            migrations.Sort((a, b) => a.Version.CompareTo(b.Version));

            foreach (var migration in migrations)
            {
                if (migration.Version > currentVersion)
                {
                    Debug.Log($"Running migration {migration.Version}: {migration.Description}");

                    using (var transaction = db.BeginTransaction())
                    {
                        try
                        {
                            migration.Migrate(db);

                            db.ExecuteNonQuery(
                                "INSERT INTO __migrations (version, applied_at) VALUES (@version, @applied_at)",
                                new Dictionary<string, object>
                                {
                                    { "@version", migration.Version },
                                    { "@applied_at", DateTime.UtcNow.ToString("o") }
                                }
                            );

                            transaction.Commit();
                            Debug.Log($"Migration {migration.Version} completed");
                        }
                        catch (Exception e)
                        {
                            Debug.LogError($"Migration {migration.Version} failed: {e.Message}");
                            transaction.Rollback();
                            throw;
                        }
                    }
                }
            }
        }

        private int GetCurrentVersion()
        {
            var result = db.ExecuteScalar("SELECT MAX(version) FROM __migrations");
            if (result == null || result == DBNull.Value)
            {
                return 0;
            }
            return Convert.ToInt32(result);
        }

        private class Migration
        {
            public int Version;
            public string Description;
            public Action<SQLiteDatabase> Migrate;
        }
    }
}
