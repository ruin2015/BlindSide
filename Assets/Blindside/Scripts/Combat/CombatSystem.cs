// Ported from the COMBAT block in blindside_source/04_gameplay_combat_ai.js:
// hitChance, shieldBlocks, canShoot, headshotChance, shoot, exchange, hitStop — plus traceLine from
// 01_core_data_map_vision.js, which exchange()/canShoot() need to trace the return-fire line themselves.
// Presentation (tracers, popups, sfx, camera shake, kill/checkWin side effects) is left to the caller
// via the On* events below (same split as VisionSystem). The source's log() lines from shoot()/kill()
// go to HudLog, a plain static sink with no UI/render dependency.
// The one exception is each unit's own figure, reached only through IUnitView (SetFacing on aim,
// PlayHit per damaging hit, PlayDeath on the kill) — never through a prefab or renderer.
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Blindside.Data;
using Blindside.Map;
using Blindside.Movement;
using Blindside.UI;

namespace Blindside.Combat
{
    /// <summary>Read/write view of whatever unit is shooting or being shot at.</summary>
    public interface ICombatUnit
    {
        float X { get; }
        float Y { get; }
        float Facing { get; set; }
        OperatorTeam Team { get; }
        bool Alive { get; }
        bool IsHostage { get; }   // team === 'host' in the source; hostages aren't an OperatorTeam
        int HP { get; set; }
        WeaponData Weapon { get; }
        bool ShieldUp { get; }    // t.shield deployed
        IUnitView View { get; }   // this unit's figure; null when it has none (e.g. headless tests)
    }

    /// <summary>Attacker→target line-of-fire info, e.g. from MapGrid/VisionSystem (source: traceLine).</summary>
    public readonly struct ShotPath
    {
        public readonly bool Blocked;
        public readonly int Walls;
        public ShotPath(bool blocked, int walls) { Blocked = blocked; Walls = walls; }
    }

    public readonly struct ShotResult
    {
        public readonly int Hits, Shots, Headshots, TotalDamage;
        public readonly bool Blocked, TargetKilled;
        public ShotResult(int hits, int shots, int headshots, int totalDamage, bool blocked, bool targetKilled)
        {
            Hits = hits; Shots = shots; Headshots = headshots; TotalDamage = totalDamage;
            Blocked = blocked; TargetKilled = targetKilled;
        }
    }

    /// <summary>One shot of a burst, as <see cref="CombatSystem.OnShot"/> reports it: who shot whom, whether it
    /// hit, whether it was a headshot and the damage it dealt (0 on a miss or a shield block).</summary>
    public readonly struct ShotEvent
    {
        public readonly ICombatUnit Attacker, Target;
        public readonly int Index, Shots, Damage;
        public readonly bool Hit, Headshot, ShieldBlocked;
        public ShotEvent(ICombatUnit attacker, ICombatUnit target, int index, int shots, bool hit, bool headshot, bool shieldBlocked, int damage)
        {
            Attacker = attacker; Target = target; Index = index; Shots = shots;
            Hit = hit; Headshot = headshot; ShieldBlocked = shieldBlocked; Damage = damage;
        }
    }

    /// <summary>
    /// Presentation-only hook around an engagement (a shot plus its return fire). It may stage the
    /// camera and stretch or shorten the waits between shots, but never touches the rolls: every
    /// Random call in <see cref="CombatSystem.Shoot"/> happens in the same order with or without it.
    /// </summary>
    public interface ICombatDirector
    {
        /// <summary>True while an engagement is running through this director (its Wait/Freeze pace it).</summary>
        bool Directing { get; }
        /// <summary>Wraps one engagement; returning `engagement` unchanged leaves it exactly as it was.</summary>
        IEnumerator Direct(ICombatUnit first, ICombatUnit second, IEnumerator engagement);
        /// <summary>The shot pacing's sleep(ms), in unscaled seconds.</summary>
        IEnumerator Wait(float seconds);
        /// <summary>hitStop(ms): Time.timeScale 0 for `seconds` of real time.</summary>
        IEnumerator Freeze(float seconds);
    }

    /// <summary>
    /// The #combat cut-in half of shoot(): combatShow / combatUpdate / combatCrit / combatResult / combatHide.
    /// When a presenter is set, Shoot() also follows the source's pacing around it (sleep(700) after
    /// combatShow, sleep(w.shots>4?90:150) per shot, sleep(750) → combatHide → sleep(150) at the end).
    /// </summary>
    public interface IShotPresenter
    {
        void Show(ICombatUnit attacker, ICombatUnit target, int chance, int dmg, int shots, bool preempt, int walls, bool blocked, int hsChance);
        void UpdateHp(ICombatUnit target);
        void Crit(int headshots);
        void Result(int hits, int shots, int total, bool blocked, int headshots);
        void Hide();
    }

