// Ported from the ABILITIES block in blindside_source/04_gameplay_combat_ai.js:
// breakPanel, abilitySpec, spendAbility, useAbility, refreshGas (~line 214-252).
// Same split as CombatSystem/EncounterResolver: this file has no UI/render dependency. Each IAbility
// only mutates game state (MapGrid, gadget lists, unit HP/flags) and raises events; fx/sfx/log/HUD/
// win-check stay with the caller (mirrors the source's boom/sfx/log/popup/updateHUD/checkWin calls).
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Blindside.Map;
using Blindside.Vision;
using Blindside.Drones;
using Blindside.Combat;

namespace Blindside.Abilities
{
    /// <summary>Read/write view of whatever unit is using an ability. Extends combat's unit view.</summary>
    public interface IAbilityUnit : ICombatUnit
    {
        string Id { get; }
        string AbilityKey { get; }     // hammer, launcher, thermite, stim, shield, ghost, shock, gas, mirror, cam
        int MaxHP { get; }
        int Mv { get; }                 // base speed, for moveRange = mv*3+1 (stim)
        int Charges { get; set; }       // 99 is the "unlimited" sentinel (BASTION's shield)
        bool AbilityUsed { get; set; }
        float MoveLeft { get; set; }
        bool Stim { get; set; }         // set true by the stim ability on its target
        bool Ghost { get; set; }        // set true by the ghost ability on self
        bool Shield { get; set; }       // set true by the shield ability; backs ICombatUnit.ShieldUp (read-only there)
    }

    public enum AbilityTargetKind { Panel, Ally, Instant, Point }

    /// <summary>
    /// Port of one case's return value in abilitySpec(u): what kind of target the ability needs and
    /// how to validate a candidate. Only the field matching Kind is populated.
    /// </summary>
    public readonly struct AbilitySpec
    {
        public readonly AbilityTargetKind Kind;
        public readonly float Range;    // 0 when not applicable (instant abilities)
        public readonly float Radius;   // 0 when not applicable (panel/ally/instant abilities)
        public readonly string Hint;    // e.g. "破壊する壁を選択してください"
        public readonly string Sub;     // e.g. "隣接する通常の壁のみ可能"
        public readonly System.Func<WallPanel, bool> ValidPanel;
        public readonly System.Func<IAbilityUnit, bool> ValidAlly;
        public readonly System.Func<float, float, bool> ValidPoint; // (x,y)

        public AbilitySpec(AbilityTargetKind kind, string hint, string sub, float range = 0f, float radius = 0f,
            System.Func<WallPanel, bool> validPanel = null, System.Func<IAbilityUnit, bool> validAlly = null,
            System.Func<float, float, bool> validPoint = null)
        {
            Kind = kind; Hint = hint; Sub = sub; Range = range; Radius = radius;
            ValidPanel = validPanel; ValidAlly = validAlly; ValidPoint = validPoint;
        }
    }

    /// <summary>Tagged-union target passed to IAbility.Use, matching whichever AbilitySpec.Kind was offered.</summary>
    public readonly struct AbilityTarget
    {
        public readonly AbilityTargetKind Kind;
        public readonly WallPanel Panel;
        public readonly IAbilityUnit Ally;
        public readonly float X, Y;

        AbilityTarget(AbilityTargetKind kind, WallPanel panel, IAbilityUnit ally, float x, float y)
        {
            Kind = kind; Panel = panel; Ally = ally; X = x; Y = y;
        }

        public static AbilityTarget FromPanel(WallPanel p) => new AbilityTarget(AbilityTargetKind.Panel, p, null, 0, 0);
        public static AbilityTarget FromAlly(IAbilityUnit a) => new AbilityTarget(AbilityTargetKind.Ally, default, a, 0, 0);
        public static AbilityTarget FromPoint(float x, float y) => new AbilityTarget(AbilityTargetKind.Point, default, null, x, y);
        public static readonly AbilityTarget Instant = new AbilityTarget(AbilityTargetKind.Instant, default, null, 0, 0);
    }

