// MOVE command + END TURN for PlayerSelectionController. Ports, from blindside_source:
//   05_ui_input_intro.js — startMoveMode (~162), doCommand's move branch (~170), extendPath (~286),
//                          onMoveRelease (~296), afterAction (~193), modal (~118), setHint/hideHint
//                          (~98), log (~28), updateHUD's #endBtn disabled/pulse (~56-57), and the
//                          #endBtn onclick in bindInput (~389)
//   03_renderer_threejs.js — syncOverlay reach cells (~177), syncUIHelpers path ribbon / end ring /
//                          #pathLbl (~217-226), syncUnits' ended opacity (~140), syncLabels' .ul-t
//                          "行動終了" tag (~275-277)
//   00_markup_and_styles.html — #hint, #log, .modal/.mbox, #pathLbl, .ul-t
//
// Encounters: MovementSystem.MoveAlong runs them for every mover (this MOVE and EnemyAI's aiMoveTo share
// the `movement` instance built here). Its UI hooks live below: showContact (~110) + sfx('alert') → the
// bridge's #contact banner, askEngage (~127) → modal(), and syncLabels' `.ul-a` "!" (03_renderer ~275).
// The drone mover (ui.isDrone) shares this flow — see PlayerSelectionController.Drone.cs.
// The facing/fire modes that afterAction and hintAction hand off to live in PlayerSelectionController.Fire.cs.
//
// One intentional difference from modal(): Escape in the confirm dialog returns to the unit's
// command menu (as requested) instead of answering いいえ; N still answers いいえ, Enter/Y answers はい.
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using TMPro;
using Blindside.Data;
using Blindside.Map;
using Blindside.Movement;
using Blindside.Rendering;
using Blindside.Turns;
using Blindside.UI;

namespace Blindside.Integration
{
    public partial class PlayerSelectionController
    {
        // ---- G.busy / G.modal ----
        bool busy, modal;

        MovementSystem movement;
        GameplayUIBridge bridge;

        /// <summary>The match's moveAlong — shared with EnemyAI so both sides walk (and meet) through one code path.</summary>
        public MovementSystem Movement => movement;

        // ---- G.ui for mode 'move' ----
        SimUnit moveMover;          // ui.mover (the unit itself; null while a drone is the mover — moveDrone)
        float moveBudget;           // ui.budget = mover.moveLeft at startMoveMode
        float[] moveReach;          // ui.reach (dijkstra D)
        int moveReachKey;           // ui.reachKey — bumps whenever reach is recomputed/cleared
        List<Vector2> movePath = new List<Vector2>();

        static readonly Color PathYellow = new Color32(0xff, 0xd2, 0x1f, 0xff);   // 0xffd21f

        void BindMovement(GameplayUIBridge b)
        {
            movement = new MovementSystem(map, units, hostage)
            {
                PlayerSide = hasSide ? side : (OperatorTeam?)null,
                Log = Log,
                CheckWin = () => turns != null && turns.CheckWin(),
                IsOver = () => turns == null || turns.Over,
                PanTo = (x, y, ms) => { if (BattlefieldCamera.Instance != null) BattlefieldCamera.Instance.PanTo(x, y, ms); },
                OnStep = _ => UnitsChanged?.Invoke(),
                Vision = vision,
                Marks = marks,
                Combat = combat,
                ShowContact = ShowContact,
                AskEngage = AskEngage,
            };
            if (bridge != null && bridge != b) bridge.EndTurnHandler = null;
            bridge = b;
            if (bridge != null) bridge.EndTurnHandler = EndTurnClicked;
            endPulseOn = null;
        }

        void ResetMoveState()
        {
            moveMover = null; moveDrone = null; moveIsDrone = false; moveReach = null; moveReachKey++;
            movePath = new List<Vector2>(); downPath = false;
        }

        // ================================================================
        //  startMoveMode / extendPath / onMoveRelease / afterAction
        // ================================================================

        /// <summary>startMoveMode(u,mover,false): reach = dijkstra from the mover's cell around every
        /// blocked (hostage/known-enemy) cell; budget = the mover's remaining move.</summary>
        void StartMoveMode(SimUnit u, SimUnit mover)
        {
            movement.Dijkstra(MovementSystem.JsRound(mover.X), MovementSystem.JsRound(mover.Y), false, movement.BlockedSet(u.Team), out var D, out _);
            mode = Mode.Move; uiUnit = u; panelUnit = u;
            NotifySelection();
            moveMover = mover; moveBudget = mover.MoveLeft; moveReach = D; moveReachKey++;
            movePath = new List<Vector2>();
        }