    public class CombatSystem
    {
        /// <summary>#combat cut-in (null: no cut-in, the shorter headless pacing).</summary>
        public IShotPresenter Presenter { get; set; }

        /// <summary>Camera/pacing director for engagements (null: the plain pacing below).</summary>
        public ICombatDirector Director { get; set; }

        bool Directed => Director != null && Director.Directing;
        // sleep(ms) / hitStop(ms): the director's (unscaled, skippable) versions while it runs an engagement,
        // otherwise exactly the waits this class always used.
        object Sleep(float seconds) => Directed ? (object)Director.Wait(seconds) : new WaitForSeconds(seconds);
        object HitStopFor(float ms) => Directed ? Director.Freeze(ms / 1000f) : HitStop(ms);

        /// <summary>Runs `body` as one engagement through <see cref="Director"/> (unchanged without one).</summary>
        public IEnumerator Engagement(ICombatUnit first, ICombatUnit second, IEnumerator body)
            => Director != null ? Director.Direct(first, second, body) : body;

        /// <summary>The map traceLine() walks for <see cref="Exchange"/>. Shoot() still takes the caller's path.</summary>
        public MapGrid Map { get; set; }

        /// <summary>
        /// `G.over` as exchange() sees it after the first shot. In the source that shot's kill() has
        /// already run checkWin() (which sets G.over through finish()), so the owner wires this to its
        /// win check (TurnManager.CheckWin — idempotent, true once the match is over).
        /// </summary>
        public System.Func<bool> IsOver;

        public CombatSystem(MapGrid map = null) { Map = map; }
        // Port of HS_BASE.
        static readonly Dictionary<string, int> HeadshotBase = new Dictionary<string, int>
        {
            { "AR", 10 }, { "SMG", 6 }, { "SG", 4 }, { "PST", 14 }, { "DMR", 20 },
        };

        public static float Distance(ICombatUnit a, ICombatUnit b)
        {
            float dx = a.X - b.X, dy = a.Y - b.Y;
            return Mathf.Sqrt(dx * dx + dy * dy);
        }

        static float AngNorm(float a)
        {
            while (a > Mathf.PI) a -= 2f * Mathf.PI;
            while (a < -Mathf.PI) a += 2f * Mathf.PI;
            return a;
        }

        static int JsRound(float v, float lo, float hi) => Mathf.FloorToInt(Mathf.Clamp(v, lo, hi) + 0.5f);

        /// <summary>
        /// Port of hitChance(a,t,o): weapon base accuracy, +15 for a breaching shotgun inside 2.5m,
        /// the gun's accuracyFalloffPerTile per tile past its range (the source's 12 for its four weapons),
        /// -22 per wall in the way, +15 on a preemptive shot. Clamped 5..95.
        /// </summary>
        public static int HitChance(ICombatUnit attacker, ICombatUnit target, ShotPath path, bool preempt)
        {
            WeaponData w = attacker.Weapon;
            float d = Distance(attacker, target);
            float c = w.accuracy;
            if (w.breach && d <= 2.5f) c += 15f;
            if (d > w.range) c -= (d - w.range) * w.accuracyFalloffPerTile;
            c -= path.Walls * 22f;
            if (preempt) c += 15f;
            if (attacker is ICombatModifiers m)
            {
                if (m.AccuracyBonus != 0f) c += m.AccuracyBonus;   // growth (精密力 / mastery)
                var g = m.Gear;
                if (g != null) c += g.AccuracyAt(d);               // attachments (flat + the range band's)
            }
            return JsRound(c, 5, 95);
        }

        /// <summary>
        /// Port of headshotChance(a,t,o): per-weapon base crit chance, +18 on a preemptive shot,
        /// -5 past weapon range, +4 inside 2.5m, -4 per wall in the way. Clamped 2..60. Hostages
        /// can never be headshot (returns 0), matching `if(!t||t.team==='host') return 0`.
        /// </summary>
        public static int HeadshotChance(ICombatUnit attacker, ICombatUnit target, ShotPath path, bool preempt)
        {
            if (target == null || target.IsHostage) return 0;
            WeaponData w = attacker.Weapon;
            float d = Distance(attacker, target);
            float c = HeadshotBase.TryGetValue(w.ClassKey, out int b) ? b : 8;
            if (preempt) c += 18f;
            if (d > w.range) c -= 5f;
            if (d <= 2.5f) c += 4f;
            c -= path.Walls * 4f;
            var g = CombatModifiers.GearOf(attacker);
            if (g != null) c += g.headshot;                        // attachments (scopes, grips …)
            return JsRound(c, 2, 60);
        }

