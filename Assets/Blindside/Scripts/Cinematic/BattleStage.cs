// Battle stage for BattleDirector: an urban street far off the map (Origin, beyond the camera's far plane from the map)
// where copies of the two fighters face each other down the street, the opening shooter on the left. Each volley is
// cut like a firefight: first the shooter alone (front three-quarter, low) firing — muzzle flashes, ejected casings,
// rounds streaking off-screen — then a cut to the unit being shot at, where those rounds land (sparks, dust, the
// hit reaction, damage numbers; a headshot snaps to a close-up on the head). Return fire is the same cut reversed.
// Presentation only: the copies have no scripts or colliders, nothing here reads or draws UnityEngine.Random (the
// combat rolls share that stream; jitter uses a System.Random of its own), and everything runs on unscaled time so
// hit stops don't freeze it.
using System;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using Blindside.Combat;
using Blindside.Map;
using Blindside.Rendering;
using Blindside.UI;

namespace Blindside.Cinematic
{
    public class BattleStage : MonoBehaviour
    {
        public static readonly Vector3 Origin = new Vector3(-400f, 0f, 0f);
        const float ActorX = 4f;

        /// <summary>Camera shake impulse (metres) — the director forwards it to BattlefieldCamera.Kick.</summary>
        public Action<float> Kick;
        /// <summary>K.I.A popup at a world point.</summary>
        public Action<Vector3> Kia;
        /// <summary>Headshot UI: unit, label point, head point, damage.</summary>
        public Action<ICombatUnit, Vector3, Vector3, int> Headshot;

        BattleActor leftActor, rightActor;
        readonly System.Random rng = new System.Random(0xB47);
        readonly List<Fx> fx = new List<Fx>();
        readonly List<(float at, Action act)> timeline = new List<(float, Action)>();
        float fov;

        // ================================================================
        //  Camera: which shot is up, on whom, since when (each shot drifts in a little)
        // ================================================================
        enum Shot { Shooter, Target, Head, Face }
        Shot shot;
        BattleActor camActor;
        float shotT0;

        void Cut(Shot s, BattleActor on)
        {
            if (on == null) return;
            shot = s; camActor = on; shotT0 = Time.unscaledTime;
        }

        public CamPose CameraPose()
        {
            var a = camActor ?? leftActor;
            if (a == null) return new CamPose(Origin + new Vector3(0f, 1.2f, -6f), Quaternion.identity, fov);
            float t = Time.unscaledTime - shotT0;
            float s = a.Scale;   // a taller model: every offset grows with it so the framing stays the same
            Vector3 c, look; float f;
            switch (shot)
            {
                case Shot.Shooter:
                {
                    // In front of the shooter, low, off to the street's near side: the muzzle points past the lens.
                    float push = Mathf.Min(t, 1.2f) * .22f;
                    look = a.Torso + new Vector3(a.Dir * .35f * s, .05f * s, 0f);
                    c = a.Home + new Vector3(a.Dir * (1.8f - push) * s, .78f * s, (-1.3f + push * .45f) * s);
                    f = fov + 8f;
                    break;
                }
                case Shot.Face:
                {
                    // the critical round: tight on the shooter's face, pushing in while it lines up the shot
                    float push = UIEase.Tactical(Mathf.Clamp01(t / CritLead)) * .25f;
                    look = a.Head + new Vector3(0f, -.02f * s, 0f);
                    c = a.Head + new Vector3(a.Dir * (1.05f - push) * s, .04f * s, (-.42f + push * .12f) * s);
                    f = fov - 10f;
                    break;
                }
                case Shot.Head:
                {
                    float push = UIEase.Tactical(Mathf.Clamp01(t / .5f)) * .3f;
                    look = a.Head;
                    c = a.Head + new Vector3(a.Dir * (1.75f - push) * s, .06f * s, (-.6f + push * .15f) * s);
                    f = fov - 4f;
                    break;
                }
                default:
                {
                    // From the shooter's side of the street, a little off the line of fire, on the unit being shot at.
                    float push = Mathf.Min(t, 1.5f) * .18f;
                    look = a.Torso + new Vector3(0f, .05f * s, 0f);
                    c = a.Home + new Vector3(a.Dir * (2.5f - push) * s, 1f * s, -1.05f * s);
                    f = fov + 4f;
                    break;
                }
            }
            return new CamPose(c, Quaternion.LookRotation(look - c, Vector3.up), f);
        }

        /// <summary>Builds the stage (inactive) for an engagement between a and b (onRight stands on the right), or returns
        /// null when either has no figure to copy.</summary>
        public static BattleStage TryBuild(ICombatUnit a, ICombatUnit b, ICombatUnit onRight, float fov)
        {
            if (!(a?.View is Component va) || va == null || !(b?.View is Component vb) || vb == null) return null;
            var go = new GameObject("BattleStage");
            go.SetActive(false);
            go.transform.position = Origin;
            var stage = go.AddComponent<BattleStage>();
            stage.fov = fov;
            stage.BuildSet(onRight);
            var right = onRight == b ? b : a;
            var left = right == a ? b : a;
            stage.leftActor = BattleActor.Create(left, go.transform, Origin + new Vector3(-ActorX, 0f, 0f), +1f);
            stage.rightActor = BattleActor.Create(right, go.transform, Origin + new Vector3(ActorX, 0f, 0f), -1f);
            stage.Cut(Shot.Shooter, stage.leftActor);
            return stage;
        }

