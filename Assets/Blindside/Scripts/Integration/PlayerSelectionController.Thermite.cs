// IGNIS's 発熱爆薬 for the player, in two presses: the ability on a wall plants the charge (free — no charge, no turn spent; the
// charge sits on the wall's face on IGNIS's side, its lamp blinking), and the ability again sets it off from wherever IGNIS is
// (AbilityContext.Remote: no turn to the wall, no ability clip) — that is the use that spends the charge and the turn's ability.
// A planted charge is lost if its wall goes (broken, holed by someone else) or carries current, or IGNIS falls. The AI's thermite
// (EnemyAI → AbilitySystem.Use) is unchanged: it still burns through at once.
// The lamp blinks on unscaled time; nothing is allocated per frame (one reused removal list).
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Blindside.Abilities;
using Blindside.Map;
using Blindside.Rendering;
using Blindside.Turns;

namespace Blindside.Integration
{
    public partial class PlayerSelectionController
    {
        public const string ThermiteKey = "thermite";
        public const float ThermiteAttachTime = .45f, ThermiteBlink = .5f;

        class PlantedCharge
        {
            public int panel;
            public GameObject root;
            public Renderer lamp;
        }

        readonly Dictionary<SimUnit, PlantedCharge> planted = new Dictionary<SimUnit, PlantedCharge>();
        readonly List<SimUnit> plantedGone = new List<SimUnit>();
        static Material chargeBodyMat, chargeLampMat;

        /// <summary>IGNIS has a charge on a wall (the command menu shows 起爆).</summary>
        public bool HasPlantedCharge(SimUnit u) => u != null && planted.ContainsKey(u);

        bool ChargeStillGood(PlantedCharge c, SimUnit u)
        {
            if (u == null || !u.Alive || map == null || c.panel < 0 || c.panel >= map.Panels.Count) return false;
            var p = map.Panels[c.panel];
            var t = map.TileAt(p.cells[0].x, p.cells[0].y);
            return (t == TileType.Wall || t == TileType.Hole) && !p.electrified;
        }

        /// <summary>The ability pressed with a charge planted: set it off (true), or — the charge lost meanwhile — drop it and say so
        /// (false: the ability starts over, to plant a new one).</summary>
        bool TryDetonate(SimUnit u)
        {
            if (u.AbilityKey != ThermiteKey || !planted.TryGetValue(u, out var c)) return false;
            if (!ChargeStillGood(c, u))
            {
                DropCharge(u);
                Log($"{KrOf(u)}: 設置した発熱爆薬が無効になりました", "w");
                return false;
            }
            var p = map.Panels[c.panel];
            DropCharge(u);
            Blindside.UI.SoundBank.Play(Blindside.UI.Snd.BreachBeep);   // IGNIS presses the detonator
            HideMenu(); HideHint();
            busy = true;
            mode = Mode.Busy; uiUnit = u;
            StartCoroutine(DetonateRoutine(u, p));
            return true;
        }

        IEnumerator DetonateRoutine(SimUnit u, WallPanel p)
        {
            abilityCtx.Remote = true;
            try { yield return AbilityRoutine(u, AbilityTarget.FromPanel(p)); }
            finally { abilityCtx.Remote = false; }
        }

        /// <summary>The first press on a wall: IGNIS turns to it, sticks the charge on (one ring and knock), and is free again.</summary>
        IEnumerator AttachRoutine(SimUnit u, WallPanel p)
        {
            u.Facing = Mathf.Atan2(p.centerY - u.Y, p.centerX - u.X);
            u.View?.PlayAbility(ThermiteKey);
            abilities.RaiseBreachSwing(u, p);
            Blindside.UI.SoundBank.Play(Blindside.UI.Snd.BreachPlace);   // the charge clicked onto the wall
            float t = 0f;
            while (t < ThermiteAttachTime) { t += Time.unscaledDeltaTime; yield return null; }
            DropCharge(u);
            var root = BuildCharge(p, u, out var lamp);
            planted[u] = new PlantedCharge { panel = p.id, root = root, lamp = lamp };
            Log($"{KrOf(u)}: 発熱爆薬を設置 — もう一度「{AbilityName(u)}」で起爆", "a");
            busy = false;
            if (turns == null || turns.Over) yield break;
            AfterAction(u);
        }

        static string AbilityName(SimUnit u) => u.Op != null && !string.IsNullOrEmpty(u.Op.abilityName) ? u.Op.abilityName : "能力";

        void DropCharge(SimUnit u)
        {
            if (!planted.TryGetValue(u, out var c)) return;
            if (c.root != null) Destroy(c.root);
            planted.Remove(u);
        }