        /// <summary>Port of shieldBlocks(t,a): a raised shield stops fire from within ~69° of its facing.</summary>
        public static bool ShieldBlocks(ICombatUnit target, ICombatUnit attacker)
        {
            if (!target.ShieldUp) return false;
            float ang = Mathf.Atan2(attacker.Y - target.Y, attacker.X - target.X);
            return Mathf.Abs(AngNorm(ang - target.Facing)) <= 1.2f;
        }

        /// <summary>
        /// Port of traceLine(x0,y0,x1,y1): steps the segment every ≤0.1m, skipping the shooter's cell, the
        /// target's cell and repeats. Off-map ('void') and hard cells block; a wall cell blocks if its panel
        /// is reinforced or a mirror, otherwise it's shot through and counted in `walls`.
        /// </summary>
        public static ShotPath TraceLine(MapGrid map, float x0, float y0, float x1, float y1)
        {
            int W = MapGrid.Width, H = map.Height;
            float dx = x1 - x0, dy = y1 - y0;
            int n = Mathf.Max(1, Mathf.CeilToInt(Mathf.Sqrt(dx * dx + dy * dy) / .1f));
            int walls = 0, last = -1;
            int s = JsRoundI(y0) * W + JsRoundI(x0), e = JsRoundI(y1) * W + JsRoundI(x1);
            for (int i = 1; i < n; i++)
            {
                int cx = JsRoundI(x0 + dx * i / n), cy = JsRoundI(y0 + dy * i / n), k = cy * W + cx;
                if (k == s || k == e || k == last) continue;
                last = k;
                bool inside = cx >= 0 && cy >= 0 && cx < W && cy < H;          // TL(): 'void' off the map
                var t = inside ? map.TileAt(cx, cy) : TileType.Hard;
                if (!inside || t == TileType.Hard) return new ShotPath(true, walls);
                if (t == TileType.Wall)
                {
                    int pi = map.PanelIndexAt[k];
                    // a reinforced wall stops the round (a mirror on one too); a mirror on a plain wall is shot through
                    if (pi >= 0 && map.Panels[pi].reinforced) return new ShotPath(true, walls);
                    walls++;
                }
            }
            return new ShotPath(false, walls);
        }

        static int JsRoundI(float v) => Mathf.FloorToInt(v + 0.5f);   // JS Math.round (halves up)

        public ShotPath TraceLine(ICombatUnit a, ICombatUnit t)
        {
            if (Map == null) throw new System.InvalidOperationException("CombatSystem.Map is not set — traceLine() needs the map.");
            return TraceLine(Map, a.X, a.Y, t.X, t.Y);
        }

        /// <summary>Port of canShoot(a,t): unobstructed and within weapon range + 4.</summary>
        public static bool CanShoot(ICombatUnit attacker, ICombatUnit target, ShotPath path)
            => !path.Blocked && Distance(attacker, target) <= attacker.Weapon.range + 4f;

        /// <summary>The lowest HP a hit can leave (0 = a hit can kill). The tutorial holds it at 1 so nobody falls in its practice duel.</summary>
        public static int FloorHP = 0;

        public event System.Action<ICombatUnit, ICombatUnit> OnShotStart;             // attacker, target — the line is open (revealT)
        public event System.Action<ICombatUnit, ICombatUnit, bool, bool> OnShotFired; // attacker, target, hit, crit
        public event System.Action<ICombatUnit> OnShieldBlock;                        // target
        public event System.Action<ICombatUnit, int, bool> OnDamage;                  // target, amount, crit
        public event System.Action<ICombatUnit, ICombatUnit, ShotResult> OnResolved;   // attacker, target, summary
        public event System.Action<ICombatUnit, ICombatUnit> OnKill;                  // target, killer
        /// <summary>A kill from outside the gun path (a thrown grenade): every OnKill listener reacts as to a shot kill.</summary>
        public void ReportKill(ICombatUnit target, ICombatUnit killer) => OnKill?.Invoke(target, killer);
        public event System.Action<ShotEvent> OnShot;                                 // every shot: who→whom, hit, headshot, damage

