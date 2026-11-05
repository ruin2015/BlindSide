// Inspector helpers for UnitAnimationProfile: fill every slot from a folder of clips by their names, add a slot per ability,
// and say what is still missing. Plus a menu to switch selected model files to the Humanoid rig.
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Blindside.Rendering.AnimationTools
{
    [CustomEditor(typeof(UnitAnimationProfile))]
    public class UnitAnimationProfileEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            var p = (UnitAnimationProfile)target;

            EditorGUILayout.HelpBox(
                "1) Auto-fill: pick the folder with the animation files (.fbx / .anim); clips are matched by name " +
                "(idle, aim, walk/run + back/left/right, fire/shoot, reload, hit, death/die, and the ability names).\n" +
                "2) Put this profile on OperatorData.animationProfile (one operator) or on the unit prefab's UnitView (everyone), " +
                "together with the model.", MessageType.Info);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Auto-fill from folder…")) AutoFill(p);
                if (GUILayout.Button("Add a slot per ability")) AddAbilitySlots(p);
            }
            var missing = Missing(p);
            if (missing.Count > 0) EditorGUILayout.HelpBox("Empty: " + string.Join(", ", missing) + (p.idle == null ? "\n'idle' is required — without it the model stays unanimated." : ""), p.idle == null ? MessageType.Warning : MessageType.None);
            EditorGUILayout.Space();
            DrawDefaultInspector();
        }

        static List<string> Missing(UnitAnimationProfile p)
        {
            var m = new List<string>();
            if (p.idle == null) m.Add("idle");
            if (p.aim == null) m.Add("aim");
            if (p.walkForward == null) m.Add("walkForward");
            if (!p.HasDirectionalWalk) m.Add("leg directions (optional: with 4 or 8 the legs blend, else the spine twists)");
            if (p.moveUpperBody == null) m.Add("moveUpperBody (optional: the aim pose while moving)");
            if (p.fire == null) m.Add("fire");
            if (p.reload == null) m.Add("reload");
            if (p.hit == null) m.Add("hit");
            if (p.dodge == null && p.block == null) m.Add("dodge / block");
            if (p.death == null) m.Add("death");
            var withClip = new HashSet<string>(p.abilities.Where(a => a != null && a.clip != null).Select(a => a.abilityKey));
            var noClip = UnitAnimationProfile.AbilityKeys.Where(k => !withClip.Contains(k)).ToList();
            if (noClip.Count > 0) m.Add("abilities: " + string.Join("/", noClip));
            return m;
        }

        static void AddAbilitySlots(UnitAnimationProfile p)
        {
            Undo.RecordObject(p, "Add ability slots");
            foreach (var k in UnitAnimationProfile.AbilityKeys)
                if (!p.abilities.Any(a => a != null && a.abilityKey == k))
                    p.abilities.Add(new UnitAnimationProfile.AbilityClip { abilityKey = k, fullBody = k == "hammer" || k == "launcher" || k == "gas" });
            EditorUtility.SetDirty(p);
        }

        static void AutoFill(UnitAnimationProfile p)
        {
            string abs = EditorUtility.OpenFolderPanel("Animation clips folder", Application.dataPath, "");
            if (string.IsNullOrEmpty(abs)) return;
            abs = abs.Replace('\\', '/');
            string dataPath = Application.dataPath.Replace('\\', '/');
            if (!abs.StartsWith(dataPath)) { EditorUtility.DisplayDialog("Auto-fill", "Pick a folder inside this project's Assets.", "OK"); return; }
            string folder = "Assets" + abs.Substring(dataPath.Length);

            var clips = new List<AnimationClip>();
            foreach (var guid in AssetDatabase.FindAssets("t:AnimationClip t:Model", new[] { folder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                foreach (var o in AssetDatabase.LoadAllAssetsAtPath(path))
                    if (o is AnimationClip c && !c.name.StartsWith("__preview__") && !clips.Contains(c)) clips.Add(c);
            }
            if (clips.Count == 0) { EditorUtility.DisplayDialog("Auto-fill", "No animation clips in " + folder + ".", "OK"); return; }

            Undo.RecordObject(p, "Auto-fill animation profile");
            int filled = 0;
            AnimationClip Pick(System.Func<string, bool> match) => clips.FirstOrDefault(c => match(c.name.ToLowerInvariant()));
            bool Has(string n, params string[] words) => words.Any(w => n.Contains(w));
            bool Walky(string n) => Has(n, "walk", "run", "move", "jog");
            bool Dir(string n) => Has(n, "back", "bwd", "left", "right", "strafe");

            void Set(ref AnimationClip slot, AnimationClip c) { if (c != null) { slot = c; filled++; } }
            // directions: "forward left", "backward right", "left", "strafe right", … (Mixamo packs name the file, not the take)
            bool Fw(string n) => Has(n, "forward", "foward", "fwd", "front");
            bool Bw(string n) => Has(n, "backward", "back", "bwd");
            bool L(string n) => Has(n, "left"); bool R(string n) => Has(n, "right");
            bool Death(string n) => Has(n, "death", "die", "dying", "dead", "hittoback", "hit to back");
            Set(ref p.idle, Pick(n => Has(n, "idle") && !Has(n, "aim")) ?? Pick(n => Has(n, "idle")));
            Set(ref p.aim, Pick(n => Has(n, "aim") && !Walky(n) && !Has(n, "fire", "shoot")));
            Set(ref p.moveUpperBody, Pick(n => Has(n, "run") && !Dir(n)));
            Set(ref p.walkForward, Pick(n => Fw(n) && !L(n) && !R(n)) ?? Pick(n => Walky(n) && !Dir(n)));
            Set(ref p.walkForwardLeft, Pick(n => Fw(n) && L(n)));
            Set(ref p.walkForwardRight, Pick(n => Fw(n) && R(n)));
            Set(ref p.strafeLeft, Pick(n => L(n) && !Fw(n) && !Bw(n)));
            Set(ref p.strafeRight, Pick(n => R(n) && !Fw(n) && !Bw(n)));
            Set(ref p.walkBackward, Pick(n => Bw(n) && !L(n) && !R(n) && !Death(n)));
            Set(ref p.walkBackwardLeft, Pick(n => Bw(n) && L(n)));
            Set(ref p.walkBackwardRight, Pick(n => Bw(n) && R(n)));
            Set(ref p.fire, Pick(n => Has(n, "firing", "fire", "shoot", "shot", "attack", "recoil")));
            Set(ref p.reload, Pick(n => Has(n, "reload")));
            Set(ref p.hit, Pick(n => Has(n, "hit reaction", "hitreaction", "damage", "impact", "react", "flinch")) ?? Pick(n => Has(n, "hit") && !Death(n)));
            Set(ref p.dodge, Pick(n => Has(n, "dodg", "evade")));
            Set(ref p.block, Pick(n => Has(n, "block")));
            Set(ref p.death, Pick(n => Death(n)));

            AddAbilitySlots(p);
            var aliases = new Dictionary<string, string[]>
            {
                { "hammer", new[] { "hammer", "smash", "swing" } }, { "launcher", new[] { "launcher", "launch" } }, { "thermite", new[] { "thermite", "plant" } },
                { "stim", new[] { "stim", "inject", "heal" } }, { "shield", new[] { "shield" } }, { "shock", new[] { "shock", "electric" } },
                { "gas", new[] { "gas", "throw", "grenade" } }, { "ghost", new[] { "ghost", "jam" } }, { "mirror", new[] { "mirror" } },
                { "cam", new[] { "camera", "cam" } }, { "drone", new[] { "drone" } },
            };
            foreach (var a in p.abilities)
            {
                if (a == null || a.clip != null || !aliases.TryGetValue(a.abilityKey ?? "", out var words)) continue;
                var c = Pick(n => Has(n, words));
                if (c != null) { a.clip = c; filled++; }
            }
            EditorUtility.SetDirty(p);
            EditorUtility.DisplayDialog("Auto-fill", $"{clips.Count} clips found, {filled} slots filled. Check the result below.", "OK");
        }

        // ---- gun grip ----
        /// <summary>
        /// For every operator with a humanoid model and a profile: poses the model in the profile's aim clip (idle without one)
        /// and stores in the profile how the right hand holds a rifle — the barrel toward the left hand, up, the palm — in
        /// right-hand space. UnitView puts the gun there (a freshly spawned rig can't be posed before its first frame).
        /// </summary>
        [MenuItem("Blindside/Animation/Calibrate Operator Grips")]
        static void CalibrateGripsMenu() => EditorUtility.DisplayDialog("Grips", CalibrateOperatorGrips(), "OK");

        public static string CalibrateOperatorGrips()
        {
            var lines = new List<string>();
            foreach (var guid in AssetDatabase.FindAssets("t:Blindside.Data.OperatorData"))
            {
                var op = AssetDatabase.LoadAssetAtPath<Blindside.Data.OperatorData>(AssetDatabase.GUIDToAssetPath(guid));
                var p = op != null ? op.animationProfile : null;
                if (op == null || op.modelPrefab == null || p == null) continue;
                var pose = p.aim != null ? p.aim : p.idle;
                if (pose == null) { lines.Add(op.key + ": no aim / idle clip"); continue; }
                var inst = Object.Instantiate(op.modelPrefab);
                inst.hideFlags = HideFlags.HideAndDontSave;
                try
                {
                    var an = inst.GetComponentInChildren<Animator>();
                    if (an == null || an.avatar == null || !an.avatar.isHuman) { lines.Add(op.key + ": not humanoid"); continue; }
                    AnimationMode.StartAnimationMode();
                    AnimationMode.BeginSampling();
                    AnimationMode.SampleAnimationClip(an.gameObject, pose, pose.length * .5f);
                    AnimationMode.EndSampling();
                    var rh = an.GetBoneTransform(HumanBodyBones.RightHand);
                    var lh = an.GetBoneTransform(HumanBodyBones.LeftHand);
                    var mid = an.GetBoneTransform(HumanBodyBones.RightMiddleProximal);
                    var lmid = an.GetBoneTransform(HumanBodyBones.LeftMiddleProximal);
                    Vector3 palm = mid != null ? Vector3.Lerp(rh.position, mid.position, .55f) : rh.position;
                    Vector3 lpalm = lmid != null ? Vector3.Lerp(lh.position, lmid.position, .55f) : lh.position;
                    // the clip aims straight ahead: the barrel runs along the figure's forward
                    var fwd = inst.transform.forward;
                    var barrelFrame = Quaternion.LookRotation(fwd, Vector3.up);
                    Undo.RecordObject(p, "Calibrate grip");
                    p.gripBarrel = rh.InverseTransformDirection(fwd);
                    p.gripUp = rh.InverseTransformDirection(Vector3.up);
                    p.gripPalm = rh.InverseTransformPoint(palm);
                    p.gripLeftRot = Quaternion.Inverse(barrelFrame) * lh.rotation;
                    p.gripLeftPalm = lh.InverseTransformPoint(lpalm);
                    p.gripCalibrated = true;
                    EditorUtility.SetDirty(p);
                    lines.Add($"{op.key} ({p.name}): barrel {p.gripBarrel:F2}");
                }
                finally
                {
                    if (AnimationMode.InAnimationMode()) AnimationMode.StopAnimationMode();
                    Object.DestroyImmediate(inst);
                }
            }
            AssetDatabase.SaveAssets();
            return string.Join("\n", lines);
        }

        // ---- model import ----
        [MenuItem("Blindside/Animation/Set Selected Models To Humanoid")]
        static void SetHumanoid()
        {
            int n = 0;
            var notes = new List<string>();
            foreach (var o in Selection.objects)
            {
                string path = AssetDatabase.GetAssetPath(o);
                if (!(AssetImporter.GetAtPath(path) is ModelImporter mi)) continue;
                mi.animationType = ModelImporterAnimationType.Human;
                mi.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                mi.SaveAndReimport();
                var avatar = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Avatar>().FirstOrDefault();
                if (avatar == null || !avatar.isValid || !avatar.isHuman) notes.Add(System.IO.Path.GetFileName(path) + " — no valid humanoid skeleton (rig the model first, e.g. Mixamo)");
                n++;
            }
            EditorUtility.DisplayDialog("Humanoid", $"{n} model(s) switched to Humanoid." + (notes.Count > 0 ? "\n\n" + string.Join("\n", notes) : ""), "OK");
        }

        [MenuItem("Blindside/Animation/Set Selected Models To Humanoid", true)]
        static bool SetHumanoidValid() => Selection.objects.Any(o => AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(o)) is ModelImporter);

        /// <summary>
        /// Animation-only FBX files (e.g. a Mixamo pack, every take called "mixamo.com"), selected or inside a selected folder:
        /// Humanoid rig from the file itself, the take renamed after the file, looping for idle / aim / run / walk / direction
        /// clips, and played in place (root motion baked into the pose — the game moves the unit itself).
        /// </summary>
        [MenuItem("Blindside/Animation/Prepare Selected Animation FBX (Humanoid, in place)")]
        static void PrepareAnimations()
        {
            var paths = new List<string>();
            foreach (var o in Selection.objects)
            {
                string path = AssetDatabase.GetAssetPath(o);
                if (AssetDatabase.IsValidFolder(path)) paths.AddRange(AssetDatabase.FindAssets("t:Model", new[] { path }).Select(AssetDatabase.GUIDToAssetPath));
                else if (AssetImporter.GetAtPath(path) is ModelImporter) paths.Add(path);
            }
            int n = 0;
            foreach (var path in paths.Distinct()) if (PrepareAnimation(path)) n++;
            EditorUtility.DisplayDialog("Prepare animations", $"{n} file(s) prepared.", "OK");
        }

        public static bool PrepareAnimation(string path)
        {
            if (!(AssetImporter.GetAtPath(path) is ModelImporter mi)) return false;
            mi.animationType = ModelImporterAnimationType.Human;
            mi.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            mi.importAnimation = true;
            mi.SaveAndReimport();
            var takes = mi.defaultClipAnimations;
            if (takes == null || takes.Length == 0) return false;
            string file = System.IO.Path.GetFileNameWithoutExtension(path);
            string lower = file.ToLowerInvariant();
            bool loop = new[] { "idle", "aim", "run", "walk", "forward", "foward", "backward", "left", "right", "strafe" }.Any(lower.Contains)
                        && !new[] { "hit", "death", "die", "block", "dodg", "reload", "fire", "firing" }.Any(lower.Contains);
            for (int i = 0; i < takes.Length; i++)
            {
                var c = takes[i];
                c.name = takes.Length == 1 ? file : file + "_" + i;
                c.loopTime = loop;
                c.lockRootRotation = true; c.keepOriginalOrientation = true;
                c.lockRootHeightY = true; c.keepOriginalPositionY = true;
                c.lockRootPositionXZ = true; c.keepOriginalPositionXZ = true;
            }
            mi.clipAnimations = takes;
            mi.SaveAndReimport();
            return true;
        }
    }
}
