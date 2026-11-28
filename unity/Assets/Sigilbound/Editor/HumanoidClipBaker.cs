// Skeleton library, editor side (docs/sigilbound/art/SKELETON_LIBRARY.md): turns the reference rigs' clips into
// Unity Humanoid (muscle-space) clips during the build, so every migrated hero plays them on its own avatar,
// whatever its proportions (Client/HumanoidRig.cs).
// For each clip set (Core.RigProfiles.ClipSources: the shared clips on the reference biped, the Titan's own moves on
// the brute) it reads the source .glb (unity/Tools~/models, Core.GlbAnim), builds the reference skeleton as
// GameObjects (glTFast's axis conversion), gives it a Humanoid avatar (Core.HumanoidMap), plays every clip frame by
// frame (30 per second), reads the human pose (HumanPoseHandler: muscles plus body position and rotation) and
// writes it as a Humanoid AnimationClip to Assets/Resources/HumanoidClips/<set>/<clip>.anim. Then it plays each
// baked clip back on the reference avatar and logs how far the hands, feet and head land from the source
// ("SBHUMAN ..." lines in the build log). Runs at the start of every BuildScript build (generated assets, not in
// git); menu Sigilbound > Art > Bake Humanoid Clips. A failure leaves the set empty and the game keeps the
// legacy path for every hero (HumanoidRig.Clips.Available).
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Sigilbound.Core;

namespace Sigilbound.EditorTools
{
    public static class HumanoidClipBaker
    {
        const string OutRoot = "Assets/Resources/" + RigProfiles.ResourcesDir;

        [MenuItem("Sigilbound/Art/Bake Humanoid Clips")]
        static void Menu() => Bake();

        public static int Bake()
        {
            int total = 0;
            var models = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Tools~", "models"));
            foreach (var (set, src, prefix) in RigProfiles.ClipSources)
            {
                var path = Path.Combine(models, src);
                if (!File.Exists(path)) { Debug.LogWarning($"SBHUMAN {set}: source {path} missing, set skipped"); continue; }
                try { total += BakeSet(set, GlbAnim.Read(File.ReadAllBytes(path)), prefix); }
                catch (Exception e) { Debug.LogError($"SBHUMAN {set}: bake failed: {e}"); }
            }
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"SBHUMAN baked {total} humanoid clips into {OutRoot}");
            return total;
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        static string CurveName(string muscle) => RigProfiles.CurveName(muscle);

        sealed class Skeleton
        {
            public GameObject Top; public Transform[] Nodes; public Avatar Avatar;
            public Vector3[] RestT; public Quaternion[] RestR; public Vector3[] RestS;
            public void Destroy() { if (Top) UnityEngine.Object.DestroyImmediate(Top); if (Avatar) UnityEngine.Object.DestroyImmediate(Avatar); }
        }

        static Vector3 V(float[] p) => new Vector3(p[0], p[1], p[2]);
        static Quaternion Q(float[] q) => new Quaternion(q[0], q[1], q[2], q[3]);

        static Skeleton Build(string name, GlbAnim g)
        {
            var s = new Skeleton { Top = new GameObject(name) };
            s.Top.hideFlags = HideFlags.HideAndDontSave;
            int n = g.Nodes.Count;
            s.Nodes = new Transform[n]; s.RestT = new Vector3[n]; s.RestR = new Quaternion[n]; s.RestS = new Vector3[n];
            for (int i = 0; i < n; i++) { s.Nodes[i] = new GameObject(g.Nodes[i].Name).transform; s.Nodes[i].gameObject.hideFlags = HideFlags.HideAndDontSave; }
            for (int i = 0; i < n; i++)
            {
                var nd = g.Nodes[i];
                s.Nodes[i].SetParent(nd.Parent >= 0 ? s.Nodes[nd.Parent] : s.Top.transform, false);
                s.RestT[i] = V(GlbAnim.ToUnityPos(nd.T)); s.RestR[i] = Q(GlbAnim.ToUnityRot(nd.R)); s.RestS[i] = V(nd.S);
            }
            Rest(s);
            var map = HumanoidMap.Map(g.Hierarchy());
            var missing = HumanoidMap.Missing(map);
            if (missing.Count > 0) throw new InvalidOperationException("reference skeleton lacks " + string.Join(", ", missing));
            s.Avatar = AvatarBuilder.BuildHumanAvatar(s.Top, Describe(s.Top.transform, s.Nodes, map));
            if (s.Avatar == null || !s.Avatar.isValid || !s.Avatar.isHuman) throw new InvalidOperationException("AvatarBuilder refused the reference skeleton");
            s.Avatar.name = "AV_Ref_" + name;
            return s;
        }