        /// <summary>Shows the stage, opening on `firstShooter` (the shot the fade reveals).</summary>
        public void Show(ICombatUnit firstShooter)
        {
            Cut(Shot.Shooter, ActorOf(firstShooter) ?? leftActor);
            gameObject.SetActive(true);
        }

        public Vector3? LabelOf(ICombatUnit u)
        {
            var a = ActorOf(u);
            return a != null ? a.Label : (Vector3?)null;
        }

        BattleActor ActorOf(ICombatUnit u)
        {
            if (leftActor != null && leftActor.Unit == u) return leftActor;
            if (rightActor != null && rightActor.Unit == u) return rightActor;
            foreach (var a in supporters) if (a.Unit == u) return a;
            return null;
        }

        /// <summary>`delay` seconds (unscaled) from now, run `act`.</summary>
        public void After(float delay, Action act) => timeline.Add((Time.unscaledTime + delay, act));

        // ================================================================
        //  Volleys: shooter cut → target cut
        // ================================================================
        BattleActor volleyShooter, volleyTarget;
        bool targetUp;                                       // this volley has cut to its target
        readonly List<ShotEvent> pendingImpacts = new List<ShotEvent>();
        float busyUntil;                                     // when the queued cuts/impacts are done

        const float AimBeat = .26f, FewInterval = .17f, BurstInterval = .075f, AfterBurst = .22f;
        const float ImpactSeconds = .42f, BurstImpactSeconds = .14f, HeadshotSeconds = .8f, DeathSeconds = 1.25f;

        float ImpactTime(ShotEvent e) => e.Headshot ? (CritCut(e) ? CritLead : 0f) + HeadshotSeconds : e.Shots >= 4 ? BurstImpactSeconds : ImpactSeconds;

        // ---- a critical round (either side's): the shooter's face in close-up first, then the round, then the head ----
        /// <summary>Whether a unit is the player's (set by the director; kept for presentation choices).</summary>
        public Func<ICombatUnit, bool> IsAlly;
        const float CritLead = .7f, CritFire = .52f;

        static bool CritCut(ShotEvent e) => e.Headshot && e.Hit && !e.ShieldBlocked;

        /// <summary>Where a round lands: a critical one goes through the shooter's face close-up first.</summary>
        void Land(ShotEvent e)
        {
            if (!CritCut(e)) { Impact(e); return; }
            var s = ActorOf(e.Attacker); var t = ActorOf(e.Target);
            if (s == null || t == null || s.Dead) { Impact(e); return; }
            Cut(Shot.Face, s);
            After(CritFire, () => CritRound(s, t));
            After(CritLead, () => Impact(e));
        }

        /// <summary>The critical round leaving the muzzle: a bigger flash, a hot streak, a harder kick.</summary>
        void CritRound(BattleActor s, BattleActor t)
        {
            if (s.Dead) return;
            s.Recoil();
            s.PlayFire();
            Vector3 m = s.Muzzle, dir = new Vector3(s.Dir, 0f, 0f);
            MuzzleFlash.Play(m, dir);
            Blob(m + dir * .08f, new Color32(0xff, 0xc8, 0x1e, 0xff), .34f, .1f);
            Sparks(m + dir * .1f, new Color32(0xff, 0xd2, 0x7a, 0xff), 10, 3.4f);
            Beam(m, t.Head, new Color32(0xff, 0x3b, 0x54, 0xff), .06f, .12f);
            Casing(s);
            Kick?.Invoke(.08f);
        }

        /// <summary>One shot of the engagement; returns how long it needs on screen (the director holds the next shot until then).
        /// The first shot of a volley opens on the shooter firing the whole burst, then cuts to the target where the rounds land.</summary>
        public float PlayShot(ShotEvent e)
        {
            var s = ActorOf(e.Attacker); var t = ActorOf(e.Target);
            if (t != null && e.Attacker != leftActor?.Unit && e.Attacker != rightActor?.Unit) return PlaySupport(e, t);   // 支援射撃: not one of the two fighters
            if (s == null || t == null) return 0f;
            float now = Time.unscaledTime;
            if (s != volleyShooter || e.Index == 0)
            {
                if (volleyShooter != null && volleyShooter != s) volleyShooter.Advance(false);
                volleyShooter = s; volleyTarget = t; targetUp = false;
                pendingImpacts.Clear();
                s.Advance(true);
                Cut(Shot.Shooter, s);
                int n = Mathf.Max(1, e.Shots);
                float interval = n >= 4 ? BurstInterval : FewInterval;
                for (int k = 0; k < n; k++) After(AimBeat + k * interval, () => MuzzleBurst(s, t));
                float burst = AimBeat + (n - 1) * interval + AfterBurst;
                pendingImpacts.Add(e);
                After(burst, () => { if (volleyShooter == s) { Cut(Shot.Target, t); targetUp = true; FlushImpacts(); } });
                busyUntil = now + burst + ImpactTime(e);
                return burst + ImpactTime(e);
            }
            if (!targetUp)
            {
                pendingImpacts.Add(e);
                busyUntil = Mathf.Max(busyUntil, now) + ImpactTime(e);
                return busyUntil - now;
            }
            Land(e);
            busyUntil = Mathf.Max(busyUntil, now + ImpactTime(e));
            return ImpactTime(e);
        }

        // ---- 支援射撃: an ally joins the fight — it steps in beside the shooter, its face in close-up with the badge, then it fires ----
        /// <summary>The 支援射撃 badge for this unit (set by the director).</summary>
        public Action<ICombatUnit> Support;
        const float SupportFace = .55f, SupportAim = .2f, SupportFly = .32f, SupportForward = .9f, SupportSide = 1.25f, SupportStep = 1.05f;
        readonly List<BattleActor> supporters = new List<BattleActor>(2);