        /// <summary>Port of extendPath(g).</summary>
        void ExtendPath(GroundHit g)
        {
            var P = movePath;
            var pos = MoverPos();
            if (P.Count == 0 || !pos.HasValue) return;
            var q = new Vector2(g.fx, g.fy);
            var last = P[P.Count - 1];
            if (Vector2.Distance(last, q) < .12f) return;
            for (int i = 0; i < P.Count - 3; i++)
                if (Vector2.Distance(P[i], q) < .22f) { P.RemoveRange(i + 1, P.Count - (i + 1)); return; }   // backtrack
            float rem = moveBudget - MovementSystem.PathLen(P);
            if (rem <= .02f) return;
            // bodyOk=(a,b)=>ui.isDrone||bodyClear(a,b,bodyObstacles(G.side,ui.mover)) — a drone ignores bodies.
            var obs = moveIsDrone ? null : movement.BodyObstacles(side, pos.Value);
            if (movement.SegFree(last, q, moveIsDrone) && MovementSystem.BodyClear(last, q, obs))
            {
                float d = Vector2.Distance(last, q);
                if (d <= rem) P.Add(q);
                else P.Add(new Vector2(last.x + (q.x - last.x) * rem / d, last.y + (q.y - last.y) * rem / d));
                return;
            }
            var pl = movement.PlanPath(last, q.x, q.y, moveIsDrone, side, rem);
            if (pl != null && pl.Count > 1) P.AddRange(pl.GetRange(1, pl.Count - 1));
        }

        /// <summary>Port of onMoveRelease(): auto-path on a plain click, back the end point off other
        /// bodies, confirm, then walk it.</summary>
        IEnumerator OnMoveRelease(Vector2 screenPos)
        {
            var mover = moveMover;
            var drone = moveDrone;
            bool isDrone = moveIsDrone;
            var u = uiUnit;
            var start = MoverPos();
            if (!start.HasValue || u == null || mode != Mode.Move) yield break;
            if (movePath.Count <= 1 && BattlefieldCamera.GroundAt(screenPos, out var hover))
            {
                var pl = movement.PlanPath(start.Value, hover.fx, hover.fy, isDrone, side, moveBudget);
                if (pl != null) movePath = pl;
            }
            var Q = movePath;
            // if(!ui.isDrone){ while(…!spotClear(end,mover)) back the end point off }
            while (!isDrone && Q.Count > 1 && !movement.SpotClear(Q[Q.Count - 1].x, Q[Q.Count - 1].y, mover))
            {
                Vector2 a = Q[Q.Count - 2], b = Q[Q.Count - 1];
                float d = Vector2.Distance(a, b);
                if (d <= .15f) Q.RemoveAt(Q.Count - 1);
                else { b.x -= (b.x - a.x) / d * .15f; b.y -= (b.y - a.y) / d * .15f; Q[Q.Count - 1] = b; }
            }
            if (Q.Count <= 1 || MovementSystem.PathLen(Q) < .15f) { movePath = new List<Vector2>(); yield break; }
            float L = MovementSystem.PathLen(Q);

            DialogResult res = DialogResult.None;
            yield return ModalRoutine("移動しますか？", "CONFIRM MOVE",
                $"{(isDrone ? "ドローン" : KrOf(u))} · 経路 <b>{M1(L)}</b>m · 移動後の残り移動力 {M1(moveBudget - L)}m" +
                (!isDrone && u.LockFacing ? "\n<size=11><color=#FFC81E>視界固定中 — 現在の視野角を維持したまま移動します</color></size>" : ""),
                "はい", "いいえ", r => res = r);
            if (res == DialogResult.Escape) { if (u.Alive && !u.Ended && PhaseIsPlayers()) SelectUnit(u); else Deselect(); yield break; }
            if (res != DialogResult.Yes) { Deselect(); yield break; }

            var pts = Q.GetRange(1, Q.Count - 1);
            busy = true; HideHint(); moveReach = null; moveReachKey++; movePath = new List<Vector2>();
            if (isDrone) { yield return droneOps.MoveDrone(drone, pts); u.GadgetUsed = true; }   // moveDrone(ui.mover,pts); u.gadgetUsed=true
            else yield return movement.MoveAlong(u, pts, true);
            busy = false;
            if (turns == null || turns.Over) yield break;
            AfterAction(u);
        }

