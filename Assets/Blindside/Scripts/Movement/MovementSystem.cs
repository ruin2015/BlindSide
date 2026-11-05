// Free-path movement, ported function-for-function from blindside_source:
//   01_core_data_map_vision.js — blockCell/freeAt/segFree (~117-125), blockedSet/dijkstra/pathLen/
//                                truncate/bodyObstacles/bodyClear/planPath/spotClear (~197-236)
//   04_gameplay_combat_ai.js   — followHostage/moveAlong (~138-178)
//
// Scope:
//   - enemyVisible(team,e) is VisionSystem.EnemyVisible (unit sight only, no gadget term): only seen enemies
//     count for blockedSet/bodyObstacles/spotClear, and an unseen mover isn't "shown" (no camera follow,
//     near-instant tween). Without a Vision every enemy counts as seen, as before.
//   - A blocked step sets the blocker's revealT (+3s) and addMark(u.team,…,'drone') like the source.
//   - moveAlong's encounter checks (trigM/trigE, the mark-only branch, resolveEncounter) run here for every
//     mover — the player's MOVE and EnemyAI's aiMoveTo both walk through this one MoveAlong. They need Vision
//     (sees) and Combat (canShoot's traceLine + exchange); without either the walk skips them.
//     resolveEncounter's showContact/askEngage are the ShowContact/AskEngage hooks (UI stays with the owner).
//   - sfx('step') isn't played (only sfx('alert') is ported, in the ShowContact hook).
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Blindside.Combat;
using Blindside.Data;
using Blindside.Map;
using Blindside.Turns;
using Blindside.Vision;

namespace Blindside.Movement
{
    /// <summary>The hostage side of setEscort/followHostage: a position that can be dragged along
    /// behind its escort and the escort's breadcrumb trail (`h.trail`).</summary>
    public interface IEscortableHostage
    {
        List<Vector2> Trail { get; }
        void SetPosition(float x, float y);
    }

    public class MovementSystem
    {
        /// <summary>UR — unit body radius, freeAt's default clearance.</summary>
        public const float UR = 0.28f;
        const float Inf = 1e9f;

        readonly MapGrid map;
        readonly IReadOnlyList<ITurnUnit> units;
        readonly ITurnHostage hostage;

        /// <summary>G.side — the human player's team (null: no human side, i.e. G.autoplay).</summary>
        public OperatorTeam? PlayerSide;
        /// <summary>log(msg,cls) — cls is '', 'a', 'f' or 'w' like the source.</summary>
        public System.Action<string, string> Log;
        /// <summary>checkWin() — evaluates the win conditions; true once the match is over.</summary>
        public System.Func<bool> CheckWin;
        /// <summary>G.over — read-only.</summary>
        public System.Func<bool> IsOver;
        /// <summary>panTo(x,y,ms) on the battlefield camera.</summary>
        public System.Action<float, float, float> PanTo;
        /// <summary>After every step (position + moveLeft changed) and once when the walk ends — the
        /// HUD's cue that the mover's "移動 X.Xm" changed.</summary>
        public System.Action<ITurnUnit> OnStep;
        /// <summary>enemyVisible() source (null: every enemy counts as seen).</summary>
        public VisionSystem Vision;
        /// <summary>G.marks (null: addMark is skipped).</summary>
        public MarkBoard Marks;
        /// <summary>exchange() for resolveEncounter (null: no encounter checks while walking).</summary>
        public CombatSystem Combat;
        /// <summary>resolveEncounter's `sfx('alert'); await showContact(u,e,kind)` (null: skipped).</summary>
        public System.Func<ITurnUnit, ITurnUnit, EncounterKind, IEnumerator> ShowContact;
        /// <summary>askEngage(u,e) for a human mover's SelfOnly encounter — reports true to attack
        /// (null: a human mover attacks, same as askEngage under G.autoplay).</summary>
        public System.Func<ITurnUnit, ITurnUnit, System.Action<bool>, IEnumerator> AskEngage;
        /// <summary>An encounter was resolved: mover, enemy, who spotted whom, and whether it was fought (false: ignored and walked on).
        /// Presentation / the tutorial only.</summary>
        public event System.Action<ITurnUnit, ITurnUnit, EncounterKind, bool> EncounterResolved;

