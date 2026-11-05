// What the tutorial (TutorialDirector) needs from the selection UI beyond TutorialGate's switches: where a command's button is on
// screen (for the guide frame), whether the MOVE path is being drawn, and the demonstration move — the path drawn by itself along a
// given line with the usual yellow ribbon / end ring / length label, then walked like a confirmed MOVE.
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Blindside.Abilities;
using Blindside.Movement;
using Blindside.Rendering;
using Blindside.UI;

namespace Blindside.Integration
{
    public partial class PlayerSelectionController
    {
        /// <summary>The menu button of command `id` while the menu is up (null otherwise).</summary>
        public RectTransform MenuButtonRect(string id)
        {
            if (kitMenu == null || !MenuVisible || uiUnit == null) return null;
            var list = Commands(uiUnit);
            int i = list.FindIndex(c => c.id == id);
            if (i < 0 || i >= kitMenu.Buttons.Count || kitMenu.Buttons[i] == null) return null;
            return kitMenu.Buttons[i].transform as RectTransform;
        }

        /// <summary>The MOVE path is being chosen (the reach overlay is up).</summary>
        public bool InMoveMode => mode == Mode.Move;
        /// <summary>A move / shot is being played out.</summary>
        public bool Busy => busy;
        /// <summary>FIRE's aim is up (the red line follows the mouse).</summary>
        public bool InFireMode => mode == Mode.Fire;
        /// <summary>The unit detail panel (bottom left) is up.</summary>
        public bool UnitPanelUp => kitPanel != null ? kitPanel.Visible : panelRoot != null && panelRoot.gameObject.activeSelf;

        /// <summary>Where a demonstration's "hand" is (grid): the drawn path's tip / the aim's end; null outside a demonstration.</summary>
        public Vector2? DemoCursorGrid { get; private set; }

        /// <summary>ABILITY is picking its target (the valid walls glow).</summary>
        public bool InAbilityMode => mode == Mode.Ability;
        /// <summary>VIEW / the end-of-action facing prompt is up.</summary>
        public bool InFacingMode => mode == Mode.Facing;
        /// <summary>A button of the hint bar (e.g. "lock" — 視界固定 [F]) while it is shown, else null.</summary>
        public RectTransform HintButtonRect(string id) => hintView != null ? hintView.ButtonRect(id) : null;

        /// <summary>
        /// The tutorial's FIRE demonstration: `u` is selected, FIRE is entered, the aim sweeps from `fromAng` to `toAng` over
        /// `sweepSeconds` (the red aim line, its end ring and the tip follow), then — if `shoot` — fires along the last angle as a
        /// confirmed click would. Ends with nothing selected (the caller gives the unit its action back).
        /// </summary>
        /// <param name="pressCommand">played with the menu up, before FIRE is entered (the tutorial shows the button being pressed)</param>
        /// <param name="onShoot">called the moment the demonstration "clicks" to fire</param>
        public IEnumerator TutorialDemoFire(SimUnit u, float fromAng, float toAng, float sweepSeconds, bool shoot,
                                            System.Func<IEnumerator> pressCommand = null, System.Action onShoot = null)
        {
            if (u == null || combat == null) yield break;
            SelectUnit(u);
            yield return new WaitForSecondsRealtime(.7f);
            if (pressCommand != null) yield return pressCommand();
            HideMenu();
            StartFire(u);
            float t = 0f;
            while (t < sweepSeconds)
            {
                t += Time.unscaledDeltaTime;
                float p = Mathf.Clamp01(t / sweepSeconds);
                float a = Mathf.LerpAngle(fromAng * Mathf.Rad2Deg, toAng * Mathf.Rad2Deg, p * p * (3f - 2f * p)) * Mathf.Deg2Rad;
                fireAim = a; fireCast = FireCast(u, a); hasFireCast = true; u.Facing = a;
                DemoCursorGrid = fireCast.end;
                var cam = Cam;
                if (cam != null)
                {
                    var s = cam.WorldToScreenPoint(GridSpace.GridToWorld(fireCast.end.x, fireCast.end.y, 0f));
                    UpdateTip(new Vector2(s.x, s.y));
                }
                yield return null;
            }
            yield return new WaitForSecondsRealtime(.5f);
            if (shoot)
            {
                onShoot?.Invoke();
                busy = true; HideHint(); HideTip();
                mode = Mode.Busy; hasFireCast = false;
                yield return FireDirection(u, fireAim);
                busy = false;
                yield return new WaitForSecondsRealtime(.4f);
            }
            DemoCursorGrid = null;
            Deselect();
        }

