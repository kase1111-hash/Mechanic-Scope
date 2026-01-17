using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MechanicScope.Performance
{
    /// <summary>
    /// Optimizes assets at runtime for better performance.
    /// Handles texture compression, mesh optimization, and asset pooling.
    /// </summary>
    public class AssetOptimizer : MonoBehaviour
    {
        public static AssetOptimizer Instance { get; private set; }

        [Header("Texture Settings")]
        [SerializeField] private int maxTextureSize = 1024;
        [SerializeField] private bool generateMipmaps = true;
        [SerializeField] private FilterMode defaultFilterMode = FilterMode.Bilinear;
        [SerializeField] private int anisoLevel = 4;

        [Header("Mesh Settings")]
        [SerializeField] private bool optimizeMeshes = true;
        [SerializeField] private bool recalculateNormals = false;
        [SerializeField] private bool recalculateBounds = true;

        [Header("Object Pooling")]
        [SerializeField] private bool enablePooling = true;
        [SerializeField] private int defaultPoolSize = 10;

        [Header("Async Loading")]
        [SerializeField] private int maxConcurrentLoads = 3;
        [SerializeField] private float loadingPriority = 0.5f;

        // Object pools
        private Dictionary<string, Queue<GameObject>> objectPools = new Dictionary<string, Queue<GameObject>>();
        private Dictionary<string, GameObject> poolPrefabs = new Dictionary<string, GameObject>();

        // Loading queue
        private Queue<AsyncLoadRequest> loadQueue = new Queue<AsyncLoadRequest>();
        private int activeLoads = 0;

        // Cache
        private Dictionary<string, Texture2D> textureCache = new Dictionary<string, Texture2D>();
        private Dictionary<string, Mesh> meshCache = new Dictionary<string, Mesh>();

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

        private void Update()
        {
            ProcessLoadQueue();
        }

        #region Texture Optimization

        /// <summary>
        /// Optimizes a texture for mobile rendering.
        /// </summary>
        public Texture2D OptimizeTexture(Texture2D source, TextureOptimizationSettings settings = null)
        {
            if (source == null) return null;

            settings ??= TextureOptimizationSettings.Default;

            int targetSize = Mathf.Min(settings.maxSize, maxTextureSize);

            // Check if resizing needed
            if (source.width <= targetSize && source.height <= targetSize)
            {
                ApplyTextureSettings(source, settings);
                return source;
            }

            // Calculate new dimensions
            float aspect = (float)source.width / source.height;
            int newWidth, newHeight;

            if (source.width > source.height)
            {
                newWidth = targetSize;
                newHeight = Mathf.RoundToInt(targetSize / aspect);
            }
            else
            {
                newHeight = targetSize;
                newWidth = Mathf.RoundToInt(targetSize * aspect);
            }

            // Create resized texture
            RenderTexture rt = RenderTexture.GetTemporary(newWidth, newHeight, 0, RenderTextureFormat.ARGB32);
            rt.filterMode = FilterMode.Bilinear;

            RenderTexture.active = rt;
            Graphics.Blit(source, rt);

            Texture2D resized = new Texture2D(newWidth, newHeight, TextureFormat.RGBA32, settings.generateMipmaps);
            resized.ReadPixels(new Rect(0, 0, newWidth, newHeight), 0, 0);
            resized.Apply(settings.generateMipmaps);

            RenderTexture.active = null;
            RenderTexture.ReleaseTemporary(rt);

            ApplyTextureSettings(resized, settings);

            return resized;
        }

        private void ApplyTextureSettings(Texture2D texture, TextureOptimizationSettings settings)
        {
            texture.filterMode = settings.filterMode;
            texture.anisoLevel = settings.anisoLevel;
            texture.wrapMode = settings.wrapMode;
        }

        /// <summary>
        /// Compresses a texture for reduced memory usage.
        /// </summary>
        public Texture2D CompressTexture(Texture2D source, bool highQuality = false)
        {
            if (source == null) return null;

            // Choose compression format based on platform
            TextureFormat format = GetOptimalCompressionFormat(highQuality);

            // Create compressed copy
            Texture2D compressed = new Texture2D(source.width, source.height, format, source.mipmapCount > 1);
            compressed.SetPixels(source.GetPixels());
            compressed.Compress(highQuality);
            compressed.Apply(true);

            return compressed;
        }

        private TextureFormat GetOptimalCompressionFormat(bool highQuality)
        {
            #if UNITY_IOS
            return highQuality ? TextureFormat.ASTC_6x6 : TextureFormat.ASTC_8x8;
            #elif UNITY_ANDROID
            return highQuality ? TextureFormat.ETC2_RGBA8 : TextureFormat.ETC2_RGB;
            #else
            return highQuality ? TextureFormat.DXT5 : TextureFormat.DXT1;
            #endif
        }

        #endregion

        #region Mesh Optimization

        /// <summary>
        /// Optimizes a mesh for better rendering performance.
        /// </summary>
        public Mesh OptimizeMesh(Mesh source, MeshOptimizationSettings settings = null)
        {
            if (source == null) return null;

            settings ??= MeshOptimizationSettings.Default;

            // Create copy if needed
            Mesh optimized = source;
            if (!source.isReadable)
            {
                Debug.LogWarning($"Mesh {source.name} is not readable, cannot optimize");
                return source;
            }

            // Optimize mesh data layout
            if (settings.optimizeIndexBuffer)
            {
                optimized.OptimizeIndexBuffers();
            }

            if (settings.optimizeReorderVertexBuffer)
            {
                optimized.OptimizeReorderVertexBuffer();
            }

            // Recalculate if needed
            if (settings.recalculateNormals || recalculateNormals)
            {
                optimized.RecalculateNormals();
            }

            if (settings.recalculateTangents)
            {
                optimized.RecalculateTangents();
            }

            if (settings.recalculateBounds || recalculateBounds)
            {
                optimized.RecalculateBounds();
            }

            // Mark as non-readable to save memory (if not needed for further processing)
            if (settings.markNoLongerReadable)
            {
                optimized.UploadMeshData(true);
            }

            return optimized;
        }

        /// <summary>
        /// Combines multiple meshes into one for fewer draw calls.
        /// </summary>
        public Mesh CombineMeshes(MeshFilter[] meshFilters, bool mergeSubMeshes = true)
        {
            if (meshFilters == null || meshFilters.Length == 0) return null;

            CombineInstance[] combine = new CombineInstance[meshFilters.Length];

            for (int i = 0; i < meshFilters.Length; i++)
            {
                combine[i].mesh = meshFilters[i].sharedMesh;
                combine[i].transform = meshFilters[i].transform.localToWorldMatrix;
            }

            Mesh combined = new Mesh();
            combined.CombineMeshes(combine, mergeSubMeshes);
            combined.RecalculateBounds();

            return combined;
        }

        #endregion

        #region Object Pooling

        /// <summary>
        /// Registers a prefab for object pooling.
        /// </summary>
        public void RegisterPool(string poolId, GameObject prefab, int initialSize = 0)
        {
            if (string.IsNullOrEmpty(poolId) || prefab == null) return;

            if (!objectPools.ContainsKey(poolId))
            {
                objectPools[poolId] = new Queue<GameObject>();
                poolPrefabs[poolId] = prefab;
            }

            // Pre-warm pool
            int size = initialSize > 0 ? initialSize : defaultPoolSize;
            for (int i = 0; i < size; i++)
            {
                GameObject obj = Instantiate(prefab, transform);
                obj.SetActive(false);
                objectPools[poolId].Enqueue(obj);
            }
        }

        /// <summary>
        /// Gets an object from the pool.
        /// </summary>
        public GameObject GetFromPool(string poolId, Vector3 position, Quaternion rotation)
        {
            if (!enablePooling || !objectPools.ContainsKey(poolId))
            {
                // Fallback to instantiation
                if (poolPrefabs.ContainsKey(poolId))
                {
                    return Instantiate(poolPrefabs[poolId], position, rotation);
                }
                return null;
            }

            GameObject obj;
            if (objectPools[poolId].Count > 0)
            {
                obj = objectPools[poolId].Dequeue();
            }
            else
            {
                // Pool exhausted, create new
                obj = Instantiate(poolPrefabs[poolId], transform);
            }

            obj.transform.position = position;
            obj.transform.rotation = rotation;
            obj.SetActive(true);

            return obj;
        }

        /// <summary>
        /// Returns an object to the pool.
        /// </summary>
        public void ReturnToPool(string poolId, GameObject obj)
        {
            if (!enablePooling || obj == null) return;

            obj.SetActive(false);
            obj.transform.SetParent(transform);

            if (objectPools.ContainsKey(poolId))
            {
                objectPools[poolId].Enqueue(obj);
            }
            else
            {
                Destroy(obj);
            }
        }

        /// <summary>
        /// Clears a specific pool.
        /// </summary>
        public void ClearPool(string poolId)
        {
            if (!objectPools.ContainsKey(poolId)) return;

            while (objectPools[poolId].Count > 0)
            {
                var obj = objectPools[poolId].Dequeue();
                if (obj != null) Destroy(obj);
            }
        }

        /// <summary>
        /// Clears all pools.
        /// </summary>
        public void ClearAllPools()
        {
            foreach (var poolId in objectPools.Keys)
            {
                ClearPool(poolId);
            }
            objectPools.Clear();
        }

        #endregion

        #region Async Loading

        /// <summary>
        /// Loads an asset asynchronously.
        /// </summary>
        public void LoadAssetAsync<T>(string path, Action<T> onComplete) where T : UnityEngine.Object
        {
            loadQueue.Enqueue(new AsyncLoadRequest
            {
                path = path,
                type = typeof(T),
                callback = (obj) => onComplete?.Invoke(obj as T)
            });
        }

        private void ProcessLoadQueue()
        {
            while (loadQueue.Count > 0 && activeLoads < maxConcurrentLoads)
            {
                var request = loadQueue.Dequeue();
                StartCoroutine(LoadAssetCoroutine(request));
            }
        }

        private IEnumerator LoadAssetCoroutine(AsyncLoadRequest request)
        {
            activeLoads++;

            ResourceRequest resourceRequest = Resources.LoadAsync(request.path, request.type);
            resourceRequest.priority = Mathf.RoundToInt(loadingPriority * 100);

            yield return resourceRequest;

            request.callback?.Invoke(resourceRequest.asset);

            activeLoads--;
        }

        private struct AsyncLoadRequest
        {
            public string path;
            public Type type;
            public Action<UnityEngine.Object> callback;
        }

        #endregion

        #region Cache Management

        /// <summary>
        /// Caches a texture.
        /// </summary>
        public void CacheTexture(string key, Texture2D texture)
        {
            textureCache[key] = texture;
        }

        /// <summary>
        /// Gets a cached texture.
        /// </summary>
        public Texture2D GetCachedTexture(string key)
        {
            return textureCache.TryGetValue(key, out var texture) ? texture : null;
        }

        /// <summary>
        /// Clears texture cache.
        /// </summary>
        public void ClearTextureCache()
        {
            foreach (var texture in textureCache.Values)
            {
                if (texture != null) Destroy(texture);
            }
            textureCache.Clear();
        }

        /// <summary>
        /// Gets cache statistics.
        /// </summary>
        public CacheStats GetCacheStats()
        {
            return new CacheStats
            {
                textureCount = textureCache.Count,
                meshCount = meshCache.Count,
                poolCount = objectPools.Count,
                totalPooledObjects = CountPooledObjects()
            };
        }

        private int CountPooledObjects()
        {
            int count = 0;
            foreach (var pool in objectPools.Values)
            {
                count += pool.Count;
            }
            return count;
        }

        #endregion
    }

    /// <summary>
    /// Settings for texture optimization.
    /// </summary>
    [Serializable]
    public class TextureOptimizationSettings
    {
        public int maxSize = 1024;
        public bool generateMipmaps = true;
        public FilterMode filterMode = FilterMode.Bilinear;
        public int anisoLevel = 4;
        public TextureWrapMode wrapMode = TextureWrapMode.Clamp;

        public static TextureOptimizationSettings Default => new TextureOptimizationSettings();

        public static TextureOptimizationSettings HighQuality => new TextureOptimizationSettings
        {
            maxSize = 2048,
            generateMipmaps = true,
            filterMode = FilterMode.Trilinear,
            anisoLevel = 8
        };

        public static TextureOptimizationSettings LowMemory => new TextureOptimizationSettings
        {
            maxSize = 512,
            generateMipmaps = false,
            filterMode = FilterMode.Bilinear,
            anisoLevel = 1
        };
    }

    /// <summary>
    /// Settings for mesh optimization.
    /// </summary>
    [Serializable]
    public class MeshOptimizationSettings
    {
        public bool optimizeIndexBuffer = true;
        public bool optimizeReorderVertexBuffer = true;
        public bool recalculateNormals = false;
        public bool recalculateTangents = false;
        public bool recalculateBounds = true;
        public bool markNoLongerReadable = false;

        public static MeshOptimizationSettings Default => new MeshOptimizationSettings();
    }

    /// <summary>
    /// Cache statistics.
    /// </summary>
    public struct CacheStats
    {
        public int textureCount;
        public int meshCount;
        public int poolCount;
        public int totalPooledObjects;
    }
}
