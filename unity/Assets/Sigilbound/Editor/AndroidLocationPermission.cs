// Real sun at my location (Client/SkySun.cs): the Android manifest asks for approximate location only, in both the
// tester APK and the Play Store bundle (SB_STORE).
//   - Adds ACCESS_COARSE_LOCATION, which Client/SkySun requests at run time (Permission.RequestUserPermission).
//   - Removes ACCESS_FINE_LOCATION: Unity adds it to the unityLibrary manifest on its own whenever a script uses
//     Input.location, and the sky never needs a precise fix (one low-accuracy reading, rounded to 0.1 degree).
//     A "dangerous" precise-location permission would also change the Play listing's permission list.
// The project has no Plugins/Android manifest; like AndroidUpdatePermission, this edits the generated unityLibrary
// manifest after Gradle project generation, which the manifest merger folds into the app's.
#if UNITY_ANDROID
using System.Collections.Generic;
using System.IO;
using System.Xml;
using UnityEditor.Android;

namespace Sigilbound.EditorTools
{
    public sealed class AndroidLocationPermission : IPostGenerateGradleAndroidProject
    {
        const string Android = "http://schemas.android.com/apk/res/android";
        public const string Coarse = "android.permission.ACCESS_COARSE_LOCATION";
        public const string Fine = "android.permission.ACCESS_FINE_LOCATION";
        public int callbackOrder => 11;

        public void OnPostGenerateGradleAndroidProject(string path)
        {
            // Both builds: the store bundle and the tester APK ask for the same approximate location.
            var manifest = Path.Combine(path, "src", "main", "AndroidManifest.xml");
            if (!File.Exists(manifest)) { UnityEngine.Debug.LogWarning("Sigilbound: no manifest at " + manifest); return; }
            var doc = new XmlDocument();
            doc.Load(manifest);
            var root = doc.DocumentElement;
            bool hasCoarse = false; var fine = new List<XmlNode>();
            foreach (XmlNode n in root.SelectNodes("uses-permission"))
            {
                var name = n.Attributes?["android:name"]?.Value;
                if (name == Coarse) hasCoarse = true;
                if (name == Fine) fine.Add(n);
            }
            foreach (var n in fine) root.RemoveChild(n);
            if (!hasCoarse)
            {
                var el = doc.CreateElement("uses-permission");
                el.SetAttribute("name", Android, Coarse);
                root.PrependChild(el);
            }
            doc.Save(manifest);
            UnityEngine.Debug.Log($"Sigilbound: approximate location in the manifest (fine location removed: {fine.Count})");
        }
    }
}
#endif
