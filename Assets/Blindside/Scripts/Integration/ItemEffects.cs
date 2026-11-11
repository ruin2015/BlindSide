// The HQ's items as far as a unit itself needs them: the squad's stock and the 除細動器 going off. The commands are
// PlayerSelectionController.Items.cs.
using UnityEngine;
using Blindside.Rendering;
using Blindside.UI;

namespace Blindside.Integration
{
    public static class ItemEffects
    {
        public const string DefibKey = "defib";
        public const int DefibPercent = 50;
        static readonly Color ReviveGreen = new Color32(0x5d, 0xff, 0xa2, 0xff);

        public static int Stock(string key)
        {
            if (TutorialGate.Active) return 99;   // (training: nothing runs out, nothing is taken from the squad)
            var d = Blindside.Save.SaveSystem.Current;
            return d != null && !string.IsNullOrEmpty(key) ? d.GadgetOf(key).count : 0;
        }

        /// <summary>One of `key` taken from the squad's stock.</summary>
        public static void Spend(string key)
        {
            var d = Blindside.Save.SaveSystem.Current;
            if (d == null || TutorialGate.Active) return;
            var line = d.GadgetOf(key);
            line.count = Mathf.Max(0, line.count - 1);
        }

        /// <summary>A 除細動器 has brought `u` back: one is used up.</summary>
        public static void Revived(SimUnit u)
        {
            Spend(DefibKey);
            WorldPopup.Show(u.X, u.Y, "REVIVED", ReviveGreen);
            CombatFx.Ring(GridSpace.GridToWorld(u.X, u.Y), ReviveGreen, .7f, 1.6f);
            HudLog.Add($"{u.Kr}: 除細動器が作動 — 体力{DefibPercent}%で復帰", HudLogType.Ally);
        }
    }
}
