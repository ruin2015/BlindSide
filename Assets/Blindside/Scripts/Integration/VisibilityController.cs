// The player-side vision rules on screen. Ports, from blindside_source:
//   03_renderer_threejs.js — syncVision()'s fog texture + out-of-sight wall dimming (~162-167) and
//                            syncUnits()' `m.g.visible = u.alive && u.placed!==false && shownToPlayer(u)` (~136)
//   01_core_data_map_vision.js — shownToPlayer(e) (~186):
//     if(e.team===G.side||e.team==='host') return true; return enemyVisible(G.side,e)||e.revealT>now();
//
// The fog and wall dimming are cell-based (teamVision), recomputed only when the player side's sight can
// have changed — one of its units moved / turned / died, a panel broke or changed, or a phase began (the
// source's G.ver gate). Enemy figures are position-based (enemyVisible via seesPt) plus the revealT
// timer, so that check runs every frame (the timer runs out on its own). The hostage is never touched.
// Without a player side (AI-vs-AI) nothing is hidden, like the source's title/cinematic `title` case.
using System.Collections.Generic;
using UnityEngine;
using Blindside.Combat;
using Blindside.Data;
using Blindside.Map;
using Blindside.Rendering;
using Blindside.Turns;
using Blindside.Vision;

namespace Blindside.Integration
{
    public class VisibilityController : MonoBehaviour
    {
        VisionSystem vision;
        MapGrid map;
        IReadOnlyList<SimUnit> units;
        TurnManager turns;
        OperatorTeam side;
        bool active;
        bool fogDirty;

        FogOfWarRenderer fog;

        public void Bind(VisionSystem vision, MapGrid map, IReadOnlyList<SimUnit> units, TurnManager turns, OperatorTeam? playerTeam)
        {
            Unbind();
            this.vision = vision; this.map = map; this.units = units; this.turns = turns;
            active = playerTeam.HasValue;
            side = playerTeam ?? OperatorTeam.Attack;
            if (!active) return;

            EnsureFog();
            fog.gameObject.SetActive(true);
            foreach (var u in units) u.Changed += OnUnitChanged;
            map.Changed += OnMapChanged;
            turns.OnPhaseBegin += OnPhaseBegin;
            fogDirty = true;
            SyncFog();
            SyncFigures();
        }

        public void Unbind()
        {
            if (units != null)
                foreach (var u in units)
                {
                    u.Changed -= OnUnitChanged;
                    if (u.View is Component c && c != null) u.View.SetVisible(true);
                }
            if (map != null) map.Changed -= OnMapChanged;
            if (turns != null) turns.OnPhaseBegin -= OnPhaseBegin;
            if (fog != null) fog.gameObject.SetActive(false);
            MapWallSync.ApplyVision(null);
            vision = null; map = null; units = null; turns = null; active = false;
            Gadgets = null; gadgetSig = int.MinValue; visibleCells = null;
        }

        void OnDestroy()
        {
            Unbind();
            if (fog != null) Destroy(fog.gameObject);
        }

        void OnUnitChanged(SimUnit u) { if (u.Team == side) fogDirty = true; }
        void OnMapChanged() => fogDirty = true;
        void OnPhaseBegin(MatchPhase _) => fogDirty = true;

        /// <summary>shownToPlayer(e). True for everything when there's no player side.</summary>
        public bool ShownToPlayer(ICombatUnit e)
        {
            if (!active || e == null || e.IsHostage || e.Team == side) return true;
            if (e is IVisionAgent a && vision.EnemyVisible(side, a)) return true;
            return e is IRevealable r && r.RevealT > Time.unscaledTime;
        }

        /// <summary>enemyVisible(G.side, e) — false without a player side.</summary>
        public bool EnemyVisibleToPlayer(IVisionAgent e) => active && vision.EnemyVisible(side, e);

        public bool Active => active;
        public OperatorTeam Side => side;

        void LateUpdate()
        {
            if (!active) return;
            SyncFog();
            SyncFigures();
        }

        /// <summary>G.cams / G.drones for teamVision()'s gadget half (set by MatchBootstrap after the abilities exist).</summary>
        public Blindside.Abilities.AbilityContext Gadgets;
        int gadgetSig = int.MinValue;

        // teamVision(team).gadget: cameras (def, r 3.2) / drones (atk, r 2.5), LOS-gated circles.
        void AddGadgetCells(HashSet<int> tv)
        {
            if (Gadgets == null) return;
            if (side == OperatorTeam.Defense)
                foreach (var c in Gadgets.Cameras) { if (c.Working) TeamVisionBuilder.AddCircleCells(vision, map, c.x, c.y, 3.2f, side, tv); }
            else if (Gadgets.Drones != null)
                foreach (var d in Gadgets.Drones.Drones) { if (d.alive) TeamVisionBuilder.AddCircleCells(vision, map, d.x, d.y, 2.5f, side, tv); }
        }

        // A camera placed / a drone gone: the gadget half changed without any unit or panel changing.
        int GadgetSignature()
        {
            if (Gadgets == null) return 0;
            int h = 17;
            foreach (var c in Gadgets.Cameras) h = h * 31 + (c.Working ? 1 : c.alive ? 4 : 2);
            if (Gadgets.Drones != null)
                foreach (var d in Gadgets.Drones.Drones) h = h * 31 + (d.alive ? Mathf.RoundToInt(d.x * 10f) * 997 + Mathf.RoundToInt(d.y * 10f) : 3);
            return h;
        }

        HashSet<int> visibleCells;

        /// <summary>cellVisible(G.side,x,y): the cell holding (x,y) is in the player side's sight (units + gadgets).
        /// True for everything without a player side.</summary>
        public bool CellVisible(float x, float y)
        {
            if (!active || visibleCells == null) return true;
            return visibleCells.Contains(Mathf.FloorToInt(y + .5f) * MapGrid.Width + Mathf.FloorToInt(x + .5f));
        }

        // updateDiscovery(): what the attack side's sight has come across stays known (cams, electric fields).
        void UpdateDiscovery(HashSet<int> tv)
        {
            if (side != OperatorTeam.Attack || Gadgets == null) return;
            foreach (var c in Gadgets.Cameras)
                if (c.alive && tv.Contains(Mathf.FloorToInt(c.y + .5f) * MapGrid.Width + Mathf.FloorToInt(c.x + .5f))) c.discovered = true;
            foreach (var e in Gadgets.Electrics)
                if (tv.Contains(Mathf.FloorToInt(e.y + .5f) * MapGrid.Width + Mathf.FloorToInt(e.x + .5f))) e.discovered = true;
        }

        void SyncFog()
        {
            int sig = GadgetSignature();
            if (sig != gadgetSig) { gadgetSig = sig; fogDirty = true; }
            if (!fogDirty) return;
            fogDirty = false;
            var tv = vision.TeamVision(side);
            AddGadgetCells(tv);
            visibleCells = tv;
            UpdateDiscovery(tv);
            fog.Apply(tv);
            MapWallSync.ApplyVision(tv);
        }

        void SyncFigures()
        {
            foreach (var u in units)
                if (u.Team != side && u.Alive && u.View != null) u.View.SetVisible(ShownToPlayer(u));
        }

        void EnsureFog()
        {
            if (fog != null) return;
            // Scene root: the quad's vertices are already world-space grid positions.
            var go = new GameObject("FogOfWar", typeof(MeshFilter), typeof(MeshRenderer));
            fog = go.AddComponent<FogOfWarRenderer>();
            fog.Build(MapGrid.Width, map.Height);
        }
    }
}