        static void Rest(Skeleton s)
        {
            for (int i = 0; i < s.Nodes.Length; i++) { s.Nodes[i].localPosition = s.RestT[i]; s.Nodes[i].localRotation = s.RestR[i]; s.Nodes[i].localScale = s.RestS[i]; }
        }

        // Same description the runtime builds for a hero (Client/HumanoidRig.Describe): the rest pose is the T-pose.
        public static HumanDescription Describe(Transform top, Transform[] nodes, Dictionary<string, int> map)
        {
            var names = HumanTrait.BoneName;
            var human = new List<HumanBone>();
            foreach (var kv in map)
            {
                var hn = names.FirstOrDefault(x => x.Replace(" ", "") == kv.Key);
                if (hn == null) continue;
                human.Add(new HumanBone { boneName = nodes[kv.Value].name, humanName = hn, limit = new HumanLimit { useDefaultValues = true } });
            }
            var skeleton = new List<SkeletonBone> { new SkeletonBone { name = top.name, position = Vector3.zero, rotation = Quaternion.identity, scale = Vector3.one } };
            skeleton.AddRange(nodes.Select(t => new SkeletonBone { name = t.name, position = t.localPosition, rotation = t.localRotation, scale = t.localScale }));
            return new HumanDescription
            {
                human = human.ToArray(), skeleton = skeleton.ToArray(),
                upperArmTwist = 0.5f, lowerArmTwist = 0.5f, upperLegTwist = 0.5f, lowerLegTwist = 0.5f,
                armStretch = 0.05f, legStretch = 0.05f, feetSpacing = 0f, hasTranslationDoF = false,
            };
        }

        static void Pose(Skeleton s, GlbAnim g, GlbAnim.Clip c, float t)
        {
            Rest(s);
            foreach (var tr in c.Tracks)
            {
                var v = GlbAnim.Sample(tr, t);
                var x = s.Nodes[tr.Node];
                if (tr.Path == "rotation") x.localRotation = Q(GlbAnim.ToUnityRot(v));
                else if (tr.Path == "translation") x.localPosition = V(GlbAnim.ToUnityPos(v));
                else x.localScale = V(v);
            }
        }

        static AnimationCurve Curve(float[] times, float[] vals)
        {
            var keys = new Keyframe[times.Length];
            for (int i = 0; i < keys.Length; i++)
            {
                int a = Math.Max(0, i - 1), b = Math.Min(keys.Length - 1, i + 1);
                float slope = b > a ? (vals[b] - vals[a]) / (times[b] - times[a]) : 0f;
                keys[i] = new Keyframe(times[i], vals[i], slope, slope);
            }
            return new AnimationCurve(keys);
        }

