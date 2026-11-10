// 持ち替え (FLARE's co-op rank 6, 二丁持ち): an operator carrying a second gun (SimUnit.Weapon2) swaps the two — free, before acting.
// The gun in hand changes everything that depends on it (ProgressBonus.ApplyWeapon: its mastery, its attachments) and the model's gun.
using UnityEngine;
using Blindside.Rendering;
using Blindside.UI;

namespace Blindside.Integration
{
    public partial class PlayerSelectionController
    {
        void SwapGun(SimUnit u)
        {
            if (u == null || u.Weapon2 == null || u.Acted) return;
            var w = u.Weapon;
            u.Weapon = u.Weapon2;
            u.Weapon2 = w;
            Blindside.Progress.ProgressBonus.ApplyWeapon(u);
            if (u.View is UnitView uv) uv.SetWeapon(u.Weapon);
            Sfx.Ui();
            Log($"{u.Kr}: {u.Weapon.weaponName} に持ち替えた", "a");
            WorldPopup.Show(u.X, u.Y, u.Weapon.weaponName, u.Op != null ? u.Op.color : Color.white);
            SelectUnit(u);               // the menu and the panel show the new gun
            UnitsChanged?.Invoke();
        }
    }
}
