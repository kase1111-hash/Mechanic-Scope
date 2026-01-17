#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using MechanicScope.Core;

namespace MechanicScope.Editor
{
    /// <summary>
    /// Unity Editor window for creating and editing procedure JSON files.
    /// </summary>
    public class ProcedureEditorWindow : EditorWindow
    {
        // Current procedure
        private Procedure procedure;
        private string currentFilePath;
        private bool isDirty;

        // UI State
        private Vector2 scrollPosition;
        private Vector2 stepListScroll;
        private int selectedStepIndex = -1;
        private bool showProcedureSettings = true;
        private bool showStepList = true;
        private bool showStepEditor = true;
        private bool showDependencyGraph = false;

        // Styles
        private GUIStyle headerStyle;
        private GUIStyle subHeaderStyle;
        private GUIStyle boxStyle;
        private bool stylesInitialized;

        // Step editing
        private string[] toolsText = new string[0];
        private string[] warningsText = new string[0];
        private string requiresText = "";

        [MenuItem("Mechanic Scope/Procedure Editor")]
        public static void ShowWindow()
        {
            var window = GetWindow<ProcedureEditorWindow>("Procedure Editor");
            window.minSize = new Vector2(600, 500);
            window.Show();
        }

        private void OnEnable()
        {
            if (procedure == null)
            {
                NewProcedure();
            }
        }

        private void OnGUI()
        {
            InitializeStyles();

            EditorGUILayout.BeginVertical();

            DrawToolbar();
            DrawMainContent();

            EditorGUILayout.EndVertical();

            HandleKeyboardShortcuts();
        }

        private void InitializeStyles()
        {
            if (stylesInitialized) return;

            headerStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 14,
                margin = new RectOffset(0, 0, 10, 5)
            };

            subHeaderStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 12
            };

            boxStyle = new GUIStyle("box")
            {
                padding = new RectOffset(10, 10, 10, 10),
                margin = new RectOffset(5, 5, 5, 5)
            };

