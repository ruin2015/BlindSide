// What the engagement panel (EngagePreview) shows of one combatant: face / codename / kana / weapon / colour from the unit, and the
// numbers of its volley in the panel's display strings. Shared by the preemptive forecast (PlayerSelectionController) and the
// battle scene's live panel (GameplayUIBridge). Allocates only when a panel is filled (strings), never per frame.
using UnityEngine;
using Blindside.Combat;
using Blindside.Data;
using Blindside.UI;

namespace Blindside.Integration
{
    public static class EngageSides
    {
        static readonly Color HostCol = UIPalette.Hex("#FFB13B");

        /// <summary>`x` as a panel side (`playerSide`: the player's team, for the ally colour); `after`: its HP after the exchange.</summary>
        public static EngagePreview.Side Of(ICombatUnit x, OperatorTeam playerSide, int after)
        {
            var su = x as SimUnit;
            var op = su != null ? su.Op : null;
            string key = op != null ? op.key : x is SimHostage h ? h.Key : "?";
            string kr = x is Blindside.Movement.IOperatorNamed n && !string.IsNullOrEmpty(n.Kr) ? n.Kr : x.IsHostage ? "人質" : key;
            return new EngagePreview.Side
            {
                key = key, en = x.IsHostage ? "HOSTAGE" : op != null ? op.DisplayName : key, kr = kr,
                weapon = x.Weapon != null ? x.Weapon.weaponName : "",
                color = x.IsHostage ? HostCol : op != null ? op.color : Color.white,
                ally = !x.IsHostage && x.Team == playerSide,
                hp = x.HP, maxHp = MaxHp(x), after = after,
            };
        }

        public static int MaxHp(ICombatUnit u) => u is SimUnit su ? su.MaxHP : u is SimHostage h ? h.MaxHP : 100;

        /// <summary>The numbers of one volley: damage per round, hit %, headshot %, rounds, and whether it is the preemptive one.</summary>
        public static void Volley(ref EngagePreview.Side s, int chance, int dmg, int shots, int hs, bool preempt)
        {
            s.dmg = dmg.ToString();
            s.hit = chance + "%";
            s.hs = hs + "%";
            s.shots = "×" + shots;
            s.preempt = preempt;
        }
    }
}
