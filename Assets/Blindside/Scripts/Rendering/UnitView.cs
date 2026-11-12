// Visual counterpart of makeFigure()/syncUnits() in blindside_source/03_renderer_threejs.js — the
// only thing that knows what a unit looks like. Gameplay code reaches it solely through
// Blindside.Combat.IUnitView (SetPosition/SetFacing/SetSelected/PlayHit/PlayDeath, plus the
// spawn-time SetIdentity and the per-frame SetHealth), never through the prefab's children.
//
// Unit_Simple.prefab layout (every reference below is a serialized field, so swapping the art only
// means editing the prefab — or replacing this component with another IUnitView):
//   Unit_Simple        (this component; moved by SetPosition)
//   ├ Model            art only — the capsule/band/head today; swap for a 3D model or sprite rig.
//   │                  Yawed by SetFacing when modelFollowsFacing (turn off for billboards).
//   ├ Facing           yaw pivot that always follows SetFacing
//   │  └ FacingIndicator
//   ├ Ring             team-colored foot ring
//   ├ SelectionRing    shown by SetSelected
//   └ Anchors          Label (name tag, source's proj(u.rx,1.2,u.ry)) · HealthBar · Head · Torso
//
// Per-frame pose smoothing matches `m.dispA+=angNorm(u.facing-m.dispA)*.3` and the ally/enemy/host
// ring color comes from makeFigure's `tc`.
using System.Collections;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using UnityEngine.Serialization;
using Blindside.Combat;
using Blindside.Data;

namespace Blindside.Rendering
{
    [DisallowMultipleComponent]
    public class UnitView : MonoBehaviour, IUnitView
    {
        [Header("Art (swap this subtree for another model/sprite)")]
        [FormerlySerializedAs("rotatingPart")]
        [SerializeField] Transform modelRoot;          // source: `rot` group
        [SerializeField] bool modelFollowsFacing = true;
        [FormerlySerializedAs("bandRenderers")]
        [SerializeField] Renderer[] teamTintRenderers; // tinted with the team color (source: `band` cylinder)
        [Tooltip("Unit_Simple's primitives carry a stray Built-in-RP material; replace every MeshRenderer's " +
                 "material with a fresh URP one at spawn. Turn off for art that ships its own URP materials.")]
        [SerializeField] bool replaceMaterials = true;

        [Header("Model + animation (optional — empty keeps the capsule)")]
        [Tooltip("A character model (prefab or FBX). It replaces the capsule art under the model root, scaled to the profile's " +
                 "modelHeight (1 m without a profile). An operator's own model (OperatorData.modelPrefab) wins over this one.")]
        [SerializeField] GameObject modelPrefab;
        [Tooltip("Clips for idle / aim / walk / fire / reload / hit / death / abilities. Needs a rigged model (Humanoid or Generic).")]
        [SerializeField] UnitAnimationProfile animationProfile;

        [Header("Overlays")]
        [SerializeField] Renderer ringRenderer;        // team-color foot ring
        [SerializeField] Renderer selectionRing;       // shown only while this unit is selected
        [SerializeField] Transform facingPivot;        // yaws with SetFacing regardless of the art
        [SerializeField] Renderer facingIndicator;     // small nose marking the look direction

        [Header("Anchors")]
        [SerializeField] Transform labelAnchor;        // overhead name tag / badges
        [SerializeField] Transform healthBarAnchor;
        [SerializeField] Transform headAnchor;         // headshot hit point
        [SerializeField] Transform torsoAnchor;        // body hit point / tracer target
        [SerializeField] Transform muzzleAnchor;       // gun muzzle; yaws with the facing pivot

        // Team colors match TitleSelectScreen's SelBlue/SelRed exactly, so the battlefield and the
        // faction-select screen agree. No exact hex was given for the hostage; this orange reads
        // clearly against both team colors and the floor.
        static readonly Color AllyColor = new Color32(0x1f, 0x96, 0xe8, 0xff);
        static readonly Color EnemyColor = new Color32(0xe5, 0x36, 0x4d, 0xff);
        static readonly Color HostColor = new Color32(0xff, 0x8c, 0x1a, 0xff);
        [SerializeField] Color selectedColor = new Color32(0xff, 0xd2, 0x1f, 0xff);

        // Health bar: ported from .ul-hp in blindside_source/00_markup_and_styles.html —
        // `width:44px;height:5px;background:rgba(0,0,0,.75)` and `.ul-hp i{transition:width .4s}`.
        // Its host color (#ffab3b) is the CSS's own `.ul.host .ul-hp i` value, kept distinct from
        // HostColor above (chosen without a source spec) so this bar matches the CSS exactly.
        const float HpBarWidth = 0.44f, HpBarDepth = 0.05f, HpTickSpacing = 0.10f;
        const float HpLerpSeconds = 0.4f;
        const float DeathFadeSeconds = 0.3f;
        static readonly Color HpHostColor = new Color32(0xff, 0xab, 0x3b, 0xff);
        Renderer hpFillRenderer;
        Transform hpFill;
        float hpDisplayed = 1f, hpFrom = 1f, hpTo = 1f, hpT = 1f;

        float dispFacing, targetFacing;
        bool hasFacing;
        bool dying;
        bool isAlly;

        public Vector3 HeadPoint => headAnchor.position;
        public Vector3 TorsoPoint => torsoAnchor.position;
        public Vector3 LabelPoint => labelAnchor.position;
        public Vector3 MuzzlePoint => muzzleAnchor.position;

