// The loadout screen's 3D room (HubArmoryScreen): a dark tactical armoury — concrete, ribbed walls, lockers, a gun rack, crates — with the
// selected operator standing on a steel plate in its idle, lit by a hard key from above-front, a cool rim from behind and a dim blue fill,
// seen by a camera of its own into a RenderTexture (the screen shows it full-screen). Built at runtime far below the HQ (y −260, like the
// workbench at −200) so no other camera sees it; its camera renders only while the screen is open.
// The figure is the battle's own (the unit prefab + UnitView.ApplyModel: the operator's model, its profile's idle, the carried gun in hand),
// its rings / bars hidden, turned to the camera. The camera moves between shots (SetShot: eye + look-at, eased, unscaled time) — the
// screen's idle shot, the gun's (Aim: the figure shoulders it, turned to +X; GunShot from its right side; SlotShot close on a part's point
// on the held gun — HubWorkbench.SlotPoints), the gadget's next. Every move eases out (fast, then settling: EaseOut).
// ITimedEffect.Evaluate(t) poses the move.
using UnityEngine;
using Blindside.Data;
using Blindside.Rendering;
using Blindside.UI;

namespace Blindside.Hub
{
    public class HubArmoryStage : MonoBehaviour, ITimedEffect
    {
        public const int TexW = 1920, TexH = 1080;
        public static readonly Vector3 Origin = new Vector3(0f, -260f, 0f);
        // the figure stands right of the middle (the screen's list covers the left)
        public static readonly Vector3 FigurePos = new Vector3(1.45f, 0f, .35f);
        // the idle shot: a little left of and below the figure's eyes, the figure right of the middle
        // (close: the pelvis up to the head; the camera looks left of the figure, so it stands right, ~3/4 across)
        public static readonly Vector3 IdleEye = new Vector3(.95f, 1.35f, -1.55f), IdleLook = new Vector3(1.08f, 1.3f, .35f);
        public static readonly Vector3 EntranceEye = new Vector3(.8f, 1.5f, -2.7f);
        public const float ShotTime = .8f, EntranceTime = 1.1f;

        Camera cam;
        RenderTexture rt;
        UnitView figure;
        string figureKey;
        Vector3 fromEye, fromLook, toEye, toLook, curEye, curLook;
        float clock = 99f, moveTime = ShotTime;

        public RenderTexture Texture => rt;
        public Camera View => cam;
        public UnitView Figure => figure;
        public float Duration => moveTime;

        public static HubArmoryStage Build(Transform parent = null)
        {
            var go = new GameObject("HubArmoryStage");
            if (parent != null) go.transform.SetParent(parent, false);
            go.transform.position = Origin;
            var s = go.AddComponent<HubArmoryStage>();
            s.BuildRoom();
            return s;
        }

