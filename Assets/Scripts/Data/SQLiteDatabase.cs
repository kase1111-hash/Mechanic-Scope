using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.IO;
using UnityEngine;

namespace MechanicScope.Data
{
    /// <summary>
    /// Lightweight SQLite wrapper.
    ///
    /// The wrapper is written against the ADO.NET base types in System.Data.Common, which are part
    /// of .NET Standard, so this file always compiles. Only the creation of the concrete provider
    /// connection is conditional — see <see cref="CreateConnection"/>.
    ///
    /// This layer is OPT-IN and disabled by default. The app's shipping data path is the JSON layer
    /// (PartDatabase + ProgressTracker); this is the Phase 2 store kept for a future migration.
    /// To enable it, see Docs/SQLITE_SETUP.md.
    /// </summary>
    public class SQLiteDatabase : IDisposable
    {
        private DbConnection connection;
        private bool isDisposed;

        public string DatabasePath { get; private set; }
        public bool IsConnected => connection != null && connection.State == ConnectionState.Open;

        /// <summary>
        /// True when this build actually has a SQLite provider compiled in. Callers that can fall
        /// back to another store should check this instead of catching the constructor's exception.
        /// </summary>
        public static bool IsSupported =>
#if MECHANICSCOPE_SQLITE
            true;
#else
            false;
#endif

        /// <summary>
        /// Opens or creates a SQLite database at the specified path.
        /// Throws <see cref="NotSupportedException"/> when no provider is compiled in.
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
            connection = CreateConnection(connectionString);
            connection.Open();
        }

        /// <summary>
        /// The single point where a concrete ADO.NET provider is bound.
        ///
        /// Mono.Data.Sqlite only exists under the legacy .NET Framework API compatibility level.
        /// This project targets .NET Standard (ProjectSettings: apiCompatibilityLevel 6) with IL2CPP
        /// on iOS/Android, where that type is not available — referencing it unconditionally fails
        /// to compile and takes the entire MechanicScope assembly down with it.
        ///
        /// So the reference lives behind MECHANICSCOPE_SQLITE. Define that symbol only after adding
        /// a provider that works on your target platforms (Docs/SQLITE_SETUP.md).
        /// </summary>
        private static DbConnection CreateConnection(string connectionString)
        {
#if MECHANICSCOPE_SQLITE
            return new Mono.Data.Sqlite.SqliteConnection(connectionString);
#else
            throw new NotSupportedException(
                "No SQLite provider is compiled into this build, so SQLiteDatabase cannot open a " +
                "connection. The app's active data layer is the JSON store (PartDatabase / " +
                "ProgressTracker); this Phase 2 layer is opt-in. See Docs/SQLITE_SETUP.md to enable it.");
#endif
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

        private void AddParameters(DbCommand command, Dictionary<string, object> parameters)
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

        // No finalizer: this type holds only a managed DbConnection, which has its own finalizer.
        // Touching it from ours would risk using an already-finalized object.
    }

    /// <summary>
    /// Wrapper for SQLite transactions.
    /// </summary>
    public class SQLiteTransaction : IDisposable
    {
        private DbTransaction transaction;
        private bool isCompleted;

        internal SQLiteTransaction(DbTransaction transaction)
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
