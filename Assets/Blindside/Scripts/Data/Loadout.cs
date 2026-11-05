// Which of an operator's guns (OperatorData.weapons) goes into the operation — chosen before it starts, remembered per
// operator in PlayerPrefs. The AI's operators always take their first gun.
using UnityEngine;

namespace Blindside.Data
{
    public static class Loadout
    {
        const string Prefix = "Blindside.Loadout.";

        /// <summary>Index into op.weapons the player picked (0 when never picked or out of range).</summary>
        public static int Choice(OperatorData op)
        {
            if (op == null || op.weapons == null || op.weapons.Length == 0) return 0;
            int i = PlayerPrefs.GetInt(Prefix + op.key, 0);
            return i >= 0 && i < op.weapons.Length && op.weapons[i] != null ? i : 0;
        }

        public static void SetChoice(OperatorData op, int index)
        {
            if (op == null) return;
            PlayerPrefs.SetInt(Prefix + op.key, index);
            PlayerPrefs.Save();
        }

        /// <summary>The gun `op` fights with: the player's pick for the player's own operators, else the first one; `op.weapon`
        /// when the operator has no list.</summary>
        public static WeaponData Of(OperatorData op, bool playerPicks)
        {
            if (op == null) return null;
            if (op.weapons == null || op.weapons.Length == 0) return op.weapon;
            var w = op.weapons[playerPicks ? Choice(op) : 0];
            return w != null ? w : op.weapon;
        }
    }
}