        public MovementSystem(MapGrid map, IReadOnlyList<ITurnUnit> units, ITurnHostage hostage)
        {
            this.map = map; this.units = units; this.hostage = hostage;
        }

        int W => MapGrid.Width;
        int H => map.Height;
        public int Idx(int x, int y) => y * W + x;
        // JS Math.round: halves round up (Mathf.RoundToInt is banker's rounding).
        public static int JsRound(float v) => Mathf.FloorToInt(v + 0.5f);
        static float Dist(Vector2 a, Vector2 b) => Mathf.Sqrt((a.x - b.x) * (a.x - b.x) + (a.y - b.y) * (a.y - b.y));
        static float Hypot(float dx, float dy) => Mathf.Sqrt(dx * dx + dy * dy);

        /// <summary>enemyVisible(team,e).</summary>
        bool EnemyVisible(OperatorTeam team, ITurnUnit e) => Vision == null || Vision.EnemyVisible(team, (IVisionAgent)e);

        /// <summary>The `team!==G.side||G.autoplay||enemyVisible(team,e)` test shared by blockedSet/bodyObstacles.</summary>
        bool Knows(OperatorTeam team, ITurnUnit e) => !PlayerSide.HasValue || team != PlayerSide.Value || EnemyVisible(team, e);

        // ================================================================
        //  collision
        // ================================================================

        /// <summary>blockCell(x,y,isDrone) — TL() returns 'void' (blocking) outside the grid.</summary>
        public bool BlockCell(int x, int y, bool isDrone)
        {
            if (x < 0 || y < 0 || x >= W || y >= H) return true;
            var t = map.TileAt(x, y);
            if (t == TileType.Floor || t == TileType.Out || t == TileType.Rubble || t == TileType.Extract) return false;
            if (isDrone && t == TileType.Hole) return false;
            return true;
        }

        /// <summary>freeAt(px,py,isDrone,r=UR)</summary>
        public bool FreeAt(float px, float py, bool isDrone, float r = UR)
        {
            for (int cy = JsRound(py - r); cy <= JsRound(py + r); cy++)
                for (int cx = JsRound(px - r); cx <= JsRound(px + r); cx++)
                {
                    if (!BlockCell(cx, cy, isDrone)) continue;
                    float qx = Mathf.Clamp(px, cx - .5f, cx + .5f), qy = Mathf.Clamp(py, cy - .5f, cy + .5f);
                    if ((px - qx) * (px - qx) + (py - qy) * (py - qy) < r * r) return false;
                }
            return true;
        }

        /// <summary>segFree(a,b,isDrone)</summary>
        public bool SegFree(Vector2 a, Vector2 b, bool isDrone)
        {
            float d = Hypot(b.x - a.x, b.y - a.y);
            int n = Mathf.Max(1, Mathf.CeilToInt(d / .08f));
            for (int i = 1; i <= n; i++)
            {
                float t = (float)i / n;
                if (!FreeAt(a.x + (b.x - a.x) * t, a.y + (b.y - a.y) * t, isDrone)) return false;
            }
            return true;
        }

        // ================================================================
        //  path planning
        // ================================================================

        /// <summary>blockedSet(team): the unescorted hostage's cell + every known living enemy's cell.</summary>
        public HashSet<int> BlockedSet(OperatorTeam team)
        {
            var s = new HashSet<int>();
            var h = hostage;
            if (h != null && h.Alive && !(h.Escort != null && h.Escort.Team == team)) s.Add(Idx(JsRound(h.X), JsRound(h.Y)));
            foreach (var e in units)
                if (e.Alive && e.Team != team && Knows(team, e)) s.Add(Idx(JsRound(e.X), JsRound(e.Y)));
            return s;
        }