        void Awake()
        {
            if (modelRoot == null) modelRoot = transform;
            if (facingPivot == null) facingPivot = modelRoot;
            // Fallback heights = makeFigure's head (y .8), syncLabels' proj height (1.2), the tracer/boom
            // height (.5) and the bar's previous fixed height (1.15) — only used if the prefab lacks them.
            if (labelAnchor == null) labelAnchor = MakeAnchor("Label", 1.2f);
            if (healthBarAnchor == null) healthBarAnchor = MakeAnchor("HealthBar", 1.15f);
            if (headAnchor == null) headAnchor = MakeAnchor("Head", 0.8f);
            if (torsoAnchor == null) torsoAnchor = MakeAnchor("Torso", 0.5f);
            if (muzzleAnchor == null)
            {
                // CombatFx.Tracer's muzzle: .55 out along the facing, .55 up — parented to the facing pivot
                // so it turns with the unit.
                muzzleAnchor = new GameObject("Muzzle").transform;
                muzzleAnchor.SetParent(facingPivot, false);
                muzzleAnchor.localPosition = new Vector3(0f, 0.55f, 0.55f);
            }

            // Unit_Simple.prefab's renderers all point at a stray Built-in-RP default material (guid
            // 31321ba1..., not backed by any .mat asset in the project) — under this project's URP
            // pipeline that shader fails to compile, so every instance rendered flat pink. Giving every
            // renderer its own fresh, transparent-capable URP material fixes that (and lets death fade
            // the alpha) without touching the prefab.
            if (replaceMaterials)
                foreach (var r in GetComponentsInChildren<MeshRenderer>(true))
                    r.material = MakeMaterial();

            if (facingIndicator == null) facingIndicator = BuildFacingIndicator();
            BuildHealthBar();
            SetupSelectionRing();
            CollectFigureRenderers();
            if (modelPrefab != null) ApplyModel(modelPrefab, animationProfile);
        }

        // ================================================================
        //  Model + animation
        // ================================================================
        GameObject modelInstance;
        UnitAnimator unitAnimator;
        readonly System.Collections.Generic.List<Renderer> placeholderArt = new System.Collections.Generic.List<Renderer>();

        public Transform ModelRoot => modelRoot;
        public UnitAnimator UnitAnimator => unitAnimator;
        public bool HasModel => modelInstance != null;

        /// <summary>
        /// Puts `prefab` in place of the capsule art (null: the prefab's own modelPrefab, or nothing) and plays `profile` on it
        /// (null: the prefab's own profile). The model is scaled to the profile's modelHeight; the head / torso / muzzle hit
        /// points follow its bones (Humanoid, or the profile's muzzle name), or its bounds for a model without a rig.
        /// </summary>
        public void ApplyModel(GameObject prefab, UnitAnimationProfile profile)
        {
            if (prefab == null) prefab = modelPrefab;
            if (profile == null) profile = animationProfile;
            if (prefab == null) return;
            if (modelInstance != null) Destroy(modelInstance);

            // hide the capsule art (the team ring, selection ring and HP bar stay)
            placeholderArt.Clear();
            foreach (var r in modelRoot.GetComponentsInChildren<Renderer>(true))
            {
                if (r == ringRenderer || r == selectionRing || (healthBarAnchor != null && r.transform.IsChildOf(healthBarAnchor))) continue;
                r.enabled = false; placeholderArt.Add(r);
            }
            if (facingIndicator) facingIndicator.enabled = false;

            modelInstance = Instantiate(prefab, modelRoot, false);
            modelInstance.name = "Model_" + prefab.name;
            var mt = modelInstance.transform;
            mt.localPosition = Vector3.zero; mt.localRotation = Quaternion.identity;
            foreach (var c in modelInstance.GetComponentsInChildren<Collider>(true)) Destroy(c);

            // scale to the wanted height, feet on the ground
            float wantH = profile != null ? profile.modelHeight : 1f;
            var b = ModelBounds();
            if (b.size.y > 1e-3f) mt.localScale *= wantH / b.size.y;
            b = ModelBounds();
            mt.position += Vector3.up * (transform.position.y - b.min.y);
            b = ModelBounds();
            float h = b.max.y - transform.position.y;
            modelHeight = h;

            // hit points / overhead anchors
            var animator = modelInstance.GetComponentInChildren<Animator>();
            bool human = animator != null && animator.avatar != null && animator.avatar.isHuman;
            Transform Bone(HumanBodyBones hb) => human ? animator.GetBoneTransform(hb) : null;
            headAnchor = Bone(HumanBodyBones.Head) ?? Pin("Head", h * .9f);
            torsoAnchor = Bone(HumanBodyBones.Chest) ?? Bone(HumanBodyBones.Spine) ?? Pin("Torso", h * .55f);
            labelAnchor.position = new Vector3(labelAnchor.position.x, transform.position.y + h + .2f, labelAnchor.position.z);
            if (healthBarAnchor != null) healthBarAnchor.position = new Vector3(healthBarAnchor.position.x, transform.position.y + h + .15f, healthBarAnchor.position.z);
            var muzzle = profile != null ? UnitAnimator.FindDeep(mt, profile.muzzleTransform) : null;
            if (muzzle == null) muzzle = Bone(HumanBodyBones.RightHand);
            if (muzzle != null) muzzleAnchor = muzzle;

            // how this rig holds a rifle (read from the aim pose, before the animator takes the model over)
            CalibrateGrip(animator, human, profile);

            // animation (needs a rig: an Animator is added for a Generic model that came without one)
            if (unitAnimator != null) { Destroy(unitAnimator); unitAnimator = null; }
            if (profile != null && profile.idle != null)
            {
                if (animator == null) animator = modelInstance.AddComponent<Animator>();
                unitAnimator = gameObject.AddComponent<UnitAnimator>();
                unitAnimator.Init(animator, profile, modelRoot);
                unitAnimator.AimYaw = modelRoot.eulerAngles.y;
                unitAnimator.DeathFinished = OnDeathClipFinished;
            }
            allRenderers = null;
            CollectFigureRenderers();
            if (heldWeapon != null) SetWeapon(heldWeapon);   // a new model: the gun moves to its hand
        }