        // beside the fighter who isn't being shot at, a step ahead of it (clear of the cover behind), in a row on the far side of the
        // street: the cuts' cameras stand on the near side of each, so on the near side of the street they would end up inside the buildings
        BattleActor SupporterFor(ICombatUnit u, BattleActor target)
        {
            foreach (var a in supporters) if (a.Unit == u) return a;
            if (!(u?.View is Component c) || c == null) return null;
            var mate = target == rightActor ? leftActor : rightActor;
            float side = SupportSide + supporters.Count * SupportStep;
            var made = BattleActor.Create(u, transform, mate.Home + new Vector3(mate.Dir * SupportForward, 0f, side), mate.Dir);
            supporters.Add(made);
            return made;
        }

        /// <summary>One 支援射撃 round, after whatever is still playing; returns how long it needs on screen.</summary>
        float PlaySupport(ShotEvent e, BattleActor t)
        {
            float now = Time.unscaledTime, wait = Mathf.Max(0f, busyUntil - now);
            float lead = SupportFace + SupportAim + SupportFly;
            float total = wait + lead + (CritCut(e) ? HeadshotSeconds : ImpactSeconds);
            busyUntil = now + total;
            After(wait, () =>
            {
                var s = SupporterFor(e.Attacker, t);
                if (s == null) { Impact(e); return; }
                if (volleyShooter != null && volleyShooter != s) volleyShooter.Advance(false);
                volleyShooter = s; volleyTarget = t; targetUp = true;
                pendingImpacts.Clear();
                Cut(Shot.Face, s);
                Support?.Invoke(e.Attacker);
                After(SupportFace, () => { s.Advance(true); Cut(Shot.Shooter, s); });
                After(SupportFace + SupportAim, () => { if (CritCut(e)) CritRound(s, t); else MuzzleBurst(s, t); });
                After(lead, () => { Cut(Shot.Target, t); Impact(e); });
            });
            return total;
        }

        void FlushImpacts()
        {
            float delay = 0f;
            foreach (var e in pendingImpacts)
            {
                var ev = e;
                if (delay <= 0f) Land(ev); else After(delay, () => Land(ev));
                delay += ImpactTime(ev);
            }
            pendingImpacts.Clear();
        }

        /// <summary>Death of `u` once the queued impacts have landed; returns how long until it has played out.</summary>
        public float PlayDeath(ICombatUnit u)
        {
            var a = ActorOf(u);
            if (a == null) return 0f;
            float now = Time.unscaledTime;
            float start = Mathf.Max(0f, busyUntil - now) + .08f;
            After(start, () =>
            {
                Cut(Shot.Target, a);
                a.Die();
                Kick?.Invoke(.06f);
                Dust(a.Home + new Vector3(-a.Dir * .45f, .05f, 0f), 4, .5f);
                Kia?.Invoke(a.Label);
            });
            busyUntil = now + start + DeathSeconds;
            return start + DeathSeconds;
        }

        // ---- the shooter's cut: muzzle flash, light, casing, a round streaking off toward the target ----
        void MuzzleBurst(BattleActor s, BattleActor t)
        {
            if (s.Dead) return;
            s.Recoil();
            s.PlayFire();
            Vector3 m = s.Muzzle, dir = new Vector3(s.Dir, 0f, 0f);
            MuzzleFlash.Play(m, dir);   // particles + its own light, on the gun's muzzle
            Casing(s);
            var aim = t.Torso + new Vector3(0f, (float)(rng.NextDouble() - .5) * .2f, (float)(rng.NextDouble() - .5) * .3f);
            Beam(m, aim, new Color32(0xff, 0xf0, 0xb4, 0xff), .03f, .07f);
            Kick?.Invoke(.035f);
        }

