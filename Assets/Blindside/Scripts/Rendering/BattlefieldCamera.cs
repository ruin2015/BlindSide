// Port of the in-game camera, blindside_source/03_renderer_threejs.js + 05_ui_input_intro.js, read verbatim:
//
//   const TILT=.32;
//   camera=new THREE.PerspectiveCamera(38,VW/VH,.1,300);
//   groundPl=new THREE.Plane(new THREE.Vector3(0,1,0),0);
//   function updateCamera(){ ... const c=G.cam; camera.position.set(c.x,c.h,c.y+c.h*Math.tan(TILT)); camera.lookAt(c.x,0,c.y); }
//   function groundAt(sx,sy){ raycaster.setFromCamera(new THREE.Vector2(sx/VW*2-1,-(sy/VH)*2+1),camera);
//     const p=new THREE.Vector3(); if(!raycaster.ray.intersectPlane(groundPl,p)) return null;
//     return {fx:p.x,fy:p.z,x:Math.round(p.x),y:Math.round(p.z)}; }
//   cv.addEventListener('wheel',e=>{ e.preventDefault(); G.cam.h=clamp(G.cam.h*(e.deltaY>0?1.1:.9),9,42); });
//   loop(): const sp=.012*dt*G.cam.h/20; w/↑: G.cam.y-=sp; s/↓: G.cam.y+=sp; a/←: G.cam.x-=sp; d/→: G.cam.x+=sp;
//
// Three.js puts grid Y straight onto world +Z; this project maps grid (x,y) to world (x, -y) via
// GridSpace, so the "pull back toward the bottom of the screen" offset y+h·tan(TILT) lands at world
// z = -(y + h·tan(TILT)) — the sign flips together with the axis, keeping grid +Y pointing down-screen.
// The drag pan (pointermove) lives in PlayerSelectionController next to the rest of bindInput().
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Unity.Cinemachine;

namespace Blindside.Rendering
{
    /// <summary>groundAt() result: exact ground point (fx,fy) and the rounded grid cell (x,y), in grid coords.</summary>
    public struct GroundHit
    {
        public float fx, fy;
        public int x, y;
        public override string ToString() => $"cell=({x},{y}) f=({fx:0.00},{fy:0.00})";
    }

    /// <summary>A full camera pose: world position, rotation and vertical FOV.</summary>
    public struct CamPose
    {
        public Vector3 position;
        public Quaternion rotation;
        public float fov;
        public CamPose(Vector3 position, Quaternion rotation, float fov) { this.position = position; this.rotation = rotation; this.fov = fov; }
        public static CamPose Lerp(CamPose a, CamPose b, float t) =>
            new CamPose(Vector3.LerpUnclamped(a.position, b.position, t), Quaternion.SlerpUnclamped(a.rotation, b.rotation, t), Mathf.LerpUnclamped(a.fov, b.fov, t));
    }

    // Runs its LateUpdate before everything else so every screen-space overlay projected in LateUpdate
    // (labels, badges, the battle director's popups) sees this frame's pose, not last frame's.
    [DefaultExecutionOrder(-100)]
    [RequireComponent(typeof(Camera))]
    public class BattlefieldCamera : MonoBehaviour
    {
        public const float Fov = 38f, Near = .1f, Far = 300f, Tilt = .32f;
        public const float MinH = 9f, MaxH = 42f, StartH = 27f;

        public static BattlefieldCamera Instance { get; private set; }

        /// <summary>While set, WASD/arrows/wheel are ignored (PlayerSelectionController's drag pan and clicks too).</summary>
        public static bool InputLocked { get; set; }

        /// <summary>The map editor uses S as its "select" tool hotkey, so while set the S key no longer pans down (the ↓ arrow still does).
        /// False in the game.</summary>
        public static bool SKeyReserved { get; set; }

        /// <summary>G.cam: look-at point on the grid plane (x,y) and camera height h.</summary>
        public float x, y, h = StartH;   // x,y: Activate() always sets them (the map's camera start)

        /// <summary>When set, the look-at point is kept inside this rectangle (grid coordinates) — the map editor keeps the camera
        /// over the map. Null in the game; Activate() clears it.</summary>
        public Rect? Limit;

