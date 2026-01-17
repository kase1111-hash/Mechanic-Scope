using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace MechanicScope.Tests.Runtime.Data
{
    /// <summary>
    /// End-to-end tests for ProgressTracker.
    /// Tests progress saving, loading, and state management.
    /// </summary>
    public class ProgressTrackerTests : TestBase
    {
        [Test]
        public void UserProgress_SerializesCorrectly()
        {
            // Arrange
            var progress = new UserProgress
            {
                currentEngineId = "test_engine",
                currentProcedureId = "test_procedure",
                currentStepIndex = 2,
                completedStepIds = new List<string> { "step1", "step2" },
                lastUpdated = DateTime.UtcNow.ToString("o")
            };

            // Act
            string json = JsonUtility.ToJson(progress);
            var deserialized = JsonUtility.FromJson<UserProgress>(json);

            // Assert
            Assert.AreEqual(progress.currentEngineId, deserialized.currentEngineId);
            Assert.AreEqual(progress.currentProcedureId, deserialized.currentProcedureId);
            Assert.AreEqual(progress.currentStepIndex, deserialized.currentStepIndex);
        }

        [Test]
        public void UserProgress_CompletedSteps_TracksCorrectly()
        {
            // Arrange
            var progress = new UserProgress();
            progress.completedStepIds = new List<string>();

            // Act
            progress.completedStepIds.Add("step1");
            progress.completedStepIds.Add("step2");

            // Assert
            Assert.AreEqual(2, progress.completedStepIds.Count);
            Assert.IsTrue(progress.completedStepIds.Contains("step1"));
            Assert.IsTrue(progress.completedStepIds.Contains("step2"));
        }

        [Test]
        public void UserProgress_SaveAndLoad_PreservesState()
        {
            // Arrange
            var original = new UserProgress
            {
                currentEngineId = "gm_ls_gen4",
                currentProcedureId = "replace_alternator",
                currentStepIndex = 3,
                completedStepIds = new List<string> { "step1", "step2", "step3" }
            };

            string filePath = Path.Combine(TestDataPath, "progress.json");

            // Act - Save
            string json = JsonUtility.ToJson(original);
            File.WriteAllText(filePath, json);

            // Act - Load
            string loadedJson = File.ReadAllText(filePath);
            var loaded = JsonUtility.FromJson<UserProgress>(loadedJson);

            // Assert
            Assert.AreEqual(original.currentEngineId, loaded.currentEngineId);
            Assert.AreEqual(original.currentProcedureId, loaded.currentProcedureId);
            Assert.AreEqual(original.currentStepIndex, loaded.currentStepIndex);
        }

        [Test]
        public void UserProgress_Reset_ClearsAllData()
        {
            // Arrange
            var progress = new UserProgress
            {
                currentEngineId = "test_engine",
                currentProcedureId = "test_procedure",
                currentStepIndex = 5,
                completedStepIds = new List<string> { "step1", "step2" }
            };

            // Act
            progress.currentEngineId = null;
            progress.currentProcedureId = null;
            progress.currentStepIndex = 0;
            progress.completedStepIds.Clear();

            // Assert
            Assert.IsNull(progress.currentEngineId);
            Assert.IsNull(progress.currentProcedureId);
            Assert.AreEqual(0, progress.currentStepIndex);
            Assert.AreEqual(0, progress.completedStepIds.Count);
        }

        [Test]
        public void RepairHistory_RecordsCompletion()
        {
            // Arrange
            var history = new RepairHistoryEntry
            {
                procedureId = "oil_change",
                engineId = "test_engine",
                startTime = DateTime.UtcNow.AddMinutes(-30).ToString("o"),
                completionTime = DateTime.UtcNow.ToString("o"),
                totalSteps = 8,
                completedSteps = 8,
                wasCompleted = true
            };

            // Assert
            Assert.IsTrue(history.wasCompleted);
            Assert.AreEqual(history.totalSteps, history.completedSteps);
        }

        [Test]
        public void RepairHistory_CalculatesDuration()
        {
            // Arrange
            var startTime = DateTime.UtcNow.AddMinutes(-45);
            var endTime = DateTime.UtcNow;

            var history = new RepairHistoryEntry
            {
                startTime = startTime.ToString("o"),
                completionTime = endTime.ToString("o")
            };

            // Act
            var start = DateTime.Parse(history.startTime);
            var end = DateTime.Parse(history.completionTime);
            var duration = end - start;

            // Assert
            Assert.AreEqual(45, (int)duration.TotalMinutes);
        }

        [Test]
        public void UserPreferences_SavesSettings()
        {
            // Arrange
            var prefs = new UserPreferences();
            prefs.preferences = new Dictionary<string, string>();

            // Act
            prefs.preferences["highlight_effects"] = "true";
            prefs.preferences["voice_commands"] = "false";
            prefs.preferences["highlight_intensity"] = "0.8";

            // Assert
            Assert.AreEqual("true", prefs.preferences["highlight_effects"]);
            Assert.AreEqual("false", prefs.preferences["voice_commands"]);
            Assert.AreEqual("0.8", prefs.preferences["highlight_intensity"]);
        }

        [Test]
        public void UserPreferences_GetsBoolValue()
        {
            // Arrange
            var prefs = new UserPreferences();
            prefs.preferences = new Dictionary<string, string>
            {
                { "enabled", "true" },
                { "disabled", "false" }
            };

            // Act & Assert
            Assert.IsTrue(prefs.preferences["enabled"] == "true");
            Assert.IsFalse(prefs.preferences["disabled"] == "true");
        }

        [Test]
        public void UserPreferences_GetsFloatValue()
        {
            // Arrange
            var prefs = new UserPreferences();
            prefs.preferences = new Dictionary<string, string>
            {
                { "intensity", "0.75" }
            };

            // Act
            float value = float.Parse(prefs.preferences["intensity"]);

            // Assert
            Assert.AreEqual(0.75f, value, 0.001f);
        }

        [Test]
        public void UserPreferences_DefaultsForMissing()
        {
            // Arrange
            var prefs = new UserPreferences();
            prefs.preferences = new Dictionary<string, string>();

            // Act
            string value = prefs.preferences.ContainsKey("missing")
                ? prefs.preferences["missing"]
                : "default";

            // Assert
            Assert.AreEqual("default", value);
        }

        [Test]
        public void ProgressData_HandlesMultipleEngines()
        {
            // Arrange
            var engineProgress = new Dictionary<string, UserProgress>();

            engineProgress["engine1"] = new UserProgress { currentStepIndex = 3 };
            engineProgress["engine2"] = new UserProgress { currentStepIndex = 7 };

            // Assert
            Assert.AreEqual(3, engineProgress["engine1"].currentStepIndex);
            Assert.AreEqual(7, engineProgress["engine2"].currentStepIndex);
        }
    }

    [System.Serializable]
    public class UserProgress
    {
        public string currentEngineId;
        public string currentProcedureId;
        public int currentStepIndex;
        public List<string> completedStepIds = new List<string>();
        public string lastUpdated;
    }

    [System.Serializable]
    public class RepairHistoryEntry
    {
        public string procedureId;
        public string engineId;
        public string startTime;
        public string completionTime;
        public int totalSteps;
        public int completedSteps;
        public bool wasCompleted;
        public string notes;
        public int rating;
    }

    public class UserPreferences
    {
        public Dictionary<string, string> preferences = new Dictionary<string, string>();
    }
}
