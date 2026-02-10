using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using MechanicScope.Core;

namespace MechanicScope.Tests
{
    /// <summary>
    /// Tests PartDatabase: import from JSON, search, category filtering,
    /// engine association, add/update, export. Uses real GameObjects.
    /// </summary>
    [TestFixture]
    public class PartDatabaseTests
    {
        private GameObject dbGO;
        private PartDatabase partDb;

        // Subset of the real DefaultPartsData.json schema
        private const string SamplePartsJson = @"{
            ""parts"": [
                {
                    ""id"": ""alternator_gm_ls"",
                    ""name"": ""Alternator"",
                    ""description"": ""Generates electrical power for the vehicle"",
                    ""category"": ""electrical"",
                    ""crossReferences"": [""ACDelco 334-2742"", ""Denso 210-0580""],
                    ""engines"": [""gm_ls_gen4""],
                    ""specs"": [
                        { ""key"": ""voltage"", ""value"": ""14.2V"" },
                        { ""key"": ""amperage"", ""value"": ""145A"" }
                    ]
                },
                {
                    ""id"": ""serpentine_belt_gm_ls"",
                    ""name"": ""Serpentine Belt"",
                    ""description"": ""Drives alternator, power steering, and AC compressor"",
                    ""category"": ""mechanical"",
                    ""engines"": [""gm_ls_gen4""],
                    ""specs"": [
                        { ""key"": ""length"", ""value"": ""92.5 inches"" }
                    ]
                },
                {
                    ""id"": ""oil_filter_gm_ls"",
                    ""name"": ""Oil Filter"",
                    ""description"": ""Filters contaminants from engine oil"",
                    ""category"": ""fluid"",
                    ""engines"": [""gm_ls_gen4"", ""gm_ls_gen3""],
                    ""specs"": [
                        { ""key"": ""type"", ""value"": ""spin-on"" }
                    ]
                }
            ]
        }";

        [SetUp]
        public void SetUp()
        {
            dbGO = new GameObject("TestPartDatabase");
            partDb = dbGO.AddComponent<PartDatabase>();
            // Note: Awake() calls LoadDefaultData() which requires a TextAsset.
            // Since we're in edit mode without one, the DB starts empty. We import manually.
            partDb.ImportPartData(SamplePartsJson);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(dbGO);
        }

        // === Import ===

        [Test]
        public void ImportPartData_LoadsCorrectCount()
        {
            Assert.AreEqual(3, partDb.PartCount);
        }

        [Test]
        public void ImportPartData_EmptyJson_DoesNotCrash()
        {
            var emptyDbGO = new GameObject("EmptyDB");
            var emptyDb = emptyDbGO.AddComponent<PartDatabase>();
            emptyDb.ImportPartData("");

            Assert.AreEqual(0, emptyDb.PartCount);
            Object.DestroyImmediate(emptyDbGO);
        }

        [Test]
        public void ImportPartData_NullPartsArray_DoesNotCrash()
        {
            var emptyDbGO = new GameObject("NullPartsDB");
            var emptyDb = emptyDbGO.AddComponent<PartDatabase>();
            emptyDb.ImportPartData(@"{ ""parts"": null }");

            Assert.AreEqual(0, emptyDb.PartCount);
            Object.DestroyImmediate(emptyDbGO);
        }

        [Test]
        public void ImportPartData_PartWithNoId_Skipped()
        {
            var testGO = new GameObject("SkipDB");
            var testDb = testGO.AddComponent<PartDatabase>();
            testDb.ImportPartData(@"{ ""parts"": [ { ""name"": ""No ID Part"" } ] }");

            Assert.AreEqual(0, testDb.PartCount);
            Object.DestroyImmediate(testGO);
        }

        // === Lookup ===

        [Test]
        public void GetPart_ValidId_ReturnsPart()
        {
            PartInfo part = partDb.GetPart("alternator_gm_ls");

            Assert.IsNotNull(part);
            Assert.AreEqual("Alternator", part.Name);
            Assert.AreEqual("electrical", part.Category);
            Assert.AreEqual("Generates electrical power for the vehicle", part.Description);
        }

        [Test]
        public void GetPart_InvalidId_ReturnsNull()
        {
            Assert.IsNull(partDb.GetPart("nonexistent"));
        }

        [Test]
        public void GetPart_NullId_ReturnsNull()
        {
            Assert.IsNull(partDb.GetPart(null));
        }

        // === Specs ===

        [Test]
        public void PartSpecs_ParsedCorrectly()
        {
            PartInfo part = partDb.GetPart("alternator_gm_ls");

            Assert.AreEqual("14.2V", part.GetSpec("voltage"));
            Assert.AreEqual("145A", part.GetSpec("amperage"));
        }

        [Test]
        public void PartSpecs_MissingKey_ReturnsDefault()
        {
            PartInfo part = partDb.GetPart("alternator_gm_ls");

            Assert.IsNull(part.GetSpec("nonexistent"));
            Assert.AreEqual("N/A", part.GetSpec("nonexistent", "N/A"));
        }

        [Test]
        public void PartSpecs_GetFormattedSpecs_ContainsAllSpecs()
        {
            PartInfo part = partDb.GetPart("alternator_gm_ls");
            string formatted = part.GetFormattedSpecs();

            Assert.IsTrue(formatted.Contains("voltage: 14.2V"));
            Assert.IsTrue(formatted.Contains("amperage: 145A"));
        }

        // === Cross References ===