    /// <summary>A single active gas cloud. Port of an entry in G.gases.</summary>
    public class GasCloud { public float x, y, r; public float ttl; }

    /// <summary>A single active shock-wire field. Port of an entry in G.electrics.</summary>
    public class ElectricField { public float x, y, r; public bool discovered; public int disabledLeft; }   // disabledLeft > 0: knocked out (SILENCE)

    /// <summary>A placed or fixed surveillance camera. Port of an entry in G.cams.</summary>
    public class SurveillanceCamera
    {
        public float x, y; public bool alive = true; public bool discovered; public bool placed = true;   // placed:false = the map's fixed cams
        /// <summary>Phases it stays knocked out (SILENCE's ホロスキャン): it sees nothing while > 0.</summary>
        public int disabledLeft;
        public bool Working => alive && disabledLeft <= 0;
    }

    /// <summary>
    /// Everything an IAbility needs besides the acting unit and target: the map/vision systems it
    /// mutates or queries, the gadget lists it may add to, and the drone list the shock ability
    /// reaches into (kept in its own DroneController per the split requested for drone logic).
    /// </summary>
    public class AbilityContext
    {
        public MapGrid Map;
        public VisionSystem Vision;
        public DroneController Drones;
        public List<GasCloud> Gases = new List<GasCloud>();
        public List<ElectricField> Electrics = new List<ElectricField>();
        public List<SurveillanceCamera> Cameras = new List<SurveillanceCamera>();
        public IReadOnlyList<IAbilityUnit> Units;   // all units, both teams, incl. hostage-side escort checks elsewhere
        public bool PrepPhase;                      // G.phase==='prep': ability use doesn't cost the unit's turn
        public AbilitySystem System;                // set by AbilitySystem before dispatch, for raising events
    }

    /// <summary>One ability's targeting spec + effect. Implementations live in BreachAbilities/SupportAbilities/GadgetAbilities.</summary>
    public interface IAbility
    {
        string Key { get; }
        AbilitySpec GetSpec(IAbilityUnit u, AbilityContext ctx);
        IEnumerator Use(IAbilityUnit u, AbilityTarget target, AbilityContext ctx);
    }

    public class AbilitySystem
    {
        readonly Dictionary<string, IAbility> registry = new Dictionary<string, IAbility>();

        public AbilitySystem(IEnumerable<IAbility> abilities)
        {
            foreach (var a in abilities) registry[a.Key] = a;
        }

        /// <summary>Default 10-ability roster: the 3 breachers, heal, shield, jam, electric, gas, mirror, camera.</summary>
        public static AbilitySystem CreateDefault() => new AbilitySystem(new IAbility[]
        {
            new HammerAbility(), new LauncherAbility(), new ThermiteAbility(),
            new StimAbility(), new ShieldAbility(), new GhostAbility(),
            new ShockAbility(), new GasAbility(), new MirrorAbility(), new CameraAbility(),
            new HeartbeatAbility(), new HoloScanAbility(),   // GHOST / SILENCE (ReconAbilities.cs)
        });

        // ---- geometry helpers shared by GetSpec implementations (ported from 01_core_data_map_vision.js) ----

        static int JsRound(float v) => Mathf.FloorToInt(v + 0.5f);

        public static float Dist(IAbilityUnit a, IAbilityUnit b)
        {
            float dx = a.X - b.X, dy = a.Y - b.Y;
            return Mathf.Sqrt(dx * dx + dy * dy);
        }

        static bool InRange(IAbilityUnit u, float x, float y, float r)
        {
            float dx = x - u.X, dy = y - u.Y;
            return dx * dx + dy * dy <= r * r;
        }

        /// <summary>Port of distToCell(px,py,cx,cy): distance from a point to the nearest edge of cell (cx,cy).</summary>
        static float DistToCell(float px, float py, int cx, int cy)
        {
            float qx = Mathf.Clamp(px, cx - 0.5f, cx + 0.5f);
            float qy = Mathf.Clamp(py, cy - 0.5f, cy + 0.5f);
            return Mathf.Sqrt((px - qx) * (px - qx) + (py - qy) * (py - qy));
        }