        /// <summary>dijkstra(sx,sy,isDrone,blocked): 8-way, no corner cutting, diagonal 1.4142.</summary>
        public void Dijkstra(int sx, int sy, bool isDrone, HashSet<int> blocked, out float[] D, out int[] P)
        {
            int N = W * H;
            D = new float[N]; P = new int[N];
            var done = new bool[N];
            for (int i = 0; i < N; i++) { D[i] = Inf; P[i] = -1; }
            int s = Idx(sx, sy);
            if (s < 0 || s >= N) return;
            D[s] = 0;
            var open = new List<int> { s };
            while (open.Count > 0)
            {
                int bi = 0;
                for (int i = 1; i < open.Count; i++) if (D[open[i]] < D[open[bi]]) bi = i;
                int c = open[bi]; open[bi] = open[open.Count - 1]; open.RemoveAt(open.Count - 1);
                if (done[c]) continue; done[c] = true;
                int cx = c % W, cy = c / W;
                for (int k = 0; k < 8; k++)
                {
                    int dx = Dir8[k, 0], dy = Dir8[k, 1];
                    int nx = cx + dx, ny = cy + dy;
                    if (!Pass(nx, ny, isDrone)) continue;
                    int n = Idx(nx, ny);
                    if (done[n] || (blocked != null && blocked.Contains(n))) continue;
                    if (dx != 0 && dy != 0 && (!Pass(cx + dx, cy, isDrone) || !Pass(cx, cy + dy, isDrone))) continue;
                    float nd = D[c] + (dx != 0 && dy != 0 ? 1.4142f : 1f);
                    if (nd < D[n]) { D[n] = nd; P[n] = c; open.Add(n); }
                }
            }
        }

        static readonly int[,] Dir8 = { { 1, 0 }, { -1, 0 }, { 0, 1 }, { 0, -1 }, { 1, 1 }, { 1, -1 }, { -1, 1 }, { -1, -1 } };
        bool Pass(int x, int y, bool isDrone) => x >= 0 && y >= 0 && x < W && y < H && !BlockCell(x, y, isDrone);

        /// <summary>pathLen(pts)</summary>
        public static float PathLen(IList<Vector2> pts)
        {
            float l = 0;
            for (int i = 1; i < pts.Count; i++) l += Dist(pts[i], pts[i - 1]);
            return l;
        }

        /// <summary>truncate(pts,b)</summary>
        public static List<Vector2> Truncate(IList<Vector2> pts, float b)
        {
            var output = new List<Vector2> { pts[0] };
            float rem = b;
            for (int i = 1; i < pts.Count; i++)
            {
                float s = Dist(pts[i], pts[i - 1]);
                if (s <= rem) { output.Add(pts[i]); rem -= s; }
                else
                {
                    float t = rem / s;
                    if (rem > .02f) output.Add(new Vector2(pts[i - 1].x + (pts[i].x - pts[i - 1].x) * t, pts[i - 1].y + (pts[i].y - pts[i - 1].y) * t));
                    break;
                }
            }
            return output;
        }

        /// <summary>bodyObstacles(team,from): unescorted hostage + known living enemies, minus anything
        /// within .3 of `from` (the mover itself / the point the new leg starts at).</summary>
        public List<Vector2> BodyObstacles(OperatorTeam team, Vector2? from)
        {
            var obs = new List<Vector2>();
            var h = hostage;
            if (h != null && h.Alive && !(h.Escort != null && h.Escort.Team == team)) obs.Add(new Vector2(h.X, h.Y));
            foreach (var e in units)
                if (e.Alive && e.Team != team && Knows(team, e)) obs.Add(new Vector2(e.X, e.Y));
            if (from.HasValue) obs.RemoveAll(o => Hypot(o.x - from.Value.x, o.y - from.Value.y) <= .3f);
            return obs;
        }

