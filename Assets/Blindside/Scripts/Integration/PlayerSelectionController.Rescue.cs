// The rescue operation's 救出 (RESCUE): next to a captive (SimUnit.Captive — GHOST / SILENCE, held where the map says), an attacker frees
// it and it joins the squad on the spot — on the map (Placed), its own sight from now on, but no move this turn (it acts from the next
// turn). Like 人質確保 (.Escort.cs) it spends nothing of the rescuer's. The command shows within RescueReach with an open line.
using System.Collections.Generic;
using UnityEngine;
using Blindside.Data;
using Blindside.Rendering;
using Blindside.UI;

namespace Blindside.Integration
{
    public partial class PlayerSelectionController
    {
        public const float RescueReach = 1.6f;

        SimUnit CaptiveNear(SimUnit u)
        {
            if (units == null || u == null || u.Team != OperatorTeam.Attack) return null;
            SimUnit best = null; float bd = RescueReach;
            foreach (var c in units)
            {
                if (!c.Captive || !c.Alive || c.Team != u.Team) continue;
                float d = Vector2.Distance(new Vector2(u.X, u.Y), new Vector2(c.X, c.Y));
                if (d <= bd && SegFree(u.X, u.Y, c.X, c.Y)) { bd = d; best = c; }
            }
            return best;
        }

        void AddRescueCommand(SimUnit u, List<Command> L)
        {
            var c = CaptiveNear(u);
            if (c == null) return;
            L.Add(new Command { id = "rescue", kr = "救出", en = "RESCUE", ic = "♥", ok = true, sub = c.Op != null ? c.Op.DisplayName : "" });
        }

        void DoRescue(SimUnit u)
        {
            var c = CaptiveNear(u);
            if (c == null) return;
            c.Captive = false;
            c.Facing = Mathf.Atan2(u.Y - c.Y, u.X - c.X);
            c.MoveLeft = 0f; c.Acted = true; c.Ended = true;   // (it acts from the next turn)
            c.Placed = true;                                   // sight (vision polygon + fog) starts here
            string name = c.Op != null ? c.Op.DisplayName : c.Id;
            Log($"{KrOf(u)}: {name} を救出 — 分隊に合流", "a");
            Sfx.Ui();
            WorldPopup.Show(c.X, c.Y, "RESCUED", c.Op != null ? c.Op.color : Color.white);
            CombatFx.Ring(GridSpace.GridToWorld(c.X, c.Y), c.Op != null ? c.Op.color : Color.white, .6f, 1.4f);
            SelectUnit(u);
            UnitsChanged?.Invoke();
        }
    }
}
