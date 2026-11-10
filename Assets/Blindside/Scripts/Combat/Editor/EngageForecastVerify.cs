// Checks that the engagement forecast (CombatSystem.ForecastExchange) is what the fight then does: many seeded trials on a throw-away
// map (open floor with a few breakable and reinforced walls), random weapons / positions / HP / growth / gear / shields / preempt /
// FloorHP; each trial forecasts, then runs the real Exchange (no presenter; a director whose waits are empty, so nothing waits) and
// compares both volleys (fired, hits, headshots, damage) and both HPs afterwards, and that the forecast drew nothing from GameRng.
// Nothing is saved; MapLibrary.Current and GameRng's seed are left as a fresh seed afterwards. Menu: Blindside/Combat/Verify Engage Forecast.
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using Blindside.Core;
using Blindside.Data;
using Blindside.Map;

namespace Blindside.Combat.Editor
{
    public static class EngageForecastVerify
    {
        const int W = 16, H = 14, Trials = 600;   // (each shot writes a [Log] line: kept modest)

        sealed class Unit : ICombatUnit, ICombatModifiers
        {
            public float X { get; set; }
            public float Y { get; set; }
            public float Facing { get; set; }
            public OperatorTeam Team { get; set; }
            public bool Alive { get; set; } = true;
            public bool IsHostage { get; set; }
            public int HP { get; set; }
            public WeaponData Weapon { get; set; }
            public bool ShieldUp { get; set; }
            public IUnitView View => null;
            public float DamageScale { get; set; } = 1f;
            public float AccuracyBonus { get; set; }
            public float DamageTakenScale { get; set; } = 1f;
            public float MoveBonus => 0f;
            public AttachmentEffect Gear { get; set; }
        }

        sealed class InstantDirector : ICombatDirector
        {
            public bool Directing { get; private set; }
            public IEnumerator Direct(ICombatUnit first, ICombatUnit second, IEnumerator engagement)
            {
                Directing = true;
                yield return engagement;
                Directing = false;
            }
            public IEnumerator Wait(float seconds) { yield break; }
            public IEnumerator Freeze(float seconds) { yield break; }
        }

        static MapData MakeMap()
        {
            var d = new MapData
            {
                name = "engage-forecast-test", width = W, height = H,
                terrain = new MapTerrain[W * H], crates = new bool[W * H], cameras = new Vector2Int[0],
                hostage = new Vector2Int(1, 12),
                exfils = new[] { new MapExfil { name = "A", x0 = 0, y0 = 0, x1 = 0, y1 = 0 } },
                defenderStarts = MapData.DefenderKeys.Select((k, i) => new MapDefenderStart { operatorKey = k, cell = new Vector2Int(1 + i, 1) }).ToArray(),
                anchors = new MapAnchors { attackerDrops = Enumerable.Range(0, 5).Select(i => new Vector2Int(1 + i, 0)).ToArray() },
            };
            for (int y = 0; y < H; y++) for (int x = 0; x < W; x++) d.terrain[y * W + x] = y == 0 ? MapTerrain.Out : MapTerrain.In;
            for (int x = 3; x <= 12; x++) d.terrain[5 * W + x] = MapTerrain.Breakable;
            for (int y = 7; y <= 11; y++) d.terrain[y * W + 8] = MapTerrain.Breakable;
            for (int x = 10; x <= 12; x++) d.terrain[9 * W + x] = MapTerrain.Reinforced;
            return d;
        }

        // runs a coroutine to the end at once (nested IEnumerators followed; WaitForSeconds and the like skipped)
        static void RunNow(IEnumerator root)
        {
            var stack = new Stack<IEnumerator>();
            stack.Push(root);
            int guard = 0;
            while (stack.Count > 0 && guard++ < 100000)
            {
                var top = stack.Peek();
                if (!top.MoveNext()) { stack.Pop(); continue; }
                if (top.Current is IEnumerator inner) stack.Push(inner);
            }
        }

        [MenuItem("Blindside/Combat/Verify Engage Forecast")]
        public static void Menu()
        {
            string report = Run(out bool ok);
            if (ok) Debug.Log(report); else Debug.LogError(report);
        }

