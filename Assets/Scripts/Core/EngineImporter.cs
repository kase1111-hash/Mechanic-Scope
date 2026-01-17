using System;
using System.Collections;
using System.IO;
using System.IO.Compression;
using UnityEngine;

namespace MechanicScope.Core
{
    /// <summary>
    /// Handles importing engine models and procedure packages.
    /// Supports .zip packages and individual file imports.
    /// </summary>
    public class EngineImporter : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField] private string[] supportedModelFormats = { ".glb", ".gltf", ".fbx", ".obj" };
        [SerializeField] private long maxFileSizeMB = 100;

        // Events
        public event Action<string> OnImportStarted;
        public event Action<float> OnImportProgress;
        public event Action<EngineManifest> OnImportCompleted;
        public event Action<string> OnImportFailed;

        // Properties
        public bool IsImporting { get; private set; }
        public float Progress { get; private set; }

        private string enginesDirectory;

        private void Awake()
        {
            enginesDirectory = Path.Combine(Application.persistentDataPath, "engines");
            EnsureDirectoryExists(enginesDirectory);
        }

        /// <summary>
        /// Imports an engine from a zip package.
        /// Package should contain engine.json and the model file.
        /// </summary>
        public void ImportFromZip(string zipPath)
        {
            if (IsImporting)
            {
                OnImportFailed?.Invoke("Another import is already in progress.");
                return;
            }

            StartCoroutine(ImportZipCoroutine(zipPath));
        }

        /// <summary>
        /// Imports an engine from a folder path.
        /// </summary>
        public void ImportFromFolder(string folderPath)
        {
            if (IsImporting)
            {
                OnImportFailed?.Invoke("Another import is already in progress.");
                return;
            }

            StartCoroutine(ImportFolderCoroutine(folderPath));
        }

        /// <summary>
        /// Imports a single model file and creates a basic manifest.
        /// </summary>
        public void ImportModelFile(string modelPath, string engineName)
        {
            if (IsImporting)
            {
                OnImportFailed?.Invoke("Another import is already in progress.");
                return;
            }

            StartCoroutine(ImportSingleModelCoroutine(modelPath, engineName));
        }

        private IEnumerator ImportZipCoroutine(string zipPath)
        {
            IsImporting = true;
            Progress = 0f;
            OnImportStarted?.Invoke(Path.GetFileName(zipPath));

            // Validate file exists and size
            if (!File.Exists(zipPath))
            {
                FailImport("File not found: " + zipPath);
                yield break;
            }

            FileInfo fileInfo = new FileInfo(zipPath);
            if (fileInfo.Length > maxFileSizeMB * 1024 * 1024)
            {
                FailImport($"File too large. Maximum size is {maxFileSizeMB}MB.");
                yield break;
            }

            Progress = 0.1f;
            OnImportProgress?.Invoke(Progress);
            yield return null;

            // Create temp extraction directory
            string tempDir = Path.Combine(Application.temporaryCachePath, "import_" + Guid.NewGuid().ToString("N"));

            try
            {
                // Extract zip
                Directory.CreateDirectory(tempDir);
                ZipFile.ExtractToDirectory(zipPath, tempDir);

                Progress = 0.4f;
                OnImportProgress?.Invoke(Progress);
                yield return null;

                // Find manifest
                string manifestPath = FindFile(tempDir, "engine.json");
                if (string.IsNullOrEmpty(manifestPath))
                {
                    FailImport("Package missing engine.json manifest file.");
                    CleanupTempDir(tempDir);
                    yield break;
                }

                // Parse manifest
                string manifestJson = File.ReadAllText(manifestPath);
                EngineManifest manifest = JsonUtility.FromJson<EngineManifest>(manifestJson);

                if (string.IsNullOrEmpty(manifest?.id))
                {
                    FailImport("Invalid manifest: missing engine ID.");
                    CleanupTempDir(tempDir);
                    yield break;
                }

                Progress = 0.5f;
                OnImportProgress?.Invoke(Progress);
                yield return null;

                // Validate model file exists
                string sourceModelPath = Path.Combine(Path.GetDirectoryName(manifestPath), manifest.modelFile);
                if (!File.Exists(sourceModelPath))
                {
                    // Try finding model in temp directory
                    sourceModelPath = FindModelFile(tempDir);
                    if (string.IsNullOrEmpty(sourceModelPath))
                    {
                        FailImport("Package missing model file: " + manifest.modelFile);
                        CleanupTempDir(tempDir);
                        yield break;
                    }
                    manifest.modelFile = Path.GetFileName(sourceModelPath);
                }

                Progress = 0.6f;
                OnImportProgress?.Invoke(Progress);
                yield return null;

                // Create destination directory
                string destDir = Path.Combine(enginesDirectory, manifest.id);
                if (Directory.Exists(destDir))
                {
                    // Engine already exists - ask user or overwrite
                    Directory.Delete(destDir, true);
                }
                Directory.CreateDirectory(destDir);
                Directory.CreateDirectory(Path.Combine(destDir, "procedures"));

                Progress = 0.7f;
                OnImportProgress?.Invoke(Progress);
                yield return null;

                // Copy files
                CopyDirectory(Path.GetDirectoryName(manifestPath), destDir);

                Progress = 0.9f;
                OnImportProgress?.Invoke(Progress);
                yield return null;

                // Update manifest with new path
                manifest.BasePath = destDir;

                // Clean up temp
                CleanupTempDir(tempDir);

                Progress = 1f;
                OnImportProgress?.Invoke(Progress);
                IsImporting = false;

                OnImportCompleted?.Invoke(manifest);
            }
            catch (Exception e)
            {
                CleanupTempDir(tempDir);
                FailImport("Import failed: " + e.Message);
            }
        }

        private IEnumerator ImportFolderCoroutine(string folderPath)
        {
            IsImporting = true;
            Progress = 0f;
            OnImportStarted?.Invoke(Path.GetFileName(folderPath));

            if (!Directory.Exists(folderPath))
            {
                FailImport("Folder not found: " + folderPath);
                yield break;
            }

            Progress = 0.2f;
            OnImportProgress?.Invoke(Progress);
            yield return null;

            // Find manifest
            string manifestPath = Path.Combine(folderPath, "engine.json");
            if (!File.Exists(manifestPath))
            {
                FailImport("Folder missing engine.json manifest file.");
                yield break;
            }

            // Parse manifest
            string manifestJson = File.ReadAllText(manifestPath);
            EngineManifest manifest = JsonUtility.FromJson<EngineManifest>(manifestJson);

            if (string.IsNullOrEmpty(manifest?.id))
            {
                FailImport("Invalid manifest: missing engine ID.");
                yield break;
            }

            Progress = 0.4f;
            OnImportProgress?.Invoke(Progress);
            yield return null;

            // Validate model file
            string modelPath = Path.Combine(folderPath, manifest.modelFile);
            if (!File.Exists(modelPath))
            {
                FailImport("Folder missing model file: " + manifest.modelFile);
                yield break;
            }

            Progress = 0.5f;
            OnImportProgress?.Invoke(Progress);
            yield return null;

            // Copy to engines directory
            string destDir = Path.Combine(enginesDirectory, manifest.id);
            if (Directory.Exists(destDir))
            {
                Directory.Delete(destDir, true);
            }

            CopyDirectory(folderPath, destDir);

            Progress = 0.9f;
            OnImportProgress?.Invoke(Progress);
            yield return null;

            manifest.BasePath = destDir;

            Progress = 1f;
            OnImportProgress?.Invoke(Progress);
            IsImporting = false;

            OnImportCompleted?.Invoke(manifest);
        }

        private IEnumerator ImportSingleModelCoroutine(string modelPath, string engineName)
        {
            IsImporting = true;
            Progress = 0f;
            OnImportStarted?.Invoke(Path.GetFileName(modelPath));

            if (!File.Exists(modelPath))
            {
                FailImport("Model file not found: " + modelPath);
                yield break;
            }

            // Validate format
            string extension = Path.GetExtension(modelPath).ToLower();
            if (!IsSupportedFormat(extension))
            {
                FailImport($"Unsupported format: {extension}. Supported: {string.Join(", ", supportedModelFormats)}");
                yield break;
            }

            Progress = 0.2f;
            OnImportProgress?.Invoke(Progress);
            yield return null;

            // Generate engine ID from name
            string engineId = SanitizeEngineId(engineName);

            // Create engine directory
            string destDir = Path.Combine(enginesDirectory, engineId);
            if (Directory.Exists(destDir))
            {
                // Add suffix to make unique
                engineId += "_" + DateTime.Now.ToString("yyyyMMddHHmmss");
                destDir = Path.Combine(enginesDirectory, engineId);
            }

            Directory.CreateDirectory(destDir);
            Directory.CreateDirectory(Path.Combine(destDir, "procedures"));

            Progress = 0.4f;
            OnImportProgress?.Invoke(Progress);
            yield return null;

            // Copy model file
            string modelFileName = Path.GetFileName(modelPath);
            string destModelPath = Path.Combine(destDir, modelFileName);
            File.Copy(modelPath, destModelPath);

            Progress = 0.7f;
            OnImportProgress?.Invoke(Progress);
            yield return null;

            // Create basic manifest
            EngineManifest manifest = new EngineManifest
            {
                id = engineId,
                name = engineName,
                modelFile = modelFileName,
                partMappings = new PartMapping[0],
                defaultAlignment = new DefaultAlignment
                {
                    position = new float[] { 0, 0, 0.5f },
                    rotation = new float[] { 0, 180, 0 },
                    scale = new float[] { 1, 1, 1 }
                }
            };

            string manifestJson = JsonUtility.ToJson(manifest, true);
            File.WriteAllText(Path.Combine(destDir, "engine.json"), manifestJson);

            Progress = 0.9f;
            OnImportProgress?.Invoke(Progress);
            yield return null;

            manifest.BasePath = destDir;

            Progress = 1f;
            OnImportProgress?.Invoke(Progress);
            IsImporting = false;

            OnImportCompleted?.Invoke(manifest);
        }

        private void FailImport(string message)
        {
            Debug.LogError("Engine import failed: " + message);
            IsImporting = false;
            Progress = 0f;
            OnImportFailed?.Invoke(message);
        }

        private string FindFile(string directory, string fileName)
        {
            string[] files = Directory.GetFiles(directory, fileName, SearchOption.AllDirectories);
            return files.Length > 0 ? files[0] : null;
        }

        private string FindModelFile(string directory)
        {
            foreach (string format in supportedModelFormats)
            {
                string[] files = Directory.GetFiles(directory, "*" + format, SearchOption.AllDirectories);
                if (files.Length > 0)
                {
                    return files[0];
                }
            }
            return null;
        }

        private bool IsSupportedFormat(string extension)
        {
            foreach (string format in supportedModelFormats)
            {
                if (format.Equals(extension, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        }

        private string SanitizeEngineId(string name)
        {
            // Remove invalid characters and convert to lowercase
            string id = name.ToLower();
            id = System.Text.RegularExpressions.Regex.Replace(id, @"[^a-z0-9_]", "_");
            id = System.Text.RegularExpressions.Regex.Replace(id, @"_+", "_");
            id = id.Trim('_');

            if (string.IsNullOrEmpty(id))
            {
                id = "engine_" + Guid.NewGuid().ToString("N").Substring(0, 8);
            }

            return id;
        }

        private void CopyDirectory(string sourceDir, string destDir)
        {
            Directory.CreateDirectory(destDir);

            foreach (string file in Directory.GetFiles(sourceDir))
            {
                string destFile = Path.Combine(destDir, Path.GetFileName(file));
                File.Copy(file, destFile, true);
            }

            foreach (string subDir in Directory.GetDirectories(sourceDir))
            {
                string destSubDir = Path.Combine(destDir, Path.GetFileName(subDir));
                CopyDirectory(subDir, destSubDir);
            }
        }

        private void CleanupTempDir(string tempDir)
        {
            try
            {
                if (Directory.Exists(tempDir))
                {
                    Directory.Delete(tempDir, true);
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("Failed to clean up temp directory: " + e.Message);
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
        /// Gets the path where engine packages should be placed for import.
        /// </summary>
        public string GetImportDirectory()
        {
            string importDir = Path.Combine(Application.persistentDataPath, "import");
            EnsureDirectoryExists(importDir);
            return importDir;
        }

        /// <summary>
        /// Checks the import directory for any new packages and returns their paths.
        /// </summary>
        public string[] ScanForImportablePackages()
        {
            string importDir = GetImportDirectory();
            var zipFiles = Directory.GetFiles(importDir, "*.zip");
            return zipFiles;
        }

        /// <summary>
        /// Deletes an imported engine package.
        /// </summary>
        public bool DeleteEngine(string engineId)
        {
            string engineDir = Path.Combine(enginesDirectory, engineId);
            if (Directory.Exists(engineDir))
            {
                try
                {
                    Directory.Delete(engineDir, true);
                    return true;
                }
                catch (Exception e)
                {
                    Debug.LogError("Failed to delete engine: " + e.Message);
                }
            }
            return false;
        }

        /// <summary>
        /// Gets the total size of all imported engines.
        /// </summary>
        public long GetTotalEngineStorageSize()
        {
            if (!Directory.Exists(enginesDirectory)) return 0;

            long totalSize = 0;
            foreach (string file in Directory.GetFiles(enginesDirectory, "*", SearchOption.AllDirectories))
            {
                totalSize += new FileInfo(file).Length;
            }
            return totalSize;
        }
    }
}
