using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;
using GLTFast;

namespace MechanicScope.Core
{
    /// <summary>
    /// Handles loading engine models and their associated metadata.
    /// Uses glTFast for runtime GLB/glTF import.
    /// </summary>
    public class EngineModelLoader : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField] private Material defaultMaterial;
        [SerializeField] private Material highlightMaterial;
        [SerializeField] private HighlightController highlightController;

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

                case ".fbx":
                case ".obj":
                    OnLoadError?.Invoke($"Format '{extension}' is not supported for runtime loading. Please convert to GLB format using Blender or another tool.");
                    break;

                default:
                    OnLoadError?.Invoke($"Unsupported model format: {extension}");
                    break;
            }

            IsLoading = false;
        }

        private IEnumerator LoadGLBModel(string path, EngineManifest manifest)
        {
            Debug.Log($"[EngineModelLoader] Loading GLB/glTF model from: {path}");

            var gltf = new GltfImport();
            bool loadComplete = false;
            bool loadSuccess = false;

            // Run async glTFast load and wait for it in the coroutine
            var loadTask = gltf.Load($"file://{path}");
            loadTask.ContinueWith(task =>
            {
                loadSuccess = task.Result;
                loadComplete = true;
            }, TaskScheduler.FromCurrentSynchronizationContext());

            while (!loadComplete)
            {
                yield return null;
            }

            if (!loadSuccess)
            {
                OnLoadError?.Invoke($"glTFast failed to load model: {path}");
                gltf.Dispose();
                yield break;
            }

            // Instantiate the loaded model into the scene
            GameObject modelRoot = new GameObject(manifest.name);
            bool instantiateSuccess = false;
            bool instantiateComplete = false;

            var instantiateTask = gltf.InstantiateMainSceneAsync(modelRoot.transform);
            instantiateTask.ContinueWith(task =>
            {
                instantiateSuccess = task.Result;
                instantiateComplete = true;
            }, TaskScheduler.FromCurrentSynchronizationContext());

            while (!instantiateComplete)
            {
                yield return null;
            }

            if (!instantiateSuccess)
            {
                OnLoadError?.Invoke("glTFast failed to instantiate model scene.");
                Destroy(modelRoot);
                gltf.Dispose();
                yield break;
            }

            // Generate MeshColliders on all rendered parts for raycasting
            foreach (var meshFilter in modelRoot.GetComponentsInChildren<MeshFilter>())
            {
                if (meshFilter.sharedMesh != null && meshFilter.GetComponent<Collider>() == null)
                {
                    meshFilter.gameObject.AddComponent<MeshCollider>();
                }
            }

            // Apply default alignment from manifest
            ApplyDefaultAlignment(modelRoot, manifest);

            loadedModels[manifest.id] = modelRoot;
            ApplyPartMappings(modelRoot, manifest);

            // Register with HighlightController for shader-based highlighting
            if (highlightController != null)
            {
                highlightController.RegisterModel(modelRoot);
            }

            OnModelLoaded?.Invoke(modelRoot, manifest);

            Debug.Log($"[EngineModelLoader] Model loaded: {manifest.name} ({modelRoot.GetComponentsInChildren<MeshRenderer>().Length} meshes)");
        }

        private void ApplyDefaultAlignment(GameObject root, EngineManifest manifest)
        {
            if (manifest.defaultAlignment == null) return;

            if (manifest.defaultAlignment.position != null && manifest.defaultAlignment.position.Length == 3)
            {
                root.transform.localPosition = new Vector3(
                    manifest.defaultAlignment.position[0],
                    manifest.defaultAlignment.position[1],
                    manifest.defaultAlignment.position[2]
                );
            }

            if (manifest.defaultAlignment.rotation != null && manifest.defaultAlignment.rotation.Length == 3)
            {
                root.transform.localEulerAngles = new Vector3(
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
        /// Highlights specific parts on the current model as primary (pulsing).
        /// </summary>
        public void HighlightParts(List<string> partIds)
        {
            if (highlightController != null)
            {
                highlightController.ClearAllHighlights();
                highlightController.HighlightParts(partIds, HighlightController.HighlightType.Primary);
                return;
            }

            // Fallback: direct material swap if no HighlightController
            if (CurrentEngine == null || !loadedModels.ContainsKey(CurrentEngine.id)) return;
            GameObject model = loadedModels[CurrentEngine.id];
            PartIdentifier[] parts = model.GetComponentsInChildren<PartIdentifier>();
            foreach (var part in parts)
            {
                Renderer renderer = part.GetComponent<Renderer>();
                if (renderer == null) continue;
                if (partIds.Contains(part.PartId))
                    renderer.material = highlightMaterial != null ? highlightMaterial : renderer.material;
                else
                    renderer.material = defaultMaterial != null ? defaultMaterial : renderer.material;
            }
        }

        /// <summary>
        /// Highlights parts as secondary (available steps, no pulsing).
        /// </summary>
        public void HighlightPartsSecondary(List<string> partIds)
        {
            if (highlightController != null)
            {
                highlightController.HighlightParts(partIds, HighlightController.HighlightType.Secondary);
            }
        }

        /// <summary>
        /// Marks a part as completed (brief green flash).
        /// </summary>
        public void MarkPartCompleted(string partId)
        {
            if (highlightController != null)
            {
                highlightController.MarkPartCompleted(partId);
            }
        }

        /// <summary>
        /// Clears all part highlights.
        /// </summary>
        public void ClearHighlights()
        {
            if (highlightController != null)
            {
                highlightController.ClearAllHighlights();
                return;
            }
            HighlightParts(new List<string>());
        }

        /// <summary>
        /// Unloads the current engine model from memory.
        /// </summary>
        public void UnloadCurrentEngine()
        {
            if (CurrentEngine != null && loadedModels.ContainsKey(CurrentEngine.id))
            {
                if (highlightController != null)
                {
                    highlightController.UnregisterModel();
                }

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