        // ================================================================
        //  The gun in the right hand
        // ================================================================
        // From the aim pose, in the right hand's own space: the barrel direction (the figure's forward), up, and the palm;
        // the left hand's rotation around the barrel and its palm (GunRig puts it on the foregrip). A rig without a
        // humanoid aim pose falls back to a fixed spot in front of the chest.
        bool gripCalibrated, gripHasLeft, gripHandAxes;
        Vector3 leftFingerLocal, leftThumbLocal;
        HandRest gripHands;
        Transform gripHand;
        Animator gripAnimator;
        Vector3 gripBarrel, gripUp, gripPalm, gripLeftPalm;
        Quaternion gripLeftRot = Quaternion.identity;
        UnitAnimationProfile gripProfile;
        GameObject gunInstance;
        WeaponData heldWeapon;

        /// <summary>How the held gun is gripped and aimed (GunRig), for the battle stage's copy of this figure.</summary>
        public GunRig.Setup GunSetup { get; private set; }
        /// <summary>The held gun's model (its own space: the WeaponData placements), null with none.</summary>
        public GameObject HeldGun => gunInstance;
        /// <summary>Name of the held gun's object under the right hand; null with none or without a rig.</summary>
        public string GunObjectName { get; private set; }

        void CalibrateGrip(Animator animator, bool human, UnitAnimationProfile profile)
        {
            gripCalibrated = false; gripHasLeft = false; gripHand = null; gripAnimator = null; gripProfile = profile;
            if (!human || animator == null) return;
            var rh = animator.GetBoneTransform(HumanBodyBones.RightHand);
            var lh = animator.GetBoneTransform(HumanBodyBones.LeftHand);
            if (rh == null || lh == null) return;
            gripAnimator = animator;
            // the left hand's own axes from the rest pose (the model is not posed yet): toward the middle finger and the thumb
            var lMid = animator.GetBoneTransform(HumanBodyBones.LeftMiddleProximal);
            var lThumb = animator.GetBoneTransform(HumanBodyBones.LeftThumbProximal);
            gripHandAxes = lMid != null && lThumb != null;
            gripHands = HandRest.Read(animator);
            if (gripHandAxes)
            {
                leftFingerLocal = lh.InverseTransformDirection(lMid.position - lh.position).normalized;
                leftThumbLocal = lh.InverseTransformDirection(lThumb.position - lh.position).normalized;
            }
            if (profile != null && profile.gripCalibrated)
            {
                // baked in the editor (a freshly spawned rig can't be posed until its first frame)
                gripHand = rh;
                gripBarrel = profile.gripBarrel; gripUp = profile.gripUp; gripPalm = profile.gripPalm;
                gripLeftRot = profile.gripLeftRot; gripLeftPalm = profile.gripLeftPalm;
                gripHasLeft = profile.gripLeftPalm != Vector3.zero;
                gripCalibrated = true;
                return;
            }
            var pose = profile != null ? (profile.aim != null ? profile.aim : profile.idle) : null;
            if (pose != null)
            {
                var g = PlayableGraph.Create("GripCalibration");
                var output = AnimationPlayableOutput.Create(g, "Grip", animator);
                var clip = AnimationClipPlayable.Create(g, pose);
                clip.SetTime(pose.length * .5f);
                output.SetSourcePlayable(clip);
                g.Evaluate();
                g.Destroy();
            }
            var mid = animator.GetBoneTransform(HumanBodyBones.RightMiddleProximal);
            Vector3 palm = mid != null ? Vector3.Lerp(rh.position, mid.position, .55f) : rh.position;
            gripHand = rh;
            var fwd = modelInstance.transform.forward;
            gripBarrel = rh.InverseTransformDirection(fwd);
            gripUp = rh.InverseTransformDirection(Vector3.up);
            gripPalm = rh.InverseTransformPoint(palm);
            gripCalibrated = true;
        }

        // The right hand on the pistol grip: the grip's rake back from vertical (degrees), how far the knuckles turn round
        // its front (0 straight along the right side), and the grip's distance in front of the palm's bone line (metres, 1.7 m figure).
        public static float GripRake = 16f, GripWrap = .35f, GripPalmOffset = .028f;

        // A 1.7 m person's arm (shoulder to wrist) is about .56 m; WeaponData.modelLength is for that.
        const float ReferenceArm = .56f;

        float GunSizeScale()
        {
            if (gripAnimator != null)
            {
                var u = gripAnimator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
                var l = gripAnimator.GetBoneTransform(HumanBodyBones.LeftLowerArm);
                var h = gripAnimator.GetBoneTransform(HumanBodyBones.LeftHand);
                if (u != null && l != null && h != null)
                    return (Vector3.Distance(u.position, l.position) + Vector3.Distance(l.position, h.position)) / ReferenceArm;
            }
            return modelHeight / 1.7f;
        }