        // ---- the room ----------------------------------------------------------------------------------------------------------
        void BuildRoom()
        {
            var concrete = Mat(new Color(.07f, .074f, .08f), .2f);
            var wall = Mat(new Color(.075f, .08f, .088f), .15f);
            var rib = Mat(new Color(.045f, .048f, .054f), .4f);
            var steel = Mat(new Color(.16f, .17f, .185f), .55f);
            var locker = Mat(new Color(.1f, .12f, .11f), .35f);
            var crate = Mat(new Color(.13f, .115f, .085f), .15f);
            var stripe = Mat(new Color(.5f, .4f, .1f), .2f);
            var gunBlack = Mat(new Color(.05f, .055f, .06f), .5f);

            // shell: floor, walls, ceiling (closed: the HQ's sun never gets in)
            Box("DecoFloor", new Vector3(0f, -.05f, 0f), new Vector3(10f, .1f, 10f), concrete);
            Box("DecoBackWall", new Vector3(0f, 2f, 3.3f), new Vector3(10f, 4f, .2f), wall);
            Box("DecoLeftWall", new Vector3(-4.6f, 2f, 0f), new Vector3(.2f, 4f, 10f), wall);
            Box("DecoRightWall", new Vector3(4.6f, 2f, 0f), new Vector3(.2f, 4f, 10f), wall);
            Box("DecoFrontWall", new Vector3(0f, 2f, -6f), new Vector3(10f, 4f, .2f), wall);
            Box("DecoCeiling", new Vector3(0f, 3.75f, 0f), new Vector3(10f, .1f, 12f), rib);
            // ribs on the back wall, a skirting band and a hazard stripe along the floor
            for (int i = 0; i < 9; i++) Box("DecoRib", new Vector3(-4f + i * 1f, 2f, 3.17f), new Vector3(.08f, 4f, .06f), rib);
            Box("DecoSkirting", new Vector3(0f, .18f, 3.15f), new Vector3(10f, .36f, .08f), rib);
            Box("DecoHazard", new Vector3(0f, .005f, 2.6f), new Vector3(9f, .01f, .12f), stripe);
            Box("DecoWallBand", new Vector3(0f, 2.55f, 3.16f), new Vector3(10f, .05f, .05f), steel);

            // the plate the operator stands on
            var plate = Prim(PrimitiveType.Cylinder, "DecoPlate", new Vector3(FigurePos.x, .03f, FigurePos.z), new Vector3(1.6f, .03f, 1.6f), steel);
            plate.receiveShadows = true;
            Prim(PrimitiveType.Cylinder, "DecoPlateEdge", new Vector3(FigurePos.x, .002f, FigurePos.z), new Vector3(1.68f, .002f, 1.68f), Unlit(new Color(.18f, .55f, .95f)));

            // lockers along the back wall, left
            for (int i = 0; i < 5; i++)
            {
                float x = -3.9f + i * .62f;
                Box("DecoLocker", new Vector3(x, .98f, 2.95f), new Vector3(.58f, 1.96f, .55f), locker);
                Box("DecoLockerVent", new Vector3(x, 1.62f, 2.67f), new Vector3(.36f, .14f, .01f), rib);
                Box("DecoLockerHandle", new Vector3(x + .2f, 1.05f, 2.665f), new Vector3(.03f, .18f, .02f), steel);
            }
            // a gun rack on the back wall, right: a board with three rifles hung across it
            Box("DecoRackBoard", new Vector3(2.75f, 1.55f, 3.14f), new Vector3(2.1f, 1.3f, .06f), rib);
            for (int i = 0; i < 3; i++)
            {
                float y = 1.15f + i * .38f;
                Box("DecoRackRifle", new Vector3(2.75f, y, 3.07f), new Vector3(1.4f, .09f, .05f), gunBlack);
                Box("DecoRackStock", new Vector3(2.15f, y - .05f, 3.07f), new Vector3(.3f, .16f, .05f), gunBlack);
                Box("DecoRackPeg", new Vector3(2.4f, y - .07f, 3.08f), new Vector3(.04f, .04f, .08f), steel);
                Box("DecoRackPeg", new Vector3(3.1f, y - .07f, 3.08f), new Vector3(.04f, .04f, .08f), steel);
            }
            // crates and a bench
            Box("DecoCrate", new Vector3(-2.6f, .35f, .9f), new Vector3(.75f, .7f, .75f), crate);
            Box("DecoCrate", new Vector3(-2.55f, .95f, .95f), new Vector3(.55f, .5f, .55f), crate);
            Box("DecoCrate", new Vector3(3.4f, .3f, .4f), new Vector3(.8f, .6f, .6f), crate);
            Box("DecoBench", new Vector3(-.9f, .22f, 2.4f), new Vector3(1.6f, .08f, .45f), steel);
            Box("DecoBenchLeg", new Vector3(-1.55f, .1f, 2.4f), new Vector3(.06f, .2f, .4f), rib);
            Box("DecoBenchLeg", new Vector3(-.25f, .1f, 2.4f), new Vector3(.06f, .2f, .4f), rib);
            // the lamp over the plate (its light is the key below)
            Box("DecoLamp", new Vector3(FigurePos.x, 3.62f, FigurePos.z - .3f), new Vector3(1.4f, .05f, .16f), Unlit(new Color(1f, .97f, .9f)));

            // lights: a hard key from above-front, a cool rim from behind, a dim blue fill, a faint warm glow on the lockers
            var key = AddLight("Key", LightType.Spot, new Vector3(FigurePos.x + .6f, 3.4f, FigurePos.z - 1.8f), new Color(1f, .95f, .88f), 9f, 7f);
            key.spotAngle = 52f; key.innerSpotAngle = 30f; key.shadows = LightShadows.Soft;
            key.transform.LookAt(transform.TransformPoint(FigurePos + Vector3.up * .9f));
            var rim = AddLight("Rim", LightType.Spot, new Vector3(FigurePos.x - .9f, 2.6f, FigurePos.z + 1.7f), new Color(.45f, .7f, 1f), 10f, 6f);
            rim.spotAngle = 60f;
            rim.transform.LookAt(transform.TransformPoint(FigurePos + Vector3.up * 1.2f));
            AddLight("Fill", LightType.Point, new Vector3(-1.4f, 1.4f, -2.2f), new Color(.35f, .5f, .8f), 1.4f, 6f);
            AddLight("Practical", LightType.Point, new Vector3(-2.7f, 2.3f, 2.2f), new Color(1f, .7f, .4f), 1.2f, 3.2f);

            // the camera
            var cgo = new GameObject("ArmoryCamera");
            cgo.transform.SetParent(transform, false);
            cam = cgo.AddComponent<Camera>();
            cam.fieldOfView = 30f; cam.nearClipPlane = .05f; cam.farClipPlane = 20f;
            cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(.02f, .022f, .028f);
            rt = new RenderTexture(TexW, TexH, 24) { name = "ArmoryRT", antiAliasing = 4 };
            cam.targetTexture = rt;
            cam.enabled = false;
            fromEye = toEye = IdleEye; fromLook = toLook = IdleLook;
            Place(IdleEye, IdleLook);
        }

