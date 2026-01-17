using System;
using System.Collections;
using System.IO;
using UnityEngine;

namespace MechanicScope.AppStore
{
    /// <summary>
    /// Captures screenshots for app store listings.
    /// Supports various device resolutions and formats.
    /// </summary>
    public class ScreenshotCapture : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField] private string outputDirectory = "Screenshots";
        [SerializeField] private string filePrefix = "MechanicScope";
        [SerializeField] private ImageFormat format = ImageFormat.PNG;
        [SerializeField] private int superSampleScale = 1;

        [Header("Device Presets")]
        [SerializeField] private DevicePreset[] presets = new DevicePreset[]
        {
            new DevicePreset { name = "iPhone_6.7", width = 1290, height = 2796, description = "iPhone 14 Pro Max, 15 Pro Max" },
            new DevicePreset { name = "iPhone_6.5", width = 1284, height = 2778, description = "iPhone 12 Pro Max, 13 Pro Max" },
            new DevicePreset { name = "iPhone_5.5", width = 1242, height = 2208, description = "iPhone 8 Plus" },
            new DevicePreset { name = "iPad_12.9", width = 2048, height = 2732, description = "iPad Pro 12.9\"" },
            new DevicePreset { name = "iPad_11", width = 1668, height = 2388, description = "iPad Pro 11\"" },
            new DevicePreset { name = "Android_Phone", width = 1080, height = 1920, description = "Standard Android phone" },
            new DevicePreset { name = "Android_Tablet", width = 1600, height = 2560, description = "Android tablet" }
        };

        [Header("Automation")]
        [SerializeField] private bool hideUIForCapture = false;
        [SerializeField] private float captureDelay = 0.5f;
        [SerializeField] private GameObject[] objectsToHide;

        public event Action<string> OnScreenshotCaptured;
        public event Action<string> OnCaptureFailed;

        public enum ImageFormat
        {
            PNG,
            JPG
        }

        [Serializable]
        public class DevicePreset
        {
            public string name;
            public int width;
            public int height;
            public string description;
        }

        private string GetOutputPath()
        {
            string path = Path.Combine(Application.persistentDataPath, outputDirectory);
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }
            return path;
        }

        /// <summary>
        /// Captures a screenshot at the current resolution.
        /// </summary>
        public void CaptureScreenshot()
        {
            StartCoroutine(CaptureCoroutine(Screen.width, Screen.height, "Current"));
        }

        /// <summary>
        /// Captures a screenshot at a specific device preset.
        /// </summary>
        public void CaptureForDevice(string presetName)
        {
            DevicePreset preset = Array.Find(presets, p => p.name == presetName);
            if (preset == null)
            {
                OnCaptureFailed?.Invoke($"Preset not found: {presetName}");
                return;
            }

            StartCoroutine(CaptureCoroutine(preset.width, preset.height, preset.name));
        }

        /// <summary>
        /// Captures screenshots for all device presets.
        /// </summary>
        public void CaptureAllDevices()
        {
            StartCoroutine(CaptureAllCoroutine());
        }

        private IEnumerator CaptureCoroutine(int width, int height, string deviceName)
        {
            // Wait for end of frame
            yield return new WaitForEndOfFrame();

            // Hide objects if needed
            bool[] previousStates = null;
            if (hideUIForCapture && objectsToHide != null)
            {
                previousStates = new bool[objectsToHide.Length];
                for (int i = 0; i < objectsToHide.Length; i++)
                {
                    if (objectsToHide[i] != null)
                    {
                        previousStates[i] = objectsToHide[i].activeSelf;
                        objectsToHide[i].SetActive(false);
                    }
                }
                yield return new WaitForSeconds(captureDelay);
            }

            try
            {
                // Create render texture at target resolution
                int captureWidth = width * superSampleScale;
                int captureHeight = height * superSampleScale;

                RenderTexture rt = new RenderTexture(captureWidth, captureHeight, 24, RenderTextureFormat.ARGB32);
                rt.antiAliasing = 4;

                Camera camera = Camera.main;
                if (camera == null)
                {
                    camera = FindFirstObjectByType<Camera>();
                }

                if (camera != null)
                {
                    RenderTexture previousRT = camera.targetTexture;
                    camera.targetTexture = rt;
                    camera.Render();
                    camera.targetTexture = previousRT;
                }

                // Read pixels
                RenderTexture.active = rt;
                Texture2D screenshot = new Texture2D(captureWidth, captureHeight, TextureFormat.RGB24, false);
                screenshot.ReadPixels(new Rect(0, 0, captureWidth, captureHeight), 0, 0);
                screenshot.Apply();

                // Downscale if supersampled
                if (superSampleScale > 1)
                {
                    screenshot = ScaleTexture(screenshot, width, height);
                }

                // Encode and save
                byte[] bytes;
                string extension;

                if (format == ImageFormat.PNG)
                {
                    bytes = screenshot.EncodeToPNG();
                    extension = "png";
                }
                else
                {
                    bytes = screenshot.EncodeToJPG(95);
                    extension = "jpg";
                }

                string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                string filename = $"{filePrefix}_{deviceName}_{timestamp}.{extension}";
                string filePath = Path.Combine(GetOutputPath(), filename);

                File.WriteAllBytes(filePath, bytes);

                // Cleanup
                RenderTexture.active = null;
                Destroy(rt);
                Destroy(screenshot);

                Debug.Log($"Screenshot saved: {filePath}");
                OnScreenshotCaptured?.Invoke(filePath);
            }
            catch (Exception e)
            {
                Debug.LogError($"Screenshot failed: {e.Message}");
                OnCaptureFailed?.Invoke(e.Message);
            }

            // Restore hidden objects
            if (previousStates != null)
            {
                for (int i = 0; i < objectsToHide.Length; i++)
                {
                    if (objectsToHide[i] != null)
                    {
                        objectsToHide[i].SetActive(previousStates[i]);
                    }
                }
            }
        }

        private IEnumerator CaptureAllCoroutine()
        {
            foreach (var preset in presets)
            {
                yield return CaptureCoroutine(preset.width, preset.height, preset.name);
                yield return new WaitForSeconds(0.5f);
            }

            Debug.Log($"Captured {presets.Length} screenshots for all device presets");
        }

        private Texture2D ScaleTexture(Texture2D source, int targetWidth, int targetHeight)
        {
            RenderTexture rt = RenderTexture.GetTemporary(targetWidth, targetHeight, 0, RenderTextureFormat.ARGB32);
            rt.filterMode = FilterMode.Bilinear;

            RenderTexture.active = rt;
            Graphics.Blit(source, rt);

            Texture2D result = new Texture2D(targetWidth, targetHeight, TextureFormat.RGB24, false);
            result.ReadPixels(new Rect(0, 0, targetWidth, targetHeight), 0, 0);
            result.Apply();

            RenderTexture.active = null;
            RenderTexture.ReleaseTemporary(rt);
            Destroy(source);

            return result;
        }

        /// <summary>
        /// Gets all available device presets.
        /// </summary>
        public DevicePreset[] GetPresets()
        {
            return presets;
        }

        /// <summary>
        /// Opens the screenshot output directory.
        /// </summary>
        public void OpenOutputDirectory()
        {
            string path = GetOutputPath();
            #if UNITY_EDITOR
            UnityEditor.EditorUtility.RevealInFinder(path);
            #elif UNITY_STANDALONE_WIN
            System.Diagnostics.Process.Start("explorer.exe", path);
            #elif UNITY_STANDALONE_OSX
            System.Diagnostics.Process.Start("open", path);
            #endif
        }
    }
}
