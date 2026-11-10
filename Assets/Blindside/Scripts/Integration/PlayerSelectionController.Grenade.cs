// The HQ item in the match — for now the 破片手榴弾 (frag grenade), the player's operators only (SimUnit.Item, from the save):
//   command   「破片手榴弾」 (FRAG, ✹) in the unit's menu while it carries one, has not used it this match, can still act (not Acted)
//             and the squad still holds one (SaveData stock). Not in the deploy / prep phases.
//   aiming    Mode.Throw: the pointer gives the direction and how far (MinThrow … MaxThrow from the unit, clamped). The flight is
//             worked out on the grid: it travels along the ground line; a Wall / Hard cell bounces it (the crossed axis flips, half
//             the rest of the throw is lost; three bounces at most). FLARE's holes (Hole), rubble, crates and the outside let it
//             through (it flies over them). The preview: the dashed arc (a parabola per leg, lower after each bounce), the landing
//             point with the blast ring (BlastR) and the lethal ring (KillR), the reach ring around the unit. Left click throws,
//             right click / ESC cancel (back to the menu).
//   throw     the unit faces the throw, spends its action (Acted) and the item (one per match; one taken from the squad's
//             stock). The grenade flies the arc (unscaled time) and lands; it blinks and goes off 3 s after it left the hand.
//   blast     every living enemy within BlastR with a clear line from the blast: inside KillR dead outright, else 80 → 30 damage
//             by distance (× its 被ダメージ). Kills go through CombatSystem.ReportKill (counted, K.I.A, the win check as a shot
//             kill). Plain walls within WallR are half-broken (TileType.Hole: seen through, not walked through; reinforced ones hold).
// Nothing here runs for the AI (no SimUnit.Item outside the player's real matches), and no GameRng draw is made.
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Blindside.Map;
using Blindside.Rendering;
using Blindside.Turns;
using Blindside.UI;

namespace Blindside.Integration
{
    public partial class PlayerSelectionController
    {
        public const string FragKey = "frag";
        public const float MinThrow = 1.2f, MaxThrow = 8f, BlastR = 3f, KillR = .7f, WallR = 1.3f, FuseSeconds = 3f;
        public const float EdgeDamage = 30f, NearDamage = 80f;
        const float ThrowStep = .04f;
        const int MaxBounces = 3;
        static readonly Color FragOrange = new Color32(0xff, 0xb1, 0x3b, 0xff);

        // ---- the command -------------------------------------------------------------------------------------------------------
        static int ItemStock(string key)
        {
            var d = Blindside.Save.SaveSystem.Current;
            return d != null && !string.IsNullOrEmpty(key) ? d.GadgetOf(key).count : 0;
        }

        void AddItemCommand(SimUnit u, List<Command> L, bool prep)
        {
            if (u.Item != FragKey || u.ItemSpent || turns == null) return;
            if (prep || turns.Phase == MatchPhase.Deploy) return;
            L.Add(new Command
            {
                id = "item", kr = "破片手榴弾", en = "FRAG", ic = "✹",
                ok = !u.Acted && !u.Shield && ItemStock(u.Item) > 0, sub = "1個",
            });
        }

        // ---- aiming ------------------------------------------------------------------------------------------------------------
        float throwAngle, throwDist = 4f;
        readonly List<Vector2> throwPlan = new List<Vector2>(256);   // grid points along the flight
        readonly List<float> throwHeight = new List<float>(256);     // the height at each
        readonly List<Vector2> throwBounces = new List<Vector2>(4);
        Vector2 throwLand;
        bool hasThrow;

        void StartThrow(SimUnit u)
        {
            mode = Mode.Throw; uiUnit = u; panelUnit = u;
            throwAngle = u.Facing; throwDist = 4f;
            PlanThrow(u);
            NotifySelection();
            SetHint("投げる方向と距離を選択してください", "マウスの位置まで投げる (最大 " + MaxThrow.ToString("0") + "m) · 壁で跳ね返る · 3秒後に爆発 · 左クリックで投擲",
                ("cancel", "ESC キャンセル", false));
        }

        void UpdateThrow(float fx, float fy)
        {
            var u = uiUnit;
            if (u == null) return;
            float dx = fx - u.X, dy = fy - u.Y;
            float d = Mathf.Sqrt(dx * dx + dy * dy);
            if (d < .05f) return;
            float a = Mathf.Atan2(dy, dx), dist = Mathf.Clamp(d, MinThrow, MaxThrow);
            if (Mathf.Abs(AngNorm(a - throwAngle)) < .002f && Mathf.Abs(dist - throwDist) < .01f && hasThrow) return;
            throwAngle = a; throwDist = dist;
            u.Facing = a;
            PlanThrow(u);
        }

