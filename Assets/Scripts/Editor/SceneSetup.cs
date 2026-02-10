#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using TMPro;

namespace MechanicScope.Editor
{
    /// <summary>
    /// One-time scene builder. Creates the main AR scene with all required
    /// GameObjects, components, and UI wiring for the Mechanic Scope app.
    /// Run via: MechanicScope > Setup Main Scene
    /// </summary>
    public static class SceneSetup
    {
        [MenuItem("MechanicScope/Setup Main Scene")]
        public static void CreateMainScene()
        {
            // Create a new empty scene
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // === AR Infrastructure ===
            // AR Session
            var arSessionGO = new GameObject("AR Session");
            arSessionGO.AddComponent<UnityEngine.XR.ARFoundation.ARSession>();
            arSessionGO.AddComponent<UnityEngine.XR.ARFoundation.ARInputManager>();

            // AR Session Origin + Camera
            var arOriginGO = new GameObject("AR Session Origin");
            var arOrigin = arOriginGO.AddComponent<UnityEngine.XR.ARFoundation.ARSessionOrigin>();

            var arCameraGO = new GameObject("AR Camera");
            arCameraGO.transform.SetParent(arOriginGO.transform);
            arCameraGO.tag = "MainCamera";

            var camera = arCameraGO.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 20f;

            arCameraGO.AddComponent<UnityEngine.XR.ARFoundation.ARCameraManager>();
            arCameraGO.AddComponent<UnityEngine.XR.ARFoundation.ARCameraBackground>();
            arCameraGO.AddComponent<AudioListener>();
            arOrigin.camera = camera;

            // AR Raycast Manager (needed for part tapping)
            arOriginGO.AddComponent<UnityEngine.XR.ARFoundation.ARRaycastManager>();

            // AR Plane Manager (for surface detection)
            arOriginGO.AddComponent<UnityEngine.XR.ARFoundation.ARPlaneManager>();

            // === Directional Light ===
            var lightGO = new GameObject("Directional Light");
            var light = lightGO.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(1f, 0.957f, 0.839f);
            light.intensity = 1f;
            light.shadows = LightShadows.Soft;
            lightGO.transform.rotation = Quaternion.Euler(50, -30, 0);

            // === Core Systems Manager ===
            var managersGO = new GameObject("--- MANAGERS ---");

            // AppInitializer
            var initGO = new GameObject("AppInitializer");
            initGO.transform.SetParent(managersGO.transform);
            initGO.AddComponent<Core.AppInitializer>();

            // EngineModelLoader
            var modelLoaderGO = new GameObject("EngineModelLoader");
            modelLoaderGO.transform.SetParent(managersGO.transform);
            var modelLoader = modelLoaderGO.AddComponent<Core.EngineModelLoader>();

            // ProcedureRunner
            var procedureRunnerGO = new GameObject("ProcedureRunner");
            procedureRunnerGO.transform.SetParent(managersGO.transform);
            var procedureRunner = procedureRunnerGO.AddComponent<Core.ProcedureRunner>();

            // PartDatabase
            var partDbGO = new GameObject("PartDatabase");
            partDbGO.transform.SetParent(managersGO.transform);
            var partDatabase = partDbGO.AddComponent<Core.PartDatabase>();

            // ProgressTracker
            var progressGO = new GameObject("ProgressTracker");
            progressGO.transform.SetParent(managersGO.transform);
            var progressTracker = progressGO.AddComponent<Core.ProgressTracker>();

            // ARAlignment (on AR Session Origin for access to AR components)
            var arAlignment = arOriginGO.AddComponent<Core.ARAlignment>();

            // HighlightController
            var highlightGO = new GameObject("HighlightController");
            highlightGO.transform.SetParent(managersGO.transform);
            highlightGO.AddComponent<Core.HighlightController>();

            // === UI Canvas ===
            var canvasGO = new GameObject("UI Canvas");
            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            var canvasScaler = canvasGO.AddComponent<CanvasScaler>();
            canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvasScaler.referenceResolution = new Vector2(1080, 1920);
            canvasScaler.matchWidthOrHeight = 0.5f;
            canvasGO.AddComponent<GraphicRaycaster>();

            // EventSystem
            var eventSystemGO = new GameObject("EventSystem");
            eventSystemGO.AddComponent<UnityEngine.EventSystems.EventSystem>();
            eventSystemGO.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();

            // === Header ===
            var headerGO = CreatePanel(canvasGO.transform, "Header", new Vector2(0, 1), new Vector2(1, 1),
                new Vector2(0, -80), new Vector2(0, 0), new Color(0.15f, 0.15f, 0.2f, 0.95f));
            var headerRect = headerGO.GetComponent<RectTransform>();
            headerRect.anchoredPosition = new Vector2(0, 0);
            headerRect.sizeDelta = new Vector2(0, 80);

            // Header title
            var titleGO = CreateTextElement(headerGO.transform, "HeaderTitle", "Mechanic Scope",
                new Vector2(0.15f, 0), new Vector2(0.85f, 1), 28, TextAlignmentOptions.Center);

            // Menu button
            var menuBtnGO = CreateButton(headerGO.transform, "MenuButton", "\u2630",
                new Vector2(0, 0), new Vector2(0.15f, 1));

            // Settings button
            var settingsBtnGO = CreateButton(headerGO.transform, "SettingsButton", "\u2699",
                new Vector2(0.85f, 0), new Vector2(1, 1));

            // === Screen Panels ===
            var splashScreen = CreateScreenPanel(canvasGO.transform, "SplashScreen");
            var engineSelectionScreen = CreateScreenPanel(canvasGO.transform, "EngineSelectionScreen");
            var alignmentScreen = CreateScreenPanel(canvasGO.transform, "AlignmentScreen");
            var procedureSelectionScreen = CreateScreenPanel(canvasGO.transform, "ProcedureSelectionScreen");
            var procedureActiveScreen = CreateScreenPanel(canvasGO.transform, "ProcedureActiveScreen");
            var partInspectorScreen = CreateScreenPanel(canvasGO.transform, "PartInspectorScreen");
            var settingsScreen = CreateScreenPanel(canvasGO.transform, "SettingsScreen");
            var completionScreen = CreateScreenPanel(canvasGO.transform, "CompletionScreen");

            // Add placeholder labels to each screen
            CreateTextElement(splashScreen.transform, "SplashLabel", "MECHANIC SCOPE",
                new Vector2(0.1f, 0.3f), new Vector2(0.9f, 0.7f), 48, TextAlignmentOptions.Center);
            CreateTextElement(engineSelectionScreen.transform, "Hint", "Select an engine to begin",
                new Vector2(0.1f, 0.4f), new Vector2(0.9f, 0.6f), 24, TextAlignmentOptions.Center);
            CreateTextElement(completionScreen.transform, "CompleteLabel", "Repair Complete!",
                new Vector2(0.1f, 0.3f), new Vector2(0.9f, 0.7f), 36, TextAlignmentOptions.Center);

            // === Add UI Component Scripts ===
            var engineSelectionUI = engineSelectionScreen.AddComponent<UI.EngineSelectionUI>();
            var procedureSelectionUI = procedureSelectionScreen.AddComponent<UI.ProcedureSelectionUI>();
            var procedureCardUI = procedureActiveScreen.AddComponent<UI.ProcedureCardUI>();
            var partInfoPopup = partInspectorScreen.AddComponent<UI.PartInfoPopup>();
            var alignmentControlsUI = alignmentScreen.AddComponent<UI.AlignmentControlsUI>();
            var settingsUI = settingsScreen.AddComponent<UI.SettingsUI>();
            var completionSummaryUI = completionScreen.AddComponent<UI.CompletionSummaryUI>();

            // === MainUIController ===
            var mainUIGO = new GameObject("MainUIController");
            mainUIGO.transform.SetParent(canvasGO.transform);
            var mainUI = mainUIGO.AddComponent<UI.MainUIController>();

            // Wire references via SerializedObject
            var so = new SerializedObject(mainUI);
            so.FindProperty("arAlignment").objectReferenceValue = arAlignment;
            so.FindProperty("modelLoader").objectReferenceValue = modelLoader;
            so.FindProperty("procedureRunner").objectReferenceValue = procedureRunner;
            so.FindProperty("partDatabase").objectReferenceValue = partDatabase;
            so.FindProperty("progressTracker").objectReferenceValue = progressTracker;
            so.FindProperty("engineSelection").objectReferenceValue = engineSelectionUI;
            so.FindProperty("procedureSelection").objectReferenceValue = procedureSelectionUI;
            so.FindProperty("procedureCard").objectReferenceValue = procedureCardUI;
            so.FindProperty("partInfoPopup").objectReferenceValue = partInfoPopup;
            so.FindProperty("alignmentControls").objectReferenceValue = alignmentControlsUI;
            so.FindProperty("splashScreen").objectReferenceValue = splashScreen;
            so.FindProperty("engineSelectionScreen").objectReferenceValue = engineSelectionScreen;
            so.FindProperty("alignmentScreen").objectReferenceValue = alignmentScreen;
            so.FindProperty("procedureSelectionScreen").objectReferenceValue = procedureSelectionScreen;
            so.FindProperty("procedureActiveScreen").objectReferenceValue = procedureActiveScreen;
            so.FindProperty("partInspectorScreen").objectReferenceValue = partInspectorScreen;
            so.FindProperty("settingsScreen").objectReferenceValue = settingsScreen;
            so.FindProperty("completionScreen").objectReferenceValue = completionScreen;
            so.FindProperty("header").objectReferenceValue = headerGO;
            so.FindProperty("menuButton").objectReferenceValue = menuBtnGO.GetComponent<Button>();
            so.FindProperty("settingsButton").objectReferenceValue = settingsBtnGO.GetComponent<Button>();
            so.FindProperty("headerTitle").objectReferenceValue = titleGO.GetComponent<TextMeshProUGUI>();
            so.ApplyModifiedPropertiesWithoutUndo();

            // Wire sub-UI component references
            WireSerializedField(engineSelectionUI, "modelLoader", modelLoader);
            WireSerializedField(procedureSelectionUI, "procedureRunner", procedureRunner);
            WireSerializedField(procedureSelectionUI, "progressTracker", progressTracker);
            WireSerializedField(procedureCardUI, "procedureRunner", procedureRunner);
            WireSerializedField(partInfoPopup, "arAlignment", arAlignment);
            WireSerializedField(partInfoPopup, "partDatabase", partDatabase);
            WireSerializedField(alignmentControlsUI, "arAlignment", arAlignment);
            WireSerializedField(completionSummaryUI, "procedureRunner", procedureRunner);
            WireSerializedField(completionSummaryUI, "modelLoader", modelLoader);

            // Wire ARAlignment references
            var arSO = new SerializedObject(arAlignment);
            arSO.FindProperty("arSession").objectReferenceValue = arSessionGO.GetComponent<UnityEngine.XR.ARFoundation.ARSession>();
            arSO.FindProperty("arSessionOrigin").objectReferenceValue = arOrigin;
            arSO.FindProperty("raycastManager").objectReferenceValue = arOriginGO.GetComponent<UnityEngine.XR.ARFoundation.ARRaycastManager>();
            arSO.FindProperty("arCamera").objectReferenceValue = camera;
            arSO.ApplyModifiedPropertiesWithoutUndo();

            // === Save Scene ===
            string scenePath = "Assets/Scenes/MainScene.unity";
            if (!System.IO.Directory.Exists("Assets/Scenes"))
            {
                System.IO.Directory.CreateDirectory("Assets/Scenes");
            }
            EditorSceneManager.SaveScene(scene, scenePath);
            Debug.Log($"[SceneSetup] Main scene created and saved to {scenePath}");
            Debug.Log("[SceneSetup] Next: assign materials to EngineModelLoader (defaultMaterial, highlightMaterial)");

            Selection.activeGameObject = mainUIGO;
        }

