using System;
using UnityEngine;

namespace MechanicScope.AppStore
{
    /// <summary>
    /// App store configuration and metadata.
    /// Contains app info, feature flags, and store-specific settings.
    /// </summary>
    [CreateAssetMenu(fileName = "AppStoreConfig", menuName = "MechanicScope/App Store Config")]
    public class AppStoreConfig : ScriptableObject
    {
        [Header("App Information")]
        public string appName = "Mechanic Scope";
        public string bundleId = "com.mechanicscope.app";
        public string version = "1.0.0";
        public int buildNumber = 1;

        [Header("Store Metadata")]
        [TextArea(2, 3)]
        public string shortDescription = "AR-powered engine repair assistant for shade tree mechanics";

        [TextArea(5, 10)]
        public string fullDescription = @"Mechanic Scope is your personal AR-powered repair assistant, designed for shade tree mechanics who want to tackle engine repairs with confidence.

FEATURES:
• AR Part Identification - Point your camera at any engine and tap to identify parts instantly
• Step-by-Step Procedures - Follow guided repair instructions with 3D overlays
• Progress Tracking - Save your work and resume anytime
• Offline Operation - All content works without internet
• Voice Commands - Hands-free navigation while working
• Community Procedures - Create and share repair guides

SUPPORTED ENGINES:
• GM LS Series (Gen III/IV/V)
• Ford Coyote & Modular
• Chrysler Hemi
• Honda K-Series
• Toyota 2JZ
• And more coming soon!

Perfect for DIY mechanics, automotive students, and anyone learning engine repair.";

        [TextArea(2, 4)]
        public string whatsNew = @"Version 1.0.0:
• Initial release
• AR part identification
• Step-by-step procedures
• Progress tracking
• Voice commands";

        [Header("Keywords")]
        public string[] keywords = new string[]
        {
            "mechanic",
            "engine",
            "repair",
            "AR",
            "automotive",
            "DIY",
            "car",
            "maintenance",
            "tutorial",
            "guide"
        };

        [Header("Categories")]
        public string primaryCategory = "Utilities";
        public string secondaryCategory = "Education";

        [Header("Age Rating")]
        public AgeRating ageRating = AgeRating.Rating4Plus;

        [Header("Pricing")]
        public bool isFree = true;
        public string priceUSD = "0.00";
        public bool hasInAppPurchases = false;

        [Header("Requirements")]
        public string minimumIOSVersion = "14.0";
        public int minimumAndroidAPI = 26; // Android 8.0
        public bool requiresARCore = true;
        public bool requiresARKit = true;

        [Header("Permissions")]
        public PermissionInfo[] requiredPermissions = new PermissionInfo[]
        {
            new PermissionInfo { permission = "Camera", reason = "Required for AR experience and part identification" },
            new PermissionInfo { permission = "Microphone", reason = "Optional voice commands for hands-free operation" }
        };

        [Header("URLs")]
        public string privacyPolicyURL = "https://mechanicscope.app/privacy";
        public string supportURL = "https://mechanicscope.app/support";
        public string marketingURL = "https://mechanicscope.app";

        [Header("Contact")]
        public string supportEmail = "support@mechanicscope.app";
        public string developerName = "MechanicScope";

        [Header("Feature Flags")]
        public bool enableAnalytics = true;
        public bool enableCrashReporting = true;
        public bool enableRemoteConfig = false;
        public bool enablePushNotifications = false;

        public enum AgeRating
        {
            Rating4Plus,
            Rating9Plus,
            Rating12Plus,
            Rating17Plus
        }

        [Serializable]
        public class PermissionInfo
        {
            public string permission;
            [TextArea(1, 2)]
            public string reason;
        }

        /// <summary>
        /// Gets the version string in semver format.
        /// </summary>
        public string GetVersionString()
        {
            return $"{version} ({buildNumber})";
        }

        /// <summary>
        /// Gets keywords as comma-separated string.
        /// </summary>
        public string GetKeywordsString()
        {
            return string.Join(", ", keywords);
        }

        /// <summary>
        /// Validates the configuration.
        /// </summary>
        public bool Validate(out string[] errors)
        {
            var errorList = new System.Collections.Generic.List<string>();

            if (string.IsNullOrEmpty(appName))
                errorList.Add("App name is required");

            if (string.IsNullOrEmpty(bundleId))
                errorList.Add("Bundle ID is required");

            if (!bundleId.Contains("."))
                errorList.Add("Bundle ID should be in reverse domain format");

            if (string.IsNullOrEmpty(version))
                errorList.Add("Version is required");

            if (shortDescription.Length > 80)
                errorList.Add("Short description should be under 80 characters");

            if (fullDescription.Length > 4000)
                errorList.Add("Full description should be under 4000 characters");

            if (keywords.Length > 100)
                errorList.Add("Maximum 100 keywords allowed");

            if (string.IsNullOrEmpty(privacyPolicyURL))
                errorList.Add("Privacy policy URL is required");

            errors = errorList.ToArray();
            return errors.Length == 0;
        }
    }
}
