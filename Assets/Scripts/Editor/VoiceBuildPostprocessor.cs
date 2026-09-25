#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

#if UNITY_ANDROID
using System.Xml;
using UnityEditor.Android;
#endif

#if UNITY_IOS
using UnityEditor.iOS.Xcode;
#endif

namespace MechanicScope.Editor
{
    /// <summary>
    /// Platform project settings that voice commands need and Unity does not add on its own.
    /// Each half compiles only when its platform is the active build target, because the
    /// Android and iOS editor APIs exist only when that platform's build support is installed.
    /// </summary>
    public static class VoiceBuildSettings
    {
        public const string MicrophoneUsage =
            "Mechanic Scope listens for voice commands like \"next step\" so you can work hands-free.";

        // Accurate while VoiceCommandManager.allowCloudRecognition is off (the default). Reword it
        // if cloud recognition is ever enabled.
        public const string SpeechRecognitionUsage =
            "Mechanic Scope turns your voice commands into actions. Recognition runs on your device.";
    }

#if UNITY_ANDROID
    /// <summary>
    /// Adds to the unityLibrary manifest (merged into the app's):
    /// - RECORD_AUDIO, requested at runtime by AndroidVoiceRecognizer.
    /// - &lt;queries&gt; for the speech recognition and text-to-speech services. From Android 11,
    ///   package visibility hides them otherwise, so SpeechRecognizer.isRecognitionAvailable()
    ///   returns false and TextToSpeech finds no engine.
    /// </summary>
    public class VoiceAndroidManifestPostprocessor : IPostGenerateGradleAndroidProject
    {
        private const string AndroidNs = "http://schemas.android.com/apk/res/android";

        public int callbackOrder => 0;

        public void OnPostGenerateGradleAndroidProject(string path)
        {
            string manifestPath = Path.Combine(path, "src", "main", "AndroidManifest.xml");
            if (!File.Exists(manifestPath))
            {
                Debug.LogError($"[Voice] AndroidManifest.xml not found at {manifestPath}; voice may not work");
                return;
            }

            var doc = new XmlDocument();
            doc.Load(manifestPath);
            XmlElement manifest = doc.DocumentElement;

            EnsurePermission(doc, manifest, "android.permission.RECORD_AUDIO");

            XmlElement queries = manifest["queries"];
            if (queries == null)
            {
                queries = doc.CreateElement("queries");
                manifest.AppendChild(queries);
            }
            EnsureQueryIntent(doc, queries, "android.speech.RecognitionService");
            EnsureQueryIntent(doc, queries, "android.intent.action.TTS_SERVICE");

            doc.Save(manifestPath);
        }

        private static void EnsurePermission(XmlDocument doc, XmlElement manifest, string permission)
        {
            foreach (XmlNode node in manifest.ChildNodes)
            {
                if (node is XmlElement el && el.Name == "uses-permission" &&
                    el.GetAttribute("name", AndroidNs) == permission)
                {
                    return;
                }
            }

            XmlElement uses = doc.CreateElement("uses-permission");
            uses.Attributes.Append(AndroidAttribute(doc, "name", permission));
            manifest.PrependChild(uses);
        }

        private static void EnsureQueryIntent(XmlDocument doc, XmlElement queries, string action)
        {
            foreach (XmlNode intentNode in queries.ChildNodes)
            {
                if (intentNode is XmlElement intent && intent.Name == "intent" &&
                    intent["action"]?.GetAttribute("name", AndroidNs) == action)
                {
                    return;
                }
            }

            XmlElement newIntent = doc.CreateElement("intent");
            XmlElement actionEl = doc.CreateElement("action");
            actionEl.Attributes.Append(AndroidAttribute(doc, "name", action));
            newIntent.AppendChild(actionEl);
            queries.AppendChild(newIntent);
        }

        private static XmlAttribute AndroidAttribute(XmlDocument doc, string name, string value)
        {
            XmlAttribute attr = doc.CreateAttribute("android", name, AndroidNs);
            attr.Value = value;
            return attr;
        }
    }
#endif

#if UNITY_IOS
    /// <summary>
    /// Adds the Info.plist privacy strings that iOS requires before it will show the microphone
    /// and speech recognition permission prompts (the app is killed on first use without them),
    /// and links Speech.framework, which MechanicScopeSpeech.mm uses.
    /// </summary>
    public class VoiceIOSPostprocessor : IPostprocessBuildWithReport
    {
        public int callbackOrder => 0;

        public void OnPostprocessBuild(BuildReport report)
        {
            if (report.summary.platform != BuildTarget.iOS) return;

            string buildPath = report.summary.outputPath;

            string plistPath = Path.Combine(buildPath, "Info.plist");
            var plist = new PlistDocument();
            plist.ReadFromFile(plistPath);
            SetIfMissing(plist.root, "NSMicrophoneUsageDescription", VoiceBuildSettings.MicrophoneUsage);
            SetIfMissing(plist.root, "NSSpeechRecognitionUsageDescription", VoiceBuildSettings.SpeechRecognitionUsage);
            plist.WriteToFile(plistPath);

            string projectPath = PBXProject.GetPBXProjectPath(buildPath);
            var project = new PBXProject();
            project.ReadFromFile(projectPath);
            project.AddFrameworkToProject(project.GetUnityFrameworkTargetGuid(), "Speech.framework", false);
            project.WriteToFile(projectPath);
        }

        private static void SetIfMissing(PlistElementDict root, string key, string value)
        {
            if (!root.values.ContainsKey(key) || string.IsNullOrEmpty(root[key].AsString()))
            {
                root.SetString(key, value);
            }
        }
    }
#endif
}
#endif
