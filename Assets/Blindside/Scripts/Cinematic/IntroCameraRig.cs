// Port of camAt(p)/heliPath(p), blindside_source/05_ui_input_intro.js:232-244, read verbatim:
//
//   function heliPath(p){ p=clamp(p,0,1);
//     const P=HELI_P||[{x:-2,y:44,z:9},{x:4,y:27,z:7},{x:9.5,y:13,z:7},{x:10.5,y:1.5,z:6}];
//     const s=p*(P.length-1), i=Math.min(P.length-2,Math.floor(s)), q=s-i, a=P[i], b=P[i+1];
//     return {x:lerp(a.x,b.x,q),y:lerp(a.y,b.y,q),z:lerp(a.z,b.z,q),hd:Math.atan2(b.y-a.y,b.x-a.x)};
//   }
//   ...
//   const HX=12.5, HZ=11;
//   const TOTAL=dur(11500), HELI_AT=dur(4200), HELI_DUR=dur(6200);
//   const camAt=p=>{ const e=p*p*(3-2*p), a=1.0+e*.7, R=44-e*6; return {a,px:HX+Math.cos(a)*R,py:20-e*5,pz:HZ+Math.sin(a)*R}; };
//   const ca=camAt(HELI_AT/TOTAL), dx=Math.cos(ca.a), dz=Math.sin(ca.a), side=-Math.sin(ca.a), sidez=Math.cos(ca.a);
//   HELI_P=[{x:HX+dx*54+side*3,y:HZ+dz*54+sidez*3,z:19},{x:HX+dx*30+side*2,y:HZ+dz*30+sidez*2,z:13},
//           {x:HX+dx*6,y:HZ+dz*6,z:8.5},{x:10.5,y:1.5,z:6}];
//
// (2026-11: the orbit above has since been replaced by the shot list further down — a wide shot of the town the
// helicopter flies through, then a low-angle cut on the house for its arrival and the rope; heliPath keeps the
// source's idea of a polyline ending nose-in over the hover point.)
// Replaces the hand-keyed Camera_Motion/Heli_Motion Timeline AnimationTracks (which didn't reproduce
// this math — see the earlier camera-path audit) with the formula itself, evaluated every frame. The
// PlayableDirector is kept only as the 0..1 progress clock (time/duration) and for the Signal-driven
// title/caption/rappel events on the Events track; this script now owns the camera and heli transforms.
//
// camAt's `px`/`pz` are grid-plane X/Y (py is height) and heliPath's {x,y,z} are grid-plane X/Y plus
// height in z (syncHeli() in the source sets `position.set(h.x, h.z+bob, h.y)` — world Y comes from
// heliPath's z, world Z-ish from its y) — every place below that assigns to `.position`/`.rotation`/
// `LookAt` routes through GridSpace.GridToWorld/FacingToWorldDirection to turn those grid-plane values
// into actual world space, same as the rest of the project. The look-at target (lx,ly,lz) and heli tilt
// blend are ported from the same G.cineFn block (lines 246-249) since a position-only port would leave
// the camera facing whatever static rotation the prefab happened to have, and the heli facing forward
// regardless of travel direction — both needed for the shot to read as the same orbit, not just occupy
// the same points.
using UnityEngine;
using UnityEngine.Playables;
using Blindside.Data;
using Blindside.Rendering;

namespace Blindside.Cinematic
{
    public class IntroCameraRig : MonoBehaviour
    {
        public PlayableDirector director;
        public Transform cameraTarget;   // IntroOrbitCam
        public Transform heliTarget;     // HeliRig root
        public HeliRig heliRig;          // for Tilt (HeliRig.cs already expects this driven externally)
        [Tooltip("Show the House_Exterior model beside the map during the intro. Off: only the map is drawn (the helicopter still turns to where the house stands).")]
        public bool showHouseExterior = false;
        [Tooltip("The house the intro shows where the map stands (with its lawn). The map's own pieces are hidden until the intro ends.")]
        public GameObject introHouse;
        [Tooltip("The map's visible pieces (Blindside_MapRoot), hidden while the intro shows the house.")]
        public Transform mapRoot;
        [Tooltip("Other map things hidden with it: the exfil zone markers (built at match start, found by name) and the rappel figure waiting at the origin.")]
        public Transform[] alsoHide;
        Transform exfilZones;
        readonly System.Collections.Generic.List<Renderer> mapPieces = new System.Collections.Generic.List<Renderer>();
        readonly System.Collections.Generic.List<Renderer> hiddenMap = new System.Collections.Generic.List<Renderer>();
        bool introWorld;

