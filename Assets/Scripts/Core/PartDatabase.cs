using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace MechanicScope.Core
{
    /// <summary>
    /// Stores and retrieves part metadata, specifications, and cross-references.
    /// Phase 1 uses JSON-based storage; Phase 2 will migrate to SQLite.
    /// </summary>
    public class PartDatabase : MonoBehaviour
    {
        [Header("Data Sources")]
        [SerializeField] private TextAsset defaultPartsData;

        // Events
        public event Action OnDatabaseLoaded;
        public event Action<string> OnLoadError;

        // Properties
        public bool IsLoaded { get; private set; }
        public int PartCount => parts.Count;

        private Dictionary<string, PartInfo> parts = new Dictionary<string, PartInfo>();
        private Dictionary<string, List<string>> engineParts = new Dictionary<string, List<string>>();
        private Dictionary<string, List<string>> categoryIndex = new Dictionary<string, List<string>>();

        private void Awake()
        {
            LoadDefaultData();
        }

        private void LoadDefaultData()
        {
            // Load bundled default parts data
            if (defaultPartsData != null)
            {
                ImportPartData(defaultPartsData.text);
            }

            // Load user-imported parts
            string userPartsPath = Path.Combine(Application.persistentDataPath, "parts.json");
            if (File.Exists(userPartsPath))
            {
                try
                {
                    string json = File.ReadAllText(userPartsPath);
                    ImportPartData(json);
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"Failed to load user parts data: {e.Message}");
                }
            }

            IsLoaded = true;
            OnDatabaseLoaded?.Invoke();
        }

        /// <summary>
        /// Gets a part by its ID.
        /// </summary>
        public PartInfo GetPart(string partId)
        {
            if (string.IsNullOrEmpty(partId)) return null;
            parts.TryGetValue(partId, out PartInfo part);
            return part;
        }

        /// <summary>
        /// Searches parts by name or description.
        /// </summary>
        public List<PartInfo> SearchParts(string query)
        {
            if (string.IsNullOrEmpty(query)) return new List<PartInfo>();

            string lowerQuery = query.ToLower();
            return parts.Values
                .Where(p => p.Name.ToLower().Contains(lowerQuery) ||
                           (p.Description != null && p.Description.ToLower().Contains(lowerQuery)))
                .ToList();
        }

        /// <summary>
        /// Gets all parts associated with an engine.
        /// </summary>
        public List<PartInfo> GetPartsForEngine(string engineId)
        {
            if (!engineParts.ContainsKey(engineId))
            {
                return new List<PartInfo>();
            }

            return engineParts[engineId]
                .Select(partId => GetPart(partId))
                .Where(p => p != null)
                .ToList();
        }

        /// <summary>
        /// Gets all parts in a category.
        /// </summary>
        public List<PartInfo> GetPartsByCategory(string category)
        {
            if (!categoryIndex.ContainsKey(category))
            {
                return new List<PartInfo>();
            }

            return categoryIndex[category]
                .Select(partId => GetPart(partId))
                .Where(p => p != null)
                .ToList();
        }

        /// <summary>
        /// Gets all available categories.
        /// </summary>
        public List<string> GetCategories()
        {
            return categoryIndex.Keys.ToList();
        }

        /// <summary>
        /// Imports part data from JSON.
        /// </summary>
        public void ImportPartData(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                Debug.LogWarning("PartDatabase: Cannot import empty JSON data");
                return;
            }

            try
            {
                PartsDataFile dataFile = JsonUtility.FromJson<PartsDataFile>(json);
                if (dataFile == null)
                {
                    OnLoadError?.Invoke("Failed to import parts data: JSON parsing returned null");
                    return;
                }

                if (dataFile.parts == null)
                {
                    Debug.LogWarning("PartDatabase: Parts array is null in JSON data");
                    return;
                }

                int importedCount = 0;
                int skippedCount = 0;

                foreach (PartData partData in dataFile.parts)
                {
                    if (partData == null)
                    {
                        skippedCount++;
                        continue;
                    }

                    if (string.IsNullOrEmpty(partData.id))
                    {
                        Debug.LogWarning($"PartDatabase: Skipping part with empty ID (name: '{partData.name ?? "unknown"}')");
                        skippedCount++;
                        continue;
                    }

                    PartInfo part = new PartInfo
                    {
                        Id = partData.id,
                        Name = partData.name ?? partData.id,
                        Description = partData.description,
                        Category = partData.category,
                        ImagePath = partData.imagePath,
                        Specs = new Dictionary<string, string>(),
                        CrossReferences = partData.crossReferences?.ToList() ?? new List<string>()
                    };

                    // Parse specs
                    if (partData.specs != null)
                    {
                        foreach (var spec in partData.specs)
                        {
                            if (spec != null && !string.IsNullOrEmpty(spec.key))
                            {
                                part.Specs[spec.key] = spec.value ?? "";
                            }
                        }
                    }

                    parts[part.Id] = part;
                    importedCount++;

                    // Update category index
                    if (!string.IsNullOrEmpty(part.Category))
                    {
                        if (!categoryIndex.ContainsKey(part.Category))
                        {
                            categoryIndex[part.Category] = new List<string>();
                        }
                        if (!categoryIndex[part.Category].Contains(part.Id))
                        {
                            categoryIndex[part.Category].Add(part.Id);
                        }
                    }

                    // Update engine parts mapping
                    if (partData.engines != null)
                    {
                        foreach (string engineId in partData.engines)
                        {
                            if (string.IsNullOrEmpty(engineId)) continue;

                            if (!engineParts.ContainsKey(engineId))
                            {
                                engineParts[engineId] = new List<string>();
                            }
                            if (!engineParts[engineId].Contains(part.Id))
                            {
                                engineParts[engineId].Add(part.Id);
                            }
                        }
                    }
                }

                Debug.Log($"Imported {importedCount} parts into database" + (skippedCount > 0 ? $" ({skippedCount} skipped)" : ""));
            }
            catch (Exception e)
            {
                OnLoadError?.Invoke($"Failed to import parts data: {e.Message}");
            }
        }

        /// <summary>
        /// Imports part data from a file path.
        /// </summary>
        public void ImportPartDataFromFile(string filePath)
        {
            if (!File.Exists(filePath))
            {
                OnLoadError?.Invoke($"Parts file not found: {filePath}");
                return;
            }

            try
            {
                string json = File.ReadAllText(filePath);
                ImportPartData(json);
            }
            catch (Exception e)
            {
                OnLoadError?.Invoke($"Failed to read parts file: {e.Message}");
            }
        }

        /// <summary>
        /// Adds or updates a single part.
        /// </summary>
        public void AddOrUpdatePart(PartInfo part)
        {
            if (part == null || string.IsNullOrEmpty(part.Id)) return;

            parts[part.Id] = part;

            // Update category index
            if (!string.IsNullOrEmpty(part.Category))
            {
                if (!categoryIndex.ContainsKey(part.Category))
                {
                    categoryIndex[part.Category] = new List<string>();
                }
                if (!categoryIndex[part.Category].Contains(part.Id))
                {
                    categoryIndex[part.Category].Add(part.Id);
                }
            }
        }

        /// <summary>
        /// Associates a part with an engine.
        /// </summary>
        public void AssociatePartWithEngine(string partId, string engineId)
        {
            if (!parts.ContainsKey(partId)) return;

            if (!engineParts.ContainsKey(engineId))
            {
                engineParts[engineId] = new List<string>();
            }
            if (!engineParts[engineId].Contains(partId))
            {
                engineParts[engineId].Add(partId);
            }
        }

        /// <summary>
        /// Exports current parts data to JSON.
        /// </summary>
        public string ExportToJson()
        {
            List<PartData> partDataList = new List<PartData>();

            foreach (var part in parts.Values)
            {
                PartData data = new PartData
                {
                    id = part.Id,
                    name = part.Name,
                    description = part.Description,
                    category = part.Category,
                    imagePath = part.ImagePath,
                    crossReferences = part.CrossReferences?.ToArray(),
                    specs = part.Specs?.Select(kvp => new SpecData { key = kvp.Key, value = kvp.Value }).ToArray(),
                    engines = engineParts.Where(kvp => kvp.Value.Contains(part.Id)).Select(kvp => kvp.Key).ToArray()
                };
                partDataList.Add(data);
            }

            PartsDataFile dataFile = new PartsDataFile { parts = partDataList.ToArray() };
            return JsonUtility.ToJson(dataFile, true);
        }

        /// <summary>
        /// Saves parts data to persistent storage.
        /// </summary>
        public void SaveToFile()
        {
            string json = ExportToJson();
            string path = Path.Combine(Application.persistentDataPath, "parts.json");
            File.WriteAllText(path, json);
        }

        /// <summary>
        /// Clears all parts data.
        /// </summary>
        public void ClearDatabase()
        {
            parts.Clear();
            engineParts.Clear();
            categoryIndex.Clear();
        }
    }

    /// <summary>
    /// Part information structure.
    /// </summary>
    public class PartInfo
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string Category { get; set; }
        public Dictionary<string, string> Specs { get; set; }
        public List<string> CrossReferences { get; set; }
        public string ImagePath { get; set; }

        /// <summary>
        /// Gets a formatted string of all specifications.
        /// </summary>
        public string GetFormattedSpecs()
        {
            if (Specs == null || Specs.Count == 0) return "No specifications available";

            return string.Join("\n", Specs.Select(kvp => $"{kvp.Key}: {kvp.Value}"));
        }

        /// <summary>
        /// Gets a specific spec value.
        /// </summary>
        public string GetSpec(string key, string defaultValue = null)
        {
            if (Specs == null || !Specs.ContainsKey(key)) return defaultValue;
            return Specs[key];
        }
    }

    // JSON serialization structures
    [Serializable]
    public class PartsDataFile
    {
        public PartData[] parts;
    }

    [Serializable]
    public class PartData
    {
        public string id;
        public string name;
        public string description;
        public string category;
        public string imagePath;
        public string[] crossReferences;
        public string[] engines;
        public SpecData[] specs;
    }

    [Serializable]
    public class SpecData
    {
        public string key;
        public string value;
    }
}