        bool BlocksThrow(float x, float y)
        {
            var t = map.TileAt(Mathf.FloorToInt(x + .5f), Mathf.FloorToInt(y + .5f));
            return t == TileType.Wall || t == TileType.Hard;
        }

        /// <summary>Works out the flight from the unit along throwAngle for throwDist (bounces included) into throwPlan / throwHeight.</summary>
        void PlanThrow(SimUnit u)
        {
            throwPlan.Clear(); throwHeight.Clear(); throwBounces.Clear();
            var pos = new Vector2(u.X, u.Y);
            var dir = new Vector2(Mathf.Cos(throwAngle), Mathf.Sin(throwAngle));
            float left = throwDist;
            // the legs: from the hand to the first bounce, then between bounces
            var legStart = new List<int>(4) { 0 };
            throwPlan.Add(pos);
            int bounces = 0, guard = 0;
            while (left > 0f && guard++ < 2000)
            {
                var next = pos + dir * ThrowStep;
                if (BlocksThrow(next.x, next.y))
                {
                    if (bounces >= MaxBounces) break;   // (stops against the wall)
                    int cx0 = Mathf.FloorToInt(pos.x + .5f), cy0 = Mathf.FloorToInt(pos.y + .5f);
                    int cx1 = Mathf.FloorToInt(next.x + .5f), cy1 = Mathf.FloorToInt(next.y + .5f);
                    bool flipX = cx1 != cx0 && BlocksThrow(next.x, pos.y);
                    bool flipY = cy1 != cy0 && BlocksThrow(pos.x, next.y);
                    if (!flipX && !flipY) { flipX = cx1 != cx0; flipY = cy1 != cy0; }
                    if (!flipX && !flipY) { flipX = flipY = true; }
                    if (flipX) dir.x = -dir.x;
                    if (flipY) dir.y = -dir.y;
                    left *= .5f;
                    bounces++;
                    throwBounces.Add(pos);
                    legStart.Add(throwPlan.Count - 1);
                    continue;
                }
                pos = next; left -= ThrowStep;
                throwPlan.Add(pos);
            }
            throwLand = pos;
            // heights: a parabola per leg; the first from the hand (1.1 m) down to the floor, lower hops after each bounce
            legStart.Add(throwPlan.Count - 1);
            for (int i = 0; i < throwPlan.Count; i++) throwHeight.Add(.1f);
            for (int l = 0; l + 1 < legStart.Count; l++)
            {
                int a = legStart[l], b = legStart[l + 1];
                if (b <= a) continue;
                float len = (b - a) * ThrowStep;
                float peak = l == 0 ? Mathf.Clamp(len * .3f, .6f, 2.2f) : Mathf.Clamp(len * .22f, .1f, .6f);
                float start = l == 0 ? 1.1f : .1f;
                for (int i = a; i <= b; i++)
                {
                    float t = (i - a) / (float)(b - a);
                    throwHeight[i] = Mathf.Lerp(start, .1f, t) + 4f * peak * t * (1f - t);
                }
            }
            hasThrow = true;
            throwKey = "";
        }

        bool OnClickThrowMode()
        {
            if (mode != Mode.Throw || uiUnit == null) return false;
            if (!hasThrow) return true;
            var u = uiUnit;
            busy = true; HideHint(); mode = Mode.Busy;
            StartCoroutine(ThrowRoutine(u));
            return true;
        }

        // ---- the preview ---------------------------------------------------------------------------------------------------------
        GameObject throwArcGo, throwBlastGo, throwKillGo, throwDotGo, throwReachGo;
        Mesh throwArcMesh;
        string throwKey = "";