        // The house the orbit and the helicopter approach are centred on: MapData.anchors.introHouseCenter (12.5, 11 on the default map).
        static float HX => Blindside.Map.MapLibrary.Current.anchors.introHouseCenter.x;
        static float HZ => Blindside.Map.MapLibrary.Current.anchors.introHouseCenter.y;
        bool introCutDone;
        // ---- the shot list (seconds on the Intro_Timeline clock, 22 s long) ----
        //   0 – HoloStart    the whole town from far off, centred on the target house, drifting in slowly; from HeliStart
        //                    the helicopter comes in from behind the camera, skims past just off its right and flies on
        //                    down toward the house
        //   HoloStart – CutAt lines are drawn over the picture and it wipes into the tactical hologram (IntroHologram /
        //                    IntroHoloScreen): the map's structure builds up, then the camera closes in on the hostage
        //   CutAt – ArriveAt cut: low angle off the house's back corner, looking up as the helicopter comes in over the house
        //                    and stops high over the middle of the roof, still on its approach heading
        //   RopeStart – RopeEnd the rope goes down onto the roof, the operators slide down it; held until the Timeline ends →
        //                    the deployment screen
        // The spoken lines (IntroSubtitle) run over the three parts: Lines.
        // The rescue intro (RescueStyle: MapData.rescueIntro) is the same flight, but its hologram is the operation's briefing on two of
        // the squad taken in the last operation: the camera comes round the whole hologram slowly instead of closing in on one hostage,
        // the two captives' places are driven in one after the other (each in its operator's colour, M.I.A + codename), and each
        // one's profile card comes up tied to its marker (IntroMiaCards). Its own lines (RescueLines).
        const float HeliStart = 3f, HoloStart = 7f, CutAt = 13.8f, ArriveAt = 17.8f, RopeStart = 18.1f, RopeEnd = 19.5f;
        // the helicopter's progress along its approach: the wide shot's ease-out (as if it arrived WideSpan after HeliStart)
        // until the hologram, slow while it is off screen, then easing into the hover over the low-angle shot
        const float WideSpan = 10f, QAtCut = .8f;
        // the hologram: the camera holds the wide framing through the wipe, then closes in on the hostage until just before the cut
        // (the push-in settles HoloSettle before the cut, just ahead of IntroHologram.SlamAt: the marker lands on a still frame)
        const float HoloMoveIn = 1.0f, HoloSettle = 2.2f;
        const float HoloEndDistance = 16f, HoloEndHeight = 13f, HoloOrbit = 25f, HoloFov = 38f;
        // CRUSHER on the radio (TutorialRadio, lower left) — they replaced the bottom subtitles (IntroSubtitle, kept for its previews)
        static readonly (float at, float seconds, string text)[] Lines =
        {
            (.8f, 5.6f, "緊張しろ、新入り。\n今回は実戦だ。"),
            (HoloStart + .9f, 4.9f, "テロリストは5人、民間人は1名。\nここが現在、人質が拘束されている位置だ。"),
            (CutAt + .5f, 5.2f, "敵を全員倒すか、\n目標地点まで人質を護送しろ。"),
        };
        // ---- the rescue intro ----
        /// <summary>The next intro is the rescue one (set by whoever starts it: TitleSelectScreen from MapData.rescueIntro, the debug menu).</summary>
        public static bool RescueStyle;
        /// <summary>Debug: the next house intro plays as the rescue one (Blindside > Debug > Preview Rescue Intro).</summary>
        public static bool ForceRescueOnce;
        /// <summary>The captives the rescue intro shows (OperatorData keys), in marker order.</summary>
        public static readonly string[] RescueKeys = { "GHOST", "SILENCE" };
        // when each captive's marker is driven in (s after the hologram begins); its card follows on impact
        const float RescueSlam0 = 2.3f, RescueSlamGap = 1.45f;
        // the camera round the hologram: RescueRadius out, RescueHeight up, turning RescueTurn degrees over the shot
        const float RescueRadius = 34f, RescueHeight = 25f, RescueTurn = 55f, RescueBlend = 1.6f, RescueFov = 40f;
        static readonly (float at, float seconds, string text)[] RescueLines =
        {
            (.8f, 5.6f, "今回は救出作戦だ。\n気を引き締めろ。"),
            (HoloStart + .9f, 5.6f, "前回の作戦で、GHOSTとSILENCEが敵に捕らえられた。\n二人はここに拘束されている。"),
            (CutAt + .5f, 5.2f, "二人を救出しろ。\n救出すれば、そのまま戦力に加わる。"),
        };
        (float at, float seconds, string text)[] lines = Lines;
        bool rescue;
        IntroMiaCards miaCards;
        Blindside.Tutorial.TutorialRadio radio;
        float radioCloseAt = -1f;
        readonly IntroHologram holo = new IntroHologram();
        IntroHoloScreen holoScreen;
        IntroSubtitle subtitle;
        IntroBriefing briefing;
        int nextLine;
        bool holoBegun, holoEnded, briefShown;
        // the mission briefing (top left) over the opening shot: after the OPERATION title (BlindsideTitle runs ~3 s from t=0, its exit
        // included), gone before the hologram wipe
        const float BriefAt = 3.05f, BriefUntil = HoloStart - .15f;
        // the wide camera: on bearing WideBearing (grid radians) from the house, drifting in radius, height and bearing
        const float WideBearing = 1.0f, WideDrift = .08f;
        const float WideR0 = 215f, WideR1 = 195f, WideH0 = 108f, WideH1 = 98f;
        const float WideFov = 30f, LowFov = 50f;
        // the low camera: on the lawn off the house's back right corner, eye height (grid offset from the house centre) —
        // the helicopter comes over the roof toward it and hovers between it and the house, the rope in full view
        static readonly Vector3 LowCamOffset = new Vector3(19.5f, -19f, 1.5f);
        const float LowRoofShare = .35f;   // how much of the look goes to the house (it stays in the frame)
        // the pass: PassAt s in, PassSide m to the wide camera's right and PassBelow m under it; it stops ArriveZ m up over
        // the middle of the house (the rope then reaches down to the roof)
        // SwingOut: how far the middle of the curve swings off the camera→house line (negative: to the left — so it sweeps
        // across the frame right → left before curling back onto the house)
        const float PassAt = 4.4f, PassSide = 6f, PassBelow = 5.5f, ArriveZ = 17f, SwingOut = -28f;
        // the pass's punch: camera shake and a wider lens while the helicopter is close (falling off over these metres)
        const float ShakeRange = 10f, ShakeMetres = .6f, ShakeDegrees = 2.5f, PunchRange = 14f, PunchFov = 9f;
        float rollNow, shakeRoll;

