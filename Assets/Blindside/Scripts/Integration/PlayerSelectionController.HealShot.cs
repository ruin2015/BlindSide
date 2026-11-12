// SERUM's 戦闘スティム on the player's side, as a shot: the ability's ally targeting becomes an aim like an attack's. SERUM draws a pistol
// (the figure only: the unit's own gun is not changed), raises it (UnitAnimator.ForceAim) and turns to the ally under the cursor — or
// to the ground point — as an aim does (its facing; put back on cancel). A line runs from the muzzle to the target: green when the shot
// can reach that ally, red when not. A click fires one round — the pistol's kick, the muzzle flash, a tracer onto the ally — and the hit
// heals it (StimAbility through AbilitySystem.Use: the heal, the move bonus, the charge, the presenter as before).
// Reach: HealShotRange m with a clear line (TraceLine: no wall in between), or any ally at co-op rank 8 (ナノボット); SERUM itself:
// clicking SERUM injects itself as before (no shot). The AI's stim is unchanged (StimAbility's own spec).
// Per frame: the aim line is moved, nothing is allocated.
using UnityEngine;
using UnityEngine.InputSystem;
using Blindside.Abilities;
using Blindside.Combat;
using Blindside.Data;
using Blindside.Map;
using Blindside.Rendering;
using Blindside.UI;

namespace Blindside.Integration
{
    public partial class PlayerSelectionController
    {
        public const float HealShotRange = 6f, HealShotTurn = .22f, HealShotHold = .55f;
        static readonly Color HealLineOk = new Color32(0x5d, 0xff, 0xa2, 0xff);

        bool healShot;
        SimUnit healShooter;
        float healBackupFacing;
        WeaponData healPistol;
        LineRenderer healLine;

        static bool IsHealShot(SimUnit u) => u != null && u.AbilityKey == "stim";

        /// <summary>Who the shot can heal: SERUM itself (an injection), and every ally in reach with a clear line (any ally at rank 8).</summary>
        void HealShotTargets(SimUnit u, AbilitySpec sp)
        {
            bool anywhere = sp.Range > 100f;
            foreach (var a in units)
            {
                if (!a.Alive || a.Team != u.Team || a.Captive) continue;
                if (a == u || (anywhere && a.Placed)) { validAllies.Add(a); continue; }
                if (!a.Placed) continue;
                float dx = a.X - u.X, dy = a.Y - u.Y;
                if (dx * dx + dy * dy > HealShotRange * HealShotRange) continue;
                var path = CombatSystem.TraceLine(map, u.X, u.Y, a.X, a.Y);
                if (path.Blocked || path.Walls > 0) continue;
                validAllies.Add(a);
            }
        }

        /// <summary>The aim begins: the pistol out and up.</summary>
        void BeginHealAim(SimUnit u)
        {
            healShot = true; healShooter = u;
            healBackupFacing = u.Facing;
            if (healPistol == null) healPistol = BuildAssetRegistry.Weapon("Weapon_PST");
            if (u.View is UnitView uv && uv != null)
            {
                if (healPistol != null) uv.SetWeapon(healPistol);
                if (uv.UnitAnimator != null) uv.UnitAnimator.ForceAim = true;
            }
        }

        /// <summary>The aim is over: the unit's own gun back in its hands, the pistol lowered (and on a cancel, the old facing).</summary>
        void EndHealAim(bool restoreFacing)
        {
            if (!healShot) return;
            healShot = false;
            var u = healShooter; healShooter = null;
            if (healLine != null) healLine.enabled = false;
            if (u == null) return;
            if (restoreFacing) u.Facing = healBackupFacing;
            if (u.View is UnitView uv && uv != null)
            {
                uv.SetWeapon(u.Weapon);
                if (uv.UnitAnimator != null) uv.UnitAnimator.ForceAim = false;
            }
        }