        // ---- the target's cut: where each round lands ----
        void Impact(ShotEvent e)
        {
            var s = ActorOf(e.Attacker); var t = ActorOf(e.Target);
            if (s == null || t == null) return;
            Vector3 from = s.Muzzle;
            if (e.ShieldBlocked)
            {
                var at = t.Torso + new Vector3(t.Dir * .32f, 0f, 0f);
                Beam(from, at, new Color32(0xff, 0xf0, 0xb4, 0xff), .03f, .08f);
                Sparks(at, Color.white, 12, 3.2f);
                Blob(at, Color.white, .3f, .1f);
                t.Flinch(.05f, false);
                t.PlayDodge(true);
                Kick?.Invoke(.04f);
                Popup(t, "BLOCKED", new Color32(0xd8, 0xe2, 0xec, 0xff), 2f);
            }
            else if (!e.Hit)
            {
                // a miss: the round goes wide — past one side of the body (never through it) — and kicks up the street behind
                float side = (float)(rng.NextDouble() - .5) * 2f;
                var at = t.Home + new Vector3(-t.Dir * (2.2f + (float)rng.NextDouble() * 2.5f), .02f + (float)rng.NextDouble() * 1.1f,
                                              Mathf.Sign(side) * (.6f + Mathf.Abs(side) * .6f) * t.Scale);
                Beam(from, at, new Color32(0xff, 0xf0, 0xb4, 0xff), .025f, .08f);
                Dust(at, 3, .35f);
                Chips(at, 5);
                Popup(t, "ミス", new Color32(0xb8, 0xc0, 0xca, 0xff), 2.2f);
            }
            else if (e.Headshot)
            {
                var at = t.Head;
                Beam(from, at, new Color32(0xff, 0xf0, 0xb4, 0xff), .035f, .08f);
                Cut(Shot.Head, t);
                Blob(at, Color.white, .36f, .09f);
                Sparks(at, new Color32(0xff, 0xd2, 0x7a, 0xff), 16, 3.6f);
                Sparks(at, Color.white, 8, 2.4f);
                t.Flinch(.22f, true);
                Kick?.Invoke(.1f);
                Popup(t, $"<size=60%>クリティカル</size>\n{e.Damage}", new Color32(0xff, 0xc8, 0x1e, 0xff), 2.8f);
                Headshot?.Invoke(e.Target, t.Label, t.Head, e.Damage);
                After(.62f, () => { if (shot == Shot.Head && camActor == t && !t.Dead) Cut(Shot.Target, t); });
            }
            else
            {
                var at = t.Torso + new Vector3(0f, (float)(rng.NextDouble() - .5) * .18f, (float)(rng.NextDouble() - .5) * .16f);
                Beam(from, at, new Color32(0xff, 0xf0, 0xb4, 0xff), .03f, .07f);
                Blob(at, new Color32(0xff, 0xb1, 0x3b, 0xff), .18f, .08f);
                Sparks(at, new Color32(0xff, 0xc8, 0x6a, 0xff), 9, 2.8f);
                t.Flinch(.13f, true);
                Kick?.Invoke(.05f);
                Popup(t, e.Damage.ToString(), Color.white, 2.6f);
            }
        }

        void Update()
        {
            float now = Time.unscaledTime, dt = Time.unscaledDeltaTime;
            for (int i = 0; i < timeline.Count; i++)
            {
                if (timeline[i].at > now) continue;
                var act = timeline[i].act;
                timeline.RemoveAt(i--);
                try { act(); } catch (Exception ex) { Debug.LogException(ex); }
            }
            leftActor?.Tick(now, dt);
            rightActor?.Tick(now, dt);
            foreach (var a in supporters) a.Tick(now, dt);
            var camRot = CameraPose().rotation;
            for (int i = fx.Count - 1; i >= 0; i--)
                if (!fx[i].Tick(now, dt, camRot)) { if (fx[i].go != null) Destroy(fx[i].go); fx.RemoveAt(i); }
        }

        void OnDestroy()
        {
            foreach (var f in fx) { f.Release(); if (f.go != null) Destroy(f.go); }
            leftActor?.DestroyMaterials();
            rightActor?.DestroyMaterials();
            foreach (var a in supporters) a.DestroyMaterials();
            foreach (var m in ownMaterials) if (m != null) Destroy(m);
        }

        // ================================================================
        //  Set: a street between two rows of buildings, the map's own materials (one tile per metre like the map)
        // ================================================================
        readonly List<Material> ownMaterials = new List<Material>();

        void BuildSet(ICombatUnit onRight)
        {
            var layout = new System.Random(0x51);
            bool indoor = IsIndoor(onRight);
            Block(PrimitiveType.Plane, new Vector3(0f, 0f, 0f), new Vector3(4.4f, 1f, 1.6f),
                Tiled(FindMapMaterial(indoor ? "Mat_Floor_Inside" : "Mat_Floor_Outside"), new Vector2(44f, 16f), new Color(.22f, .24f, .27f))).name = "Street";

            var wood = Tiled(FindMapMaterial("Mat_Wall_Wood"), new Vector2(3f, 5f), new Color(.36f, .3f, .24f));
            var hard = Tiled(FindMapMaterial("Mat_Hard"), new Vector2(3f, 5f), new Color(.29f, .32f, .37f));
            var windowDark = Flat(new Color(.04f, .05f, .07f), false);
            var windowLit = Flat(new Color(1f, .74f, .38f), true);
            const float facade = 3.3f, depth = 2.6f;
            for (int side = -1; side <= 1; side += 2)
                for (float x = -21f; x < 21f; x += 3.5f)
                {
                    float h = 3.4f + (float)layout.NextDouble() * 3.4f;
                    float zc = side * (facade + depth / 2f);
                    Block(PrimitiveType.Cube, new Vector3(x + 1.75f, h / 2f, zc), new Vector3(3.3f, h, depth), layout.NextDouble() < .5 ? wood : hard).name = "Building";
                    for (float wy = 1.5f; wy < h - .6f; wy += 1.7f)
                        for (int wx = 0; wx < 2; wx++)
                        {
                            var mat = layout.NextDouble() < .18 ? windowLit : windowDark;
                            Block(PrimitiveType.Cube, new Vector3(x + .95f + wx * 1.6f, wy, side * (facade - .02f)), new Vector3(.7f, .95f, .06f), mat).name = "Window";
                        }
                }
            // the street's ends, so a long lens never looks into the void
            foreach (float ex in new[] { -22.5f, 22.5f })
                Block(PrimitiveType.Cube, new Vector3(ex, 3.5f, 0f), new Vector3(1.5f, 7f, 12f), hard).name = "EndBlock";

            // cover beside each fighter, a barricade further back, rubble along the kerbs, two lamp posts
            var crate = Tiled(FindMapMaterial("Mat_Crate"), Vector2.one, new Color(.45f, .36f, .2f));
            foreach (float dir in new[] { 1f, -1f })
            {
                float ax = -dir * ActorX;
                // behind each fighter (as the cameras see them), so the cover frames the shot instead of blocking it
                Block(PrimitiveType.Cube, new Vector3(ax - dir * .75f, .45f, .95f), Vector3.one * .9f, crate).name = "Cover";
                Block(PrimitiveType.Cube, new Vector3(ax - dir * 1.3f, .3f, 1.75f), new Vector3(.8f, .6f, .6f), crate).name = "Cover";
                Block(PrimitiveType.Cube, new Vector3(ax - dir * 2.6f, .32f, -1.1f), new Vector3(1.8f, .64f, .5f), hard).name = "Barricade";
            }
            for (int i = 0; i < 26; i++)
            {
                float x = (float)(layout.NextDouble() * 36 - 18), z = (float)(layout.NextDouble() < .5 ? -1 : 1) * (2.4f + (float)layout.NextDouble() * .8f);
                float s = .08f + (float)layout.NextDouble() * .22f;
                var r = Block(PrimitiveType.Cube, new Vector3(x, s / 2f, z), new Vector3(s * 1.3f, s, s), hard);
                r.name = "Rubble";
                r.transform.localRotation = Quaternion.Euler(0f, (float)layout.NextDouble() * 90f, 0f);
            }
            var pole = Flat(new Color(.12f, .13f, .15f), false);
            foreach (var p in new[] { new Vector3(-1.2f, 0f, 2.9f), new Vector3(9.5f, 0f, -2.9f), new Vector3(-11f, 0f, -2.9f) })
            {
                Block(PrimitiveType.Cylinder, p + new Vector3(0f, 1.6f, 0f), new Vector3(.09f, 1.6f, .09f), pole).name = "LampPost";
                Block(PrimitiveType.Cube, p + new Vector3(0f, 3.2f, -Mathf.Sign(p.z) * .3f), new Vector3(.22f, .1f, .6f), pole).name = "LampArm";
                Block(PrimitiveType.Cube, p + new Vector3(0f, 3.12f, -Mathf.Sign(p.z) * .55f), new Vector3(.2f, .06f, .3f), windowLit).name = "Lamp";
            }
        }

