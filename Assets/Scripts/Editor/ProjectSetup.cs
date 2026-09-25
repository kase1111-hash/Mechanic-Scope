#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.XR.Management;
using UnityEditor.XR.Management.Metadata;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.Management;

namespace MechanicScope.Editor
{
    /// <summary>
    /// One-time project configuration for AR, as Editor menu items:
    ///
    ///   MechanicScope > Configure Project for AR   applies everything below (safe to re-run)
    ///   MechanicScope > Check AR Configuration     reports what is still missing, changes nothing
    ///
    /// These are settings Unity stores as generated assets (render pipeline, XR loaders) or
    /// PlayerSettings, which are fragile to hand-write, so they are created through Unity's APIs:
    ///   1. URP: a pipeline asset whose renderer has AR Foundation's background feature. The
    ///      highlight shader needs URP, and under URP the camera feed is only drawn by that feature.
    ///   2. XR Plug-in Management: ARCore enabled for Android and ARKit for iOS. Without a loader
    ///      the AR session never starts and the camera stays black.
    ///   3. iOS camera usage description, required by ARKit's build checks and by iOS itself.
    ///   4. Android graphics API: OpenGL ES 3 only (the ARCore plugin does not support Vulkan).
    /// </summary>
    public static class ProjectSetup
    {
        private const string SettingsFolder = "Assets/Settings";
        private const string RendererPath = SettingsFolder + "/MechanicScope_Renderer.asset";
        private const string PipelinePath = SettingsFolder + "/MechanicScope_URP.asset";
        private const string XRFolder = "Assets/XR";
        private const string XRSettingsPath = XRFolder + "/XRGeneralSettingsPerBuildTarget.asset";

        private const string ARCoreLoader = "UnityEngine.XR.ARCore.ARCoreLoader";
        private const string ARKitLoader = "UnityEngine.XR.ARKit.ARKitLoader";

        public const string CameraUsage =
            "Mechanic Scope uses the camera to overlay part names and repair steps on your engine.";

        [MenuItem("MechanicScope/Configure Project for AR", priority = 0)]
        public static void ConfigureProjectForAR()
        {
            ConfigureRenderPipeline();
            ConfigureXRLoaders();
            ConfigurePlayerSettings();
            AssetDatabase.SaveAssets();

            List<string> problems = FindProblems();
            if (problems.Count == 0)
            {
                Debug.Log("[ProjectSetup] Project configured for AR. Next: MechanicScope > Setup Main Scene.");
            }
            else
            {
                Debug.LogWarning("[ProjectSetup] Configured, but some items still need attention:\n- " +
                                 string.Join("\n- ", problems));
            }
        }

        [MenuItem("MechanicScope/Check AR Configuration", priority = 1)]
        public static void CheckConfiguration()
        {
            List<string> problems = FindProblems();
            if (problems.Count == 0)
            {
                Debug.Log("[ProjectSetup] AR configuration looks complete.");
            }
            else
            {
                Debug.LogWarning("[ProjectSetup] AR configuration is incomplete " +
                                 "(run MechanicScope > Configure Project for AR):\n- " + string.Join("\n- ", problems));
            }
        }