        // === Helper Methods ===

        private static void WireSerializedField(Component component, string fieldName, UnityEngine.Object value)
        {
            var so = new SerializedObject(component);
            var prop = so.FindProperty(fieldName);
            if (prop != null)
            {
                prop.objectReferenceValue = value;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            else
            {
                Debug.LogWarning($"[SceneSetup] Could not find field '{fieldName}' on {component.GetType().Name}");
            }
        }

        private static GameObject CreateScreenPanel(Transform parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);

            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(0, 0);
            rect.offsetMax = new Vector2(0, -80); // Leave space for header
            rect.anchoredPosition = Vector2.zero;

            var image = go.AddComponent<Image>();
            image.color = new Color(0, 0, 0, 0); // Transparent background
            image.raycastTarget = true;

            go.SetActive(false); // All screens start hidden

            return go;
        }

        private static GameObject CreatePanel(Transform parent, string name,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);

            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;

            var image = go.AddComponent<Image>();
            image.color = color;

            return go;
        }

        private static GameObject CreateTextElement(Transform parent, string name, string text,
            Vector2 anchorMin, Vector2 anchorMax, float fontSize, TextAlignmentOptions alignment)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);

            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.alignment = alignment;
            tmp.color = Color.white;

            return go;
        }

        private static GameObject CreateButton(Transform parent, string name, string label,
            Vector2 anchorMin, Vector2 anchorMax)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);

            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var image = go.AddComponent<Image>();
            image.color = new Color(1, 1, 1, 0); // Transparent, just a hit target

            var button = go.AddComponent<Button>();
            button.targetGraphic = image;

            // Button label
            var labelGO = new GameObject("Label");
            labelGO.transform.SetParent(go.transform, false);

            var labelRect = labelGO.AddComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;

            var tmp = labelGO.AddComponent<TextMeshProUGUI>();
            tmp.text = label;
            tmp.fontSize = 32;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.white;

            return go;
        }
    }
}
#endif