        // ---- the fast-rope: the first RappelCount operators of the attack squad slide down the rope onto the roof, one
        // after another (RappelStart, RappelGap apart, RappelSlide each), then step aside on the roof ----
        const int RappelCount = 2;
        const float RappelStart = 19.4f, RappelGap = .75f, RappelSlide = .9f, RappelStepAside = .45f;
        readonly System.Collections.Generic.List<UnitView> rappellers = new System.Collections.Generic.List<UnitView>();
        readonly System.Collections.Generic.List<bool> rappelLanded = new System.Collections.Generic.List<bool>();
        /// <summary>The rappellers' touchdowns play their own dust rings on the roof (IntroSequence's landing signal stands down).</summary>
        public static bool OwnsLandingFx;

        void SpawnRappellers()
        {
            ClearRappellers();
            var mb = FindFirstObjectByType<Blindside.Integration.MatchBootstrap>(FindObjectsInactive.Include);
            var title = FindFirstObjectByType<Blindside.UI.TitleSelectScreen>(FindObjectsInactive.Include);
            var prefab = mb != null ? mb.UnitPrefab : null;
            if (prefab == null || title == null) return;
            var squad = new System.Collections.Generic.List<OperatorData>();
            title.GetSquad(OperatorTeam.Attack, squad);
            var parent = introHouse != null ? introHouse.transform : transform;
            for (int i = 0; i < squad.Count && rappellers.Count < RappelCount; i++)
            {
                var op = squad[i];
                var go = Instantiate(prefab, parent);
                go.name = "IntroRappel_" + op.key;
                var uv = go.GetComponent<UnitView>();
                if (uv == null) { Destroy(go); continue; }
                if (op.modelPrefab != null || op.animationProfile != null) uv.ApplyModel(op.modelPrefab, op.animationProfile);
                uv.SetWeapon(Loadout.Of(op, true));
                uv.SetIdentity(true, false);
                uv.SetOverheadVisible(false);
                uv.SetSelected(false);
                go.SetActive(false);
                rappellers.Add(uv); rappelLanded.Add(false);
            }
            OwnsLandingFx = rappellers.Count > 0;
        }

        void ClearRappellers()
        {
            foreach (var r in rappellers) if (r != null) Destroy(r.gameObject);
            rappellers.Clear(); rappelLanded.Clear();
            OwnsLandingFx = false;
        }

        /// <summary>Each rappeller: hidden until its turn, then down the rope (just beside it) from under the hull to the
        /// roof, easing in and braking at the end, and a step aside once down.</summary>
        void UpdateRappellers(float t, HeliPose hp)
        {
            if (rappellers.Count == 0 || heliTarget == null) return;
            // the house's ridge runs along world X (its long side): stepping along it keeps them on the roof's top
            var right = Vector3.right;
            for (int i = 0; i < rappellers.Count; i++)
            {
                var uv = rappellers[i];
                if (uv == null) continue;
                float start = RappelStart + i * RappelGap;
                if (t < start) { if (uv.gameObject.activeSelf) uv.gameObject.SetActive(false); continue; }
                if (!uv.gameObject.activeSelf) uv.gameObject.SetActive(true);
                float p = Mathf.Clamp01((t - start) / RappelSlide);
                float top = hp.z - .8f - 1.6f;
                float h = Mathf.Lerp(top, arriveRoofY, p * p * (3f - 2f * p));
                // beside the rope, then a step aside along the ridge (alternate sides)
                float side = (i % 2 == 0 ? -1f : 1f);
                float aside = Smooth(start + RappelSlide, start + RappelSlide + RappelStepAside, t);
                var at = GridSpace.GridToWorld(hp.x, hp.y) + right * (side * (.35f + 1.6f * aside));
                uv.SetHeight(h);
                uv.SetPosition(at.x, -at.z);
                uv.SetFacing(hp.hd + Mathf.PI);   // facing back along the approach, toward the low camera's side
                if (p >= 1f && !rappelLanded[i])
                {
                    rappelLanded[i] = true;
                    CombatFx.Ring(new Vector3(at.x, arriveRoofY + .05f, at.z), new Color32(0xd6, 0xc9, 0xa8, 0xff), .5f, .9f);
                }
            }
        }

        Vector3[] heliP;   // the approach: grid x, grid y, height
        float[] heliAt;    // arc length at each point
        bool ready, lowCut;
        Unity.Cinemachine.CinemachineCamera vcam;
        // Source: `R.house.visible=!!G.cineOn` — the exterior shell exists only while the intro plays
        // (hidden behind the title screen before it, and for the whole match after it).
        GameObject houseExterior;

        struct HeliPose { public float x, y, z, hd; }

        /// <summary>The helicopter `q` (0..1) of the way along the approach, by distance flown. Its heading follows the path
        /// through the corners (the direction over ±HeadingSpan metres), and it keeps its approach heading at the end.</summary>
        HeliPose HeliPath(float q)
        {
            float total = heliAt[heliAt.Length - 1], s = Mathf.Clamp01(q) * total;
            var p = PathAt(s);
            var ahead = PathAt(Mathf.Min(total, s + HeadingSpan)) - PathAt(Mathf.Max(0f, Mathf.Min(s, total - HeadingSpan) - HeadingSpan));
            return new HeliPose { x = p.x, y = p.y, z = p.z, hd = Mathf.Atan2(ahead.y, ahead.x) };
        }
        const float HeadingSpan = 8f;