        /// <summary>Port of afterAction(u): out of move (moveAlong zeroes anything under .35) → the
        /// end-of-action facing prompt, same as WAIT.</summary>
        void AfterAction(SimUnit u)
        {
            if (turns == null || turns.Over) return;
            UnitsChanged?.Invoke();   // updateHUD()
            if (!u.Alive) { Deselect(); return; }
            if (turns.Phase != MatchPhase.Prep && u.MoveLeft <= 0 && !u.Ended) { EnterFacing(u, true); return; }
            if (u.Ended) { Deselect(); return; }
            SelectUnit(u);
        }

        static string KrOf(SimUnit u) => u.Op != null ? u.Op.kr : u.Id;
        static string KrOf(ITurnUnit u) => u is IOperatorNamed n && !string.IsNullOrEmpty(n.Kr) ? n.Kr : u.Id;

        // ================================================================
        //  resolveEncounter's UI: sfx('alert') + showContact / askEngage
        // ================================================================

        /// <summary>
        /// sfx('alert'); await showContact(u,e,kind). Subtitles verbatim from showContact; the colour is the
        /// player's view (pm = u.team===G.side): we spotted first → blue (m1), mutual → yellow (m2),
        /// we were spotted first → red.
        /// </summary>
        IEnumerator ShowContact(ITurnUnit u, ITurnUnit e, Blindside.Combat.EncounterKind kind)
        {
            Sfx.Alert();
            bool pm = u.Team == side;
            string sub; int col;   // 1 blue · 2 yellow · 3 red
            switch (kind)
            {
                case Blindside.Combat.EncounterKind.SelfOnly:
                    sub = pm ? $"{KrOf(u)}が先に発見 — 奇襲攻撃が可能" : $"{KrOf(u)}に発見された — 敵の奇襲！";
                    col = pm ? 1 : 3;
                    break;
                case Blindside.Combat.EncounterKind.Mutual:
                    sub = $"相互遭遇 — 速度判定 ({KrOf(u)} {Blindside.Combat.EncounterResolver.SpeedOf(u)} : {KrOf(e)} {Blindside.Combat.EncounterResolver.SpeedOf(e)})";
                    col = 2;
                    break;
                default:
                    sub = pm ? $"{KrOf(e)}に発見された — 敵の先制攻撃！" : $"{KrOf(e)}が敵を捕捉 — 先制攻撃！";
                    col = pm ? 3 : 1;
                    break;
            }
            if (bridge != null) yield return bridge.PlayContact(col, sub);
            else yield return new WaitForSeconds(1.1f);
        }

        /// <summary>
        /// Port of askEngage(u,e): modal('敵を発見 — 攻撃しますか？','AMBUSH',…,[{攻撃,red},{無視して移動}]).
        /// Enter/Y → 攻撃, Escape/N → 無視して移動 (modal's kd: first / last button). Numbers come straight
        /// from CombatSystem.HitChance/HeadshotChance with the preemptive bonus, like the source.
        /// </summary>
        IEnumerator AskEngage(ITurnUnit u, ITurnUnit e, System.Action<bool> answer)
        {
            var tr = Blindside.Combat.CombatSystem.TraceLine(map, u.X, u.Y, e.X, e.Y);
            int c = Blindside.Combat.CombatSystem.HitChance(u, e, tr, preempt: true);
            int hs = Blindside.Combat.CombatSystem.HeadshotChance(u, e, tr, preempt: true);
            var w = u.Weapon;
            int dmg = Mathf.RoundToInt(w.damage * 1.4f);   // Math.round(w.dmg*1.4) — the rounding shoot() deals with
            string foe = ColorUtility.ToHtmlStringRGB(Foe);
            // .mbox .mb b → blue 22px (ModalRoutine); <b style="color:var(--foe)"> keeps the size, recoloured.
            // <small style="color:var(--dimp)"> / <small style="color:var(--dim)"> → 11px #6a7078 / #838a93.
            string body =
                $"{KrOf(u)} → <color=#{foe}>{KrOf(e)}</color> ({e.HP}/{e.MaxHP})\n" +
                $"先制攻撃: 命中 <b>{c}%</b> · ダメージ <b>{dmg}×{w.shots}</b>\n" +
                $"ヘッドショット確率 <b><color=#{foe}>{hs}%</color></b> <size=11><color=#6A7078>(先制攻撃ボーナス適用 · ダメージ×2)</color></size>" +
                (tr.Walls > 0 ? $" · 壁貫通 {tr.Walls}" : "") + "\n" +
                "<size=11><color=#838A93>攻撃すると、この場で移動を終了します。無視すると、描いた経路のまま移動を続けます。</color></size>";
            DialogResult res = DialogResult.None;
            yield return ModalRoutine("敵を発見 — 攻撃しますか？", "AMBUSH", body, "攻撃", "無視して移動", r => res = r, Foe);
            answer(res == DialogResult.Yes);
        }