        /// <summary>bodyClear(a,b,obs): every .1 step along a→b stays ≥.58 from every obstacle.</summary>
        public static bool BodyClear(Vector2 a, Vector2 b, List<Vector2> obs)
        {
            if (obs == null || obs.Count == 0) return true;
            float d = Hypot(b.x - a.x, b.y - a.y);
            int n = Mathf.Max(1, Mathf.CeilToInt(d / .1f));
            for (int i = 1; i <= n; i++)
            {
                float px = a.x + (b.x - a.x) * i / n, py = a.y + (b.y - a.y) * i / n;
                foreach (var o in obs) if (Hypot(o.x - px, o.y - py) < .58f) return false;
            }
            return true;
        }

        /// <summary>planPath(from,tx,ty,isDrone,team,budget): dijkstra cell route, exact end point when
        /// reachable in a straight line, then collapsed to the fewest clear straight legs and truncated
        /// to `budget`. Null when the goal cell is out of the grid or unreachable.</summary>
        public List<Vector2> PlanPath(Vector2 from, float tx, float ty, bool isDrone, OperatorTeam team, float? budget)
        {
            int sx = JsRound(from.x), sy = JsRound(from.y), gx = JsRound(tx), gy = JsRound(ty);
            if (gx < 0 || gy < 0 || gx >= W || gy >= H) return null;
            var bl = isDrone ? null : BlockedSet(team);
            bl?.Remove(Idx(sx, sy));
            var obs = isDrone ? null : BodyObstacles(team, from);
            bool Ok(Vector2 a, Vector2 b) => SegFree(a, b, isDrone) && BodyClear(a, b, obs);
            Dijkstra(sx, sy, isDrone, bl, out var D, out var P);
            int gk = Idx(gx, gy);
            if (D[gk] >= Inf) return null;
            var cells = new List<Vector2>();
            int c = gk, sk = Idx(sx, sy);
            while (c != sk && c >= 0) { cells.Add(new Vector2(c % W, c / W)); c = P[c]; }
            cells.Reverse();
            var pts = new List<Vector2> { from };
            pts.AddRange(cells);
            var end = new Vector2(tx, ty);
            if (FreeAt(tx, ty, isDrone) && Ok(pts[pts.Count - 1], end) && Dist(pts[pts.Count - 1], end) > .05f)
            {
                if (cells.Count > 0 && Ok(pts[pts.Count - 2], end)) pts[pts.Count - 1] = end;
                else pts.Add(end);
            }
            var output = new List<Vector2> { pts[0] };
            int i = 0;
            while (i < pts.Count - 1)
            {
                int j = pts.Count - 1;
                while (j > i + 1 && !Ok(pts[i], pts[j])) j--;
                output.Add(pts[j]); i = j;
            }
            return budget.HasValue ? Truncate(output, budget.Value) : output;
        }

        /// <summary>spotClear(px,py,self): no other body (hostage, ally, or known enemy) within .6.</summary>
        public bool SpotClear(float px, float py, ITurnUnit self)
        {
            var h = hostage;
            if (h != null && h.Alive && !ReferenceEquals(h.Escort, self) && Hypot(h.X - px, h.Y - py) < .6f) return false;
            foreach (var u in units)
            {
                if (!u.Alive || ReferenceEquals(u, self) || Hypot(u.X - px, u.Y - py) >= .6f) continue;
                if (u.Team == self.Team || !PlayerSide.HasValue || self.Team != PlayerSide.Value || EnemyVisible(self.Team, u)) return false;
            }
            return true;
        }

        // ================================================================
        //  moveAlong
        // ================================================================