        Vector3 PathAt(float s)
        {
            int i = 0;
            while (i < heliP.Length - 2 && s > heliAt[i + 1]) i++;
            float k = Mathf.Clamp01((s - heliAt[i]) / Mathf.Max(1e-4f, heliAt[i + 1] - heliAt[i]));
            return Vector3.Lerp(heliP[i], heliP[i + 1], k);
        }

        /// <summary>The wide camera at time t (world).</summary>
        static Vector3 WideCamPos(float t)
        {
            float w = Smooth(0f, CutAt, t);
            float a = WideBearing + WideDrift * w, r = Mathf.Lerp(WideR0, WideR1, w), h = Mathf.Lerp(WideH0, WideH1, w);
            return GridSpace.GridToWorld(HX + Mathf.Cos(a) * r, HZ + Mathf.Sin(a) * r, h);
        }

        static Vector3 ToGrid(Vector3 world) => new Vector3(world.x, -world.z, world.y);   // GridSpace.GridToWorld inverted

        /// <summary>The approach, one smooth curve: from behind the wide camera on its right, skimming just past it (to the
        /// right and under it, PassAt s into the shot), sweeping across to the left (SwingOut m off the camera→house line)
        /// and curling back down onto the house — the last stretch straight, so it stops over the middle of the roof at
        /// ArriveZ nose first, no turn at the end. The pass time is solved from the timing curve (the wide camera moves).</summary>
        void BuildApproach(Vector2 house, float roofY)
        {
            arriveRoofY = roofY;
            var hw = GridSpace.GridToWorld(house.x, house.y);
            float tPass = PassAt;
            for (int iter = 0; iter < 3; iter++)
            {
                var cam = WideCamPos(tPass);
                var look = GridSpace.GridToWorld(HX, HZ, 1.2f);
                var fwd = (look - cam).normalized;
                var flat = new Vector3(fwd.x, 0f, fwd.z).normalized;
                var right = Vector3.Cross(Vector3.up, flat).normalized;
                var camGround = new Vector3(cam.x, 0f, cam.z);
                var mid = Vector3.Lerp(camGround, hw, .45f) + right * SwingOut + Vector3.up * 50f;
                var inDir = new Vector3(hw.x - mid.x, 0f, hw.z - mid.z).normalized;
                var keys = new[]
                {
                    cam - flat * 70f + right * 8f + Vector3.up * 1f,
                    cam + flat * 4f + right * PassSide - Vector3.up * PassBelow,
                    mid,
                    hw - inDir * 14f + Vector3.up * (ArriveZ + 2f),
                    hw + Vector3.up * ArriveZ,
                };
                SampleSpline(keys);
                // when is it at the pass key: arc length there → timing curve q = 1 − (1 − u)² → time
                float qPass = passArc / heliAt[heliAt.Length - 1];
                tPass = HeliStart + (1f - Mathf.Sqrt(1f - qPass)) * WideSpan;   // the pass falls in HeliProgress's first part
            }
        }
        float arriveRoofY = 6.6f, passArc;

        /// <summary>Hermite curve through `keys` (world), tangents along the neighbours' mean direction scaled by each
        /// segment's length (no overshoot), sampled into heliP (grid) with arc lengths.</summary>
        void SampleSpline(Vector3[] keys)
        {
            const int perSeg = 16;
            int n = keys.Length;
            var dirs = new Vector3[n];
            for (int i = 0; i < n; i++)
            {
                var din = i > 0 ? (keys[i] - keys[i - 1]).normalized : Vector3.zero;
                var dout = i < n - 1 ? (keys[i + 1] - keys[i]).normalized : Vector3.zero;
                dirs[i] = (din + dout).normalized;
            }
            heliP = new Vector3[(n - 1) * perSeg + 1];
            heliAt = new float[heliP.Length];
            int k = 0;
            for (int i = 0; i < n - 1; i++)
            {
                float len = Vector3.Distance(keys[i], keys[i + 1]);
                Vector3 m0 = dirs[i] * len, m1 = dirs[i + 1] * len;
                for (int j = 0; j < perSeg; j++, k++)
                {
                    float s = (float)j / perSeg, s2 = s * s, s3 = s2 * s;
                    var p = (2f * s3 - 3f * s2 + 1f) * keys[i] + (s3 - 2f * s2 + s) * m0 + (-2f * s3 + 3f * s2) * keys[i + 1] + (s3 - s2) * m1;
                    heliP[k] = ToGrid(p);
                    if (k > 0) heliAt[k] = heliAt[k - 1] + Vector3.Distance(heliP[k - 1], heliP[k]);
                    if (i == 1 && j == 0) passArc = heliAt[k];
                }
            }
            heliP[k] = ToGrid(keys[n - 1]);
            heliAt[k] = heliAt[k - 1] + Vector3.Distance(heliP[k - 1], heliP[k]);
        }

        /// <summary>Grid heading (radians, atan2(dy,dx)) from (x, y) to the house the helicopter brings the team to — the
        /// intro's house model (introHouse, else House_Exterior; neither: the orbit centre).</summary>
        public static float HeadingToHouse(float x, float y)
        {
            var h = houseKnown ? houseGrid : new Vector2(HX, HZ);
            return Mathf.Atan2(h.y - y, h.x - x);
        }
        static Vector2 houseGrid;
        static bool houseKnown;

