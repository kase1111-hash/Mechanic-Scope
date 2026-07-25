using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using MechanicScope.Core;

namespace MechanicScope.Tests
{
    /// <summary>
    /// Validates the content that actually ships in StreamingAssets/Resources, not synthetic
    /// fixtures. A procedure can be perfectly valid JSON and still strand the user — a step that
    /// requires a step that does not exist, a dependency cycle that leaves steps permanently
    /// locked, or a partId with no model node to highlight. These tests catch that class of
    /// breakage, which unit tests over hand-written JSON cannot.
    /// </summary>
    [TestFixture]
    public class ShippedDataValidationTests
    {
        private const string EngineId = "gm_ls_gen4";

        private static string EngineDirectory =>
            Path.Combine(Application.streamingAssetsPath, "Engines", EngineId);

        private static string PartsDataPath =>
            Path.Combine(Application.dataPath, "Resources", "DefaultPartsData.json");

        private static EngineManifest LoadManifest()
        {
            string path = Path.Combine(EngineDirectory, "engine.json");
            Assert.IsTrue(File.Exists(path), $"Engine manifest missing: {path}");

            EngineManifest manifest = JsonUtility.FromJson<EngineManifest>(File.ReadAllText(path));
            Assert.IsNotNull(manifest, "engine.json failed to deserialize");
            return manifest;
        }

        private static List<(string File, Procedure Procedure)> LoadProcedures()
        {
            string dir = Path.Combine(EngineDirectory, "procedures");
            Assert.IsTrue(Directory.Exists(dir), $"Procedures directory missing: {dir}");

            var loaded = new List<(string, Procedure)>();
            foreach (string file in Directory.GetFiles(dir, "*.json"))
            {
                Procedure procedure = JsonUtility.FromJson<Procedure>(File.ReadAllText(file));
                Assert.IsNotNull(procedure, $"{Path.GetFileName(file)} failed to deserialize");
                loaded.Add((Path.GetFileName(file), procedure));
            }

            Assert.IsNotEmpty(loaded, "No bundled procedures found");
            return loaded;
        }

        private static HashSet<string> LoadPartIds()
        {
            Assert.IsTrue(File.Exists(PartsDataPath), $"Parts data missing: {PartsDataPath}");

            PartsDataFile data = JsonUtility.FromJson<PartsDataFile>(File.ReadAllText(PartsDataPath));
            Assert.IsNotNull(data, "DefaultPartsData.json failed to deserialize");
            Assert.IsNotNull(data.parts, "DefaultPartsData.json has no parts array");

            return new HashSet<string>(data.parts.Select(p => p.id));
        }

        // === Manifest ===

        [Test]
        public void EngineManifest_HasRequiredFields()
        {
            EngineManifest manifest = LoadManifest();

            Assert.AreEqual(EngineId, manifest.id);
            Assert.IsNotEmpty(manifest.name);
            Assert.IsNotEmpty(manifest.modelFile);
            Assert.IsNotNull(manifest.partMappings);
            Assert.IsNotEmpty(manifest.partMappings);
        }

        [Test]
        public void EngineManifest_PartMappingsAreCompleteAndUnique()
        {
            EngineManifest manifest = LoadManifest();

            foreach (PartMapping mapping in manifest.partMappings)
            {
                Assert.IsNotEmpty(mapping.nodeNameInModel, "Mapping has no node name");
                Assert.IsNotEmpty(mapping.partId, $"Mapping '{mapping.nodeNameInModel}' has no partId");
            }

            var nodeNames = manifest.partMappings.Select(m => m.nodeNameInModel).ToList();
            CollectionAssert.AllItemsAreUnique(nodeNames, "Duplicate nodeNameInModel in engine.json");

            var partIds = manifest.partMappings.Select(m => m.partId).ToList();
            CollectionAssert.AllItemsAreUnique(partIds, "Duplicate partId in engine.json");
        }

        [Test]
        public void EngineManifest_DefaultAlignmentIsWellFormed()
        {
            EngineManifest manifest = LoadManifest();

            Assert.IsNotNull(manifest.defaultAlignment);
            Assert.AreEqual(3, manifest.defaultAlignment.position.Length);
            Assert.AreEqual(3, manifest.defaultAlignment.rotation.Length);
            Assert.AreEqual(3, manifest.defaultAlignment.scale.Length);

            foreach (float axis in manifest.defaultAlignment.scale)
            {
                Assert.Greater(axis, 0f, "Default scale must be positive or the model is invisible");
            }
        }

        [Test]
        public void EngineManifest_MappedPartsExistInPartsDatabase()
        {
            EngineManifest manifest = LoadManifest();
            HashSet<string> knownParts = LoadPartIds();

            var unknown = manifest.partMappings
                .Select(m => m.partId)
                .Where(id => !knownParts.Contains(id))
                .ToList();

            CollectionAssert.IsEmpty(unknown,
                "engine.json maps model nodes to partIds absent from DefaultPartsData.json, " +
                "so tapping those parts shows no information");
        }

        // === Procedures ===

        [Test]
        public void Procedures_HaveRequiredFields()
        {
            foreach ((string file, Procedure procedure) in LoadProcedures())
            {
                Assert.IsNotEmpty(procedure.id, $"{file}: missing id");
                Assert.IsNotEmpty(procedure.name, $"{file}: missing name");
                Assert.AreEqual(EngineId, procedure.engineId, $"{file}: wrong engineId");
                Assert.IsNotNull(procedure.steps, $"{file}: missing steps");
                Assert.IsNotEmpty(procedure.steps, $"{file}: has no steps");
            }
        }

