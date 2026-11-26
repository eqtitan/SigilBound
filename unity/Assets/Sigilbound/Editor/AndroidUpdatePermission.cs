// Adds the REQUEST_INSTALL_PACKAGES permission and the update-APK provider
// (Plugins/Android/SigilboundApkProvider.java) to the Android manifest at build time, for the
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
        const string Provider = "com.sigilbound.updater.SigilboundApkProvider";
        public int callbackOrder => 10;

        public void OnPostGenerateGradleAndroidProject(string path)
        {
            // Play Store builds (BuildScript.AndroidStore, define SB_STORE): no install permission, no provider.
            if (BuildScript.IsStoreBuild) { UnityEngine.Debug.Log("Sigilbound: store build, in-app updater left out of the manifest"); return; }
            // `path` is the unityLibrary module; its manifest is merged into the app's.
            var manifest = Path.Combine(path, "src", "main", "AndroidManifest.xml");
            if (!File.Exists(manifest)) { UnityEngine.Debug.LogWarning("Sigilbound: no manifest at " + manifest); return; }
            var doc = new XmlDocument();
            doc.Load(manifest);
            var root = doc.DocumentElement;
            bool hasPermission = false, hasProvider = false;
            foreach (XmlNode n in root.SelectNodes("uses-permission"))
                if (n.Attributes?["android:name"]?.Value == Permission) hasPermission = true;
            var app = root.SelectSingleNode("application") as XmlElement;
            if (app != null)
                foreach (XmlNode n in app.SelectNodes("provider"))
                    if (n.Attributes?["android:name"]?.Value == Provider) hasProvider = true;
            if (!hasPermission)
            {
                var el = doc.CreateElement("uses-permission");
                el.SetAttribute("name", Android, Permission);
                root.PrependChild(el);
            }
            if (app != null && !hasProvider)
            {
                // ${applicationId} is filled in by the manifest merger; the Java side builds the same authority.
                var p = doc.CreateElement("provider");
                p.SetAttribute("name", Android, Provider);
                p.SetAttribute("authorities", Android, "${applicationId}.sbupdate");
                p.SetAttribute("exported", Android, "false");
                p.SetAttribute("grantUriPermissions", Android, "true");
                app.AppendChild(p);
            }
            doc.Save(manifest);
            UnityEngine.Debug.Log("Sigilbound: in-app update permission and APK provider are in the manifest");
        }
    }
}
#endif