        /// <summary>A new match: no charges carried over.</summary>
        void ClearCharges()
        {
            foreach (var c in planted.Values) if (c.root != null) Destroy(c.root);
            planted.Clear();
        }

        /// <summary>Every frame (SyncAbilityHelpers): the lamps blink; a charge whose wall or owner is gone goes with it.</summary>
        void SyncCharges()
        {
            if (planted.Count == 0) return;
            bool on = Mathf.Repeat(Time.unscaledTime, ThermiteBlink * 2f) < ThermiteBlink;
            plantedGone.Clear();
            foreach (var kv in planted)
            {
                if (!ChargeStillGood(kv.Value, kv.Key)) { plantedGone.Add(kv.Key); continue; }
                if (kv.Value.lamp != null && kv.Value.lamp.enabled != on) kv.Value.lamp.enabled = on;
            }
            for (int i = 0; i < plantedGone.Count; i++)
            {
                var u = plantedGone[i];
                DropCharge(u);
                if (u.Alive) Log($"{KrOf(u)}: 設置した発熱爆薬が失われました", "w");
            }
        }

        /// <summary>The charge on the wall's face toward `u`: a dark slab with an orange band and a blinking red lamp.</summary>
        /// <summary>How far in from the wall top's edge on IGNIS's side the charge's centre sits (half its depth and a little).</summary>
        const float ChargeIn = .3f;

        GameObject BuildCharge(WallPanel p, SimUnit u, out Renderer lamp)
        {
            if (chargeBodyMat == null)
            {
                var shader = ToonShader.Lit ?? Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                chargeBodyMat = new Material(shader) { name = "ThermiteCharge" };
                SetMatColor(chargeBodyMat, new Color(.16f, .17f, .18f));
                var unlit = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
                chargeLampMat = new Material(unlit) { name = "ThermiteLamp" };
                SetMatColor(chargeLampMat, new Color(1f, .18f, .12f));
            }
            // which side of the wall: the panel's long axis, and IGNIS's side of it
            bool alongX = p.cells.Count > 1 ? p.cells[0].y == p.cells[p.cells.Count - 1].y : Mathf.Abs(u.X - p.centerX) < Mathf.Abs(u.Y - p.centerY);
            Vector2 n = alongX ? new Vector2(0f, Mathf.Sign(u.Y - p.centerY)) : new Vector2(Mathf.Sign(u.X - p.centerX), 0f);
            if (n.sqrMagnitude < .5f) n = new Vector2(0f, 1f);
            // on the wall's top, face up, toward IGNIS's side, the lamp toward the far side: the top-down camera sees its face, band and
            // lamp (flat on the wall's side it was only an edge from above)
            var outward = (GridSpace.GridToWorld(n.x, n.y) - GridSpace.GridToWorld(0f, 0f)).normalized;
            Bounds wb = WallBoundsAt(p.cells[0]);
            for (int i = 1; i < p.cells.Count; i++) wb.Encapsulate(WallBoundsAt(p.cells[i]));
            var centre = GridSpace.GridToWorld(p.centerX, p.centerY, 0f);
            float half = Mathf.Abs(Vector3.Dot(wb.extents, outward));
            var face = new Vector3(centre.x, wb.max.y + .005f, centre.z) + outward * Mathf.Max(0f, half - ChargeIn);

            var root = new GameObject("ThermiteCharge_" + p.id);
            root.transform.SetParent(transform, false);
            root.transform.position = face;
            root.transform.rotation = Quaternion.LookRotation(Vector3.up, -outward);
            Part(root.transform, chargeBodyMat, new Vector3(0f, 0f, .03f), new Vector3(.62f, .5f, .06f));
            Part(root.transform, chargeLampMat, new Vector3(0f, -.12f, .065f), new Vector3(.56f, .05f, .012f)).GetComponent<Renderer>().sharedMaterial = OrangeBand();
            lamp = Part(root.transform, chargeLampMat, new Vector3(.2f, .16f, .07f), new Vector3(.07f, .07f, .03f)).GetComponent<Renderer>();
            return root;
        }

        static Material orangeBandMat;
        static Material OrangeBand()
        {
            if (orangeBandMat == null)
            {
                orangeBandMat = new Material(chargeLampMat) { name = "ThermiteBand" };
                SetMatColor(orangeBandMat, new Color(1f, .55f, .1f));
            }
            return orangeBandMat;
        }

        static GameObject Part(Transform parent, Material m, Vector3 local, Vector3 size)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = local;
            go.transform.localScale = size;
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = m;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go;
        }
        // (SetMatColor: .SupportAbility.cs)
    }
}
