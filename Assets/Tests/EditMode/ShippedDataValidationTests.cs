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
        private static string EnginesRoot => Path.Combine(Application.streamingAssetsPath, "Engines");

        /// <summary>
        /// Every engine folder that ships. Each test below runs once per engine, so a new engine is
        /// validated as soon as its folder exists, with no test changes.
        /// </summary>
        private static IEnumerable<string> EngineIds() =>
            Directory.GetDirectories(EnginesRoot).Select(Path.GetFileName).OrderBy(id => id);

        private static string EngineDirectory(string engineId) => Path.Combine(EnginesRoot, engineId);

        private static string PartsDataPath =>
            Path.Combine(Application.dataPath, "Resources", "DefaultPartsData.json");

        private static EngineManifest LoadManifest(string engineId)
        {
            string path = Path.Combine(EngineDirectory(engineId), "engine.json");
            Assert.IsTrue(File.Exists(path), $"Engine manifest missing: {path}");

            EngineManifest manifest = JsonUtility.FromJson<EngineManifest>(File.ReadAllText(path));
            Assert.IsNotNull(manifest, $"{engineId}/engine.json failed to deserialize");
            return manifest;
        }

        private static List<(string File, Procedure Procedure)> LoadProcedures(string engineId)
        {
            string dir = Path.Combine(EngineDirectory(engineId), "procedures");
            Assert.IsTrue(Directory.Exists(dir), $"Procedures directory missing: {dir}");

            var loaded = new List<(string, Procedure)>();
            foreach (string file in Directory.GetFiles(dir, "*.json"))
            {
                string label = $"{engineId}/{Path.GetFileName(file)}";
                Procedure procedure = JsonUtility.FromJson<Procedure>(File.ReadAllText(file));
                Assert.IsNotNull(procedure, $"{label} failed to deserialize");
                loaded.Add((label, procedure));
            }

            Assert.IsNotEmpty(loaded, $"{engineId}: no bundled procedures found");
            return loaded;
        }

        private static PartData[] LoadParts()
        {
            Assert.IsTrue(File.Exists(PartsDataPath), $"Parts data missing: {PartsDataPath}");

            PartsDataFile data = JsonUtility.FromJson<PartsDataFile>(File.ReadAllText(PartsDataPath));
            Assert.IsNotNull(data, "DefaultPartsData.json failed to deserialize");
            Assert.IsNotNull(data.parts, "DefaultPartsData.json has no parts array");
            return data.parts;
        }

        private static HashSet<string> LoadPartIds() => new HashSet<string>(LoadParts().Select(p => p.id));

        // === Engines ===

        [Test]
        public void Engines_AtLeastOneShips()
        {
            CollectionAssert.IsNotEmpty(EngineIds(), $"No engine folders in {EnginesRoot}");
        }

        [Test]
        public void Parts_IdsAreUnique()
        {
            CollectionAssert.AllItemsAreUnique(LoadParts().Select(p => p.id).ToList(),
                "Duplicate part id in DefaultPartsData.json");
        }

        // === Manifest ===

        [TestCaseSource(nameof(EngineIds))]
        public void EngineManifest_HasRequiredFields(string engineId)
        {
            EngineManifest manifest = LoadManifest(engineId);

            Assert.AreEqual(engineId, manifest.id, "engine.json id must match its folder name");
            Assert.IsNotEmpty(manifest.name);
            Assert.IsNotEmpty(manifest.modelFile);
            Assert.IsNotNull(manifest.partMappings);
            Assert.IsNotEmpty(manifest.partMappings);
        }

        [TestCaseSource(nameof(EngineIds))]
        public void EngineManifest_PartMappingsAreCompleteAndUnique(string engineId)
        {
            EngineManifest manifest = LoadManifest(engineId);

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

        [TestCaseSource(nameof(EngineIds))]
        public void EngineManifest_DefaultAlignmentIsWellFormed(string engineId)
        {
            EngineManifest manifest = LoadManifest(engineId);

            Assert.IsNotNull(manifest.defaultAlignment);
            Assert.AreEqual(3, manifest.defaultAlignment.position.Length);
            Assert.AreEqual(3, manifest.defaultAlignment.rotation.Length);
            Assert.AreEqual(3, manifest.defaultAlignment.scale.Length);

            foreach (float axis in manifest.defaultAlignment.scale)
            {
                Assert.Greater(axis, 0f, "Default scale must be positive or the model is invisible");
            }
        }

        [TestCaseSource(nameof(EngineIds))]
        public void EngineManifest_MappedPartsExistInPartsDatabase(string engineId)
        {
            EngineManifest manifest = LoadManifest(engineId);
            HashSet<string> knownParts = LoadPartIds();

            var unknown = manifest.partMappings
                .Select(m => m.partId)
                .Where(id => !knownParts.Contains(id))
                .ToList();

            CollectionAssert.IsEmpty(unknown,
                "engine.json maps model nodes to partIds absent from DefaultPartsData.json, " +
                "so tapping those parts shows no information");
        }

        [TestCaseSource(nameof(EngineIds))]
        public void EngineManifest_MappedPartsListThisEngine(string engineId)
        {
            EngineManifest manifest = LoadManifest(engineId);
            Dictionary<string, PartData> parts = LoadParts().ToDictionary(p => p.id);

            var unlisted = manifest.partMappings
                .Select(m => m.partId)
                .Where(id => parts.TryGetValue(id, out PartData part) &&
                             (part.engines == null || !part.engines.Contains(engineId)))
                .ToList();

            CollectionAssert.IsEmpty(unlisted,
                $"These parts are mapped on {engineId} but their 'engines' list in DefaultPartsData.json " +
                "omits it, so engine-filtered part lookups will not return them");
        }

        // === Model ===

        [TestCaseSource(nameof(EngineIds))]
        public void EngineModel_ExistsAndIsGlb(string engineId)
        {
            EngineManifest manifest = LoadManifest(engineId);
            string path = Path.Combine(EngineDirectory(engineId), manifest.modelFile);

            Assert.IsTrue(File.Exists(path),
                $"engine.json references {manifest.modelFile} but it is not in {EngineDirectory(engineId)}");
            Assert.IsNotEmpty(ReadGlbNodeNames(path), $"{manifest.modelFile} contains no named nodes");
        }

        [TestCaseSource(nameof(EngineIds))]
        public void EngineModel_ContainsEveryMappedNode(string engineId)
        {
            EngineManifest manifest = LoadManifest(engineId);
            HashSet<string> nodeNames = ReadGlbNodeNames(Path.Combine(EngineDirectory(engineId), manifest.modelFile));

            var missing = manifest.partMappings
                .Select(m => m.nodeNameInModel)
                .Where(name => !nodeNames.Contains(name))
                .ToList();

            CollectionAssert.IsEmpty(missing,
                "engine.json maps node names the model does not contain, so those parts can never be " +
                "tapped or highlighted. Rename the model's nodes or update nodeNameInModel.");
        }

        /// <summary>
        /// Returns the names of the glTF nodes in a binary .glb. glTFast names each GameObject after
        /// its node, and EngineModelLoader matches partMappings against those GameObject names, so
        /// node names (not mesh names) are what matter. Parsed by hand because JsonUtility cannot
        /// read the glTF schema and no general JSON library is available inside Unity.
        /// </summary>
        private static HashSet<string> ReadGlbNodeNames(string path)
        {
            byte[] bytes = File.ReadAllBytes(path);
            Assert.GreaterOrEqual(bytes.Length, 20, $"{path} is too short to be a GLB");
            Assert.AreEqual(0x46546C67u, System.BitConverter.ToUInt32(bytes, 0), $"{path} lacks the glTF magic");
            Assert.AreEqual(2u, System.BitConverter.ToUInt32(bytes, 4), $"{path} is not glTF 2.0");

            int jsonLength = (int)System.BitConverter.ToUInt32(bytes, 12);
            Assert.AreEqual(0x4E4F534Au, System.BitConverter.ToUInt32(bytes, 16), "First GLB chunk is not JSON");
            string json = System.Text.Encoding.UTF8.GetString(bytes, 20, jsonLength);

            var names = new HashSet<string>();
            int nodesKey = json.IndexOf("\"nodes\"", System.StringComparison.Ordinal);
            // The scene object also has a "nodes" key (an int array); the top-level one holds objects.
            while (nodesKey >= 0)
            {
                int open = json.IndexOf('[', nodesKey);
                int next = SkipWhitespace(json, open + 1);
                if (next < json.Length && json[next] == '{')
                {
                    string nodes = json.Substring(open, MatchingBracket(json, open) - open + 1);
                    foreach (System.Text.RegularExpressions.Match m in System.Text.RegularExpressions.Regex.Matches(
                                 nodes, "\"name\"\\s*:\\s*\"((?:[^\"\\\\]|\\\\.)*)\""))
                    {
                        names.Add(System.Text.RegularExpressions.Regex.Unescape(m.Groups[1].Value));
                    }
                    break;
                }
                nodesKey = json.IndexOf("\"nodes\"", nodesKey + 1, System.StringComparison.Ordinal);
            }
            return names;
        }

        private static int SkipWhitespace(string s, int i)
        {
            while (i < s.Length && char.IsWhiteSpace(s[i])) i++;
            return i;
        }

        /// <summary>Index of the ']' closing the '[' at <paramref name="open"/>, ignoring brackets in strings.</summary>
        private static int MatchingBracket(string s, int open)
        {
            int depth = 0;
            bool inString = false;
            for (int i = open; i < s.Length; i++)
            {
                char c = s[i];
                if (inString)
                {
                    if (c == '\\') i++;
                    else if (c == '"') inString = false;
                }
                else if (c == '"') inString = true;
                else if (c == '[' || c == '{') depth++;
                else if ((c == ']' || c == '}') && --depth == 0) return i;
            }
            Assert.Fail("Unterminated nodes array in GLB JSON chunk");
            return -1;
        }

        // === Procedures ===

        [TestCaseSource(nameof(EngineIds))]
        public void Procedures_HaveRequiredFields(string engineId)
        {
            foreach ((string file, Procedure procedure) in LoadProcedures(engineId))
            {
                Assert.IsNotEmpty(procedure.id, $"{file}: missing id");
                Assert.IsNotEmpty(procedure.name, $"{file}: missing name");
                Assert.AreEqual(engineId, procedure.engineId, $"{file}: wrong engineId");
                Assert.IsNotNull(procedure.steps, $"{file}: missing steps");
                Assert.IsNotEmpty(procedure.steps, $"{file}: has no steps");
            }
        }

        [TestCaseSource(nameof(EngineIds))]
        public void Procedures_StepIdsAreUniqueAndPositive(string engineId)
        {
            foreach ((string file, Procedure procedure) in LoadProcedures(engineId))
            {
                var ids = procedure.steps.Select(s => s.id).ToList();
                CollectionAssert.AllItemsAreUnique(ids, $"{file}: duplicate step ids");

                foreach (int id in ids)
                {
                    Assert.Greater(id, 0, $"{file}: step ids must be positive");
                }
            }
        }

        [TestCaseSource(nameof(EngineIds))]
        public void Procedures_EveryStepHasAnAction(string engineId)
        {
            foreach ((string file, Procedure procedure) in LoadProcedures(engineId))
            {
                foreach (ProcedureStep step in procedure.steps)
                {
                    Assert.IsNotEmpty(step.action, $"{file}: step {step.id} has no action text");
                }
            }
        }

        [TestCaseSource(nameof(EngineIds))]
        public void Procedures_DependenciesReferenceExistingSteps(string engineId)
        {
            foreach ((string file, Procedure procedure) in LoadProcedures(engineId))
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

        [TestCaseSource(nameof(EngineIds))]
        public void Procedures_AreCompletableWithNoDeadlocks(string engineId)
        {
            foreach ((string file, Procedure procedure) in LoadProcedures(engineId))
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

        [TestCaseSource(nameof(EngineIds))]
        public void Procedures_TorqueSpecsAreSensible(string engineId)
        {
            foreach ((string file, Procedure procedure) in LoadProcedures(engineId))
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

        [TestCaseSource(nameof(EngineIds))]
        public void Procedures_ReferencedPartsExistInPartsDatabase(string engineId)
        {
            HashSet<string> knownParts = LoadPartIds();

            foreach ((string file, Procedure procedure) in LoadProcedures(engineId))
            {
                foreach (ProcedureStep step in procedure.steps)
                {
                    if (string.IsNullOrEmpty(step.partId)) continue;

                    Assert.IsTrue(knownParts.Contains(step.partId),
                        $"{file}: step {step.id} references unknown part '{step.partId}'");
                }
            }
        }

        [TestCaseSource(nameof(EngineIds))]
        public void Procedures_ReferencedPartsAreMappedToModelNodes(string engineId)
        {
            EngineManifest manifest = LoadManifest(engineId);
            var mapped = new HashSet<string>(manifest.partMappings.Select(m => m.partId));

            foreach ((string file, Procedure procedure) in LoadProcedures(engineId))
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

        [TestCaseSource(nameof(EngineIds))]
        public void ShippedProcedures_RunToCompletionThroughTheRunner(string engineId)
        {
            foreach ((string file, Procedure procedure) in LoadProcedures(engineId))
            {
                var go = new GameObject("ShippedProcedureRunner");
                try
                {
                    var runner = go.AddComponent<ProcedureRunner>();

                    bool completed = false;
                    runner.OnProcedureCompleted += () => completed = true;
                    runner.LoadProcedure(procedure, engineId);

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