        /// <summary>followHostage(u): the escorted hostage trails .85m behind along the escort's path.</summary>
        void FollowHostage(ITurnUnit u)
        {
            var h = hostage;
            if (h == null || !h.Alive || !ReferenceEquals(h.Escort, u) || !(h is IEscortableHostage eh)) return;
            var trail = eh.Trail;
            trail.Add(new Vector2(u.X, u.Y));
            if (trail.Count > 80) trail.RemoveAt(0);
            float acc = 0;
            for (int i = trail.Count - 1; i > 0; i--)
            {
                Vector2 a = trail[i], b = trail[i - 1];
                float s = Dist(a, b);
                if (acc + s >= .85f)
                {
                    float t = (.85f - acc) / s;
                    eh.SetPosition(a.x + (b.x - a.x) * t, a.y + (b.y - a.y) * t);
                    return;
                }
                acc += s;
            }
        }

        /// <summary>
        /// Port of moveAlong(u,pts,human): walks the path in ≤.2m steps. Each step moves the logical
        /// position (u.x/u.y) at once and tweens the drawn position (u.rx/u.ry) linearly over st·230ms,
        /// with the camera easing after it (panTo 200ms). A living enemy within .56 or the unescorted
        /// hostage within .5 of the next step stops the walk with a log line. Leftover budget under .35
        /// (or any stop) zeroes moveLeft. `onDone(stopped)` reports the source's return value.
        /// </summary>
        public IEnumerator MoveAlong(ITurnUnit u, List<Vector2> pts, bool human, System.Action<bool> onDone = null)
        {
            bool stopped = false, halt = false;
            // const trigM=new Set(), trigE=new Set();
            // for(const e of enemies()){ if(sees(u,e)) trigM.add(e.id); if(sees(e,u)) trigE.add(e.id); }
            // — pairs already in sight when the walk starts never count as a new encounter.
            bool checkEnc = Vision != null && Combat != null;
            var trigM = new HashSet<ITurnUnit>();
            var trigE = new HashSet<ITurnUnit>();
            if (checkEnc)
                foreach (var e in units)
                {
                    if (!e.Alive || e.Team == u.Team) continue;
                    if (Sees(u, e)) trigM.Add(e);
                    if (Sees(e, u)) trigE.Add(e);
                }
            var cur = new Vector2(u.X, u.Y);
            const float STEP = .2f;
            foreach (var q in pts)
            {
                float seg = Dist(cur, q);
                float a = Mathf.Atan2(q.y - cur.y, q.x - cur.x), cs = Mathf.Cos(a), sn = Mathf.Sin(a);
                while (seg > 1e-3f)
                {
                    if (!u.Alive || (IsOver != null && IsOver()) || u.MoveLeft <= 1e-3f) { halt = true; break; }
                    float st = Mathf.Min(STEP, Mathf.Min(seg, u.MoveLeft));
                    float nx = cur.x + cs * st, ny = cur.y + sn * st;
                    ITurnUnit blk = null;
                    foreach (var e in units)
                        if (e.Alive && e.Team != u.Team && Hypot(e.X - nx, e.Y - ny) < .56f) { blk = e; break; }
                    var h = hostage;
                    bool hb = h != null && h.Alive && !ReferenceEquals(h.Escort, u) && Hypot(h.X - nx, h.Y - ny) < .5f;
                    if (blk != null || hb)
                    {
                        // if(blk){ blk.revealT=now()+3000; addMark(u.team,blk.x,blk.y,'drone'); }
                        if (blk != null)
                        {
                            if (blk is IRevealable rv) rv.RevealT = Time.unscaledTime + 3f;
                            Marks?.Add(u.Team, blk.X, blk.Y, MarkKind.Spotted);
                        }
                        Log?.Invoke($"{KrOf(u)}: 経路が塞がれたため移動を止めました", "");
                        halt = true; break;
                    }
                    if (!(u is IFacingLockable fl && fl.LockFacing)) u.Facing = a;   // if(!u.lockFacing) u.facing=a;
                    // u.x=nx; u.y=ny — logical position jumps; the drawn one (rx/ry) is tweened below.
                    float rx0 = u.RX, ry0 = u.RY;
                    u.MoveTo(nx, ny);
                    u.RX = rx0; u.RY = ry0;
                    cur = new Vector2(nx, ny); u.MoveLeft -= st; seg -= st;
                    FollowHostage(u);
                    OnStep?.Invoke(u);
                    bool show = !PlayerSide.HasValue || u.Team == PlayerSide.Value || EnemyVisible(PlayerSide.Value, u);
                    if (show) PanTo?.Invoke(nx, ny, 200f);
                    yield return TweenRender(u, nx, ny, show ? st * 230f : 6f);
                    if (CheckWin != null && CheckWin()) { onDone?.Invoke(true); yield break; }
                    if (!checkEnc) continue;

                    // let enc=null; for(const e of enemies()){ const us=sees(u,e), es=sees(e,u); ... }
                    ITurnUnit encE = null; EncounterKind encK = EncounterKind.SelfOnly;
                    foreach (var e in units)
                    {
                        if (!e.Alive || e.Team == u.Team) continue;
                        bool us = Sees(u, e), es = Sees(e, u);
                        // if((us||es)&&!canShoot(u,e)&&!canShoot(e,u)){ if(us&&!trigM.has(e.id)){trigM.add(e.id); addMark(u.team,e.x,e.y,'drone');}
                        //   if(es&&!trigE.has(e.id)){trigE.add(e.id); addMark(e.team,u.x,u.y,'drone');} continue; }
                        if ((us || es) && !CanShoot(u, e) && !CanShoot(e, u))
                        {
                            if (us && trigM.Add(e)) Marks?.Add(u.Team, e.X, e.Y, MarkKind.Spotted);
                            if (es && trigE.Add(e)) Marks?.Add(e.Team, u.X, u.Y, MarkKind.Spotted);
                            continue;
                        }
                        var k = EncounterResolver.Classify(us, es, trigM.Contains(e), trigE.Contains(e));
                        if (k == null) continue;
                        // if(es&&!trigE.has(e.id)){ trigE.add(e.id); if(us) trigM.add(e.id); enc={e,kind:us?2:3}; break; }
                        // if(us&&!trigM.has(e.id)){ trigM.add(e.id); enc={e,kind:1}; break; }
                        if (k.Value == EncounterKind.SelfOnly) trigM.Add(e);
                        else { trigE.Add(e); if (us) trigM.Add(e); }
                        encE = e; encK = k.Value;
                        break;
                    }
                    // if(enc){ const r=await resolveEncounter(u,enc.e,enc.kind,human); if(r==='stop'){ stopped=true; break outer; } }
                    if (encE != null)
                    {
                        bool stop = false;
                        yield return ResolveEncounter(u, encE, encK, human, s => stop = s);
                        if (stop) { stopped = true; halt = true; break; }
                    }
                }
                if (halt) break;
                cur = new Vector2(u.X, u.Y);
            }
            if (stopped || u.MoveLeft < .35f) u.MoveLeft = 0;
            OnStep?.Invoke(u);
            onDone?.Invoke(stopped);
        }