        // ================================================================
        //  syncLabels(): `.ul-a` "!" for 1.8s after u.alertT
        //  .ul-a{font:700 30px/1 var(--fd);color:var(--acc);text-shadow:0 2px 0 #000;
        //        animation:bounce .32s ease-in-out infinite alternate}  @keyframes bounce{to{transform:translateY(-6px)}}
        // ================================================================
        readonly Dictionary<SimUnit, RectTransform> alertTags = new Dictionary<SimUnit, RectTransform>();

        float EndedTagLift(SimUnit u) =>
            endedTags.TryGetValue(u, out var t) && t != null && t.gameObject.activeSelf ? t.rect.height + 2f : 0f;

        void SyncAlertLabels()
        {
            if (endedTagRoot == null) return;
            var cam = Cam;
            float sf = canvas.scaleFactor > 0 ? canvas.scaleFactor : 1f;
            float now = Time.unscaledTime;
            if (units != null)
                foreach (var u in units)
                {
                    bool show = cam != null && u.Alive && u.View != null && Shown(u) && now - u.AlertT < 1.8f;
                    alertTags.TryGetValue(u, out var tag);
                    if (!show) { if (tag != null) tag.gameObject.SetActive(false); continue; }
                    var s = cam.WorldToScreenPoint(u.View.LabelPoint);
                    if (s.z < 0f) { if (tag != null) tag.gameObject.SetActive(false); continue; }
                    if (tag == null) { tag = BuildAlertTag(); alertTags[u] = tag; }
                    tag.gameObject.SetActive(true);
                    // ease-in-out ping-pong between 0 and -6px (screen y up here, so +6)
                    float p = Mathf.PingPong((now - u.AlertT) / .32f, 1f);
                    float bounce = 6f * (p * p * (3f - 2f * p));
                    // .ul column: .ul-a sits on top of .ul-t / the LOCK tag
                    tag.anchoredPosition = new Vector2(s.x / sf, s.y / sf + 4f + LockTagLift(u) + EndedTagLift(u) + StatusTagLift(u) + bounce);
                }
            foreach (var kv in alertTags)
                if (units == null || !ContainsUnit(kv.Key)) kv.Value.gameObject.SetActive(false);
        }

        RectTransform BuildAlertTag()
        {
            var rt = NewRect("AlertTag", endedTagRoot);
            rt.anchorMin = rt.anchorMax = Vector2.zero;
            rt.pivot = new Vector2(.5f, 0f);
            rt.sizeDelta = new Vector2(30, 32);
            var sh = AddText(rt, "!", 30, Color.black, true, TextAlignmentOptions.Bottom, "Shadow");   // text-shadow:0 2px 0 #000
            Stretch(sh.rectTransform); sh.rectTransform.anchoredPosition = new Vector2(0, -2);
            var t = AddText(rt, "!", 30, Acc, true, TextAlignmentOptions.Bottom, "Mark");
            Stretch(t.rectTransform);
            sh.raycastTarget = t.raycastTarget = false;
            return rt;
        }