        void Awake()
        {
            if (heliTarget) heliTarget.gameObject.SetActive(false); // source: h.vis=false until HELI_AT

            // Cached while still active (GameObject.Find can't see it once it's switched off).
            var house = transform.Find("House_Exterior");
            houseExterior = house ? house.gameObject : GameObject.Find("House_Exterior");
            if (houseExterior)
            {
                // where it stands, in grid coordinates — from its meshes (switched-off renderers report no bounds)
                bool any = false; var b = new Bounds();
                foreach (var mf in houseExterior.GetComponentsInChildren<MeshFilter>(true))
                {
                    if (mf.sharedMesh == null) continue;
                    var c = mf.transform.TransformPoint(mf.sharedMesh.bounds.center);
                    if (!any) { b = new Bounds(c, Vector3.zero); any = true; } else b.Encapsulate(c);
                }
                if (any) { houseGrid = new Vector2(b.center.x, -b.center.z); houseKnown = true; }
                houseExterior.SetActive(false);
            }
            // the intro's house stands where the map is: the helicopter turns to it
            if (mapRoot == null) { var m = GameObject.Find("Blindside_MapRoot"); if (m) mapRoot = m.transform; }
            float roofY = 6.6f;
            if (introHouse)
            {
                houseGrid = new Vector2(introHouse.transform.position.x, -introHouse.transform.position.z);
                houseKnown = true;
                // the house model's middle and the top of its roof (from the meshes: works while switched off)
                var model = introHouse.transform.Find("HouseModel");
                bool any = false; var hb = new Bounds();
                if (model)
                    foreach (var mf in model.GetComponentsInChildren<MeshFilter>(true))
                    {
                        if (mf.sharedMesh == null) continue;
                        var mb = mf.sharedMesh.bounds;
                        for (int c = 0; c < 8; c++)
                        {
                            var pt = mf.transform.TransformPoint(mb.center + Vector3.Scale(mb.extents, new Vector3((c & 1) == 0 ? -1 : 1, (c & 2) == 0 ? -1 : 1, (c & 4) == 0 ? -1 : 1)));
                            if (!any) { hb = new Bounds(pt, Vector3.zero); any = true; } else hb.Encapsulate(pt);
                        }
                    }
                if (any) { houseGrid = new Vector2(hb.center.x, -hb.center.z); roofY = hb.max.y; }
                introHouse.SetActive(false);
            }
            BuildApproach(houseKnown ? houseGrid : new Vector2(HX, HZ), roofY);
            ready = true;

            if (director != null) { director.played += OnIntroPlayed; director.stopped += OnIntroStopped; }
        }

        void OnDestroy()
        {
            if (director != null) { director.played -= OnIntroPlayed; director.stopped -= OnIntroStopped; }
            holo.End();
        }

        void OnIntroPlayed(PlayableDirector d)
        {
            rescue = RescueStyle;
            lines = rescue ? RescueLines : Lines;
            lowCut = false; rollNow = 0f;
            nextLine = 0; holoBegun = holoEnded = briefShown = false;
            EnsureIntroUi();
            SpawnRappellers();
            if (!introCutDone && houseExterior && showHouseExterior) houseExterior.SetActive(true);
            if (!introCutDone) ShowIntroWorld(true);
        }

        // The intro shows the house (and its lawn) in the map's place; the map comes back when it ends.
        void ShowIntroWorld(bool on)
        {
            if (introHouse == null) return;
            introWorld = on;
            introHouse.SetActive(on);
            if (on) { HideMapPieces(); return; }
            for (int i = 0; i < hiddenMap.Count; i++) if (hiddenMap[i] != null) hiddenMap[i].enabled = true;
            hiddenMap.Clear();
        }

        // Every frame while the house shows: pieces built or switched on meanwhile stay hidden too.
        void HideMapPieces()
        {
            HideUnder(mapRoot);
            if (alsoHide != null) for (int i = 0; i < alsoHide.Length; i++) HideUnder(alsoHide[i]);
            if (exfilZones == null) { var z = GameObject.Find("ExfilZones"); if (z) exfilZones = z.transform; }
            HideUnder(exfilZones);
        }

        void HideUnder(Transform root)
        {
            if (root == null) return;
            root.GetComponentsInChildren(false, mapPieces);
            for (int i = 0; i < mapPieces.Count; i++)
            {
                var r = mapPieces[i];
                if (!r.enabled) continue;
                r.enabled = false;
                hiddenMap.Add(r);
            }
        }

        /// <summary>
        /// The orbit shot's job ends when the intro Timeline does — left alone, IntroOrbitCam just
        /// freezes at whatever position/rotation Update() last wrote (the final orbit pose). The source
        /// ends playIntro() with `G.cineCam=null; ... G.cam.x=11; G.cam.y=6; G.cam.h=27;`, i.e. a hard
        /// cut back to the regular game camera, so hand the Main Camera to BattlefieldCamera here.
        /// </summary>
        void OnIntroStopped(PlayableDirector d)
        {
            d.stopped -= OnIntroStopped;
            introCutDone = true;
            CutToBattlefieldCamera();
        }