        [Test]
        public void CrossReferences_ParsedCorrectly()
        {
            PartInfo part = partDb.GetPart("alternator_gm_ls");

            Assert.AreEqual(2, part.CrossReferences.Count);
            Assert.IsTrue(part.CrossReferences.Contains("ACDelco 334-2742"));
            Assert.IsTrue(part.CrossReferences.Contains("Denso 210-0580"));
        }

        // === Search ===

        [Test]
        public void SearchParts_ByName_FindsMatch()
        {
            List<PartInfo> results = partDb.SearchParts("alternator");

            Assert.AreEqual(1, results.Count);
            Assert.AreEqual("alternator_gm_ls", results[0].Id);
        }

        [Test]
        public void SearchParts_ByDescription_FindsMatch()
        {
            List<PartInfo> results = partDb.SearchParts("contaminants");

            Assert.AreEqual(1, results.Count);
            Assert.AreEqual("oil_filter_gm_ls", results[0].Id);
        }

        [Test]
        public void SearchParts_CaseInsensitive()
        {
            List<PartInfo> results = partDb.SearchParts("ALTERNATOR");

            Assert.AreEqual(1, results.Count);
        }

        [Test]
        public void SearchParts_NoMatch_ReturnsEmpty()
        {
            List<PartInfo> results = partDb.SearchParts("turbocharger");

            Assert.AreEqual(0, results.Count);
        }

        [Test]
        public void SearchParts_EmptyQuery_ReturnsEmpty()
        {
            Assert.AreEqual(0, partDb.SearchParts("").Count);
            Assert.AreEqual(0, partDb.SearchParts(null).Count);
        }

        // === Engine Association ===

        [Test]
        public void GetPartsForEngine_ReturnsAssociatedParts()
        {
            List<PartInfo> parts = partDb.GetPartsForEngine("gm_ls_gen4");

            Assert.AreEqual(3, parts.Count);
        }

        [Test]
        public void GetPartsForEngine_MultiEngineAssociation()
        {
            List<PartInfo> gen3Parts = partDb.GetPartsForEngine("gm_ls_gen3");

            Assert.AreEqual(1, gen3Parts.Count);
            Assert.AreEqual("oil_filter_gm_ls", gen3Parts[0].Id);
        }

        [Test]
        public void GetPartsForEngine_UnknownEngine_ReturnsEmpty()
        {
            Assert.AreEqual(0, partDb.GetPartsForEngine("unknown_engine").Count);
        }

        // === Categories ===

        [Test]
        public void GetCategories_ReturnsAllCategories()
        {
            List<string> categories = partDb.GetCategories();

            Assert.AreEqual(3, categories.Count);
            Assert.IsTrue(categories.Contains("electrical"));
            Assert.IsTrue(categories.Contains("mechanical"));
            Assert.IsTrue(categories.Contains("fluid"));
        }

        [Test]
        public void GetPartsByCategory_FiltersCorrectly()
        {
            List<PartInfo> electrical = partDb.GetPartsByCategory("electrical");

            Assert.AreEqual(1, electrical.Count);
            Assert.AreEqual("alternator_gm_ls", electrical[0].Id);
        }

        [Test]
        public void GetPartsByCategory_UnknownCategory_ReturnsEmpty()
        {
            Assert.AreEqual(0, partDb.GetPartsByCategory("imaginary").Count);
        }

        // === Add/Update ===

        [Test]
        public void AddOrUpdatePart_NewPart_IncreasesCount()
        {
            var newPart = new PartInfo
            {
                Id = "new_part",
                Name = "New Part",
                Category = "mechanical",
                Specs = new Dictionary<string, string>()
            };

            partDb.AddOrUpdatePart(newPart);

            Assert.AreEqual(4, partDb.PartCount);
            Assert.IsNotNull(partDb.GetPart("new_part"));
        }

        [Test]
        public void AddOrUpdatePart_ExistingPart_UpdatesIt()
        {
            var updated = new PartInfo
            {
                Id = "alternator_gm_ls",
                Name = "Updated Alternator",
                Category = "electrical",
                Specs = new Dictionary<string, string>()
            };

            partDb.AddOrUpdatePart(updated);

            Assert.AreEqual(3, partDb.PartCount);
            Assert.AreEqual("Updated Alternator", partDb.GetPart("alternator_gm_ls").Name);
        }

        [Test]
        public void AddOrUpdatePart_NullPart_DoesNothing()
        {
            partDb.AddOrUpdatePart(null);
            Assert.AreEqual(3, partDb.PartCount);
        }

        // === Export ===

        [Test]
        public void ExportToJson_RoundTrips()
        {
            string json = partDb.ExportToJson();

            var roundTripGO = new GameObject("RoundTrip");
            var roundTripDb = roundTripGO.AddComponent<PartDatabase>();
            roundTripDb.ImportPartData(json);

            Assert.AreEqual(partDb.PartCount, roundTripDb.PartCount);
            Assert.IsNotNull(roundTripDb.GetPart("alternator_gm_ls"));
            Assert.IsNotNull(roundTripDb.GetPart("serpentine_belt_gm_ls"));

            Object.DestroyImmediate(roundTripGO);
        }

        // === Clear ===

        [Test]
        public void ClearDatabase_RemovesEverything()
        {
            partDb.ClearDatabase();

            Assert.AreEqual(0, partDb.PartCount);
            Assert.IsNull(partDb.GetPart("alternator_gm_ls"));
            Assert.AreEqual(0, partDb.GetCategories().Count);
        }
    }
}