        /// <summary>Puts `w`'s model in the right hand (scaled to w.modelLength for a 1.7 m figure), its muzzle becoming the
        /// tracer origin. Null / no model: the hand stays empty.</summary>
        public void SetWeapon(WeaponData w)
        {
            if (gunInstance != null) Destroy(gunInstance);
            gunInstance = null; heldWeapon = w; GunObjectName = null;
            if (unitAnimator != null) unitAnimator.Gun = null;
            if (w == null || w.model == null || modelInstance == null) return;

            gunInstance = Instantiate(w.model);
            gunInstance.name = "Gun_" + w.key;
            foreach (var c in gunInstance.GetComponentsInChildren<Collider>(true)) Destroy(c);
            // the gun's own geometry: length, grip point, muzzle (its local space, scale 1)
            var mesh = new Bounds(); bool first = true;
            foreach (var mf in gunInstance.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf.sharedMesh == null) continue;
                var b = mf.sharedMesh.bounds;
                var m = gunInstance.transform.worldToLocalMatrix * mf.transform.localToWorldMatrix;
                foreach (var corner in new[] { b.min, b.max, new Vector3(b.min.x, b.max.y, b.min.z), new Vector3(b.max.x, b.min.y, b.max.z) })
                {
                    var p = m.MultiplyPoint3x4(corner);
                    if (first) { mesh = new Bounds(p, Vector3.zero); first = false; } else mesh.Encapsulate(p);
                }
            }
            float sign = w.modelForwardSign >= 0f ? 1f : -1f;
            float rearX = sign > 0f ? mesh.min.x : mesh.max.x;
            var grip = new Vector3(rearX + sign * w.gripFromRear * mesh.size.x, mesh.min.y + w.gripHeight * mesh.size.y, mesh.center.z);
            var gunFwd = new Vector3(sign, 0f, 0f);
            // sized to the arms, not the height: the hands must reach both grips (stylised figures have short arms)
            float worldLength = w.modelLength * GunSizeScale();
            float s = mesh.size.x > 1e-4f ? worldLength / mesh.size.x : 1f;

            var muzzle = new GameObject("Muzzle").transform;
            muzzle.SetParent(gunInstance.transform, false);
            muzzle.localPosition = new Vector3(sign > 0f ? mesh.max.x : mesh.min.x, mesh.min.y + .8f * mesh.size.y, mesh.center.z);

            if (gripCalibrated && gripHand != null)
            {
                var t = gunInstance.transform;
                t.SetParent(gripHand, false);
                float ls = s / Mathf.Max(1e-4f, gripHand.lossyScale.x);
                t.localScale = Vector3.one * ls;
                // where the aim clip's hand carries the gun (the grip at the palm, the barrel straight ahead) …
                var carryRot = Quaternion.LookRotation(gripBarrel, gripUp) * Quaternion.Inverse(Quaternion.LookRotation(gunFwd, Vector3.up));
                var carryPos = gripPalm - carryRot * (grip * ls);
                bool wrap = gripHands != null && gripHands.hasRight;
                if (wrap)
                {
                    // … and how the hand really holds it: the palm against the grip's right side, the knuckles along the
                    // (raked) grip, the fingers wrapping round its front, the thumb over the far side; the grip runs just
                    // in front of the palm, not through the hand's bones. GunRig puts the hand there.
                    float rake = GripRake * Mathf.Deg2Rad;
                    var finger = new Vector3(-GripWrap, -Mathf.Sin(rake), Mathf.Cos(rake)).normalized;
                    var thumb = new Vector3(-.3f, Mathf.Cos(rake), Mathf.Sin(rake)).normalized;
                    var handInBarrel = Quaternion.LookRotation(finger, thumb) * Quaternion.Inverse(Quaternion.LookRotation(gripHands.rightFinger, gripHands.rightThumb));
                    t.localRotation = Quaternion.Inverse(Quaternion.LookRotation(gunFwd, Vector3.up) * handInBarrel);
                    var palm = gripPalm + gripHands.rightPalm * (GripPalmOffset * (modelHeight / 1.7f) / Mathf.Max(1e-4f, gripHand.lossyScale.x));
                    t.localPosition = palm - t.localRotation * (grip * ls);
                }
                else { t.localRotation = carryRot; t.localPosition = carryPos; }

                // the left hand on the foregrip, raised to the eye when aiming (UnitAnimator runs it after the pose)
                var p = gripProfile;
                var setup = new GunRig.Setup
                {
                    foregripLocal = new Vector3(rearX + sign * w.foregripFromRear * mesh.size.x, mesh.min.y + w.foregripHeight * mesh.size.y, mesh.center.z),
                    sightLocal = new Vector3(rearX, mesh.min.y + w.sightHeight * mesh.size.y, mesh.center.z),
                    buttLocal = new Vector3(rearX, mesh.min.y + w.buttHeight * mesh.size.y, mesh.center.z),
                    forwardLocal = gunFwd,
                    leftRotInBarrel = gripLeftRot, leftRoll = w.foregripRoll, leftPalm = gripLeftPalm, hasLeftPose = gripHasLeft,
                    leftFinger = leftFingerLocal, leftThumb = leftThumbLocal, hasHandAxes = gripHandAxes, hands = gripHands,
                    eyeOffset = p != null ? p.adsEyeOffset : new Vector3(.03f, .07f, .09f),
                    eyeDrop = p != null ? p.adsEyeDrop : .035f,
                    shoulderPocket = p != null ? p.adsShoulderPocket : new Vector3(.04f, -.01f, .05f),
                    headPitch = p != null ? p.adsHeadPitch : 12f,
                    headRoll = p != null ? p.adsHeadRoll : 7f,
                    blade = p != null ? p.adsBlade : 30f,
                    lean = p != null ? p.adsLean : 0f,
                    gripLocal = grip,
                    figure = modelHeight / 1.7f,
                    carryRot = carryRot, carryPos = carryPos, hasCarry = wrap,
                    handReach = w.handgun ? Mathf.Max(.2f, w.handgunReach) : 0f,
                };
                GunSetup = setup;
                GunObjectName = gunInstance.name;
                if (unitAnimator != null) unitAnimator.Gun = GunRig.Bind(gripAnimator, t, setup);
            }
            else
            {
                // no rig to hold it: level in front of the chest, along the figure's facing
                var t = gunInstance.transform;
                t.SetParent(modelInstance.transform, false);
                float ls = s / Mathf.Max(1e-4f, modelInstance.transform.lossyScale.x);
                t.localScale = Vector3.one * ls;
                t.localRotation = Quaternion.LookRotation(Vector3.forward, Vector3.up) * Quaternion.Inverse(Quaternion.LookRotation(gunFwd, Vector3.up));
                var chest = modelInstance.transform.InverseTransformPoint(transform.position + Vector3.up * (modelHeight * .58f)
                    + modelRoot.forward * (modelHeight * .12f) + modelRoot.right * (modelHeight * .1f));
                t.localPosition = chest - t.localRotation * (grip * ls);
            }
            muzzleAnchor = muzzle;
            allRenderers = null;
            CollectFigureRenderers();
        }