        // ================================================================
        //  END TURN — #endBtn onclick + updateHUD's disabled / pulse
        // ================================================================
        void EndTurnClicked()
        {
            if (!PlayerCanAct()) return;
            if (turns.Phase == MatchPhase.Deploy) { StartOperation(); return; }   // [作戦開始] (.Deploy.cs)
            foreach (var u in units) if (u.Alive && u.Team == side) u.Ended = true;
            UnitsChanged?.Invoke();
            ClearUI();                 // endPhase(): clearUI()
            turns.EndPlayerTurn();
        }

        bool? endPulseOn;
        void SyncEndButton()
        {
            if (bridge == null || units == null) return;
            bool can = PlayerCanAct();
            if (bridge.endTurnButton != null) bridge.endTurnButton.interactable = can;
            // eb.classList.toggle('pulse', playerCanAct() && my.length>0 && my.every(u=>u.ended))
            int my = 0; bool all = true;
            foreach (var u in units) if (u.Alive && u.Team == side) { my++; if (!u.Ended) all = false; }
            bool on = can && my > 0 && all;
            if (bridge.endTurnPulse != null && endPulseOn != on) { endPulseOn = on; bridge.endTurnPulse.SetOn(on); }
        }

        // ================================================================
        //  Ended units — opacity .42 + "行動終了" tag while it's their own phase
        // ================================================================
        OperatorTeam? PhaseTeam()
        {
            if (turns == null) return null;
            if (turns.Phase == MatchPhase.Attack) return OperatorTeam.Attack;
            if (turns.Phase == MatchPhase.Defense) return OperatorTeam.Defense;
            return null;   // G.phase is never 'atk'/'def' during deploy/prep
        }

        bool EndedInOwnPhase(SimUnit u) => u.Ended && PhaseTeam() == u.Team;

        void SyncFigureOpacity()
        {
            if (units == null) return;
            foreach (var u in units)
                if (u.View != null && u.Alive) u.View.SetOpacity(EndedInOwnPhase(u) ? .42f : u.Ghost ? .5f : 1f);
        }

        readonly Dictionary<SimUnit, RectTransform> endedTags = new Dictionary<SimUnit, RectTransform>();

        void SyncEndedLabels()
        {
            if (hudRoot == null) return;
            var cam = Cam;
            float sf = canvas.scaleFactor > 0 ? canvas.scaleFactor : 1f;
            if (units != null)
                foreach (var u in units)
                {
                    bool show = cam != null && u.Alive && u.View != null && EndedInOwnPhase(u);
                    endedTags.TryGetValue(u, out var tag);
                    if (!show) { if (tag != null) tag.gameObject.SetActive(false); continue; }
                    var s = cam.WorldToScreenPoint(u.View.LabelPoint);
                    if (s.z < 0f) { if (tag != null) tag.gameObject.SetActive(false); continue; }
                    if (tag == null) { tag = BuildEndedTag(); endedTags[u] = tag; }
                    tag.gameObject.SetActive(true);
                    tag.anchoredPosition = new Vector2(s.x / sf, s.y / sf + 4f + LockTagLift(u));   // .ul-t sits above .ul-n
                }
            // Tags of units from a previous match.
            foreach (var kv in endedTags)
                if (units == null || !ContainsUnit(kv.Key)) kv.Value.gameObject.SetActive(false);
        }

        bool ContainsUnit(SimUnit u) { foreach (var x in units) if (x == u) return true; return false; }

        // .ul-t{font:700 9px var(--fk);background:var(--paper);color:var(--ink);padding:1px 6px}
        RectTransform BuildEndedTag()
        {
            var rt = NewRect("EndedTag", endedTagRoot);
            rt.anchorMin = rt.anchorMax = Vector2.zero;
            rt.pivot = new Vector2(.5f, 0f);                 // transform:translate(-50%,-100%)
            AddImage(rt, Paper);
            HLG(rt, 0, new RectOffset(6, 6, 1, 1), TextAnchor.MiddleCenter);
            var fit = rt.gameObject.AddComponent<ContentSizeFitter>();
            fit.horizontalFit = fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            AddText(rt, "行動終了", 9, Ink, true, TextAlignmentOptions.Center);
            return rt;
        }

        // ================================================================
        //  HUD construction (ended tags, path label, hint) — #log is HudLogView in the scene's HUD
        // ================================================================
        RectTransform hudRoot, endedTagRoot, pathLbl, hintRoot;
        TMP_Text pathLblText;
        KitHintBar hintView;                   // the hint bar itself (Blindside.UI; the old HintView is in UI/Legacy)

