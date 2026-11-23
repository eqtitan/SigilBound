// Hero art import pipeline (unity/ART_PIPELINE.md). Takes a folder of hero files named per
// docs/sigilbound/art/ART_BIBLE.md (SB_Hero_<id>_LOD<n>.glb|fbx, T_Hero_<id>_<map>.png,
// SB_Gear_<id>_<slot>[_r<n>].glb|fbx, SB_Anim_[<owner>_]<state>.fbx|glb) and for every hero:
//   1. copies the files under Assets/Sigilbound/Art (HeroArtPostprocessor sets the import options),
//   2. gives it a Humanoid avatar (Unity's mapping for FBX; Core.HumanoidMap for GLB or when Unity's fails),
//   3. picks the animation source: the shared retargeted library (hero -> archetype -> shared clips)
//      when the avatar is Humanoid, else the file's own clips; builds an Animator controller with one
//      state per battle state (Core.HeroClips), loops and the return to Idle included,
//   4. makes materials on Sigilbound/CharacterPainted (URP Lit if missing) from the hero's textures,
//   5. adds sockets (hands, back, head, hip, cast point) and attaches gear meshes and rarity variants,
//   6. puts LOD1/LOD2 in a LODGroup (or flags them missing),
//   7. saves Resources/Heroes/<id>.prefab with a HeroRig the game picks up at runtime,
//   8. writes a validation report (tris, bones, materials, textures, animations, sockets vs budget).
// Heroes that fail validation are saved under Art/Rejected instead, so the game keeps today's look.
// Menu: Sigilbound > Art. Batch: -executeMethod Sigilbound.EditorTools.HeroImportPipeline.ImportFromCommandLine
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using Sigilbound.Client;
using Sigilbound.Core;

namespace Sigilbound.EditorTools
{
    public static class HeroImportPipeline
    {
        const string Root = HeroArtPostprocessor.ArtRoot;
        const string Incoming = Root + "/Incoming";
        const string AnimDir = Root + "/Animations";
        const string Generated = Root + "/Generated";
        const string PrefabDir = Root + "/Resources/Heroes";      // CharacterView loads Resources/Heroes/<id>
        const string RejectedDir = Root + "/Rejected";
        const string ReportDir = Root + "/Reports";
        const string TestRoot = Root + "/Test";

        public sealed class Options
        {
            public string OutputPrefabDir = PrefabDir;
            public string GeneratedDir = Generated;
            public bool KeepFailing;          // write failing heroes to the output folder anyway
            public bool Quiet;                // no dialogs (batch mode)
            public string ReportName = "HeroImport";
        }

        // ---------- menu ----------
        [MenuItem("Sigilbound/Art/Import Hero Folder...")]
        static void MenuImportFolder()
        {
            var dir = EditorUtility.OpenFolderPanel("Folder of hero art (SB_Hero_<id>_LOD0.glb/.fbx, T_Hero_*, SB_Anim_*, SB_Gear_*)", "", "");
            if (!string.IsNullOrEmpty(dir)) Run(dir, new Options());
        }

        [MenuItem("Sigilbound/Art/Rebuild Heroes From Incoming")]
        static void MenuRebuild() => Run(Path.GetFullPath(Incoming), new Options());