        static int BakeSet(string set, GlbAnim g, string prefix)
        {
            var dir = $"{OutRoot}/{set}";
            if (AssetDatabase.IsValidFolder(dir)) AssetDatabase.DeleteAsset(dir);
            EnsureFolder(dir);
            var s = Build("SB_Ref_" + set, g);
            var check = s.Top.AddComponent<Animator>();
            check.avatar = s.Avatar; check.applyRootMotion = false;
            var handler = new HumanPoseHandler(s.Avatar, s.Top.transform);
            var muscles = HumanTrait.MuscleName;
            var pose = new HumanPose();
            int count = 0;
            var probe = new[] { "hand_l", "hand_r", "foot_l", "foot_r", "Head" }.Select(g.Find).Where(i => i >= 0).ToArray();
            int pelvis = g.Find("pelvis");
            try
            {
                foreach (var c in g.Clips)
                {
                    if (prefix != null && !c.Name.StartsWith(prefix, StringComparison.Ordinal)) continue;
                    if (c.Name.Contains("TPose") || c.Length <= 0) continue;
                    int frames = Mathf.Max(2, Mathf.CeilToInt(c.Length * RigProfiles.BakeFps) + 1);
                    var times = new float[frames];
                    var mus = new float[muscles.Length][];
                    for (int m = 0; m < mus.Length; m++) mus[m] = new float[frames];
                    var rootT = new float[3][]; var rootQ = new float[4][];
                    for (int k = 0; k < 3; k++) rootT[k] = new float[frames];
                    for (int k = 0; k < 4; k++) rootQ[k] = new float[frames];
                    Quaternion prevQ = Quaternion.identity;
                    var source = new Vector3[frames, probe.Length];
                    for (int f = 0; f < frames; f++)
                    {
                        float t = Mathf.Min(c.Length, f / RigProfiles.BakeFps);
                        times[f] = t;
                        Pose(s, g, c, t);
                        for (int p = 0; p < probe.Length; p++) source[f, p] = s.Top.transform.InverseTransformPoint(s.Nodes[probe[p]].position) - (pelvis >= 0 ? s.Top.transform.InverseTransformPoint(s.Nodes[pelvis].position) : Vector3.zero);
                        handler.GetHumanPose(ref pose);
                        for (int m = 0; m < mus.Length && m < pose.muscles.Length; m++) mus[m][f] = pose.muscles[m];
                        var q = pose.bodyRotation;
                        if (f > 0 && Quaternion.Dot(q, prevQ) < 0) q = new Quaternion(-q.x, -q.y, -q.z, -q.w);
                        prevQ = q;
                        rootT[0][f] = pose.bodyPosition.x; rootT[1][f] = pose.bodyPosition.y; rootT[2][f] = pose.bodyPosition.z;
                        rootQ[0][f] = q.x; rootQ[1][f] = q.y; rootQ[2][f] = q.z; rootQ[3][f] = q.w;
                    }
                    var clip = new AnimationClip { name = c.Name, frameRate = RigProfiles.BakeFps };
                    for (int m = 0; m < mus.Length; m++)
                        AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve("", typeof(Animator), CurveName(muscles[m])), Curve(times, mus[m]));
                    string[] tn = { "RootT.x", "RootT.y", "RootT.z" }, qn = { "RootQ.x", "RootQ.y", "RootQ.z", "RootQ.w" };
                    for (int k = 0; k < 3; k++) AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve("", typeof(Animator), tn[k]), Curve(times, rootT[k]));
                    for (int k = 0; k < 4; k++) AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve("", typeof(Animator), qn[k]), Curve(times, rootQ[k]));
                    // In place, as authored: the body's motion stays in the pose (no root motion is extracted).
                    var st = AnimationUtility.GetAnimationClipSettings(clip);
                    st.loopTime = c.Name.EndsWith("_Loop", StringComparison.Ordinal) || c.Name == "Titan_Idle" || c.Name == "Titan_Walk";
                    st.loopBlendOrientation = true; st.loopBlendPositionY = true; st.loopBlendPositionXZ = true;
                    st.keepOriginalOrientation = true; st.keepOriginalPositionY = true; st.keepOriginalPositionXZ = true;
                    st.heightFromFeet = false; st.mirror = false;
                    AnimationUtility.SetAnimationClipSettings(clip, st);
                    AssetDatabase.CreateAsset(clip, $"{dir}/{c.Name}.anim");
                    count++;

                    // Round trip on the reference avatar: the baked clip should put the limbs where the source did.
                    float err = 0, worst = 0; int nerr = 0;
                    for (int f = 0; f < frames; f += 3)
                    {
                        Rest(s);
                        clip.SampleAnimation(s.Top, times[f]);
                        var hip = pelvis >= 0 ? s.Top.transform.InverseTransformPoint(s.Nodes[pelvis].position) : Vector3.zero;
                        for (int p = 0; p < probe.Length; p++)
                        {
                            float e = (s.Top.transform.InverseTransformPoint(s.Nodes[probe[p]].position) - hip - source[f, p]).magnitude;
                            err += e; worst = Mathf.Max(worst, e); nerr++;
                        }
                    }
                    Debug.Log($"SBHUMAN {set} {c.Name}: {frames} frames, human={clip.humanMotion}, round trip mean {100 * err / Mathf.Max(1, nerr):0.0} cm, worst {100 * worst:0.0} cm");
                }
            }
            finally { handler.Dispose(); s.Destroy(); }
            return count;
        }
    }
}