        /// <summary>Every frame while aiming: turned to the ally (or the point) under the cursor, the line drawn green / red.</summary>
        void SyncHealAim()
        {
            if (!healShot) return;
            var u = healShooter;
            if (mode == Mode.Busy) return;   // (the shot itself: HealShotRoutine)
            if (u == null || mode != Mode.Ability || u != uiUnit) { EndHealAim(true); return; }
            var mouse = Mouse.current;
            if (mouse == null) return;
            var pos = mouse.position.ReadValue();
            var a = PickUnit(pos);
            bool ally = a != null && a != u && a.Team == u.Team && a.Alive;
            Vector3 end;
            float tx, ty;
            if (ally) { tx = a.X; ty = a.Y; end = a.View is UnitView av && av != null ? av.TorsoPoint : GridSpace.GridToWorld(a.RX, a.RY, 1f); }
            else if (BattlefieldCamera.GroundAt(pos, out var h)) { tx = h.fx; ty = h.fy; end = GridSpace.GridToWorld(h.fx, h.fy, .9f); }
            else { if (healLine != null) healLine.enabled = false; return; }
            if (Mathf.Abs(tx - u.X) + Mathf.Abs(ty - u.Y) > .05f)
            {
                float f = Mathf.Atan2(ty - u.Y, tx - u.X);
                if (Mathf.Abs(AngNorm(f - u.Facing)) > .01f) u.Facing = f;
            }
            EnsureHealLine();
            var from = u.View is UnitView uv && uv != null ? uv.MuzzlePoint : GridSpace.GridToWorld(u.RX, u.RY, 1f);
            healLine.SetPosition(0, from);
            healLine.SetPosition(1, end);
            var c = ally && validAllies.Contains(a) ? HealLineOk : InvalidRed;
            healLine.startColor = new Color(c.r, c.g, c.b, .9f);
            healLine.endColor = new Color(c.r, c.g, c.b, .35f);
            healLine.enabled = true;
        }

        void EnsureHealLine()
        {
            if (healLine != null) return;
            var go = new GameObject("HealShotLine");
            go.transform.SetParent(transform, false);
            healLine = go.AddComponent<LineRenderer>();
            healLine.positionCount = 2;
            healLine.useWorldSpace = true;
            healLine.widthMultiplier = .045f;
            healLine.numCapVertices = 2;
            healLine.sharedMaterial = new Material(Shader.Find("Sprites/Default"));
            healLine.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            healLine.receiveShadows = false;
            healLine.enabled = false;
        }

        /// <summary>The click on a reachable ally: turn, fire one round onto it, then the heal (the ability as before).</summary>
        System.Collections.IEnumerator HealShotRoutine(SimUnit u, SimUnit a)
        {
            if (a != u)
            {
                u.Facing = Mathf.Atan2(a.Y - u.Y, a.X - u.X);
                if (healLine != null) healLine.enabled = false;
                yield return new WaitForSeconds(HealShotTurn);
                if (u.View is UnitView uv && uv != null)
                {
                    uv.PlayFire();
                    var muzzle = uv.MuzzlePoint;
                    var end = a.View is UnitView av && av != null ? av.TorsoPoint : GridSpace.GridToWorld(a.RX, a.RY, 1f);
                    MuzzleFlash.Play(muzzle, (end - muzzle).normalized, .7f);
                    CombatFx.TracerTo(uv.TorsoPoint, u.Facing, end, muzzle);
                }
                SfxNoise.Shot();
                yield return new WaitForSeconds(.12f);
            }
            // (the facing stays where the shot went; the pistol is put away after the heal)
            abilityCtx.PrepPhase = turns.Phase == Blindside.Turns.MatchPhase.Prep;
            yield return abilities.Use(u, AbilityTarget.FromAlly(a), abilityCtx);
            yield return new WaitForSeconds(HealShotHold);
            EndHealAim(false);
            UnitsChanged?.Invoke();
            if (turns != null) turns.CheckWin();
            busy = false;
            if (turns == null || turns.Over) yield break;
            AfterAction(u);
        }
    }
}