        void BuildMoveHud(Transform parent)
        {
            hudRoot = NewRect("MoveHud", parent);
            Stretch(hudRoot);

            endedTagRoot = NewRect("EndedTags", hudRoot);
            Stretch(endedTagRoot);

            // #pathLbl{transform:translate(14px,-130%);font:700 18px var(--fd);color:var(--acc);
            //          background:var(--ink);padding:2px 9px;border-left:3px solid var(--acc)}
            pathLbl = NewRect("PathLbl", hudRoot);
            pathLbl.anchorMin = pathLbl.anchorMax = Vector2.zero;
            pathLbl.pivot = new Vector2(0f, 0f);
            AddImage(pathLbl, Ink);
            HLG(pathLbl, 0, new RectOffset(12, 9, 2, 2), TextAnchor.MiddleLeft);
            var pfit = pathLbl.gameObject.AddComponent<ContentSizeFitter>();
            pfit.horizontalFit = pfit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var pb = NewRect("Border", pathLbl);
            LE(pb).ignoreLayout = true;
            pb.anchorMin = new Vector2(0, 0); pb.anchorMax = new Vector2(0, 1); pb.pivot = new Vector2(0, .5f);
            pb.anchoredPosition = Vector2.zero; pb.sizeDelta = new Vector2(3, 0);
            AddImage(pb, Acc);
            pathLblText = AddText(pathLbl, "", 18, Acc, true, TextAlignmentOptions.MidlineLeft, role: UITheme.TextRole.Display);
            pathLbl.gameObject.SetActive(false);

            hintView = KitHintBar.Build(hudRoot);
            hintView.CoroutineHost = this;
            hintRoot = hintView.Root;
        }

        // ---- #log ---- log(msg,cls) → HudLog (drawn by the scene HUD's HudLogView)
        void Log(string msg, string cls) => HudLog.Add(msg, HudLog.FromCls(cls));

        // ---- #hint (HintView) ----
        /// <summary>Port of setHint(main,sub,btns) — `on` is the button's `.hb.on` (accent background).</summary>
        void SetHint(string main, string sub, params (string id, string label, bool on)[] btns)
        {
            if (hintView == null) return;
            var list = new List<HintButton>(btns.Length);
            foreach (var b in btns) list.Add(new HintButton(b.id, b.label, b.on));
            hintView.Set(main, sub, list, HintAction);
        }

        void HideHint()
        {
            if (hintView != null) hintView.Hide();
        }

        /// <summary>Port of hintAction(id).</summary>
        void HintAction(string id)
        {
            if (id == "cancel") CancelMode();
            else if (id == "lock" && mode == Mode.Facing && uiUnit != null)
            {
                // const u=G.ui.unit; u.lockFacing=!u.lockFacing; enterFacing(u,G.ui.endAfter,G.ui.backup);
                var u = uiUnit;
                u.LockFacing = !u.LockFacing;
                EnterFacing(u, facingEndAfter, facingBackup);
                RefreshUnitPanel();   // the panel's "視界固定" tag
            }
        }

        // ================================================================
        //  modal(title,en,body,buttons) — #dialog .mbox
        // ================================================================
        enum DialogResult { None, Yes, No, Escape }

        DialogView dialogView;                 // the dialog itself (Blindside.UI)
        RectTransform dialogRoot;
        DialogResult dialogResult;

        void BuildDialog(Transform parent)
        {
            dialogView = DialogView.Build(parent, () => AnswerDialog(DialogResult.Yes), () => AnswerDialog(DialogResult.No));
            dialogView.CoroutineHost = this;
            dialogRoot = dialogView.Root;
        }

        /// <summary>modal(): shows the dialog, sets G.modal, and resolves on a button or key. `yesBg` is the
        /// first button's background (default --blu; `.mbtns button.red` passes --foe).</summary>
        IEnumerator ModalRoutine(string title, string en, string body, string yes, string no, System.Action<DialogResult> onResult, Color? yesBg = null)
        {
            modal = true;
            dialogResult = DialogResult.None;
            dialogView.Open(title, en, body, yes, no, yesBg);

            while (dialogResult == DialogResult.None && modal) yield return null;
            var r = dialogResult;
            dialogView.Close();
            modal = false;
            onResult?.Invoke(r == DialogResult.None ? DialogResult.No : r);
        }

