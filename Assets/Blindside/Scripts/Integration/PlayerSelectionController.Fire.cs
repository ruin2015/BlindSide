// FIRE / VIEW / WAIT commands for PlayerSelectionController. Ports, from blindside_source:
//   05_ui_input_intro.js — doCommand's face/fire/wait branches (~171-172, 185), enterFacing (~187),
//                          endUnit (~192), afterAction (~193), hintAction (~200), cancelMode (~204),
//                          onClick's facing/fire branches (~320-322), updateAim (~341), updateTip (~348),
//                          and bindInput's F key (~385)
//   04_gameplay_combat_ai.js — fireCast (~105), fireDirection (~119), breakPanel (~214)
//   03_renderer_threejs.js — syncUIHelpers' aim ribbon + aimDot (~227-228), syncLabels' `<em>LOCK</em>` (~277)
//   00_markup_and_styles.html — #tip (~169-170), .ul-n / .ul-n em (~301-302)
//
// Angles (facing, aim) are all computed in grid space (x, y as-is); GridSpace.GridToWorld (z = -y) is only
// applied where something is drawn.
//
// Not connected (by request, or no object to act on yet):
//   - sfx() calls: no audio system yet (the gadget shot's is in DroneOps.DestroyGadget).
// fireCast's enemy-gadget stop (G.cams / G.drones) goes through DroneOps (the player's fire mode ends the unit's turn,
// unlike the click-to-shoot branch in PlayerSelectionController.Drone.cs).
// updateTip names the unit in the line of fire only when it's the hostage or shownToPlayer(), and
// fireDirection's addMark(other(u.team),…,'shot'|'breach') goes to the enemy team's MarkBoard memory
// (never drawn for the player).
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Blindside.Combat;
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
        CombatSystem combat;

        // ---- G.ui for mode 'facing' ----
        bool facingEndAfter;      // ui.endAfter
        float facingBackup;       // ui.backup

        // ---- G.ui for mode 'fire' ----
        float fireAim;            // ui.aim
        float fireBackup;         // ui.backup
        FireCastResult fireCast;  // ui.cast
        bool hasFireCast;

        static readonly Color AimRed = new Color32(0xff, 0x3b, 0x54, 0xff);   // 0xff3b54

        /// <summary>fireCast(u,ang)'s return: {end, walls, unit?, gadget?, stop?, reinforced?, breach?}.</summary>
        public struct FireCastResult
        {
            public Vector2 end;
            public int walls;
            public ICombatUnit unit;
            public GadgetTarget? gadget;   // an enemy drone / camera in the line of fire
            public bool stop, reinforced;
            public int breachPanel;   // -1: none
        }

        static float AngNorm(float a)
        {
            while (a > Mathf.PI) a -= 2f * Mathf.PI;
            while (a < -Mathf.PI) a += 2f * Mathf.PI;
            return a;
        }

        static int JsRound(float v) => Mathf.FloorToInt(v + .5f);
        static float Hypot(float dx, float dy) => Mathf.Sqrt(dx * dx + dy * dy);

        // ================================================================
        //  fireCast(u,ang)
        // ================================================================
        /// <summary>
        /// Port of fireCast(u,ang): march from .3m out to range+4 in .08m steps. The first enemy (or the living
        /// hostage) within .32 stops it; off-map/'hard' cells stop it; a reinforced wall (mirrored or not) stops it
        /// (reinforced:true); a breaching shotgun meeting its first ordinary wall within 2.4m blows it
        /// (breach); any other wall is shot through (walls++).
        /// </summary>
        public FireCastResult FireCast(SimUnit u, float ang)
        {
            var w = u.Weapon;
            float maxR = w.range + 4f, cs = Mathf.Cos(ang), sn = Mathf.Sin(ang);
            int W = MapGrid.Width, H = map.Height;
            int s = JsRound(u.Y) * W + JsRound(u.X);
            int walls = 0, last = -1;
            var gs = droneOps != null ? droneOps.EnemyGadgets(u.Team, true) : null;   // const gs=enemyGadgets(u.team) (+ discovered electric fields)
            for (float d = .3f; d <= maxR; d += .08f)
            {
                float px = u.X + cs * d, py = u.Y + sn * d;
                var end = new Vector2(px, py);
                foreach (var e in units)
                    if (e.Alive && e.Team != u.Team && Hypot(e.X - px, e.Y - py) < .32f)
                        return new FireCastResult { end = new Vector2(e.X, e.Y), walls = walls, unit = e, breachPanel = -1 };
                foreach (var h in hostages)
                    if (h.Alive && !h.Extracted && Hypot(h.X - px, h.Y - py) < .32f)
                        return new FireCastResult { end = new Vector2(h.X, h.Y), walls = walls, unit = h, breachPanel = -1 };
                if (gs != null)
                    foreach (var gg in gs)
                        if (Hypot(gg.X - px, gg.Y - py) < .3f)
                            return new FireCastResult { end = new Vector2(gg.X, gg.Y), walls = walls, gadget = gg, breachPanel = -1 };
                int cx = JsRound(px), cy = JsRound(py), k = cy * W + cx;
                if (k == last || k == s) continue;
                last = k;
                bool inside = cx >= 0 && cy >= 0 && cx < W && cy < H;           // TL(): 'void' off the map
                var t = inside ? map.TileAt(cx, cy) : TileType.Hard;
                if (!inside || t == TileType.Hard) return new FireCastResult { end = end, walls = walls, stop = true, breachPanel = -1 };
                if (t == TileType.Wall)
                {
                    int pi = map.PanelIndexAt[k];
                    if (pi >= 0)
                    {
                        var p = map.Panels[pi];
                        // reinforced (with or without a mirror on it) stops the round; a mirror on a plain wall is shot through
                        if (p.reinforced) return new FireCastResult { end = end, walls = walls, stop = true, reinforced = true, breachPanel = -1 };
                        if (w.breach && walls == 0 && d <= 2.4f) return new FireCastResult { end = new Vector2(cx, cy), walls = walls, breachPanel = pi };
                    }
                    walls++;
                }
            }
            return new FireCastResult { end = new Vector2(u.X + cs * maxR, u.Y + sn * maxR), walls = walls, breachPanel = -1 };
        }

        // ================================================================
        //  fireDirection(u,ang)
        // ================================================================
        IEnumerator FireDirection(SimUnit u, float ang)
        {
            var c = FireCast(u, ang);
            u.Facing = ang;
            // knocking out an enemy camera / drone costs no action (as the click-to-shoot branch already does)
            if (!c.gadget.HasValue || c.unit != null) u.Acted = true;
            if (c.unit != null)
            {
                // the shooter calls the fight on the squad radio first (a beat, then the exchange)
                if (CalloutEngage(u, c.unit)) yield return new WaitForSecondsRealtime(EngageCallHold);
                // exchange(u,c.unit,true): preemptive (×1.4) shot, then return fire if it lives and can shoot back.
                yield return combat.Exchange(u, c.unit, true);
            }
            else if (c.gadget.HasValue)
            {
                yield return droneOps.DestroyGadget(u, c.gadget.Value);   // else if(c.gadget) await destroyGadget(u,c.gadget)
            }
            else
            {
                int shots = CombatModifiers.ShotsOf(u);
                for (int i = 0; i < shots; i++)
                {
                    // fxTracer(u,{rx:c.end.x+(rand-.5)*.3, ry:c.end.y+(rand-.5)*.3},true)
                    var to = GridSpace.GridToWorld(c.end.x + (Random.value - .5f) * .3f, c.end.y + (Random.value - .5f) * .3f, .5f);
                    CombatFx.Tracer(u.View != null ? u.View.TorsoPoint : GridSpace.GridToWorld(u.X, u.Y, .5f), u.Facing, to, true,
                                    u.View != null ? u.View.MuzzlePoint : (Vector3?)null);
                    yield return new WaitForSeconds(.11f);
                }
                var gear = CombatModifiers.GearOf(u);   // a サプレッサー leaves no 銃声 mark
                if (gear == null || !gear.silenced) marks?.Add(Blindside.Vision.MarkBoard.Other(u.Team), u.X, u.Y, Blindside.Vision.MarkKind.Shot);
                if (c.breachPanel >= 0)
                {
                    var bp = map.Panels[c.breachPanel];
                    BreakPanel(c.breachPanel, TileType.Rubble);
                    Log($"{KrOf(u)}: 散弾銃で壁を破壊しました", "a");
                    marks?.Add(Blindside.Vision.MarkBoard.Other(u.Team), bp.centerX, bp.centerY, Blindside.Vision.MarkKind.Breach);
                }
                else Log($"{KrOf(u)}: 射撃 — 命中した目標なし", "");
            }
        }

        /// <summary>breakPanel(p,st): MapGrid.BreakPanel (stamps the cells, clears reinforced/electrified/mirror,
        /// raises Changed → bumpMap + MapWallSync's refreshPanel) plus boom(c.x,c.y,0xffb13b,16) per cell.</summary>
        void BreakPanel(int panelId, TileType state, bool blast = false)
        {
            var p = map.Panels[panelId];
            map.BreakPanel(panelId, state);
            // particles: the wall's dust and chips (a shotgun); `blast` (a grenade) adds the flash, sparks and fire
            foreach (var cell in p.cells) FxBlast.WallBreak(GridSpace.GridToWorld(cell.x, cell.y, .9f), BreachAbilityPresenter.FxWall, blast);
        }

        // ================================================================
        //  doCommand's face / fire / wait branches, enterFacing, endUnit
        // ================================================================
        void StartFire(SimUnit u)
        {
            // G.ui={mode:'fire',unit:u,panelUnit:u,aim:u.facing,backup:u.facing,cast:fireCast(u,u.facing)}
            mode = Mode.Fire; uiUnit = u; panelUnit = u;
            fireAim = u.Facing; fireBackup = u.Facing;
            fireCast = FireCast(u, u.Facing); hasFireCast = true;
            NotifySelection();
            SetHint("攻撃方向を選択してください", "マウスで狙いを定め、左クリックで射撃 · 強化壁を除く壁は貫通 (命中率低下)", ("cancel", "ESC キャンセル", false));
        }

        /// <summary>Port of enterFacing(u,endAfter,keepBackup).</summary>
        void EnterFacing(SimUnit u, bool endAfter, float? keepBackup = null)
        {
            float bk = keepBackup ?? u.Facing;
            mode = Mode.Facing; uiUnit = u; panelUnit = u;
            facingEndAfter = endAfter; facingBackup = bk;
            HideMenu();
            NotifySelection();
            SetHint(endAfter ? "行動終了前に視野角を設定してください" : "視野角を調整してください",
                "マウスで方向を指定 · 左クリックで確定 · ESC " + (endAfter ? "現在の向きで終了" : "キャンセル"),
                ("lock", $"視界固定 {(u.LockFacing ? "ON" : "OFF")} [F]", u.LockFacing), ("cancel", "ESC", false));
        }

        /// <summary>Port of endUnit(u).</summary>
        void EndUnit(SimUnit u)
        {
            u.Ended = true; u.MoveLeft = 0;
            Deselect();
            UnitsChanged?.Invoke();   // updateHUD()
            if (AllEnded() && !TutorialGate.Active) { Log("全隊員が行動を終えました。ターンを終了します。", "w"); StartCoroutine(AutoEndTurn()); }
        }

        bool AllEnded()
        {
            if (units == null) return false;
            foreach (var x in units) if (x.Alive && x.Team == side && !x.Ended) return false;
            return true;
        }

        /// <summary>Every unit has ended: after a short beat, the turn ends by itself (as if END TURN were pressed),
        /// unless something changed meanwhile (an ability gave a unit its move back, a modal is up, the match ended).</summary>
        IEnumerator AutoEndTurn()
        {
            yield return new WaitForSecondsRealtime(.6f);
            if (turns == null || turns.Over || turns.Phase == MatchPhase.Deploy || turns.Phase == MatchPhase.Prep) yield break;
            if (!AllEnded() || !PlayerCanAct()) yield break;
            EndTurnClicked();
        }

        /// <summary>updateHUD()'s `if(G.ui.panelUnit && #unitPanel visible) showUnitPanel(G.ui.panelUnit,true)`.</summary>
        void RefreshUnitPanel()
        {
            if (panelUnit != null && panelRoot != null && panelRoot.gameObject.activeSelf) ShowUnitPanel(panelUnit, true);
        }

        // ================================================================
        //  onClick's facing / fire branches
        // ================================================================
        /// <returns>true when the click was consumed by the facing/fire mode.</returns>
        bool OnClickAimModes()
        {
            if (mode == Mode.Facing)
            {
                var u = uiUnit;
                if (facingEndAfter) EndUnit(u); else SelectUnit(u);
                return true;
            }
            if (mode == Mode.Fire)
            {
                var u = uiUnit; float ang = fireAim;
                busy = true; HideHint(); HideTip();
                mode = Mode.Busy; hasFireCast = false;   // G.ui={mode:'busy',unit:u}
                StartCoroutine(FireRoutine(u, ang));
                return true;
            }
            return false;
        }

        IEnumerator FireRoutine(SimUnit u, float ang)
        {
            bool acted = u.Acted;
            var aimed = TutorialGate.Active ? FireCast(u, ang) : default;   // what the shot went at (the tutorial judges it)
            if (TutorialGate.Active) { TutorialGate.LastShotAngle = ang; TutorialGate.LastShotWalls = aimed.walls; }
            yield return FireDirection(u, ang);
            busy = false;
            UnitsChanged?.Invoke();
            if (turns == null || turns.Over) yield break;
            // a gadget shot left the action unspent: the unit carries on
            if (!u.Acted && !acted && u.Alive) SelectUnit(u);
            else EndUnit(u);
            TutorialGate.RaiseShotFinished(u, aimed.unit, aimed.gadget.HasValue);
        }

        // ================================================================
        //  updateAim / updateTip
        // ================================================================
        Vector2 lastAimMouse = new Vector2(float.NaN, float.NaN);

        /// <summary>pointermove → updateAim(): only re-aims when the pointer actually moved.</summary>
        void PointerMoveAim(Vector2 pos)
        {
            if (pos == lastAimMouse) return;
            lastAimMouse = pos;
            UpdateAim(pos);
        }

        void UpdateAim(Vector2 pos)
        {
            if (!BattlefieldCamera.GroundAt(pos, out var h)) return;   // const h=G.hover; if(!h) return;
            var u = uiUnit;
            if (u == null) return;
            if (mode == Mode.Throw) { UpdateThrow(h.fx, h.fy); return; }   // (.Grenade.cs)
            if (mode == Mode.Facing)
            {
                float a = Mathf.Atan2(h.fy - u.Y, h.fx - u.X);
                if (Mathf.Abs(AngNorm(a - u.Facing)) > .01f) u.Facing = a;
            }
            else if (mode == Mode.Fire)
            {
                // const e=pickUnit(G.mouse.x,G.mouse.y); a = enemy under the cursor ? toward it : toward the ground point
                var e = PickUnit(pos);
                float a = e != null && e.Team != side ? Mathf.Atan2(e.Y - u.Y, e.X - u.X) : Mathf.Atan2(h.fy - u.Y, h.fx - u.X);
                fireAim = a; fireCast = FireCast(u, a); hasFireCast = true;
                if (Mathf.Abs(AngNorm(a - u.Facing)) > .01f) u.Facing = a;
                UpdateTip(pos);
            }
        }

        static string B(string v) => $"<size=16><color=#FFC81E><b>{v}</b></color></size>";   // #tip b
        static string R(string v) => $"<color=#FF8A98>{v}</color>";                            // #tip .r

        void UpdateTip(Vector2 pos)
        {
            if (tipRoot == null) return;
            if (mode != Mode.Fire || !hasFireCast) { HideTip(); return; }
            var c = fireCast; var u = uiUnit;
            string html;
            if (c.unit != null && (c.unit.IsHostage || Shown(c.unit)))
            {
                var path = new ShotPath(false, c.walls);
                int ch = CombatSystem.HitChance(u, c.unit, path, true);
                if (c.unit.IsHostage)
                    html = $"{R("⚠ 人質が射線上にいます！")}\n命中 {B(ch + "%")}";
                else
                {
                    var t = (SimUnit)c.unit;
                    html = $"{R(KrOf(t))} {t.HP}/{t.MaxHP}\n先制命中 {B(ch + "%")} · {JsRound(CombatModifiers.BaseDamage(u, 0f) * 1.4f)}×{CombatModifiers.ShotsOf(u)}" +
                           $"\nヘッドショット {B(CombatSystem.HeadshotChance(u, c.unit, path, true) + "%")}";
                }
            }
            else if (c.gadget.HasValue) html = "敵の装備 — 破壊";
            else if (c.breachPanel >= 0) html = "散弾射撃 — 壁を破壊";
            else html = "目標未確認" + (c.reinforced ? " · 強化壁で停止" : "");
            if (c.walls > 0) html += $"\n壁貫通 ×{c.walls} (命中 -{c.walls * 22}%)";

            tipText.text = html;
            tipRoot.gameObject.SetActive(true);
            LayoutRebuilder.ForceRebuildLayoutImmediate(tipRoot);
            // tip.style.left=(G.mouse.x+18)+'px'; tip.style.top=(G.mouse.y+14)+'px'
            float sf = canvas.scaleFactor > 0 ? canvas.scaleFactor : 1f;
            tipRoot.anchoredPosition = new Vector2(pos.x / sf + 18f, pos.y / sf - 14f);
        }

        void HideTip() { if (tipRoot != null) tipRoot.gameObject.SetActive(false); }

        // ================================================================
        //  #tip — built with the rest of the selection HUD
        // ================================================================
        RectTransform tipRoot;
        TMP_Text tipText;

        void BuildTip(Transform parent)
        {
            // #tip{position:fixed;z-index:40;pointer-events:none;background:var(--ink);color:#fff;padding:6px 10px;
            //      font-size:12px;border-left:4px solid var(--foe);white-space:nowrap;box-shadow:var(--shadow)}
            tipRoot = NewRect("Tip", parent);
            tipRoot.anchorMin = tipRoot.anchorMax = Vector2.zero;
            tipRoot.pivot = new Vector2(0f, 1f);
            AddImage(tipRoot, Ink);
            var sh = tipRoot.gameObject.AddComponent<Shadow>();
            sh.effectColor = new Color(0, 0, 0, .5f); sh.effectDistance = new Vector2(0, -10);
            HLG(tipRoot, 0, new RectOffset(10 + 4, 10, 6, 6), TextAnchor.UpperLeft);
            var fit = tipRoot.gameObject.AddComponent<ContentSizeFitter>();
            fit.horizontalFit = fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var border = NewRect("Border", tipRoot);
            LE(border).ignoreLayout = true;
            border.anchorMin = new Vector2(0, 0); border.anchorMax = new Vector2(0, 1); border.pivot = new Vector2(0, .5f);
            border.anchoredPosition = Vector2.zero; border.sizeDelta = new Vector2(4, 0);
            AddImage(border, Foe);
            tipText = AddText(tipRoot, "", 12, Color.white, false, TextAlignmentOptions.TopLeft, role: UITheme.TextRole.Body);
            UseSymbolFont(tipText);   // "⚠" isn't in LiberationSans; the symbol font falls back to it for the rest
            tipRoot.gameObject.SetActive(false);
        }

        // ================================================================
        //  syncUIHelpers(): aim line (red, dashed) + aimDot ring at ui.cast.end
        // ================================================================
        GameObject aimGo, aimDotGo;
        Mesh aimMesh;
        string builtAimKey = "";

        void SyncAimHelpers()
        {
            if (aimGo == null)
            {
                var root = new GameObject("AimHelpers").transform;
                root.SetParent(transform, false);
                aimMesh = new Mesh { name = "AimLine" };
                // R.aim: mkBasic(0xff3b54,{transparent:true,opacity:.85,depthWrite:false}), renderOrder 6
                aimGo = MakeMeshObject("Aim", root, aimMesh, new Color(AimRed.r, AimRed.g, AimRed.b, .85f), 3006);
                // R.aimDot: RingGeometry(.22,.32,32), 0xff3b54
                aimDotGo = MakeMeshObject("AimDot", root, BuildRingMesh(.22f, .32f, 32), AimRed, 3006);
                aimGo.SetActive(false); aimDotGo.SetActive(false);
            }

            bool A = mode == Mode.Fire && hasFireCast && uiUnit != null;
            aimGo.SetActive(A); aimDotGo.SetActive(A);
            if (!A) { builtAimKey = ""; return; }

            var u = uiUnit; var end = fireCast.end;
            string key = $"{u.X:0.000},{u.Y:0.000}>{end.x:0.000},{end.y:0.000}";
            if (key != builtAimKey)
            {
                builtAimKey = key;
                BuildDashedLine(aimMesh, new Vector2(u.X, u.Y), end, .05f, .08f, .28f, .16f);
            }
            aimDotGo.transform.position = GridSpace.GridToWorld(end.x, end.y, .08f);
            // const s=1+.15*Math.sin(t/90) — t in ms
            float s = 1f + .15f * Mathf.Sin(Time.unscaledTime * 1000f / 90f);
            aimDotGo.transform.localScale = new Vector3(s, s, s);
        }

        /// <summary>A flat dashed ribbon a→b (grid coords), w wide at height h: `dash` on, `gap` off.</summary>
        static void BuildDashedLine(Mesh mesh, Vector2 a, Vector2 b, float w, float h, float dash, float gap)
        {
            var verts = new List<Vector3>(); var tris = new List<int>();
            float dx = b.x - a.x, dy = b.y - a.y, l = Mathf.Sqrt(dx * dx + dy * dy);
            if (l > 1e-4f)
            {
                float ux = dx / l, uy = dy / l, nx = -uy * w / 2f, ny = ux * w / 2f;
                for (float d0 = 0f; d0 < l; d0 += dash + gap)
                {
                    float d1 = Mathf.Min(l, d0 + dash);
                    Vector2 p0 = new Vector2(a.x + ux * d0, a.y + uy * d0), p1 = new Vector2(a.x + ux * d1, a.y + uy * d1);
                    int v = verts.Count;
                    verts.Add(GridSpace.GridToWorld(p0.x + nx, p0.y + ny, h));
                    verts.Add(GridSpace.GridToWorld(p0.x - nx, p0.y - ny, h));
                    verts.Add(GridSpace.GridToWorld(p1.x + nx, p1.y + ny, h));
                    verts.Add(GridSpace.GridToWorld(p1.x - nx, p1.y - ny, h));
                    tris.Add(v); tris.Add(v + 1); tris.Add(v + 2); tris.Add(v + 1); tris.Add(v + 3); tris.Add(v + 2);
                }
            }
            mesh.Clear();
            mesh.SetVertices(verts); mesh.SetTriangles(tris, 0); mesh.RecalculateBounds();
        }

        // ================================================================
        //  syncLabels(): `.ul-n` = key + ` <em>LOCK</em>` for the player's units with lockFacing
        // ================================================================
        readonly Dictionary<SimUnit, RectTransform> lockTags = new Dictionary<SimUnit, RectTransform>();

        /// <summary>Screen height (canvas units) the lock tag takes above a unit's label point, so the
        /// "行動終了" tag (.ul-t) stacks above it like the source's .ul column.</summary>
        float LockTagLift(SimUnit u) =>
            lockTags.TryGetValue(u, out var t) && t != null && t.gameObject.activeSelf ? t.rect.height + 2f : 0f;

        void SyncLockLabels()
        {
            if (endedTagRoot == null) return;
            var cam = Cam;
            float sf = canvas.scaleFactor > 0 ? canvas.scaleFactor : 1f;
            if (units != null)
                foreach (var u in units)
                {
                    bool show = cam != null && hasSide && u.Alive && u.View != null && u.LockFacing && u.Team == side;
                    lockTags.TryGetValue(u, out var tag);
                    if (!show) { if (tag != null) tag.gameObject.SetActive(false); continue; }
                    var s = cam.WorldToScreenPoint(u.View.LabelPoint);
                    if (s.z < 0f) { if (tag != null) tag.gameObject.SetActive(false); continue; }
                    if (tag == null) { tag = BuildLockTag(u.Op != null ? u.Op.key : u.Id); lockTags[u] = tag; }
                    tag.gameObject.SetActive(true);
                    tag.anchoredPosition = new Vector2(s.x / sf, s.y / sf + 4f);
                }
            foreach (var kv in lockTags)
                if (units == null || !ContainsUnit(kv.Key)) kv.Value.gameObject.SetActive(false);
        }

        // .ul-n{font:700 10px var(--fd);letter-spacing:.1em;color:#fff;background:rgba(14,16,20,.88);
        //       padding:1px 6px 1px 5px;border-left:3px solid var(--blu)} .ul-n em{font:700 8px var(--fm);color:var(--acc)}
        RectTransform BuildLockTag(string key)
        {
            var rt = NewRect("LockTag", endedTagRoot);
            rt.anchorMin = rt.anchorMax = Vector2.zero;
            rt.pivot = new Vector2(.5f, 0f);
            AddImage(rt, new Color32(14, 16, 20, (byte)(.88f * 255)));
            HLG(rt, 0, new RectOffset(5 + 3, 6, 1, 1), TextAnchor.MiddleCenter);
            var fit = rt.gameObject.AddComponent<ContentSizeFitter>();
            fit.horizontalFit = fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var b = NewRect("Border", rt);
            LE(b).ignoreLayout = true;
            b.anchorMin = new Vector2(0, 0); b.anchorMax = new Vector2(0, 1); b.pivot = new Vector2(0, .5f);
            b.anchoredPosition = Vector2.zero; b.sizeDelta = new Vector2(3, 0);
            AddImage(b, Blu);
            var t = AddText(rt, $"{key} <size=8><color=#FFC81E>LOCK</color></size>", 10, Color.white, true, TextAlignmentOptions.Center);
            t.characterSpacing = 10;
            return rt;
        }
    }
}
