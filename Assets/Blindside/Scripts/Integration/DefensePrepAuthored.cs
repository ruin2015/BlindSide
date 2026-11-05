// The hand-placed half of the defense preparation (MapData.authored). Same steps 2-4 as aiDefPrep in
// blindside_source/04_gameplay_combat_ai.js, with the map's own coordinates instead of the ones DefensePrep computes:
//   electric fields  — placed as listed; each electrifies the reinforced panels with a cell within DefensePrep.ElectrifyReach;
//   cameras          — placed as listed;
//   mirror           — the panel containing the listed cell (nothing happens when that cell is not a wall), reinforcement stripped.
// Step 1 (reinforcing the 10 panels nearest the hostage) has already run in DefensePrep.Apply, which hands over here.
using System.Collections.Generic;
using UnityEngine;
using Blindside.Abilities;
using Blindside.Map;
using Blindside.Vision;

namespace Blindside.Integration
{
    public static class DefensePrepAuthored
    {
        public static PrepReport Apply(MapGrid map, VisionSystem vision, AbilityContext ctx, MapAuthored authored, PrepReport report,
                                       SimUnit volt, SimUnit argus, SimUnit obs)
        {
            if (authored.electrics != null)
                foreach (var s in authored.electrics)
                {
                    ctx.Electrics.Add(new ElectricField { x = s.x, y = s.y, r = DefensePrep.FieldRadius, discovered = false });
                    if (volt != null) volt.Charges--;
                    report.electrics.Add(s);
                    for (int i = 0; i < map.Panels.Count; i++)
                    {
                        var p = map.Panels[i];
                        if (!p.reinforced) continue;
                        foreach (var q in p.cells)
                            if (Mathf.Sqrt((q.x - s.x) * (q.x - s.x) + (q.y - s.y) * (q.y - s.y)) <= DefensePrep.ElectrifyReach) { map.SetPanelElectrified(p.id, true); break; }
                    }
                }

            if (authored.prepCameras != null)
                foreach (var s in authored.prepCameras)
                {
                    ctx.Cameras.Add(new SurveillanceCamera { x = s.x, y = s.y, alive = true, discovered = false, placed = true });
                    if (argus != null) argus.Charges--;
                    report.cameras.Add(s);
                }

            if (authored.hasMirror)
            {
                var mp = map.PanelAt(authored.mirror.x, authored.mirror.y);
                if (mp.HasValue)
                {
                    map.SetPanelMirror(mp.Value.id, true);   // keeps any reinforcement (MirrorAbility)
                    if (obs != null) obs.Charges--;
                    report.mirror = new Vector2Int(Mathf.RoundToInt(mp.Value.centerX), Mathf.RoundToInt(mp.Value.centerY));
                }
            }

            // bumpMap(): opacity grids / fog / polygons rebuild.
            if (vision != null) vision.MapVersion++;
            map.BumpMap();
            return report;
        }
    }
}
