using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using MechanicScope.Core;

namespace MechanicScope.Data
{
    /// <summary>
    /// SQLite-based repository for part data.
    /// Provides efficient storage and querying of parts with full-text search.
    /// </summary>
    public class PartRepository : IDisposable
    {
        private SQLiteDatabase db;
        private bool isDisposed;

        public bool IsInitialized => db != null;

        /// <summary>
        /// Initializes the part repository with the database at the given path.
        /// </summary>
        public void Initialize(string databasePath)
        {
            db = new SQLiteDatabase(databasePath);
            RunMigrations();
        }

        private void RunMigrations()
        {
            var migrator = new DatabaseMigrator(db);

            // Migration 1: Initial schema
            migrator.AddMigration(1, "Create parts tables", database =>
            {
                database.ExecuteNonQuery(@"
                    CREATE TABLE IF NOT EXISTS parts (
                        id TEXT PRIMARY KEY,
                        name TEXT NOT NULL,
                        description TEXT,
                        category TEXT,
                        image_path TEXT,
                        created_at TEXT DEFAULT CURRENT_TIMESTAMP,
                        updated_at TEXT DEFAULT CURRENT_TIMESTAMP
                    )
                ");

                database.ExecuteNonQuery(@"
                    CREATE TABLE IF NOT EXISTS part_specs (
                        id INTEGER PRIMARY KEY AUTOINCREMENT,
                        part_id TEXT NOT NULL,
                        spec_key TEXT NOT NULL,
                        spec_value TEXT,
                        FOREIGN KEY (part_id) REFERENCES parts(id) ON DELETE CASCADE,
                        UNIQUE(part_id, spec_key)
                    )
                ");

                database.ExecuteNonQuery(@"
                    CREATE TABLE IF NOT EXISTS part_cross_refs (
                        id INTEGER PRIMARY KEY AUTOINCREMENT,
                        part_id TEXT NOT NULL,
                        ref_type TEXT,
                        ref_value TEXT NOT NULL,
                        FOREIGN KEY (part_id) REFERENCES parts(id) ON DELETE CASCADE
                    )
                ");

                database.ExecuteNonQuery(@"
                    CREATE TABLE IF NOT EXISTS engine_parts (
                        engine_id TEXT NOT NULL,
                        part_id TEXT NOT NULL,
                        model_node_name TEXT,
                        PRIMARY KEY (engine_id, part_id),
                        FOREIGN KEY (part_id) REFERENCES parts(id) ON DELETE CASCADE
                    )
                ");

                // Create indexes for common queries
                database.ExecuteNonQuery("CREATE INDEX IF NOT EXISTS idx_parts_category ON parts(category)");
                database.ExecuteNonQuery("CREATE INDEX IF NOT EXISTS idx_parts_name ON parts(name)");
                database.ExecuteNonQuery("CREATE INDEX IF NOT EXISTS idx_engine_parts_engine ON engine_parts(engine_id)");
            });

            // Migration 2: Add full-text search
            migrator.AddMigration(2, "Add full-text search", database =>
            {
                database.ExecuteNonQuery(@"
                    CREATE VIRTUAL TABLE IF NOT EXISTS parts_fts USING fts5(
                        id,
                        name,
                        description,
                        category,
                        content='parts',
                        content_rowid='rowid'
                    )
                ");

                // Populate FTS table from existing data
                database.ExecuteNonQuery(@"
                    INSERT INTO parts_fts(id, name, description, category)
                    SELECT id, name, description, category FROM parts
                ");

                // Create triggers to keep FTS in sync
                database.ExecuteNonQuery(@"
                    CREATE TRIGGER IF NOT EXISTS parts_ai AFTER INSERT ON parts BEGIN
                        INSERT INTO parts_fts(id, name, description, category)
                        VALUES (new.id, new.name, new.description, new.category);
                    END
                ");

                database.ExecuteNonQuery(@"
                    CREATE TRIGGER IF NOT EXISTS parts_ad AFTER DELETE ON parts BEGIN
                        DELETE FROM parts_fts WHERE id = old.id;
                    END
                ");

                database.ExecuteNonQuery(@"
                    CREATE TRIGGER IF NOT EXISTS parts_au AFTER UPDATE ON parts BEGIN
                        DELETE FROM parts_fts WHERE id = old.id;
                        INSERT INTO parts_fts(id, name, description, category)
                        VALUES (new.id, new.name, new.description, new.category);
                    END
                ");
            });

            migrator.RunMigrations();
        }

        /// <summary>
        /// Gets a part by ID.
        /// </summary>
        public PartInfo GetPart(string partId)
        {
            var row = db.ExecuteQuerySingle(
                "SELECT * FROM parts WHERE id = @id",
                new Dictionary<string, object> { { "@id", partId } }
            );

            if (row == null) return null;

            return RowToPartInfo(row, loadRelations: true);
        }

        /// <summary>
        /// Gets all parts.
        /// </summary>
        public List<PartInfo> GetAllParts()
        {
            var rows = db.ExecuteQuery("SELECT * FROM parts ORDER BY name");
            return rows.Select(r => RowToPartInfo(r, loadRelations: false)).ToList();
        }

        /// <summary>
        /// Searches parts using full-text search.
        /// </summary>
        public List<PartInfo> SearchParts(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                return new List<PartInfo>();
            }

            // Use FTS5 search
            var rows = db.ExecuteQuery(
                @"SELECT p.* FROM parts p
                  INNER JOIN parts_fts fts ON p.id = fts.id
                  WHERE parts_fts MATCH @query
                  ORDER BY rank",
                new Dictionary<string, object> { { "@query", $"{query}*" } }
            );

            return rows.Select(r => RowToPartInfo(r, loadRelations: false)).ToList();
        }

        /// <summary>
        /// Gets all parts for an engine.
        /// </summary>
        public List<PartInfo> GetPartsForEngine(string engineId)
        {
            var rows = db.ExecuteQuery(
                @"SELECT p.*, ep.model_node_name
                  FROM parts p
                  INNER JOIN engine_parts ep ON p.id = ep.part_id
                  WHERE ep.engine_id = @engineId
                  ORDER BY p.name",
                new Dictionary<string, object> { { "@engineId", engineId } }
            );

            return rows.Select(r => RowToPartInfo(r, loadRelations: false)).ToList();
        }

        /// <summary>
        /// Gets all parts in a category.
        /// </summary>
        public List<PartInfo> GetPartsByCategory(string category)
        {
            var rows = db.ExecuteQuery(
                "SELECT * FROM parts WHERE category = @category ORDER BY name",
                new Dictionary<string, object> { { "@category", category } }
            );

            return rows.Select(r => RowToPartInfo(r, loadRelations: false)).ToList();
        }

        /// <summary>
        /// Gets all unique categories.
        /// </summary>
        public List<string> GetCategories()
        {
            var rows = db.ExecuteQuery(
                "SELECT DISTINCT category FROM parts WHERE category IS NOT NULL ORDER BY category"
            );

            return rows.Select(r => r["category"]?.ToString()).Where(c => c != null).ToList();
        }

        /// <summary>
        /// Inserts or updates a part.
        /// </summary>
        public void SavePart(PartInfo part)
        {
            using (var transaction = db.BeginTransaction())
            {
                try
                {
                    // Upsert main part record
                    db.ExecuteNonQuery(
                        @"INSERT INTO parts (id, name, description, category, image_path, updated_at)
                          VALUES (@id, @name, @description, @category, @imagePath, CURRENT_TIMESTAMP)
                          ON CONFLICT(id) DO UPDATE SET
                              name = @name,
                              description = @description,
                              category = @category,
                              image_path = @imagePath,
                              updated_at = CURRENT_TIMESTAMP",
                        new Dictionary<string, object>
                        {
                            { "@id", part.Id },
                            { "@name", part.Name },
                            { "@description", part.Description },
                            { "@category", part.Category },
                            { "@imagePath", part.ImagePath }
                        }
                    );

                    // Update specs
                    db.ExecuteNonQuery(
                        "DELETE FROM part_specs WHERE part_id = @partId",
                        new Dictionary<string, object> { { "@partId", part.Id } }
                    );

                    if (part.Specs != null)
                    {
                        foreach (var spec in part.Specs)
                        {
                            db.ExecuteNonQuery(
                                @"INSERT INTO part_specs (part_id, spec_key, spec_value)
                                  VALUES (@partId, @key, @value)",
                                new Dictionary<string, object>
                                {
                                    { "@partId", part.Id },
                                    { "@key", spec.Key },
                                    { "@value", spec.Value }
                                }
                            );
                        }
                    }

                    // Update cross references
                    db.ExecuteNonQuery(
                        "DELETE FROM part_cross_refs WHERE part_id = @partId",
                        new Dictionary<string, object> { { "@partId", part.Id } }
                    );

                    if (part.CrossReferences != null)
                    {
                        foreach (var crossRef in part.CrossReferences)
                        {
                            db.ExecuteNonQuery(
                                @"INSERT INTO part_cross_refs (part_id, ref_value)
                                  VALUES (@partId, @value)",
                                new Dictionary<string, object>
                                {
                                    { "@partId", part.Id },
                                    { "@value", crossRef }
                                }
                            );
                        }
                    }

                    transaction.Commit();
                }
                catch
                {
                    transaction.Rollback();
                    throw;
                }
            }
        }

        /// <summary>
        /// Associates a part with an engine.
        /// </summary>
        public void AssociatePartWithEngine(string partId, string engineId, string modelNodeName = null)
        {
            db.ExecuteNonQuery(
                @"INSERT INTO engine_parts (engine_id, part_id, model_node_name)
                  VALUES (@engineId, @partId, @nodeName)
                  ON CONFLICT(engine_id, part_id) DO UPDATE SET
                      model_node_name = @nodeName",
                new Dictionary<string, object>
                {
                    { "@engineId", engineId },
                    { "@partId", partId },
                    { "@nodeName", modelNodeName }
                }
            );
        }

        /// <summary>
        /// Removes association between a part and an engine.
        /// </summary>
        public void RemovePartFromEngine(string partId, string engineId)
        {
            db.ExecuteNonQuery(
                "DELETE FROM engine_parts WHERE part_id = @partId AND engine_id = @engineId",
                new Dictionary<string, object>
                {
                    { "@partId", partId },
                    { "@engineId", engineId }
                }
            );
        }

        /// <summary>
        /// Deletes a part and all its relations.
        /// </summary>
        public void DeletePart(string partId)
        {
            db.ExecuteNonQuery(
                "DELETE FROM parts WHERE id = @id",
                new Dictionary<string, object> { { "@id", partId } }
            );
        }

        /// <summary>
        /// Gets the total number of parts.
        /// </summary>
        public int GetPartCount()
        {
            var result = db.ExecuteScalar("SELECT COUNT(*) FROM parts");
            return Convert.ToInt32(result);
        }

        /// <summary>
        /// Imports parts from JSON data (for migration from Phase 1).
        /// </summary>
        public void ImportFromJson(string json)
        {
            try
            {
                var dataFile = JsonUtility.FromJson<PartsDataFile>(json);
                if (dataFile?.parts == null) return;

                using (var transaction = db.BeginTransaction())
                {
                    try
                    {
                        foreach (var partData in dataFile.parts)
                        {
                            var part = new PartInfo
                            {
                                Id = partData.id,
                                Name = partData.name,
                                Description = partData.description,
                                Category = partData.category,
                                ImagePath = partData.imagePath,
                                Specs = new Dictionary<string, string>(),
                                CrossReferences = partData.crossReferences?.ToList() ?? new List<string>()
                            };

                            if (partData.specs != null)
                            {
                                foreach (var spec in partData.specs)
                                {
                                    part.Specs[spec.key] = spec.value;
                                }
                            }

                            SavePart(part);

                            // Associate with engines
                            if (partData.engines != null)
                            {
                                foreach (var engineId in partData.engines)
                                {
                                    AssociatePartWithEngine(part.Id, engineId);
                                }
                            }
                        }

                        transaction.Commit();
                        Debug.Log($"Imported {dataFile.parts.Length} parts from JSON");
                    }
                    catch
                    {
                        transaction.Rollback();
                        throw;
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to import parts from JSON: {e.Message}");
            }
        }

        private PartInfo RowToPartInfo(Dictionary<string, object> row, bool loadRelations)
        {
            var part = new PartInfo
            {
                Id = row["id"]?.ToString(),
                Name = row["name"]?.ToString(),
                Description = row["description"]?.ToString(),
                Category = row["category"]?.ToString(),
                ImagePath = row["image_path"]?.ToString(),
                Specs = new Dictionary<string, string>(),
                CrossReferences = new List<string>()
            };

            if (loadRelations)
            {
                // Load specs
                var specs = db.ExecuteQuery(
                    "SELECT spec_key, spec_value FROM part_specs WHERE part_id = @partId",
                    new Dictionary<string, object> { { "@partId", part.Id } }
                );

                foreach (var spec in specs)
                {
                    string key = spec["spec_key"]?.ToString();
                    string value = spec["spec_value"]?.ToString();
                    if (key != null)
                    {
                        part.Specs[key] = value;
                    }
                }

                // Load cross references
                var crossRefs = db.ExecuteQuery(
                    "SELECT ref_value FROM part_cross_refs WHERE part_id = @partId",
                    new Dictionary<string, object> { { "@partId", part.Id } }
                );

                foreach (var crossRef in crossRefs)
                {
                    string value = crossRef["ref_value"]?.ToString();
                    if (value != null)
                    {
                        part.CrossReferences.Add(value);
                    }
                }
            }

            return part;
        }

        public void Dispose()
        {
            if (isDisposed) return;

            db?.Dispose();
            db = null;
            isDisposed = true;
        }
    }
}