        void AnswerDialog(DialogResult r) { if (modal && dialogResult == DialogResult.None) dialogResult = r; }

        // kd: Enter|y → buttons[0]; Escape → back to the command menu (see header); n → buttons[last].
        void DialogKeys()
        {
            var kb = Keyboard.current;
            if (kb == null) return;
            if (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame || kb.yKey.wasPressedThisFrame) AnswerDialog(DialogResult.Yes);
            else if (kb.escapeKey.wasPressedThisFrame) AnswerDialog(DialogResult.Escape);
            else if (kb.nKey.wasPressedThisFrame) AnswerDialog(DialogResult.No);
        }

        // ================================================================
        //  World helpers: reach overlay, path ribbon, end ring, #pathLbl
        // ================================================================
        GameObject reachGo, pathGo, pathEndGo;
        Mesh reachMesh, pathMesh;
        int builtReachKey = int.MinValue;
        string builtPathKey = "";

        void SyncMoveHelpers()
        {
            EnsureWorldHelpers();

            // syncOverlay(): cells with 0 < reach ≤ budget+.5, rgba(47,228,255,40/255)
            if (builtReachKey != moveReachKey)
            {
                builtReachKey = moveReachKey;
                // The deploy phase reuses it (ui.reach = deployMask(), budget 2): every valid drop cell is tinted.
                bool on = (mode == Mode.Move || mode == Mode.Deploy) && moveReach != null;
                reachGo.SetActive(on);
                if (on) { SetMatColor(reachGo.GetComponent<MeshRenderer>().sharedMaterial, mode == Mode.Deploy ? ReachDeploy : moveIsDrone ? ReachDrone : ReachUnit); BuildReachMesh(); }   // ui.isDrone?[124,240,200]:[47,228,255]
            }

            // syncUIHelpers(): ribbonGeo(P,.1,.07) + RingGeometry(.3,.4) at the end + #pathLbl
            var P = movePath != null && movePath.Count > 1 ? movePath : null;
            string k = P != null ? P.Count + ":" + P[P.Count - 1].x.ToString("0.00") + P[P.Count - 1].y.ToString("0.00") : "";
            if (k != builtPathKey)
            {
                builtPathKey = k;
                if (P != null) BuildRibbon(pathMesh, P, .1f, .07f); else pathMesh.Clear();
            }
            pathGo.SetActive(P != null);
            pathEndGo.SetActive(P != null);
            if (P != null)
            {
                var l = P[P.Count - 1];
                pathEndGo.transform.position = GridSpace.GridToWorld(l.x, l.y, .07f);
            }

            var cam = Cam;
            if (P != null && pathLbl != null && cam != null)
            {
                var l = P[P.Count - 1];
                var s = cam.WorldToScreenPoint(GridSpace.GridToWorld(l.x, l.y, 0f));
                float sf = canvas.scaleFactor > 0 ? canvas.scaleFactor : 1f;
                pathLbl.gameObject.SetActive(true);
                pathLblText.text = $"{MovementSystem.PathLen(P).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture)}" +
                                   $"<size=10><color=#FFFFFF99> / {moveBudget.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture)}m</color></size>";
                LayoutRebuilder.ForceRebuildLayoutImmediate(pathLbl);
                // translate(14px,-130%): left edge 14px right of the point, bottom edge .3·h above it
                pathLbl.anchoredPosition = new Vector2(s.x / sf + 14f, s.y / sf + .3f * pathLbl.rect.height);
            }
            else if (pathLbl != null) pathLbl.gameObject.SetActive(false);
        }

        void EnsureWorldHelpers()
        {
            if (reachGo != null) return;
            var root = new GameObject("MoveHelpers").transform;
            root.SetParent(transform, false);

            reachMesh = new Mesh { name = "Reach" };
            reachGo = MakeMeshObject("Reach", root, reachMesh, new Color32(47, 228, 255, 40), 3002);
            pathMesh = new Mesh { name = "PathRibbon" };
            pathGo = MakeMeshObject("PathRibbon", root, pathMesh, new Color(PathYellow.r, PathYellow.g, PathYellow.b, .95f), 3006);
            var ring = BuildRingMesh(.3f, .4f, 32);
            pathEndGo = MakeMeshObject("PathEnd", root, ring, PathYellow, 3006);
            reachGo.SetActive(false); pathGo.SetActive(false); pathEndGo.SetActive(false);
        }

