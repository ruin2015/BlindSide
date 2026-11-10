// Ported from the AI block in blindside_source/04_gameplay_combat_ai.js: bestTarget, aiFire, aiDef,
// aiAtk, distField, aiPhase (~line 30-34, 257-334). Out of scope for this pass (left as extension
// points for later, once the corresponding systems exist as project scripts):
//   - aiShootGadgets / aiDrone are the ShootGadgets / DeployDrone hooks (DroneOps); gadgetSees lives in VisionSystem.GadgetSees.
//   - Moves go through aiMoveTo → the shared MovementSystem.MoveAlong (set as `Movement`), so mid-path
//     encounters (sees()-triggered resolveEncounter while walking) are the same code the player's MOVE runs.
//     Without a Movement the unit is lerped to the cell with no encounter checks (headless harness).
//   - `marks` (last-known-position memory after losing sight): knownEnemies() below only reports
//     enemies a teammate can currently see, matching enemyVisible() minus its gadget-vision term.
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Blindside.Data;
using Blindside.Map;
using Blindside.Vision;
using Blindside.Combat;
using Blindside.Movement;
using Blindside.UI;
using Blindside.Abilities;

namespace Blindside.Turns
{
    public class EnemyAI
    {
        // Port of DIR4 in distField's BFS (01_core_data_map_vision.js / 04_gameplay_combat_ai.js).
        static readonly Vector2Int[] Dir4 = { new Vector2Int(1, 0), new Vector2Int(-1, 0), new Vector2Int(0, 1), new Vector2Int(0, -1) };

        readonly MapGrid map;
        readonly VisionSystem vision;
        readonly CombatSystem combat;
        readonly IReadOnlyList<ITurnUnit> units;
        readonly ITurnHostage hostage0;
        readonly IReadOnlyList<ITurnHostage> hostages;
        /// <summary>The hostage the AI is about: the first one on the board that nobody escorts — else the first on the board (one
        /// hostage: always that one, as before).</summary>
        ITurnHostage hostage
        {
            get
            {
                ITurnHostage first = null;
                for (int i = 0; i < hostages.Count; i++)
                {
                    var h = hostages[i];
                    if (!HostageRules.OnBoard(h)) continue;
                    if (h.Escort == null) return h;
                    if (first == null) first = h;
                }
                return first ?? hostage0;
            }
        }

        ITurnHostage EscortedBy(ITurnUnit u)
        {
            for (int i = 0; i < hostages.Count; i++) if (HostageRules.OnBoard(hostages[i]) && hostages[i].Escort == u) return hostages[i];
            return null;
        }

        /// <summary>
        /// Fires right before a unit takes an aiFire() shot from where it stands (bestTarget). `mutual` is
        /// true when the target had already spotted the shooter back (no preemptive bonus). Not an
        /// encounter — those happen while walking, inside Movement.MoveAlong.
        /// </summary>
        public event System.Action<ITurnUnit, ITurnUnit, bool> OnEncounter;

        /// <summary>moveAlong for aiMoveTo — the same MovementSystem the player's MOVE uses (null: lerp, no encounters).</summary>
        public MovementSystem Movement;

        /// <summary>Fires after an AI unit finishes a move (HUD refresh cue — the source's updateHUD() after moveAlong).</summary>
        public event System.Action<ITurnUnit> OnUnitMoved;

        /// <summary>G.side — the human player's team (null: AI-vs-AI, every move is shown).</summary>
        public OperatorTeam? PlayerSide;
        /// <summary>panTo(x,y,ms) on the battlefield camera (null: the camera never follows).</summary>
        public System.Action<float, float, float> PanTo;

        /// <summary>useAbility for aiAtk's breach shortcuts — the same dispatcher/context the player's ABILITY
        /// command uses (null: the AI never breaches, e.g. headless harnesses).</summary>
        public AbilitySystem Abilities;
        public AbilityContext AbilityCtx;

        /// <summary>aiPhase's `await aiShootGadgets(team)` (null: skipped).</summary>
        public System.Func<OperatorTeam, IEnumerator> ShootGadgets;
        /// <summary>aiPhase's `if(team==='atk') await aiDrone()` (null: skipped).</summary>
        public System.Func<IEnumerator> DeployDrone;

        public EnemyAI(MapGrid map, VisionSystem vision, CombatSystem combat, IReadOnlyList<ITurnUnit> units, ITurnHostage hostage,
                       IReadOnlyList<ITurnHostage> hostages = null)
        {
            this.map = map; this.vision = vision; this.combat = combat; this.units = units; this.hostage0 = hostage;
            this.hostages = HostageRules.ListOf(hostage, hostages);

            // the defence hears every attacker's shot: where it came from is the latest intel (DefendTurn moves on it)
            combat.OnShotStart += (a, t) => { if (a.Team == OperatorTeam.Attack) NoteIntel(a.X, a.Y); };

            // Port of kill()'s escort-clear side effect: "護送中だった人質はその場で待機します".
            combat.OnKill += (victim, killer) =>
            {
                foreach (var h in this.hostages)
                    if (h != null && h.Escort == victim)
                    {
                        h.SetEscort(null);
                        HudLog.Add("護送中だった人質はその場で待機します", HudLogType.Warn);
                    }
            };
        }