        /// <summary>Everything the app needs that is not in place. Empty when fully configured.</summary>
        public static List<string> FindProblems()
        {
            var problems = new List<string>();

            var pipeline = GraphicsSettings.defaultRenderPipeline as UniversalRenderPipelineAsset;
            if (pipeline == null)
            {
                problems.Add("No URP asset is assigned in Graphics settings (the highlight shader needs URP)");
            }
            else if (!HasBackgroundFeature(pipeline))
            {
                problems.Add($"The URP renderer used by '{pipeline.name}' has no ARBackgroundRendererFeature, " +
                             "so the camera feed will be black");
            }

            for (int i = 0; i < QualitySettings.names.Length; i++)
            {
                RenderPipelineAsset levelPipeline = QualitySettings.GetRenderPipelineAssetAt(i);
                if (levelPipeline != null && levelPipeline != pipeline)
                {
                    problems.Add($"Quality level '{QualitySettings.names[i]}' overrides the render pipeline " +
                                 $"with '{levelPipeline.name}'");
                }
            }

            if (!HasLoader(BuildTargetGroup.Android, ARCoreLoader))
            {
                problems.Add("ARCore is not enabled for Android in XR Plug-in Management");
            }
            if (!HasLoader(BuildTargetGroup.iOS, ARKitLoader))
            {
                problems.Add("ARKit is not enabled for iOS in XR Plug-in Management");
            }

            if (string.IsNullOrWhiteSpace(PlayerSettings.iOS.cameraUsageDescription))
            {
                problems.Add("iOS Camera Usage Description is empty");
            }

            if (PlayerSettings.GetUseDefaultGraphicsAPIs(BuildTarget.Android) ||
                PlayerSettings.GetGraphicsAPIs(BuildTarget.Android).Contains(GraphicsDeviceType.Vulkan))
            {
                problems.Add("Android graphics APIs may include Vulkan, which the ARCore plugin does not support");
            }

            return problems;
        }

        // === 1. Render pipeline ===

        private static void ConfigureRenderPipeline()
        {
            EnsureFolder(SettingsFolder);

            var pipeline = GraphicsSettings.defaultRenderPipeline as UniversalRenderPipelineAsset;
            if (pipeline == null)
            {
                pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelinePath);
            }

            if (pipeline == null)
            {
                var rendererData = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererPath);
                if (rendererData == null)
                {
                    rendererData = ScriptableObject.CreateInstance<UniversalRendererData>();
                    AssetDatabase.CreateAsset(rendererData, RendererPath);
                }

                pipeline = UniversalRenderPipelineAsset.Create(rendererData);
                AssetDatabase.CreateAsset(pipeline, PipelinePath);
            }

            foreach (ScriptableRendererData renderer in RendererDataOf(pipeline).ToList())
            {
                AddBackgroundFeature(renderer);
            }

            GraphicsSettings.defaultRenderPipeline = pipeline;

            // Quality levels can override the default pipeline; point them all at the same one.
            int current = QualitySettings.GetQualityLevel();
            for (int i = 0; i < QualitySettings.names.Length; i++)
            {
                QualitySettings.SetQualityLevel(i, false);
                QualitySettings.renderPipeline = pipeline;
            }
            QualitySettings.SetQualityLevel(current, false);