        Camera cam;
        Volume noBlurVolume;

        /// <summary>
        /// Hands the Main Camera over to the game camera, centred on grid (x,y) at the default height.
        /// Disables the CinemachineBrain so nothing else writes the camera transform after the intro.
        /// </summary>
        public static BattlefieldCamera Activate(float x, float y)
        {
            var main = Camera.main;
            if (main == null) return null;
            var brain = main.GetComponent<CinemachineBrain>();
            if (brain != null) brain.enabled = false;
            var bc = main.GetComponent<BattlefieldCamera>();
            if (bc == null) bc = main.gameObject.AddComponent<BattlefieldCamera>();
            bc.enabled = true;
            bc.Limit = null;
            bc.x = x; bc.y = y; bc.h = StartH;
            bc.Apply();
            return bc;
        }

        /// <summary>Gives the Main Camera back to Cinemachine (the title backdrop) and clears the input lock —
        /// the reverse of <see cref="Activate"/>, for "タイトルへ".</summary>
        public static void Deactivate()
        {
            InputLocked = false;
            var main = Camera.main;
            if (main == null) return;
            var bc = main.GetComponent<BattlefieldCamera>();
            if (bc != null) bc.enabled = false;
            var brain = main.GetComponent<CinemachineBrain>();
            if (brain != null) brain.enabled = true;
        }

        void OnEnable()
        {
            Instance = this;
            cam = GetComponent<Camera>();
            cam.orthographic = false;
            cam.fieldOfView = Fov;
            cam.nearClipPlane = Near;
            cam.farClipPlane = Far;
            EnsureNoBlur();
        }

        void OnDisable()
        {
            EndOverride();
            if (Instance == this) Instance = null;
            if (noBlurVolume != null) noBlurVolume.enabled = false;
        }

