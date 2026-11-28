// Compile-check stubs for the UnityEditor animation API used by Editor/HumanoidClipBaker.cs. Signatures from Unity's
// C# reference source, branch 6000.0 (Editor/Mono/Animation/AnimationUtility.bindings.cs, EditorCurveBinding.bindings.cs).
// CI also compiles against the real Unity 6000.0.58f1 assemblies. Bodies throw: this code is compiled, never run.
using System;
using UnityEngine;

namespace UnityEditor
{
    public struct EditorCurveBinding
    {
        public string path; public Type type; public string propertyName;
        public static EditorCurveBinding FloatCurve(string inPath, Type inType, string inPropertyName) => throw new NotImplementedException();
    }

    public class AnimationClipSettings
    {
        public AnimationClip additiveReferencePoseClip; public float additiveReferencePoseTime; public float startTime; public float stopTime;
        public float orientationOffsetY; public float level; public float cycleOffset; public bool hasAdditiveReferencePose;
        public bool loopTime; public bool loopBlend; public bool loopBlendOrientation; public bool loopBlendPositionY; public bool loopBlendPositionXZ;
        public bool keepOriginalOrientation; public bool keepOriginalPositionY; public bool keepOriginalPositionXZ; public bool heightFromFeet; public bool mirror;
    }

    public static class AnimationUtility
    {
        public static void SetEditorCurve(AnimationClip clip, EditorCurveBinding binding, AnimationCurve curve) => throw new NotImplementedException();
        public static AnimationClipSettings GetAnimationClipSettings(AnimationClip clip) => throw new NotImplementedException();
        public static void SetAnimationClipSettings(AnimationClip clip, AnimationClipSettings srcClipInfo) => throw new NotImplementedException();
    }
}
