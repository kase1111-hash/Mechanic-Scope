using System;
using System.Collections.Generic;
using UnityEngine;

namespace MechanicScope.Performance
{
    /// <summary>
    /// Manages Level of Detail (LOD) for 3D engine models.
    /// Optimizes rendering performance by showing simpler models at distance.
    /// </summary>
    public class LODManager : MonoBehaviour
    {
        public static LODManager Instance { get; private set; }

        [Header("LOD Settings")]
        [SerializeField] private float lodBias = 1.0f;
        [SerializeField] private float[] lodDistances = { 2f, 5f, 10f };
        [SerializeField] private bool enableAutomaticLOD = true;

        [Header("Performance Targets")]
        [SerializeField] private int targetFrameRate = 60;
        [SerializeField] private bool adaptiveLOD = true;
        [SerializeField] private float adaptiveCheckInterval = 1f;

        [Header("Memory")]
        [SerializeField] private long maxTextureMemoryMB = 256;
        [SerializeField] private bool unloadUnusedAssets = true;
        [SerializeField] private float unloadInterval = 30f;

        // Runtime state
        private Dictionary<GameObject, LODGroup> managedLODGroups = new Dictionary<GameObject, LODGroup>();
        private float lastAdaptiveCheck;
        private float lastUnloadTime;
        private float currentFrameRate;
        private int frameCount;
        private float frameTimer;

        // Performance metrics
        public float CurrentFrameRate => currentFrameRate;
        public int ManagedObjectCount => managedLODGroups.Count;
        public long EstimatedTextureMemoryMB { get; private set; }

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void Start()
        {
            Application.targetFrameRate = targetFrameRate;
            QualitySettings.lodBias = lodBias;

            // Set texture memory budget
            QualitySettings.masterTextureLimit = CalculateTextureQualityLevel();
        }

        private void Update()
        {
            UpdateFrameRate();

            if (adaptiveLOD && Time.time - lastAdaptiveCheck > adaptiveCheckInterval)
            {
                lastAdaptiveCheck = Time.time;
                AdaptLODToPerformance();
            }

            if (unloadUnusedAssets && Time.time - lastUnloadTime > unloadInterval)
            {
                lastUnloadTime = Time.time;
                UnloadUnused();
            }
        }

        private void UpdateFrameRate()
        {
            frameCount++;
            frameTimer += Time.unscaledDeltaTime;

            if (frameTimer >= 0.5f)
            {
                currentFrameRate = frameCount / frameTimer;
                frameCount = 0;
                frameTimer = 0;
            }
        }

        /// <summary>
        /// Registers a model for LOD management.
        /// Creates LOD levels from the model if not already present.
        /// </summary>
        public void RegisterModel(GameObject model, LODConfiguration config = null)
        {
            if (model == null || managedLODGroups.ContainsKey(model)) return;

            LODGroup lodGroup = model.GetComponent<LODGroup>();

            if (lodGroup == null && enableAutomaticLOD)
            {
                lodGroup = CreateAutomaticLOD(model, config ?? LODConfiguration.Default);
            }

            if (lodGroup != null)
            {
                managedLODGroups[model] = lodGroup;
            }
        }

        /// <summary>
        /// Unregisters a model from LOD management.
        /// </summary>
        public void UnregisterModel(GameObject model)
        {
            managedLODGroups.Remove(model);
        }

        /// <summary>
        /// Creates automatic LOD levels for a model.
        /// </summary>
        private LODGroup CreateAutomaticLOD(GameObject model, LODConfiguration config)
        {
            Renderer[] renderers = model.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return null;

            LODGroup lodGroup = model.AddComponent<LODGroup>();

            LOD[] lods = new LOD[config.levels + 1]; // +1 for cull level

            // LOD 0 - Full detail
            lods[0] = new LOD(GetLODScreenHeight(0, config), renderers);

            // Generate simplified LOD levels
            for (int i = 1; i < config.levels; i++)
            {
                float screenHeight = GetLODScreenHeight(i, config);

                // In production, you would use mesh simplification
                // For now, we just use the same renderers with material swaps
                Renderer[] lodRenderers = CreateSimplifiedRenderers(model, renderers, i, config);
                lods[i] = new LOD(screenHeight, lodRenderers);
            }

            // Cull level (nothing rendered)
            lods[config.levels] = new LOD(config.cullScreenHeight, new Renderer[0]);

            lodGroup.SetLODs(lods);
            lodGroup.RecalculateBounds();

            return lodGroup;
        }

        private float GetLODScreenHeight(int level, LODConfiguration config)
        {
            if (level == 0) return config.lod0ScreenHeight;

            float ratio = 1f - ((float)level / config.levels);
            return Mathf.Max(config.cullScreenHeight + 0.01f, config.lod0ScreenHeight * ratio * ratio);
        }

        private Renderer[] CreateSimplifiedRenderers(GameObject model, Renderer[] originals, int level, LODConfiguration config)
        {
            // For actual simplification, you would use:
            // - Unity's built-in mesh simplification
            // - Third-party decimation (e.g., Simplygon, Meshlab)
            // - Pre-authored LOD meshes

            // For this implementation, we use the same meshes but could apply:
            // - Simpler materials
            // - Lower resolution textures
            // - Disabled features (normal maps, etc.)

            List<Renderer> lodRenderers = new List<Renderer>();

            foreach (var original in originals)
            {
                // Clone for LOD level
                if (original is MeshRenderer meshRenderer)
                {
                    MeshFilter meshFilter = original.GetComponent<MeshFilter>();
                    if (meshFilter != null && meshFilter.sharedMesh != null)
                    {
                        // Create simplified version
                        GameObject lodObject = new GameObject($"{original.name}_LOD{level}");
                        lodObject.transform.SetParent(model.transform);
                        lodObject.transform.localPosition = original.transform.localPosition;
                        lodObject.transform.localRotation = original.transform.localRotation;
                        lodObject.transform.localScale = original.transform.localScale;

                        MeshFilter lodFilter = lodObject.AddComponent<MeshFilter>();
                        lodFilter.sharedMesh = SimplifyMesh(meshFilter.sharedMesh, level, config);

                        MeshRenderer lodRenderer = lodObject.AddComponent<MeshRenderer>();
                        lodRenderer.sharedMaterials = GetLODMaterials(meshRenderer.sharedMaterials, level);

                        lodRenderers.Add(lodRenderer);
                        lodObject.SetActive(false); // LOD system controls visibility
                    }
                }
            }

            return lodRenderers.Count > 0 ? lodRenderers.ToArray() : originals;
        }

