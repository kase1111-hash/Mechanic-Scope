using System;
using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace MechanicScope.Tests.Runtime
{
    /// <summary>
    /// Base class for all MechanicScope tests.
    /// Provides common utilities and setup/teardown functionality.
    /// </summary>
    public abstract class TestBase
    {
        protected string TestDataPath { get; private set; }
        protected GameObject TestRoot { get; private set; }

        [SetUp]
        public virtual void SetUp()
        {
            // Create test data directory
            TestDataPath = Path.Combine(Application.temporaryCachePath, "TestData_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(TestDataPath);

            // Create root game object for test objects
            TestRoot = new GameObject("TestRoot");
        }

        [TearDown]
        public virtual void TearDown()
        {
            // Clean up test objects
            if (TestRoot != null)
            {
                UnityEngine.Object.DestroyImmediate(TestRoot);
            }

            // Clean up test data directory
            if (Directory.Exists(TestDataPath))
            {
                try
                {
                    Directory.Delete(TestDataPath, true);
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"Failed to clean up test data: {e.Message}");
                }
            }
        }

        /// <summary>
        /// Creates a test component attached to the test root.
        /// </summary>
        protected T CreateTestComponent<T>() where T : Component
        {
            return TestRoot.AddComponent<T>();
        }

        /// <summary>
        /// Creates a new game object with the specified component.
        /// </summary>
        protected T CreateGameObjectWithComponent<T>(string name = null) where T : Component
        {
            GameObject go = new GameObject(name ?? typeof(T).Name);
            go.transform.SetParent(TestRoot.transform);
            return go.AddComponent<T>();
        }

        /// <summary>
        /// Writes test data to a file.
        /// </summary>
        protected void WriteTestFile(string filename, string content)
        {
            string path = Path.Combine(TestDataPath, filename);
            string directory = Path.GetDirectoryName(path);
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
            File.WriteAllText(path, content);
        }

        /// <summary>
        /// Reads test data from a file.
        /// </summary>
        protected string ReadTestFile(string filename)
        {
            string path = Path.Combine(TestDataPath, filename);
            return File.ReadAllText(path);
        }

        /// <summary>
        /// Creates sample procedure JSON for testing.
        /// </summary>
        protected string CreateSampleProcedureJson()
        {
            return @"{
                ""id"": ""test_procedure"",
                ""name"": ""Test Procedure"",
                ""description"": ""A test procedure for unit testing"",
                ""engineId"": ""test_engine"",
                ""estimatedTime"": 30,
                ""difficulty"": ""beginner"",
                ""steps"": [
                    {
                        ""id"": ""step1"",
                        ""title"": ""First Step"",
                        ""instruction"": ""Do the first thing"",
                        ""partIds"": [""part1""],
                        ""dependencies"": []
                    },
                    {
                        ""id"": ""step2"",
                        ""title"": ""Second Step"",
                        ""instruction"": ""Do the second thing"",
                        ""partIds"": [""part2""],
                        ""dependencies"": [""step1""]
                    }
                ]
            }";
        }

        /// <summary>
        /// Creates sample engine manifest JSON for testing.
        /// </summary>
        protected string CreateSampleEngineJson()
        {
            return @"{
                ""id"": ""test_engine"",
                ""name"": ""Test Engine"",
                ""manufacturer"": ""Test Manufacturer"",
                ""years"": ""2020-2024"",
                ""displacement"": ""5.0L"",
                ""configuration"": ""V8"",
                ""modelPath"": ""Models/test_engine.glb"",
                ""parts"": [
                    {
                        ""id"": ""part1"",
                        ""name"": ""Test Part 1"",
                        ""meshName"": ""TestPart1_mesh""
                    },
                    {
                        ""id"": ""part2"",
                        ""name"": ""Test Part 2"",
                        ""meshName"": ""TestPart2_mesh""
                    }
                ]
            }";
        }

        /// <summary>
        /// Creates sample parts database JSON for testing.
        /// </summary>
        protected string CreateSamplePartsJson()
        {
            return @"{
                ""parts"": [
                    {
                        ""id"": ""part1"",
                        ""name"": ""Test Part 1"",
                        ""category"": ""engine"",
                        ""description"": ""A test part for testing""
                    },
                    {
                        ""id"": ""part2"",
                        ""name"": ""Test Part 2"",
                        ""category"": ""engine"",
                        ""description"": ""Another test part""
                    }
                ]
            }";
        }

        /// <summary>
        /// Waits for a condition to be true or times out.
        /// </summary>
        protected IEnumerator WaitForCondition(Func<bool> condition, float timeout = 5f)
        {
            float elapsed = 0;
            while (!condition() && elapsed < timeout)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }

            if (!condition())
            {
                Assert.Fail($"Condition not met within {timeout} seconds");
            }
        }

        /// <summary>
        /// Asserts that an action throws an exception.
        /// </summary>
        protected void AssertThrows<T>(Action action) where T : Exception
        {
            try
            {
                action();
                Assert.Fail($"Expected exception of type {typeof(T).Name}");
            }
            catch (T)
            {
                // Expected
            }
        }
    }
}