        Bounds ModelBounds()
        {
            var rs = modelInstance.GetComponentsInChildren<Renderer>(true);
            if (rs.Length == 0) return new Bounds(modelInstance.transform.position, Vector3.zero);
            var b = rs[0].bounds;
            for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
            return b;
        }

        // An anchor fixed at a height on the model (a model without bones).
        Transform Pin(string name, float height)
        {
            var t = new GameObject(name + "Point").transform;
            t.SetParent(modelInstance.transform, false);
            t.position = transform.position + Vector3.up * height;
            return t;
        }

        void OnDeathClipFinished()
        {
            if (unitAnimator != null && unitAnimator.Profile != null && unitAnimator.Profile.keepBodyAfterDeath) return;
            if (isActiveAndEnabled) StartCoroutine(FadeOutAndDeactivate());
        }

        /// <summary>A round fired (the fire clip, and the aim pose held for a while).</summary>
        public void PlayFire() { if (unitAnimator != null) unitAnimator.PlayFire(); }
        /// <summary>The volley is over (the reload clip, when the profile wants one).</summary>
        public void PlayReload() { if (unitAnimator != null && unitAnimator.Profile.reloadAfterVolley) unitAnimator.PlayReload(); }
        float modelHeight = 1f;

        /// <summary>The shield stopped a round (block). Misses play nothing: the round just flies wide.</summary>
        public void PlayDodge(bool blocked) { if (unitAnimator != null && !dying) unitAnimator.PlayDodge(blocked); }
        /// <summary>The unit uses its ability (`key` = OperatorData.abilityKey).</summary>
        public void PlayAbility(string key) { if (unitAnimator != null) unitAnimator.PlayAbility(key); }
        /// <summary>In combat (seen / shooting / shot at): the upper body stays in the aim pose.</summary>
        public void SetCombatReady(bool on) { if (unitAnimator != null) unitAnimator.CombatReady = on; }

        Transform MakeAnchor(string name, float height)
        {
            var t = new GameObject(name).transform;
            t.SetParent(transform, false);
            t.localPosition = new Vector3(0f, height, 0f);
            return t;
        }

        void Update()
        {
            if (selected && selectionRing)
            {
                // Port of syncUnits(): `const s=1+.07*Math.sin(t/160); m.sel.scale.set(s,s,s)` (t in ms).
                float s = 1f + 0.07f * Mathf.Sin(Time.time * 1000f / 160f);
                selectionRing.transform.localScale = selectionRingBaseScale * s;
            }

            if (hasFacing && !dying)
            {
                // `m.dispA+=angNorm(u.facing-m.dispA)*.3; m.rot.rotation.y=-m.dispA` — once per frame.
                dispFacing += AngNorm(targetFacing - dispFacing) * 0.3f;
                ApplyFacing();
            }

            if (hpT >= 1f) return;
            hpT = Mathf.Min(1f, hpT + Time.deltaTime / HpLerpSeconds);
            hpDisplayed = Mathf.Lerp(hpFrom, hpTo, hpT);
            ApplyHpFill(hpDisplayed);
        }

        void ApplyFacing()
        {
            var rot = Quaternion.LookRotation(GridSpace.FacingToWorldDirection(dispFacing), Vector3.up);
            if (unitAnimator != null && unitAnimator.enabled)
            {
                // the animator turns the model (legs toward the move, upper body toward this aim)
                unitAnimator.AimYaw = rot.eulerAngles.y;
                if (facingPivot != modelRoot) facingPivot.rotation = rot;
                return;
            }
            // syncUnits: `if(u.rz>0) m.rot.rotation.z=Math.sin(t/180)*.06 else 0` — a slight side-to-side sway on the rope.
            if (heightOffset > 0f) rot *= Quaternion.Euler(0f, 0f, Mathf.Sin(Time.time * 1000f / 180f) * 0.06f * Mathf.Rad2Deg);
            if (modelFollowsFacing) modelRoot.rotation = rot;
            if (facingPivot != modelRoot) facingPivot.rotation = rot;
        }

