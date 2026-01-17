using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using UnityEngine;

namespace MechanicScope.Core
{
    /// <summary>
    /// Handles exporting and importing procedure packages for community sharing.
    /// Creates portable .msproc (ZIP) files containing procedure JSON and optional media.
    /// </summary>
    public class ProcedureSharing : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField] private string packageExtension = ".msproc";
        [SerializeField] private bool includeMediaInExport = true;
        [SerializeField] private int packageVersion = 1;

        // Events
        public event Action<string> OnExportCompleted;
        public event Action<string> OnExportFailed;
        public event Action<Procedure> OnImportCompleted;
        public event Action<string> OnImportFailed;

        private string exportDirectory;
        private string importDirectory;

        private void Awake()
        {
            exportDirectory = Path.Combine(Application.persistentDataPath, "exports");
            importDirectory = Path.Combine(Application.persistentDataPath, "imports");

            EnsureDirectoryExists(exportDirectory);
            EnsureDirectoryExists(importDirectory);
        }

        /// <summary>
        /// Exports a procedure to a shareable package file.
        /// </summary>
        public string ExportProcedure(Procedure procedure, string engineId, bool includeMedia = true)
        {
            try
            {
                // Create package info
                var packageInfo = new ProcedurePackageInfo
                {
                    version = packageVersion,
                    procedureId = procedure.id,
                    procedureName = procedure.name,
                    engineId = engineId,
                    exportDate = DateTime.UtcNow.ToString("o"),
                    author = "", // Could be set from user profile
                    description = procedure.description
                };

                // Create temp directory for packaging
                string tempDir = Path.Combine(Application.temporaryCachePath, "export_" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(tempDir);

                try
                {
                    // Write procedure JSON
                    string procedureJson = JsonUtility.ToJson(procedure, true);
                    File.WriteAllText(Path.Combine(tempDir, "procedure.json"), procedureJson);

                    // Write package info
                    string infoJson = JsonUtility.ToJson(packageInfo, true);
                    File.WriteAllText(Path.Combine(tempDir, "package.json"), infoJson);

                    // Copy media files if requested
                    if (includeMedia && includeMediaInExport)
                    {
                        CopyProcedureMedia(procedure, engineId, tempDir);
                    }

                    // Create ZIP package
                    string fileName = SanitizeFileName($"{procedure.name}_{DateTime.Now:yyyyMMdd}") + packageExtension;
                    string outputPath = Path.Combine(exportDirectory, fileName);

                    if (File.Exists(outputPath))
                    {
                        File.Delete(outputPath);
                    }

                    ZipFile.CreateFromDirectory(tempDir, outputPath);

                    OnExportCompleted?.Invoke(outputPath);
                    return outputPath;
                }
                finally
                {
                    // Cleanup temp directory
                    if (Directory.Exists(tempDir))
                    {
                        Directory.Delete(tempDir, true);
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to export procedure: {e.Message}");
                OnExportFailed?.Invoke(e.Message);
                return null;
            }
        }

        /// <summary>
        /// Imports a procedure from a package file.
        /// </summary>
        public Procedure ImportProcedure(string packagePath, string targetEngineId = null)
        {
            try
            {
                if (!File.Exists(packagePath))
                {
                    throw new FileNotFoundException("Package file not found", packagePath);
                }

                // Create temp directory for extraction
                string tempDir = Path.Combine(Application.temporaryCachePath, "import_" + Guid.NewGuid().ToString("N"));

                try
                {
                    // Extract package
                    ZipFile.ExtractToDirectory(packagePath, tempDir);

                    // Read package info
                    string infoPath = Path.Combine(tempDir, "package.json");
                    ProcedurePackageInfo packageInfo = null;

                    if (File.Exists(infoPath))
                    {
                        string infoJson = File.ReadAllText(infoPath);
                        packageInfo = JsonUtility.FromJson<ProcedurePackageInfo>(infoJson);
                    }

                    // Read procedure
                    string procedurePath = Path.Combine(tempDir, "procedure.json");
                    if (!File.Exists(procedurePath))
                    {
                        throw new Exception("Package missing procedure.json");
                    }

                    string procedureJson = File.ReadAllText(procedurePath);
                    Procedure procedure = JsonUtility.FromJson<Procedure>(procedureJson);

                    // Update engine ID if specified
                    if (!string.IsNullOrEmpty(targetEngineId))
                    {
                        procedure.engineId = targetEngineId;
                    }

                    // Copy media files to engine directory
                    string engineId = targetEngineId ?? packageInfo?.engineId ?? procedure.engineId;
                    if (!string.IsNullOrEmpty(engineId))
                    {
                        CopyImportedMedia(tempDir, engineId, procedure.id);
                    }

                    // Save procedure to engine's procedures folder
                    SaveImportedProcedure(procedure, engineId);

                    OnImportCompleted?.Invoke(procedure);
                    return procedure;
                }
                finally
                {
                    // Cleanup temp directory
                    if (Directory.Exists(tempDir))
                    {
                        Directory.Delete(tempDir, true);
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to import procedure: {e.Message}");
                OnImportFailed?.Invoke(e.Message);
                return null;
            }
        }

        /// <summary>
        /// Generates a shareable link/code for a procedure.
        /// In a real implementation, this would upload to a server.
        /// </summary>
        public string GenerateShareCode(Procedure procedure)
        {
            // Simple implementation: base64 encode the procedure JSON
            string json = JsonUtility.ToJson(procedure);
            byte[] bytes = Encoding.UTF8.GetBytes(json);
            string base64 = Convert.ToBase64String(bytes);

            // Add version prefix
            return $"MS1:{base64}";
        }

        /// <summary>
        /// Imports a procedure from a share code.
        /// </summary>
        public Procedure ImportFromShareCode(string shareCode)
        {
            try
            {
                if (!shareCode.StartsWith("MS1:"))
                {
                    throw new Exception("Invalid share code format");
                }

                string base64 = shareCode.Substring(4);
                byte[] bytes = Convert.FromBase64String(base64);
                string json = Encoding.UTF8.GetString(bytes);

                Procedure procedure = JsonUtility.FromJson<Procedure>(json);
                return procedure;
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to import from share code: {e.Message}");
                OnImportFailed?.Invoke(e.Message);
                return null;
            }
        }

        /// <summary>
        /// Gets the export directory path.
        /// </summary>
        public string GetExportDirectory()
        {
            return exportDirectory;
        }

        /// <summary>
        /// Gets all exported package files.
        /// </summary>
        public string[] GetExportedPackages()
        {
            return Directory.GetFiles(exportDirectory, "*" + packageExtension);
        }

        /// <summary>
        /// Reads package info without fully importing.
        /// </summary>
        public ProcedurePackageInfo ReadPackageInfo(string packagePath)
        {
            try
            {
                using (var zip = ZipFile.OpenRead(packagePath))
                {
                    var infoEntry = zip.GetEntry("package.json");
                    if (infoEntry == null) return null;

                    using (var stream = infoEntry.Open())
                    using (var reader = new StreamReader(stream))
                    {
                        string json = reader.ReadToEnd();
                        return JsonUtility.FromJson<ProcedurePackageInfo>(json);
                    }
                }
            }
            catch
            {
                return null;
            }
        }

        private void CopyProcedureMedia(Procedure procedure, string engineId, string destDir)
        {
            if (procedure.steps == null) return;

            string mediaDestDir = Path.Combine(destDir, "media");
            Directory.CreateDirectory(mediaDestDir);

            foreach (var step in procedure.steps)
            {
                if (step.media?.image != null)
                {
                    CopyMediaFile(engineId, procedure.id, step.media.image, mediaDestDir);
                }
                if (step.media?.video != null)
                {
                    CopyMediaFile(engineId, procedure.id, step.media.video, mediaDestDir);
                }
            }
        }

        private void CopyMediaFile(string engineId, string procedureId, string mediaPath, string destDir)
        {
            // Try to find the media file in various locations
            string[] searchPaths = new[]
            {
                Path.Combine(Application.persistentDataPath, "engines", engineId, "procedures", "media", mediaPath),
                Path.Combine(Application.persistentDataPath, "engines", engineId, "media", mediaPath),
                Path.Combine(Application.streamingAssetsPath, "Engines", engineId, "procedures", "media", mediaPath)
            };

            foreach (string sourcePath in searchPaths)
            {
                if (File.Exists(sourcePath))
                {
                    string destPath = Path.Combine(destDir, Path.GetFileName(mediaPath));
                    File.Copy(sourcePath, destPath, true);
                    return;
                }
            }
        }

        private void CopyImportedMedia(string tempDir, string engineId, string procedureId)
        {
            string mediaSourceDir = Path.Combine(tempDir, "media");
            if (!Directory.Exists(mediaSourceDir)) return;

            string mediaDestDir = Path.Combine(Application.persistentDataPath, "engines", engineId, "procedures", "media");
            EnsureDirectoryExists(mediaDestDir);

            foreach (string file in Directory.GetFiles(mediaSourceDir))
            {
                string destPath = Path.Combine(mediaDestDir, Path.GetFileName(file));
                File.Copy(file, destPath, true);
            }
        }

        private void SaveImportedProcedure(Procedure procedure, string engineId)
        {
            if (string.IsNullOrEmpty(engineId)) return;

            string proceduresDir = Path.Combine(Application.persistentDataPath, "engines", engineId, "procedures");
            EnsureDirectoryExists(proceduresDir);

            string filePath = Path.Combine(proceduresDir, $"{procedure.id}.json");
            string json = JsonUtility.ToJson(procedure, true);
            File.WriteAllText(filePath, json);
        }

        private string SanitizeFileName(string name)
        {
            char[] invalid = Path.GetInvalidFileNameChars();
            foreach (char c in invalid)
            {
                name = name.Replace(c, '_');
            }
            return name;
        }

        private void EnsureDirectoryExists(string path)
        {
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }
        }
    }

    /// <summary>
    /// Metadata for procedure packages.
    /// </summary>
    [Serializable]
    public class ProcedurePackageInfo
    {
        public int version;
        public string procedureId;
        public string procedureName;
        public string engineId;
        public string exportDate;
        public string author;
        public string description;
        public string[] tags;
    }
}