        private Mesh SimplifyMesh(Mesh original, int level, LODConfiguration config)
        {
            // Simplified mesh generation placeholder
            // In production, use mesh decimation algorithms

            float reduction = config.reductionPerLevel * level;
            int targetVertices = Mathf.Max(100, Mathf.RoundToInt(original.vertexCount * (1f - reduction)));

            // For now, return original mesh
            // Real implementation would decimate the mesh
            return original;
        }

        private Material[] GetLODMaterials(Material[] originals, int level)
        {
            Material[] lodMaterials = new Material[originals.Length];

            for (int i = 0; i < originals.Length; i++)
            {
                if (level >= 2)
                {
                    // Use simpler shader for distant LODs
                    lodMaterials[i] = CreateSimpleMaterial(originals[i]);
                }
                else
                {
                    lodMaterials[i] = originals[i];
                }
            }

            return lodMaterials;
        }

        private Material CreateSimpleMaterial(Material original)
        {
            // Create a simpler version of the material for LOD
            Material simple = new Material(Shader.Find("Mobile/Diffuse"));
            simple.color = original.color;

            if (original.mainTexture != null)
            {
                simple.mainTexture = original.mainTexture;
            }

            return simple;
        }

        private void AdaptLODToPerformance()
        {
            if (!adaptiveLOD) return;

            float targetFPS = targetFrameRate;
            float currentFPS = currentFrameRate;

            if (currentFPS < targetFPS * 0.8f)
            {
                // Performance is poor, increase LOD bias to show lower detail
                lodBias = Mathf.Max(0.5f, lodBias - 0.1f);
                QualitySettings.lodBias = lodBias;
                Debug.Log($"[LODManager] Decreasing quality, LOD bias: {lodBias}");
            }
            else if (currentFPS > targetFPS * 0.95f && lodBias < 2f)
            {
                // Performance is good, can increase quality
                lodBias = Mathf.Min(2f, lodBias + 0.05f);
                QualitySettings.lodBias = lodBias;
            }
        }

        private void UnloadUnused()
        {
            Resources.UnloadUnusedAssets();
            GC.Collect();

            EstimatedTextureMemoryMB = Profiler.GetAllocatedMemoryForGraphicsDriver() / (1024 * 1024);
        }

        private int CalculateTextureQualityLevel()
        {
            long systemMemory = SystemInfo.systemMemorySize;

            // Adjust texture quality based on device memory
            if (systemMemory <= 2048) return 2; // Quarter resolution
            if (systemMemory <= 4096) return 1; // Half resolution
            return 0; // Full resolution
        }

        /// <summary>
        /// Forces a specific LOD level for all managed objects.
        /// </summary>
        public void ForceLODLevel(int level)
        {
            foreach (var kvp in managedLODGroups)
            {
                if (kvp.Value != null)
                {
                    kvp.Value.ForceLOD(level);
                }
            }
        }

        /// <summary>
        /// Clears forced LOD and returns to automatic selection.
        /// </summary>
        public void ClearForcedLOD()
        {
            foreach (var kvp in managedLODGroups)
            {
                if (kvp.Value != null)
                {
                    kvp.Value.ForceLOD(-1);
                }
            }
        }

        /// <summary>
        /// Sets the target frame rate.
        /// </summary>
        public void SetTargetFrameRate(int fps)
        {
            targetFrameRate = Mathf.Clamp(fps, 30, 120);
            Application.targetFrameRate = targetFrameRate;
        }

        /// <summary>
        /// Gets performance statistics.
        /// </summary>
        public PerformanceStats GetStats()
        {
            return new PerformanceStats
            {
                frameRate = currentFrameRate,
                lodBias = lodBias,
                managedObjects = managedLODGroups.Count,
                textureMemoryMB = EstimatedTextureMemoryMB,
                systemMemoryMB = SystemInfo.systemMemorySize
            };
        }
    }

    /// <summary>
    /// Configuration for LOD generation.
    /// </summary>
    [Serializable]
    public class LODConfiguration
    {
        public int levels = 3;
        public float lod0ScreenHeight = 0.6f;
        public float cullScreenHeight = 0.01f;
        public float reductionPerLevel = 0.25f;

        public static LODConfiguration Default => new LODConfiguration();

        public static LODConfiguration HighQuality => new LODConfiguration
        {
            levels = 4,
            lod0ScreenHeight = 0.8f,
            cullScreenHeight = 0.005f,
            reductionPerLevel = 0.2f
        };

        public static LODConfiguration Performance => new LODConfiguration
        {
            levels = 2,
            lod0ScreenHeight = 0.5f,
            cullScreenHeight = 0.02f,
            reductionPerLevel = 0.4f
        };
    }

    /// <summary>
    /// Performance statistics snapshot.
    /// </summary>
    public struct PerformanceStats
    {
        public float frameRate;
        public float lodBias;
        public int managedObjects;
        public long textureMemoryMB;
        public int systemMemoryMB;
    }
}