        void SyncThrowHelpers()
        {
            if (throwArcGo == null)
            {
                var root = new GameObject("ThrowHelpers").transform;
                root.SetParent(transform, false);
                throwArcMesh = new Mesh { name = "ThrowArc" };
                throwArcGo = MakeMeshObject("Arc", root, throwArcMesh, new Color(FragOrange.r, FragOrange.g, FragOrange.b, .9f), 3006);
                throwBlastGo = MakeMeshObject("Blast", root, BuildRingMesh(.96f, 1f, 96), new Color(FragOrange.r, FragOrange.g, FragOrange.b, .8f), 3006);
                throwKillGo = MakeMeshObject("Kill", root, BuildRingMesh(.88f, 1f, 64), new Color(1f, .23f, .33f, .85f), 3006);
                throwDotGo = MakeMeshObject("Dot", root, BuildRingMesh(.12f, .2f, 32), FragOrange, 3006);
                throwReachGo = MakeMeshObject("Reach", root, BuildRingMesh(.985f, 1f, 128), new Color(1f, 1f, 1f, .25f), 3005);
                throwArcGo.SetActive(false); throwBlastGo.SetActive(false); throwKillGo.SetActive(false); throwDotGo.SetActive(false); throwReachGo.SetActive(false);
            }
            bool on = mode == Mode.Throw && hasThrow && uiUnit != null;
            throwArcGo.SetActive(on); throwBlastGo.SetActive(on); throwKillGo.SetActive(on); throwDotGo.SetActive(on); throwReachGo.SetActive(on);
            if (!on) return;
            var u = uiUnit;
            string key = u.X.ToString("0.000") + "," + u.Y.ToString("0.000") + ">" + throwLand.x.ToString("0.000") + "," + throwLand.y.ToString("0.000") + "#" + throwPlan.Count;
            if (key != throwKey)
            {
                throwKey = key;
                BuildArcMesh(throwArcMesh, throwPlan, throwHeight, .07f, .22f, .12f);
            }
            var land = GridSpace.GridToWorld(throwLand.x, throwLand.y, .06f);
            throwBlastGo.transform.position = land; throwBlastGo.transform.localScale = Vector3.one * BlastR;
            throwKillGo.transform.position = land; throwKillGo.transform.localScale = Vector3.one * KillR;
            float s = 1f + .2f * Mathf.Sin(Time.unscaledTime * 9f);
            throwDotGo.transform.position = land; throwDotGo.transform.localScale = new Vector3(s, s, s);
            throwReachGo.transform.position = GridSpace.GridToWorld(u.X, u.Y, .05f); throwReachGo.transform.localScale = Vector3.one * MaxThrow;
        }

        /// <summary>A dashed ribbon along grid points `pts` at heights `hs` (`w` wide, `dash` on, `gap` off), lying flat.</summary>
        static void BuildArcMesh(Mesh mesh, List<Vector2> pts, List<float> hs, float w, float dash, float gap)
        {
            var verts = new List<Vector3>(); var tris = new List<int>();
            float run = 0f;
            for (int i = 1; i < pts.Count; i++)
            {
                var a = pts[i - 1]; var b = pts[i];
                float dx = b.x - a.x, dy = b.y - a.y, l = Mathf.Sqrt(dx * dx + dy * dy);
                float phase = run % (dash + gap);
                run += l;
                if (l < 1e-5f || phase > dash) continue;
                float nx = -dy / l * w * .5f, ny = dx / l * w * .5f;
                int v = verts.Count;
                verts.Add(GridSpace.GridToWorld(a.x + nx, a.y + ny, hs[i - 1]));
                verts.Add(GridSpace.GridToWorld(a.x - nx, a.y - ny, hs[i - 1]));
                verts.Add(GridSpace.GridToWorld(b.x + nx, b.y + ny, hs[i]));
                verts.Add(GridSpace.GridToWorld(b.x - nx, b.y - ny, hs[i]));
                tris.Add(v); tris.Add(v + 1); tris.Add(v + 2); tris.Add(v + 1); tris.Add(v + 3); tris.Add(v + 2);
            }
            mesh.Clear();
            mesh.SetVertices(verts); mesh.SetTriangles(tris, 0); mesh.RecalculateBounds();
        }