        /// <summary>sees(a,b).</summary>
        bool Sees(ITurnUnit a, ITurnUnit b) => Vision.Sees((IVisionAgent)a, (IVisionAgent)b);

        /// <summary>canShoot(a,t): traceLine(a→t) open and within weapon range + 4.</summary>
        bool CanShoot(ITurnUnit a, ITurnUnit t) =>
            a.Weapon != null && CombatSystem.CanShoot(a, t, CombatSystem.TraceLine(map, a.X, a.Y, t.X, t.Y));

        /// <summary>
        /// Port of resolveEncounter(u,e,kind,human): alert marks ("!") on whoever spotted, both revealed
        /// for 3s, camera to the midpoint (500ms), sfx('alert') + showContact, then EncounterResolver
        /// resolves the fight. SelfOnly asks the human (askEngage) or, for the AI, attacks when the
        /// preemptive hit chance is ≥30; ignoring reports 'go' (onDone(false)), every fight reports 'stop'
        /// and marks the mover as having acted.
        /// </summary>
        public IEnumerator ResolveEncounter(ITurnUnit u, ITurnUnit e, EncounterKind kind, bool human, System.Action<bool> onDone)
        {
            float T = Time.unscaledTime;
            // if(kind===1) u.alertT=T; else if(kind===2){u.alertT=T;e.alertT=T;} else e.alertT=T;
            if (kind != EncounterKind.EnemyOnly && u is IAlertable au) au.AlertT = T;
            if (kind != EncounterKind.SelfOnly && e is IAlertable ae) ae.AlertT = T;
            // e.revealT=T+3000; u.revealT=T+3000;
            if (e is IRevealable re) re.RevealT = T + 3f;
            if (u is IRevealable ru) ru.RevealT = T + 3f;
            // await panTo((u.x+e.x)/2,(u.y+e.y)/2,500); sfx('alert'); await showContact(u,e,kind);
            PanTo?.Invoke((u.X + e.X) / 2f, (u.Y + e.Y) / 2f, 500f);
            yield return new WaitForSeconds(.5f);
            if (ShowContact != null) yield return ShowContact(u, e, kind);

            bool engage = true;
            if (kind == EncounterKind.SelfOnly)
            {
                // const attack=human?await askEngage(u,e):hitChance(u,e,{preempt:true,walls:traceLine(...).walls})>=30;
                if (human)
                {
                    if (AskEngage != null) { bool ans = false; yield return AskEngage(u, e, a => ans = a); engage = ans; }
                }
                else engage = CombatSystem.HitChance(u, e, CombatSystem.TraceLine(map, u.X, u.Y, e.X, e.Y), preempt: true) >= 30;
                // if(!attack){ log(`${u.kr}: 敵を無視して移動を続けます`,''); return 'go'; }
                if (!engage) { Log?.Invoke($"{KrOf(u)}: 敵を無視して移動を続けます", ""); EncounterResolved?.Invoke(u, e, kind, false); onDone?.Invoke(false); yield break; }
            }
            yield return EncounterResolver.Resolve(Combat, kind, u, e, engage);
            u.Acted = true;
            EncounterResolved?.Invoke(u, e, kind, true);
            onDone?.Invoke(true);
        }

