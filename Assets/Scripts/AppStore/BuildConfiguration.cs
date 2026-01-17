using System;
using UnityEngine;

namespace MechanicScope.AppStore
{
    /// <summary>
    /// Build configuration for different environments and platforms.
    /// </summary>
    [CreateAssetMenu(fileName = "BuildConfiguration", menuName = "MechanicScope/Build Configuration")]
    public class BuildConfiguration : ScriptableObject
    {
        [Header("Environment")]
        public BuildEnvironment environment = BuildEnvironment.Development;

        [Header("iOS Settings")]
        public iOSSettings iOS = new iOSSettings();

        [Header("Android Settings")]
        public AndroidSettings android = new AndroidSettings();

        [Header("Debug Settings")]
        public DebugSettings debug = new DebugSettings();

        [Header("Feature Toggles")]
        public FeatureToggles features = new FeatureToggles();

        public enum BuildEnvironment
        {
            Development,
            Staging,
            Production
        }

        [Serializable]
        public class iOSSettings
        {
            public string teamId = "";
            public string signingCertificate = "iPhone Distribution";
            public string provisioningProfile = "";
            public iOSTargetDevice targetDevice = iOSTargetDevice.iPhoneAndiPad;
            public bool automaticallySign = true;
            public bool enableBitcode = false;
            public string minimumVersion = "14.0";

            [Header("Capabilities")]
            public bool enableARKit = true;
            public bool enableCamera = true;
            public bool enableMicrophone = true;
            public bool enableFileSaving = true;

            public enum iOSTargetDevice
            {
                iPhone,
                iPad,
                iPhoneAndiPad
            }
        }

        [Serializable]
        public class AndroidSettings
        {
            public int minSdkVersion = 26;
            public int targetSdkVersion = 34;
            public string keystorePath = "";
            public string keystoreAlias = "";
            public AndroidArchitecture architecture = AndroidArchitecture.ARM64;
            public bool splitAPKsByArchitecture = false;
            public bool useAppBundle = true;

            [Header("Capabilities")]
            public bool enableARCore = true;
            public bool enableCamera = true;
            public bool enableMicrophone = true;
            public bool enableExternalStorage = true;

            public enum AndroidArchitecture
            {
                ARMv7,
                ARM64,
                Both
            }
        }

        [Serializable]
        public class DebugSettings
        {
            public bool enableDebugUI = true;
            public bool enableLogging = true;
            public bool enablePerformanceMonitor = true;
            public bool enableTestMode = false;
            public LogLevel logLevel = LogLevel.Warning;

            public enum LogLevel
            {
                Verbose,
                Debug,
                Info,
                Warning,
                Error
            }
        }

        [Serializable]
        public class FeatureToggles
        {
            public bool voiceCommands = true;
            public bool procedureSharing = true;
            public bool cloudSync = false;
            public bool premiumFeatures = false;
            public bool analytics = true;
            public bool crashReporting = true;
        }

        /// <summary>
        /// Gets environment-specific API endpoint.
        /// </summary>
        public string GetAPIEndpoint()
        {
            return environment switch
            {
                BuildEnvironment.Development => "https://dev-api.mechanicscope.app",
                BuildEnvironment.Staging => "https://staging-api.mechanicscope.app",
                BuildEnvironment.Production => "https://api.mechanicscope.app",
                _ => "https://api.mechanicscope.app"
            };
        }

        /// <summary>
        /// Checks if this is a production build.
        /// </summary>
        public bool IsProduction => environment == BuildEnvironment.Production;

        /// <summary>
        /// Checks if debug features should be enabled.
        /// </summary>
        public bool IsDebugEnabled => environment != BuildEnvironment.Production && debug.enableDebugUI;

        /// <summary>
        /// Applies build settings.
        /// </summary>
        public void ApplySettings()
        {
            // Apply debug settings
            Debug.unityLogger.filterLogType = debug.logLevel switch
            {
                DebugSettings.LogLevel.Verbose => LogType.Log,
                DebugSettings.LogLevel.Debug => LogType.Log,
                DebugSettings.LogLevel.Info => LogType.Log,
                DebugSettings.LogLevel.Warning => LogType.Warning,
                DebugSettings.LogLevel.Error => LogType.Error,
                _ => LogType.Warning
            };

            // Apply quality settings based on environment
            if (IsProduction)
            {
                QualitySettings.vSyncCount = 0;
                Application.targetFrameRate = 60;
            }
        }
    }
}
