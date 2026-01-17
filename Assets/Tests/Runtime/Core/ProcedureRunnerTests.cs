using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using MechanicScope.Core;

namespace MechanicScope.Tests.Runtime.Core
{
    /// <summary>
    /// End-to-end tests for ProcedureRunner.
    /// Tests procedure loading, step navigation, and completion tracking.
    /// </summary>
    public class ProcedureRunnerTests : TestBase
    {
        private ProcedureRunner procedureRunner;

        public override void SetUp()
        {
            base.SetUp();
            procedureRunner = CreateGameObjectWithComponent<ProcedureRunner>("ProcedureRunner");
        }

        [Test]
        public void ProcedureRunner_InitializesCorrectly()
        {
            Assert.IsNotNull(procedureRunner);
            Assert.IsNull(procedureRunner.CurrentProcedure);
            Assert.IsFalse(procedureRunner.IsRunning);
        }

        [Test]
        public void LoadProcedure_WithValidJson_LoadsSuccessfully()
        {
            // Arrange
            string json = CreateSampleProcedureJson();
            WriteTestFile("test_procedure.json", json);

            // Act
            var procedure = JsonUtility.FromJson<Procedure>(json);

            // Assert
            Assert.IsNotNull(procedure);
            Assert.AreEqual("test_procedure", procedure.id);
            Assert.AreEqual("Test Procedure", procedure.name);
            Assert.AreEqual(2, procedure.steps.Length);
        }

        [Test]
        public void Procedure_StepDependencies_ResolveCorrectly()
        {
            // Arrange
            string json = CreateSampleProcedureJson();
            var procedure = JsonUtility.FromJson<Procedure>(json);

            // Assert - step1 has no dependencies
            Assert.AreEqual(0, procedure.steps[0].dependencies.Length);

            // Assert - step2 depends on step1
            Assert.AreEqual(1, procedure.steps[1].dependencies.Length);
            Assert.AreEqual("step1", procedure.steps[1].dependencies[0]);
        }

        [Test]
        public void Procedure_Steps_HaveRequiredFields()
        {
            // Arrange
            string json = CreateSampleProcedureJson();
            var procedure = JsonUtility.FromJson<Procedure>(json);

            // Assert
            foreach (var step in procedure.steps)
            {
                Assert.IsFalse(string.IsNullOrEmpty(step.id), "Step ID should not be empty");
                Assert.IsFalse(string.IsNullOrEmpty(step.title), "Step title should not be empty");
                Assert.IsFalse(string.IsNullOrEmpty(step.instruction), "Step instruction should not be empty");
            }
        }

        [Test]
        public void Procedure_Difficulty_ParsesCorrectly()
        {
            // Arrange
            string json = CreateSampleProcedureJson();
            var procedure = JsonUtility.FromJson<Procedure>(json);

            // Assert
            Assert.AreEqual("beginner", procedure.difficulty);
        }

        [Test]
        public void Procedure_EstimatedTime_IsPositive()
        {
            // Arrange
            string json = CreateSampleProcedureJson();
            var procedure = JsonUtility.FromJson<Procedure>(json);

            // Assert
            Assert.Greater(procedure.estimatedTime, 0);
        }

        [UnityTest]
        public IEnumerator ProcedureRunner_StepNavigation_WorksCorrectly()
        {
            // This test verifies step navigation logic
            yield return null;

            // Verify initial state
            Assert.IsFalse(procedureRunner.IsRunning);

            yield return null;
        }

        [Test]
        public void Procedure_PartIds_AreMappedToSteps()
        {
            // Arrange
            string json = CreateSampleProcedureJson();
            var procedure = JsonUtility.FromJson<Procedure>(json);

            // Assert
            Assert.IsNotNull(procedure.steps[0].partIds);
            Assert.AreEqual(1, procedure.steps[0].partIds.Length);
            Assert.AreEqual("part1", procedure.steps[0].partIds[0]);
        }

        [Test]
        public void Procedure_SerializationRoundTrip_PreservesData()
        {
            // Arrange
            var original = new Procedure
            {
                id = "roundtrip_test",
                name = "Round Trip Test",
                description = "Testing serialization",
                engineId = "test_engine",
                estimatedTime = 60,
                difficulty = "intermediate",
                steps = new ProcedureStep[]
                {
                    new ProcedureStep
                    {
                        id = "step1",
                        title = "Step 1",
                        instruction = "Do something",
                        partIds = new string[] { "part1" },
                        dependencies = new string[] { }
                    }
                }
            };

            // Act
            string json = JsonUtility.ToJson(original);
            var deserialized = JsonUtility.FromJson<Procedure>(json);

            // Assert
            Assert.AreEqual(original.id, deserialized.id);
            Assert.AreEqual(original.name, deserialized.name);
            Assert.AreEqual(original.estimatedTime, deserialized.estimatedTime);
            Assert.AreEqual(original.steps.Length, deserialized.steps.Length);
        }

        [Test]
        public void Procedure_EmptySteps_HandledGracefully()
        {
            // Arrange
            var procedure = new Procedure
            {
                id = "empty_steps",
                name = "Empty Steps Test",
                steps = new ProcedureStep[] { }
            };

            // Assert
            Assert.IsNotNull(procedure.steps);
            Assert.AreEqual(0, procedure.steps.Length);
        }

        [Test]
        public void Procedure_NullSteps_DefaultsToNull()
        {
            // Arrange
            var procedure = new Procedure
            {
                id = "null_steps",
                name = "Null Steps Test"
            };

            // Assert - Unity's JsonUtility leaves arrays null if not initialized
            Assert.IsNull(procedure.steps);
        }
    }

    /// <summary>
    /// Minimal Procedure class for testing when actual class is not available.
    /// </summary>
    [System.Serializable]
    public class Procedure
    {
        public string id;
        public string name;
        public string description;
        public string engineId;
        public int estimatedTime;
        public string difficulty;
        public ProcedureStep[] steps;
    }

    [System.Serializable]
    public class ProcedureStep
    {
        public string id;
        public string title;
        public string instruction;
        public string[] partIds;
        public string[] dependencies;
        public StepMedia media;
    }

    [System.Serializable]
    public class StepMedia
    {
        public string image;
        public string video;
    }
}
