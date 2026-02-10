using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using MechanicScope.Core;

namespace MechanicScope.Tests
{
    /// <summary>
    /// Tests ProcedureRunner: dependency resolution, step sequencing, completion,
    /// uncomplete, and event firing. Uses real GameObjects, no mocks.
    /// </summary>
    [TestFixture]
    public class ProcedureRunnerTests
    {
        private GameObject runnerGO;
        private ProcedureRunner runner;

        // Sample procedure JSON matching the real oil_change.json schema
        private const string LinearProcedureJson = @"{
            ""id"": ""test_linear"",
            ""name"": ""Linear Test"",
            ""description"": ""A simple linear procedure"",
            ""engineId"": ""test_engine"",
            ""estimatedTime"": ""10 min"",
            ""difficulty"": ""beginner"",
            ""tools"": [""wrench""],
            ""steps"": [
                { ""id"": 1, ""action"": ""Step one"", ""details"": ""First step"", ""partId"": ""part_a"" },
                { ""id"": 2, ""action"": ""Step two"", ""details"": ""Second step"", ""requires"": [1] },
                { ""id"": 3, ""action"": ""Step three"", ""details"": ""Third step"", ""requires"": [2] }
            ]
        }";

        // Diamond dependency: 1 -> 2, 1 -> 3, 2+3 -> 4
        private const string DiamondProcedureJson = @"{
            ""id"": ""test_diamond"",
            ""name"": ""Diamond Deps"",
            ""engineId"": ""test_engine"",
            ""steps"": [
                { ""id"": 1, ""action"": ""Remove cover"" },
                { ""id"": 2, ""action"": ""Disconnect left"", ""requires"": [1] },
                { ""id"": 3, ""action"": ""Disconnect right"", ""requires"": [1] },
                { ""id"": 4, ""action"": ""Remove component"", ""requires"": [2, 3] },
                { ""id"": 5, ""action"": ""Install new"", ""requires"": [4] }
            ]
        }";

        // No dependencies — all steps available immediately
        private const string ParallelProcedureJson = @"{
            ""id"": ""test_parallel"",
            ""name"": ""Parallel Steps"",
            ""engineId"": ""test_engine"",
            ""steps"": [
                { ""id"": 1, ""action"": ""Check oil"" },
                { ""id"": 2, ""action"": ""Check coolant"" },
                { ""id"": 3, ""action"": ""Check brakes"" }
            ]
        }";

        [SetUp]
        public void SetUp()
        {
            runnerGO = new GameObject("TestProcedureRunner");
            runner = runnerGO.AddComponent<ProcedureRunner>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(runnerGO);
        }

        // === Loading ===

        [Test]
        public void LoadProcedureFromJson_ValidJson_LoadsProcedure()
        {
            runner.LoadProcedureFromJson(LinearProcedureJson, "test_engine");

            Assert.IsTrue(runner.IsLoaded);
            Assert.AreEqual("test_linear", runner.CurrentProcedure.id);
            Assert.AreEqual("Linear Test", runner.CurrentProcedure.name);
            Assert.AreEqual(3, runner.CurrentProcedure.steps.Length);
        }

        [Test]
        public void LoadProcedureFromJson_NullJson_FiresError()
        {
            string errorMessage = null;
            runner.OnLoadError += msg => errorMessage = msg;

            runner.LoadProcedureFromJson(null, "test_engine");

            Assert.IsFalse(runner.IsLoaded);
            Assert.IsNotNull(errorMessage);
        }

        [Test]
        public void LoadProcedureFromJson_EmptyJson_FiresError()
        {
            string errorMessage = null;
            runner.OnLoadError += msg => errorMessage = msg;

            runner.LoadProcedureFromJson("", "test_engine");

            Assert.IsFalse(runner.IsLoaded);
            Assert.IsNotNull(errorMessage);
        }

        [Test]
        public void LoadProcedureFromJson_InvalidJson_FiresError()
        {
            string errorMessage = null;
            runner.OnLoadError += msg => errorMessage = msg;

            runner.LoadProcedureFromJson("{not valid json!!!", "test_engine");

            Assert.IsFalse(runner.IsLoaded);
        }

        [Test]
        public void LoadProcedure_FiresOnProcedureLoaded()
        {
            Procedure loadedProcedure = null;
            runner.OnProcedureLoaded += p => loadedProcedure = p;

            runner.LoadProcedureFromJson(LinearProcedureJson, "test_engine");

            Assert.IsNotNull(loadedProcedure);
            Assert.AreEqual("test_linear", loadedProcedure.id);
        }

        // === Dependency Resolution ===

        [Test]
        public void LinearDeps_OnlyFirstStepAvailableInitially()
        {
            runner.LoadProcedureFromJson(LinearProcedureJson, "test_engine");

            Assert.AreEqual(1, runner.AvailableSteps.Count);
            Assert.AreEqual(1, runner.AvailableSteps[0].id);
        }

        [Test]
        public void LinearDeps_CompletingStepUnlocksNext()
        {
            runner.LoadProcedureFromJson(LinearProcedureJson, "test_engine");

            runner.CompleteStep(1);

            Assert.AreEqual(1, runner.AvailableSteps.Count);
            Assert.AreEqual(2, runner.AvailableSteps[0].id);
        }

        [Test]
        public void LinearDeps_CompletingAllStepsFiresCompleted()
        {
            bool completed = false;
            runner.OnProcedureCompleted += () => completed = true;

            runner.LoadProcedureFromJson(LinearProcedureJson, "test_engine");
            runner.CompleteStep(1);
            runner.CompleteStep(2);
            runner.CompleteStep(3);

            Assert.IsTrue(completed);
            Assert.AreEqual(100f, runner.ProgressPercentage, 0.01f);
        }

        [Test]
        public void DiamondDeps_OnlyRootAvailableInitially()
        {
            runner.LoadProcedureFromJson(DiamondProcedureJson, "test_engine");

            Assert.AreEqual(1, runner.AvailableSteps.Count);
            Assert.AreEqual(1, runner.AvailableSteps[0].id);
        }

        [Test]
        public void DiamondDeps_CompletingRootUnlocksBothBranches()
        {
            runner.LoadProcedureFromJson(DiamondProcedureJson, "test_engine");

            runner.CompleteStep(1);

            Assert.AreEqual(2, runner.AvailableSteps.Count);
            var ids = new HashSet<int> { runner.AvailableSteps[0].id, runner.AvailableSteps[1].id };
            Assert.IsTrue(ids.Contains(2));
            Assert.IsTrue(ids.Contains(3));
        }

        [Test]
        public void DiamondDeps_JoinStepBlockedUntilBothBranchesComplete()
        {
            runner.LoadProcedureFromJson(DiamondProcedureJson, "test_engine");

            runner.CompleteStep(1);
            runner.CompleteStep(2);

            // Step 4 requires both 2 AND 3 — only 2 is done
            Assert.IsFalse(runner.IsStepAvailable(4));
            Assert.AreEqual(StepStatus.Blocked, runner.GetStepStatus(4));
        }

        [Test]
        public void DiamondDeps_JoinStepUnlocksWhenBothBranchesDone()
        {
            runner.LoadProcedureFromJson(DiamondProcedureJson, "test_engine");

            runner.CompleteStep(1);
            runner.CompleteStep(2);
            runner.CompleteStep(3);

            Assert.IsTrue(runner.IsStepAvailable(4));
            Assert.AreEqual(StepStatus.Available, runner.GetStepStatus(4));
        }

        [Test]
        public void ParallelSteps_AllAvailableImmediately()
        {
            runner.LoadProcedureFromJson(ParallelProcedureJson, "test_engine");

            Assert.AreEqual(3, runner.AvailableSteps.Count);
        }

        [Test]
        public void ParallelSteps_CanCompleteInAnyOrder()
        {
            bool completed = false;
            runner.OnProcedureCompleted += () => completed = true;

            runner.LoadProcedureFromJson(ParallelProcedureJson, "test_engine");

            runner.CompleteStep(3);
            runner.CompleteStep(1);
            runner.CompleteStep(2);

            Assert.IsTrue(completed);
        }

        // === Step Completion ===

        [Test]
        public void CompleteStep_BlockedStep_DoesNothing()
        {
            runner.LoadProcedureFromJson(LinearProcedureJson, "test_engine");

            // Step 2 requires step 1, which isn't done
            runner.CompleteStep(2);

            Assert.AreEqual(0, runner.CompletedSteps.Count);
        }

        [Test]
        public void CompleteStep_FiresOnStepCompleted()
        {
            ProcedureStep completedStep = null;
            runner.OnStepCompleted += s => completedStep = s;

            runner.LoadProcedureFromJson(LinearProcedureJson, "test_engine");
            runner.CompleteStep(1);

            Assert.IsNotNull(completedStep);
            Assert.AreEqual(1, completedStep.id);
        }

        [Test]
        public void CompleteStep_AdvancesActiveStep()
        {
            runner.LoadProcedureFromJson(LinearProcedureJson, "test_engine");

            Assert.AreEqual(1, runner.ActiveStep.id);

            runner.CompleteStep(1);

            Assert.AreEqual(2, runner.ActiveStep.id);
        }

        // === Uncomplete ===

        [Test]
        public void UncompleteStep_RestoresAvailability()
        {
            runner.LoadProcedureFromJson(LinearProcedureJson, "test_engine");
            runner.CompleteStep(1);
            runner.CompleteStep(2);

            Assert.AreEqual(StepStatus.Completed, runner.GetStepStatus(2));

            runner.UncompleteStep(2);

            Assert.AreEqual(StepStatus.Available, runner.GetStepStatus(2));
            Assert.AreEqual(1, runner.CompletedSteps.Count);
        }

        [Test]
        public void UncompleteStep_BlockedByDependent_DoesNothing()
        {
            runner.LoadProcedureFromJson(LinearProcedureJson, "test_engine");
            runner.CompleteStep(1);
            runner.CompleteStep(2);

            // Step 2 depends on step 1, so can't uncomplete step 1
            runner.UncompleteStep(1);

            Assert.IsTrue(runner.IsStepCompleted(1));
            Assert.AreEqual(2, runner.CompletedSteps.Count);
        }

        // === Navigation ===

        [Test]
        public void NextStep_AdvancesWithinAvailableSteps()
        {
            runner.LoadProcedureFromJson(ParallelProcedureJson, "test_engine");

            Assert.AreEqual(1, runner.ActiveStep.id);

            runner.NextStep();
            Assert.AreEqual(2, runner.ActiveStep.id);

            runner.NextStep();
            Assert.AreEqual(3, runner.ActiveStep.id);
        }

        [Test]
        public void PreviousStep_GoesBackWithinAvailableSteps()
        {
            runner.LoadProcedureFromJson(ParallelProcedureJson, "test_engine");

            runner.NextStep();
            runner.NextStep();
            Assert.AreEqual(3, runner.ActiveStep.id);

            runner.PreviousStep();
            Assert.AreEqual(2, runner.ActiveStep.id);
        }

        // === Progress ===

        [Test]
        public void ProgressPercentage_CalculatesCorrectly()
        {
            runner.LoadProcedureFromJson(LinearProcedureJson, "test_engine");

            Assert.AreEqual(0f, runner.ProgressPercentage, 0.01f);

            runner.CompleteStep(1);
            Assert.AreEqual(33.33f, runner.ProgressPercentage, 0.5f);

            runner.CompleteStep(2);
            Assert.AreEqual(66.67f, runner.ProgressPercentage, 0.5f);
        }

        // === Reset ===

        [Test]
        public void ResetProcedure_ClearsAllProgress()
        {
            runner.LoadProcedureFromJson(LinearProcedureJson, "test_engine");
            runner.CompleteStep(1);
            runner.CompleteStep(2);

            runner.ResetProcedure();

            Assert.AreEqual(0, runner.CompletedSteps.Count);
            Assert.AreEqual(0f, runner.ProgressPercentage);
            Assert.AreEqual(1, runner.AvailableSteps.Count);
            Assert.AreEqual(1, runner.AvailableSteps[0].id);
        }

        // === Unload ===

        [Test]
        public void UnloadProcedure_ClearsEverything()
        {
            runner.LoadProcedureFromJson(LinearProcedureJson, "test_engine");
            runner.CompleteStep(1);

            runner.UnloadProcedure();

            Assert.IsFalse(runner.IsLoaded);
            Assert.IsNull(runner.CurrentProcedure);
            Assert.IsNull(runner.ActiveStep);
            Assert.AreEqual(0, runner.CompletedSteps.Count);
            Assert.AreEqual(0, runner.AvailableSteps.Count);
        }

        // === Part Highlighting ===

        [Test]
        public void GetHighlightedParts_ReturnsActiveStepPart()
        {
            runner.LoadProcedureFromJson(LinearProcedureJson, "test_engine");

            List<string> parts = runner.GetHighlightedParts();

            Assert.AreEqual(1, parts.Count);
            Assert.AreEqual("part_a", parts[0]);
        }

        [Test]
        public void GetHighlightedParts_StepWithNoPart_ReturnsEmpty()
        {
            runner.LoadProcedureFromJson(LinearProcedureJson, "test_engine");
            runner.CompleteStep(1);

            // Step 2 has no partId
            List<string> parts = runner.GetHighlightedParts();

            Assert.AreEqual(0, parts.Count);
        }

        // === Step Status ===

        [Test]
        public void GetStepStatus_ReturnsCorrectStates()
        {
            runner.LoadProcedureFromJson(LinearProcedureJson, "test_engine");

            Assert.AreEqual(StepStatus.Available, runner.GetStepStatus(1));
            Assert.AreEqual(StepStatus.Blocked, runner.GetStepStatus(2));
            Assert.AreEqual(StepStatus.Blocked, runner.GetStepStatus(3));

            runner.CompleteStep(1);

            Assert.AreEqual(StepStatus.Completed, runner.GetStepStatus(1));
            Assert.AreEqual(StepStatus.Available, runner.GetStepStatus(2));
            Assert.AreEqual(StepStatus.Blocked, runner.GetStepStatus(3));
        }

        // === Torque Spec ===

        [Test]
        public void TorqueSpec_ToString_FormatsCorrectly()
        {
            var spec = new TorqueSpec { value = 37f, unit = "ft-lbs", note = "dry threads" };
            Assert.AreEqual("37 ft-lbs (dry threads)", spec.ToString());
        }

        [Test]
        public void TorqueSpec_ToString_NoNote_OmitsParentheses()
        {
            var spec = new TorqueSpec { value = 18f, unit = "ft-lbs" };
            Assert.AreEqual("18 ft-lbs", spec.ToString());
        }
    }
}