        [Test]
        public void Procedures_StepIdsAreUniqueAndPositive()
        {
            foreach ((string file, Procedure procedure) in LoadProcedures())
            {
                var ids = procedure.steps.Select(s => s.id).ToList();
                CollectionAssert.AllItemsAreUnique(ids, $"{file}: duplicate step ids");

                foreach (int id in ids)
                {
                    Assert.Greater(id, 0, $"{file}: step ids must be positive");
                }
            }
        }

        [Test]
        public void Procedures_EveryStepHasAnAction()
        {
            foreach ((string file, Procedure procedure) in LoadProcedures())
            {
                foreach (ProcedureStep step in procedure.steps)
                {
                    Assert.IsNotEmpty(step.action, $"{file}: step {step.id} has no action text");
                }
            }
        }

        [Test]
        public void Procedures_DependenciesReferenceExistingSteps()
        {
            foreach ((string file, Procedure procedure) in LoadProcedures())
            {
                var ids = new HashSet<int>(procedure.steps.Select(s => s.id));

                foreach (ProcedureStep step in procedure.steps)
                {
                    if (step.requires == null) continue;

                    foreach (int required in step.requires)
                    {
                        Assert.AreNotEqual(step.id, required, $"{file}: step {step.id} requires itself");
                        Assert.IsTrue(ids.Contains(required),
                            $"{file}: step {step.id} requires step {required}, which does not exist");
                    }
                }
            }
        }

        [Test]
        public void Procedures_AreCompletableWithNoDeadlocks()
        {
            foreach ((string file, Procedure procedure) in LoadProcedures())
            {
                // Replays the runner's own rule: a step unlocks once all of its requires are done.
                // Anything still locked when no further progress is possible is unreachable —
                // a dependency cycle, and the user could never finish the job.
                var completed = new HashSet<int>();
                bool progressed = true;

                while (progressed)
                {
                    progressed = false;
                    foreach (ProcedureStep step in procedure.steps)
                    {
                        if (completed.Contains(step.id)) continue;
                        if (step.requires != null && step.requires.Any(r => !completed.Contains(r))) continue;

                        completed.Add(step.id);
                        progressed = true;
                    }
                }

                var stranded = procedure.steps.Where(s => !completed.Contains(s.id)).Select(s => s.id).ToList();
                CollectionAssert.IsEmpty(stranded, $"{file}: steps unreachable due to a dependency cycle");
            }
        }

        [Test]
        public void Procedures_TorqueSpecsAreSensible()
        {
            foreach ((string file, Procedure procedure) in LoadProcedures())
            {
                foreach (ProcedureStep step in procedure.steps)
                {
                    if (step.torqueSpec == null) continue;

                    Assert.Greater(step.torqueSpec.value, 0f,
                        $"{file}: step {step.id} has a non-positive torque value");
                    Assert.IsNotEmpty(step.torqueSpec.unit,
                        $"{file}: step {step.id} has a torque value with no unit");
                }
            }
        }

        [Test]
        public void Procedures_ReferencedPartsExistInPartsDatabase()
        {
            HashSet<string> knownParts = LoadPartIds();

            foreach ((string file, Procedure procedure) in LoadProcedures())
            {
                foreach (ProcedureStep step in procedure.steps)
                {
                    if (string.IsNullOrEmpty(step.partId)) continue;

                    Assert.IsTrue(knownParts.Contains(step.partId),
                        $"{file}: step {step.id} references unknown part '{step.partId}'");
                }
            }
        }

        [Test]
        public void Procedures_ReferencedPartsAreMappedToModelNodes()
        {
            EngineManifest manifest = LoadManifest();
            var mapped = new HashSet<string>(manifest.partMappings.Select(m => m.partId));

            foreach ((string file, Procedure procedure) in LoadProcedures())
            {
                foreach (ProcedureStep step in procedure.steps)
                {
                    if (string.IsNullOrEmpty(step.partId)) continue;

                    // Without a mapping the runner asks to highlight a part the model has no node
                    // for, so the step silently highlights nothing — the app's core feature.
                    Assert.IsTrue(mapped.Contains(step.partId),
                        $"{file}: step {step.id} references part '{step.partId}', which engine.json " +
                        "does not map to a model node, so it cannot be highlighted");
                }
            }
        }

        // === End-to-end through the real runner ===

        [Test]
        public void ShippedProcedures_RunToCompletionThroughTheRunner()
        {
            foreach ((string file, Procedure procedure) in LoadProcedures())
            {
                var go = new GameObject("ShippedProcedureRunner");
                try
                {
                    var runner = go.AddComponent<ProcedureRunner>();

                    bool completed = false;
                    runner.OnProcedureCompleted += () => completed = true;
                    runner.LoadProcedure(procedure, EngineId);

                    Assert.IsTrue(runner.IsLoaded, $"{file}: failed to load");
                    Assert.IsNotEmpty(runner.AvailableSteps, $"{file}: no step is available at start");

                    // Complete whatever is available until the procedure reports done.
                    int guard = procedure.steps.Length + 1;
                    while (runner.AvailableSteps.Count > 0 && guard-- > 0)
                    {
                        runner.CompleteStep(runner.AvailableSteps[0].id);
                    }

                    Assert.IsTrue(completed, $"{file}: never reported completion");
                    Assert.AreEqual(100f, runner.ProgressPercentage, 0.01f, $"{file}: progress did not reach 100%");
                }
                finally
                {
                    Object.DestroyImmediate(go);
                }
            }
        }
    }
}
