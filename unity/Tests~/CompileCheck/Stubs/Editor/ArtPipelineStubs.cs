// Compile-check stubs for the UnityEditor API used by Assets/Sigilbound/Editor/Art (the hero art
// import pipeline). Signatures copied from Unity's C# reference source, branch 6000.0
// (github.com/Unity-Technologies/UnityCsReference: Editor/Mono/AssetPostprocessor.cs,
// AssetPipeline/AssetImporter.bindings.cs, ImportSettings/ModelImporter.bindings.cs,
// ImportSettings/TextureImporter.bindings.cs, TextureImporterEnums.cs, AssetDatabase/AssetDatabase.bindings.cs,
// Prefabs/PrefabUtility.cs, EditorUtility.bindings.cs, Animation/AnimatorController.cs,
// Animation/StateMachine.bindings.cs). Only what our editor scripts call is declared. CI also compiles
// against the real Unity 6000.0.58f1 assemblies, which is what proves these match.
// Bodies throw: this code is compiled, never run.
#pragma warning disable CS0067
using System;
using UnityEngine;

namespace UnityEditor
{
    public class AssetImporter : UnityEngine.Object
    {
        public string assetPath => throw new NotImplementedException();
        public string userData { get; set; }
        public void SaveAndReimport() => throw new NotImplementedException();
        public static AssetImporter GetAtPath(string path) => throw new NotImplementedException();
    }

    public class AssetPostprocessor
    {
        public string assetPath { get; set; }
        public AssetImporter assetImporter => throw new NotImplementedException();
    }

    public enum ModelImporterAnimationType { None = 0, Legacy = 1, Generic = 2, Human = 3 }
    public enum ModelImporterAvatarSetup { NoAvatar = 0, CreateFromThisModel = 1, CopyFromOther = 2 }
    public enum ModelImporterMaterialImportMode { None = 0, ImportStandard = 1, ImportViaMaterialDescription = 2 }
    public enum ModelImporterMeshCompression { Off = 0, Low = 1, Medium = 2, High = 3 }
    public enum ModelImporterSkinWeights { Standard = 0, Custom = 1 }

    public class ModelImporterClipAnimation
    {
        public string name { get; set; }
        public float firstFrame { get; set; }
        public float lastFrame { get; set; }
        public bool loopTime { get; set; }
        public bool loopPose { get; set; }
        public bool lockRootRotation { get; set; }
        public bool lockRootHeightY { get; set; }
        public bool lockRootPositionXZ { get; set; }
        public bool keepOriginalOrientation { get; set; }
        public bool keepOriginalPositionY { get; set; }
        public bool keepOriginalPositionXZ { get; set; }
    }

    public class ModelImporter : AssetImporter
    {
        public bool importCameras { get; set; }
        public bool importLights { get; set; }
        public bool useFileScale { get; set; }
        public float globalScale { get; set; }
        public bool isReadable { get; set; }
        public bool optimizeGameObjects { get; set; }
        public ModelImporterSkinWeights skinWeights { get; set; }
        public ModelImporterMeshCompression meshCompression { get; set; }
        public bool importBlendShapes { get; set; }
        public ModelImporterMaterialImportMode materialImportMode { get; set; }
        public ModelImporterAnimationType animationType { get; set; }
        public ModelImporterAvatarSetup avatarSetup { get; set; }
        public bool importAnimation { get; set; }
        public HumanDescription humanDescription { get; set; }
        public ModelImporterClipAnimation[] defaultClipAnimations => throw new NotImplementedException();
        public ModelImporterClipAnimation[] clipAnimations { get; set; }
    }

    public enum TextureImporterType { Default = 0, NormalMap = 1, GUI = 2, Sprite = 8, Cursor = 7, Cookie = 4, Lightmap = 6, SingleChannel = 10, DirectionalLightmap = 12, Shadowmask = 11 }
    public enum TextureImporterFormat { Automatic = -1, RGBA32 = 4, ASTC_4x4 = 48, ASTC_5x5 = 49, ASTC_6x6 = 50, ASTC_8x8 = 51, ASTC_10x10 = 52, ASTC_12x12 = 53 }

    public sealed class TextureImporterPlatformSettings
    {
        public string name { get; set; }
        public bool overridden { get; set; }
        public int maxTextureSize { get; set; }
        public TextureImporterFormat format { get; set; }
    }

    public sealed class TextureImporter : AssetImporter
    {
        public TextureImporterType textureType { get; set; }
        public bool sRGBTexture { get; set; }
        public bool mipmapEnabled { get; set; }
        public int maxTextureSize { get; set; }
        public void SetPlatformTextureSettings(TextureImporterPlatformSettings platformSettings) => throw new NotImplementedException();
        public void GetSourceTextureWidthAndHeight(out int width, out int height) => throw new NotImplementedException();
    }

    public sealed partial class AssetDatabase
    {
        public static void Refresh(ImportAssetOptions options) => throw new NotImplementedException();
        public static string[] FindAssets(string filter, string[] searchInFolders) => throw new NotImplementedException();
        public static UnityEngine.Object[] LoadAllAssetsAtPath(string assetPath) => throw new NotImplementedException();
        public static bool DeleteAsset(string path) => throw new NotImplementedException();
        public static string GetAssetPath(UnityEngine.Object assetObject) => throw new NotImplementedException();
    }

    public sealed partial class EditorUtility
    {
        public static string OpenFolderPanel(string title, string folder, string defaultName) => throw new NotImplementedException();
        public static bool DisplayDialog(string title, string message, string ok) => throw new NotImplementedException();
        public static void DisplayProgressBar(string title, string info, float progress) => throw new NotImplementedException();
        public static void ClearProgressBar() => throw new NotImplementedException();
    }

    public static class PrefabUtility
    {
        public static GameObject SaveAsPrefabAsset(GameObject instanceRoot, string assetPath, out bool success) => throw new NotImplementedException();
    }
}

namespace UnityEditor.Animations
{
    public class AnimatorStateTransition : UnityEngine.Object
    {
        public bool hasExitTime { get; set; }
        public float exitTime { get; set; }
        public bool hasFixedDuration { get; set; }
        public float duration { get; set; }
    }

    public sealed class AnimatorState : UnityEngine.Object
    {
        public Motion motion { get; set; }
        public bool writeDefaultValues { get; set; }
        public bool speedParameterActive { get; set; }
        public string speedParameter { get; set; }
        public AnimatorStateTransition AddTransition(AnimatorState destinationState) => throw new NotImplementedException();
    }

    public sealed class AnimatorStateMachine : UnityEngine.Object
    {
        public AnimatorState defaultState { get; set; }
        public AnimatorState AddState(string name) => throw new NotImplementedException();
    }

    public class AnimatorControllerLayer
    {
        public AnimatorStateMachine stateMachine { get; set; }
    }

    public sealed class AnimatorController : RuntimeAnimatorController
    {
        public AnimatorControllerLayer[] layers { get; set; }
        public AnimatorControllerParameter[] parameters { get; set; }
        public void AddParameter(string name, AnimatorControllerParameterType type) => throw new NotImplementedException();
        public static AnimatorController CreateAnimatorControllerAtPath(string path) => throw new NotImplementedException();
    }
}