            stylesInitialized = true;
        }

        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            if (GUILayout.Button("New", EditorStyles.toolbarButton, GUILayout.Width(50)))
            {
                NewProcedure();
            }

            if (GUILayout.Button("Open", EditorStyles.toolbarButton, GUILayout.Width(50)))
            {
                OpenProcedure();
            }

            if (GUILayout.Button("Save", EditorStyles.toolbarButton, GUILayout.Width(50)))
            {
                SaveProcedure();
            }

            if (GUILayout.Button("Save As", EditorStyles.toolbarButton, GUILayout.Width(60)))
            {
                SaveProcedureAs();
            }

            GUILayout.FlexibleSpace();

            // File info
            string fileName = string.IsNullOrEmpty(currentFilePath) ? "Untitled" : Path.GetFileName(currentFilePath);
            if (isDirty) fileName += " *";
            GUILayout.Label(fileName, EditorStyles.toolbarButton);

            EditorGUILayout.EndHorizontal();
        }

        private void DrawMainContent()
        {
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

            // Procedure Settings
            showProcedureSettings = EditorGUILayout.Foldout(showProcedureSettings, "Procedure Settings", true);
            if (showProcedureSettings)
            {
                DrawProcedureSettings();
            }

            EditorGUILayout.Space(10);

            // Steps List and Editor side by side
            EditorGUILayout.BeginHorizontal();

            // Steps List (left panel)
            EditorGUILayout.BeginVertical(GUILayout.Width(250));
            showStepList = EditorGUILayout.Foldout(showStepList, "Steps", true);
            if (showStepList)
            {
                DrawStepsList();
            }
            EditorGUILayout.EndVertical();

            // Step Editor (right panel)
            EditorGUILayout.BeginVertical();
            showStepEditor = EditorGUILayout.Foldout(showStepEditor, "Step Editor", true);
            if (showStepEditor && selectedStepIndex >= 0)
            {
                DrawStepEditor();
            }
            else if (showStepEditor)
            {
                EditorGUILayout.HelpBox("Select a step to edit, or add a new step.", MessageType.Info);
            }
            EditorGUILayout.EndVertical();

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(10);

            // Dependency Graph
            showDependencyGraph = EditorGUILayout.Foldout(showDependencyGraph, "Dependency Graph", true);
            if (showDependencyGraph)
            {
                DrawDependencyGraph();
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawProcedureSettings()
        {
            EditorGUILayout.BeginVertical(boxStyle);

            EditorGUI.BeginChangeCheck();

            procedure.id = EditorGUILayout.TextField("ID", procedure.id);
            procedure.name = EditorGUILayout.TextField("Name", procedure.name);
            procedure.description = EditorGUILayout.TextField("Description", procedure.description);
            procedure.engineId = EditorGUILayout.TextField("Engine ID", procedure.engineId);
            procedure.estimatedTime = EditorGUILayout.TextField("Estimated Time", procedure.estimatedTime);

            // Difficulty dropdown
            string[] difficulties = { "beginner", "intermediate", "advanced", "expert" };
            int currentDifficulty = Array.IndexOf(difficulties, procedure.difficulty);
            if (currentDifficulty < 0) currentDifficulty = 1;
            currentDifficulty = EditorGUILayout.Popup("Difficulty", currentDifficulty, difficulties);
            procedure.difficulty = difficulties[currentDifficulty];

            // Tools list
            EditorGUILayout.LabelField("Required Tools", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;

            int toolCount = procedure.tools?.Length ?? 0;
            int newToolCount = EditorGUILayout.IntField("Count", toolCount);

            if (newToolCount != toolCount)
            {
                Array.Resize(ref procedure.tools, newToolCount);
                for (int i = toolCount; i < newToolCount; i++)
                {
                    procedure.tools[i] = "";
                }
            }

            if (procedure.tools != null)
            {
                for (int i = 0; i < procedure.tools.Length; i++)
                {
                    procedure.tools[i] = EditorGUILayout.TextField($"Tool {i + 1}", procedure.tools[i]);
                }
            }

            EditorGUI.indentLevel--;

            EditorGUILayout.Space();
            procedure.reinstallNotes = EditorGUILayout.TextArea(procedure.reinstallNotes, GUILayout.Height(60));
            EditorGUILayout.LabelField("Reinstall Notes", EditorStyles.miniLabel);

            if (EditorGUI.EndChangeCheck())
            {
                isDirty = true;
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawStepsList()
        {
            EditorGUILayout.BeginVertical(boxStyle, GUILayout.MinHeight(300));

            // Add step button
            if (GUILayout.Button("+ Add Step"))
            {
                AddStep();
            }

            EditorGUILayout.Space();

            stepListScroll = EditorGUILayout.BeginScrollView(stepListScroll, GUILayout.Height(250));

            if (procedure.steps != null)
            {
                for (int i = 0; i < procedure.steps.Length; i++)
                {
                    var step = procedure.steps[i];
                    bool isSelected = i == selectedStepIndex;

                    EditorGUILayout.BeginHorizontal(isSelected ? "SelectionRect" : "box");

                    // Selection button
                    if (GUILayout.Button($"{step.id}. {TruncateText(step.action, 20)}",
                        isSelected ? EditorStyles.boldLabel : EditorStyles.label,
                        GUILayout.ExpandWidth(true)))
                    {
                        SelectStep(i);
                    }

                    // Move buttons
                    EditorGUI.BeginDisabledGroup(i == 0);
                    if (GUILayout.Button("↑", GUILayout.Width(25)))
                    {
                        MoveStep(i, i - 1);
                    }
                    EditorGUI.EndDisabledGroup();

                    EditorGUI.BeginDisabledGroup(i == procedure.steps.Length - 1);
                    if (GUILayout.Button("↓", GUILayout.Width(25)))
                    {
                        MoveStep(i, i + 1);
                    }
                    EditorGUI.EndDisabledGroup();

                    // Delete button
                    if (GUILayout.Button("×", GUILayout.Width(25)))
                    {
                        DeleteStep(i);
                    }

                    EditorGUILayout.EndHorizontal();
                }
            }

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        private void DrawStepEditor()
        {
            if (procedure.steps == null || selectedStepIndex < 0 || selectedStepIndex >= procedure.steps.Length)
                return;

            var step = procedure.steps[selectedStepIndex];

            EditorGUILayout.BeginVertical(boxStyle);

            EditorGUI.BeginChangeCheck();

            EditorGUILayout.LabelField($"Step {step.id}", headerStyle);

            step.action = EditorGUILayout.TextField("Action", step.action);

            EditorGUILayout.LabelField("Details");
            step.details = EditorGUILayout.TextArea(step.details, GUILayout.Height(60));

            step.partId = EditorGUILayout.TextField("Part ID", step.partId);

            EditorGUILayout.Space();

            // Tools
            EditorGUILayout.LabelField("Tools", subHeaderStyle);
            DrawStringArrayEditor(ref step.tools, "Tool");

            EditorGUILayout.Space();

            // Warnings
            EditorGUILayout.LabelField("Warnings", subHeaderStyle);
            DrawStringArrayEditor(ref step.warnings, "Warning");

            EditorGUILayout.Space();

            // Dependencies
            EditorGUILayout.LabelField("Requires (Step IDs)", subHeaderStyle);
            string reqText = step.requires != null ? string.Join(", ", step.requires) : "";
            string newReqText = EditorGUILayout.TextField(reqText);
            if (newReqText != reqText)
            {
                if (string.IsNullOrWhiteSpace(newReqText))
                {
                    step.requires = new int[0];
                }
                else
                {
                    var ids = newReqText.Split(',')
                        .Select(s => s.Trim())
                        .Where(s => int.TryParse(s, out _))
                        .Select(s => int.Parse(s))
                        .ToArray();
                    step.requires = ids;
                }
            }

            EditorGUILayout.Space();

            // Torque spec
            EditorGUILayout.LabelField("Torque Specification", subHeaderStyle);
            bool hasTorque = step.torqueSpec != null;
            bool newHasTorque = EditorGUILayout.Toggle("Has Torque Spec", hasTorque);

            if (newHasTorque && !hasTorque)
            {
                step.torqueSpec = new TorqueSpec { value = 0, unit = "ft-lbs", note = "" };
            }
            else if (!newHasTorque && hasTorque)
            {
                step.torqueSpec = null;
            }

            if (step.torqueSpec != null)
            {
                EditorGUI.indentLevel++;
                step.torqueSpec.value = EditorGUILayout.FloatField("Value", step.torqueSpec.value);
                step.torqueSpec.unit = EditorGUILayout.TextField("Unit", step.torqueSpec.unit);
                step.torqueSpec.note = EditorGUILayout.TextField("Note", step.torqueSpec.note);
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space();

            // Media
            EditorGUILayout.LabelField("Media", subHeaderStyle);
            bool hasMedia = step.media != null;
            bool newHasMedia = EditorGUILayout.Toggle("Has Media", hasMedia);

            if (newHasMedia && !hasMedia)
            {
                step.media = new StepMedia { image = "", video = "" };
            }
            else if (!newHasMedia && hasMedia)
            {
                step.media = null;
            }

            if (step.media != null)
            {
                EditorGUI.indentLevel++;
                step.media.image = EditorGUILayout.TextField("Image Path", step.media.image);
                step.media.video = EditorGUILayout.TextField("Video Path", step.media.video);
                EditorGUI.indentLevel--;
            }

            if (EditorGUI.EndChangeCheck())
            {
                isDirty = true;
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawStringArrayEditor(ref string[] array, string label)
        {
            int count = array?.Length ?? 0;
            int newCount = EditorGUILayout.IntField("Count", count);

            if (newCount != count)
            {
                Array.Resize(ref array, newCount);
                for (int i = count; i < newCount; i++)
                {
                    array[i] = "";
                }
                isDirty = true;
            }

            if (array != null)
            {
                EditorGUI.indentLevel++;
                for (int i = 0; i < array.Length; i++)
                {
                    string newValue = EditorGUILayout.TextField($"{label} {i + 1}", array[i]);
                    if (newValue != array[i])
                    {
                        array[i] = newValue;
                        isDirty = true;
                    }
                }
                EditorGUI.indentLevel--;
            }
        }

        private void DrawDependencyGraph()
        {
            EditorGUILayout.BeginVertical(boxStyle, GUILayout.Height(150));

            if (procedure.steps == null || procedure.steps.Length == 0)
            {
                EditorGUILayout.LabelField("No steps to display");
            }
            else
            {
                // Simple text-based dependency visualization
                EditorGUILayout.LabelField("Step Dependencies:", subHeaderStyle);

                foreach (var step in procedure.steps)
                {
                    string deps = step.requires != null && step.requires.Length > 0
                        ? string.Join(", ", step.requires)
                        : "none";
                    EditorGUILayout.LabelField($"  Step {step.id}: requires [{deps}]");
                }

                // Validate dependencies
                EditorGUILayout.Space();
                if (ValidateDependencies(out string error))
                {
                    EditorGUILayout.HelpBox("Dependencies are valid", MessageType.Info);
                }
                else
                {
                    EditorGUILayout.HelpBox(error, MessageType.Error);
                }
            }

            EditorGUILayout.EndVertical();
        }

        private bool ValidateDependencies(out string error)
        {
            error = "";
            if (procedure.steps == null) return true;

            var stepIds = new HashSet<int>(procedure.steps.Select(s => s.id));

            foreach (var step in procedure.steps)
            {
                if (step.requires == null) continue;

                foreach (int reqId in step.requires)
                {
                    if (!stepIds.Contains(reqId))
                    {
                        error = $"Step {step.id} requires non-existent step {reqId}";
                        return false;
                    }

                    if (reqId == step.id)
                    {
                        error = $"Step {step.id} cannot require itself";
                        return false;
                    }
                }
            }

            // Check for circular dependencies
            foreach (var step in procedure.steps)
            {
                if (HasCircularDependency(step.id, new HashSet<int>()))
                {
                    error = $"Circular dependency detected involving step {step.id}";
                    return false;
                }
            }

            return true;
        }

        private bool HasCircularDependency(int stepId, HashSet<int> visited)
        {
            if (visited.Contains(stepId)) return true;

            var step = procedure.steps.FirstOrDefault(s => s.id == stepId);
            if (step?.requires == null || step.requires.Length == 0) return false;

            visited.Add(stepId);

            foreach (int reqId in step.requires)
            {
                if (HasCircularDependency(reqId, new HashSet<int>(visited)))
                {
                    return true;
                }
            }

            return false;
        }

        // Operations
        private void NewProcedure()
        {
            if (isDirty && !EditorUtility.DisplayDialog("New Procedure",
                "You have unsaved changes. Create new procedure anyway?", "Yes", "Cancel"))
            {
                return;
            }

            procedure = new Procedure
            {
                id = "new_procedure",
                name = "New Procedure",
                description = "",
                engineId = "",
                estimatedTime = "30-60 minutes",
                difficulty = "intermediate",
                tools = new string[0],
                steps = new ProcedureStep[0],
                reinstallNotes = ""
            };

            currentFilePath = null;
            isDirty = false;
            selectedStepIndex = -1;
        }

        private void OpenProcedure()
        {
            string path = EditorUtility.OpenFilePanel("Open Procedure", Application.dataPath, "json");
            if (string.IsNullOrEmpty(path)) return;

            try
            {
                string json = File.ReadAllText(path);
                procedure = JsonUtility.FromJson<Procedure>(json);
                currentFilePath = path;
                isDirty = false;
                selectedStepIndex = -1;
            }
            catch (Exception e)
            {
                EditorUtility.DisplayDialog("Error", $"Failed to open procedure: {e.Message}", "OK");
            }
        }

        private void SaveProcedure()
        {
            if (string.IsNullOrEmpty(currentFilePath))
            {
                SaveProcedureAs();
                return;
            }

            SaveToFile(currentFilePath);
        }

        private void SaveProcedureAs()
        {
            string defaultName = string.IsNullOrEmpty(procedure.id) ? "procedure" : procedure.id;
            string path = EditorUtility.SaveFilePanel("Save Procedure", Application.dataPath, defaultName, "json");
            if (string.IsNullOrEmpty(path)) return;

            currentFilePath = path;
            SaveToFile(path);
        }

        private void SaveToFile(string path)
        {
            try
            {
                string json = JsonUtility.ToJson(procedure, true);
                File.WriteAllText(path, json);
                isDirty = false;
                AssetDatabase.Refresh();
            }
            catch (Exception e)
            {
                EditorUtility.DisplayDialog("Error", $"Failed to save procedure: {e.Message}", "OK");
            }
        }

        private void AddStep()
        {
            int newId = 1;
            if (procedure.steps != null && procedure.steps.Length > 0)
            {
                newId = procedure.steps.Max(s => s.id) + 1;
            }

            var newStep = new ProcedureStep
            {
                id = newId,
                action = "New step",
                details = "",
                partId = "",
                tools = new string[0],
                warnings = new string[0],
                requires = new int[0]
            };

            var list = procedure.steps?.ToList() ?? new List<ProcedureStep>();
            list.Add(newStep);
            procedure.steps = list.ToArray();

            selectedStepIndex = procedure.steps.Length - 1;
            isDirty = true;
        }

        private void DeleteStep(int index)
        {
            if (!EditorUtility.DisplayDialog("Delete Step",
                $"Delete step {procedure.steps[index].id}?", "Delete", "Cancel"))
            {
                return;
            }

            var list = procedure.steps.ToList();
            list.RemoveAt(index);
            procedure.steps = list.ToArray();

            if (selectedStepIndex >= procedure.steps.Length)
            {
                selectedStepIndex = procedure.steps.Length - 1;
            }

            isDirty = true;
        }

        private void MoveStep(int fromIndex, int toIndex)
        {
            var list = procedure.steps.ToList();
            var item = list[fromIndex];
            list.RemoveAt(fromIndex);
            list.Insert(toIndex, item);
            procedure.steps = list.ToArray();

            selectedStepIndex = toIndex;
            isDirty = true;
        }

        private void SelectStep(int index)
        {
            selectedStepIndex = index;
        }

        private string TruncateText(string text, int maxLength)
        {
            if (string.IsNullOrEmpty(text)) return "";
            if (text.Length <= maxLength) return text;
            return text.Substring(0, maxLength - 3) + "...";
        }

        private void HandleKeyboardShortcuts()
        {
            Event e = Event.current;
            if (e.type == EventType.KeyDown && e.control)
            {
                switch (e.keyCode)
                {
                    case KeyCode.S:
                        SaveProcedure();
                        e.Use();
                        break;
                    case KeyCode.N:
                        NewProcedure();
                        e.Use();
                        break;
                    case KeyCode.O:
                        OpenProcedure();
                        e.Use();
                        break;
                }
            }
        }
    }
}
#endif
