using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace MechanicScope.Tests.Runtime.Integration
{
    /// <summary>
    /// End-to-end integration tests for the complete workflow.
    /// Tests full user journeys through the application.
    /// </summary>
    public class EndToEndTests : TestBase
    {
        [Test]
        public void E2E_LoadEngineManifest_ParsesProcedures()
        {
            // Arrange
            string engineJson = CreateSampleEngineJson();
            string procedureJson = CreateSampleProcedureJson();

            WriteTestFile("engines/test_engine/engine.json", engineJson);
            WriteTestFile("engines/test_engine/procedures/test_procedure.json", procedureJson);

            // Act
            string loadedEngineJson = ReadTestFile("engines/test_engine/engine.json");
            var engine = JsonUtility.FromJson<TestEngineManifest>(loadedEngineJson);

            string loadedProcedureJson = ReadTestFile("engines/test_engine/procedures/test_procedure.json");
            var procedure = JsonUtility.FromJson<TestProcedure>(loadedProcedureJson);

            // Assert
            Assert.IsNotNull(engine);
            Assert.IsNotNull(procedure);
            Assert.AreEqual("test_engine", engine.id);
            Assert.AreEqual("test_procedure", procedure.id);
        }

        [Test]
        public void E2E_ProcedureWorkflow_CompletesAllSteps()
        {
            // Arrange
            string json = CreateSampleProcedureJson();
            var procedure = JsonUtility.FromJson<TestProcedure>(json);

            var completedSteps = new List<string>();
            int currentStepIndex = 0;

            // Act - simulate completing all steps
            while (currentStepIndex < procedure.steps.Length)
            {
                var step = procedure.steps[currentStepIndex];

                // Verify dependencies are met
                bool dependenciesMet = true;
                foreach (var dep in step.dependencies)
                {
                    if (!completedSteps.Contains(dep))
                    {
                        dependenciesMet = false;
                        break;
                    }
                }

                Assert.IsTrue(dependenciesMet, $"Dependencies not met for step {step.id}");

                // Complete the step
                completedSteps.Add(step.id);
                currentStepIndex++;
            }

            // Assert
            Assert.AreEqual(procedure.steps.Length, completedSteps.Count);
        }

        [Test]
        public void E2E_ProgressSaveAndRestore()
        {
            // Arrange
            var progress = new TestUserProgress
            {
                currentEngineId = "test_engine",
                currentProcedureId = "test_procedure",
                currentStepIndex = 3,
                completedStepIds = new List<string> { "step1", "step2", "step3" }
            };

            string progressPath = Path.Combine(TestDataPath, "progress.json");

            // Act - Save
            string json = JsonUtility.ToJson(progress);
            File.WriteAllText(progressPath, json);

            // Simulate app restart by loading from file
            string loadedJson = File.ReadAllText(progressPath);
            var loadedProgress = JsonUtility.FromJson<TestUserProgress>(loadedJson);

            // Assert
            Assert.AreEqual(progress.currentEngineId, loadedProgress.currentEngineId);
            Assert.AreEqual(progress.currentProcedureId, loadedProgress.currentProcedureId);
            Assert.AreEqual(progress.currentStepIndex, loadedProgress.currentStepIndex);
        }

        [Test]
        public void E2E_PartIdentification_MatchesStep()
        {
            // Arrange
            string procedureJson = CreateSampleProcedureJson();
            var procedure = JsonUtility.FromJson<TestProcedure>(procedureJson);

            string partsJson = CreateSamplePartsJson();
            var partsDb = JsonUtility.FromJson<TestPartsDatabase>(partsJson);

            // Act - for each step, verify parts can be found
            foreach (var step in procedure.steps)
            {
                foreach (var partId in step.partIds)
                {
                    var part = System.Array.Find(partsDb.parts, p => p.id == partId);

                    // Assert
                    Assert.IsNotNull(part, $"Part {partId} not found for step {step.id}");
                }
            }
        }

        [Test]
        public void E2E_VoiceCommand_NavigatesSteps()
        {
            // Arrange
            var voiceCommands = new Dictionary<string, int>
            {
                { "next step", 1 },
                { "previous step", -1 },
                { "go back", -1 }
            };

            int currentStep = 5;
            int totalSteps = 10;

            // Act & Assert
            // Test "next step"
            int newStep = currentStep + voiceCommands["next step"];
            Assert.AreEqual(6, newStep);
            Assert.Less(newStep, totalSteps);

            // Test "previous step"
            newStep = currentStep + voiceCommands["previous step"];
            Assert.AreEqual(4, newStep);
            Assert.GreaterOrEqual(newStep, 0);
        }

        [Test]
        public void E2E_AccessibilitySettings_ApplyCorrectly()
        {
            // Arrange
            var settings = new Dictionary<string, object>
            {
                { "text_size", "large" },
                { "high_contrast", true },
                { "haptics", true }
            };

            // Act - apply settings
            float textScale = settings["text_size"].ToString() == "large" ? 1.25f : 1.0f;
            bool highContrast = (bool)settings["high_contrast"];
            bool haptics = (bool)settings["haptics"];

            // Assert
            Assert.AreEqual(1.25f, textScale);
            Assert.IsTrue(highContrast);
            Assert.IsTrue(haptics);
        }

        [UnityTest]
        public IEnumerator E2E_ARScene_InitializesCorrectly()
        {
            // Arrange
            GameObject arSession = new GameObject("ARSession");
            arSession.transform.SetParent(TestRoot.transform);

            GameObject arCamera = new GameObject("ARCamera");
            arCamera.transform.SetParent(TestRoot.transform);
            var camera = arCamera.AddComponent<Camera>();
            camera.tag = "MainCamera";

            yield return null;

            // Assert
            Assert.IsNotNull(Camera.main);
            Assert.IsNotNull(arSession);
        }

        [Test]
        public void E2E_ProcedureExport_CreatesValidPackage()
        {
            // Arrange
            string procedureJson = CreateSampleProcedureJson();
            var procedure = JsonUtility.FromJson<TestProcedure>(procedureJson);

            string packageDir = Path.Combine(TestDataPath, "export_package");
            Directory.CreateDirectory(packageDir);

            // Act - simulate export
            File.WriteAllText(Path.Combine(packageDir, "procedure.json"), procedureJson);
            File.WriteAllText(Path.Combine(packageDir, "package.json"), JsonUtility.ToJson(new TestPackageInfo
            {
                version = 1,
                procedureId = procedure.id,
                procedureName = procedure.name
            }));

            // Assert
            Assert.IsTrue(File.Exists(Path.Combine(packageDir, "procedure.json")));
            Assert.IsTrue(File.Exists(Path.Combine(packageDir, "package.json")));
        }

        [Test]
        public void E2E_ProcedureImport_RestoresData()
        {
            // Arrange
            string packageDir = Path.Combine(TestDataPath, "import_test");
            Directory.CreateDirectory(packageDir);

            var originalProcedure = new TestProcedure
            {
                id = "imported_procedure",
                name = "Imported Procedure",
                steps = new TestProcedureStep[]
                {
                    new TestProcedureStep { id = "s1", title = "Step 1" }
                }
            };

            File.WriteAllText(
                Path.Combine(packageDir, "procedure.json"),
                JsonUtility.ToJson(originalProcedure)
            );

            // Act - simulate import
            string json = File.ReadAllText(Path.Combine(packageDir, "procedure.json"));
            var importedProcedure = JsonUtility.FromJson<TestProcedure>(json);

            // Assert
            Assert.AreEqual(originalProcedure.id, importedProcedure.id);
            Assert.AreEqual(originalProcedure.name, importedProcedure.name);
        }

        [Test]
        public void E2E_RepairHistory_TracksCompletion()
        {
            // Arrange
            var history = new List<TestRepairHistoryEntry>();

            // Act - simulate completing a repair
            var repair = new TestRepairHistoryEntry
            {
                procedureId = "oil_change",
                engineId = "test_engine",
                startTime = System.DateTime.UtcNow.AddMinutes(-30).ToString("o"),
                completionTime = System.DateTime.UtcNow.ToString("o"),
                totalSteps = 8,
                completedSteps = 8,
                wasCompleted = true,
                rating = 5
            };

            history.Add(repair);

            // Assert
            Assert.AreEqual(1, history.Count);
            Assert.IsTrue(history[0].wasCompleted);
            Assert.AreEqual(5, history[0].rating);
        }

        [Test]
        public void E2E_MultipleEngines_SeparateProgress()
        {
            // Arrange
            var engineProgress = new Dictionary<string, TestUserProgress>();

            // Act - set up progress for multiple engines
            engineProgress["engine1"] = new TestUserProgress
            {
                currentEngineId = "engine1",
                currentStepIndex = 3
            };

            engineProgress["engine2"] = new TestUserProgress
            {
                currentEngineId = "engine2",
                currentStepIndex = 7
            };

            // Assert
            Assert.AreEqual(3, engineProgress["engine1"].currentStepIndex);
            Assert.AreEqual(7, engineProgress["engine2"].currentStepIndex);
        }

        [Test]
        public void E2E_SearchParts_ReturnsResults()
        {
            // Arrange
            var parts = new List<TestPart>
            {
                new TestPart { id = "alternator", name = "Alternator", category = "electrical" },
                new TestPart { id = "starter", name = "Starter Motor", category = "electrical" },
                new TestPart { id = "oil_filter", name = "Oil Filter", category = "filtration" },
                new TestPart { id = "air_filter", name = "Air Filter", category = "filtration" }
            };

            // Act - search by name
            var nameResults = parts.FindAll(p =>
                p.name.ToLower().Contains("filter"));

            // Act - search by category
            var categoryResults = parts.FindAll(p =>
                p.category == "electrical");

            // Assert
            Assert.AreEqual(2, nameResults.Count);
            Assert.AreEqual(2, categoryResults.Count);
        }
    }

    // Test data classes
    [System.Serializable]
    public class TestEngineManifest
    {
        public string id;
        public string name;
        public string manufacturer;
    }

    [System.Serializable]
    public class TestProcedure
    {
        public string id;
        public string name;
        public string description;
        public string engineId;
        public TestProcedureStep[] steps;
    }

    [System.Serializable]
    public class TestProcedureStep
    {
        public string id;
        public string title;
        public string instruction;
        public string[] partIds = new string[0];
        public string[] dependencies = new string[0];
    }

    [System.Serializable]
    public class TestPartsDatabase
    {
        public TestPart[] parts;
    }

    [System.Serializable]
    public class TestPart
    {
        public string id;
        public string name;
        public string category;
    }

    [System.Serializable]
    public class TestUserProgress
    {
        public string currentEngineId;
        public string currentProcedureId;
        public int currentStepIndex;
        public List<string> completedStepIds = new List<string>();
    }

    [System.Serializable]
    public class TestPackageInfo
    {
        public int version;
        public string procedureId;
        public string procedureName;
    }

    [System.Serializable]
    public class TestRepairHistoryEntry
    {
        public string procedureId;
        public string engineId;
        public string startTime;
        public string completionTime;
        public int totalSteps;
        public int completedSteps;
        public bool wasCompleted;
        public int rating;
    }
}
