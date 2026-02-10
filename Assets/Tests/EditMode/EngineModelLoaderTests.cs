using System.IO;
using NUnit.Framework;
using UnityEngine;
using MechanicScope.Core;

namespace MechanicScope.Tests
{
    /// <summary>
    /// Tests EngineModelLoader: manifest parsing, engine ID validation,
    /// file extension handling, and part mapping. Uses real GameObjects.
    /// </summary>
    [TestFixture]
    public class EngineModelLoaderTests
    {
        // === Engine ID Validation (security-critical) ===

        [Test]
        public void SanitizeEngineId_ValidId_ReturnsId()
        {
            Assert.AreEqual("gm_ls_gen4", EngineModelLoader.SanitizeEngineId("gm_ls_gen4"));
        }

        [Test]
        public void SanitizeEngineId_WithSpaces_TrimsAndReturns()
        {
            Assert.AreEqual("gm_ls_gen4", EngineModelLoader.SanitizeEngineId("  gm_ls_gen4  "));
        }

        [Test]
        public void SanitizeEngineId_PathTraversal_ReturnsNull()
        {
            Assert.IsNull(EngineModelLoader.SanitizeEngineId("../../etc/passwd"));
            Assert.IsNull(EngineModelLoader.SanitizeEngineId(".."));
            Assert.IsNull(EngineModelLoader.SanitizeEngineId("a/../b"));
        }

        [Test]
        public void SanitizeEngineId_AbsolutePath_ReturnsNull()
        {
            Assert.IsNull(EngineModelLoader.SanitizeEngineId("/etc/passwd"));
            Assert.IsNull(EngineModelLoader.SanitizeEngineId("\\windows\\system32"));
        }

        [Test]
        public void SanitizeEngineId_InvalidCharacters_ReturnsNull()
        {
            Assert.IsNull(EngineModelLoader.SanitizeEngineId("engine:v8"));
            Assert.IsNull(EngineModelLoader.SanitizeEngineId("engine*wild"));
            Assert.IsNull(EngineModelLoader.SanitizeEngineId("engine?query"));
            Assert.IsNull(EngineModelLoader.SanitizeEngineId("engine<html>"));
            Assert.IsNull(EngineModelLoader.SanitizeEngineId("engine|pipe"));
            Assert.IsNull(EngineModelLoader.SanitizeEngineId("engine.with.dots"));
        }

        [Test]
        public void SanitizeEngineId_NullOrEmpty_ReturnsNull()
        {
            Assert.IsNull(EngineModelLoader.SanitizeEngineId(null));
            Assert.IsNull(EngineModelLoader.SanitizeEngineId(""));
            Assert.IsNull(EngineModelLoader.SanitizeEngineId("   "));
        }

        [Test]
        public void IsValidEngineId_MirrorsValidation()
        {
            Assert.IsTrue(EngineModelLoader.IsValidEngineId("gm_ls_gen4"));
            Assert.IsFalse(EngineModelLoader.IsValidEngineId("../hack"));
            Assert.IsFalse(EngineModelLoader.IsValidEngineId(null));
        }

        // === Manifest Parsing ===

        [Test]
        public void EngineManifest_ParsesFromJson()
        {
            string json = @"{
                ""id"": ""gm_ls_gen4"",
                ""name"": ""GM LS Gen IV"",
                ""manufacturer"": ""General Motors"",
                ""years"": ""2007-2017"",
                ""modelFile"": ""gm_ls_gen4.glb"",
                ""partMappings"": [
                    { ""nodeNameInModel"": ""Alternator_Mesh"", ""partId"": ""alternator_gm_ls"" },
                    { ""nodeNameInModel"": ""Battery_Terminal_Mesh"", ""partId"": ""battery_terminal_gm_ls"" }
                ],
                ""defaultAlignment"": {
                    ""position"": [0, 0, 0.5],
                    ""rotation"": [0, 180, 0],
                    ""scale"": [1, 1, 1]
                }
            }";

            EngineManifest manifest = JsonUtility.FromJson<EngineManifest>(json);

            Assert.AreEqual("gm_ls_gen4", manifest.id);
            Assert.AreEqual("GM LS Gen IV", manifest.name);
            Assert.AreEqual("General Motors", manifest.manufacturer);
            Assert.AreEqual("2007-2017", manifest.years);
            Assert.AreEqual("gm_ls_gen4.glb", manifest.modelFile);
            Assert.AreEqual(2, manifest.partMappings.Length);
            Assert.AreEqual("Alternator_Mesh", manifest.partMappings[0].nodeNameInModel);
            Assert.AreEqual("alternator_gm_ls", manifest.partMappings[0].partId);
        }

        [Test]
        public void DefaultAlignment_ParsesFloatArrays()
        {
            string json = @"{
                ""id"": ""test"",
                ""name"": ""Test"",
                ""modelFile"": ""test.glb"",
                ""defaultAlignment"": {
                    ""position"": [1.5, 2.0, -0.5],
                    ""rotation"": [0, 180, 0],
                    ""scale"": [0.5, 0.5, 0.5]
                }
            }";