        // ---- the throw -------------------------------------------------------------------------------------------------------------
        IEnumerator ThrowRoutine(SimUnit u)
        {
            float t0 = Time.unscaledTime;
            u.Facing = throwAngle;
            u.Acted = true;
            u.ItemSpent = true;
            var d = Blindside.Save.SaveSystem.Current;
            if (d != null) { var line = d.GadgetOf(u.Item); line.count = Mathf.Max(0, line.count - 1); }
            u.View?.PlayAbility("frag");
            Log(KrOf(u) + ": 破片手榴弾を投げた", "a");
            UnitsChanged?.Invoke();

            // the flight (a copy: the plan is the aiming's)
            var plan = new List<Vector2>(throwPlan); var hs = new List<float>(throwHeight);
            var land = throwLand;
            var g = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            g.name = "FragGrenade";
            var col = g.GetComponent<Collider>(); if (col != null) Destroy(col);
            g.transform.localScale = Vector3.one * .2f;
            var mr = g.GetComponent<MeshRenderer>();
            var lit = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var mat = new Material(lit);
            var dark = new Color(.13f, .13f, .14f);
            mat.SetColor("_BaseColor", dark); mat.SetColor("_Color", dark);
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            float flight = .35f + plan.Count * ThrowStep * .07f;
            float f = 0f;
            while (f < flight)
            {
                f += Time.unscaledDeltaTime;
                float p = Mathf.Clamp01(f / flight);
                float idx = p * (plan.Count - 1);
                int i0 = Mathf.FloorToInt(idx), i1 = Mathf.Min(plan.Count - 1, i0 + 1);
                float k = idx - i0;
                var gp = Vector2.Lerp(plan[i0], plan[i1], k);
                g.transform.position = GridSpace.GridToWorld(gp.x, gp.y, Mathf.Lerp(hs[i0], hs[i1], k));
                g.transform.Rotate(720f * Time.unscaledDeltaTime, 360f * Time.unscaledDeltaTime, 0f);
                yield return null;
            }
            g.transform.position = GridSpace.GridToWorld(land.x, land.y, .1f);
            // on the floor: it blinks faster and faster until the fuse runs out
            while (Time.unscaledTime - t0 < FuseSeconds)
            {
                float left = FuseSeconds - (Time.unscaledTime - t0);
                float rate = Mathf.Lerp(16f, 4f, left / FuseSeconds);
                bool hot = Mathf.Sin(Time.unscaledTime * rate * Mathf.PI) > 0f;
                var c = hot ? new Color(1f, .2f, .2f) : dark;
                mat.SetColor("_BaseColor", c); mat.SetColor("_Color", c);
                yield return null;
            }
            Destroy(g); Destroy(mat);

            Explode(u, land);
            UnitsChanged?.Invoke();
            turns?.CheckWin();
            busy = false;
            if (turns == null || turns.Over) yield break;
            AfterAction(u);
        }

        void Explode(SimUnit thrower, Vector2 at)
        {
            var w = GridSpace.GridToWorld(at.x, at.y, .2f);
            FxBlast.Explosion(GridSpace.GridToWorld(at.x, at.y, .15f));   // particles: flash, fireball, sparks, debris, dust, smoke
            CombatFx.Ring(GridSpace.GridToWorld(at.x, at.y, .06f), FragOrange, .6f, BlastR);
            SfxNoise.Boom();
            if (BattlefieldCamera.Instance != null) BattlefieldCamera.Instance.Shake(.35f);
            Log("破片手榴弾が爆発した", "a");

            // the enemies in reach with a clear line from the blast
            if (units != null)
                foreach (var t in units)
                {
                    if (!t.Alive || !t.Placed || t.Team == thrower.Team) continue;
                    float dx = t.X - at.x, dy = t.Y - at.y, dist = Mathf.Sqrt(dx * dx + dy * dy);
                    if (dist > BlastR) continue;
                    if (vision != null && !vision.LineOfSight(at.x, at.y, t.X, t.Y, thrower.Team)) continue;
                    int dmg = dist <= KillR ? t.HP
                        : Mathf.Max(1, Mathf.RoundToInt(Mathf.Lerp(NearDamage, EdgeDamage, (dist - KillR) / (BlastR - KillR)) * t.DamageTakenScale));
                    t.HP = Mathf.Max(0, t.HP - dmg);
                    WorldPopup.Show(t.X, t.Y, "-" + dmg, FragOrange);
                    if (t.HP <= 0)
                    {
                        t.View?.PlayDeath();
                        WorldPopup.Show(t.X, t.Y - .4f, "K.I.A", new Color(1f, .23f, .33f));
                        if (combat != null) combat.ReportKill(t, thrower);
                        else t.Alive = false;
                        Log(KrOf(t) + " を破片手榴弾で倒した", "a");
                    }
                    else t.View?.PlayHit(false);
                }

            // plain walls close by are half-broken (seen through, not walked through)
            if (map != null)
            {
                bool any = false;
                for (int i = 0; i < map.Panels.Count; i++)
                {
                    var p = map.Panels[i];
                    if (p.reinforced || p.cells == null || p.cells.Count == 0) continue;
                    bool near = false, wall = false;
                    foreach (var c in p.cells)
                    {
                        if (map.TileAt(c.x, c.y) == TileType.Wall) wall = true;
                        float cx = c.x - at.x, cy = c.y - at.y;
                        if (cx * cx + cy * cy <= WallR * WallR) near = true;
                    }
                    if (!near || !wall) continue;
                    BreakPanel(p.id, TileType.Hole, true);
                    any = true;
                }
                if (any && vision != null) vision.MapVersion++;
                if (any)
                {
                    // the other side hears it as a breach (the operator's own breach event would log its ability's line)
                    marks?.Add(Blindside.Vision.MarkBoard.Other(thrower.Team), at.x, at.y, Blindside.Vision.MarkKind.Breach);
                    SfxNoise.Boom();
                    Log("爆風で壁が半壊した", "a");
                }
            }
        }
    }
}