        /// <summary>
        /// Port of shoot(a,t,o). `path` is the attacker→target line trace the caller already has
        /// (MapGrid/VisionSystem); mirrors the source's early return when the shot is blocked or out
        /// of range. Damage is ×1.4 per shot on a preemptive attack, and ×2 on a headshot crit.
        /// </summary>
        public IEnumerator Shoot(ICombatUnit attacker, ICombatUnit target, ShotPath path, bool preempt)
        {
            if (!attacker.Alive || !target.Alive) yield break;
            // behind a raised shield (BASTION) there is no shooting — not in an encounter, not in return fire
            if (attacker.ShieldUp)
            {
                HudLog.Add($"{Kr(attacker)}: シールド展開中のため射撃できません");
                yield break;
            }
            WeaponData w = attacker.Weapon;
            if (path.Blocked || Distance(attacker, target) > w.range + 4f)
            {
                HudLog.Add($"{Kr(attacker)}: 射線が遮られて射撃できません");
                yield break;
            }

            attacker.Facing = Mathf.Atan2(target.Y - attacker.Y, target.X - attacker.X);
            attacker.View?.SetFacing(attacker.Facing);
            // a.revealT=now()+3500; t.revealT=Math.max(t.revealT||0,now()+2500);
            OnShotStart?.Invoke(attacker, target);

            int chance = HitChance(attacker, target, path, preempt);
            int hsChance = HeadshotChance(attacker, target, path, preempt);
            // attachments: extra rounds, the first round's own hit chance (マズルブレーキ); none without gear
            var gear = CombatModifiers.GearOf(attacker);
            int shots = CombatModifiers.ShotsOf(attacker);
            int firstChance = gear != null && gear.firstAcc != 0f ? JsRound(chance + gear.firstAcc, 5, 95) : chance;
            // the gun's damage at this distance (its own falloff curve, or none with a ロングバレル; the gear's 攻撃力 %), ×1.4 on a preemptive shot
            int dmg = Mathf.RoundToInt(CombatModifiers.BaseDamage(attacker, Distance(attacker, target)) * (preempt ? 1.4f : 1f));
            // growth: the shooter's 攻撃力 / mastery, the target's 防御力 (only when set: a unit without growth keeps the exact numbers)
            if (attacker is ICombatModifiers am && am.DamageScale != 1f) dmg = Mathf.RoundToInt(dmg * am.DamageScale);
            float takenScale = target is ICombatModifiers tm ? tm.DamageTakenScale : 1f;
            bool blocked = ShieldBlocks(target, attacker);

            // combatShow(a,t,chance,dmg,w.shots,o.preempt,tr.walls,blocked,blocked?0:hsC); await sleep(700);
            var pres = Presenter;
            if (pres != null)
            {
                pres.Show(attacker, target, chance, dmg, shots, preempt, path.Walls, blocked, blocked ? 0 : hsChance);
                yield return Sleep(0.7f);
            }

            int hits = 0, total = 0, headshots = 0;
            // The target falls the moment its HP reaches 0. The rest of the volley is still resolved — the same GameRng draws
            // and counts as before, so a seeded match plays out identically — but no longer shown (no shot, no hit, no event).
            bool down = false;
            for (int i = 0; i < shots; i++)
            {
                if (!target.Alive) break;
                if (!down) attacker.View?.PlayFire();   // animation only
                bool hit = Blindside.Core.GameRng.Range(0f, 100f) < (i == 0 ? firstChance : chance);
                bool crit = false;
                int dealt = 0;
                if (hit)
                {
                    if (blocked)
                    {
                        if (!down) OnShieldBlock?.Invoke(target);
                    }
                    else
                    {
                        crit = Blindside.Core.GameRng.Range(0f, 100f) < hsChance;
                        int d = crit ? dmg * 2 : dmg;
                        if (takenScale != 1f) d = Mathf.Max(1, Mathf.RoundToInt(d * takenScale));
                        dealt = d;
                        hits++; total += d;
                        target.HP = Mathf.Max(FloorHP, target.HP - d);
                        if (crit) headshots++;
                        if (!down)
                        {
                            target.View?.PlayHit(crit);
                            OnDamage?.Invoke(target, d, crit);
                        }
                    }
                }
                if (down) continue;
                if (target.HP <= 0) { down = true; target.View?.PlayDeath(); }   // animation only (kill() itself follows the volley)
                if (hit && blocked) target.View?.PlayDodge(true);   // the shield takes it (animation only); a miss just flies wide
                OnShotFired?.Invoke(attacker, target, hit, crit);
                OnShot?.Invoke(new ShotEvent(attacker, target, i, shots, hit, crit, hit && blocked, dealt));
                if (pres != null)
                {
                    // if(crit){ ...; combatUpdate(t); combatCrit(hs); await headshotFx(a,t,d); } — headshotFx's
                    // badge/flash hang off OnDamage(crit); its `await hitStop(150)` is this one.
                    if (crit) { pres.UpdateHp(target); pres.Crit(headshots); yield return HitStopFor(150f); }
                    pres.UpdateHp(target);
                    yield return Sleep(shots > 4 ? 0.09f : 0.15f);   // combatUpdate(t); await sleep(w.shots>4?90:150)
                    continue;
                }
                if (crit) yield return HitStopFor(150f);
                yield return Sleep(0.1f); // space out a burst's shots so tracers read individually
            }

            // log(`${a.kr} → ${t.kr}: ${blocked?'シールドに阻まれた':`${hits}/${w.shots} 命中, ${total} ダメージ`}${hs?` (ヘッドショット ×${hs})`:''}${o.preempt?' (先制)':''}`, a.team===G.side?'a':'f')
            HudLog.Add($"{Kr(attacker)} → {Kr(target)}: {(blocked ? "シールドに阻まれた" : $"{hits}/{shots} 命中, {total} ダメージ")}{(headshots > 0 ? $" (ヘッドショット ×{headshots})" : "")}{(preempt ? " (先制)" : "")}",
                HudLog.TeamType(attacker.Team));
            pres?.Result(hits, shots, total, blocked, headshots);   // combatResult(hits,w.shots,total,blocked,hs)

            bool killed = target.HP <= 0;
            if (attacker.Alive) attacker.View?.PlayReload();   // animation only
            OnResolved?.Invoke(attacker, target, new ShotResult(hits, shots, headshots, total, blocked, killed));
            if (killed)
            {
                target.View?.PlayDeath();
                OnKill?.Invoke(target, attacker);   // subscribers do kill()'s escort release (+ its log) first
                // kill(): host → log(`人質が死亡しました！(${by.kr})`,'w'); else log(`${t.kr}が倒された`, t.team===G.side?'f':'a')
                if (target.IsHostage) HudLog.Add($"人質が死亡しました！({Kr(attacker)})", HudLogType.Warn);
                else HudLog.Add($"{Kr(target)}が倒された", target.Team == HudLog.Side ? HudLogType.Foe : HudLogType.Ally);
            }

            // updateHUD(); await sleep(750); combatHide(); await sleep(150);
            if (pres != null)
            {
                yield return Sleep(0.75f);
                pres.Hide();
                yield return Sleep(0.15f);
            }
        }

