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
        /// <summary>The line goes through a smoke screen (スモーク弾): harder to hit.</summary>
        public readonly bool Smoked;
        public ShotPath(bool blocked, int walls, bool smoked = false) { Blocked = blocked; Walls = walls; Smoked = smoked; }
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
        public const float SmokePenalty = 30f;

        public static int HitChance(ICombatUnit attacker, ICombatUnit target, ShotPath path, bool preempt)
        {
            WeaponData w = attacker.Weapon;
            float d = Distance(attacker, target);
            float c = w.accuracy;
            if (w.breach && d <= 2.5f) c += 15f;
            if (d > w.range) c -= (d - w.range) * w.accuracyFalloffPerTile;
            c -= path.Walls * 22f;
            if (path.Smoked) c -= SmokePenalty;
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
            if (target is IItemEffects fx && fx.Flashed) return 100;   // (閃光弾: every hit is a headshot)
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
            bool smoked = false;
            int s = JsRoundI(y0) * W + JsRoundI(x0), e = JsRoundI(y1) * W + JsRoundI(x1);
            for (int i = 1; i < n; i++)
            {
                int cx = JsRoundI(x0 + dx * i / n), cy = JsRoundI(y0 + dy * i / n), k = cy * W + cx;
                if (k == s || k == e || k == last) continue;
                last = k;
                bool inside = cx >= 0 && cy >= 0 && cx < W && cy < H;          // TL(): 'void' off the map
                var t = inside ? map.TileAt(cx, cy) : TileType.Hard;
                if (!inside || t == TileType.Hard) return new ShotPath(true, walls);
                if (map.SmokeCells.Count > 0 && map.SmokeCells.Contains(k)) smoked = true;
                if (t == TileType.Wall)
                {
                    int pi = map.PanelIndexAt[k];
                    // a reinforced wall stops the round (a mirror on one too); a mirror on a plain wall is shot through
                    if (pi >= 0 && map.Panels[pi].reinforced) return new ShotPath(true, walls);
                    walls++;
                }
            }
            return new ShotPath(false, walls, smoked);
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
        public event System.Action<ICombatUnit, ICombatUnit> OnSupportFire;           // an ally joins in with 支援射撃: who, target

        /// <summary>
        /// Port of shoot(a,t,o). `path` is the attacker→target line trace the caller already has
        /// (MapGrid/VisionSystem); mirrors the source's early return when the shot is blocked or out
        /// of range. Damage is ×1.4 per shot on a preemptive attack, and ×2 on a headshot crit.
        /// </summary>
        public IEnumerator Shoot(ICombatUnit attacker, ICombatUnit target, ShotPath path, bool preempt, bool sureHit = false)
        {
            if (!attacker.Alive || !target.Alive) yield break;
            // behind a raised shield (BASTION) there is no shooting — not in an encounter, not in return fire (co-op rank 8: he can)
            if (attacker.ShieldUp && !CoopRules.CanFireShielded(attacker))
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
            // 弱点露出 (GHOST's co-op): twice the crit chance and twice the damage
            if (CoopRules.IsExposed(target)) { hsChance = Mathf.Min(hsChance >= 100 ? 100 : 95, hsChance * 2); dmg *= 2; }

            // combatShow(a,t,chance,dmg,w.shots,o.preempt,tr.walls,blocked,blocked?0:hsC); await sleep(700);
            var pres = Presenter;
            if (pres != null)
            {
                pres.Show(attacker, target, sureHit ? 100 : chance, dmg, shots, preempt, path.Walls, blocked, blocked ? 0 : hsChance);
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
                // (a sure hit — an encounter's first strike — still draws its roll: the forecast and the stream stay in step)
                bool hit = Blindside.Core.GameRng.Range(0f, 100f) < (i == 0 ? firstChance : chance) || sureHit;
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
                        if (target.HP <= 0 && target is IItemEffects rv && rv.ReviveHp > 0) rv.Revive();   // (除細動器: it never goes down)
                        if (crit) headshots++;
                        if (!down)
                        {
                            target.View?.PlayHit(crit);
                            OnDamage?.Invoke(target, d, crit);
                        }
                    }
                }
                if (down) continue;
                if (target.HP <= 0) { down = true; if (!Held(target)) target.View?.PlayDeath(); }   // animation only (kill() itself follows the volley)
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
                if (!Held(target)) target.View?.PlayDeath();
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

            // 追撃 (co-op rank 2): allies who see the target fire one round each with it
            if (!pursuing && PursuitAllies != null && attacker.Alive && target.Alive && !blocked)
            {
                pursuers.Clear();
                PursuitAllies(attacker, target, pursuers);
                if (pursuers.Count > 0)
                {
                    pursuing = true;
                    try { for (int i = 0; i < pursuers.Count && i < CoopRules.MaxPursuers && target.Alive; i++) yield return PursuitShot(pursuers[i], target); }
                    finally { pursuing = false; }
                }
            }
        }

        /// <summary>The player's side's 追撃 (co-op rank 2): given the shooter and its target, fills the allies who join in (they see the
        /// target and can shoot it). Null: no pursuit (AI-only matches, the tutorial).</summary>
        public System.Action<ICombatUnit, ICombatUnit, List<ICombatUnit>> PursuitAllies;
        readonly List<ICombatUnit> pursuers = new List<ICombatUnit>(4);
        bool pursuing;

        /// <summary>One round from `a` at `t` (the pursuit): its own hit and crit roll, the gun's damage at the distance; the kill as a
        /// normal shot's.</summary>
        IEnumerator PursuitShot(ICombatUnit a, ICombatUnit t)
        {
            if (!a.Alive || !t.Alive || a.Weapon == null) yield break;
            if (a.ShieldUp && !CoopRules.CanFireShielded(a)) yield break;
            var path = TraceLine(a, t);
            float dist = Distance(a, t);
            if (path.Blocked || dist > a.Weapon.range + 4f) yield break;
            OnSupportFire?.Invoke(a, t);
            a.Facing = Mathf.Atan2(t.Y - a.Y, t.X - a.X);
            a.View?.SetFacing(a.Facing);
            OnShotStart?.Invoke(a, t);
            a.View?.PlayFire();
            int chance = HitChance(a, t, path, false), hs = HeadshotChance(a, t, path, false);
            int dmg = Mathf.RoundToInt(CombatModifiers.BaseDamage(a, dist));
            if (a is ICombatModifiers am && am.DamageScale != 1f) dmg = Mathf.RoundToInt(dmg * am.DamageScale);
            if (CoopRules.IsExposed(t)) { hs = Mathf.Min(hs >= 100 ? 100 : 95, hs * 2); dmg *= 2; }
            bool blocked = ShieldBlocks(t, a);
            bool hit = Blindside.Core.GameRng.Range(0f, 100f) < chance, crit = false;
            int dealt = 0;
            if (hit && !blocked)
            {
                crit = Blindside.Core.GameRng.Range(0f, 100f) < hs;
                int d = crit ? dmg * 2 : dmg;
                float takenScale = t is ICombatModifiers tm ? tm.DamageTakenScale : 1f;
                if (takenScale != 1f) d = Mathf.Max(1, Mathf.RoundToInt(d * takenScale));
                dealt = d;
                t.HP = Mathf.Max(FloorHP, t.HP - d);
                t.View?.PlayHit(crit);
                OnDamage?.Invoke(t, d, crit);
            }
            OnShotFired?.Invoke(a, t, hit, crit);
            OnShot?.Invoke(new ShotEvent(a, t, 0, 1, hit, crit, hit && blocked, dealt));
            HudLog.Add($"{Kr(a)}: 支援射撃 → {Kr(t)} {(hit && !blocked ? dealt + " ダメージ" + (crit ? " (ヘッドショット)" : "") : "外れ")}", HudLog.TeamType(a.Team));
            yield return Sleep(.35f);
            if (t.HP <= 0)
            {
                if (!Held(t)) t.View?.PlayDeath();
                OnKill?.Invoke(t, a);
                HudLog.Add($"{Kr(t)}が倒された", t.Team == HudLog.Side ? HudLogType.Foe : HudLogType.Ally);
            }
        }

        /// <summary>Presentation hook (the battle director): true while a unit's fall is shown somewhere else (the battle stage) — the
        /// map figure is then not felled by the shot; the director lays it down when the stage ends. Null: never held.</summary>
        public System.Func<ICombatUnit, bool> HoldDeath;
        bool Held(ICombatUnit t) => HoldDeath != null && HoldDeath(t);

        /// <summary>u.kr — the operator's display name in katakana (人質 for the hostage).</summary>
        static string Kr(ICombatUnit u) => u is IOperatorNamed n && !string.IsNullOrEmpty(n.Kr) ? n.Kr : u.IsHostage ? "人質" : "?";

        /// <summary>
        /// Port of exchange(f,s,preempt):
        /// <code>await shoot(f,s,{preempt}); if(G.over) return; if(f.alive&amp;&amp;s.alive&amp;&amp;s.team!=='host'&amp;&amp;canShoot(s,f)) await shoot(s,f,{});</code>
        /// Both lines of fire are traced here with traceLine(), like shoot() does in the source.
        /// </summary>
        public IEnumerator Exchange(ICombatUnit first, ICombatUnit second, bool preempt)
        {
            bool sure = NextSureHit;
            NextSureHit = false;
            ExchangeSureHit = sure;
            return Engagement(first, second, ExchangeBody(first, second, preempt, sure));
        }

        /// <summary>The next <see cref="Exchange"/>'s opening volley always hits (read once): an encounter's first strike, before the
        /// fight proper (EncounterResolver: SelfOnly / EnemyOnly). The return fire rolls as usual.</summary>
        public bool NextSureHit { get; set; }
        /// <summary>The exchange being started opens with a sure hit (set by Exchange; the director reads and clears it, like NextMutual —
        /// an engagement that is not an Exchange never sees a stale one).</summary>
        public bool ExchangeSureHit { get; set; }

        /// <summary>Staging hint for the director's next engagement (presentation only, read once by it): a mutual
        /// encounter (both saw each other) — the director goes in on the player's unit instead of showing the opening
        /// shot on the map.</summary>
        public bool NextMutual { get; set; }

        // ================================================================
        //  The engagement forecast: the coming exchange rolled ahead on a copy of the game's random stream
        // ================================================================
        /// <summary>One volley as it is going to play out.</summary>
        public struct VolleyOutcome
        {
            public bool fired, blocked, preempt;
            public int chance, hsChance, dmg, shots, hits, crits, total;
        }

        /// <summary>
        /// Exactly what <see cref="Exchange"/>(first, second, preempt) is going to do if it starts now: the same rolls in the same order
        /// on a copy of GameRng (nothing is drawn from the real stream, no unit is changed). As long as nothing else draws from GameRng
        /// before the exchange starts, the forecast and the fight are the same. Keep it in step with <see cref="Shoot"/> and ExchangeBody.
        /// </summary>
        /// <param name="pursuit">the match's <see cref="PursuitAllies"/> (null: no 追撃)</param>
        /// <param name="pursuits">filled with the 追撃 rounds that will be fired, in order</param>
        public static void ForecastExchange(MapGrid map, ICombatUnit first, ICombatUnit second, bool preempt,
                                            out VolleyOutcome opening, out VolleyOutcome reply, out int firstHp, out int secondHp, bool sureHit = false,
                                            System.Action<ICombatUnit, ICombatUnit, List<ICombatUnit>> pursuit = null, List<PursuitOutcome> pursuits = null)
        {
            var r = Blindside.Core.GameRng.Peek();
            firstHp = first.HP; secondHp = second.HP;
            reply = default;
            pursuits?.Clear();
            opening = ForecastVolley(first, second, TraceLine(map, first.X, first.Y, second.X, second.Y), preempt, ref secondHp, r, sureHit);
            if (opening.fired && !opening.blocked && firstHp > 0 && secondHp > 0) ForecastPursuit(map, first, second, ref secondHp, r, pursuit, pursuits);
            // ExchangeBody: the return fire when both still stand and the target can shoot back
            if (firstHp > 0 && secondHp > 0 && !second.IsHostage)
            {
                var back = TraceLine(map, second.X, second.Y, first.X, first.Y);
                if (CanShoot(second, first, back))
                {
                    reply = ForecastVolley(second, first, back, false, ref firstHp, r);
                    if (reply.fired && !reply.blocked && firstHp > 0 && secondHp > 0) ForecastPursuit(map, second, first, ref firstHp, r, pursuit, pursuits);
                }
            }
        }

        /// <summary>One 追撃 round as it is going to play out.</summary>
        public struct PursuitOutcome
        {
            public ICombatUnit who;
            public bool hit, crit, blocked;
            public int damage;
        }

        static readonly List<ICombatUnit> forecastPursuers = new List<ICombatUnit>(4);

        // what Shoot's 追撃 does after a volley (PursuitShot's rolls, on the copy of the stream)
        static void ForecastPursuit(MapGrid map, ICombatUnit attacker, ICombatUnit target, ref int targetHp, System.Random r,
                                    System.Action<ICombatUnit, ICombatUnit, List<ICombatUnit>> pursuit, List<PursuitOutcome> into)
        {
            if (pursuit == null) return;
            forecastPursuers.Clear();
            pursuit(attacker, target, forecastPursuers);
            for (int i = 0; i < forecastPursuers.Count && i < CoopRules.MaxPursuers && targetHp > 0; i++)
            {
                var a = forecastPursuers[i];
                if (!a.Alive || a.Weapon == null) continue;
                if (a.ShieldUp && !CoopRules.CanFireShielded(a)) continue;
                var path = TraceLine(map, a.X, a.Y, target.X, target.Y);
                float dist = Distance(a, target);
                if (path.Blocked || dist > a.Weapon.range + 4f) continue;
                int chance = HitChance(a, target, path, false), hs = HeadshotChance(a, target, path, false);
                int dmg = Mathf.RoundToInt(CombatModifiers.BaseDamage(a, dist));
                if (a is ICombatModifiers am && am.DamageScale != 1f) dmg = Mathf.RoundToInt(dmg * am.DamageScale);
                if (CoopRules.IsExposed(target)) { hs = Mathf.Min(hs >= 100 ? 100 : 95, hs * 2); dmg *= 2; }
                var o = new PursuitOutcome { who = a, blocked = ShieldBlocks(target, a) };
                o.hit = (float)r.NextDouble() * 100f < chance;
                if (o.hit && !o.blocked)
                {
                    o.crit = (float)r.NextDouble() * 100f < hs;
                    int d = o.crit ? dmg * 2 : dmg;
                    float takenScale = target is ICombatModifiers tm ? tm.DamageTakenScale : 1f;
                    if (takenScale != 1f) d = Mathf.Max(1, Mathf.RoundToInt(d * takenScale));
                    o.damage = d;
                    targetHp = Mathf.Max(FloorHP, targetHp - d);
                }
                into?.Add(o);
            }
        }

        static VolleyOutcome ForecastVolley(ICombatUnit attacker, ICombatUnit target, ShotPath path, bool preempt, ref int targetHp, System.Random r, bool sureHit = false)
        {
            var o = new VolleyOutcome();
            if (attacker.ShieldUp && !CoopRules.CanFireShielded(attacker)) return o;
            WeaponData w = attacker.Weapon;
            if (w == null || path.Blocked || Distance(attacker, target) > w.range + 4f) return o;
            o.fired = true; o.preempt = preempt;
            o.chance = HitChance(attacker, target, path, preempt);
            o.hsChance = HeadshotChance(attacker, target, path, preempt);
            var gear = CombatModifiers.GearOf(attacker);
            o.shots = CombatModifiers.ShotsOf(attacker);
            int firstChance = gear != null && gear.firstAcc != 0f ? JsRound(o.chance + gear.firstAcc, 5, 95) : o.chance;
            int dmg = Mathf.RoundToInt(CombatModifiers.BaseDamage(attacker, Distance(attacker, target)) * (preempt ? 1.4f : 1f));
            if (attacker is ICombatModifiers am && am.DamageScale != 1f) dmg = Mathf.RoundToInt(dmg * am.DamageScale);
            if (CoopRules.IsExposed(target)) { o.hsChance = Mathf.Min(o.hsChance >= 100 ? 100 : 95, o.hsChance * 2); dmg *= 2; }
            o.dmg = dmg;
            float takenScale = target is ICombatModifiers tm ? tm.DamageTakenScale : 1f;
            int revive = target is IItemEffects rv ? rv.ReviveHp : 0;
            o.blocked = ShieldBlocks(target, attacker);
            for (int i = 0; i < o.shots; i++)
            {
                // (GameRng.Range(0,100) = 0 + NextDouble() * 100, float like the real roll)
                bool hit = (float)r.NextDouble() * 100f < (i == 0 ? firstChance : o.chance) || sureHit;
                if (!hit || o.blocked) continue;
                bool crit = (float)r.NextDouble() * 100f < o.hsChance;
                int d = crit ? dmg * 2 : dmg;
                if (takenScale != 1f) d = Mathf.Max(1, Mathf.RoundToInt(d * takenScale));
                o.hits++; o.total += d; if (crit) o.crits++;
                targetHp = Mathf.Max(FloorHP, targetHp - d);
                if (targetHp <= 0 && revive > 0) { targetHp = revive; revive = 0; }
            }
            if (sureHit) o.chance = 100;
            return o;
        }

        IEnumerator ExchangeBody(ICombatUnit first, ICombatUnit second, bool preempt, bool sureHit = false)
        {
            yield return Shoot(first, second, TraceLine(first, second), preempt, sureHit);
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