        // ================================================================
        //  IUnitView
        // ================================================================

        /// <summary>Port of the ally/enemy/host branch of `tc` in makeFigure(team,opColor,isHost).</summary>
        public void SetIdentity(bool isAlly, bool isHost)
        {
            this.isAlly = isAlly && !isHost;
            var tc = isHost ? HostColor : isAlly ? AllyColor : EnemyColor;
            if (ringRenderer) SetColor(ringRenderer, tc);
            if (teamTintRenderers != null)
                foreach (var r in teamTintRenderers)
                    if (r) SetColor(r, tc);
            if (facingIndicator) SetColor(facingIndicator, Color.white);
            if (hpFillRenderer) SetColor(hpFillRenderer, isHost ? HpHostColor : isAlly ? AllyColor : EnemyColor);
        }

        /// <summary>`m.g.position.set(u.rx,0,u.ry)` — rigid, no smoothing (same as the source).</summary>
        public void SetPosition(float gridX, float gridY)
        {
            if (dying) return;
            transform.position = GridSpace.GridToWorld(gridX, gridY, heightOffset);
        }

        /// <summary>`u.rz`: lifts the whole figure (rope descent). The sway itself runs in Update.</summary>
        public void SetHeight(float height)
        {
            if (Mathf.Approximately(height, heightOffset)) return;
            heightOffset = height;
            if (dying) return;
            var p = transform.position; p.y = height; transform.position = p;
            if (hasFacing) ApplyFacing();   // drops the roll when the figure lands
        }

        float heightOffset;

        /// <summary>Retargets the smoothed facing; the first call snaps (makeFigure: `m.dispA=u.facing`).</summary>
        public void SetFacing(float facingRadians)
        {
            targetFacing = facingRadians;
            if (!hasFacing)
            {
                hasFacing = true;
                dispFacing = facingRadians;
                ApplyFacing();
            }
        }

        /// <summary>Retargets the fill's width toward current/max, animating over HpLerpSeconds from
        /// wherever it's currently displayed (port of the CSS's `transition:width .4s`).</summary>
        public void SetHealth(float current, float max)
        {
            float frac = max > 0f ? Mathf.Clamp01(current / max) : 0f;
            if (Mathf.Approximately(frac, hpTo)) return;
            hpFrom = hpDisplayed;
            hpTo = frac;
            hpT = 0f;
        }

        /// <summary>Port of `m.sel.visible=G.ui.unit===u` — the yellow ring under the selected unit.</summary>
        public void SetSelected(bool on)
        {
            if (unitAnimator != null) unitAnimator.Selected = on;
            if (selected == on) return;
            selected = on;
            if (!selectionRing) return;
            selectionRing.enabled = on;
            if (!on) selectionRing.transform.localScale = selectionRingBaseScale;
        }

        /// <summary>
        /// Headshot: the world-space half of headshotFx(a,t,dmg) — `boom(t.x,t.y,0xff2a44,18);
        /// fxRing(t.x,t.y,0xffffff,420,1.1); fxRing(t.x,t.y,mine?0xffc81e:0xff2a44,650,1.8)`, where
        /// `mine` (the shooter is the player's side) is "this unit is not the player's". The source
        /// has no world fx for a body hit (only the damage popup), so that gets a small impact spark
        /// on the torso point.
        /// </summary>
        public void PlayHit(bool headshot)
        {
            if (dying) return;
            if (unitAnimator != null) unitAnimator.PlayHit();
            if (!headshot)
            {
                CombatFx.Spark(TorsoPoint, new Color32(0xff, 0xd2, 0x7a, 0xff));
                return;
            }
            var at = HeadPoint;
            CombatFx.Spark(at, new Color32(0xff, 0x2a, 0x44, 0xff), 0.22f, 0.15f);
            CombatFx.Boom(at, new Color32(0xff, 0x2a, 0x44, 0xff), 18);
            CombatFx.Ring(at, Color.white, 0.42f, 1.1f);
            CombatFx.Ring(at, isAlly ? new Color32(0xff, 0x2a, 0x44, 0xff) : new Color32(0xff, 0xc8, 0x1e, 0xff), 0.65f, 1.8f);
        }

        /// <summary>
        /// Port of syncUnits' `for(const mt of m.mats) mt.opacity=op`: m.mats are the figure's own
        /// materials (body/band/head/visor/gun/nose/foot ring) — never the selection ring, and the HP
        /// bar is a DOM label in the source, so it stays opaque too.
        /// </summary>
        public void SetOpacity(float alpha)
        {
            if (dying || Mathf.Approximately(alpha, opacity)) return;
            opacity = alpha;
            for (int i = 0; i < figureRenderers.Length; i++)
                if (figureRenderers[i]) SetAlpha(figureRenderers[i], figureBaseAlpha[i] * alpha);
        }

        /// <summary>Port of syncUnits' `m.g.visible=show`. forceRenderingOff leaves each renderer's own
        /// enabled state (selection ring, pulse glow, …) untouched, so showing again restores it as it was.</summary>
        public void SetVisible(bool on)
        {
            if (hidden == !on) return;
            hidden = !on;
            if (allRenderers == null) allRenderers = GetComponentsInChildren<Renderer>(true);
            foreach (var r in allRenderers) if (r) r.forceRenderingOff = hidden;
        }

