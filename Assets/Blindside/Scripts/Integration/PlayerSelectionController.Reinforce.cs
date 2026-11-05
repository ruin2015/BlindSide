// REINFORCE command (defense preparation only) for PlayerSelectionController. Ports, from blindside_source:
//   05_ui_input_intro.js — doCommand's 'reinforce' branch (~183), onClick's reinforce branch (~329-331),
//                          updateAim's `ui.hoverPanel=panelAt(h.x,h.y)` (~346)
//   01_core_data_map_vision.js — canReinforce / panelNear (~239, 242), wallProgress (~241)
// The valid panels glow through the same yellow shells as the ability mode's (.Ability.cs SyncAbilityHelpers): blinking,
// the hovered one steady and brighter. The steel look of a reinforced panel is ReinforcedWallViews' job.
using UnityEngine;
using System.Collections.Generic;
using Blindside.Map;
using Blindside.Rendering;
using Blindside.UI;

namespace Blindside.Integration
{
    public partial class PlayerSelectionController
    {
        /// <summary>panels.filter(p=>canReinforce(p)&&panelNear(p,u)): intact, not yet reinforced, a cell within .85 of the unit.
        /// A mirrored wall can be reinforced too (it then opens only to thermite / the launcher and stops rounds).</summary>
        List<int> ReinforceablePanelsNear(SimUnit u)
        {
            var ids = new List<int>();
            if (map == null) return ids;
            foreach (var p in map.Panels)
            {
                if (p.reinforced) continue;
                bool intact = true, near = false;
                foreach (var c in p.cells)
                {
                    if (map.TileAt(c.x, c.y) != TileType.Wall) intact = false;
                    float qx = Mathf.Clamp(u.X, c.x - .5f, c.x + .5f), qy = Mathf.Clamp(u.Y, c.y - .5f, c.y + .5f);
                    if (Mathf.Sqrt((u.X - qx) * (u.X - qx) + (u.Y - qy) * (u.Y - qy)) <= .85f) near = true;
                }
                if (intact && near) ids.Add(p.id);
            }
            return ids;
        }

        /// <summary>doCommand's 'reinforce': G.ui={mode:'reinforce',…,validPanels}, then the hint.</summary>
        void StartReinforce(SimUnit u)
        {
            HideMenu();
            mode = Mode.Reinforce; uiUnit = u; panelUnit = u; hoverPanel = -1;
            validPanels.Clear();
            foreach (int id in ReinforceablePanelsNear(u)) validPanels.Add(id);
            NotifySelection();
            SetHint("強化する壁を選択してください", $"残り強化 {reinforceLeft}/10 · 壁ごとに必要な強化数が異なります", ("cancel", "ESC キャンセル", false));
        }

        /// <summary>onClick's reinforce branch: a click off the valid panels does nothing; on one it becomes reinforced.</summary>
        /// <returns>true when the click was consumed by the reinforce mode.</returns>
        bool OnClickReinforceMode(Vector2 screenPos)
        {
            if (mode != Mode.Reinforce) return false;
            var u = uiUnit;
            int pid = BattlefieldCamera.GroundAt(screenPos, out var g) ? PanelIdAt(g.x, g.y) : -1;
            if (pid < 0 || !validPanels.Contains(pid)) return true;
            map.SetPanelReinforced(pid, true);
            reinforceLeft--;
            if (vision != null) vision.MapVersion++;   // G.ver++
            var p = map.Panels[pid];
            SfxNoise.Hit();
            foreach (var c in p.cells) CombatFx.Ring(GridSpace.GridToWorld(c.x, c.y, .08f), new Color32(0xff, 0xd2, 0x1f, 0xff), .5f, .8f);
            int r = 0, n = 0;
            foreach (var w in map.Walls)
            {
                if (w.id != p.wallId) continue;
                n = w.panelIds.Count;
                foreach (int i in w.panelIds) if (map.Panels[i].reinforced) r++;
            }
            Log($"壁を強化 — この壁 {r}/{n} 強化完了 (残り強化 {reinforceLeft})", "a");
            SelectUnit(u);
            UnitsChanged?.Invoke();
            return true;
        }
    }
}