        /// <summary>u.kr — the operator's display name in katakana (人質 for the hostage).</summary>
        static string Kr(ICombatUnit u) => u is IOperatorNamed n && !string.IsNullOrEmpty(n.Kr) ? n.Kr : u.IsHostage ? "人質" : "?";

        /// <summary>
        /// Port of exchange(f,s,preempt):
        /// <code>await shoot(f,s,{preempt}); if(G.over) return; if(f.alive&amp;&amp;s.alive&amp;&amp;s.team!=='host'&amp;&amp;canShoot(s,f)) await shoot(s,f,{});</code>
        /// Both lines of fire are traced here with traceLine(), like shoot() does in the source.
        /// </summary>
        public IEnumerator Exchange(ICombatUnit first, ICombatUnit second, bool preempt)
            => Engagement(first, second, ExchangeBody(first, second, preempt));

        /// <summary>Staging hint for the director's next engagement (presentation only, read once by it): a mutual
        /// encounter (both saw each other) — the director goes in on the player's unit instead of showing the opening
        /// shot on the map.</summary>
        public bool NextMutual { get; set; }

        IEnumerator ExchangeBody(ICombatUnit first, ICombatUnit second, bool preempt)
        {
            yield return Shoot(first, second, TraceLine(first, second), preempt);
            if (IsOver != null && IsOver()) yield break;
            if (first.Alive && second.Alive && !second.IsHostage)
            {
                var back = TraceLine(second, first);
                if (CanShoot(second, first, back)) yield return Shoot(second, first, back, false);
            }
        }

        /// <summary>
        /// Port of hitStop(ms): freeze the game for a real-time duration via Time.timeScale = 0, then
        /// restore the previous scale. Unlike the JS version (which has to manually fast-forward every
        /// tween/fx timer by the elapsed pause because setTimeout keeps running under the freeze),
        /// Time.timeScale already pauses anything driven by scaled Time.deltaTime, so no compensation
        /// is needed — WaitForSecondsRealtime is what still elapses time while frozen.
        /// </summary>
        public static IEnumerator HitStop(float ms)
        {
            if (ms < 2f) yield break;
            float prevScale = Time.timeScale;
            Time.timeScale = 0f;
            try { yield return new WaitForSecondsRealtime(ms / 1000f); }
            finally { Time.timeScale = prevScale; }
        }
    }
}