        bool hidden;
        /// <summary>Switched off by SetVisible (its renderers don't draw).</summary>
        public bool Hidden => hidden;
        Renderer[] allRenderers;

        /// <summary>Shows/hides the HP bar only; independent of SetVisible (which hides the whole figure).</summary>
        public void SetOverheadVisible(bool on)
        {
            if (hpBarRoot != null && hpBarRoot.activeSelf != on) hpBarRoot.SetActive(on);
        }

        GameObject hpBarRoot;

        float opacity = 1f;
        Renderer[] figureRenderers = new Renderer[0];
        float[] figureBaseAlpha = new float[0];

        void CollectFigureRenderers()
        {
            var hpRoot = healthBarAnchor;
            var list = new System.Collections.Generic.List<Renderer>();
            foreach (var r in GetComponentsInChildren<Renderer>(true))
            {
                if (r == selectionRing) continue;
                if (hpRoot != null && r.transform.IsChildOf(hpRoot)) continue;
                list.Add(r);
            }
            figureRenderers = list.ToArray();
            figureBaseAlpha = new float[figureRenderers.Length];
            for (int i = 0; i < figureRenderers.Length; i++)
            {
                var m = figureRenderers[i].material;
                figureBaseAlpha[i] = m.HasProperty("_BaseColor") ? m.GetColor("_BaseColor").a : m.HasProperty("_Color") ? m.GetColor("_Color").a : 1f;
            }
        }

        /// <summary>The source just hides a dead figure (`m.g.visible=u.alive`); here it fades out over
        /// DeathFadeSeconds first. Later calls while the fade runs (or after) are no-ops.</summary>
        public void PlayDeath()
        {
            if (dying || !gameObject.activeSelf) return;
            dying = true;
            SetSelected(false);
            // with a death clip the figure falls first (OnDeathClipFinished fades it, or keeps the body)
            if (unitAnimator != null && unitAnimator.PlayDeath()) return;
            StartCoroutine(FadeOutAndDeactivate());
        }

        /// <summary>Already down: the body lies as at the end of its fall (the battle stage showed the fall itself). Without a death clip
        /// it fades like PlayDeath.</summary>
        public void ShowDead()
        {
            if (dying || !gameObject.activeSelf) return;
            dying = true;
            SetSelected(false);
            if (unitAnimator != null && unitAnimator.ShowDead()) return;
            StartCoroutine(FadeOutAndDeactivate());
        }

        // ================================================================
        //  Building blocks
        // ================================================================

        // Source selection ring (makeFigure in 03_renderer_threejs.js):
        // `new THREE.RingGeometry(.52,.6,40)` in 0xffd21f, laid flat at y=.05, hidden by default.
        const float SelRingInner = 0.52f, SelRingOuter = 0.6f;
        const int SelRingSegments = 40;
        bool selected;
        Vector3 selectionRingBaseScale = Vector3.one;

        /// <summary>
        /// Unit_Simple.prefab's SelectionRing is a filled Quad (and Awake just replaced its material
        /// with a plain white one), so it would read as a white square plate. Swap in a real annulus
        /// sized to the source's .52–.6 world radii (divided by the object's own scale so the world
        /// size matches) and tint it selectedColor.
        /// </summary>
        void SetupSelectionRing()
        {
            if (!selectionRing) return;
            selectionRingBaseScale = selectionRing.transform.localScale;
            var mf = selectionRing.GetComponent<MeshFilter>();
            if (mf != null)
            {
                float k = Mathf.Max(0.0001f, selectionRingBaseScale.x);
                mf.sharedMesh = BuildRingMesh(SelRingInner / k, SelRingOuter / k, SelRingSegments);
            }
            SetColor(selectionRing, selectedColor);
            selectionRing.enabled = false;
        }