        /// <summary>Port of panelNear(p,u,r=.85): any cell of the panel within r of the unit.</summary>
        public static bool PanelNear(WallPanel p, IAbilityUnit u, float r = 0.85f)
        {
            foreach (var c in p.cells)
                if (DistToCell(u.X, u.Y, c.x, c.y) <= r) return true;
            return false;
        }

        /// <summary>Port of throwClear(u,p): any cell within 6.5m with LOS to the unit, ignoring the panel's own cells.</summary>
        public static bool ThrowClear(IAbilityUnit u, WallPanel p, AbilityContext ctx)
        {
            var except = new HashSet<int>();
            foreach (var c in p.cells) except.Add(c.y * MapGrid.Width + c.x);
            foreach (var c in p.cells)
            {
                float dx = c.x - u.X, dy = c.y - u.Y;
                if (Mathf.Sqrt(dx * dx + dy * dy) <= 6.5f &&
                    ctx.Vision.LineOfSight(u.X, u.Y, c.x, c.y, u.Team, exceptCells: except))
                    return true;
            }
            return false;
        }

        /// <summary>Port of blockCell(x,y,isDrone): whether the tile itself blocks standing/throwing there.</summary>
        static bool BlockCell(MapGrid map, int x, int y, bool isDrone)
        {
            var t = map.TileAt(x, y);
            if (t == TileType.Floor || t == TileType.Out || t == TileType.Rubble || t == TileType.Extract) return false;
            if (isDrone && t == TileType.Hole) return false;
            return true;
        }

        /// <summary>Port of freeAt(px,py,isDrone,r): no blocking cell's footprint comes within r of the point.</summary>
        public static bool FreeAt(AbilityContext ctx, float px, float py, bool isDrone, float r)
        {
            int y0 = JsRound(py - r), y1 = JsRound(py + r);
            int x0 = JsRound(px - r), x1 = JsRound(px + r);
            for (int cy = y0; cy <= y1; cy++)
                for (int cx = x0; cx <= x1; cx++)
                {
                    if (!BlockCell(ctx.Map, cx, cy, isDrone)) continue;
                    float qx = Mathf.Clamp(px, cx - 0.5f, cx + 0.5f);
                    float qy = Mathf.Clamp(py, cy - 0.5f, cy + 0.5f);
                    if ((px - qx) * (px - qx) + (py - qy) * (py - qy) < r * r) return false;
                }
            return true;
        }

        /// <summary>Port of the `inR` closure captured in abilitySpec(u): point within range of the unit.</summary>
        public static bool InRangeOfUnit(IAbilityUnit u, float x, float y, float r) => InRange(u, x, y, r);

        // ---- dispatch ----

        public AbilitySpec GetSpec(IAbilityUnit u, AbilityContext ctx)
        {
            ctx.System = this;
            return registry[u.AbilityKey].GetSpec(u, ctx);
        }

        /// <summary>Port of spendAbility(u): charges tick down (99 = unlimited), and using it outside
        /// prep phase burns the unit's ability-use-this-turn flag.</summary>
        public void SpendCharge(IAbilityUnit u, AbilityContext ctx)
        {
            if (u.Charges < 99) u.Charges--;
            if (!ctx.PrepPhase) u.AbilityUsed = true;
        }

        public event System.Action<IAbilityUnit, AbilityTarget> OnUsed;

        /// <summary>Port of useAbility(u,target) + its trailing spendAbility/updateHUD/checkWin.
        /// Caller should refresh HUD and check win conditions after this returns (subscribe OnUsed, or
        /// simply do so after the coroutine completes).</summary>
        public IEnumerator Use(IAbilityUnit u, AbilityTarget target, AbilityContext ctx)
        {
            ctx.System = this;
            u.View?.PlayAbility(u.AbilityKey);   // animation only
            yield return registry[u.AbilityKey].Use(u, target, ctx);
            SpendCharge(u, ctx);
            OnUsed?.Invoke(u, target);
        }

