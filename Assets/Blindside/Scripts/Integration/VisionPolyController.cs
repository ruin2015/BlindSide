// The polygon half of syncVision() + stylePolys() (blindside_source/03_renderer_threejs.js:162-173):
//   for(const u of G.units){ if(!u.alive||u.placed===false) continue; const ally=u.team===G.side;
//     if(!ally&&!enemyVisible(G.side,u)) continue; want.add(u);
//     let o=R.polys.get(u); if(!o){ o=mkPoly(); R.polys.set(u,o); dynRoot.add(o.g); } setPoly(o,u,visionPoly(u)); o.ally=ally; }
//   for(const [u,o] of R.polys) if(!want.has(u)){ dynRoot.remove(o.g); R.polys.delete(u); }
//   stylePolys(): ally → 0x2fe4ff, fill .2/.07 and line .85/.3 (selected/not); enemy → 0xff3b54, fill .14, line .6.
// Unlike the source, an ally's polygon is drawn only while that unit is selected (when there is a player side).
// Enemy polygons follow enemyVisible() only (not revealT), and only when there is a player side — an
// AI-vs-AI match keeps showing just the attack side's polygons. syncVision() is gated on G.ver in the
// source, i.e. it only recomputes after a state change; here any unit's Changed (position / facing /
// alive) and the map's Changed (panel broken, mirror, reinforced) mark polygons dirty, and LateUpdate
// rebuilds just those (plus the enemy visibility pass), at most once a frame.
using System.Collections.Generic;
using UnityEngine;
using Blindside.Data;
using Blindside.Map;
using Blindside.Rendering;
using Blindside.Vision;

namespace Blindside.Integration
{
    public class VisionPolyController : MonoBehaviour
    {
        [Tooltip("VisionPoly.prefab — one instance per drawn unit.")]
        public VisionMeshRenderer prefab;

        VisionSystem vision;
        MapGrid map;
        IReadOnlyList<SimUnit> units;
        OperatorTeam side;
        bool showEnemies;
        PlayerSelectionController selection;

        readonly Dictionary<SimUnit, VisionMeshRenderer> polys = new Dictionary<SimUnit, VisionMeshRenderer>();
        readonly HashSet<SimUnit> dirty = new HashSet<SimUnit>();
        bool allDirty, wantDirty, styleDirty;

        public void Bind(VisionSystem vision, MapGrid map, IReadOnlyList<SimUnit> units, OperatorTeam? playerTeam, PlayerSelectionController selection)
        {
            Unbind();
            this.vision = vision; this.map = map; this.units = units; this.selection = selection;
            side = playerTeam ?? OperatorTeam.Attack;   // G.side (AI-vs-AI reads as the attack side, like the HUD)
            showEnemies = playerTeam.HasValue;

            foreach (var u in units) u.Changed += OnUnitChanged;
            map.Changed += OnMapChanged;
            if (selection != null) selection.SelectionChanged += OnSelectionChanged;
            allDirty = true; wantDirty = true; styleDirty = true;
            Sync();
        }

        public void Unbind()
        {
            if (units != null) foreach (var u in units) u.Changed -= OnUnitChanged;
            if (map != null) map.Changed -= OnMapChanged;
            if (selection != null) selection.SelectionChanged -= OnSelectionChanged;
            foreach (var o in polys.Values) if (o != null) Destroy(o.gameObject);
            polys.Clear(); dirty.Clear();
            vision = null; map = null; units = null; selection = null;
        }

        void OnDestroy() => Unbind();

        // Any unit moving/turning can change which enemies are seen (either side of the sight line moved).
        void OnUnitChanged(SimUnit u) { dirty.Add(u); wantDirty = true; }
        void OnMapChanged() { allDirty = true; wantDirty = true; }
        void OnSelectionChanged() { styleDirty = true; wantDirty = true; }

        void LateUpdate()
        {
            if (vision == null || (!allDirty && !wantDirty && dirty.Count == 0 && !styleDirty)) return;
            Sync();
        }

        /// <summary>The tutorial: units whose cone is drawn whatever the rules above say (null: none). Call Refresh after changing it.</summary>
        public System.Func<SimUnit, bool> ForceShow;
        /// <summary>The tutorial: units whose cone is never drawn while this says so (null: none). Wins over ForceShow.</summary>
        public System.Func<SimUnit, bool> ForceHide;
        public void Refresh() { wantDirty = true; allDirty = true; styleDirty = true; }

        bool Wanted(SimUnit u)
        {
            if (!u.Alive || !u.Placed) return false;
            if (ForceHide != null && ForceHide(u)) return false;
            if (ForceShow != null && ForceShow(u)) return true;
            // With a player side, an ally's cone shows only while that unit is selected (AI-vs-AI keeps them all).
            if (u.Team == side) return !showEnemies || (selection != null && selection.SelectedUnit == u);
            return showEnemies && vision.EnemyVisible(side, u);
        }

        void Sync()
        {
            if (wantDirty)
            {
                foreach (var u in units)
                {
                    bool want = Wanted(u), has = polys.TryGetValue(u, out var o);
                    if (want && !has)
                    {
                        o = Instantiate(prefab, transform);
                        o.name = "VisionPoly_" + (u.Op != null ? u.Op.key : u.Id);
                        o.transform.localPosition = Vector3.zero; o.transform.localRotation = Quaternion.identity; o.transform.localScale = Vector3.one;
                        polys[u] = o;
                        dirty.Add(u); styleDirty = true;
                    }
                    else if (!want && has)
                    {
                        if (o != null) Destroy(o.gameObject);   // R.polys.delete(u)
                        polys.Remove(u);
                    }
                }
                wantDirty = false;
            }
            if (allDirty) { foreach (var u in polys.Keys) dirty.Add(u); allDirty = false; }
            if (dirty.Count > 0)
            {
                foreach (var u in dirty)
                    if (polys.TryGetValue(u, out var o) && o != null) o.Rebuild(vision, u);   // setPoly(o,u,visionPoly(u))
                dirty.Clear();
            }
            if (styleDirty)
            {
                var sel = selection != null ? selection.SelectedUnit : null;
                foreach (var kv in polys)
                    if (kv.Value != null) kv.Value.SetStyle(kv.Key.Team == side, sel == kv.Key);
                styleDirty = false;
            }
        }
    }
}