        void CutToBattlefieldCamera()
        {
            // Disables the CinemachineBrain too, so the orbit vcam can't blend back in.
            var start = Blindside.Map.MapLibrary.Current.AttackCamStart;
            BattlefieldCamera.Activate(start.x, start.y);

            if (cameraTarget) cameraTarget.gameObject.SetActive(false);
            if (heliTarget) heliTarget.gameObject.SetActive(false);

            // House_Exterior (MainBlock/LeftWing/GarageWing + their roof pieces) is a solid cosmetic
            // shell built only so the helicopter flyover reads as a real house from outside (source:
            // `R.house.visible=!!G.cineOn`) — confirmed by capturing the game view with it left on: MainRoof (and the solid MainBlock volume
            // beneath it) fully occludes every interior room, hiding the units entirely. The actual
            // gameplay geometry (thin walls + floor matching MapGrid) is separate and unaffected.
            if (houseExterior) houseExterior.SetActive(false);
            ClearRappellers();
            holo.End(); holoEnded = true;
            if (holoScreen != null) holoScreen.Hide();
            if (miaCards != null) miaCards.Clear();
            if (subtitle != null) subtitle.Stop();
            if (radio != null) { radio.Close(); radioCloseAt = -1f; }
            if (briefing != null) briefing.Stop();
            ShowIntroWorld(false);
        }

        static float Smooth(float a, float b, float t) { t = Mathf.Clamp01((t - a) / (b - a)); return t * t * (3f - 2f * t); }

        /// <summary>How far along its approach the helicopter is at t (0..1), and how fast that changes: the wide shot's
        /// ease-out until the hologram, a slow even stretch while the hologram is up, then easing out into the hover.</summary>
        static float HeliProgress(float t, out float dqdt)
        {
            float uHolo = Mathf.Clamp01((HoloStart - HeliStart) / WideSpan), qHolo = 1f - (1f - uHolo) * (1f - uHolo);
            if (t < HoloStart)
            {
                float u = Mathf.Clamp01((t - HeliStart) / WideSpan);
                dqdt = 2f * (1f - u) / WideSpan;
                return 1f - (1f - u) * (1f - u);
            }
            if (t < CutAt)
            {
                dqdt = (QAtCut - qHolo) / (CutAt - HoloStart);
                return Mathf.Lerp(qHolo, QAtCut, (t - HoloStart) / (CutAt - HoloStart));
            }
            float v = Mathf.Clamp01((t - CutAt) / (ArriveAt - CutAt));
            dqdt = (1f - QAtCut) * 2f * (1f - v) / (ArriveAt - CutAt);
            return QAtCut + (1f - QAtCut) * (1f - (1f - v) * (1f - v));
        }

        /// <summary>The hologram shot: the wide framing held through the wipe, then a slow push in on the hostage, coming round
        /// HoloOrbit degrees, ending HoloEndDistance out and HoloEndHeight up (eased; settled HoloSettle s before the cut).</summary>
        void HoloPose(float t, out Vector3 pos, out Vector3 look, out float fov)
        {
            float t0 = HoloStart + HoloMoveIn, t1 = CutAt - HoloSettle;
            var wide = WideCamPos(t0);
            var houseTop = GridSpace.GridToWorld(HX, HZ, 1.2f);
            var h = holo.HostagePoint;
            float p = Mathf.Clamp01((t - t0) / (t1 - t0));
            float e = p * p * p * (p * (p * 6f - 15f) + 10f);   // smootherstep
            var flat = new Vector3(wide.x - h.x, 0f, wide.z - h.z).normalized;
            var end = h + Quaternion.Euler(0f, HoloOrbit * e, 0f) * flat * HoloEndDistance + Vector3.up * HoloEndHeight;
            // in a curve: height comes down later than the distance closes
            pos = Vector3.Lerp(wide, end, e);
            pos.y = Mathf.Lerp(wide.y, end.y, Mathf.Pow(e, 1.6f));
            look = Vector3.Lerp(houseTop, h + Vector3.up * .5f, Mathf.Min(1f, e * 1.4f));
            fov = Mathf.Lerp(WideFov, HoloFov, e);
        }

        /// <summary>The hologram's begin / tick / end and the spoken lines, on the intro clock.</summary>
        void UpdateHoloAndLines(float t)
        {
            if (!holoBegun && t >= HoloStart)
            {
                holoBegun = true;
                var env = introHouse != null ? introHouse.transform.Find("Environment") : null;
                var hostageCell = Blindside.Map.MapLibrary.Current.hostage;
                var targets = rescue ? RescueTargets() : null;
                if (holo.Begin(mapRoot, env != null ? env.Find("Buildings") : null, env != null ? env.Find("Streets") : null,
                               GridSpace.GridToWorld(hostageCell.x, hostageCell.y), Camera.main, targets) && holoScreen != null)
                    holoScreen.Play(holo.Texture);
                if (rescue && miaCards != null && holo.Built)
                {
                    miaCards.Clear();
                    // which side of the screen each marker will be on when its card comes up (the camera's pose then): left of
                    // the other marker → its card on the left, so the leader lines don't cross
                    int n = Mathf.Min(holo.MarkerCount, RescueKeys.Length);
                    var side = new float[n];
                    for (int i = 0; i < n; i++)
                    {
                        RescuePose(HoloStart + holo.MarkerImpact(i), out var cp, out var lk, out _);
                        var right = Vector3.Cross(Vector3.up, (lk - cp).normalized);
                        side[i] = Vector3.Dot(holo.MarkerPoint(i) - cp, right);
                    }
                    for (int i = 0; i < n; i++)
                    {
                        bool onRight = n > 1 ? side[i] >= side[1 - i] : side[i] > 0f;
                        miaCards.Add(BuildAssetRegistry.Operator(RescueKeys[i]), holo.MarkerImpact(i), onRight);
                    }
                }
            }
            if (holoBegun && !holoEnded)
            {
                if (t >= CutAt) { holoEnded = true; holo.End(); if (holoScreen != null) holoScreen.Hide(); if (miaCards != null) miaCards.Clear(); }
                else
                {
                    holo.Tick(t - HoloStart, Camera.main);
                    if (rescue && miaCards != null && holo.Built)
                    {
                        if (t >= CutAt - .3f) miaCards.Hide(t - HoloStart);
                        miaCards.Tick(t - HoloStart, Camera.main, holo.MarkerPoint);
                    }
                }
            }
            if (!briefShown && t >= BriefAt && briefing != null)
            {
                briefShown = true;
                briefing.Play(IntroBriefing.DefaultValues(System.DateTime.Now), BriefUntil - BriefAt);
            }
            while (radio != null && nextLine < lines.Length && t >= lines[nextLine].at)
            {
                radio.SetSpeaker(false);
                radio.Say(lines[nextLine].text, false);
                radioCloseAt = lines[nextLine].at + lines[nextLine].seconds;
                nextLine++;
            }
            if (radio != null && radioCloseAt > 0f && t >= radioCloseAt && nextLine >= lines.Length) { radio.Close(); radioCloseAt = -1f; }
        }