        public static string Run(out bool ok)
        {
            var sb = new StringBuilder();
            var weapons = AssetDatabase.FindAssets("t:WeaponData", new[] { "Assets/Blindside/Data/Weapons" })
                .Select(g => AssetDatabase.LoadAssetAtPath<WeaponData>(AssetDatabase.GUIDToAssetPath(g))).Where(w => w != null).ToArray();
            if (weapons.Length == 0) { ok = false; return "[EngageForecastVerify] no weapons found"; }

            var prevMap = MapLibrary.Current;
            int prevFloor = CombatSystem.FloorHP;
            int bad = 0, fired1 = 0, fired2 = 0, crits = 0, kills = 0, blockedN = 0, gearN = 0;
            try
            {
                var data = MakeMap();
                MapLibrary.SetCurrent(data);
                var map = new MapGrid(data);
                var pick = new System.Random(12345);
                float F(double lo, double hi) => (float)(lo + pick.NextDouble() * (hi - lo));

                for (int t = 0; t < Trials; t++)
                {
                    var a = new Unit { Team = OperatorTeam.Attack, Weapon = weapons[pick.Next(weapons.Length)], HP = pick.Next(10, 141) };
                    var b = new Unit { Team = OperatorTeam.Defense, Weapon = weapons[pick.Next(weapons.Length)], HP = pick.Next(10, 141) };
                    do { a.X = F(1, W - 2); a.Y = F(1, H - 2); b.X = F(1, W - 2); b.Y = F(1, H - 2); }
                    while (Mathf.Abs(a.X - b.X) + Mathf.Abs(a.Y - b.Y) < 1f || map.TileAt(Mathf.RoundToInt(a.X), Mathf.RoundToInt(a.Y)) != TileType.Floor
                           || map.TileAt(Mathf.RoundToInt(b.X), Mathf.RoundToInt(b.Y)) != TileType.Floor);
                    a.Facing = F(-Mathf.PI, Mathf.PI); b.Facing = F(-Mathf.PI, Mathf.PI);
                    if (pick.Next(4) == 0) { a.DamageScale = F(.8, 1.4); a.AccuracyBonus = F(-5, 10); b.DamageTakenScale = F(.7, 1.1); }
                    if (pick.Next(4) == 0)
                    {
                        a.Gear = new AttachmentEffect { acc = F(-5, 8), firstAcc = pick.Next(2) == 0 ? F(5, 20) : 0f, headshot = F(0, 10), extraShots = pick.Next(0, 2), damagePct = F(0, 15) };
                        gearN++;
                    }
                    if (pick.Next(8) == 0) b.ShieldUp = true;                  // a raised shield (blocks in front, no return fire)
                    if (pick.Next(10) == 0) a.ShieldUp = true;                 // the opener behind its shield: no opening volley
                    if (pick.Next(12) == 0) b.IsHostage = true;
                    bool preempt = pick.Next(3) != 0;
                    bool sure = preempt && pick.Next(2) == 0;                  // an encounter's first strike (always hits)
                    CombatSystem.FloorHP = pick.Next(6) == 0 ? 1 : 0;          // the tutorial's floor
                    GameRng.Seed(pick.Next());

                    // ---- the forecast ----
                    double before = GameRng.Peek().NextDouble();
                    CombatSystem.ForecastExchange(map, a, b, preempt, out var v1, out var v2, out int aHp, out int bHp, sure);
                    bool untouched = GameRng.Peek().NextDouble() == before;

                    // ---- the fight ----
                    var cs = new CombatSystem(map) { Director = new InstantDirector() };
                    var res = new List<(ICombatUnit at, ShotResult r)>();
                    cs.OnResolved += (at, tg, r) => res.Add((at, r));
                    cs.OnKill += (tg, by) => { ((Unit)tg).Alive = false; kills++; };
                    cs.NextSureHit = sure;
                    RunNow(cs.Exchange(a, b, preempt));

                    ShotResult? r1 = null, r2 = null;
                    foreach (var (at, r) in res) { if (at == a) r1 = r; else r2 = r; }
                    bool Same(CombatSystem.VolleyOutcome v, ShotResult? r) =>
                        v.fired == r.HasValue && (!v.fired || (v.hits == r.Value.Hits && v.crits == r.Value.Headshots && v.total == r.Value.TotalDamage
                                                               && v.shots == r.Value.Shots && v.blocked == r.Value.Blocked));
                    bool okT = untouched && Same(v1, r1) && Same(v2, r2) && aHp == a.HP && bHp == b.HP;
                    if (v1.fired) fired1++;
                    if (v2.fired) fired2++;
                    crits += v1.crits + v2.crits;
                    if (v1.blocked || v2.blocked) blockedN++;
                    if (!okT && bad++ < 12)
                        sb.AppendLine($"FAIL trial {t}: {a.Weapon.name}→{b.Weapon.name} preempt={preempt} floor={CombatSystem.FloorHP} untouched={untouched} " +
                                      $"forecast v1 {v1.fired}/{v1.hits}h/{v1.crits}c/{v1.total} v2 {v2.fired}/{v2.hits}h/{v2.crits}c/{v2.total} hp {aHp}/{bHp} — " +
                                      $"actual v1 {(r1.HasValue ? $"{r1.Value.Hits}h/{r1.Value.Headshots}c/{r1.Value.TotalDamage}" : "none")} " +
                                      $"v2 {(r2.HasValue ? $"{r2.Value.Hits}h/{r2.Value.Headshots}c/{r2.Value.TotalDamage}" : "none")} hp {a.HP}/{b.HP}");
                }
            }
            finally
            {
                CombatSystem.FloorHP = prevFloor;
                if (prevMap != null) MapLibrary.SetCurrent(prevMap);
                GameRng.Seed(System.Environment.TickCount);
            }
            ok = bad == 0;
            sb.Insert(0, $"[EngageForecastVerify] {(ok ? "PASS" : "FAIL")} — {Trials - bad}/{Trials} trials identical " +
                         $"(opening volleys {fired1}, return fire {fired2}, headshots {crits}, kills {kills}, shield blocks {blockedN}, with gear {gearN})\n");
            return sb.ToString();
        }
    }
}