        static GameObject MakeMeshObject(string name, Transform parent, Mesh mesh, Color color, int queue)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
            var mat = new Material(shader);
            mat.SetFloat("_Surface", 1f);
            mat.SetFloat("_Blend", 0f);
            mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetFloat("_ZWrite", 0f);
            mat.SetFloat("_Cull", 0f);   // side:THREE.DoubleSide
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);
            mat.renderQueue = queue;
            mr.sharedMaterial = mat;
            return go;
        }

        void BuildReachMesh()
        {
            var verts = new List<Vector3>(); var tris = new List<int>();
            int W = MapGrid.Width, H = map.Height;
            for (int k = 0; k < W * H && k < moveReach.Length; k++)
            {
                if (!(moveReach[k] <= moveBudget + .5f && moveReach[k] > 0)) continue;
                int x = k % W, y = k / W, v = verts.Count;
                verts.Add(GridSpace.GridToWorld(x - .5f, y - .5f, .03f));
                verts.Add(GridSpace.GridToWorld(x + .5f, y - .5f, .03f));
                verts.Add(GridSpace.GridToWorld(x + .5f, y + .5f, .03f));
                verts.Add(GridSpace.GridToWorld(x - .5f, y + .5f, .03f));
                tris.Add(v); tris.Add(v + 1); tris.Add(v + 2); tris.Add(v); tris.Add(v + 2); tris.Add(v + 3);
            }
            reachMesh.Clear();
            reachMesh.SetVertices(verts); reachMesh.SetTriangles(tris, 0); reachMesh.RecalculateBounds();
        }

        /// <summary>ribbonGeo(pts,w,y): one flat quad per leg, w wide, at height y.</summary>
        static void BuildRibbon(Mesh mesh, List<Vector2> pts, float w, float h)
        {
            var verts = new List<Vector3>(); var tris = new List<int>();
            for (int i = 0; i < pts.Count - 1; i++)
            {
                Vector2 a = pts[i], b = pts[i + 1];
                float dx = b.x - a.x, dy = b.y - a.y, l = Mathf.Sqrt(dx * dx + dy * dy);
                if (l == 0) l = 1;
                float nx = -dy / l * w / 2, ny = dx / l * w / 2;
                int v = verts.Count;
                verts.Add(GridSpace.GridToWorld(a.x + nx, a.y + ny, h));
                verts.Add(GridSpace.GridToWorld(a.x - nx, a.y - ny, h));
                verts.Add(GridSpace.GridToWorld(b.x + nx, b.y + ny, h));
                verts.Add(GridSpace.GridToWorld(b.x - nx, b.y - ny, h));
                tris.Add(v); tris.Add(v + 1); tris.Add(v + 2); tris.Add(v + 1); tris.Add(v + 3); tris.Add(v + 2);
            }
            mesh.Clear();
            mesh.SetVertices(verts); mesh.SetTriangles(tris, 0); mesh.RecalculateBounds();
        }

        /// <summary>RingGeometry(inner,outer,segments) laid flat on the ground (local XZ).</summary>
        static Mesh BuildRingMesh(float inner, float outer, int segments)
        {
            var verts = new Vector3[segments * 2]; var tris = new int[segments * 6];
            for (int i = 0; i < segments; i++)
            {
                float a = i * Mathf.PI * 2f / segments;
                var d = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                verts[i * 2] = d * inner; verts[i * 2 + 1] = d * outer;
            }
            for (int i = 0, t = 0; i < segments; i++)
            {
                int i0 = i * 2, o0 = i0 + 1, i1 = ((i + 1) % segments) * 2, o1 = i1 + 1;
                tris[t++] = i0; tris[t++] = o1; tris[t++] = i1;
                tris[t++] = i0; tris[t++] = o0; tris[t++] = o1;
            }
            var mesh = new Mesh { name = "PathEndRing", vertices = verts, triangles = tris };
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