            EngineManifest manifest = JsonUtility.FromJson<EngineManifest>(json);

            Assert.IsNotNull(manifest.defaultAlignment);
            Assert.AreEqual(3, manifest.defaultAlignment.position.Length);
            Assert.AreEqual(1.5f, manifest.defaultAlignment.position[0], 0.001f);
            Assert.AreEqual(2.0f, manifest.defaultAlignment.position[1], 0.001f);
            Assert.AreEqual(-0.5f, manifest.defaultAlignment.position[2], 0.001f);
            Assert.AreEqual(180f, manifest.defaultAlignment.rotation[1], 0.001f);
            Assert.AreEqual(0.5f, manifest.defaultAlignment.scale[0], 0.001f);
        }

        [Test]
        public void EngineManifest_MissingOptionalFields_DoesNotCrash()
        {
            string json = @"{ ""id"": ""minimal"", ""name"": ""Minimal"", ""modelFile"": ""m.glb"" }";

            EngineManifest manifest = JsonUtility.FromJson<EngineManifest>(json);

            Assert.AreEqual("minimal", manifest.id);
            Assert.IsNull(manifest.partMappings);
            Assert.IsNull(manifest.defaultAlignment);
            Assert.IsNull(manifest.manufacturer);
        }

        // === PartIdentifier Component ===

        [Test]
        public void PartIdentifier_StoresPartIdAndNodeName()
        {
            var go = new GameObject("TestPart");
            var identifier = go.AddComponent<PartIdentifier>();
            identifier.PartId = "alternator_gm_ls";
            identifier.NodeName = "Alternator_Mesh";

            Assert.AreEqual("alternator_gm_ls", identifier.PartId);
            Assert.AreEqual("Alternator_Mesh", identifier.NodeName);

            Object.DestroyImmediate(go);
        }

        // === Procedure JSON Parsing (used by ProcedureRunner but defined here) ===

        [Test]
        public void Procedure_ParsesRealAlternatorJson()
        {
            // Matches the actual replace_alternator.json structure
            string json = @"{
                ""id"": ""replace_alternator"",
                ""name"": ""Replace Alternator"",
                ""description"": ""Step-by-step alternator replacement"",
                ""engineId"": ""gm_ls_gen4"",
                ""estimatedTime"": ""45-60 minutes"",
                ""difficulty"": ""intermediate"",
                ""tools"": [""10mm socket"", ""13mm socket"", ""15mm wrench"", ""torque wrench""],
                ""steps"": [
                    {
                        ""id"": 1,
                        ""action"": ""Remove serpentine belt"",
                        ""details"": ""Use a 15mm wrench to rotate the tensioner"",
                        ""partId"": ""serpentine_belt_gm_ls"",
                        ""tools"": [""15mm wrench""],
                        ""warnings"": [""Note belt routing before removal""]
                    },
                    {
                        ""id"": 2,
                        ""action"": ""Disconnect electrical connector"",
                        ""requires"": [1],
                        ""partId"": ""alternator_connector_gm_ls""
                    },
                    {
                        ""id"": 3,
                        ""action"": ""Remove mounting bolts"",
                        ""requires"": [1],
                        ""tools"": [""13mm socket""],
                        ""torqueSpec"": { ""value"": 37, ""unit"": ""ft-lbs"", ""note"": ""for reinstall"" }
                    },
                    {
                        ""id"": 4,
                        ""action"": ""Remove alternator"",
                        ""requires"": [2, 3],
                        ""partId"": ""alternator_gm_ls""
                    },
                    {
                        ""id"": 5,
                        ""action"": ""Install new alternator"",
                        ""requires"": [4],
                        ""torqueSpec"": { ""value"": 37, ""unit"": ""ft-lbs"" }
                    }
                ]
            }";

            Procedure proc = JsonUtility.FromJson<Procedure>(json);

            Assert.AreEqual("replace_alternator", proc.id);
            Assert.AreEqual(5, proc.steps.Length);
            Assert.AreEqual("intermediate", proc.difficulty);
            Assert.AreEqual(4, proc.tools.Length);

            // Step with dependencies
            Assert.AreEqual(2, proc.steps[3].requires.Length); // Step 4 requires 2 and 3
            Assert.AreEqual(2, proc.steps[3].requires[0]);
            Assert.AreEqual(3, proc.steps[3].requires[1]);

            // Step with torque spec
            Assert.IsNotNull(proc.steps[2].torqueSpec);
            Assert.AreEqual(37f, proc.steps[2].torqueSpec.value, 0.01f);
            Assert.AreEqual("ft-lbs", proc.steps[2].torqueSpec.unit);

            // Step with warnings
            Assert.AreEqual(1, proc.steps[0].warnings.Length);
            Assert.AreEqual("Note belt routing before removal", proc.steps[0].warnings[0]);
        }
    }
}
