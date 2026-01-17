using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;
using MechanicScope.Core;

namespace MechanicScope.Utils
{
    /// <summary>
    /// Handles loading and caching of media (images, videos) for procedure steps.
    /// </summary>
    public class StepMediaLoader : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField] private int maxCacheSize = 50;
        [SerializeField] private int maxTextureSize = 1024;
        [SerializeField] private Texture2D placeholderTexture;
        [SerializeField] private Texture2D errorTexture;

        // Events
        public event Action<string, Texture2D> OnImageLoaded;
        public event Action<string, string> OnLoadError;

        // Cache
        private Dictionary<string, Texture2D> textureCache = new Dictionary<string, Texture2D>();
        private Queue<string> cacheOrder = new Queue<string>();
        private Dictionary<string, List<Action<Texture2D>>> pendingCallbacks = new Dictionary<string, List<Action<Texture2D>>>();

        public static StepMediaLoader Instance { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void OnDestroy()
        {
            ClearCache();
            if (Instance == this)
            {
                Instance = null;
            }
        }

        /// <summary>
        /// Loads an image for a procedure step.
        /// </summary>
        public void LoadStepImage(string engineId, string procedureId, string imagePath, Action<Texture2D> callback)
        {
            if (string.IsNullOrEmpty(imagePath))
            {
                callback?.Invoke(null);
                return;
            }

            // Build full path
            string fullPath = GetFullMediaPath(engineId, procedureId, imagePath);
            string cacheKey = GetCacheKey(fullPath);

            // Check cache
            if (textureCache.TryGetValue(cacheKey, out Texture2D cachedTexture))
            {
                callback?.Invoke(cachedTexture);
                return;
            }

            // Check if already loading
            if (pendingCallbacks.ContainsKey(cacheKey))
            {
                pendingCallbacks[cacheKey].Add(callback);
                return;
            }

            // Start loading
            pendingCallbacks[cacheKey] = new List<Action<Texture2D>> { callback };
            StartCoroutine(LoadImageCoroutine(fullPath, cacheKey));
        }

        /// <summary>
        /// Preloads images for a procedure (call when procedure is loaded).
        /// </summary>
        public void PreloadProcedureImages(Procedure procedure)
        {
            if (procedure?.steps == null) return;

            foreach (var step in procedure.steps)
            {
                if (step.media?.image != null)
                {
                    LoadStepImage(procedure.engineId, procedure.id, step.media.image, null);
                }
            }
        }

        /// <summary>
        /// Gets a cached texture if available.
        /// </summary>
        public Texture2D GetCachedTexture(string engineId, string procedureId, string imagePath)
        {
            if (string.IsNullOrEmpty(imagePath)) return null;

            string fullPath = GetFullMediaPath(engineId, procedureId, imagePath);
            string cacheKey = GetCacheKey(fullPath);

            textureCache.TryGetValue(cacheKey, out Texture2D texture);
            return texture;
        }

        private IEnumerator LoadImageCoroutine(string path, string cacheKey)
        {
            Texture2D result = null;

            // Try loading from file system first
            if (File.Exists(path))
            {
                byte[] imageData = null;

                try
                {
                    imageData = File.ReadAllBytes(path);
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"Failed to read image file: {e.Message}");
                }

                if (imageData != null)
                {
                    result = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                    if (result.LoadImage(imageData))
                    {
                        // Resize if needed
                        if (result.width > maxTextureSize || result.height > maxTextureSize)
                        {
                            result = ResizeTexture(result, maxTextureSize);
                        }
                    }
                    else
                    {
                        Destroy(result);
                        result = null;
                    }
                }
            }
            else if (path.StartsWith("http"))
            {
                // Load from URL
                using (UnityWebRequest request = UnityWebRequestTexture.GetTexture(path))
                {
                    yield return request.SendWebRequest();

                    if (request.result == UnityWebRequest.Result.Success)
                    {
                        result = DownloadHandlerTexture.GetContent(request);

                        // Resize if needed
                        if (result.width > maxTextureSize || result.height > maxTextureSize)
                        {
                            result = ResizeTexture(result, maxTextureSize);
                        }
                    }
                    else
                    {
                        Debug.LogWarning($"Failed to load image from URL: {request.error}");
                    }
                }
            }
            else
            {
                // Try loading from StreamingAssets
                string streamingPath = Path.Combine(Application.streamingAssetsPath, path);

                #if UNITY_ANDROID && !UNITY_EDITOR
                // Android needs UnityWebRequest for StreamingAssets
                using (UnityWebRequest request = UnityWebRequestTexture.GetTexture(streamingPath))
                {
                    yield return request.SendWebRequest();

                    if (request.result == UnityWebRequest.Result.Success)
                    {
                        result = DownloadHandlerTexture.GetContent(request);
                    }
                }
                #else
                if (File.Exists(streamingPath))
                {
                    byte[] imageData = File.ReadAllBytes(streamingPath);
                    result = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                    if (!result.LoadImage(imageData))
                    {
                        Destroy(result);
                        result = null;
                    }
                }
                #endif
            }

            // Cache the result
            if (result != null)
            {
                AddToCache(cacheKey, result);
                OnImageLoaded?.Invoke(cacheKey, result);
            }
            else
            {
                result = errorTexture;
                OnLoadError?.Invoke(cacheKey, "Failed to load image");
            }

            // Invoke callbacks
            if (pendingCallbacks.TryGetValue(cacheKey, out List<Action<Texture2D>> callbacks))
            {
                foreach (var callback in callbacks)
                {
                    callback?.Invoke(result);
                }
                pendingCallbacks.Remove(cacheKey);
            }
        }

        private string GetFullMediaPath(string engineId, string procedureId, string mediaPath)
        {
            // Check various locations for the media file

            // 1. Absolute path or URL
            if (Path.IsPathRooted(mediaPath) || mediaPath.StartsWith("http"))
            {
                return mediaPath;
            }

            // 2. Relative to procedure in persistent data
            string persistentPath = Path.Combine(
                Application.persistentDataPath, "engines", engineId, "procedures", "media", mediaPath
            );
            if (File.Exists(persistentPath))
            {
                return persistentPath;
            }

            // 3. Relative to engine in persistent data
            string engineMediaPath = Path.Combine(
                Application.persistentDataPath, "engines", engineId, "media", mediaPath
            );
            if (File.Exists(engineMediaPath))
            {
                return engineMediaPath;
            }

            // 4. StreamingAssets path
            string streamingPath = Path.Combine(
                "Engines", engineId, "procedures", "media", mediaPath
            );

            return streamingPath;
        }

        private string GetCacheKey(string path)
        {
            return path.GetHashCode().ToString();
        }

        private void AddToCache(string key, Texture2D texture)
        {
            // Remove oldest entries if cache is full
            while (textureCache.Count >= maxCacheSize && cacheOrder.Count > 0)
            {
                string oldestKey = cacheOrder.Dequeue();
                if (textureCache.TryGetValue(oldestKey, out Texture2D oldTexture))
                {
                    Destroy(oldTexture);
                    textureCache.Remove(oldestKey);
                }
            }

            textureCache[key] = texture;
            cacheOrder.Enqueue(key);
        }

        private Texture2D ResizeTexture(Texture2D source, int maxSize)
        {
            int newWidth, newHeight;

            if (source.width > source.height)
            {
                newWidth = maxSize;
                newHeight = (int)(source.height * ((float)maxSize / source.width));
            }
            else
            {
                newHeight = maxSize;
                newWidth = (int)(source.width * ((float)maxSize / source.height));
            }

            RenderTexture rt = RenderTexture.GetTemporary(newWidth, newHeight, 0, RenderTextureFormat.ARGB32);
            rt.filterMode = FilterMode.Bilinear;

            RenderTexture.active = rt;
            Graphics.Blit(source, rt);

            Texture2D result = new Texture2D(newWidth, newHeight, TextureFormat.RGBA32, false);
            result.ReadPixels(new Rect(0, 0, newWidth, newHeight), 0, 0);
            result.Apply();

            RenderTexture.active = null;
            RenderTexture.ReleaseTemporary(rt);

            Destroy(source);

            return result;
        }

        /// <summary>
        /// Clears all cached textures.
        /// </summary>
        public void ClearCache()
        {
            foreach (var kvp in textureCache)
            {
                if (kvp.Value != null && kvp.Value != placeholderTexture && kvp.Value != errorTexture)
                {
                    Destroy(kvp.Value);
                }
            }

            textureCache.Clear();
            cacheOrder.Clear();
        }

        /// <summary>
        /// Gets current cache size.
        /// </summary>
        public int GetCacheCount()
        {
            return textureCache.Count;
        }

        /// <summary>
        /// Creates a sprite from a texture.
        /// </summary>
        public static Sprite TextureToSprite(Texture2D texture)
        {
            if (texture == null) return null;

            return Sprite.Create(
                texture,
                new Rect(0, 0, texture.width, texture.height),
                new Vector2(0.5f, 0.5f),
                100f
            );
        }
    }
}