        /// <summary>The rescue intro's markers: the map's hostage cell for the first captive, and for the second the map's other
        /// hostage cell when it has one — else the indoor floor cell farthest from the first (a stand-in until the map gives it).</summary>
        System.Collections.Generic.List<IntroHologram.Target> RescueTargets()
        {
            var map = Blindside.Map.MapLibrary.Current;
            var cells = new System.Collections.Generic.List<Vector2Int> { map.hostage };
            if (map.rescueHostages != null) foreach (var c in map.rescueHostages) if (c != map.hostage && cells.Count < RescueKeys.Length) cells.Add(c);
            if (cells.Count < RescueKeys.Length && map.terrain != null)
            {
                Vector2Int best = map.hostage; float bd = -1f;
                for (int y = 0; y < map.height; y++)
                    for (int x = 0; x < map.width; x++)
                    {
                        int k = y * map.width + x;
                        if (map.terrain[k] != Blindside.Map.MapTerrain.In || (map.crates != null && map.crates[k])) continue;
                        float d = (x - map.hostage.x) * (x - map.hostage.x) + (y - map.hostage.y) * (y - map.hostage.y);
                        if (d > bd) { bd = d; best = new Vector2Int(x, y); }
                    }
                cells.Add(best);
            }
            var list = new System.Collections.Generic.List<IntroHologram.Target>();
            for (int i = 0; i < cells.Count && i < RescueKeys.Length; i++)
            {
                var op = BuildAssetRegistry.Operator(RescueKeys[i]);
                list.Add(new IntroHologram.Target
                {
                    pos = GridSpace.GridToWorld(cells[i].x, cells[i].y),
                    color = op != null ? op.color : Color.white,
                    label = "M.I.A <size=60%>" + (op != null ? op.DisplayName : RescueKeys[i]) + "</size>",
                    slamAt = RescueSlam0 + i * RescueSlamGap,
                });
            }
            return list;
        }

        /// <summary>The rescue hologram shot: from the wide framing into a slow turn round the whole hologram (RescueRadius out,
        /// RescueHeight up), RescueTurn degrees over the shot, looking at the house's middle.</summary>
        void RescuePose(float t, out Vector3 pos, out Vector3 look, out float fov)
        {
            float t0 = HoloStart + HoloMoveIn;
            var wide = WideCamPos(t0);
            var centre = GridSpace.GridToWorld(HX, HZ, 0f);
            var flat = new Vector3(wide.x - centre.x, 0f, wide.z - centre.z).normalized;
            float turn = RescueTurn * Mathf.Clamp01((t - t0) / (CutAt - t0));
            var orbit = centre + Quaternion.Euler(0f, turn, 0f) * flat * RescueRadius + Vector3.up * RescueHeight;
            float b = Mathf.Clamp01((t - t0) / RescueBlend);
            float e = b * b * b * (b * (b * 6f - 15f) + 10f);   // smootherstep
            pos = Vector3.Lerp(wide, orbit, e);
            look = Vector3.Lerp(GridSpace.GridToWorld(HX, HZ, 1.2f), centre, e);
            fov = Mathf.Lerp(WideFov, RescueFov, e);
        }

        /// <summary>The subtitle and the hologram's screen, built once under the intro's canvas (the letterbox's): the hologram view
        /// just behind the letterbox, the subtitle on top.</summary>
        void EnsureIntroUi()
        {
            if (radio != null && holoScreen != null && briefing != null && miaCards != null) return;
            var lb = FindFirstObjectByType<LetterboxBars>(FindObjectsInactive.Include);
            var canvas = lb != null ? lb.GetComponentInParent<Canvas>(true) : null;
            if (canvas == null) return;
            var lbTop = lb.transform;
            while (lbTop.parent != null && lbTop.parent != canvas.transform) lbTop = lbTop.parent;
            if (holoScreen == null) holoScreen = IntroHoloScreen.Build(canvas.transform, lbTop.GetSiblingIndex());
            if (briefing == null) briefing = IntroBriefing.Build(canvas.transform);
            if (miaCards == null) miaCards = IntroMiaCards.Build(canvas.transform);
            if (radio == null)
            {
                radio = Blindside.Tutorial.TutorialRadio.Build(transform);
                radio.canvas.sortingOrder = canvas.sortingOrder + 1;   // over the letterbox and the hologram
            }
        }

        void SetFov(float fov)
        {
            if (vcam == null && cameraTarget) vcam = cameraTarget.GetComponent<Unity.Cinemachine.CinemachineCamera>();
            if (vcam == null) return;
            var lens = vcam.Lens; lens.FieldOfView = fov; vcam.Lens = lens;
        }

