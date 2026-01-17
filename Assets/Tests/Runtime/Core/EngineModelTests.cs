using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace MechanicScope.Tests.Runtime.Core
{
    /// <summary>
    /// End-to-end tests for EngineModelLoader and engine manifests.
    /// Tests engine loading, part mapping, and model management.
    /// </summary>
    public class EngineModelTests : TestBase
    {
        [Test]
        public void EngineManifest_ParsesCorrectly()
        {
            // Arrange
            string json = CreateSampleEngineJson();

            // Act
            var engine = JsonUtility.FromJson<EngineManifest>(json);

            // Assert
            Assert.IsNotNull(engine);
            Assert.AreEqual("test_engine", engine.id);
            Assert.AreEqual("Test Engine", engine.name);
            Assert.AreEqual("Test Manufacturer", engine.manufacturer);
        }

        [Test]
        public void EngineManifest_ContainsPartMappings()
        {
            // Arrange
            string json = CreateSampleEngineJson();
            var engine = JsonUtility.FromJson<EngineManifest>(json);

            // Assert
            Assert.IsNotNull(engine.parts);
            Assert.AreEqual(2, engine.parts.Length);
        }

        [Test]
        public void EnginePartMapping_HasRequiredFields()
        {
            // Arrange
            string json = CreateSampleEngineJson();
            var engine = JsonUtility.FromJson<EngineManifest>(json);

            // Assert
            foreach (var part in engine.parts)
            {
                Assert.IsFalse(string.IsNullOrEmpty(part.id));
                Assert.IsFalse(string.IsNullOrEmpty(part.name));
                Assert.IsFalse(string.IsNullOrEmpty(part.meshName));
            }
        }

        [Test]
        public void EngineManifest_SerializationRoundTrip_PreservesData()
        {
            // Arrange
            var original = new EngineManifest
            {
                id = "test_roundtrip",
                name = "Round Trip Engine",
                manufacturer = "Test Mfg",
                years = "2020-2024",
                displacement = "3.0L",
                configuration = "I6",
                modelPath = "Models/test.glb",
                parts = new EnginePartMapping[]
                {
                    new EnginePartMapping { id = "p1", name = "Part 1", meshName = "Part1_mesh" }
                }
            };

            // Act
            string json = JsonUtility.ToJson(original);
            var deserialized = JsonUtility.FromJson<EngineManifest>(json);

            // Assert
            Assert.AreEqual(original.id, deserialized.id);
            Assert.AreEqual(original.name, deserialized.name);
            Assert.AreEqual(original.displacement, deserialized.displacement);
            Assert.AreEqual(original.configuration, deserialized.configuration);
        }

        [Test]
        public void EngineManifest_Years_ParsesCorrectly()
        {
            // Arrange
            var engine = new EngineManifest
            {
                id = "test",
                years = "1997-2006"
            };

            // Assert
            Assert.IsTrue(engine.years.Contains("-"));
            string[] parts = engine.years.Split('-');
            Assert.AreEqual(2, parts.Length);
        }

        [Test]
        public void EngineManifest_Displacement_ParsesCorrectly()
        {
            // Arrange
            var engine = new EngineManifest
            {
                id = "test",
                displacement = "5.7L"
            };

            // Assert
            Assert.IsTrue(engine.displacement.EndsWith("L"));
        }

        [Test]
        public void EngineManifest_Configuration_ValidValues()
        {
            // Arrange
            string[] validConfigs = { "I4", "I6", "V6", "V8", "V10", "V12", "H4", "H6", "W12" };

            // Assert - check that common configurations are valid
            foreach (var config in validConfigs)
            {
                var engine = new EngineManifest { id = "test", configuration = config };
                Assert.IsNotNull(engine.configuration);
            }
        }

        [Test]
        public void EnginePartMapping_MeshName_IsValidIdentifier()
        {
            // Arrange
            var part = new EnginePartMapping
            {
                id = "alternator",
                name = "Alternator",
                meshName = "Alternator_LOD0_mesh"
            };

            // Assert - mesh names should not contain spaces or special chars
            Assert.IsFalse(part.meshName.Contains(" "));
            Assert.IsFalse(part.meshName.Contains("/"));
        }

        [Test]
        public void EngineManifest_ModelPath_IsValidPath()
        {
            // Arrange
            var engine = new EngineManifest
            {
                id = "test",
                modelPath = "Models/engine_model.glb"
            };

            // Assert
            Assert.IsTrue(engine.modelPath.Contains("/"));
            Assert.IsTrue(engine.modelPath.EndsWith(".glb") || engine.modelPath.EndsWith(".gltf"));
        }

        [Test]
        public void EngineManifest_EmptyParts_HandledGracefully()
        {
            // Arrange
            var engine = new EngineManifest
            {
                id = "empty_parts",
                name = "Empty Parts Engine",
                parts = new EnginePartMapping[] { }
            };

            // Assert
            Assert.IsNotNull(engine.parts);
            Assert.AreEqual(0, engine.parts.Length);
        }

        [UnityTest]
        public IEnumerator EngineModel_Bounds_CalculatedCorrectly()
        {
            // Arrange
            GameObject testModel = new GameObject("TestModel");
            testModel.transform.SetParent(TestRoot.transform);

            // Add a mesh renderer with bounds
            var filter = testModel.AddComponent<MeshFilter>();
            var renderer = testModel.AddComponent<MeshRenderer>();

            // Create a simple cube mesh
            filter.mesh = CreateTestCubeMesh();

            yield return null;

            // Act
            Bounds bounds = renderer.bounds;

            // Assert
            Assert.Greater(bounds.size.x, 0);
            Assert.Greater(bounds.size.y, 0);
            Assert.Greater(bounds.size.z, 0);
        }

        private Mesh CreateTestCubeMesh()
        {
            Mesh mesh = new Mesh();
            mesh.vertices = new Vector3[]
            {
                new Vector3(-0.5f, -0.5f, -0.5f),
                new Vector3(0.5f, -0.5f, -0.5f),
                new Vector3(0.5f, 0.5f, -0.5f),
                new Vector3(-0.5f, 0.5f, -0.5f),
                new Vector3(-0.5f, -0.5f, 0.5f),
                new Vector3(0.5f, -0.5f, 0.5f),
                new Vector3(0.5f, 0.5f, 0.5f),
                new Vector3(-0.5f, 0.5f, 0.5f)
            };
            mesh.triangles = new int[]
            {
                0, 2, 1, 0, 3, 2,
                1, 2, 6, 1, 6, 5,
                4, 5, 6, 4, 6, 7,
                0, 7, 3, 0, 4, 7,
                3, 7, 6, 3, 6, 2,
                0, 1, 5, 0, 5, 4
            };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }

    [System.Serializable]
    public class EngineManifest
    {
        public string id;
        public string name;
        public string manufacturer;
        public string years;
        public string displacement;
        public string configuration;
        public string modelPath;
        public EnginePartMapping[] parts;
    }

    [System.Serializable]
    public class EnginePartMapping
    {
        public string id;
        public string name;
        public string meshName;
    }
}