        [MenuItem("Sigilbound/Art/Validate Hero Prefabs")]
        static void MenuValidate()
        {
            var stats = new List<HeroAssetStats>();
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { PrefabDir }))
            {
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
                var rig = go != null ? go.GetComponent<HeroRig>() : null;
                if (rig != null) stats.Add(Measure(go, rig));
            }
            var path = WriteReport(stats, "HeroValidate", "Hero prefab validation");
            Debug.Log($"Sigilbound art: validated {stats.Count} hero prefabs, report {path}");
        }

        // End-to-end check with a CC0 humanoid the game already ships (StreamingAssets/Models,
        // Quaternius): imports it as Brannoc into Art/Test (not Resources, so the game is unchanged).
        [MenuItem("Sigilbound/Art/Run Pipeline Test (CC0 model)")]
        public static void MenuSelfTest() => SelfTest(false);

        public static bool SelfTest(bool quiet)
        {
            var src = Path.Combine(Application.streamingAssetsPath, "Models", "adventurer.glb");
            var stage = Path.Combine(Path.GetTempPath(), "sb-art-selftest");
            if (Directory.Exists(stage)) Directory.Delete(stage, true);
            Directory.CreateDirectory(stage);
            if (!File.Exists(src)) { Debug.LogError("Sigilbound art self-test: " + src + " missing (run unity/Tools~/models first)"); return false; }
            File.Copy(src, Path.Combine(stage, ArtSpec.HeroFileName("brannoc", 0)));
            var report = Run(stage, new Options { OutputPrefabDir = TestRoot + "/Prefabs", GeneratedDir = TestRoot + "/Generated", KeepFailing = true, Quiet = quiet, ReportName = "SelfTest" }, TestRoot + "/Incoming");
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(TestRoot + "/Prefabs/brannoc.prefab");
            bool ok = prefab != null && prefab.GetComponent<HeroRig>() != null && report.Count == 1 && report[0].Clips.Contains("Idle") && report[0].Sockets.Count >= 4;
            Debug.Log($"Sigilbound art self-test: {(ok ? "PASS" : "FAIL")} (prefab {(prefab != null ? "built" : "missing")}, {report.FirstOrDefault()?.Clips.Count ?? 0} states, {report.FirstOrDefault()?.Sockets.Count ?? 0} sockets)");
            return ok;
        }

        // Batch mode: Unity -batchmode -projectPath unity -executeMethod Sigilbound.EditorTools.HeroImportPipeline.ImportFromCommandLine
        //   [-sbArtSelfTest] [-sbArtIn <folder>] [-sbArtKeepFailing] [-sbArtExitZero] [-customBuildPath <file>]
        // Runs the CC0 self-test and/or the import (both if both are given). Exit code 0 = everything
        // passed, 1 = the self-test or a hero failed validation, 2 = usage/IO error; -sbArtExitZero
        // exits 0 unless there was an error (CI reads the summary instead, so passing heroes still ship).
        // A JSON summary is written to -customBuildPath (game-ci checks that its build output exists)
        // and to Assets/Sigilbound/Art/Reports/summary.json.
        public static void ImportFromCommandLine()
        {
            int code = 2;
            var summary = new System.Text.StringBuilder("{");
            var args = Environment.GetCommandLineArgs();
            string Arg(string k) { int i = Array.IndexOf(args, k); return i >= 0 && i + 1 < args.Length && !args[i + 1].StartsWith("-") ? args[i + 1] : null; }
            bool Has(string k) => Array.IndexOf(args, k) >= 0;
            try
            {
                bool any = false, ok = true;
                if (Has("-sbArtSelfTest"))
                {
                    any = true; bool pass = SelfTest(true); ok &= pass;
                    summary.Append($"\"selfTest\":{(pass ? "true" : "false")},");
                }
                var dir = Arg("-sbArtIn");
                if (dir != null)
                {
                    if (!Directory.Exists(dir)) throw new DirectoryNotFoundException("-sbArtIn " + dir);
                    any = true;
                    var stats = Run(dir, new Options { Quiet = true, KeepFailing = Has("-sbArtKeepFailing") });
                    var pass = stats.Where(st => ArtValidation.Passes(ArtValidation.Validate(st))).Select(st => st.HeroId).ToList();
                    var fail = stats.Select(st => st.HeroId).Except(pass).ToList();
                    ok &= fail.Count == 0;
                    string J(IEnumerable<string> xs) => "[" + string.Join(",", xs.Select(x => "\"" + x + "\"")) + "]";
                    var fallback = stats.Where(st => ArtValidation.UniqueCount(st) < 5).Select(st => st.HeroId);
                    summary.Append($"\"passed\":{J(pass)},\"failed\":{J(fail)},\"usingFallbackMotion\":{J(fallback)},");
                }
                if (!any) { Debug.LogError("Sigilbound art: pass -sbArtIn <folder> and/or -sbArtSelfTest"); code = 2; }
                else code = ok || Has("-sbArtExitZero") ? 0 : 1;
                summary.Append($"\"ok\":{(ok ? "true" : "false")}}}");
            }
            catch (Exception e) { Debug.LogException(e); code = 2; summary.Append("\"error\":true}"); }
            try
            {
                EnsureFolder(ReportDir);
                File.WriteAllText($"{ReportDir}/summary.json", summary.ToString());
                var outPath = Arg("-customBuildPath");
                if (!string.IsNullOrEmpty(outPath))
                {
                    var d = Path.GetDirectoryName(outPath);
                    if (!string.IsNullOrEmpty(d)) Directory.CreateDirectory(d);
                    File.WriteAllText(outPath, summary.ToString());
                }
            }
            catch (Exception e) { Debug.LogException(e); }
            Debug.Log("Sigilbound art summary: " + summary);
            EditorApplication.Exit(code);
        }

        // ---------- pipeline ----------
        public static List<HeroAssetStats> Run(string sourceDir, Options opt, string incomingRoot = Incoming)
        {
            var results = new List<HeroAssetStats>();
            var ignored = new List<string>();
            try
            {
                EnsureFolder(incomingRoot); EnsureFolder(AnimDir); EnsureFolder(opt.GeneratedDir); EnsureFolder(opt.OutputPrefabDir); EnsureFolder(RejectedDir); EnsureFolder(ReportDir);
                // 1. Copy into the project (skipped for files already in place).
                var heroes = new SortedSet<string>();
                foreach (var file in Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories))
                {
                    if (file.EndsWith(".meta", StringComparison.OrdinalIgnoreCase)) continue;
                    var name = Path.GetFileName(file);
                    var p = ArtSpec.Parse(name);
                    if (p == null) { ignored.Add(name); continue; }
                    string destDir = p.Kind == "anim" ? AnimDir : $"{incomingRoot}/{p.HeroId}";
                    EnsureFolder(destDir);
                    var dest = $"{destDir}/{name}";
                    if (Path.GetFullPath(file) != Path.GetFullPath(dest)) File.Copy(file, dest, true);
                    if (p.HeroId != null && p.Kind == "hero") heroes.Add(p.HeroId);
                }
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

                // 2-7. Build each hero.
                int n = 0;
                foreach (var id in heroes)
                {
                    if (!opt.Quiet) EditorUtility.DisplayProgressBar("Sigilbound hero import", id, n++ / (float)Math.Max(1, heroes.Count));
                    try { results.Add(BuildHero(id, $"{incomingRoot}/{id}", opt)); }
                    catch (Exception e)
                    {
                        Debug.LogException(e);
                        var s = new HeroAssetStats { HeroId = id, Source = "exception" };
                        s.Notes.Add("import threw: " + e.Message);
                        results.Add(s);
                    }
                }
            }
            finally { if (!opt.Quiet) EditorUtility.ClearProgressBar(); }

            if (ignored.Count > 0 && results.Count > 0) results[0].Notes.Add("ignored files (not named per the convention): " + string.Join(", ", ignored.Take(20)));
            var report = WriteReport(results, opt.ReportName, "Hero art import");
            int fails = results.Count(s => !ArtValidation.Passes(ArtValidation.Validate(s)));
            var msg = $"Imported {results.Count} hero(es), {fails} failed validation. Report: {report}";
            Debug.Log("Sigilbound art: " + msg);
            if (!opt.Quiet) EditorUtility.DisplayDialog("Sigilbound hero import", msg, "OK");
            return results;
        }

        static HeroAssetStats BuildHero(string id, string dir, Options opt)
        {
            string LodPath(int lod) => new[] { "glb", "fbx", "gltf" }.Select(e => $"{dir}/{ArtSpec.HeroFileName(id, lod, e)}").FirstOrDefault(File.Exists);
            var lod0Path = LodPath(0);
            var stats = new HeroAssetStats { HeroId = id, Source = lod0Path != null ? Path.GetFileName(lod0Path) : "-" };
            if (lod0Path == null) { stats.Notes.Add("no LOD0 file"); return stats; }
            var lod0 = AssetDatabase.LoadAssetAtPath<GameObject>(lod0Path);
            if (lod0 == null) { stats.Notes.Add($"{lod0Path} did not import as a model (is glTFast installed for .glb?)"); return stats; }

            var root = new GameObject("SB_Hero_" + id);
            var model = (GameObject)UnityEngine.Object.Instantiate(lod0, root.transform);
            model.name = "Model";
            model.transform.localPosition = Vector3.zero; model.transform.localRotation = Quaternion.identity;
            var rig = root.AddComponent<HeroRig>();
            rig.HeroId = id;
            try
            {
                // 2. Humanoid avatar.
                var nodes = model.GetComponentsInChildren<Transform>(true);
                var list = nodes.Select(t => (t.name, Array.IndexOf(nodes, t.parent))).ToList();
                var map = HumanoidMap.Map(list);
                stats.MissingHumanBones.AddRange(HumanoidMap.Missing(map));
                stats.HumanoidProblems.AddRange(HumanoidMap.HierarchyProblems(map, list));
                Avatar avatar = AvatarFor(lod0Path, model, nodes, map, stats, opt);
                bool human = avatar != null && avatar.isValid && avatar.isHuman;

                // 3. Animation.
                ChooseAnimation(id, lod0Path, dir, model, human ? avatar : null, rig, stats, opt);

                // 4. Materials.
                rig.Renderers = model.GetComponentsInChildren<Renderer>(true);
                BuildMaterials(id, dir, rig.Renderers, stats, opt);

                // 5. Sockets and gear.
                BuildSockets(model.transform, nodes, map, rig, stats);
                AttachGear(id, dir, model.transform, nodes, rig, stats);

                // 6. LODs.
                BuildLods(id, root, model, nodes, LodPath, stats);

                // Measure, then save.
                rig.Renderers = root.GetComponentsInChildren<Renderer>(true);
                var b = BoundsOf(rig.Renderers);
                rig.NativeHeight = Mathf.Max(0.05f, b.size.y);
                stats.Height = rig.NativeHeight;
                CountMesh(root, stats);
                var issues = ArtValidation.Validate(stats);
                bool pass = ArtValidation.Passes(issues);
                rig.ImportSummary = $"{DateTime.Now:yyyy-MM-dd HH:mm} from {stats.Source}: {(pass ? "passed" : "FAILED")}, {stats.LodTris[0]:N0} tris, {stats.Bones} bones, anim {stats.AnimBackend}, {stats.Clips.Count} states";
                string outDir = pass || opt.KeepFailing ? opt.OutputPrefabDir : RejectedDir;
                PrefabUtility.SaveAsPrefabAsset(root, $"{outDir}/{id}.prefab", out bool saved);
                if (!saved) stats.Notes.Add("prefab could not be saved");
                else stats.Notes.Add($"prefab: {outDir}/{id}.prefab{(pass || opt.KeepFailing ? "" : " (rejected: the game keeps the current model)")}");
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
            return stats;
        }

        // ---------- 2. avatar ----------
        static Avatar AvatarFor(string assetPath, GameObject model, Transform[] nodes, Dictionary<string, int> map, HeroAssetStats stats, Options opt)
        {
            if (stats.HumanoidProblems.Count > 0 || stats.MissingHumanBones.Count > 0) return null; // Unity would reject it; keep Generic
            // FBX: Unity's own avatar if it is Humanoid; else hand it our mapping once and re-import.
            var mi = AssetImporter.GetAtPath(assetPath) as ModelImporter;
            if (mi != null)
            {
                var own = AssetDatabase.LoadAllAssetsAtPath(assetPath).OfType<Avatar>().FirstOrDefault();
                if (own != null && own.isValid && own.isHuman) return own;
                mi.animationType = ModelImporterAnimationType.Human;
                mi.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                mi.humanDescription = Describe(model.transform, nodes, map);
                mi.userData = "sb-humanoid";
                mi.SaveAndReimport();
                own = AssetDatabase.LoadAllAssetsAtPath(assetPath).OfType<Avatar>().FirstOrDefault();
                if (own == null || !own.isHuman) stats.Notes.Add("Unity rejected the Humanoid mapping for the FBX");
                return own;
            }
            // GLB (glTFast importer): build the avatar from the instance and store it as an asset.
            if (nodes.Select(t => t.name).Distinct().Count() != nodes.Length) { stats.Notes.Add("duplicate bone names: no Humanoid avatar"); return null; }
            var avatar = AvatarBuilder.BuildHumanAvatar(model, Describe(model.transform, nodes, map));
            if (avatar == null || !avatar.isValid || !avatar.isHuman) { stats.Notes.Add("AvatarBuilder could not build a Humanoid avatar"); return null; }
            avatar.name = "AV_Hero_" + stats.HeroId;
            var path = $"{opt.GeneratedDir}/AV_Hero_{stats.HeroId}.asset";
            AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(avatar, path);
            return avatar;
        }

        static HumanDescription Describe(Transform modelRoot, Transform[] nodes, Dictionary<string, int> map)
        {
            var names = HumanTrait.BoneName;
            var human = new List<HumanBone>();
            foreach (var kv in map)
            {
                // Core uses HumanBodyBones names ("LeftUpperArm"); HumanTrait uses spaced ones ("Left Upper Arm") for fingers.
                var hn = names.FirstOrDefault(x => x.Replace(" ", "") == kv.Key) ?? kv.Key;
                human.Add(new HumanBone { boneName = nodes[kv.Value].name, humanName = hn, limit = new HumanLimit { useDefaultValues = true } });
            }
            var skeleton = nodes.Select(t => new SkeletonBone { name = t.name, position = t.localPosition, rotation = t.localRotation, scale = t.localScale }).ToArray();
            return new HumanDescription
            {
                human = human.ToArray(), skeleton = skeleton,
                upperArmTwist = 0.5f, lowerArmTwist = 0.5f, upperLegTwist = 0.5f, lowerLegTwist = 0.5f,
                armStretch = 0.05f, legStretch = 0.05f, feetSpacing = 0f, hasTranslationDoF = false,
            };
        }

        // ---------- 3. animation ----------
        static IEnumerable<AnimationClip> ClipsIn(string path) =>
            AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().Where(c => !c.name.StartsWith("__preview__", StringComparison.Ordinal));

        static void ChooseAnimation(string id, string lod0Path, string dir, GameObject model, Avatar avatar, HeroRig rig, HeroAssetStats stats, Options opt)
        {
            // a) Retargeted library (Humanoid clips), in the policy's order per state (Core.ArtSpec.AnimSources):
            //    shared states shared-first, unique states the hero's own first, then a tag-matched
            //    archetype variant (SB_Anim_<Archetype>_<State>@<variant>), then the plain archetype clip.
            var library = new Dictionary<string, AnimationClip>();
            var origin = new Dictionary<string, string>();
            var heroTags = ReadHeroTags(dir, id);
            var variantTags = ReadVariantTags();
            AnimationClip Humanoid(string path) => File.Exists(path) ? ClipsIn(path).FirstOrDefault(k => !k.legacy && k.humanMotion) : null;
            if (avatar != null)
                foreach (var c in HeroClips.All)
                    foreach (var (cand, org) in ArtSpec.AnimSources(id, c.Name))
                    {
                        AnimationClip clip = null; var o = org;
                        if (org == ArtSpec.ClipOrigin.Archetype && c.Unique)
                        {
                            // variants of this archetype/state, picked by motion tags
                            var stem = Path.GetFileNameWithoutExtension(cand);
                            var vars = Directory.Exists(AnimDir) ? Directory.GetFiles(AnimDir, stem + "@*.fbx").Select(f => (File: Path.GetFileName(f), Tags: variantTags.TryGetValue(Path.GetFileName(f), out var t) ? t : new string[0])).ToList() : new List<(string, string[])>();
                            var pick = ArtSpec.PickVariant(id, heroTags, vars);
                            if (pick != null) { clip = Humanoid($"{AnimDir}/{pick}"); o = ArtSpec.ClipOrigin.Tagged; }
                        }
                        if (clip == null) clip = Humanoid($"{AnimDir}/{cand}");
                        if (clip != null) { library[c.Name] = clip; origin[c.Name] = o.ToString().ToLowerInvariant(); break; }
                    }
            bool libraryOk = avatar != null && HeroClips.All.Where(c => c.Required).All(c => HeroClips.Effective(c.Name, library.Keys) != null);

            // b) The file's own clips, plus per-hero clip files (SB_Anim_<id>_<state>.glb, same rig).
            var own = ClipsIn(lod0Path).ToList();
            var embedded = HeroClips.Resolve(own.Select(c => c.name)).ToDictionary(kv => kv.Key, kv => own.First(c => c.name == kv.Value));
            var embeddedOrigin = embedded.Keys.ToDictionary(k => k, k => "embedded");
            foreach (var c in HeroClips.All)
            {
                var p = $"{AnimDir}/{ArtSpec.AnimFileName(c.Name, id, "glb")}";
                var k = File.Exists(p) ? ClipsIn(p).FirstOrDefault() : null;
                if (k != null) { embedded[c.Name] = k; embeddedOrigin[c.Name] = "hero"; }
            }

            var legacyAnim = model.GetComponentInChildren<Animation>(true);
            var animator = model.GetComponentInChildren<Animator>(true);
            Dictionary<string, AnimationClip> use;
            if (libraryOk) { use = library; stats.AnimBackend = "humanoid-library"; foreach (var kv in origin) stats.ClipOrigins[kv.Key] = kv.Value; }
            else
            {
                use = embedded;
                stats.AnimBackend = use.Count == 0 ? "none" : use.Values.All(c => c.legacy) ? "embedded-legacy" : "embedded-mecanim";
                foreach (var kv in embeddedOrigin) stats.ClipOrigins[kv.Key] = kv.Value;
                if (avatar != null && library.Count > 0) stats.Notes.Add($"shared library incomplete for this hero ({library.Count} states); using the file's own clips");
            }
            foreach (var k in use.Keys) stats.Clips.Add(k);
            if (use.Count == 0) return;

            var keys = new List<string>(); var states = new List<string>(); var lengths = new List<float>();
            bool useLegacy = !libraryOk && use.Values.All(c => c.legacy);
            if (useLegacy)
            {
                // Legacy Animation (glTFast's default for .glb clips): keep the component, add our clips.
                if (legacyAnim == null) legacyAnim = model.AddComponent<Animation>();
                foreach (var c in use.Values.Distinct()) if (legacyAnim.GetClip(c.name) == null) legacyAnim.AddClip(c, c.name);
                if (use.TryGetValue("Idle", out var idle)) { legacyAnim.clip = idle; legacyAnim.playAutomatically = true; }
                if (animator != null) UnityEngine.Object.DestroyImmediate(animator);
                rig.Legacy = legacyAnim;
                foreach (var c in HeroClips.All)
                {
                    var eff = HeroClips.Effective(c.Name, use.Keys);
                    if (eff == null) continue;
                    keys.Add(c.Name); states.Add(use[eff].name); lengths.Add(use[eff].length);
                }
            }
            else
            {
                var mecanim = use.Where(kv => !kv.Value.legacy).ToDictionary(kv => kv.Key, kv => kv.Value);
                var controller = BuildController(id, mecanim, opt);
                if (animator == null) animator = model.AddComponent<Animator>();
                animator.avatar = libraryOk ? avatar : animator.avatar;
                animator.runtimeAnimatorController = controller;
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
                if (legacyAnim != null) UnityEngine.Object.DestroyImmediate(legacyAnim);
                rig.Animator = animator;
                foreach (var c in HeroClips.All)
                {
                    var eff = HeroClips.Effective(c.Name, mecanim.Keys);
                    if (eff == null) continue;
                    keys.Add(c.Name); states.Add(eff); lengths.Add(mecanim[eff].length);
                }
            }
            rig.StateKeys = keys.ToArray(); rig.StateNames = states.ToArray(); rig.StateLengths = lengths.ToArray();
        }

        // Motion tags for the tag-based fallback: SB_Hero_<id>.json (written by heroforge from the
        // hero sheets) with { "motionTags": ["whip", "fast", ...] }.
        static string[] ReadHeroTags(string dir, string id)
        {
            var p = $"{dir}/{ArtSpec.MetaFileName(id)}";
            if (!File.Exists(p)) return new string[0];
            try { return (Json.Arr(Json.Parse(File.ReadAllText(p)), "motionTags") ?? new List<object>()).Select(x => x as string).Where(x => x != null).ToArray(); }
            catch (Exception) { return new string[0]; }
        }
        // Animations/anim-tags.json: { "SB_Anim_Blade_Attack_1@spin.fbx": ["spin", "wide"], ... }
        static Dictionary<string, string[]> ReadVariantTags()
        {
            var p = $"{AnimDir}/anim-tags.json"; var o = new Dictionary<string, string[]>();
            if (!File.Exists(p)) return o;
            try { foreach (var kv in (Dictionary<string, object>)Json.Parse(File.ReadAllText(p))) o[kv.Key] = ((List<object>)kv.Value).Select(x => x as string).Where(x => x != null).ToArray(); }
            catch (Exception) { }
            return o;
        }

        // One Animator state per battle state; once-off states return to Idle, Death holds.
        static AnimatorController BuildController(string id, Dictionary<string, AnimationClip> clips, Options opt)
        {
            var path = $"{opt.GeneratedDir}/AC_Hero_{id}.controller";
            AssetDatabase.DeleteAsset(path);
            var ac = AnimatorController.CreateAnimatorControllerAtPath(path);
            ac.AddParameter("Speed", AnimatorControllerParameterType.Float);
            var ps = ac.parameters; ps[0].defaultFloat = 1f; ac.parameters = ps; // playback speed multiplier (HeroModel.Play)
            var sm = ac.layers[0].stateMachine;
            var made = new Dictionary<string, AnimatorState>();
            foreach (var c in HeroClips.All)
            {
                if (!clips.TryGetValue(c.Name, out var clip)) continue;
                var st = sm.AddState(c.Name);
                st.motion = clip;
                st.writeDefaultValues = true;
                if (c.SpeedParam) { st.speedParameterActive = true; st.speedParameter = "Speed"; }
                made[c.Name] = st;
            }
            if (made.TryGetValue("Idle", out var idle))
            {
                sm.defaultState = idle;
                foreach (var c in HeroClips.All)
                {
                    if (c.Loop || c.Clamp || !made.TryGetValue(c.Name, out var st) || st == idle) continue;
                    var tr = st.AddTransition(idle);
                    tr.hasExitTime = true;
                    tr.hasFixedDuration = true;
                    tr.duration = 0.22f;
                    float len = Mathf.Max(0.1f, clips[c.Name].length);
                    tr.exitTime = Mathf.Clamp01(1f - 0.22f / len);
                }
            }
            AssetDatabase.SaveAssets();
            return ac;
        }

        // ---------- 4. materials ----------
        static void BuildMaterials(string id, string dir, Renderer[] renderers, HeroAssetStats stats, Options opt)
        {
            Texture2D Tex(string map)
            {
                foreach (var ext in new[] { "png", "tga", "jpg", "jpeg" })
                {
                    var p = $"{dir}/T_Hero_{id}_{map}.{ext}";
                    var t = AssetDatabase.LoadAssetAtPath<Texture2D>(p);
                    if (t != null) { RecordTexture(p, t, stats); return t; }
                }
                return null;
            }
            Texture2D albedo = Tex("Albedo"), normal = Tex("Normal"), mask = Tex("Mask"), emission = Tex("Emission");
            var painted = Shader.Find("Sigilbound/CharacterPainted");
            var lit = Shader.Find("Universal Render Pipeline/Lit");
            if (painted == null) stats.Notes.Add("Sigilbound/CharacterPainted not found: materials use URP Lit");
            var made = new Dictionary<Material, Material>();
            int slot = 0;
            foreach (var r in renderers)
            {
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                {
                    var src = mats[i];
                    if (src != null && made.TryGetValue(src, out var done)) { mats[i] = done; continue; }
                    // Per-slot texture: the hero's own atlas, else whatever the file's material carried.
                    var srcTex = src == null ? null : new[] { "_BaseMap", "_MainTex", "baseColorTexture" }.Where(src.HasProperty).Select(src.GetTexture).OfType<Texture2D>().FirstOrDefault();
                    if (albedo == null && srcTex != null) RecordTexture(AssetDatabase.GetAssetPath(srcTex), srcTex, stats);
                    var srcColor = src == null ? Color.white : new[] { "_BaseColor", "_Color", "baseColorFactor" }.Where(src.HasProperty).Select(src.GetColor).DefaultIfEmpty(Color.white).First();
                    bool alpha = src != null && System.Text.RegularExpressions.Regex.IsMatch(src.name, "alpha|card|hair|feather|fringe|leaf", System.Text.RegularExpressions.RegexOptions.IgnoreCase) && (albedo ?? srcTex) != null;
                    var tex = albedo ?? srcTex;
                    var m = new Material(painted != null ? painted : lit) { name = ArtSpec.MaterialName(id, alpha ? "Alpha" : slot == 0 ? "Body" : "Slot" + slot) };
                    if (painted != null)
                    {
                        if (tex != null) m.SetTexture("_BaseMap", tex);
                        m.SetColor("_BaseColor", tex != null ? Color.white : srcColor);
                        if (mask != null) m.SetTexture("_MaskMap", mask);
                        else { m.SetFloat("_Metallic", 0f); m.SetFloat("_Smoothness", 0.35f); }
                        if (normal != null) m.SetTexture("_BumpMap", normal);
                        m.SetFloat("_Wrap", 0.3f); m.SetFloat("_Ramp", 0.35f); m.SetFloat("_Rim", 0.7f); m.SetFloat("_Sheen", 0.15f);
                        if (alpha) { m.SetFloat("_Cutoff", 0.5f); m.SetFloat("_Cull", 0f); }
                    }
                    else if (lit != null)
                    {
                        if (tex != null) m.SetTexture("_BaseMap", tex);
                        m.SetColor("_BaseColor", tex != null ? Color.white : srcColor);
                        if (normal != null) { m.SetTexture("_BumpMap", normal); m.EnableKeyword("_NORMALMAP"); }
                        if (mask != null) { m.SetTexture("_MetallicGlossMap", mask); m.EnableKeyword("_METALLICSPECGLOSSMAP"); m.SetTexture("_OcclusionMap", mask); m.EnableKeyword("_OCCLUSIONMAP"); }
                        m.SetFloat("_Smoothness", 0.35f);
                        if (emission != null) { m.SetTexture("_EmissionMap", emission); m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", Color.white); }
                        if (alpha) { m.SetFloat("_AlphaClip", 1f); m.SetFloat("_Cutoff", 0.5f); m.EnableKeyword("_ALPHATEST_ON"); m.SetFloat("_Cull", 0f); }
                    }
                    var path = $"{opt.GeneratedDir}/{m.name}.mat";
                    AssetDatabase.DeleteAsset(path);
                    AssetDatabase.CreateAsset(m, path);
                    if (src != null) made[src] = m;
                    mats[i] = m;
                    slot++;
                }
                r.sharedMaterials = mats;
            }
            if (albedo == null && stats.Textures.Count == 0) stats.Notes.Add("no textures (T_Hero_<id>_Albedo.png or embedded): flat colours from the file");
        }

        static void RecordTexture(string path, Texture2D t, HeroAssetStats stats)
        {
            int w = t.width, h = t.height;
            if (AssetImporter.GetAtPath(path) is TextureImporter ti) ti.GetSourceTextureWidthAndHeight(out w, out h);
            if (!stats.Textures.Any(x => x.Name == t.name)) stats.Textures.Add((t.name, w, h));
        }

        // ---------- 5. sockets and gear ----------
        static void BuildSockets(Transform model, Transform[] nodes, Dictionary<string, int> map, HeroRig rig, HeroAssetStats stats)
        {
            Transform Bone(params string[] keys) { foreach (var k in keys) if (map.TryGetValue(k, out var i)) return nodes[i]; return null; }
            var height = Mathf.Max(0.05f, BoundsOf(model.GetComponentsInChildren<Renderer>(true)).size.y);
            float u = height / (float)ArtSpec.BaseHeight; // offsets below are for a 2.05 m hero
            Transform Socket(string name, Transform bone, Vector3 offsetModel)
            {
                var existing = nodes.FirstOrDefault(t => t.name == name); // authored in the file: keep it
                if (existing != null) { stats.Sockets.Add(name); return existing; }
                if (bone == null) return null;
                var s = new GameObject(name).transform;
                s.SetParent(bone, false);
                s.position = bone.position + model.rotation * (offsetModel * u);
                s.rotation = model.rotation; // model axes: +Y up, +Z forward (weapons are modelled upright, grip at the origin)
                stats.Sockets.Add(name);
                return s;
            }
            rig.SocketHandR = Socket("Socket_HandR", Bone("RightHand"), new Vector3(0, -0.06f, 0.02f));
            rig.SocketHandL = Socket("Socket_HandL", Bone("LeftHand"), new Vector3(0, -0.06f, 0.02f));
            rig.SocketBack = Socket("Socket_Back", Bone("UpperChest", "Chest", "Spine"), new Vector3(0, 0, -0.16f));
            rig.SocketHead = Socket("Socket_Head", Bone("Head"), new Vector3(0, 0.2f, 0));
            rig.SocketHip = Socket("Socket_Hip", Bone("Hips"), new Vector3(0.14f, 0, 0));
            rig.SocketCast = Socket("Socket_Cast", Bone("RightHand"), new Vector3(0, 0, 0.25f));
        }

        static void AttachGear(string id, string dir, Transform model, Transform[] nodes, HeroRig rig, HeroAssetStats stats)
        {
            var variants = new List<GameObject>();
            var byName = nodes.GroupBy(t => t.name).ToDictionary(g => g.Key, g => g.First());
            foreach (var file in Directory.GetFiles(dir).Select(Path.GetFileName))
            {
                var p = ArtSpec.Parse(file);
                if (p == null || p.Kind != "gear") continue;
                var asset = AssetDatabase.LoadAssetAtPath<GameObject>($"{dir}/{file}");
                if (asset == null) continue;
                Transform socket = p.Slot == "Weapon" ? rig.SocketHandR : p.Slot == "Offhand" ? rig.SocketHandL : p.Slot == "Helm" ? rig.SocketHead : p.Slot == "Back" ? rig.SocketBack : model;
                var inst = (GameObject)UnityEngine.Object.Instantiate(asset, socket != null ? socket : model, false);
                inst.name = p.Rarity > 0 ? $"Gear_{p.Slot}_r{p.Rarity}" : $"Gear_{p.Slot}";
                // Skinned gear (chest, boots, capes) binds to the hero's skeleton by bone name.
                foreach (var smr in inst.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    var bones = smr.bones.Select(b => b != null && byName.TryGetValue(b.name, out var hb) ? hb : null).ToArray();
                    if (bones.Any(b => b == null)) { stats.Notes.Add($"{file}: some bones are not in the hero skeleton"); continue; }
                    smr.bones = bones;
                    if (smr.rootBone != null && byName.TryGetValue(smr.rootBone.name, out var rb)) smr.rootBone = rb;
                    smr.transform.SetParent(model, true);
                }
                if (p.Rarity > 0) variants.Add(inst);
                stats.Notes.Add($"gear: {inst.name} on {(socket != null ? socket.name : "model")}");
            }
            rig.GearVariants = variants.ToArray();
        }

        // ---------- 6. LODs ----------
        static void BuildLods(string id, GameObject root, GameObject model, Transform[] nodes, Func<int, string> lodPath, HeroAssetStats stats)
        {
            var byName = nodes.GroupBy(t => t.name).ToDictionary(g => g.Key, g => g.First());
            var levels = new List<LOD> { new LOD(0.45f, model.GetComponentsInChildren<Renderer>(true)) };
            float[] heights = { 0.45f, 0.12f, 0f };
            for (int lod = 1; lod <= 2; lod++)
            {
                var path = lodPath(lod);
                var asset = path != null ? AssetDatabase.LoadAssetAtPath<GameObject>(path) : null;
                if (asset == null) continue;
                var inst = (GameObject)UnityEngine.Object.Instantiate(asset);
                var rs = new List<Renderer>();
                foreach (var smr in inst.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    var bones = smr.bones.Select(b => b != null && byName.TryGetValue(b.name, out var hb) ? hb : null).ToArray();
                    if (bones.Any(b => b == null)) { stats.Notes.Add($"LOD{lod}: skeleton differs from LOD0; skipped"); rs.Clear(); break; }
                    smr.bones = bones;
                    if (smr.rootBone != null && byName.TryGetValue(smr.rootBone.name, out var rb)) smr.rootBone = rb;
                    var lod0Mats = model.GetComponentsInChildren<SkinnedMeshRenderer>(true).Select(x => x.sharedMaterials).FirstOrDefault(a => a.Length > 0) ?? new Material[0];
                    if (lod0Mats.Length > 0) smr.sharedMaterials = Enumerable.Range(0, smr.sharedMaterials.Length).Select(i => lod0Mats[Math.Min(i, lod0Mats.Length - 1)]).ToArray();
                    smr.transform.SetParent(model.transform, false);
                    smr.name = $"LOD{lod}_{smr.name}";
                    rs.Add(smr);
                }
                UnityEngine.Object.DestroyImmediate(inst);
                if (rs.Count == 0) continue;
                stats.LodTris[lod] = rs.Sum(r => Tris(r));
                levels.Add(new LOD(heights[lod], rs.ToArray()));
            }
            if (levels.Count > 1)
            {
                levels[levels.Count - 1] = new LOD(0.01f, levels[levels.Count - 1].renderers); // never culled in battle
                var group = root.AddComponent<LODGroup>();
                group.SetLODs(levels.ToArray());
                group.RecalculateBounds();
            }
        }

        // ---------- measuring and reports ----------
        static int Tris(Renderer r)
        {
            Mesh m = r is SkinnedMeshRenderer s ? s.sharedMesh : r.GetComponent<MeshFilter>() != null ? r.GetComponent<MeshFilter>().sharedMesh : null;
            if (m == null) return 0;
            long n = 0;
            for (int i = 0; i < m.subMeshCount; i++) n += m.GetIndexCount(i) / 3;
            return (int)n;
        }

        static void CountMesh(GameObject root, HeroAssetStats stats)
        {
            var all = root.GetComponentsInChildren<Renderer>(true);
            var lod0 = all.Where(r => !r.name.StartsWith("LOD1_", StringComparison.Ordinal) && !r.name.StartsWith("LOD2_", StringComparison.Ordinal)).ToArray();
            // Rarity variants are alternatives: count only the base pieces in the totals.
            var countable = lod0.Where(r => !r.GetComponentsInParent<Transform>(true).Any(t => t.name.StartsWith("Gear_", StringComparison.Ordinal) && t.name.Contains("_r"))).ToArray();
            stats.LodTris[0] = countable.Sum(Tris);
            var skinned = countable.OfType<SkinnedMeshRenderer>().ToArray();
            stats.SkinnedRenderers = skinned.Length;
            stats.Bones = skinned.SelectMany(s => s.bones).Where(b => b != null).Distinct().Count();
            stats.Materials = countable.SelectMany(r => r.sharedMaterials).Where(m => m != null).Distinct().Count();
            foreach (var s in skinned)
            {
                var m = s.sharedMesh;
                if (m == null) continue;
                try { var per = m.GetBonesPerVertex(); if (per.Length > 0) stats.MaxBoneWeights = Math.Max(stats.MaxBoneWeights, per.Max()); }
                catch (Exception) { } // not readable: the importer's 4-weight setting applies anyway
            }
        }

        static HeroAssetStats Measure(GameObject prefab, HeroRig rig)
        {
            var s = new HeroAssetStats { HeroId = rig.HeroId, Source = AssetDatabase.GetAssetPath(prefab), Height = rig.NativeHeight };
            CountMesh(prefab, s);
            var group = prefab.GetComponent<LODGroup>();
            if (group != null) { var lods = group.GetLODs(); for (int i = 1; i < Math.Min(3, lods.Length); i++) s.LodTris[i] = lods[i].renderers.Where(r => r != null).Sum(Tris); }
            foreach (var k in rig.StateKeys) s.Clips.Add(k);
            s.AnimBackend = rig.Animator != null ? "animator" : rig.Legacy != null ? "embedded" : "none";
            foreach (var (name, t) in new[] { ("Socket_HandR", rig.SocketHandR), ("Socket_HandL", rig.SocketHandL), ("Socket_Back", rig.SocketBack), ("Socket_Head", rig.SocketHead), ("Socket_Hip", rig.SocketHip), ("Socket_Cast", rig.SocketCast) })
                if (t != null) s.Sockets.Add(name);
            foreach (var r in rig.Renderers.Where(r => r != null))
                foreach (var m in r.sharedMaterials.Where(m => m != null))
                    foreach (var prop in new[] { "_BaseMap", "_MaskMap", "_BumpMap" })
                        if (m.HasProperty(prop) && m.GetTexture(prop) is Texture2D t) RecordTexture(AssetDatabase.GetAssetPath(t), t, s);
            return s;
        }

        static Bounds BoundsOf(Renderer[] rs)
        {
            var live = rs.Where(r => r != null).ToArray();
            if (live.Length == 0) return new Bounds(Vector3.zero, Vector3.one * 2);
            var b = live[0].bounds;
            foreach (var r in live) b.Encapsulate(r.bounds);
            return b;
        }

        static string WriteReport(List<HeroAssetStats> stats, string name, string title)
        {
            EnsureFolder(ReportDir);
            var md = ArtValidation.Markdown(stats, $"{title}, {DateTime.Now:yyyy-MM-dd HH:mm}");
            var path = $"{ReportDir}/{name}_{DateTime.Now:yyyyMMdd_HHmmss}.md";
            File.WriteAllText(path, md);
            File.WriteAllText($"{ReportDir}/{name}_latest.md", md);
            AssetDatabase.ImportAsset(path);
            return path;
        }

        static void EnsureFolder(string path)
        {
            path = path.Replace('\\', '/').TrimEnd('/');
            if (AssetDatabase.IsValidFolder(path)) return;
            var parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
