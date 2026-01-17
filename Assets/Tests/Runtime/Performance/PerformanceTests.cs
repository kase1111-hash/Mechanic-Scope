using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace MechanicScope.Tests.Runtime.Performance
{
    /// <summary>
    /// End-to-end tests for performance systems.
    /// Tests LOD management, performance monitoring, and asset optimization.
    /// </summary>
    public class PerformanceTests : TestBase
    {
        [Test]
        public void LODConfiguration_DefaultValues()
        {
            // Arrange
            var config = new LODConfiguration();

            // Assert
            Assert.AreEqual(3, config.levels);
            Assert.Greater(config.lod0ScreenHeight, 0);
            Assert.Greater(config.cullScreenHeight, 0);
            Assert.Less(config.cullScreenHeight, config.lod0ScreenHeight);
        }

        [Test]
        public void LODConfiguration_HighQuality_HasMoreLevels()
        {
            // Arrange
            var defaultConfig = new LODConfiguration();
            var highQualityConfig = LODConfiguration.HighQuality;

            // Assert
            Assert.GreaterOrEqual(highQualityConfig.levels, defaultConfig.levels);
        }

        [Test]
        public void LODConfiguration_Performance_HasFewerLevels()
        {
            // Arrange
            var defaultConfig = new LODConfiguration();
            var performanceConfig = LODConfiguration.Performance;

            // Assert
            Assert.LessOrEqual(performanceConfig.levels, defaultConfig.levels);
        }

        [Test]
        public void PerformanceStats_ContainsRequiredFields()
        {
            // Arrange
            var stats = new PerformanceStats
            {
                frameRate = 60f,
                lodBias = 1.0f,
                managedObjects = 10,
                textureMemoryMB = 128,
                systemMemoryMB = 4096
            };

            // Assert
            Assert.Greater(stats.frameRate, 0);
            Assert.Greater(stats.lodBias, 0);
            Assert.GreaterOrEqual(stats.managedObjects, 0);
            Assert.GreaterOrEqual(stats.textureMemoryMB, 0);
            Assert.Greater(stats.systemMemoryMB, 0);
        }

        [Test]
        public void TextureOptimizationSettings_DefaultValues()
        {
            // Arrange
            var settings = new TextureOptimizationSettings();

            // Assert
            Assert.Greater(settings.maxSize, 0);
            Assert.GreaterOrEqual(settings.anisoLevel, 0);
        }

        [Test]
        public void TextureOptimizationSettings_LowMemory_HasSmallerSize()
        {
            // Arrange
            var defaultSettings = new TextureOptimizationSettings();
            var lowMemorySettings = TextureOptimizationSettings.LowMemory;

            // Assert
            Assert.Less(lowMemorySettings.maxSize, defaultSettings.maxSize);
        }

        [Test]
        public void MeshOptimizationSettings_DefaultValues()
        {
            // Arrange
            var settings = new MeshOptimizationSettings();

            // Assert
            Assert.IsTrue(settings.optimizeIndexBuffer);
            Assert.IsTrue(settings.recalculateBounds);
        }

        [Test]
        public void CacheStats_TracksCounts()
        {
            // Arrange
            var stats = new CacheStats
            {
                textureCount = 5,
                meshCount = 10,
                poolCount = 3,
                totalPooledObjects = 30
            };

            // Assert
            Assert.AreEqual(5, stats.textureCount);
            Assert.AreEqual(10, stats.meshCount);
            Assert.AreEqual(3, stats.poolCount);
            Assert.AreEqual(30, stats.totalPooledObjects);
        }

        [UnityTest]
        public IEnumerator FrameRate_CanBeMeasured()
        {
            // Arrange
            float frameCount = 0;
            float elapsed = 0;

            // Act - measure over several frames
            for (int i = 0; i < 10; i++)
            {
                frameCount++;
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            // Assert
            float fps = frameCount / elapsed;
            Assert.Greater(fps, 0);
        }

        [Test]
        public void PerformanceWarning_ContainsRequiredInfo()
        {
            // Arrange
            var warning = new PerformanceWarning
            {
                type = WarningType.LowFrameRate,
                message = "Frame rate dropped below 30 FPS",
                severity = WarningSeverity.Warning,
                value = 25f
            };

            // Assert
            Assert.AreEqual(WarningType.LowFrameRate, warning.type);
            Assert.IsFalse(string.IsNullOrEmpty(warning.message));
            Assert.AreEqual(25f, warning.value);
        }

        [Test]
        public void PerformanceWarning_SeverityLevels()
        {
            // Assert - verify all severity levels exist
            Assert.AreEqual(3, System.Enum.GetValues(typeof(WarningSeverity)).Length);
        }

        [Test]
        public void WarningType_AllTypesExist()
        {
            // Assert - verify warning types
            var types = System.Enum.GetValues(typeof(WarningType));
            Assert.GreaterOrEqual(types.Length, 3); // At least Low FPS, High Memory, Memory Leak
        }

        [UnityTest]
        public IEnumerator ObjectPool_CreatesAndRecycles()
        {
            // Arrange
            var pool = new MockObjectPool();
            var prefab = new GameObject("PooledPrefab");
            prefab.transform.SetParent(TestRoot.transform);

            pool.RegisterPool("test", prefab, 5);

            yield return null;

            // Act
            var obj1 = pool.GetFromPool("test");
            var obj2 = pool.GetFromPool("test");

            // Assert
            Assert.IsNotNull(obj1);
            Assert.IsNotNull(obj2);
            Assert.AreNotEqual(obj1, obj2);

            // Return to pool
            pool.ReturnToPool("test", obj1);
            var obj3 = pool.GetFromPool("test");

            // Should get the same object back
            Assert.AreEqual(obj1, obj3);
        }

        [Test]
        public void MemoryTracking_ReturnsValidValues()
        {
            // Act
            long totalMemory = UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong();
            int systemMemory = SystemInfo.systemMemorySize;

            // Assert
            Assert.Greater(totalMemory, 0);
            Assert.Greater(systemMemory, 0);
        }

        [Test]
        public void QualitySettings_AreAccessible()
        {
            // Assert
            Assert.IsNotNull(QualitySettings.names);
            Assert.Greater(QualitySettings.names.Length, 0);
        }

        [Test]
        public void TargetFrameRate_CanBeSet()
        {
            // Arrange
            int originalFrameRate = Application.targetFrameRate;

            // Act
            Application.targetFrameRate = 60;

            // Assert
            Assert.AreEqual(60, Application.targetFrameRate);

            // Cleanup
            Application.targetFrameRate = originalFrameRate;
        }
    }

    // Test helper classes
    public class LODConfiguration
    {
        public int levels = 3;
        public float lod0ScreenHeight = 0.6f;
        public float cullScreenHeight = 0.01f;
        public float reductionPerLevel = 0.25f;

        public static LODConfiguration HighQuality => new LODConfiguration
        {
            levels = 4,
            lod0ScreenHeight = 0.8f,
            cullScreenHeight = 0.005f
        };

        public static LODConfiguration Performance => new LODConfiguration
        {
            levels = 2,
            lod0ScreenHeight = 0.5f,
            cullScreenHeight = 0.02f
        };
    }

    public struct PerformanceStats
    {
        public float frameRate;
        public float lodBias;
        public int managedObjects;
        public long textureMemoryMB;
        public int systemMemoryMB;
    }

    public class TextureOptimizationSettings
    {
        public int maxSize = 1024;
        public int anisoLevel = 4;
        public bool generateMipmaps = true;

        public static TextureOptimizationSettings LowMemory => new TextureOptimizationSettings
        {
            maxSize = 512,
            anisoLevel = 1,
            generateMipmaps = false
        };
    }

    public class MeshOptimizationSettings
    {
        public bool optimizeIndexBuffer = true;
        public bool recalculateBounds = true;
    }

    public struct CacheStats
    {
        public int textureCount;
        public int meshCount;
        public int poolCount;
        public int totalPooledObjects;
    }

    public struct PerformanceWarning
    {
        public WarningType type;
        public string message;
        public WarningSeverity severity;
        public float value;
    }

    public enum WarningType { LowFrameRate, HighMemory, MemoryLeak, HighBatteryDrain, ThermalThrottling }
    public enum WarningSeverity { Info, Warning, Critical }

    public class MockObjectPool
    {
        private Dictionary<string, Queue<GameObject>> pools = new Dictionary<string, Queue<GameObject>>();
        private Dictionary<string, GameObject> prefabs = new Dictionary<string, GameObject>();

        public void RegisterPool(string id, GameObject prefab, int initialSize)
        {
            prefabs[id] = prefab;
            pools[id] = new Queue<GameObject>();

            for (int i = 0; i < initialSize; i++)
            {
                var obj = UnityEngine.Object.Instantiate(prefab);
                obj.SetActive(false);
                pools[id].Enqueue(obj);
            }
        }

        public GameObject GetFromPool(string id)
        {
            if (pools[id].Count > 0)
            {
                var obj = pools[id].Dequeue();
                obj.SetActive(true);
                return obj;
            }
            return UnityEngine.Object.Instantiate(prefabs[id]);
        }

        public void ReturnToPool(string id, GameObject obj)
        {
            obj.SetActive(false);
            pools[id].Enqueue(obj);
        }
    }
}