        static int Idx(int x, int y) => y * MapGrid.Width + x;
        bool InBounds(int x, int y) => x >= 0 && y >= 0 && x < MapGrid.Width && y < map.Height;
        static float Dist(float ax, float ay, float bx, float by) => Mathf.Sqrt((ax - bx) * (ax - bx) + (ay - by) * (ay - by));
        static float MoveRange(ITurnUnit u) => Blindside.Combat.CombatModifiers.MoveRange(u.Mv, u.Weapon, u);

        /// <summary>
        /// Port of distField(goals,passFn): 4-directional unit-cost BFS from a set of seed cells.
        /// Used both as the source uses it (the attacker's distance-to-goal field) and, seeded from a
        /// single unit's own cell instead, as a stand-in for the source's separate weighted movement
        /// dijkstra (`{D}=dijkstra(...)`) — this project has no PathPlanner yet, and a uniform 4-dir BFS
        /// is a reasonable approximation of grid-step movement cost for this AI's purposes.
        /// </summary>
        public int[] DistanceField(IEnumerable<int> seeds, System.Func<int, int, bool> passable)
        {
            var dist = new int[MapGrid.Width * map.Height];
            for (int i = 0; i < dist.Length; i++) dist[i] = int.MaxValue;
            var queue = new Queue<int>();
            foreach (var g in seeds) { if (dist[g] != 0) { dist[g] = 0; queue.Enqueue(g); } }

            while (queue.Count > 0)
            {
                int c = queue.Dequeue();
                int cx = c % MapGrid.Width, cy = c / MapGrid.Width;
                foreach (var d in Dir4)
                {
                    int nx = cx + d.x, ny = cy + d.y;
                    if (!InBounds(nx, ny)) continue;
                    int n = Idx(nx, ny);
                    if (dist[n] <= dist[c] + 1 || !passable(nx, ny)) continue;
                    dist[n] = dist[c] + 1;
                    queue.Enqueue(n);
                }
            }
            return dist;
        }