        /// <summary>
        /// The tutorial's ABILITY demonstration on one wall: `u` is selected (its menu up), `pressCommand` shows ABILITY being pressed,
        /// the ability mode lights the valid walls (TutorialGate.PanelFilter narrows them), `pickWall` shows the wall being clicked, and
        /// the ability is used on panel index `panelIndex` as that click would. Ends with nothing selected.
        /// </summary>
        public IEnumerator TutorialDemoAbility(SimUnit u, int panelIndex, System.Func<IEnumerator> pressCommand = null, System.Func<IEnumerator> pickWall = null)
        {
            if (u == null || abilities == null || abilityCtx == null || map == null || panelIndex < 0) yield break;
            SelectUnit(u);
            yield return new WaitForSecondsRealtime(.7f);
            if (pressCommand != null) yield return pressCommand();
            bool lit = StartAbility(u) && mode == Mode.Ability;
            if (lit && pickWall != null) yield return pickWall();
            busy = true; HideHint(); HideMenu();
            mode = Mode.Busy;
            abilityCtx.PrepPhase = false;
            yield return abilities.Use(u, AbilityTarget.FromPanel(map.Panels[panelIndex]), abilityCtx);
            UnitsChanged?.Invoke();
            busy = false;
            Deselect();
        }

        /// <summary>
        /// The tutorial's demonstration: `u` is selected, MOVE is entered, the path `line` (starting at the unit) draws itself over
        /// `drawSeconds`, holds a moment, and is walked as a confirmed MOVE. Ends with nothing selected.
        /// </summary>
        /// <param name="pressCommand">played with the menu up, before MOVE is entered (the tutorial shows the button being pressed)</param>
        public IEnumerator TutorialDemoMove(SimUnit u, IList<Vector2> line, float drawSeconds, System.Func<IEnumerator> pressCommand = null)
        {
            if (u == null || line == null || line.Count < 2 || movement == null) yield break;
            SelectUnit(u);
            yield return new WaitForSecondsRealtime(.7f);
            if (pressCommand != null) yield return pressCommand();
            HideMenu();
            StartMoveMode(u, u);
            SetHint("移動経路を自由に描いてください", "左クリックを押したままドラッグして経路を描く", ("cancel", "ESC キャンセル", false));

            float total = 0f;
            for (int i = 1; i < line.Count; i++) total += Vector2.Distance(line[i - 1], line[i]);
            movePath.Clear();
            movePath.Add(line[0]);
            float t = 0f;
            while (t < drawSeconds)
            {
                t += Time.unscaledDeltaTime;
                float p = Mathf.Clamp01(t / drawSeconds);
                float want = total * (p * p * (3f - 2f * p));
                // the line up to `want` metres, sampled every .15m (as a hand-drawn path would be)
                movePath.Clear();
                movePath.Add(line[0]);
                float run = 0f;
                for (int i = 1; i < line.Count && run < want; i++)
                {
                    Vector2 a = line[i - 1], b = line[i];
                    float seg = Vector2.Distance(a, b);
                    for (float s = .15f; s < seg && run + s < want; s += .15f) movePath.Add(Vector2.Lerp(a, b, s / seg));
                    if (run + seg <= want) movePath.Add(b);
                    else movePath.Add(Vector2.Lerp(a, b, (want - run) / seg));
                    run += seg;
                }
                DemoCursorGrid = movePath[movePath.Count - 1];
                yield return null;
            }
            yield return new WaitForSecondsRealtime(.6f);
            DemoCursorGrid = null;

            var pts = movePath.GetRange(1, movePath.Count - 1);
            busy = true; HideHint(); moveReach = null; moveReachKey++; movePath = new List<Vector2>();
            yield return movement.MoveAlong(u, pts, true);
            busy = false;
            Deselect();
        }
    }
}