        Material Mat(Color c, float smooth)
        {
            var m = new Material(ToonShader.Lit ?? Shader.Find("Standard"));
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            if (m.HasProperty("_Color")) m.SetColor("_Color", c);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smooth);
            return m;
        }

        static Material Unlit(Color c)
        {
            var m = new Material(Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color"));
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            if (m.HasProperty("_Color")) m.SetColor("_Color", c);
            return m;
        }

        Renderer Box(string name, Vector3 pos, Vector3 size, Material m) => Prim(PrimitiveType.Cube, name, pos, size, m);

        Renderer Prim(PrimitiveType type, string name, Vector3 pos, Vector3 size, Material m)
        {
            var g = GameObject.CreatePrimitive(type);
            g.name = name;
            Kill(g.GetComponent<Collider>());
            g.transform.SetParent(transform, false);
            g.transform.localPosition = pos;
            g.transform.localScale = size;
            var r = g.GetComponent<Renderer>();
            r.sharedMaterial = m;
            return r;
        }

        Light AddLight(string name, LightType type, Vector3 pos, Color c, float intensity, float range)
        {
            var l = new GameObject(name).AddComponent<Light>();
            l.transform.SetParent(transform, false);
            l.transform.localPosition = pos;
            l.type = type; l.color = c; l.intensity = intensity; l.range = range;
            l.shadows = LightShadows.None;
            return l;
        }

        // ---- the figure --------------------------------------------------------------------------------------------------------
        /// <summary>Stands `op` on the plate in its idle with `gun` in hand (the same operator again: only the gun changes).</summary>
        public void ShowOperator(OperatorData op, WeaponData gun)
        {
            if (op == null) { ClearFigure(); return; }
            if (!Application.isPlaying) { ShowStatic(op); return; }
            if (figure == null || figureKey != op.key)
            {
                ClearFigure();
                var prefab = UnitPrefab();
                if (prefab == null) return;
                var go = Instantiate(prefab, transform, false);
                go.name = "ArmoryFigure_" + op.key;
                go.transform.localPosition = FigurePos;
                figure = go.GetComponent<UnitView>();
                if (figure == null) { Kill(go); return; }
                figure.ApplyModel(op.modelPrefab, op.animationProfile);
                figure.SetIdentity(true, false);
                figure.SetOverheadVisible(false);
                figure.SetSelected(false);
                figure.SetCombatReady(false);
                // the battle's markers are not part of the figure here
                foreach (var t in go.GetComponentsInChildren<Transform>(true))
                    if (t.name == "Ring" || t.name == "SelectionRing" || t.name == "FacingIndicator")
                        foreach (var r in t.GetComponents<Renderer>()) r.enabled = false;
                // turned to the camera (a little to its left, so the figure reads in three quarters)
                var d = transform.TransformPoint(IdleEye) - go.transform.position;
                d.y = 0f;
                d = Quaternion.Euler(0f, -14f, 0f) * d;
                idleFacing = Mathf.Atan2(-d.z, d.x);
                figure.SetFacing(aiming ? 0f : idleFacing);
                gadgetPose = false; lookBack = null; lookBackOn = null;
                figure.SetCombatReady(aiming);
                figureKey = op.key;
            }
            held = gun;
            if (figure != null) figure.SetWeapon(gun);
        }

        // ---- the gun (stage 2): the figure aims to the right (+X), the camera at its right side; a slot's shot frames its point -------
        float idleFacing;
        bool aiming;
        WeaponData held;
        GameObject slotGun;
        readonly Vector3[] slotLocal = new Vector3[Attachments.SlotCount];

        public bool Aiming => aiming;

        /// <summary>Raises the gun (the battle's aim: shouldered, the head on the stock) turned to +X, or back to the idle facing the camera.</summary>
        public void Aim(bool on)
        {
            aiming = on;
            if (figure == null) return;
            figure.SetCombatReady(on);
            figure.SetFacing(on ? 0f : idleFacing);
        }

        public void IdleShot(float time) => SetShot(IdleEye, IdleLook, time);

        /// <summary>The whole gun in the aiming figure's hands, from its right side and a little in front.</summary>
        public void GunShot(float time)
        {
            var look = FigurePos + new Vector3(.32f, 1.36f, 0f);
            SetFramed(look + new Vector3(-.5f, .16f, -1.85f), look, time);
        }

        /// <summary>The share of the distance the camera slides left so the subject sits right of the middle (~70% across: the screen's
        /// column and the picker cover the left).</summary>
        public const float FrameRight = .22f;

        void SetFramed(Vector3 eye, Vector3 look, float time, float share = FrameRight)
        {
            var f = look - eye;
            var right = Vector3.Cross(Vector3.up, f).normalized;
            var pan = -right * (f.magnitude * share);
            SetShot(eye + pan, look + pan, time);
        }

        /// <summary>Slot `slot`'s point on the held gun, room space (false: no gun in hand).</summary>
        public bool SlotPoint(int slot, out Vector3 local)
        {
            local = Vector3.zero;
            var g = figure != null ? figure.HeldGun : null;
            if (g == null) return false;
            if (g != slotGun) { HubWorkbench.SlotPoints(g, held, slotLocal); slotGun = g; }
            local = transform.InverseTransformPoint(g.transform.TransformPoint(slotLocal[slot]));
            return true;
        }

        /// <summary>Close on slot `slot` of the held gun: the optic from above, the grip / magazine from below, the muzzle from in front,
        /// the stock from behind (the gun's own pose decides the rest).</summary>
        public void SlotShot(int slot, float time)
        {
            if (!SlotPoint(slot, out var p)) { GunShot(time); return; }
            Vector3 off;
            switch (slot)
            {
                case 0: off = new Vector3(.2f, .06f, -.5f); break;     // マズル
                case 1: off = new Vector3(-.12f, .22f, -.45f); break;  // 光学機器
                case 2: case 3: off = new Vector3(-.04f, -.14f, -.5f); break;   // グリップ · マガジン
                case 5: off = new Vector3(-.22f, .08f, -.45f); break;  // ストック
                default: off = new Vector3(0f, .08f, -.5f); break;     // バレル
            }
            SetShot(p + off, p, time);   // the part's place in the middle of the picture
        }

        // ---- the gadgets (stage 3): the camera low behind the figure at its belt, the figure turned away and looking back down at it ---
        // belt height, behind and to the side, looking up past the hip to the head
        // (at the waist — the belt, not the seat — looking up to the head turned back)
        public static readonly Vector3 GadgetEye = FigurePos + new Vector3(-.55f, .72f, -.95f), GadgetLook = FigurePos + new Vector3(0f, 1.32f, 0f);
        /// <summary>The gadget shot's slide: the figure right of the middle, short of the picker at the right.</summary>
        public const float GadgetFrameRight = .14f;
        /// <summary>How far round the camera sits from where the figure faces (degrees): behind it, over its shoulder.</summary>
        public const float GadgetTurn = 100f;
        HubLookBack lookBack;
        Object lookBackOn;
        bool gadgetPose;

        public bool GadgetPosed => gadgetPose;

        /// <summary>The gadget pose: turned away from the low camera, looking back down at it over its shoulder (HubLookBack) — or the idle again.</summary>
        public void GadgetPose(bool on)
        {
            gadgetPose = on;
            var eye = transform.TransformPoint(GadgetEye);
            var toCam = eye - transform.TransformPoint(FigurePos); toCam.y = 0f;
            var fwd = Quaternion.Euler(0f, GadgetTurn, 0f) * toCam.normalized;   // facing away, the camera off its left shoulder
            if (figure != null)
            {
                figure.SetCombatReady(false);
                figure.SetFacing(on ? Mathf.Atan2(-fwd.z, fwd.x) : idleFacing);
                if (BindLookBack(figure.gameObject, figure.ModelRoot != null ? figure.ModelRoot.GetComponentInChildren<Animator>() : null)) lookBack.Turn(on);
            }
            else if (staticFigure != null)
            {
                // previews: re-sample the idle, turn the root, then pose the look once (no frames run in edit mode)
                var op = BuildAssetRegistry.Operator(figureKey);
                if (op != null && op.animationProfile != null && op.animationProfile.idle != null) op.animationProfile.idle.SampleAnimation(staticFigure, 0f);
                staticFigure.transform.localPosition = FigurePos;
                var d = transform.TransformPoint(IdleEye) - staticFigure.transform.position; d.y = 0f;
                staticFigure.transform.rotation = Quaternion.LookRotation(on ? fwd : Quaternion.Euler(0f, -14f, 0f) * d, Vector3.up);
                if (on && BindLookBack(staticFigure, staticFigure.GetComponentInChildren<Animator>())) lookBack.Apply(1f);
            }
        }

        bool BindLookBack(GameObject host, Animator animator)
        {
            if (animator == null) return false;
            if (lookBack == null || lookBackOn != host)
            {
                lookBack = host.GetComponent<HubLookBack>();
                if (lookBack == null) lookBack = host.AddComponent<HubLookBack>();
                lookBackOn = host;
                lookBack.Bind(animator, cam.transform);
                // turned and settled: the figure is held still there (no idle breathing) until it turns back
                var view = host.GetComponent<UnitView>();
                lookBack.Hold = view != null ? on => { if (view.UnitAnimator != null) view.UnitAnimator.SetFrozen(on); } : (System.Action<bool>)null;
            }
            return true;
        }

        /// <summary>The low shot at the belt, looking up past the hip to the face turned back.</summary>
        /// <summary>How far above the pelvis the shot aims (m): the pelvis sits a little under the picture's middle.</summary>
        public const float GadgetAboveHips = .14f;

        /// <summary>The low shot at the belt, the pelvis a little under the picture's middle height (the operator's own hips bone; else
        /// ~.95 m).</summary>
        public void GadgetShot(float time)
        {
            var look = GadgetLook;
            look.y = FigurePos.y + PelvisHeight() + GadgetAboveHips;
            SetFramed(GadgetEye, look, time, GadgetFrameRight);
        }

        float PelvisHeight()
        {
            Animator a = null;
            if (figure != null && figure.ModelRoot != null) a = figure.ModelRoot.GetComponentInChildren<Animator>();
            else if (staticFigure != null) a = staticFigure.GetComponentInChildren<Animator>();
            var hips = a != null && a.isHuman ? a.GetBoneTransform(HumanBodyBones.Hips) : null;
            return hips != null ? transform.InverseTransformPoint(hips.position).y - FigurePos.y : .95f;
        }

        /// <summary>The camera's ease: fast at first, slowing into its place (exponential ease-out) — a move with a bit of punch.</summary>
        public static float EaseOut(float t) => t >= 1f ? 1f : t <= 0f ? 0f : 1f - Mathf.Pow(2f, -10f * t);

        void ClearFigure()
        {
            if (figure != null) Kill(figure.gameObject);
            if (staticFigure != null) Kill(staticFigure);
            figure = null; staticFigure = null; figureKey = null;
        }

        GameObject staticFigure;

        // Previews (edit mode: no UnitView, no animator running): the model alone, scaled like the battle's, posed at its idle's first frame
        void ShowStatic(OperatorData op)
        {
            if (staticFigure != null && figureKey == op.key) return;
            ClearFigure();
            if (op.modelPrefab == null) return;
            var go = Instantiate(op.modelPrefab, transform, false);
            go.name = "ArmoryFigure_" + op.key;
            foreach (var c in go.GetComponentsInChildren<Collider>(true)) Kill(c);
            var t = go.transform;
            // the idle's first frame first: sampling a humanoid clip also sets the root's position and turn
            var prof = op.animationProfile;
            if (prof != null && prof.idle != null) prof.idle.SampleAnimation(go, 0f);
            t.localPosition = FigurePos;
            var d = transform.TransformPoint(IdleEye) - t.position; d.y = 0f;
            t.rotation = Quaternion.LookRotation(Quaternion.Euler(0f, -14f, 0f) * d, Vector3.up);
            var rs = go.GetComponentsInChildren<Renderer>();
            if (rs.Length > 0)
            {
                var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
                float want = prof != null ? prof.modelHeight : 1.75f;
                if (b.size.y > 1e-3f) t.localScale *= want / b.size.y;
                b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
                t.position += Vector3.up * (transform.position.y - b.min.y);
            }
            staticFigure = go;
            figureKey = op.key;
            gadgetPose = false; lookBack = null; lookBackOn = null;
        }

        static GameObject UnitPrefab()
        {
            var r = BuildAssetRegistry.UnitPrefab;
            if (r != null) return r;
#if UNITY_EDITOR
            return UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Blindside/Prefabs/Units/Unit_Simple.prefab");
#else
            return null;
#endif
        }

        // ---- the camera --------------------------------------------------------------------------------------------------------
        /// <summary>The render the size of the screen (the screen's angled panels are clicked through this camera: its pixels must be the
        /// screen's). Returns true when the texture was remade.</summary>
        public bool FitScreen(int w, int h)
        {
            if (rt != null && rt.width == w && rt.height == h) return false;
            if (w < 16 || h < 16) return false;
            var old = rt;
            rt = new RenderTexture(w, h, 24) { name = "ArmoryRT", antiAliasing = 4 };
            cam.targetTexture = rt;
            if (old != null) { old.Release(); Kill(old); }
            return true;
        }

        /// <summary>The camera draws every frame while true (the screen is open).</summary>
        public void SetLive(bool on) { if (cam != null) cam.enabled = on; }

        /// <summary>Draws one frame now (previews in edit mode).</summary>
        public void RenderNow() { if (cam == null) return; Canvas.ForceUpdateCanvases(); cam.Render(); }   // (the screen's angled panels too)

        /// <summary>Moves the camera to look from `eye` at `look` (room space) over `time` seconds (0: at once).</summary>
        public void SetShot(Vector3 eye, Vector3 look, float time)
        {
            fromEye = curEye; fromLook = curLook;
            toEye = eye; toLook = look;
            moveTime = Mathf.Max(.0001f, time);
            clock = 0f;
            if (time <= 0f || UIMotionSettings.reduceMotion) { clock = 99f; Evaluate(moveTime); }
        }

        /// <summary>The screen opening: the camera dollies in from further back to the idle shot.</summary>
        public void PlayEntrance()
        {
            Place(EntranceEye, IdleLook);
            SetShot(IdleEye, IdleLook, EntranceTime);
        }

        public void Evaluate(float t)
        {
            float k = EaseOut(Mathf.Clamp01(t / moveTime));
            Place(Vector3.LerpUnclamped(fromEye, toEye, k), Vector3.LerpUnclamped(fromLook, toLook, k));
        }

        void Place(Vector3 eye, Vector3 look)
        {
            curEye = eye; curLook = look;
            if (cam == null) return;
            var t = cam.transform;
            t.localPosition = eye;
            t.localRotation = Quaternion.LookRotation(look - eye, Vector3.up);
        }

        void Update()
        {
            if (clock >= moveTime) return;
            clock += Time.unscaledDeltaTime;
            Evaluate(clock);
        }

        static void Kill(Object o) { if (o == null) return; if (Application.isPlaying) Destroy(o); else DestroyImmediate(o); }

        void OnDestroy()
        {
            if (rt != null) { rt.Release(); Kill(rt); }
        }
    }
}