        void Update()
        {
            if (introWorld) HideMapPieces();
            if (!ready || introCutDone || director == null || director.duration <= 0) return;
            float t = (float)director.time;

            // ---- the helicopter: fast past the wide camera, slowing all the way down to the hover ----
            HeliPose hp = default;
            float q = 0f;
            bool heliOn = t >= HeliStart;
            if (heliOn)
            {
                if (heliTarget && !heliTarget.gameObject.activeSelf) heliTarget.gameObject.SetActive(true);
                q = HeliProgress(t, out float dqdt);
                hp = HeliPath(q);
                if (heliTarget)
                {
                    // hp.x/hp.y are the grid-plane coords, hp.z is height (world Y<-z, syncHeli).
                    heliTarget.position = GridSpace.GridToWorld(hp.x, hp.y, hp.z);
                    heliTarget.rotation = Quaternion.LookRotation(GridSpace.FacingToWorldDirection(hp.hd), Vector3.up);
                }
                if (heliRig)
                {
                    // nose down while it is fast, a flare (nose up) as it brakes over the house
                    heliRig.tilt = -0.32f * Mathf.Pow(1f - q, 1.2f) + .14f * Mathf.Sin(Mathf.PI * Mathf.Clamp01((t - (ArriveAt - 1.4f)) / 1.4f));
                    // bank into the turns: the coordinated-turn angle atan(v·ω / g) from the heading's rate along the path
                    float dq = .004f;
                    float yawRate = Mathf.DeltaAngle(hp.hd * Mathf.Rad2Deg, HeliPath(Mathf.Min(1f, q + dq)).hd * Mathf.Rad2Deg) * Mathf.Deg2Rad / dq * dqdt;
                    float v = dqdt * heliAt[heliAt.Length - 1];
                    float bank = q + dq >= 1f ? 0f : Mathf.Clamp(-Mathf.Atan(v * yawRate / 9.81f), -.5f, .5f);
                    rollNow = Mathf.Lerp(rollNow, bank, 1f - Mathf.Exp(-Time.deltaTime * 4f));
                    heliRig.roll = rollNow;
                    // the rope: from the hull (0.8 under the origin, length/1.5 at this 1× scale) down onto the roof
                    float full = 1.5f * Mathf.Max(0f, hp.z - .8f - (arriveRoofY + .05f));
                    heliRig.ropeLength = full * Smooth(RopeStart, RopeEnd, t);
                }
                UpdateRappellers(t, hp);
            }
            UpdateHoloAndLines(t);
            if (!cameraTarget) return;

            Vector3 camPos, look;
            float fov;
            var houseTop = GridSpace.GridToWorld(HX, HZ, 1.2f);
            if (t >= HoloStart + HoloMoveIn && t < CutAt && holo.Built)
            {
                // the hologram: closing in on the hostage (the rescue: coming round the whole of it)
                if (rescue) RescuePose(t, out camPos, out look, out fov);
                else HoloPose(t, out camPos, out look, out fov);
                shakeRoll = 0f;
            }
            else if (t < CutAt)
            {
                // wide: the town, centred on the house, the camera drifting slowly in and round; it leans a touch
                // toward the helicopter while it is in the shot
                camPos = WideCamPos(t);
                look = houseTop;
                // once it is past the camera, the shot leans a touch toward it
                if (heliOn) look = Vector3.Lerp(houseTop, GridSpace.GridToWorld(hp.x, hp.y, hp.z), .12f * Smooth(PassAt + .2f, PassAt + 1.5f, t));
                fov = WideFov;
                shakeRoll = 0f;
                if (heliOn)
                {
                    // the pass: the downwash shakes the camera and the lens kicks wider while it is close
                    float d = Vector3.Distance(camPos, GridSpace.GridToWorld(hp.x, hp.y, hp.z));
                    float amp = Mathf.Exp(-d / ShakeRange);
                    float n = t * 19f;
                    camPos += new Vector3(Mathf.PerlinNoise(n, 0f) - .5f, Mathf.PerlinNoise(0f, n) - .5f, Mathf.PerlinNoise(n, n) - .5f) * (2f * ShakeMetres * amp);
                    shakeRoll = (Mathf.PerlinNoise(n * .7f, 3f) - .5f) * 2f * ShakeDegrees * amp;
                    fov += PunchFov * Mathf.Exp(-d / PunchRange);
                }
            }
            else
            {
                // low angle: tracking the helicopter as it comes in over the house, the house kept in the frame
                camPos = GridSpace.GridToWorld(HX + LowCamOffset.x, HZ + LowCamOffset.y, LowCamOffset.z);
                var roof = GridSpace.GridToWorld(HX, HZ, 5f);
                var heli = heliOn ? GridSpace.GridToWorld(hp.x, hp.y, hp.z - 1f) : roof;
                look = Vector3.Lerp(heli, roof, LowRoofShare);
                fov = LowFov;
                if (!lowCut)
                {
                    lowCut = true;
                    if (vcam == null) vcam = cameraTarget.GetComponent<Unity.Cinemachine.CinemachineCamera>();
                    if (vcam != null) vcam.PreviousStateIsValid = false;   // a hard cut, no blend from the wide pose
                }
            }
            cameraTarget.position = camPos;
            cameraTarget.LookAt(look, Vector3.up);
            if (shakeRoll != 0f) cameraTarget.Rotate(0f, 0f, shakeRoll, Space.Self);
            SetFov(fov);
        }
    }
}
