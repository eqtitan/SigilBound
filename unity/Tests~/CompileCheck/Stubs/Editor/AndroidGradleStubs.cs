// Stub of UnityEditor.Android's gradle callback (UnityEditor.Android.Extensions), so the compile
// check can build Editor/AndroidUpdatePermission.cs with UNITY_ANDROID defined.
#if UNITY_ANDROID
namespace UnityEditor.Android
{
    public interface IPostGenerateGradleAndroidProject
    {
        int callbackOrder { get; }
        void OnPostGenerateGradleAndroidProject(string path);
    }
}
#endif
