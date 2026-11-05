// The only way gameplay logic (TurnManager, CombatSystem, ...) talks to a unit's visuals. Defined on
// the logic side so logic never references a rendering type, prefab or mesh: Blindside.Rendering's
// UnitView implements it for the current capsule prefab, and a sprite- or model-based view only has
// to implement the same interface (and be the component on the unit prefab) to replace it.
using UnityEngine;

namespace Blindside.Combat
{
    public interface IUnitView
    {
        /// <summary>Team tint: player's side, enemy, or hostage (makeFigure's `tc`).</summary>
        void SetIdentity(bool isAlly, bool isHost);

        /// <summary>Grid-plane position (x right, y down the map rows) — the view maps it to world space.</summary>
        void SetPosition(float gridX, float gridY);

        /// <summary>Height above the ground in metres (source: `u.rz`, the rope-descent offset in syncUnits'
        /// `m.g.position.set(u.rx,u.rz||0,u.ry)`); while &gt; 0 the figure also sways side to side.</summary>
        void SetHeight(float height);

        /// <summary>Grid-plane facing, radians (atan2(dy,dx)); the view smooths toward it.</summary>
        void SetFacing(float facingRadians);

        void SetHealth(float current, float max);

        /// <summary>The yellow selection ring (`m.sel.visible`).</summary>
        void SetSelected(bool on);

        /// <summary>Figure opacity (syncUnits: `.42` once ended in its own phase, `.5` while ghosted, else 1).</summary>
        void SetOpacity(float alpha);

        /// <summary>syncUnits' `m.g.visible=show` (shownToPlayer): false hides the whole figure incl. its HP bar.</summary>
        void SetVisible(bool on);

        /// <summary>A damaging hit landed on this unit; `headshot` picks the head vs torso hit point.</summary>
        void PlayHit(bool headshot);

        /// <summary>This unit died. Safe to call more than once.</summary>
        void PlayDeath();

        /// <summary>This unit fired a round (animation only).</summary>
        void PlayFire();

        /// <summary>This unit's volley is over (animation only: the reload move).</summary>
        void PlayReload();

        /// <summary>A round at this unit missed (`blocked` false) or was stopped by its shield (true) — animation only.</summary>
        void PlayDodge(bool blocked);

        /// <summary>This unit uses its ability, `abilityKey` = OperatorData.abilityKey (animation only).</summary>
        void PlayAbility(string abilityKey);

        /// <summary>The unit is in combat (seen, shooting or shot at): keep the aim pose (animation only).</summary>
        void SetCombatReady(bool on);

        /// <summary>Overhead HP bar (and any other over-the-head readout the view draws) on/off —
        /// hidden while a battle cinematic shows the same numbers in the cut-in.</summary>
        void SetOverheadVisible(bool on);

        /// <summary>World-space hit points / overhead anchor / gun muzzle, owned by the view's art.</summary>
        Vector3 HeadPoint { get; }
        Vector3 TorsoPoint { get; }
        Vector3 LabelPoint { get; }
        Vector3 MuzzlePoint { get; }
    }
}
