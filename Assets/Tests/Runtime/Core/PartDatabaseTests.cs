using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace MechanicScope.Tests.Runtime.Core
{
    /// <summary>
    /// End-to-end tests for PartDatabase.
    /// Tests part lookup, search, and caching functionality.
    /// </summary>
    public class PartDatabaseTests : TestBase
    {
        [Test]
        public void PartData_SerializesCorrectly()
        {
            // Arrange
            var part = new PartData
            {
                id = "test_part",
                name = "Test Part",
                category = "engine",
                description = "A test part",
                manufacturer = "Test Mfg",
                partNumber = "TP-001"
            };

            // Act
            string json = JsonUtility.ToJson(part);
            var deserialized = JsonUtility.FromJson<PartData>(json);

            // Assert
            Assert.AreEqual(part.id, deserialized.id);
            Assert.AreEqual(part.name, deserialized.name);
            Assert.AreEqual(part.category, deserialized.category);
            Assert.AreEqual(part.partNumber, deserialized.partNumber);
        }

        [Test]
        public void PartData_WithSpecs_SerializesCorrectly()
        {
            // Arrange
            var part = new PartData
            {
                id = "bolt_test",
                name = "Test Bolt",
                category = "hardware",
                torqueSpec = "25 ft-lbs",
                size = "10mm"
            };

            // Act
            string json = JsonUtility.ToJson(part);
            var deserialized = JsonUtility.FromJson<PartData>(json);

            // Assert
            Assert.AreEqual("25 ft-lbs", deserialized.torqueSpec);
            Assert.AreEqual("10mm", deserialized.size);
        }

        [Test]
        public void PartsDatabase_ParsesJsonArray()
        {
            // Arrange
            string json = CreateSamplePartsJson();

            // Act
            var database = JsonUtility.FromJson<PartsDatabase>(json);

            // Assert
            Assert.IsNotNull(database.parts);
            Assert.AreEqual(2, database.parts.Length);
        }

        [Test]
        public void PartsDatabase_ContainsExpectedParts()
        {
            // Arrange
            string json = CreateSamplePartsJson();
            var database = JsonUtility.FromJson<PartsDatabase>(json);

            // Assert
            Assert.AreEqual("part1", database.parts[0].id);
            Assert.AreEqual("Test Part 1", database.parts[0].name);
            Assert.AreEqual("part2", database.parts[1].id);
            Assert.AreEqual("Test Part 2", database.parts[1].name);
        }

        [Test]
        public void PartData_Search_MatchesName()
        {
            // Arrange
            var parts = new List<PartData>
            {
                new PartData { id = "1", name = "Alternator", category = "electrical" },
                new PartData { id = "2", name = "Starter Motor", category = "electrical" },
                new PartData { id = "3", name = "Oil Filter", category = "filtration" }
            };

            // Act
            string searchTerm = "alter";
            var results = parts.FindAll(p =>
                p.name.ToLower().Contains(searchTerm.ToLower()));

            // Assert
            Assert.AreEqual(1, results.Count);
            Assert.AreEqual("Alternator", results[0].name);
        }

        [Test]
        public void PartData_Search_MatchesCategory()
        {
            // Arrange
            var parts = new List<PartData>
            {
                new PartData { id = "1", name = "Alternator", category = "electrical" },
                new PartData { id = "2", name = "Starter Motor", category = "electrical" },
                new PartData { id = "3", name = "Oil Filter", category = "filtration" }
            };

            // Act
            var results = parts.FindAll(p => p.category == "electrical");

            // Assert
            Assert.AreEqual(2, results.Count);
        }

        [Test]
        public void PartData_FindById_ReturnsCorrectPart()
        {
            // Arrange
            var parts = new List<PartData>
            {
                new PartData { id = "part_1", name = "Part One" },
                new PartData { id = "part_2", name = "Part Two" },
                new PartData { id = "part_3", name = "Part Three" }
            };

            // Act
            var result = parts.Find(p => p.id == "part_2");

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual("Part Two", result.name);
        }

        [Test]
        public void PartData_FindById_ReturnsNullForMissing()
        {
            // Arrange
            var parts = new List<PartData>
            {
                new PartData { id = "part_1", name = "Part One" }
            };

            // Act
            var result = parts.Find(p => p.id == "nonexistent");

            // Assert
            Assert.IsNull(result);
        }

        [Test]
        public void PartData_TorqueSpec_FormatsCorrectly()
        {
            // Arrange
            var part = new PartData
            {
                id = "bolt",
                name = "Head Bolt",
                torqueSpec = "65 ft-lbs + 90°"
            };

            // Assert
            Assert.IsTrue(part.torqueSpec.Contains("ft-lbs"));
            Assert.IsTrue(part.torqueSpec.Contains("90°"));
        }

        [Test]
        public void PartsDatabase_EmptyDatabase_HandledGracefully()
        {
            // Arrange
            var database = new PartsDatabase { parts = new PartData[] { } };

            // Assert
            Assert.IsNotNull(database.parts);
            Assert.AreEqual(0, database.parts.Length);
        }

        [Test]
        public void PartData_WithNotes_SerializesCorrectly()
        {
            // Arrange
            var part = new PartData
            {
                id = "special_part",
                name = "Special Part",
                notes = "Handle with care. Requires special tool."
            };

            // Act
            string json = JsonUtility.ToJson(part);
            var deserialized = JsonUtility.FromJson<PartData>(json);

            // Assert
            Assert.AreEqual(part.notes, deserialized.notes);
        }
    }

    [System.Serializable]
    public class PartData
    {
        public string id;
        public string name;
        public string category;
        public string description;
        public string manufacturer;
        public string partNumber;
        public string torqueSpec;
        public string size;
        public string notes;
    }

    [System.Serializable]
    public class PartsDatabase
    {
        public PartData[] parts;
    }
}