        void Update()
        {
            if (InputLocked) return;
            var mouse = Mouse.current;
            if (mouse != null)
            {
                // Web deltaY>0 (wheel toward the user) zooms out; Input System scroll.y is the opposite sign.
                float sy = mouse.scroll.ReadValue().y;
                // one notch: ±10% of the height, scaled by the ズーム感度 option (×0.5–2)
                float z = .1f * Blindside.Settings.GameSettings.ZoomSpeed;
                if (sy != 0f) h = Mathf.Clamp(h * (sy < 0f ? 1f + z : 1f - z), MinH, MaxH);
            }

            var kb = Keyboard.current;
            if (kb != null)
            {
                float dt = Mathf.Min(50f, Time.unscaledDeltaTime * 1000f);
                float sp = .012f * dt * h / 20f * Blindside.Settings.GameSettings.CameraPanSpeed;   // × カメラ移動速度
                if (kb.wKey.isPressed || kb.upArrowKey.isPressed) y -= sp;
                if ((kb.sKey.isPressed && !SKeyReserved) || kb.downArrowKey.isPressed) y += sp;
                if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) x -= sp;
                if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) x += sp;
            }
        }

        // panTo(x,y,ms) = tween(G.cam,{x,y},ms): easeIO from wherever the camera is now; a new call
        // replaces the running one (tween() drops any earlier tween on the same keys).
        bool panning; float panFromX, panFromY, panToX, panToY, panT0, panDur;
        static float EaseIO(float p) => p < .5f ? 2f * p * p : 1f - Mathf.Pow(-2f * p + 2f, 2f) / 2f;

        public void PanTo(float tx, float ty, float ms = 450f)
        {
            panFromX = x; panFromY = y; panToX = tx; panToY = ty;
            panT0 = Time.unscaledTime; panDur = ms / 1000f;
            if (panDur <= .001f) { x = tx; y = ty; panning = false; return; }
            panning = true;
        }

        void LateUpdate()
        {
            if (panning)
            {
                float p = Mathf.Min(1f, (Time.unscaledTime - panT0) / panDur), e = EaseIO(p);
                x = panFromX + (panToX - panFromX) * e;
                y = panFromY + (panToY - panFromY) * e;
                if (p >= 1f) panning = false;
            }
            if (Limit.HasValue)
            {
                var r = Limit.Value;
                x = Mathf.Clamp(x, r.xMin, r.xMax);
                y = Mathf.Clamp(y, r.yMin, r.yMax);
            }
            if (overriding) { ApplyOverride(); return; }
            Apply();
            gameShake *= Mathf.Pow(.86f, Time.unscaledDeltaTime * 60f);
        }

        /// <summary>updateCamera(): sit h above the target, pulled h·tan(TILT) toward screen-bottom, looking at it.
        /// G.shake rides along: render3D's `sh=G.shake>.02?(rand-.5)*G.shake:0; G.cam.x+=sh; updateCamera(); G.cam.x-=sh`.</summary>
        public void Apply()
        {
            float sx = x + (gameShake > .02f ? ((float)shakeRng.NextDouble() - .5f) * gameShake : 0f);
            transform.position = GridSpace.GridToWorld(sx, y + h * Mathf.Tan(Tilt), h);
            transform.LookAt(GridSpace.GridToWorld(sx, y), Vector3.up);
        }

        // G.shake on the game camera (e.g. the breaching hammer's `G.shake=.15`), decaying ×.86 per 60fps frame
        // (the main loop's `G.shake*=.86`). Separate from the cinematic override's Kick().
        float gameShake;
        /// <summary>`G.shake=amount` for the game camera.</summary>
        public void Shake(float amount) => gameShake = Blindside.Settings.GameSettings.ScreenShake ? amount : 0f;   // (画面の揺れ off: none)

        /// <summary>The pose Apply() would write for the current x/y/h, with the game FOV.</summary>
        public CamPose GamePose
        {
            get
            {
                var pos = GridSpace.GridToWorld(x, y + h * Mathf.Tan(Tilt), h);
                return new CamPose(pos, Quaternion.LookRotation(GridSpace.GridToWorld(x, y) - pos, Vector3.up), Fov);
            }
        }

        // ================================================================
        //  Cinematic pose override (BattleDirector). The game camera's own state (x, y, h) is never
        //  touched, so releasing ends on exactly Apply()'s pose and the game FOV.
        // ================================================================
        bool overriding, releasing;
        System.Func<CamPose> overrideTarget;
        CamPose blendFrom, shownPose;
        float blendT0, blendDur;
        float shake; Vector3 shakeOffset;
        float punchFactor = 1f, punchT0, punchDur;
        readonly System.Random shakeRng = new System.Random(0x5EED);   // never UnityEngine.Random: combat rolls share that stream

        public bool IsOverridden => overriding;
        /// <summary>The pose shown this frame, without shake/punch (the start point of any new blend).</summary>
        public CamPose ShownPose => overriding ? shownPose : GamePose;
        public float AspectRatio => cam != null ? cam.aspect : 16f / 9f;

        /// <summary>Blend (ease in-out, unscaled time) from the pose on screen to `target`, re-evaluated every
        /// frame so moving targets (orbits) work. 0 seconds cuts.</summary>
        public void SetOverride(System.Func<CamPose> target, float seconds)
        {
            blendFrom = ShownPose;
            overrideTarget = target;
            blendT0 = Time.unscaledTime; blendDur = Mathf.Max(0f, seconds);
            overriding = true; releasing = false;
            ApplyOverride();
        }

        /// <summary>Blend back to the game camera; the override ends (Apply()) when the blend completes.</summary>
        public void ReleaseOverride(float seconds)
        {
            if (!overriding) return;
            SetOverride(() => GamePose, seconds);
            releasing = true;
            if (blendDur <= 0f) EndOverride();
        }

        /// <summary>Finishes the running blend this frame (skip).</summary>
        public void CompleteBlend() { blendDur = 0f; punchDur = 0f; shake = 0f; }

        /// <summary>Drops the override immediately and restores the exact game pose and FOV.</summary>
        public void EndOverride()
        {
            bool was = overriding;
            overriding = false; releasing = false; overrideTarget = null;
            shake = 0f; shakeOffset = Vector3.zero; punchFactor = 1f; punchDur = 0f;
            if (!was || cam == null) return;
            Apply();
            cam.fieldOfView = Fov;
        }

        /// <summary>Camera shake impulse in metres (keeps the larger of the running and new amplitude).</summary>
        public void Kick(float metres) { if (overriding) shake = Mathf.Max(shake, metres); }

        /// <summary>FOV × factor, easing back to 1 over `seconds` (CameraPunch.ZoomPunch's curve).</summary>
        public void ZoomPunch(float factor, float seconds)
        {
            if (!overriding) return;
            punchFactor = factor; punchT0 = Time.unscaledTime; punchDur = seconds;
        }

        void ApplyOverride()
        {
            var target = overrideTarget != null ? overrideTarget() : GamePose;
            float p = blendDur <= 0f ? 1f : Mathf.Clamp01((Time.unscaledTime - blendT0) / blendDur);
            shownPose = CamPose.Lerp(blendFrom, target, EaseIO(p));
            if (releasing && p >= 1f) { EndOverride(); return; }

            // CameraPunch's shake: random offset in the camera plane, decaying ×.86 per 60fps frame.
            shakeOffset = shake > .002f
                ? new Vector3((float)(shakeRng.NextDouble() * 2 - 1) * shake, (float)(shakeRng.NextDouble() * 2 - 1) * shake, 0f)
                : Vector3.zero;
            shake *= Mathf.Pow(.86f, Time.unscaledDeltaTime * 60f);

            float fovMul = 1f;
            if (punchDur > 0f)
            {
                float q = Mathf.Clamp01((Time.unscaledTime - punchT0) / punchDur);
                fovMul = Mathf.Lerp(punchFactor, 1f, q * q * (3f - 2f * q));
                if (q >= 1f) punchDur = 0f;
            }

            transform.SetPositionAndRotation(shownPose.position + shownPose.rotation * shakeOffset, shownPose.rotation);
            cam.fieldOfView = shownPose.fov * fovMul;
        }

        /// <summary>Drag pan: shift the look-at point by a grid-space delta.</summary>
        public void Pan(float dx, float dy) { x += dx; y += dy; }

        /// <summary>
        /// groundAt(sx,sy): screen pixel (Unity screen space, e.g. Mouse.position) -> the point where the
        /// camera ray meets the y=0 ground plane, in grid coords. False when the ray misses the plane.
        /// Every click/drag on the battlefield should go through this.
        /// </summary>
        public static bool GroundAt(Vector2 screenPos, out GroundHit hit)
        {
            hit = default;
            var c = Instance != null ? Instance.cam : Camera.main;
            if (c == null) return false;
            var ray = c.ScreenPointToRay(screenPos);
            if (!new Plane(Vector3.up, Vector3.zero).Raycast(ray, out float t)) return false;
            var p = ray.GetPoint(t);
            float fx = p.x, fy = -p.z; // GridSpace inverse: world (x, -y) -> grid (x, y)
            // JS Math.round = floor(v+.5) (halves round up), not Mathf.Round's banker's rounding.
            hit = new GroundHit { fx = fx, fy = fy, x = Mathf.FloorToInt(fx + .5f), y = Mathf.FloorToInt(fy + .5f) };
            return true;
        }

        // The source renders straight to the canvas with no post chain, so any lens blur left over from
        // the intro must not reach the game view: a top-priority global override pins DoF and motion
        // blur off for as long as this camera is active (Bloom/Vignette/Tonemapping are untouched).
        void EnsureNoBlur()
        {
            if (noBlurVolume == null)
            {
                var go = new GameObject("BattlefieldCamera_NoBlur");
                go.transform.SetParent(transform, false);
                noBlurVolume = go.AddComponent<Volume>();
                noBlurVolume.isGlobal = true;
                noBlurVolume.priority = 1000f;
                var profile = ScriptableObject.CreateInstance<VolumeProfile>();
                profile.name = "BattlefieldCamera_NoBlur";
                var dof = profile.Add<DepthOfField>(true);
                dof.mode.Override(DepthOfFieldMode.Off);
                var mb = profile.Add<MotionBlur>(true);
                mb.intensity.Override(0f);
                noBlurVolume.sharedProfile = profile;
            }
            noBlurVolume.enabled = true;
        }
    }
}