        // ---- presentation events, one per ability (fx/sfx/log/addMark/popup are the caller's job) ----

        public event System.Action<IAbilityUnit, WallPanel> OnBreachSwing;                    // per-hit beat (hammer/thermite)
        public event System.Action<IAbilityUnit, WallPanel, TileType, bool> OnBreachComplete;  // unit, panel, newState, wasReinforced
        public event System.Action<IAbilityUnit, IAbilityUnit, int, float> OnHeal;             // healer, target, hpAdded, moveAdded
        public event System.Action<IAbilityUnit> OnShieldUp;
        public event System.Action<IAbilityUnit, bool> OnGhostToggle;                          // unit, nowJamming
        public event System.Action<IAbilityUnit, ElectricField, int, List<Drone>> OnShockPlaced; // unit, field, panelsElectrified, dronesKilled
        public event System.Action<IAbilityUnit, float, float> OnGasLaunched;                 // unit, x, y — the arc starts
        public event System.Action<IAbilityUnit, GasCloud> OnGasThrown;
        public event System.Action<IAbilityUnit, IAbilityUnit, int, bool> OnGasDamage;         // gas source, victim, damage, killed
        public event System.Action<IAbilityUnit, WallPanel> OnMirrorPlaced;
        public event System.Action<IAbilityUnit, SurveillanceCamera> OnCameraPlaced;
        public event System.Action<IAbilityUnit, List<IAbilityUnit>> OnHeartbeat;                          // GHOST, the enemies sensed
        public event System.Action<IAbilityUnit, float, float, float> OnBlast;                               // IGNIS's co-op thermite blast: unit, x, y, radius
        public event System.Action<IAbilityUnit, List<SurveillanceCamera>, List<ElectricField>> OnHoloScan;  // SILENCE, what it knocked out

        internal void RaiseBreachSwing(IAbilityUnit u, WallPanel p) => OnBreachSwing?.Invoke(u, p);
        internal void RaiseBreachComplete(IAbilityUnit u, WallPanel p, TileType s, bool wasReinforced) => OnBreachComplete?.Invoke(u, p, s, wasReinforced);
        internal void RaiseHeal(IAbilityUnit healer, IAbilityUnit target, int hp, float move) => OnHeal?.Invoke(healer, target, hp, move);
        internal void RaiseShieldUp(IAbilityUnit u) => OnShieldUp?.Invoke(u);
        internal void RaiseGhostToggle(IAbilityUnit u, bool jamming) => OnGhostToggle?.Invoke(u, jamming);
        internal void RaiseShockPlaced(IAbilityUnit u, ElectricField f, int panels, List<Drone> killed) => OnShockPlaced?.Invoke(u, f, panels, killed);
        internal void RaiseGasLaunched(IAbilityUnit u, float x, float y) => OnGasLaunched?.Invoke(u, x, y);
        internal void RaiseGasThrown(IAbilityUnit u, GasCloud g) => OnGasThrown?.Invoke(u, g);
        internal void RaiseGasDamage(IAbilityUnit u, IAbilityUnit victim, int dmg, bool killed) => OnGasDamage?.Invoke(u, victim, dmg, killed);
        internal void RaiseMirrorPlaced(IAbilityUnit u, WallPanel p) => OnMirrorPlaced?.Invoke(u, p);
        internal void RaiseCameraPlaced(IAbilityUnit u, SurveillanceCamera c) => OnCameraPlaced?.Invoke(u, c);
        internal void RaiseHeartbeat(IAbilityUnit u, List<IAbilityUnit> found) => OnHeartbeat?.Invoke(u, found);
        internal void RaiseBlast(IAbilityUnit u, float x, float y, float r) => OnBlast?.Invoke(u, x, y, r);
        internal void RaiseHoloScan(IAbilityUnit u, List<SurveillanceCamera> cams, List<ElectricField> fields) => OnHoloScan?.Invoke(u, cams, fields);
    }
}
