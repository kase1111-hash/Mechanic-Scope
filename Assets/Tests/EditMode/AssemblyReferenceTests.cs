using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

namespace MechanicScope.Tests
{
    /// <summary>
    /// Checks each .asmdef references the package assemblies its scripts use.
    ///
    /// Unity compiles each .asmdef folder as its own assembly, which sees a package only if the
    /// .asmdef lists it. The headless harness compiles every script as one project, so a missing
    /// reference compiles fine there and fails only in Unity. This happened once: 15 runtime
    /// scripts used UnityEngine.UI without the reference. The scan is textual and only knows the
    /// namespaces in PackageAssemblies; add a row when the project starts using a new package.
    /// </summary>
    [TestFixture]
    public class AssemblyReferenceTests
    {
        private static readonly Dictionary<string, string> PackageAssemblies = new Dictionary<string, string>
        {
            { "UnityEngine.UI", "UnityEngine.UI" },
            { "UnityEngine.EventSystems", "UnityEngine.UI" },
            { "TMPro", "Unity.TextMeshPro" },
            { "UnityEngine.XR.ARFoundation", "Unity.XR.ARFoundation" },
            { "UnityEngine.XR.ARSubsystems", "Unity.XR.ARSubsystems" },
            { "GLTFast", "glTFast" },
            { "UnityEngine.InputSystem", "Unity.InputSystem" },
            { "UnityEngine.Rendering.Universal", "Unity.RenderPipelines.Universal.Runtime" },
            { "UnityEngine.XR.Management", "Unity.XR.Management" },
            { "UnityEditor.XR.Management", "Unity.XR.Management.Editor" },
        };

        private static IEnumerable<string> AssemblyDefinitions() =>
            Directory.GetFiles(Path.Combine(Application.dataPath, "Scripts"), "*.asmdef", SearchOption.AllDirectories)
                .Select(p => Path.GetRelativePath(Application.dataPath, p).Replace('\\', '/'))
                .OrderBy(p => p);

        [Test]
        public void AssemblyDefinitions_AreFound()
        {
            CollectionAssert.IsNotEmpty(AssemblyDefinitions());
        }

        [TestCaseSource(nameof(AssemblyDefinitions))]
        public void AssemblyDefinition_ReferencesEveryPackageItsScriptsUse(string asmdefPath)
        {
            string fullPath = Path.Combine(Application.dataPath, asmdefPath);
            string folder = Path.GetDirectoryName(fullPath);
            HashSet<string> referenced = ReadReferences(fullPath);

            var missing = new SortedSet<string>();
            foreach (string script in ScriptsOwnedBy(folder))
            {
                string code = StripComments(File.ReadAllText(script));
                foreach (KeyValuePair<string, string> entry in PackageAssemblies)
                {
                    // "using X;" or a fully qualified "X.Type", but not a longer namespace ("X2").
                    string pattern = $@"(?<![\w.]){Regex.Escape(entry.Key)}(\s*;|\.)";
                    if (Regex.IsMatch(code, pattern) && !referenced.Contains(entry.Value))
                    {
                        missing.Add($"{entry.Value} (for {entry.Key}, used in {Path.GetFileName(script)})");
                    }
                }
            }

            CollectionAssert.IsEmpty(missing, $"{asmdefPath} is missing references; the project will not compile in Unity");
        }

        /// <summary>Scripts in the folder tree, excluding subfolders that have their own .asmdef.</summary>
        private static IEnumerable<string> ScriptsOwnedBy(string folder)
        {
            foreach (string script in Directory.GetFiles(folder, "*.cs"))
            {
                yield return script;
            }

            foreach (string sub in Directory.GetDirectories(folder))
            {
                if (Directory.GetFiles(sub, "*.asmdef").Length > 0) continue;
                foreach (string script in ScriptsOwnedBy(sub))
                {
                    yield return script;
                }
            }
        }

        private static HashSet<string> ReadReferences(string asmdefPath)
        {
            // JsonUtility cannot read a top-level string array into a HashSet directly, so pull
            // the "references" array out with a regex; the format is simple and stable.
            string json = File.ReadAllText(asmdefPath);
            Match array = Regex.Match(json, "\"references\"\\s*:\\s*\\[(.*?)\\]", RegexOptions.Singleline);
            Assert.IsTrue(array.Success, $"{asmdefPath} has no references array");

            return new HashSet<string>(Regex.Matches(array.Groups[1].Value, "\"([^\"]+)\"")
                .Cast<Match>().Select(m => m.Groups[1].Value));
        }

        private static string StripComments(string code)
        {
            code = Regex.Replace(code, @"/\*.*?\*/", string.Empty, RegexOptions.Singleline);
            return Regex.Replace(code, @"//[^\n]*", string.Empty);
        }
    }
}