        /// <summary>tween(u,{rx:nx,ry:ny},ms,true) — linear.</summary>
        static IEnumerator TweenRender(ITurnUnit u, float x, float y, float ms)
        {
            float fx = u.RX, fy = u.RY, dur = ms / 1000f, t0 = Time.time;
            if (dur <= .001f) { u.RX = x; u.RY = y; yield break; }
            while (true)
            {
                float p = Mathf.Min(1f, (Time.time - t0) / dur);
                u.RX = fx + (x - fx) * p; u.RY = fy + (y - fy) * p;
                if (p >= 1f) yield break;
                yield return null;
            }
        }

        static string KrOf(ITurnUnit u) => u is IOperatorNamed n && !string.IsNullOrEmpty(n.Kr) ? n.Kr : u.Id;
    }

    /// <summary>u.lockFacing (視界固定): while set, moveAlong keeps the unit's facing instead of turning it
    /// toward each step.</summary>
    public interface IFacingLockable
    {
        bool LockFacing { get; }
    }

    /// <summary>u.alertT — when this unit last spotted an enemy in an encounter; its overhead "!" shows
    /// for 1.8s after (syncLabels: `t-(u.alertT||-1e9)<1800`). Time.unscaledTime seconds.</summary>
    public interface IAlertable
    {
        float AlertT { get; set; }
    }

    /// <summary>Optional display name (`u.kr`) for log lines.</summary>
    public interface IOperatorNamed
    {
        string Kr { get; }
    }
}