            EditorUtility.SetDirty(pipeline);
        }

        /// <summary>
        /// The renderer list is not public on the pipeline asset, so it is read through the
        /// serialized property that the pipeline asset's inspector uses.
        /// </summary>
        private static IEnumerable<ScriptableRendererData> RendererDataOf(UniversalRenderPipelineAsset pipeline)
        {
            var so = new SerializedObject(pipeline);
            SerializedProperty list = so.FindProperty("m_RendererDataList");
            if (list == null) yield break;

            for (int i = 0; i < list.arraySize; i++)
            {
                if (list.GetArrayElementAtIndex(i).objectReferenceValue is ScriptableRendererData data)
                {
                    yield return data;
                }
            }
        }

        private static bool HasBackgroundFeature(UniversalRenderPipelineAsset pipeline)
        {
            List<ScriptableRendererData> renderers = RendererDataOf(pipeline).ToList();
            return renderers.Count > 0 &&
                   renderers.All(r => r.rendererFeatures.Any(f => f is ARBackgroundRendererFeature));
        }

        /// <summary>
        /// Adds the feature the way the renderer's inspector does: as a sub-asset, listed in
        /// m_RendererFeatures, with its local file id in m_RendererFeatureMap.
        /// </summary>
        private static void AddBackgroundFeature(ScriptableRendererData rendererData)
        {
            if (rendererData.rendererFeatures.Any(f => f is ARBackgroundRendererFeature)) return;

            var feature = ScriptableObject.CreateInstance<ARBackgroundRendererFeature>();
            feature.name = nameof(ARBackgroundRendererFeature);
            AssetDatabase.AddObjectToAsset(feature, rendererData);
            AssetDatabase.SaveAssetIfDirty(rendererData);
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(feature, out string _, out long localId);

            var so = new SerializedObject(rendererData);
            SerializedProperty features = so.FindProperty("m_RendererFeatures");
            SerializedProperty map = so.FindProperty("m_RendererFeatureMap");

            features.arraySize++;
            features.GetArrayElementAtIndex(features.arraySize - 1).objectReferenceValue = feature;
            map.arraySize++;
            map.GetArrayElementAtIndex(map.arraySize - 1).longValue = localId;
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(rendererData);
        }

        // === 2. XR Plug-in Management ===

        private static void ConfigureXRLoaders()
        {
            EnableLoader(BuildTargetGroup.Android, ARCoreLoader);
            EnableLoader(BuildTargetGroup.iOS, ARKitLoader);
        }

        private static XRGeneralSettingsPerBuildTarget GetOrCreatePerBuildTargetSettings()
        {
            if (EditorBuildSettings.TryGetConfigObject(XRGeneralSettings.k_SettingsKey,
                    out XRGeneralSettingsPerBuildTarget settings) && settings != null)
            {
                return settings;
            }

            settings = AssetDatabase.LoadAssetAtPath<XRGeneralSettingsPerBuildTarget>(XRSettingsPath);
            if (settings == null)
            {
                EnsureFolder(XRFolder);
                settings = ScriptableObject.CreateInstance<XRGeneralSettingsPerBuildTarget>();
                AssetDatabase.CreateAsset(settings, XRSettingsPath);
            }

            EditorBuildSettings.AddConfigObject(XRGeneralSettings.k_SettingsKey, settings, true);
            return settings;
        }

        private static void EnableLoader(BuildTargetGroup group, string loaderType)
        {
            XRGeneralSettingsPerBuildTarget perTarget = GetOrCreatePerBuildTargetSettings();

            if (!perTarget.HasSettingsForBuildTarget(group))
            {
                perTarget.CreateDefaultSettingsForBuildTarget(group);
            }
            if (!perTarget.HasManagerSettingsForBuildTarget(group))
            {
                perTarget.CreateDefaultManagerSettingsForBuildTarget(group);
            }

            XRGeneralSettings settings = perTarget.SettingsForBuildTarget(group);
            settings.InitManagerOnStart = true;

            if (!HasLoader(group, loaderType) &&
                !XRPackageMetadataStore.AssignLoader(settings.AssignedSettings, loaderType, group))
            {
                Debug.LogError($"[ProjectSetup] Could not enable {loaderType} for {group}. " +
                               "Is its XR plugin package installed?");
            }

            EditorUtility.SetDirty(settings);
            EditorUtility.SetDirty(perTarget);
        }

        private static bool HasLoader(BuildTargetGroup group, string loaderType)
        {
            if (!EditorBuildSettings.TryGetConfigObject(XRGeneralSettings.k_SettingsKey,
                    out XRGeneralSettingsPerBuildTarget perTarget) || perTarget == null)
            {
                return false;
            }

            XRGeneralSettings settings = perTarget.SettingsForBuildTarget(group);
            XRManagerSettings manager = settings != null ? settings.AssignedSettings : null;
            return manager != null && manager.activeLoaders.Any(l => l != null && l.GetType().FullName == loaderType);
        }

        // === 3 and 4. Player settings ===

        private static void ConfigurePlayerSettings()
        {
            if (string.IsNullOrWhiteSpace(PlayerSettings.iOS.cameraUsageDescription))
            {
                PlayerSettings.iOS.cameraUsageDescription = CameraUsage;
            }

            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { GraphicsDeviceType.OpenGLES3 });
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;

            string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
#endif
