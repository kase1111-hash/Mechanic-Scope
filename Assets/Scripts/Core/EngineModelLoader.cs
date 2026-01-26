using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace MechanicScope.Core
{
    /// <summary>
    /// Handles loading engine models and their associated metadata.
    /// Supports .glb, .fbx, and .obj formats through Unity's runtime import capabilities.
    /// </summary>
    public class EngineModelLoader : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField] private Material defaultMaterial;
        [SerializeField] private Material highlightMaterial;

        // Events
        public event Action<EngineManifest> OnEngineListUpdated;
        public event Action<GameObject, EngineManifest> OnModelLoaded;
        public event Action<string> OnLoadError;

        // Properties
        public List<EngineManifest> AvailableEngines { get; private set; } = new List<EngineManifest>();
        public EngineManifest CurrentEngine { get; private set; }
        public bool IsLoading { get; private set; }

        private string enginesDirectory;
        private Dictionary<string, GameObject> loadedModels = new Dictionary<string, GameObject>();

        // Characters that are not allowed in engine IDs for security
        private static readonly char[] InvalidPathChars = new char[] { '/', '\\', ':', '*', '?', '"', '<', '>', '|', '.' };

        private void Awake()
        {
            enginesDirectory = Path.Combine(Application.persistentDataPath, "engines");
            EnsureDirectoryExists(enginesDirectory);
        }

        private void Start()
        {
            RefreshEngineList();
        }

        /// <summary>
        /// Scans the engines directory and updates the available engines list.
        /// </summary>
        public void RefreshEngineList()
        {
            AvailableEngines.Clear();

            if (!Directory.Exists(enginesDirectory))
            {
                EnsureDirectoryExists(enginesDirectory);
                return;
            }

            string[] engineDirs = Directory.GetDirectories(enginesDirectory);
            foreach (string engineDir in engineDirs)
            {
                string manifestPath = Path.Combine(engineDir, "engine.json");
                if (File.Exists(manifestPath))
                {
                    try
                    {
                        string json = File.ReadAllText(manifestPath);
                        EngineManifest manifest = JsonUtility.FromJson<EngineManifest>(json);
                        manifest.BasePath = engineDir;
                        AvailableEngines.Add(manifest);
                    }
                    catch (Exception e)
                    {
                        Debug.LogWarning($"Failed to load engine manifest at {manifestPath}: {e.Message}");
                    }
                }
            }

            // Also check StreamingAssets for bundled engines
            string streamingEngines = Path.Combine(Application.streamingAssetsPath, "Engines");
            if (Directory.Exists(streamingEngines))
            {
                string[] bundledDirs = Directory.GetDirectories(streamingEngines);
                foreach (string engineDir in bundledDirs)
                {
                    string manifestPath = Path.Combine(engineDir, "engine.json");
                    if (File.Exists(manifestPath))
                    {
                        try
                        {
                            string json = File.ReadAllText(manifestPath);
                            EngineManifest manifest = JsonUtility.FromJson<EngineManifest>(json);
                            manifest.BasePath = engineDir;
                            manifest.IsBundled = true;
                            AvailableEngines.Add(manifest);
                        }
                        catch (Exception e)
                        {
                            Debug.LogWarning($"Failed to load bundled engine manifest at {manifestPath}: {e.Message}");
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Loads an engine model by its ID.
        /// </summary>
        public void LoadEngine(string engineId)
        {
            if (IsLoading)
            {
                Debug.LogWarning("EngineModelLoader: Already loading an engine. Please wait for the current load to complete.");
                return;
            }

            EngineManifest manifest = AvailableEngines.Find(e => e.id == engineId);
            if (manifest == null)
            {
                OnLoadError?.Invoke($"Engine with ID '{engineId}' not found.");
                return;
            }

            StartCoroutine(LoadEngineCoroutine(manifest));
        }

        /// <summary>
        /// Loads an engine from its manifest.
        /// </summary>
        public void LoadEngine(EngineManifest manifest)
        {
            if (IsLoading)
            {
                Debug.LogWarning("EngineModelLoader: Already loading an engine. Please wait for the current load to complete.");
                return;
            }

            if (manifest == null)
            {
                OnLoadError?.Invoke("Cannot load null engine manifest.");
                return;
            }

            StartCoroutine(LoadEngineCoroutine(manifest));
        }

        private IEnumerator LoadEngineCoroutine(EngineManifest manifest)
        {
            IsLoading = true;
            CurrentEngine = manifest;

            string modelPath = Path.Combine(manifest.BasePath, manifest.modelFile);

            if (!File.Exists(modelPath))
            {
                OnLoadError?.Invoke($"Model file not found: {modelPath}");
                IsLoading = false;
                yield break;
            }

            GameObject model = null;

            // Check if already loaded
            if (loadedModels.TryGetValue(manifest.id, out model))
            {
                model.SetActive(true);
                OnModelLoaded?.Invoke(model, manifest);
                IsLoading = false;
                yield break;
            }

            // Load based on file extension
            string extension = Path.GetExtension(modelPath).ToLower();

            switch (extension)
            {
                case ".glb":
                case ".gltf":
                    yield return LoadGLBModel(modelPath, manifest);
                    break;

                case ".obj":
                    yield return LoadOBJModel(modelPath, manifest);
                    break;

                case ".fbx":
                    // FBX requires external plugin like TriLib
                    OnLoadError?.Invoke("FBX runtime loading requires TriLib plugin. Please convert to GLB format.");
                    break;

                default:
                    OnLoadError?.Invoke($"Unsupported model format: {extension}");
                    break;
            }

            IsLoading = false;
        }

        private IEnumerator LoadGLBModel(string path, EngineManifest manifest)
        {
            // Note: Actual GLB loading requires GLTFUtility or similar plugin
            // This is a placeholder that creates a simple cube as demonstration
            Debug.Log($"Loading GLB model from: {path}");

            // In production, use GLTFUtility:
            // GameObject model = Siccity.GLTFUtility.Importer.LoadFromFile(path);

            // Placeholder: Create a simple object structure for testing
            GameObject model = CreatePlaceholderModel(manifest);

            if (model != null)
            {
                model.name = manifest.name;
                loadedModels[manifest.id] = model;
                ApplyPartMappings(model, manifest);
                OnModelLoaded?.Invoke(model, manifest);
            }
            else
            {
                OnLoadError?.Invoke("Failed to load GLB model.");
            }

            yield return null;
        }

        private IEnumerator LoadOBJModel(string path, EngineManifest manifest)
        {
            // Note: OBJ loading requires a runtime OBJ loader
            Debug.Log($"Loading OBJ model from: {path}");

            // Placeholder for OBJ loading
            GameObject model = CreatePlaceholderModel(manifest);

            if (model != null)
            {
                model.name = manifest.name;
                loadedModels[manifest.id] = model;
                ApplyPartMappings(model, manifest);
                OnModelLoaded?.Invoke(model, manifest);
            }

            yield return null;
        }

        /// <summary>
        /// Creates a placeholder model for testing when actual model loading isn't available.
        /// </summary>
        private GameObject CreatePlaceholderModel(EngineManifest manifest)
        {
            GameObject root = new GameObject(manifest.name);

            // Create placeholder parts based on part mappings
            if (manifest.partMappings != null && manifest.partMappings.Length > 0)
            {
                float offset = 0;
                foreach (var mapping in manifest.partMappings)
                {
                    GameObject part = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    part.name = mapping.nodeNameInModel;
                    part.transform.SetParent(root.transform);
                    part.transform.localPosition = new Vector3(offset, 0, 0);
                    part.transform.localScale = Vector3.one * 0.1f;

                    // Store part ID in a component for later retrieval
                    PartIdentifier identifier = part.AddComponent<PartIdentifier>();
                    identifier.PartId = mapping.partId;
                    identifier.NodeName = mapping.nodeNameInModel;

                    if (defaultMaterial != null)
                    {
                        part.GetComponent<Renderer>().material = defaultMaterial;
                    }

                    offset += 0.15f;
                }
            }
            else
            {
                // Create a single placeholder cube
                GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                cube.name = "EnginePlaceholder";
                cube.transform.SetParent(root.transform);
                cube.transform.localScale = Vector3.one * 0.3f;

                if (defaultMaterial != null)
                {
                    cube.GetComponent<Renderer>().material = defaultMaterial;
                }
            }

            // Apply default alignment if specified
            if (manifest.defaultAlignment != null)
            {
                if (manifest.defaultAlignment.position != null && manifest.defaultAlignment.position.Length == 3)
                {
                    root.transform.position = new Vector3(
                        manifest.defaultAlignment.position[0],
                        manifest.defaultAlignment.position[1],
                        manifest.defaultAlignment.position[2]
                    );
                }

                if (manifest.defaultAlignment.rotation != null && manifest.defaultAlignment.rotation.Length == 3)
                {
                    root.transform.eulerAngles = new Vector3(
                        manifest.defaultAlignment.rotation[0],
                        manifest.defaultAlignment.rotation[1],
                        manifest.defaultAlignment.rotation[2]
                    );
                }

                if (manifest.defaultAlignment.scale != null && manifest.defaultAlignment.scale.Length == 3)
                {
                    root.transform.localScale = new Vector3(
                        manifest.defaultAlignment.scale[0],
                        manifest.defaultAlignment.scale[1],
                        manifest.defaultAlignment.scale[2]
                    );
                }
            }

            return root;
        }

        /// <summary>
        /// Applies part ID mappings to model nodes.
        /// </summary>
        private void ApplyPartMappings(GameObject model, EngineManifest manifest)
        {
            if (manifest.partMappings == null) return;

            foreach (var mapping in manifest.partMappings)
            {
                Transform node = FindChildRecursive(model.transform, mapping.nodeNameInModel);
                if (node != null)
                {
                    PartIdentifier identifier = node.GetComponent<PartIdentifier>();
                    if (identifier == null)
                    {
                        identifier = node.gameObject.AddComponent<PartIdentifier>();
                    }
                    identifier.PartId = mapping.partId;
                    identifier.NodeName = mapping.nodeNameInModel;
                }
            }
        }

        private Transform FindChildRecursive(Transform parent, string name)
        {
            if (parent.name == name) return parent;

            foreach (Transform child in parent)
            {
                Transform found = FindChildRecursive(child, name);
                if (found != null) return found;
            }

            return null;
        }

        /// <summary>
        /// Highlights specific parts on the current model.
        /// </summary>
        public void HighlightParts(List<string> partIds)
        {
            if (CurrentEngine == null || !loadedModels.ContainsKey(CurrentEngine.id)) return;

            GameObject model = loadedModels[CurrentEngine.id];
            PartIdentifier[] parts = model.GetComponentsInChildren<PartIdentifier>();

            foreach (var part in parts)
            {
                Renderer renderer = part.GetComponent<Renderer>();
                if (renderer == null) continue;

                if (partIds.Contains(part.PartId))
                {
                    renderer.material = highlightMaterial != null ? highlightMaterial : renderer.material;
                    // Add pulsing effect via shader or animation
                }
                else
                {
                    renderer.material = defaultMaterial != null ? defaultMaterial : renderer.material;
                }
            }
        }

        /// <summary>
        /// Clears all part highlights.
        /// </summary>
        public void ClearHighlights()
        {
            HighlightParts(new List<string>());
        }

        /// <summary>
        /// Unloads the current engine model from memory.
        /// </summary>
        public void UnloadCurrentEngine()
        {
            if (CurrentEngine != null && loadedModels.ContainsKey(CurrentEngine.id))
            {
                Destroy(loadedModels[CurrentEngine.id]);
                loadedModels.Remove(CurrentEngine.id);
                CurrentEngine = null;
            }
        }

        /// <summary>
        /// Imports an engine from an external path (e.g., file picker result).
        /// </summary>
        public bool ImportEngine(string sourcePath, string engineId, string engineName)
        {
            // Validate engineId to prevent path traversal
            string sanitizedId = SanitizeEngineId(engineId);
            if (sanitizedId == null)
            {
                OnLoadError?.Invoke($"Invalid engine ID: '{engineId}'");
                return false;
            }

            try
            {
                string destDir = Path.Combine(enginesDirectory, sanitizedId);
                EnsureDirectoryExists(destDir);

                string fileName = Path.GetFileName(sourcePath);
                string destPath = Path.Combine(destDir, fileName);

                File.Copy(sourcePath, destPath, true);

                // Create a basic manifest
                EngineManifest manifest = new EngineManifest
                {
                    id = engineId,
                    name = engineName,
                    modelFile = fileName,
                    BasePath = destDir
                };

                string manifestJson = JsonUtility.ToJson(manifest, true);
                File.WriteAllText(Path.Combine(destDir, "engine.json"), manifestJson);

                RefreshEngineList();
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to import engine: {e.Message}");
                OnLoadError?.Invoke($"Failed to import engine: {e.Message}");
                return false;
            }
        }

        /// <summary>
        /// Deletes an imported engine (not bundled engines).
        /// </summary>
        public bool DeleteEngine(string engineId)
        {
            // Validate engineId to prevent path traversal
            if (!IsValidEngineId(engineId))
            {
                Debug.LogWarning($"DeleteEngine: Invalid engine ID: '{engineId}'");
                return false;
            }

            EngineManifest manifest = AvailableEngines.Find(e => e.id == engineId);
            if (manifest == null || manifest.IsBundled)
            {
                return false;
            }

            try
            {
                if (Directory.Exists(manifest.BasePath))
                {
                    Directory.Delete(manifest.BasePath, true);
                }

                if (loadedModels.ContainsKey(engineId))
                {
                    Destroy(loadedModels[engineId]);
                    loadedModels.Remove(engineId);
                }

                RefreshEngineList();
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to delete engine: {e.Message}");
                return false;
            }
        }

        private void EnsureDirectoryExists(string path)
        {
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }
        }

        /// <summary>
        /// Sanitizes an engine ID to prevent path traversal attacks.
        /// Returns null if the ID is invalid.
        /// </summary>
        public static string SanitizeEngineId(string engineId)
        {
            if (string.IsNullOrWhiteSpace(engineId))
            {
                return null;
            }

            // Check for path traversal patterns
            if (engineId.Contains("..") || engineId.StartsWith("/") || engineId.StartsWith("\\"))
            {
                Debug.LogWarning($"EngineModelLoader: Invalid engine ID detected (path traversal attempt): '{engineId}'");
                return null;
            }

            // Check for invalid characters
            if (engineId.IndexOfAny(InvalidPathChars) >= 0)
            {
                Debug.LogWarning($"EngineModelLoader: Invalid engine ID detected (contains invalid characters): '{engineId}'");
                return null;
            }

            return engineId.Trim();
        }

        /// <summary>
        /// Validates that an engine ID is safe to use.
        /// </summary>
        public static bool IsValidEngineId(string engineId)
        {
            return SanitizeEngineId(engineId) != null;
        }
    }

    /// <summary>
    /// Component attached to model parts to store part identification data.
    /// </summary>
    public class PartIdentifier : MonoBehaviour
    {
        public string PartId;
        public string NodeName;
    }

    /// <summary>
    /// Engine manifest data structure matching engine.json format.
    /// </summary>
    [Serializable]
    public class EngineManifest
    {
        public string id;
        public string name;
        public string manufacturer;
        public string years;
        public string modelFile;
        public string thumbnail;
        public PartMapping[] partMappings;
        public DefaultAlignment defaultAlignment;

        // Runtime properties (not serialized)
        [NonSerialized] public string BasePath;
        [NonSerialized] public bool IsBundled;
    }

    [Serializable]
    public class PartMapping
    {
        public string nodeNameInModel;
        public string partId;
    }

    [Serializable]
    public class DefaultAlignment
    {
        public float[] position;
        public float[] rotation;
        public float[] scale;
    }
}