        /// <summary>Port of blockCell(x,y,isDrone) plus blockedSet's hostage-cell term: whether the tile
        /// itself (or the hostage sitting on it, unescorted by the moving team) blocks foot traffic.</summary>
        bool BlockCell(int x, int y, OperatorTeam movingTeam)
        {
            var t = map.TileAt(x, y);
            bool tilePassable = t == TileType.Floor || t == TileType.Out || t == TileType.Rubble || t == TileType.Extract;
            if (!tilePassable) return true;
            for (int i = 0; i < hostages.Count; i++)
            {
                var h = hostages[i];
                if (HostageRules.OnBoard(h) && Mathf.RoundToInt(h.X) == x && Mathf.RoundToInt(h.Y) == y &&
                    !(h.Escort != null && h.Escort.Team == movingTeam))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Port of blockedSet(team)'s enemy term, specialized to the source's always-true autoplay
        /// branch (`team!==G.side||G.autoplay` — with every side AI-controlled, this is always true):
        /// every living enemy's current cell blocks pathing, regardless of whether it's actually seen.
        /// </summary>
        bool EnemyBlocks(int x, int y, OperatorTeam movingTeam)
        {
            foreach (var e in units)
                if (e.Alive && e.Team != movingTeam && Mathf.RoundToInt(e.X) == x && Mathf.RoundToInt(e.Y) == y)
                    return true;
            return false;
        }

        bool Passable(int x, int y, OperatorTeam movingTeam) => !BlockCell(x, y, movingTeam) && !EnemyBlocks(x, y, movingTeam);

        /// <summary>Port of occupiedCell(x,y,self): a candidate destination is taken by another live unit or the hostage.</summary>
        bool OccupiedCell(int x, int y, ITurnUnit self)
        {
            foreach (var o in units)
                if (o.Alive && o != self && Dist(o.X, o.Y, x, y) < 0.7f) return true;
            for (int i = 0; i < hostages.Count; i++)
                if (HostageRules.OnBoard(hostages[i]) && Dist(hostages[i].X, hostages[i].Y, x, y) < 0.7f) return true;
            return false;
        }

        /// <summary>Port of bestTarget(u): the visible, in-range enemy with the highest score (hit chance
        /// plus a low-HP bonus, minus a heavy penalty if a raised shield would block the shot). Vision
        /// (losF) already requires a wall-free line, so any target `u` sees has an unobstructed,
        /// wall-penetration-free shot too — the same ShotPath(false,0) CombatSystem uses for that case.
        /// </summary>
        public ITurnUnit BestTarget(ITurnUnit u)
        {
            ITurnUnit best = null;
            float bestScore = float.NegativeInfinity;
            var clearPath = new ShotPath(false, 0);
            foreach (var e in units)
            {
                if (!e.Alive || e.Team == u.Team) continue;
                if (!vision.Sees((IVisionAgent)u, (IVisionAgent)e)) continue;
                if (!CombatSystem.CanShoot(u, e, clearPath)) continue;
                float score = CombatSystem.HitChance(u, e, clearPath, preempt: false) + (100 - e.HP) * 0.3f
                    - (CombatSystem.ShieldBlocks(e, u) ? 90f : 0f);
                if (score > bestScore) { bestScore = score; best = e; }
            }
            return best;
        }

        /// <summary>Port of aiFire(u,t) + exchange(f,s,preempt=!sees(t,u)): a preemptive shot if the
        /// target hasn't spotted the shooter back, then one return volley if the target survives, is
        /// still alive-and-not-hostage, and can shoot back.</summary>
        public IEnumerator FireAt(ITurnUnit u, ITurnUnit t)
        {
            bool enemySeesBack = vision.Sees((IVisionAgent)t, (IVisionAgent)u);
            OnEncounter?.Invoke(u, t, enemySeesBack);
            // Both volleys are one engagement for CombatSystem.Director (camera staging only).
            yield return combat.Engagement(u, t, FireAtBody(u, t, !enemySeesBack));
        }

        IEnumerator FireAtBody(ITurnUnit u, ITurnUnit t, bool preempt)
        {
            var path = new ShotPath(false, 0);
            yield return combat.Shoot(u, t, path, preempt);
            u.Acted = true;
            if (u.Alive && t.Alive && !t.IsHostage && vision.Sees((IVisionAgent)t, (IVisionAgent)u) && CombatSystem.CanShoot(t, u, path))
                yield return combat.Shoot(t, u, path, false);
        }

        /// <summary>Port of knownEnemies(team) minus its `marks`/gadget-vision terms (see file header):
        /// every living enemy that at least one living teammate can currently see (enemyVisible).</summary>
        List<ITurnUnit> KnownEnemies(OperatorTeam team)
        {
            var result = new List<ITurnUnit>();
            foreach (var e in units)
                if (e.Alive && e.Team != team && vision.EnemyVisible(team, (IVisionAgent)e)) result.Add(e);
            return result;
        }

        /// <summary>Animates the move in ~0.2m steps at ~0.23s/m instead of snapping instantly, so the
        /// match reads as a watchable playback rather than a teleporting simulation. Lerping the grid
        /// coordinates directly is equivalent to lerping the converted world position (GridSpace's
        /// mapping is linear), so this stays grid-only like the rest of the AI's math.
        /// moveAlong's `show=u.team===G.side||enemyVisible(G.side,u); if(show) panTo(nx,ny,200);
        /// await tween(…, show?st*230:6)`: a step the player can't see takes ~6ms and the camera stays put.</summary>
        IEnumerator MoveTo(ITurnUnit u, int x, int y, int cost)
        {
            if (Movement != null) { yield return AiMoveTo(u, x, y); yield break; }
            u.MoveLeft = Mathf.Max(0f, u.MoveLeft - cost);
            float startX = u.X, startY = u.Y;
            float dist = Dist(startX, startY, x, y);
            int steps = Mathf.Max(1, Mathf.RoundToInt(dist / 0.2f));
            float stepDuration = dist * 0.23f / steps;
            float hiddenWait = 0f;   // unseen steps' 6ms each, banked until they add up to a frame
            for (int i = 1; i <= steps; i++)
            {
                float t = (float)i / steps;
                float nx = Mathf.Lerp(startX, x, t), ny = Mathf.Lerp(startY, y, t);
                u.MoveTo(nx, ny);
                bool show = !PlayerSide.HasValue || u.Team == PlayerSide.Value || vision.EnemyVisible(PlayerSide.Value, (IVisionAgent)u);
                if (show)
                {
                    PanTo?.Invoke(nx, ny, 200f);
                    hiddenWait = 0f;
                    yield return new WaitForSeconds(stepDuration);
                }
                else
                {
                    hiddenWait += .006f;
                    if (hiddenWait >= Time.deltaTime) { hiddenWait = 0f; yield return null; }
                }
            }
            OnUnitMoved?.Invoke(u);
        }

        /// <summary>
        /// Port of aiMoveTo(u,cx,cy):
        /// <code>
        /// const path=planPath(u,cx,cy,false,u.team,u.moveLeft); if(!path||path.length&lt;2) return;
        /// const end=path[path.length-1]; if(!spotClear(end.x,end.y,u)) return;
        /// if(enemyVisible(G.side,u)) await panTo(u.x,u.y,300);
        /// await moveAlong(u,path.slice(1),false);
        /// </code>
        /// </summary>
        IEnumerator AiMoveTo(ITurnUnit u, int cx, int cy)
        {
            var path = Movement.PlanPath(new Vector2(u.X, u.Y), cx, cy, false, u.Team, u.MoveLeft);
            if (path == null || path.Count < 2) yield break;
            var end = path[path.Count - 1];
            if (!Movement.SpotClear(end.x, end.y, u)) yield break;
            if (PlayerSide.HasValue && vision.EnemyVisible(PlayerSide.Value, (IVisionAgent)u))
            {
                PanTo?.Invoke(u.X, u.Y, 300f);
                yield return new WaitForSeconds(.3f);
            }
            yield return Movement.MoveAlong(u, path.GetRange(1, path.Count - 1), false);
            OnUnitMoved?.Invoke(u);
        }

        /// <summary>`!u.alive||G.over` after aiMoveTo.</summary>
        bool MoveEnded(ITurnUnit u) => !u.Alive || (Movement != null && Movement.IsOver != null && Movement.IsOver());

        void TryEscortHostage(ITurnUnit u)
        {
            if (EscortedBy(u) != null) return;   // (one at a time)
            if (ExfilZone.All.Count == 0) return;   // (a wipe-out map without exfil zones: nowhere to take a hostage)
            for (int i = 0; i < hostages.Count; i++)
            {
            var hostage = hostages[i];
            if (!HostageRules.OnBoard(hostage) || hostage.Escort != null) continue;
            // `dist(u,h)<=1.6&&segFree(u,h,false)` — no grabbing the hostage through a wall.
            if (Dist(u.X, u.Y, hostage.X, hostage.Y) <= 1.6f &&
                (Movement == null || Movement.SegFree(new Vector2(u.X, u.Y), new Vector2(hostage.X, hostage.Y), false)))
            {
                hostage.SetEscort(u);
                // aiAtk: log(`${u.kr}: 人質を確保`, u.team===G.side?'a':'f')
                HudLog.Add($"{(u is IOperatorNamed n && !string.IsNullOrEmpty(n.Kr) ? n.Kr : u.Id)}: 人質を確保", HudLog.TeamType(u.Team));
                return;
            }
            }
        }

        /// <summary>
        /// Port of aiDef(u): shoot if possible; otherwise, if a known enemy is within move+range reach,
        /// reposition to the best cell that keeps line of sight within weapon range (scored by
        /// `-D*1.5-|d-range*0.6|`, D from the self-seeded DistanceField) while staying leashed within
        /// 9m of an unescorted hostage; then face the nearest known enemy and take a shot if one opened up.
        /// </summary>
        public IEnumerator DefendTurn(ITurnUnit u)
        {
            var target = BestTarget(u);
            if (target != null) { yield return FireAt(u, target); yield break; }

            var known = KnownEnemies(OperatorTeam.Defense);
            if (known.Count == 0)
            {
                yield return DefendIdle(u);
                yield break;
            }

            known.Sort((a, b) => Dist(a.X, a.Y, u.X, u.Y).CompareTo(Dist(b.X, b.Y, u.X, u.Y)));
            var nearest = known[0];
            NoteIntel(nearest.X, nearest.Y);
            float weaponRange = u.Weapon.range;
            bool leash = hostage != null && hostage.Alive && hostage.Escort == null;

            bool inReach = Dist(nearest.X, nearest.Y, u.X, u.Y) <= MoveRange(u) + weaponRange + 2f;
            bool placed = false;   // the source's peek found a firing cell (moved there, or already on it)
            if (inReach)
            {
                int ux = Mathf.RoundToInt(u.X), uy = Mathf.RoundToInt(u.Y);
                var selfField = DistanceField(new[] { Idx(ux, uy) }, (x, y) => Passable(x, y, OperatorTeam.Defense));

                int best = -1; float bestScore = float.NegativeInfinity;
                for (int i = 0; i < selfField.Length; i++)
                {
                    if (selfField[i] == int.MaxValue || selfField[i] > u.MoveLeft - 0.3f) continue;
                    int x = i % MapGrid.Width, y = i / MapGrid.Width;
                    if (OccupiedCell(x, y, u)) continue;
                    float d = Dist(x, y, nearest.X, nearest.Y);
                    if (d > weaponRange + 1f || !vision.LineOfSight(x, y, nearest.X, nearest.Y, OperatorTeam.Defense)) continue;
                    if (leash && Dist(x, y, hostage.X, hostage.Y) > 9f) continue;
                    float score = -selfField[i] * 1.5f - Mathf.Abs(d - weaponRange * 0.6f);
                    if (score > bestScore) { bestScore = score; best = i; }
                }
                placed = best >= 0;
                if (best >= 0 && selfField[best] > 0)
                {
                    yield return MoveTo(u, best % MapGrid.Width, best / MapGrid.Width, selfField[best]);
                    if (MoveEnded(u)) yield break;
                }
            }
            if (!placed && Roamer(u))
            {
                // out of reach, or no firing cell this turn: a roamer shifts to hold an angle on the enemy from range
                yield return Reposition(u, nearest.X, nearest.Y, weaponRange, RoamLeash, true);
                if (MoveEnded(u)) yield break;
            }

            u.Facing = Mathf.Atan2(nearest.Y - u.Y, nearest.X - u.X);
            if (!u.Acted)
            {
                target = BestTarget(u);
                if (target != null) yield return FireAt(u, target);
            }
        }

        // ================================================================
        //  Defence movement beyond the source's aiDef (which stands still until an enemy is in sight)
        // ================================================================
        // Roles, fixed at the first defence turn: the defenders furthest from the hostage (~40%, at least one when there are
        // two or more) roam — wider leash, they go to intel and close in — the rest anchor near the hostage; the one nearest
        // the hostage is the guard and only shifts a little. Intel: the last enemy seen by the team or heard firing (any
        // attacker's shot), good for IntelTurns defence phases. With nothing known the defenders patrol between the ways in.
        const float AnchorLeash = 11f, RoamLeash = 20f, GuardLeash = 6f;
        const int IntelTurns = 2;
        const float RoamShare = .4f;
        const int MaxShift = 8;   // cells per repositioning (the move range still caps it): real moves, not shuffles in place

        readonly Dictionary<ITurnUnit, bool> roamers = new Dictionary<ITurnUnit, bool>();
        int defPhase;
        int intelPhase = -100;
        Vector2 intelPos;
        List<Vector2Int> threats;
        int threatsPhase = -1;

        public void NoteIntel(float x, float y) { intelPos = new Vector2(x, y); intelPhase = defPhase; }
        bool HasIntel => defPhase - intelPhase <= IntelTurns;

        bool Roamer(ITurnUnit u)
        {
            if (roamers.Count == 0)
            {
                var defs = new List<ITurnUnit>();
                foreach (var o in units) if (o.Alive && o.Team == OperatorTeam.Defense) defs.Add(o);
                float hx = hostage != null ? hostage.X : 0f, hy = hostage != null ? hostage.Y : 0f;
                defs.Sort((a, b) => Dist(b.X, b.Y, hx, hy).CompareTo(Dist(a.X, a.Y, hx, hy)));   // furthest first
                int n = defs.Count >= 2 ? Mathf.Max(1, Mathf.RoundToInt(defs.Count * RoamShare)) : 0;
                for (int i = 0; i < defs.Count; i++) roamers[defs[i]] = i < n && i < defs.Count - 1;
            }
            return roamers.TryGetValue(u, out bool r) && r;
        }

        /// <summary>The living defender nearest the (free) hostage.</summary>
        bool IsGuard(ITurnUnit u)
        {
            if (hostage == null || !hostage.Alive || hostage.Escort != null) return false;
            ITurnUnit g = null; float gd = float.MaxValue;
            foreach (var o in units)
            {
                if (!o.Alive || o.Team != OperatorTeam.Defense) continue;
                float d = Dist(o.X, o.Y, hostage.X, hostage.Y);
                if (d < gd) { gd = d; g = o; }
            }
            return g == u;
        }

        bool Walkable(int x, int y)
        {
            if (!InBounds(x, y)) return false;
            var t = map.TileAt(x, y);
            return t == TileType.Floor || t == TileType.Out || t == TileType.Rubble || t == TileType.Extract;
        }

        bool Indoor(int x, int y) => InBounds(x, y) && MapLibrary.Current.TerrainAt(x, y) == MapTerrain.In;

        /// <summary>The ways in (as DefenseDispersal finds them): indoor floor next to outdoor ground or to a breachable wall
        /// with outdoor ground behind it. Rebuilt once per defence phase (walls get opened).</summary>
        List<Vector2Int> Threats()
        {
            if (threats != null && threatsPhase == defPhase) return threats;
            threats = new List<Vector2Int>(); threatsPhase = defPhase;
            for (int y = 0; y < map.Height; y++)
                for (int x = 0; x < MapGrid.Width; x++)
                {
                    if (!Indoor(x, y) || map.TileAt(x, y) != TileType.Floor) continue;
                    foreach (var d in Dir4)
                    {
                        int nx = x + d.x, ny = y + d.y;
                        if (Walkable(nx, ny) && !Indoor(nx, ny)) { threats.Add(new Vector2Int(x, y)); break; }
                        if (!InBounds(nx, ny) || map.TileAt(nx, ny) != TileType.Wall) continue;
                        var p = map.PanelAt(nx, ny);
                        if (p.HasValue && !p.Value.reinforced && Walkable(nx + d.x, ny + d.y) && !Indoor(nx + d.x, ny + d.y))
                        { threats.Add(new Vector2Int(x, y)); break; }
                    }
                }
            return threats;
        }

        /// <summary>No enemy in sight: go to the latest intel (roamers always, the others half the time), or patrol — move to
        /// watch one of the ways in from a few metres back (roamers mostly, anchors sometimes, the guard rarely).</summary>
        IEnumerator DefendIdle(ITurnUnit u)
        {
            bool roam = Roamer(u), guard = !roam && IsGuard(u);
            float leashR = roam ? RoamLeash : guard ? GuardLeash : AnchorLeash;
            Vector2 focus;
            if (HasIntel)
            {
                focus = intelPos;
                if (roam)
                {
                    yield return Reposition(u, focus.x, focus.y, u.Weapon.range, leashR, true);
                    if (MoveEnded(u)) yield break;
                }
            }
            else
            {
                var ways = Threats();
                if (ways.Count == 0) yield break;
                // roamers pick among all the ways in, the others among those near the hostage
                var pool = new List<Vector2Int>();
                foreach (var w in ways)
                    if (roam || hostage == null || Dist(w.x, w.y, hostage.X, hostage.Y) <= leashR + 4f) pool.Add(w);
                if (pool.Count == 0) pool = ways;
                var pick = pool[Blindside.Core.GameRng.Range(0, pool.Count)];
                focus = pick;
                if (!guard && Blindside.Core.GameRng.Value < (roam ? .6f : .3f))
                {
                    yield return Reposition(u, pick.x, pick.y, 4f, leashR, true);
                    if (MoveEnded(u)) yield break;
                }
            }
            u.Facing = Mathf.Atan2(focus.y - u.Y, focus.x - u.X);
            if (!u.Acted)
            {
                var target = BestTarget(u);
                if (target != null) yield return FireAt(u, target);
            }
        }

        /// <summary>Moves to the reachable cell that best watches (fx, fy): a line of sight to it, about `prefDist` away, with
        /// cover around, not crowding a teammate, not far to walk, inside `leashR` of a free hostage (indoors only if asked).
        /// A small per-turn jitter (one GameRng draw) keeps it from always taking the same cell. Stays put if that is best.</summary>
        IEnumerator Reposition(ITurnUnit u, float fx, float fy, float prefDist, float leashR, bool indoorOnly)
        {
            int ux = Mathf.RoundToInt(u.X), uy = Mathf.RoundToInt(u.Y);
            int self = Idx(ux, uy);
            var field = DistanceField(new[] { self }, (x, y) => Passable(x, y, OperatorTeam.Defense));
            bool leash = hostage != null && hostage.Alive && hostage.Escort == null;
            float seed = Blindside.Core.GameRng.Value * 1000f;
            float range = u.Weapon.range;
            int best = -1; float bestScore = float.NegativeInfinity;
            for (int i = 0; i < field.Length; i++)
            {
                if (field[i] == int.MaxValue || field[i] > u.MoveLeft - 0.3f || field[i] > MaxShift) continue;
                int x = i % MapGrid.Width, y = i / MapGrid.Width;
                if (i != self && OccupiedCell(x, y, u)) continue;
                if (indoorOnly && i != self && !Indoor(x, y)) continue;
                if (leash && i != self && Dist(x, y, hostage.X, hostage.Y) > leashR) continue;
                float d = Dist(x, y, fx, fy);
                bool los = d <= range + 2f && vision.LineOfSight(x, y, fx, fy, OperatorTeam.Defense);
                int cover = 0, crowd = 0;
                for (int dy = -1; dy <= 1; dy++)
                    for (int dx = -1; dx <= 1; dx++)
                        if ((dx != 0 || dy != 0) && InBounds(x + dx, y + dy) && !Walkable(x + dx, y + dy)) cover++;
                foreach (var o in units)
                    if (o != u && o.Alive && o.Team == OperatorTeam.Defense && Dist(o.X, o.Y, x, y) < 2.5f) crowd++;
                float jitter = Mathf.Abs(Mathf.Sin(i * 12.9898f + seed) * 43758.547f) % 1f;
                float score = (los ? 4f : 0f) - Mathf.Abs(d - prefDist) * .6f - field[i] * .25f
                              + Mathf.Min(cover, 3) * .4f - crowd * 2f + jitter * 1.2f;
                if (score > bestScore) { bestScore = score; best = i; }
            }
            if (best < 0 || best == self) yield break;
            yield return MoveTo(u, best % MapGrid.Width, best / MapGrid.Width, field[best]);
        }

        bool breachOpened;   // AiBreach's result: a hammer/thermite opening changed the walkable grid

        bool Intact(WallPanel p) => map.TileAt(p.cells[0].x, p.cells[0].y) == TileType.Wall;   // p.state==='intact'

        /// <summary>
        /// Port of aiAtk's breach shortcuts (04_gameplay_combat_ai.js ~303-313), between building Df and the move:
        /// <code>
        /// hammer|thermite (charges&gt;0, !abilityUsed): cur=min(Df[u],999); for each intact, non-mirror panel near u
        ///   (hammer: not reinforced; thermite: not electrified): m = min Df over the panel cells' passable 4-neighbours
        ///   more than 1.2 from u; gain=cur-(m+2). Best gain&gt;=4 → useAbility(u,best) (caller recomputes Df).
        /// launcher (charges&gt;0, !abilityUsed, 50%): a random intact, non-mirror, throwClear panel with a passable
        ///   4-neighbour whose Df &lt; cur-5 → useAbility.
        /// </code>
        /// passC is the same predicate the goal field is built with.
        /// </summary>
        IEnumerator AiBreach(ITurnUnit u, int ux, int uy, int[] goalField)
        {
            breachOpened = false;
            string ab = u.AbilityKey;
            if (u.Charges <= 0 || u.AbilityUsed) yield break;
            // updateDiscovery(): what the attackers have had in sight up to now decides which walls they know are reinforced.
            WallDiscovery.Refresh(map, vision, units);
            bool PassC(int x, int y) => InBounds(x, y) && Passable(x, y, OperatorTeam.Attack);

            WallPanel? pick = null;
            if (ab == "hammer" || ab == "thermite")
            {
                long cur = System.Math.Min(goalField[Idx(ux, uy)], 999);
                long gain = 0;
                foreach (var p in map.Panels)
                {
                    // mirrors open like any wall (MirrorAbility: a mirror on a reinforced wall is still reinforced)
                    if (!Intact(p) || !AbilitySystem.PanelNear(p, u)) continue;
                    // A reinforced wall the attackers haven't seen yet looks like any other wall to them.
                    if (ab == "hammer" && p.reinforced && p.seenByAtk) continue;
                    if (ab == "thermite" && p.electrified && p.elecSeen) continue;   // same for a wire-charged wall not yet seen
                    long m = 1000000000L;
                    foreach (var c in p.cells)
                        foreach (var d in Dir4)
                        {
                            int nx = c.x + d.x, ny = c.y + d.y;
                            if (PassC(nx, ny) && Dist(nx, ny, u.X, u.Y) > 1.2f) m = System.Math.Min(m, goalField[Idx(nx, ny)]);
                        }
                    long g = cur - (m + 2);
                    if (g > gain) { gain = g; pick = p; }
                }
                if (gain < 4) pick = null;
            }
            else if (ab == "launcher" && Blindside.Core.GameRng.Value < .5f)
            {
                long cur = goalField[Idx(ux, uy)];
                var cand = new List<WallPanel>();
                foreach (var p in map.Panels)
                {
                    if (!Intact(p) || !AbilitySystem.ThrowClear(u, p, AbilityCtx)) continue;
                    bool closer = false;
                    foreach (var c in p.cells)
                        foreach (var d in Dir4)
                        {
                            int nx = c.x + d.x, ny = c.y + d.y;
                            if (PassC(nx, ny) && goalField[Idx(nx, ny)] < cur - 5) closer = true;
                        }
                    if (closer) cand.Add(p);
                }
                if (cand.Count > 0) pick = cand[Blindside.Core.GameRng.Range(0, cand.Count)];
            }
            if (!pick.HasValue) yield break;
            // It swung at what it took for a plain wall and met steel: the wall is now known (and the hammer stays
            // unspent) — the hammer can't open a reinforced wall, so it is never used on one.
            if (ab == "hammer" && pick.Value.reinforced) { map.SetPanelSeenByAtk(pick.Value.id); yield break; }
            // Same for the thermite on a wall carrying current: the current shows itself and the thermite stays unspent.
            if (ab == "thermite" && pick.Value.electrified) { map.SetPanelElecSeen(pick.Value.id); yield break; }

            // if(enemyVisible(G.side,u)) await panTo(u.x,u.y,300);
            if (PlayerSide.HasValue && vision.EnemyVisible(PlayerSide.Value, (IVisionAgent)u))
            {
                PanTo?.Invoke(u.X, u.Y, 300f);
                yield return new WaitForSeconds(.3f);
            }
            AbilityCtx.PrepPhase = false;   // aiAtk only runs in the attack phase
            yield return Abilities.Use(u, AbilityTarget.FromPanel(pick.Value), AbilityCtx);
            breachOpened = ab != "launcher";
            OnUnitMoved?.Invoke(u);                 // useAbility's updateHUD()
            if (Movement != null) Movement.CheckWin?.Invoke();   // …; checkWin()
        }

        /// <summary>
        /// Port of aiAtk(u) (drone recon omitted, see file header): shoot if possible; otherwise pick up the
        /// hostage if adjacent and unescorted, build the goal set (extract tiles while escorting, the hostage's
        /// cell while it's free, else the nearest living defender), take a breach shortcut if it pays off
        /// (AiBreach), move to the reachable cell minimizing `goalField*10+selfField` (the source's
        /// `Df[i]*10+D[i]`), try the pickup again from the new cell, then take a shot if one opened up.
        /// </summary>
        public IEnumerator AttackTurn(ITurnUnit u)
        {
            var target = BestTarget(u);
            if (target != null) { yield return FireAt(u, target); yield break; }

            TryEscortHostage(u);

            var goals = new List<int>();
            if (EscortedBy(u) != null)
            {
                for (int y = 0; y < map.Height; y++)
                    for (int x = 0; x < MapGrid.Width; x++)
                        if (ExfilZone.IsExfilTile(x, y)) goals.Add(Idx(x, y));   // every extraction cell is a goal: the field's nearest one by path wins
            }
            else if (hostage != null && hostage.Alive && hostage.Escort == null)
            {
                goals.Add(Idx(Mathf.RoundToInt(hostage.X), Mathf.RoundToInt(hostage.Y)));
            }
            else
            {
                ITurnUnit nearestDef = null; float nd = float.MaxValue;
                foreach (var e in units)
                {
                    if (!e.Alive || e.Team != OperatorTeam.Defense) continue;
                    float d = Dist(e.X, e.Y, u.X, u.Y);
                    if (d < nd) { nd = d; nearestDef = e; }
                }
                // The source falls back to idx(13,12), a cell of its house; the hostage's cell is the map-independent equivalent.
                var fallback = MapLibrary.Current.hostage;
                if (hostage != null) fallback = new Vector2Int(Mathf.RoundToInt(hostage.X), Mathf.RoundToInt(hostage.Y));
                goals.Add(nearestDef != null ? Idx(Mathf.RoundToInt(nearestDef.X), Mathf.RoundToInt(nearestDef.Y)) : Idx(fallback.x, fallback.y));
            }

            int ux = Mathf.RoundToInt(u.X), uy = Mathf.RoundToInt(u.Y);
            var goalField = DistanceField(goals, (x, y) => Passable(x, y, OperatorTeam.Attack));
            if (Abilities != null && AbilityCtx != null)
            {
                yield return AiBreach(u, ux, uy, goalField);
                if (MoveEnded(u)) yield break;
                // Df=distField(goals,passC) after an opening that can be walked through (rubble).
                if (breachOpened) goalField = DistanceField(goals, (x, y) => Passable(x, y, OperatorTeam.Attack));
            }
            var selfField = DistanceField(new[] { Idx(ux, uy) }, (x, y) => Passable(x, y, OperatorTeam.Attack));

            int best = -1; long bestValue = long.MaxValue;
            for (int i = 0; i < goalField.Length; i++)
            {
                if (selfField[i] == int.MaxValue || selfField[i] > u.MoveLeft - 0.3f) continue;
                if (goalField[i] >= int.MaxValue) continue;
                int x = i % MapGrid.Width, y = i / MapGrid.Width;
                if (OccupiedCell(x, y, u)) continue;
                long value = (long)goalField[i] * 10 + selfField[i];
                if (value < bestValue) { bestValue = value; best = i; }
            }
            if (best >= 0 && best != Idx(ux, uy))
            {
                yield return MoveTo(u, best % MapGrid.Width, best / MapGrid.Width, selfField[best]);
                if (MoveEnded(u)) yield break;
            }

            TryEscortHostage(u);

            if (!u.Acted)
            {
                target = BestTarget(u);
                if (target != null) yield return FireAt(u, target);
            }
        }

        /// <summary>
        /// Port of aiPhase(team): units with an immediate valid target act first (matches the
        /// `.sort((a,b)=>b.b-a.b)` on `bestTarget(u)?1:0`), each takes its full turn, then `ended=true`.
        /// Checks CheckWin after every unit's turn, same as kill()'s trailing checkWin() call.
        /// </summary>
        public IEnumerator RunTeamPhase(TurnManager tm, OperatorTeam team)
        {
            // aiPhase: await aiShootGadgets(team); if(G.over) return; if(team==='atk') await aiDrone();
            if (team == OperatorTeam.Defense) defPhase++;
            if (ShootGadgets != null) yield return ShootGadgets(team);
            if (tm.Over) yield break;
            if (team == OperatorTeam.Attack && DeployDrone != null) yield return DeployDrone();
            var acting = new List<ITurnUnit>();
            foreach (var u in units) if (u.Alive && u.Team == team) acting.Add(u);
            acting.Sort((a, b) => (BestTarget(b) != null ? 1 : 0) - (BestTarget(a) != null ? 1 : 0));

            foreach (var u in acting)
            {
                if (tm.Over) yield break;
                if (!u.Alive) continue;
                yield return team == OperatorTeam.Defense ? DefendTurn(u) : AttackTurn(u);
                u.Ended = true;
                yield return new WaitForSeconds(0.12f);
                if (tm.CheckWin()) yield break;
            }
        }
    }
}
