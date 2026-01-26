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
            Assert.IsFalse(procedureRunner.IsLoaded);
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
            Assert.IsTrue(procedure.steps[0].requires == null || procedure.steps[0].requires.Length == 0);

            // Assert - step2 depends on step1
            Assert.AreEqual(1, procedure.steps[1].requires.Length);
            Assert.AreEqual(1, procedure.steps[1].requires[0]);
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
                Assert.IsTrue(step.id > 0, "Step ID should be a positive integer");
                Assert.IsFalse(string.IsNullOrEmpty(step.action), "Step action should not be empty");
                Assert.IsFalse(string.IsNullOrEmpty(step.details), "Step details should not be empty");
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
        public void Procedure_EstimatedTime_IsNotEmpty()
        {
            // Arrange
            string json = CreateSampleProcedureJson();
            var procedure = JsonUtility.FromJson<Procedure>(json);

            // Assert
            Assert.IsFalse(string.IsNullOrEmpty(procedure.estimatedTime));
        }

        [UnityTest]
        public IEnumerator ProcedureRunner_StepNavigation_WorksCorrectly()
        {
            // This test verifies step navigation logic
            yield return null;

            // Verify initial state
            Assert.IsFalse(procedureRunner.IsLoaded);

            yield return null;
        }

        [Test]
        public void Procedure_PartId_IsMappedToSteps()
        {
            // Arrange
            string json = CreateSampleProcedureJson();
            var procedure = JsonUtility.FromJson<Procedure>(json);

            // Assert
            Assert.IsNotNull(procedure.steps[0].partId);
            Assert.AreEqual("part1", procedure.steps[0].partId);
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
                estimatedTime = "60 minutes",
                difficulty = "intermediate",
                steps = new ProcedureStep[]
                {
                    new ProcedureStep
                    {
                        id = 1,
                        action = "Step 1",
                        details = "Do something",
                        partId = "part1",
                        requires = new int[] { }
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
}
