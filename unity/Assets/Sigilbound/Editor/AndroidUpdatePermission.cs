// Adds the REQUEST_INSTALL_PACKAGES permission to the Android manifest at build time, for the
// in-app updater (Plugins/Android/SigilboundUpdateInstaller.java, Client/Updater.cs; PLAN step 34).
// Android only: the Windows and Linux editors don't have the Android build module.
#if UNITY_ANDROID
using System.IO;
using System.Xml;
using UnityEditor.Android;

namespace Sigilbound.EditorTools
{
    public sealed class AndroidUpdatePermission : IPostGenerateGradleAndroidProject
    {
        const string Android = "http://schemas.android.com/apk/res/android";
        const string Permission = "android.permission.REQUEST_INSTALL_PACKAGES";
        public int callbackOrder => 10;

        public void OnPostGenerateGradleAndroidProject(string path)
        {
            // `path` is the unityLibrary module; its manifest is merged into the app's.
            var manifest = Path.Combine(path, "src", "main", "AndroidManifest.xml");
            if (!File.Exists(manifest)) { UnityEngine.Debug.LogWarning("Sigilbound: no manifest at " + manifest); return; }
            var doc = new XmlDocument();
            doc.Load(manifest);
            var root = doc.DocumentElement;
            foreach (XmlNode n in root.SelectNodes("uses-permission"))
                if (n.Attributes?["android:name"]?.Value == Permission) return;
            var el = doc.CreateElement("uses-permission");
            el.SetAttribute("name", Android, Permission);
            root.PrependChild(el);
            doc.Save(manifest);
            UnityEngine.Debug.Log("Sigilbound: added " + Permission + " for in-app updates");
        }
    }
}
#endif