        static bool IsIndoor(ICombatUnit u)
        {
            var map = MapLibrary.Current;
            if (map == null || u == null) return false;
            int x = Mathf.RoundToInt(u.X), y = Mathf.RoundToInt(u.Y);
            if (x < 0 || y < 0 || x >= map.width || y >= map.height) return false;
            return map.TerrainAt(x, y) != MapTerrain.Out;
        }

        GameObject Block(PrimitiveType type, Vector3 local, Vector3 scale, Material mat)
        {
            var go = GameObject.CreatePrimitive(type);
            Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(transform, false);
            go.transform.localPosition = local;
            go.transform.localScale = scale;
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            return go;
        }

        static Material FindMapMaterial(string name)
        {
            var root = GameObject.Find("Blindside_MapRoot");
            if (root == null) return null;
            foreach (var r in root.GetComponentsInChildren<MeshRenderer>(true))
                foreach (var m in r.sharedMaterials)
                    if (m != null && m.name.StartsWith(name, StringComparison.Ordinal)) return m;
            return null;
        }

        Material Tiled(Material src, Vector2 tiles, Color fallback)
        {
            Material m;
            if (src != null) { m = new Material(src); m.mainTextureScale = tiles; }
            else
            {
                m = new Material(Blindside.Rendering.ToonShader.Lit);
                m.SetColor("_BaseColor", fallback);
                m.SetFloat("_Smoothness", .15f);
            }
            ownMaterials.Add(m);
            return m;
        }

        Material Flat(Color c, bool unlit)
        {
            var m = new Material(Shader.Find(unlit ? "Universal Render Pipeline/Unlit" : "Universal Render Pipeline/Lit"));
            m.SetColor("_BaseColor", c);
            if (!unlit) m.SetFloat("_Smoothness", .1f);
            ownMaterials.Add(m);
            return m;
        }

        // ================================================================
        //  Effects (unscaled time)
        // ================================================================
        static Material fxTemplate;

