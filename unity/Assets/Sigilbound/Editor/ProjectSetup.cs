// Project setup, run automatically when the project opens in the editor and again before every
// command-line build: creates the (empty) main scene the game boots from, adds it to the build,
// sets mobile player settings, configures URP (UrpSetup) and checks the hero models are in
// StreamingAssets. Safe to run again: Tools > Sigilbound > Apply Project Settings.
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Sigilbound.EditorTools
{
    [InitializeOnLoad]
    public static class ProjectSetup
    {
        public const string ScenePath = "Assets/Scenes/Main.unity";
        // A separate app id and name from the Capacitor app (com.sigilbound.game), so both
        // can be installed side by side while the Unity version catches up.
        public const string AppId = "com.sigilbound.unity";
        public const string AppName = "Sigilbound (Unity)";

        static ProjectSetup() { EditorApplication.delayCall += () => { if (!File.Exists(ScenePath)) Apply(); }; }

        [MenuItem("Tools/Sigilbound/Apply Project Settings")]
        public static void Apply()
        {
            if (!File.Exists(ScenePath))
            {
                Directory.CreateDirectory("Assets/Scenes");
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                EditorSceneManager.SaveScene(scene, ScenePath);
            }
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };

            EnsureHeroPaintedMaterial();
            PlayerSettings.companyName = "Sigilbound";
            // Frame pacing at 120/60/30 Hz on high-refresh phones (MECHANICS.md 6.1), and frame timings for Quality.
#if UNITY_2022_2_OR_NEWER
            PlayerSettings.Android.optimizedFramePacing = true;
            PlayerSettings.enableFrameTimingStats = true;
#endif
            PlayerSettings.productName = AppName;
            ApplyIcons();
            PlayerSettings.SetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.Android, AppId);
            PlayerSettings.SetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.iOS, AppId);
            // Landscape on phones, either way round.
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;
            PlayerSettings.colorSpace = ColorSpace.Linear; // URP + true colours, like the web game's colour management
            // Google Play requires 64-bit (ARM64), which needs IL2CPP.
            PlayerSettings.SetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel24;
            // GPU skinning (compute; batched where the device supports it) takes the hero meshes'
            // skinning off the CPU. Static batching of the arena happens at runtime (Arena.StaticBatch).
            PlayerSettings.meshDeformation = MeshDeformation.GPUBatched;

            UrpSetup.Apply();
            if (!File.Exists("Assets/StreamingAssets/Models/adventurer.glb"))
                Debug.LogWarning("Sigilbound: hero models missing from Assets/StreamingAssets/Models. Run: cd unity/Tools~/models && npm ci && node prepare-models.mjs (placeholder figures are used until then).");
            AssetDatabase.SaveAssets();
            Debug.Log("Sigilbound: project settings applied. Press Play to start the game.");
        }
        // A material on Sigilbound/CharacterPainted in Resources, so the shader ships in builds and the
        // high-detail heroes (HeroModels.BuildHd) can clone it at runtime.
        static void EnsureHeroPaintedMaterial()
        {
            const string dir = "Assets/Resources/SigilMaterials", path = dir + "/HeroPainted.mat";
            if (AssetDatabase.LoadAssetAtPath<Material>(path) != null) return;
            var sh = Shader.Find("Sigilbound/CharacterPainted");
            if (sh == null) { Debug.LogWarning("Sigilbound: Sigilbound/CharacterPainted shader not found"); return; }
            if (!AssetDatabase.IsValidFolder("Assets/Resources")) AssetDatabase.CreateFolder("Assets", "Resources");
            if (!AssetDatabase.IsValidFolder(dir)) AssetDatabase.CreateFolder("Assets/Resources", "SigilMaterials");
            AssetDatabase.CreateAsset(new Material(sh) { name = "HeroPainted" }, path);
            AssetDatabase.SaveAssets();
        }

        // The app icon (docs/sigilbound/store/make_icon.py draws it): Android adaptive (background +
        // foreground sigil), legacy and round icons, and the default icon used by Windows and the rest.
        public const string IconDir = "Assets/Sigilbound/Art/Icon/";
        static void ApplyIcons()
        {
#if UNITY_2022_2_OR_NEWER
            var bg = AssetDatabase.LoadAssetAtPath<Texture2D>(IconDir + "icon_background.png");
            var fg = AssetDatabase.LoadAssetAtPath<Texture2D>(IconDir + "icon_foreground.png");
            var full = AssetDatabase.LoadAssetAtPath<Texture2D>(IconDir + "icon_full.png");
            if (full == null) { Debug.LogWarning("Sigilbound: app icon textures missing under " + IconDir); return; }
            PlayerSettings.SetIcons(UnityEditor.Build.NamedBuildTarget.Unknown, new[] { full }, IconKind.Any);
            var android = UnityEditor.Build.NamedBuildTarget.Android;
            void Fill(PlatformIconKind kind, params Texture2D[] layers)
            {
                var icons = PlayerSettings.GetPlatformIcons(android, kind);
                foreach (var ic in icons) ic.SetTextures(layers);
                PlayerSettings.SetPlatformIcons(android, kind, icons);
            }
            if (bg != null && fg != null) Fill(UnityEditor.Android.AndroidPlatformIconKind.Adaptive, bg, fg); // layer 0 background, layer 1 foreground
            Fill(UnityEditor.Android.AndroidPlatformIconKind.Round, full);
            Fill(UnityEditor.Android.AndroidPlatformIconKind.Legacy, full);
#endif
        }
}
}