        /// <summary>Flat annulus in the local XY plane, double-sided (the prefab's +90° X rotation lays
        /// it on the ground, same as the Quad it replaces).</summary>
        static Mesh BuildRingMesh(float inner, float outer, int segments)
        {
            // Separate vertex sets per side so each side keeps its own normal (shared vertices would
            // average the two opposing normals to zero and light as black under URP Lit).
            int n = segments * 2;
            var verts = new Vector3[n * 2];
            var normals = new Vector3[n * 2];
            var tris = new int[segments * 12];
            for (int i = 0; i < segments; i++)
            {
                float a = i * Mathf.PI * 2f / segments;
                var d = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f);
                verts[i * 2] = verts[n + i * 2] = d * inner;
                verts[i * 2 + 1] = verts[n + i * 2 + 1] = d * outer;
                normals[i * 2] = normals[i * 2 + 1] = Vector3.back;          // front faces -Z, like Unity's Quad
                normals[n + i * 2] = normals[n + i * 2 + 1] = Vector3.forward;
            }
            for (int i = 0, t = 0; i < segments; i++)
            {
                int i0 = i * 2, o0 = i0 + 1, i1 = ((i + 1) % segments) * 2, o1 = i1 + 1;
                // front: clockwise when viewed from -Z
                tris[t++] = i0; tris[t++] = i1; tris[t++] = o1;
                tris[t++] = i0; tris[t++] = o1; tris[t++] = o0;
                // back: opposite winding on the second vertex set
                tris[t++] = n + i0; tris[t++] = n + o1; tris[t++] = n + i1;
                tris[t++] = n + i0; tris[t++] = n + o0; tris[t++] = n + o1;
            }
            var mesh = new Mesh { name = "SelectionRing" };
            mesh.vertices = verts;
            mesh.normals = normals;
            mesh.triangles = tris;
            mesh.RecalculateBounds();
            return mesh;
        }

        static Material MakeMaterial()
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit")
                ?? Shader.Find("Universal Render Pipeline/Unlit")
                ?? Shader.Find("Standard");
            var mat = new Material(shader);
            // URP Lit defaults to _Smoothness=0.5, which puts a bright specular highlight on these
            // flat top-down-facing panels (most visible on the thin health-bar fill) and washes the
            // team color out toward white under the scene light. Flat and matte reads correctly from
            // every angle instead.
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0f);
            if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0f);
            // Transparent surface so the death fade's alpha actually shows instead of being ignored
            // by URP Lit's default opaque mode (same recipe CombatFx's fx materials use).
            if (mat.HasProperty("_Surface"))
            {
                mat.SetFloat("_Surface", 1f);
                mat.SetFloat("_Blend", 0f);
                mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                mat.SetFloat("_ZWrite", 0f);
                mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            }
            return mat;
        }

        /// <summary>Fallback for a prefab without an authored FacingIndicator: a small nose on the
        /// facing pivot, so a unit's facing reads at a glance even from directly above.</summary>
        Renderer BuildFacingIndicator()
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Destroy(go.GetComponent<Collider>());
            go.name = "FacingIndicator";
            go.transform.SetParent(facingPivot, false);
            go.transform.localPosition = new Vector3(0f, 0.5f, 0.22f);
            go.transform.localScale = new Vector3(0.07f, 0.07f, 0.22f);
            var r = go.GetComponent<Renderer>();
            r.material = MakeMaterial();
            return r;
        }

        /// <summary>
        /// Background + fill + tick marks, laid flat (an unrotated cube's top/bottom faces already
        /// point along +/-Y) so it reads as a bar from the top-down battlefield camera. Built under the
        /// HealthBar anchor (not the Model/Facing pivots) so it stays level regardless of facing.
        /// </summary>
        void BuildHealthBar()
        {
            var root = new GameObject("HealthBar").transform;
            root.SetParent(healthBarAnchor, false);
            hpBarRoot = root.gameObject;

            var bg = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Destroy(bg.GetComponent<Collider>());
            bg.name = "Background";
            bg.transform.SetParent(root, false);
            bg.transform.localScale = new Vector3(HpBarWidth + 0.02f, 0.02f, HpBarDepth + 0.02f);
            var bgR = bg.GetComponent<Renderer>();
            bgR.material = MakeMaterial();
            SetColor(bgR, new Color(0f, 0f, 0f, 0.75f));

            var fillGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Destroy(fillGo.GetComponent<Collider>());
            fillGo.name = "Fill";
            fillGo.transform.SetParent(root, false);
            fillGo.transform.localPosition = new Vector3(0f, 0.006f, 0f);
            fillGo.transform.localScale = new Vector3(HpBarWidth, 0.02f, HpBarDepth);
            hpFill = fillGo.transform;
            hpFillRenderer = fillGo.GetComponent<Renderer>();
            hpFillRenderer.material = MakeMaterial();

            // Thin tick lines every 10px(=0.1 world units), sitting on top of the fill like the
            // CSS's `.ul-hp::after` repeating-gradient overlay.
            for (float x = -HpBarWidth / 2f + HpTickSpacing; x < HpBarWidth / 2f - 0.01f; x += HpTickSpacing)
            {
                var tick = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Destroy(tick.GetComponent<Collider>());
                tick.name = "Tick";
                tick.transform.SetParent(root, false);
                tick.transform.localPosition = new Vector3(x, 0.012f, 0f);
                tick.transform.localScale = new Vector3(0.006f, 0.02f, HpBarDepth);
                var tickR = tick.GetComponent<Renderer>();
                tickR.material = MakeMaterial();
                SetColor(tickR, new Color(0f, 0f, 0f, 0.6f));
            }
        }

        void ApplyHpFill(float frac)
        {
            if (hpFill == null) return;
            var s = hpFill.localScale; s.x = HpBarWidth * frac; hpFill.localScale = s;
            var p = hpFill.localPosition; p.x = -HpBarWidth * (1f - frac) / 2f; hpFill.localPosition = p;
        }

        IEnumerator FadeOutAndDeactivate()
        {
            var renderers = GetComponentsInChildren<Renderer>(true);
            float t = 0f;
            while (t < DeathFadeSeconds)
            {
                t += Time.deltaTime;
                float a = 1f - Mathf.Clamp01(t / DeathFadeSeconds);
                foreach (var r in renderers) SetAlpha(r, a);
                yield return null;
            }
            gameObject.SetActive(false);
        }

        static float AngNorm(float a)
        {
            while (a > Mathf.PI) a -= 2f * Mathf.PI;
            while (a < -Mathf.PI) a += 2f * Mathf.PI;
            return a;
        }

        static void SetColor(Renderer r, Color c)
        {
            if (r.material.HasProperty("_BaseColor")) r.material.SetColor("_BaseColor", c);
            if (r.material.HasProperty("_Color")) r.material.SetColor("_Color", c);
        }

        static void SetAlpha(Renderer r, float a)
        {
            if (r.material.HasProperty("_BaseColor"))
            {
                var c = r.material.GetColor("_BaseColor"); c.a = a; r.material.SetColor("_BaseColor", c);
            }
            if (r.material.HasProperty("_Color"))
            {
                var c = r.material.GetColor("_Color"); c.a = a; r.material.SetColor("_Color", c);
            }
        }
    }
}