        static Material FxMaterial(Color c)
        {
            if (fxTemplate == null)
            {
                fxTemplate = new Material(Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color"));
                fxTemplate.SetFloat("_Surface", 1f);
                fxTemplate.SetFloat("_Blend", 0f);
                fxTemplate.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                fxTemplate.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                fxTemplate.SetFloat("_ZWrite", 0f);
                fxTemplate.SetOverrideTag("RenderType", "Transparent");
                fxTemplate.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                fxTemplate.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            }
            var m = new Material(fxTemplate);
            m.SetColor("_BaseColor", c); m.SetColor("_Color", c);
            return m;
        }

        GameObject Primitive(PrimitiveType type, Color c, Vector3 pos, Vector3 scale)
        {
            var go = GameObject.CreatePrimitive(type);
            Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(transform, true);
            go.transform.position = pos;
            go.transform.localScale = scale;
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = FxMaterial(c);
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go;
        }

        void Beam(Vector3 a, Vector3 b, Color c, float width, float seconds)
        {
            var d = b - a;
            var go = Primitive(PrimitiveType.Cube, c, (a + b) * .5f, new Vector3(width, width, d.magnitude));
            go.transform.rotation = Quaternion.LookRotation(d.sqrMagnitude > 1e-6f ? d : Vector3.right, Vector3.up);
            fx.Add(new Fx(go, seconds, Fx.Kind.Fade));
        }

        void Blob(Vector3 at, Color c, float size, float seconds)
        {
            var go = Primitive(PrimitiveType.Sphere, c, at, Vector3.one * size);
            fx.Add(new Fx(go, seconds, Fx.Kind.Pop));
        }

        Vector3 RandomDir(float upBias)
        {
            var v = new Vector3((float)(rng.NextDouble() * 2 - 1), (float)rng.NextDouble() * upBias, (float)(rng.NextDouble() * 2 - 1));
            return v.sqrMagnitude > 1e-4f ? v.normalized : Vector3.up;
        }

        void Sparks(Vector3 at, Color c, int n, float speed)
        {
            for (int i = 0; i < n; i++)
            {
                var go = Primitive(PrimitiveType.Cube, c, at, Vector3.one * (.02f + (float)rng.NextDouble() * .025f));
                fx.Add(new Fx(go, .22f + (float)rng.NextDouble() * .18f, Fx.Kind.Part) { velocity = RandomDir(1.4f) * speed * (.5f + (float)rng.NextDouble() * .7f), gravity = 7f });
            }
        }

        void Chips(Vector3 at, int n)
        {
            for (int i = 0; i < n; i++)
            {
                var go = Primitive(PrimitiveType.Cube, new Color(.55f, .55f, .52f, 1f), at, Vector3.one * (.03f + (float)rng.NextDouble() * .04f));
                fx.Add(new Fx(go, .5f + (float)rng.NextDouble() * .3f, Fx.Kind.Part) { velocity = RandomDir(2f) * (1.6f + (float)rng.NextDouble() * 1.4f), gravity = 9f });
            }
        }

        void Dust(Vector3 at, int n, float size)
        {
            for (int i = 0; i < n; i++)
            {
                var p = at + new Vector3((float)(rng.NextDouble() - .5) * .3f, (float)rng.NextDouble() * .15f, (float)(rng.NextDouble() - .5) * .3f);
                var go = Primitive(PrimitiveType.Sphere, new Color(.62f, .6f, .56f, .45f), p, Vector3.one * size * (.6f + (float)rng.NextDouble() * .5f));
                fx.Add(new Fx(go, .55f + (float)rng.NextDouble() * .3f, Fx.Kind.Puff) { velocity = new Vector3(0f, .35f, 0f) });
            }
        }

        void Casing(BattleActor s)
        {
            var at = s.Muzzle + new Vector3(-s.Dir * .35f, -.02f, .05f);
            var go = Primitive(PrimitiveType.Cube, new Color(.86f, .66f, .25f, 1f), at, new Vector3(.025f, .025f, .06f));
            var v = new Vector3(-s.Dir * (.4f + (float)rng.NextDouble() * .6f), 1.6f + (float)rng.NextDouble() * .8f, -.8f - (float)rng.NextDouble() * .7f);
            fx.Add(new Fx(go, .7f, Fx.Kind.Part) { velocity = v, gravity = 9.8f, spin = 900f });
        }

        void FlashLight(Vector3 at, Color c, float intensity, float range, float seconds)
        {
            var go = new GameObject("MuzzleLight");
            go.transform.SetParent(transform, true);
            go.transform.position = at;
            var l = go.AddComponent<Light>();
            l.type = LightType.Point; l.color = c; l.intensity = intensity; l.range = range; l.shadows = LightShadows.None;
            fx.Add(new Fx(go, seconds, Fx.Kind.Light) { light = l, lightIntensity = intensity });
        }

        void Popup(BattleActor over, string text, Color c, float size)
        {
            var go = new GameObject("Popup");
            go.transform.SetParent(transform, true);
            // Several hits in a row stack upward instead of drawing over each other.
            float now = Time.unscaledTime;
            over.popupStack = now - over.lastPopup < .5f ? over.popupStack + 1 : 0;
            over.lastPopup = now;
            go.transform.position = over.Head + Vector3.up * (.28f + over.popupStack * .2f) + new Vector3(0f, 0f, over.popupStack % 2 == 0 ? 0f : -.15f);
            var t = go.AddComponent<TextMeshPro>();
            t.text = text;
            UIFonts.Auto(t, true);
            t.fontSize = size;
            t.color = c;
            t.alignment = TextAlignmentOptions.Center;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.outlineWidth = .22f;
            t.outlineColor = new Color32(0x10, 0x14, 0x1a, 0xff);
            t.rectTransform.sizeDelta = new Vector2(4f, 2f);
            fx.Add(new Fx(go, .85f, Fx.Kind.Rise) { text = t });
        }

        sealed class Fx
        {
            public enum Kind { Fade, Pop, Rise, Part, Puff, Light }
            public readonly GameObject go;
            readonly float t0, dur;
            readonly Kind kind;
            readonly Vector3 p0, s0;
            readonly Material mat;
            readonly Color c0;
            public TMP_Text text;
            public Vector3 velocity;
            public float gravity, spin;
            public Light light;
            public float lightIntensity;

            public Fx(GameObject go, float dur, Kind kind)
            {
                this.go = go; this.dur = dur; this.kind = kind;
                t0 = Time.unscaledTime;
                p0 = go.transform.position; s0 = go.transform.localScale;
                // Effects own their material; a popup's renderer carries the TMP font material — never touched.
                var r = kind == Kind.Rise || kind == Kind.Light ? null : go.GetComponent<MeshRenderer>();
                if (r != null) { mat = r.sharedMaterial; c0 = mat.GetColor("_BaseColor"); }
            }

            public void Release() { if (mat != null) Destroy(mat); }

            public bool Tick(float now, float dt, Quaternion camRot)
            {
                if (go == null) return false;
                float p = (now - t0) / dur;
                if (p >= 1f) { Release(); return false; }
                var tr = go.transform;
                switch (kind)
                {
                    case Kind.Fade: SetAlpha(1f - p); break;
                    case Kind.Pop: tr.localScale = s0 * (1f + p * .8f); SetAlpha(1f - p); break;
                    case Kind.Puff:
                        tr.localScale = s0 * (1f + p * 1.6f);
                        tr.position += velocity * dt;
                        SetAlpha(1f - p);
                        break;
                    case Kind.Part:
                    {
                        var pos = tr.position + velocity * dt;
                        velocity.y -= gravity * dt;
                        if (pos.y < .01f) { pos.y = .01f; velocity *= .35f; velocity.y = Mathf.Abs(velocity.y) * .3f; }
                        tr.position = pos;
                        if (spin != 0f) tr.Rotate(spin * dt, spin * .6f * dt, 0f, Space.Self);
                        SetAlpha(p < .7f ? 1f : 1f - (p - .7f) / .3f);
                        break;
                    }
                    case Kind.Light:
                        if (light != null) light.intensity = lightIntensity * (1f - p);
                        break;
                    case Kind.Rise:
                    {
                        float e = 1f - (1f - p) * (1f - p);
                        tr.position = p0 + Vector3.up * (.22f * e);
                        tr.rotation = camRot;
                        float k = Mathf.Clamp01(p / .12f);
                        tr.localScale = Vector3.one * Mathf.Lerp(1.5f, 1f, k);
                        if (text != null) text.alpha = Mathf.Clamp01((1f - p) / .3f);
                        break;
                    }
                }
                return true;
            }

            void SetAlpha(float a)
            {
                if (mat == null) return;
                var c = c0; c.a *= Mathf.Clamp01(a);
                mat.SetColor("_BaseColor", c); mat.SetColor("_Color", c);
            }
        }

        // ================================================================
        //  Actor: a script-less copy of a unit's figure, posed on the stage
        // ================================================================
        sealed class BattleActor
        {
            public ICombatUnit Unit { get; private set; }
            public float Dir { get; private set; }   // +1 faces +x (stands on the left), -1 faces -x
            public Vector3 Home => home;
            public bool Dead => deathT0 >= 0f;
            public float lastPopup = -9f;
            public int popupStack;
            Transform root;
            Vector3 home, torsoL, headL, labelL, muzzleL;
            Material[] mats; Color[] baseColors;
            float step, stepTarget, recoil, knock, flashT = -1f, flashCrit, deathT0 = -1f, bobPhase;

            public Vector3 Torso => root.TransformPoint(torsoL);
            public Vector3 Head => root.TransformPoint(headL);
            public Vector3 Label => root.TransformPoint(labelL);
            // the copied gun's own muzzle (it moves with the hands); a figure without a gun: the spot captured at the copy
            Transform muzzleT;
            public Vector3 Muzzle => muzzleT != null ? muzzleT.position : root.TransformPoint(muzzleL);

            public static BattleActor Create(ICombatUnit u, Transform parent, Vector3 home, float dir)
            {
                var view = u.View;
                var src = ((Component)view).transform;
                var a = new BattleActor { Unit = u, Dir = dir, home = home, bobPhase = dir > 0 ? 0f : 1.7f };
                a.torsoL = src.InverseTransformPoint(view.TorsoPoint);
                a.headL = src.InverseTransformPoint(view.HeadPoint);
                a.labelL = src.InverseTransformPoint(view.LabelPoint);
                var facingDir = GridSpace.FacingToWorldDirection(u.Facing);
                var muzzle = view is UnitView uv ? uv.MuzzlePoint : view.TorsoPoint + facingDir * .55f;
                a.muzzleL = src.InverseTransformPoint(muzzle);

                // Instantiate under an inactive holder so no Awake runs, then strip every script and collider.
                var hold = new GameObject("BattleStage_Hold");
                hold.SetActive(false);
                var clone = UnityEngine.Object.Instantiate(src.gameObject, hold.transform);
                var scripts = clone.GetComponentsInChildren<MonoBehaviour>(true);
                for (int i = scripts.Length - 1; i >= 0; i--)
                    try { UnityEngine.Object.DestroyImmediate(scripts[i]); } catch (Exception ex) { Debug.LogException(ex); }
                foreach (var c in clone.GetComponentsInChildren<Collider>(true)) UnityEngine.Object.DestroyImmediate(c);
                foreach (var t in clone.GetComponentsInChildren<Transform>(true))
                    if (t != null && t != clone.transform && (t.name == "HealthBar" || t.name == "FacingIndicator" || t.name == "Ring" || t.name == "SelectionRing"))
                        UnityEngine.Object.DestroyImmediate(t.gameObject);
                clone.SetActive(true);
                clone.name = "Actor_" + (u is Blindside.Movement.IOperatorNamed n ? n.Kr : "Unit");
                a.root = clone.transform;
                a.root.SetParent(parent, true);
                UnityEngine.Object.DestroyImmediate(hold);

                // Own material copies at full opacity (the field figure may be dimmed, hidden from the player or fading out).
                var list = new List<Material>(); var cols = new List<Color>();
                foreach (var r in clone.GetComponentsInChildren<Renderer>(true))
                {
                    r.forceRenderingOff = false;
                    var shared = r.sharedMaterials;
                    for (int i = 0; i < shared.Length; i++)
                    {
                        if (shared[i] == null) continue;
                        var m = new Material(shared[i]);
                        var c = m.HasProperty("_BaseColor") ? m.GetColor("_BaseColor") : m.HasProperty("_Color") ? m.GetColor("_Color") : Color.white;
                        c.a = 1f;
                        SetColor(m, c);
                        shared[i] = m; list.Add(m); cols.Add(c);
                    }
                    r.sharedMaterials = shared;
                }
                a.mats = list.ToArray(); a.baseColors = cols.ToArray();

                // Yaw only: FromToRotation picks an arbitrary axis for opposite vectors and could flip the figure under the floor.
                float yaw = Vector3.SignedAngle(new Vector3(facingDir.x, 0f, facingDir.z), dir > 0 ? Vector3.right : Vector3.left, Vector3.up);
                a.root.rotation = Quaternion.AngleAxis(yaw, Vector3.up) * src.rotation;
                a.root.position = home;

                // A model with clips: the copy gets its own animator (scripts were stripped), aiming down the street.
                if (view is UnitView sv && sv.UnitAnimator != null && sv.ModelRoot != null)
                {
                    var bodyPath = RelativePath(src, sv.ModelRoot);
                    var body = bodyPath == null ? a.root : a.root.Find(bodyPath);
                    var anim = body != null ? body.GetComponentInChildren<Animator>(true) : null;
                    if (anim != null)
                    {
                        a.anim = clone.AddComponent<UnitAnimator>();
                        a.anim.Init(anim, sv.UnitAnimator.Profile, body);
                        a.anim.CopyFootAxes(sv.UnitAnimator);
                        a.anim.ForceAim = true;
                        a.anim.AimYaw = dir > 0 ? 90f : 270f;
                        // the copied gun: hands on it, raised to the eye like the field figure's
                        if (sv.GunObjectName != null)
                        {
                            var gun = UnitAnimator.FindDeep(body, sv.GunObjectName);
                            a.anim.Gun = GunRig.Bind(anim, gun, sv.GunSetup);
                            if (gun != null) a.muzzleT = gun.Find("Muzzle");
                        }
                    }
                }
                return a;
            }

            UnitAnimator anim;
            bool AnimDeath => anim != null && anim.Profile != null && anim.Profile.death != null;

            static string RelativePath(Transform root, Transform t)
            {
                if (t == root) return null;
                var parts = new List<string>();
                for (var c = t; c != null && c != root; c = c.parent) parts.Insert(0, c.name);
                return string.Join("/", parts);
            }

            public void PlayFire() { if (anim != null) anim.PlayFire(); }

            public void Advance(bool on) => stepTarget = on ? .4f : 0f;
            public void Recoil() => recoil = .06f;

            /// <summary>The figure's height over the old 1 m capsule's (head at .8), at least 1.</summary>
            public float Scale => Mathf.Max(1f, headL.y / .8f);

            public void PlayDodge(bool blocked) { if (anim != null) anim.PlayDodge(blocked); }
            public void Flinch(float amount, bool flash)
            {
                knock = Mathf.Max(knock, amount);
                if (flash) { flashT = Time.unscaledTime; flashCrit = amount > .15f ? 1f : 0f; if (anim != null) anim.PlayHit(); }
            }
            public void Die()
            {
                if (deathT0 >= 0f) return;
                deathT0 = Time.unscaledTime;
                if (anim != null) anim.PlayDeath();
            }

            public void Tick(float now, float dt)
            {
                if (root == null) return;
                step = Mathf.MoveTowards(step, stepTarget, dt * 3.2f);
                recoil = Mathf.MoveTowards(recoil, 0f, dt * .9f);
                knock = Mathf.MoveTowards(knock, 0f, dt * 1.1f);
                float x = Dir * (step - recoil - knock);
                float bob = deathT0 < 0f ? Mathf.Sin(now * 2.4f + bobPhase) * .012f : 0f;
                float shake = knock > .01f ? Mathf.Sin(now * 90f) * knock * .25f : 0f;
                root.position = home + new Vector3(x + shake, bob, 0f);

                float white = 0f;
                if (flashT >= 0f)
                {
                    float p = (now - flashT) / .22f;
                    if (p >= 1f) flashT = -1f; else white = 1f - p;
                }
                if (deathT0 >= 0f && !AnimDeath)   // with a death clip the model falls by itself
                {
                    // falls back, away from the opponent, and stays down in the street
                    float p = (now - deathT0) / .5f;
                    float e = 1f - Mathf.Pow(1f - Mathf.Clamp01(p), 3f);
                    root.rotation = Quaternion.AngleAxis(Dir * 82f * e, Vector3.forward) * RestRotation();
                    root.position = home + new Vector3(x, 0f, 0f);
                }
                for (int i = 0; i < mats.Length; i++)
                {
                    var tint = flashCrit > 0f ? new Color(1f, .35f, .35f, 1f) : Color.white;
                    SetColor(mats[i], Color.Lerp(baseColors[i], tint, white * .85f));
                }
            }

            Quaternion rest;
            bool restSet;
            Quaternion RestRotation()
            {
                if (!restSet) { rest = root.rotation; restSet = true; }
                return rest;
            }

            public void DestroyMaterials()
            {
                if (mats == null) return;
                foreach (var m in mats) if (m != null) UnityEngine.Object.Destroy(m);
            }

            static void SetColor(Material m, Color c)
            {
                if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
                if (m.HasProperty("_Color")) m.SetColor("_Color", c);
            }
        }
    }
}
