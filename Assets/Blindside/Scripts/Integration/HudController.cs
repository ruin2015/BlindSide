// Port of buildRoster() + updateHUD()'s #topbar/#roster/#objective/#teamCount half
// (blindside_source/05_ui_input_intro.js:29-55). The #endBtn half of updateHUD stays in
// PlayerSelectionController.SyncEndButton; #log lines arrive through HudLog.
//
// Refresh is event-driven, like the source (updateHUD() is called from phase changes, moves, shots,
// kills and selection changes — never from the render loop): every subscribed event only sets a dirty
// flag, and LateUpdate redraws once if (and only if) something marked it, so a burst of events in one
// frame (a shot's damage + kill + escort change) costs a single refresh.
using System.Collections.Generic;
using UnityEngine;
using Blindside.Combat;
using Blindside.Data;
using Blindside.Turns;
using Blindside.UI;

namespace Blindside.Integration
{
    public class HudController : MonoBehaviour
    {
        [Tooltip("Everything the HUD draws; hidden until a match is bound (the source's #hud.hidden during the intro).")]
        public GameObject content;
        [Tooltip("The kit top bar (Prefabs/UI/HUD_Topbar.prefab: logo, phase + round, objective + survivor pips).")]
        public KitTopbar kitTopbar;
        [Tooltip("The top bar is the mission bar (clear condition top centre; enemies left and the goals top right), put in place of kitTopbar " +
                 "when the match is bound. Off: the old kit bar (phase + round, objective + survivor pips) as before.")]
        public bool useMissionBar = true;
        [Tooltip("The mission bar (UI/Kit/KitMissionBar); made beside kitTopbar at Bind when empty.")]
        public KitMissionBar missionBar;
        [Tooltip("Draw the mission bar (clear condition and goals). Off: it is built and kept up to date but hidden — the battle screen shows neither.")]
        public bool showMissionBar;
        [Tooltip("The tactical HUD (the roster cards, the operator file and END TURN only): the battle log is not drawn.")]
        public bool tacticalHud = true;
        /// <summary>The enemy surveillance cameras still working (the map's and ARGUS's) — MatchBootstrap sets it; the 監視カメラ goal reads it.</summary>
        public System.Func<int> CamerasLeft;
        [Tooltip("Legacy bar (UI/Legacy/HUD_Topbar.prefab); used only when kitTopbar is empty.")]
        public HudTopbar topbar;
        [Tooltip("Legacy objective (inside the legacy bar); used only when kitTopbar is empty.")]
        public HudObjective objective;
        [Tooltip("Adds the preparation / deployment third line (残り強化 N/10 · 建物の外の好きな位置に隊員を配置) to the objective. Off: the " +
                 "command menu's reinforce chip, the hint bar and the log already carry it.")]
        public bool objectivePhaseLine;
        [Tooltip("Legacy list (UI/Legacy/HUD_Roster.prefab); used only when kitRoster is empty.")]
        public HudRoster roster;
        [Tooltip("The operator list of SelectableCards (Prefabs/UI/HUD_Roster.prefab).")]
        public KitRoster kitRoster;
        public HudLogView log;
        [Tooltip("The kit log (Prefabs/UI/HUD_Log.prefab); it listens to HudLog itself. The legacy `log` above is kept, inactive, for reverting.")]
        public KitLogView kitLog;

        TurnManager turns;
        CombatSystem combat;
        EnemyAI ai;
        PlayerSelectionController selection;
        IReadOnlyList<SimUnit> units;
        OperatorTeam side;
        readonly Dictionary<HudRosterCard, SimUnit> cardUnits = new Dictionary<HudRosterCard, SimUnit>();
        readonly Dictionary<TacticalRosterCard, SimUnit> kitUnits = new Dictionary<TacticalRosterCard, SimUnit>();
        readonly Dictionary<TacticalRosterCard, WeaponData> kitGuns = new Dictionary<TacticalRosterCard, WeaponData>();   // the gun each card shows
        bool dirty;

        // the role icon chip per operator (display glyphs, BSTactical): the mockup's attackers, then the defenders
        static readonly Dictionary<string, string> RoleIcons = new Dictionary<string, string>
        {
            { "CRUSHER", "▣" }, { "FLARE", "◎" }, { "IGNIS", "✹" }, { "SERUM", "✚" }, { "BASTION", "⬢" },
            { "VOLT", "✦" }, { "MIASMA", "✹" }, { "WRAITH", "⌖" }, { "OBSIDIAN", "⬢" }, { "ARGUS", "◎" },
        };

        void Awake()
        {
            if (content != null && turns == null) content.SetActive(false);
        }

        public void Bind(TurnManager turns, CombatSystem combat, EnemyAI ai, IReadOnlyList<SimUnit> units,
                         PlayerSelectionController selection, OperatorTeam? playerTeam)
        {
            Unbind();
            this.turns = turns; this.combat = combat; this.ai = ai; this.units = units; this.selection = selection;
            side = playerTeam ?? OperatorTeam.Attack;

            turns.OnPhaseBegin += OnPhase;
            turns.OnFinish += OnFinish;
            combat.OnDamage += OnDamage;
            combat.OnKill += OnKill;
            ai.OnUnitMoved += OnUnitEvent;
            if (selection != null) { selection.SelectionChanged += MarkDirty; selection.UnitsChanged += MarkDirty; }

            if (content != null) content.SetActive(true);   // $('#hud').classList.remove('hidden')
            if (useMissionBar && missionBar == null && kitTopbar != null) missionBar = KitMissionBar.Replace(kitTopbar);
            // the bar stays built and fed (the goals are still tracked), only not drawn unless showMissionBar
            if (missionBar != null && missionBar.gameObject.activeSelf != showMissionBar) missionBar.gameObject.SetActive(showMissionBar);
            // the tactical HUD: the roster cards, the operator file and END TURN only — the log is kept (HudLog still records,
            // the Tab history still reads it) but not drawn
            if (tacticalHud)
            {
                if (kitLog != null && kitLog.gameObject.activeSelf) kitLog.gameObject.SetActive(false);
                if (log != null && log.gameObject.activeSelf) log.gameObject.SetActive(false);
            }
            BindGoals();
            BuildRoster();
            Refresh();
        }

        public void Unbind()
        {
            if (turns != null) { turns.OnPhaseBegin -= OnPhase; turns.OnFinish -= OnFinish; }
            if (combat != null) { combat.OnDamage -= OnDamage; combat.OnKill -= OnKill; }
            if (ai != null) ai.OnUnitMoved -= OnUnitEvent;
            if (selection != null) { selection.SelectionChanged -= MarkDirty; selection.UnitsChanged -= MarkDirty; }
            turns = null; combat = null; ai = null; selection = null; units = null;
            cardUnits.Clear();
            kitUnits.Clear();
            if (content != null) content.SetActive(false);
        }

        void OnDestroy() => Unbind();

        void OnPhase(MatchPhase _) => MarkDirty();
        void OnFinish(MatchResult _, string __) => MarkDirty();
        void OnDamage(ICombatUnit _, int __, bool ___) => MarkDirty();
        void OnKill(ICombatUnit _, ICombatUnit __) => MarkDirty();
        void OnUnitEvent(ITurnUnit _) => MarkDirty();

        public void MarkDirty() => dirty = true;

        void LateUpdate()
        {
            if (!dirty) return;
            dirty = false;
            Refresh();
        }

        // ================================================================
        //  buildRoster()
        // ================================================================
        int rosterBuilt;

        // the squad has grown (a captive freed): the list is built again with it
        void SyncRosterSize()
        {
            int n = 0;
            foreach (var u in units) if (u.Team == side && !u.Captive && !u.Reserve) n++;
            if (n != rosterBuilt) BuildRoster();
        }

        void BuildRoster()
        {
            cardUnits.Clear();
            kitUnits.Clear();
            if ((roster == null && kitRoster == null) || units == null) return;
            var mine = new List<SimUnit>();
            foreach (var u in units) if (u.Team == side && !u.Captive && !u.Reserve) mine.Add(u);   // G.units.filter(u=>u.team===G.side); a captive joins the list once freed
            rosterBuilt = mine.Count;
            if (kitRoster != null) { BuildKitRoster(mine); return; }
            var cards = roster.Build(mine.Count);
            for (int i = 0; i < cards.Count; i++)
            {
                var u = mine[i]; var card = cards[i];
                var op = u.Op;
                string key = op != null ? op.key : u.Id;
                card.SetIdentity(op != null ? op.DisplayName : key, op != null ? $"{op.kr} · {op.role}" : u.Id, op != null ? op.color : UIPalette.Blue);
                card.Clicked += OnCardClicked;
                cardUnits[card] = u;
            }
        }

        // TacticalRosterCards: identity and gun from the operator data, the click goes to the same RosterClick as the legacy card
        void BuildKitRoster(List<SimUnit> mine)
        {
            kitGuns.Clear();
            var cards = kitRoster.Build(mine.Count);
            for (int i = 0; i < cards.Count; i++)
            {
                var u = mine[i]; var card = cards[i];
                var op = u.Op;
                string key = op != null ? op.key : u.Id;
                card.SetData(OperatorArt.Face(key), op != null ? op.DisplayName : key, op != null ? op.color : UIPalette.Blue, u.HP, u.MaxHP);
                SyncGun(card, u);
                var c = card;
                card.onClick.AddListener(() => OnKitCardClicked(c));
                kitUnits[card] = u;
            }
            if (Application.isPlaying) kitRoster.PlayEnter();
        }

        // the gun in hand (it changes with 持ち替え)
        void SyncGun(TacticalRosterCard card, SimUnit u)
        {
            var w = u.Weapon;
            if (kitGuns.TryGetValue(card, out var shown) && shown == w) return;
            kitGuns[card] = w;
            card.SetWeapon(w != null ? GunIcons.Label(w.weaponName, w.key) : "", w != null ? GunIcons.For(w.key) : null);
        }

        // d.onclick=()=>{ if(!u.alive) return; sfx('click'); if(playerCanAct()&&!u.ended) selectUnit(u); else { panTo(u.x,u.y,400); showUnitPanel(u); } }
        void OnCardClicked(HudRosterCard card)
        {
            if (selection == null || !cardUnits.TryGetValue(card, out var u)) return;
            selection.RosterClick(u);
        }

        void OnKitCardClicked(TacticalRosterCard card)
        {
            if (selection == null || !kitUnits.TryGetValue(card, out var u)) return;
            selection.RosterClick(u);
        }

        // ================================================================
        //  updateHUD()
        // ================================================================
        static OperatorTeam? PhaseTeam(MatchPhase p) => p == MatchPhase.Attack ? OperatorTeam.Attack : p == MatchPhase.Defense ? OperatorTeam.Defense : (OperatorTeam?)null;

        // m1=v=>(Math.round(v*10)/10).toFixed(1) — JS rounds halves up (Mathf.Round is banker's).
        static string M1(float v) => (Mathf.Floor(v * 10f + .5f) / 10f).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture);

        public void Refresh()
        {
            if (turns == null || units == null) return;
            var phase = turns.Phase;
            var selected = selection != null ? selection.SelectedUnit : null;
            SyncRosterSize();
            // the deployment's lines (the drop guidance) are for the first main quest only; the Tab window still records them
            if (kitLog != null) kitLog.Muted = phase == MatchPhase.Deploy && !MatchBootstrap.FirstMainQuest;

            // ---- .rcard ----
            foreach (var kv in cardUnits)
            {
                var card = kv.Key; var u = kv.Value;
                if (card == null) continue;
                if (phase == MatchPhase.Deploy)
                {
                    card.SetDeploy(u.HP, u.MaxHP, u.Captive ? "拘束中" : u.Placed ? "降下完了" : "ヘリ搭乗中", selected == u);
                    continue;
                }
                // my = G.phase===u.team || (G.phase==='prep' && u.team==='def')
                bool my = PhaseTeam(phase) == u.Team || (phase == MatchPhase.Prep && u.Team == OperatorTeam.Defense);
                string st = !u.Alive ? "" : u.Captive ? "拘束中" : u.Ended && my ? "行動終了" : my ? $"移動 {M1(u.MoveLeft)}m" : u.Shield ? "シールド" : u.Ghost ? "妨害" : "";
                card.SetState(u.HP, u.MaxHP, st, u.Ended, selected == u, u.Alive && u.Ended && my, !u.Alive, true);
            }

            // ---- TacticalRosterCard ---- same rules; the card turns "移動 …" into 行動中 and its own 行動終了 / K.I.A
            foreach (var kv in kitUnits)
            {
                var card = kv.Key; var u = kv.Value;
                if (card == null) continue;
                SyncGun(card, u);
                if (phase == MatchPhase.Deploy)
                {
                    card.SetState(u.HP, u.MaxHP, u.Captive ? "拘束中" : u.Placed ? "降下完了" : "ヘリ搭乗中", selected == u, false, card.Dead);
                    continue;
                }
                bool my = PhaseTeam(phase) == u.Team || (phase == MatchPhase.Prep && u.Team == OperatorTeam.Defense);
                string st = !u.Alive ? "" : u.Captive ? "拘束中" : my ? $"移動 {M1(u.MoveLeft)}m" : u.Shield ? "シールド" : u.Ghost ? "妨害" : "";
                card.SetState(u.HP, u.MaxHP, st, selected == u, u.Alive && u.Ended && my, !u.Alive);
            }

            if (missionBar != null) { RefreshMission(); return; }

            // ---- #phaseBadge ----
            if (topbar != null || kitTopbar != null)
            {
                bool pre = phase == MatchPhase.Prep || phase == MatchPhase.Deploy;
                bool mine = PhaseTeam(phase) == side || pre;
                var tone = pre ? HudTopbar.BadgeTone.Prep : mine ? HudTopbar.BadgeTone.Ally : HudTopbar.BadgeTone.Foe;
                string who = mine ? " · 味方" : " · 敵";
                string main, sub;
                switch (phase)
                {
                    case MatchPhase.Deploy: main = "DEPLOYMENT"; sub = "攻撃チーム配置フェーズ"; break;
                    case MatchPhase.Prep: main = "PREPARATION"; sub = "防衛準備フェーズ"; break;
                    case MatchPhase.Attack: main = "ATTACKER PHASE"; sub = "攻撃チームの番" + who; break;
                    default: main = "DEFENDER PHASE"; sub = "防衛チームの番" + who; break;
                }
                string mode = side == OperatorTeam.Attack ? "攻撃チーム · 人質救出作戦" : "防衛チーム · 人質死守作戦";
                if (side == OperatorTeam.Attack && MatchBootstrap.RescueMatch) mode = "攻撃チーム · 隊員救出作戦";
                else if (side == OperatorTeam.Attack && MatchBootstrap.HostageCount == 0) mode = "攻撃チーム · 制圧作戦";
                if (TutorialGate.Active) { main = "TRAINING"; sub = "CQB訓練場 · 基礎訓練"; mode = "チュートリアル · 訓練中"; }   // (no phases in the tutorial)
                if (kitTopbar != null)
                {
                    kitTopbar.SetPhase(main, sub, pre ? KitTopbar.Tone.Prep : mine ? KitTopbar.Tone.Ally : KitTopbar.Tone.Foe);
                    kitTopbar.SetRound(turns.Round, TurnManager.MaxRound);
                    kitTopbar.SetMode(mode);
                }
                else
                {
                    topbar.SetPhase(main, sub, tone);
                    topbar.SetRound(turns.Round, TurnManager.MaxRound);
                    topbar.SetMode(mode);
                }
            }

            // ---- #objective + #teamCount ----
            if (objective != null || kitTopbar != null)
            {
                string body = side == OperatorTeam.Attack
                    ? $"① 人質を救出地点{Blindside.Map.ExfilZone.RangeText}のいずれかまで護送\n② または防衛チームを全員制圧"
                    : $"① {TurnManager.MaxRound}ラウンド終了まで人質を死守\n② または攻撃チームを全員制圧";
                if (side == OperatorTeam.Attack && MatchBootstrap.HostageCount == 0 && !MatchBootstrap.RescueMatch)
                    body = "① 防衛チームを全員制圧";
                else if (side == OperatorTeam.Attack && MatchBootstrap.WinRule == Blindside.Map.MapData.WinWipe)
                    body = "① 防衛チームを全員制圧\n② 人質を死なせない";
                else if (side == OperatorTeam.Attack && MatchBootstrap.WinRule == Blindside.Map.MapData.WinExtract)
                    body = $"① 人質{(MatchBootstrap.HostageCount > 1 ? MatchBootstrap.HostageCount + "人全員" : "")}を救出地点{Blindside.Map.ExfilZone.RangeText}のいずれかまで護送\n② 人質を死なせない (制圧だけでは勝利になりません)";
                else if (side == OperatorTeam.Attack && MatchBootstrap.HostageCount > 1)
                    body = $"① 人質{MatchBootstrap.HostageCount}人全員を救出地点{Blindside.Map.ExfilZone.RangeText}のいずれかまで護送\n② または防衛チームを全員制圧";
                else if (side == OperatorTeam.Defense && MatchBootstrap.WinRule == Blindside.Map.MapData.WinExtract)
                    body = $"① {TurnManager.MaxRound}ラウンド終了まで人質を死守\n② または攻撃チームを全員制圧\n(防衛チームが全滅しても試合は続く)";
                if (TutorialGate.Active) body = "① クラッシャーの指示に従って訓練を進める\n② 訓練の内容を習得";
                if (MatchBootstrap.RescueMatch && side == OperatorTeam.Attack) body = "① 拘束された" + string.Join("・", MatchBootstrap.RescueKeys) + "を救出 (救出すると分隊に合流)\n② 防衛チームを全員制圧";
                if (objectivePhaseLine && phase == MatchPhase.Prep) body += $"\n<color=#A37200><b>残り強化 {(selection != null ? selection.reinforceLeft : 10)}/10</b></color>";
                if (objectivePhaseLine && phase == MatchPhase.Deploy) body += "\n<color=#A37200><b>建物の外の好きな位置に隊員を配置</b></color>";

                var ally = new List<bool>(); var enemy = new List<bool>();
                // G.units.some(u=>u.team===ft) — attackers don't exist yet during a defense-side preparation.
                foreach (var u in units) { if (u.Team == side) { if (!u.Captive) ally.Add(u.Alive); } else if (u.Placed || u.Airborne) enemy.Add(u.Alive); }
                if (enemy.Count == 0) for (int i = 0; i < 5; i++) enemy.Add(false);   // '<i class="off"></i>'.repeat(5)
                if (kitTopbar != null) kitTopbar.SetObjective(body, ally, enemy);
                else objective.Set(body, ally, enemy);
            }
            if (kitTopbar == null && topbar != null) topbar.Relayout();
        }

        // ================================================================
        //  The mission bar: the clear condition, the enemies left, the goals (the operation's challenges, as RankRules will judge them
        //  at the end — a goal that can no longer be met shows 失敗 at once)
        // ================================================================
        readonly List<Blindside.Flow.MapChallenge> goals = new List<Blindside.Flow.MapChallenge>(4);
        readonly List<string> goalTexts = new List<string>(4);
        readonly List<KitMissionBar.Goal> goalStates = new List<KitMissionBar.Goal>(4);

        void BindGoals()
        {
            goals.Clear(); goalTexts.Clear(); goalStates.Clear();
            if (TutorialGate.Active) return;   // (the training hall has no rank)
            // the same list the rank is judged by (MatchAward: the save's last map; no save: the built-in house's)
            var save = Blindside.Save.SaveSystem.Current;
            foreach (var g in Blindside.Progress.Challenges.Of(save != null ? save.lastMap ?? "" : ""))
            {
                if (g == null) continue;
                goals.Add(g);
                goalTexts.Add(Blindside.Progress.Challenges.Text(g));
                goalStates.Add(KitMissionBar.Goal.Pending);
            }
        }

        void RefreshMission()
        {
            ClearCondition(out var l1, out var l2);
            missionBar.SetCondition(l1, l2);

            int foes = 0, lost = 0;
            foreach (var u in units)
            {
                if (u.Captive || u.Reserve) continue;   // (a captive isn't fighting yet; a reserve isn't on the map)
                if (u.Team == side) { if (!u.Alive) lost++; }
                else if (u.Alive) foes++;
            }
            missionBar.SetEnemies(foes);

            bool over = turns.Over;
            bool won = over && turns.Result == (side == OperatorTeam.Attack ? MatchResult.AttackWins : MatchResult.DefenseWins);
            int cams = CamerasLeft != null ? CamerasLeft() : -1;
            for (int i = 0; i < goals.Count; i++)
            {
                var g = goals[i];
                KitMissionBar.Goal st;
                switch (g.kind)
                {
                    case Blindside.Flow.ChallengeKind.NoLosses:
                        st = lost > 0 ? KitMissionBar.Goal.Failed : over ? (won ? KitMissionBar.Goal.Done : KitMissionBar.Goal.Failed) : KitMissionBar.Goal.Pending; break;
                    case Blindside.Flow.ChallengeKind.WithinRounds:
                        st = turns.Round > g.value ? KitMissionBar.Goal.Failed : over ? (won ? KitMissionBar.Goal.Done : KitMissionBar.Goal.Failed) : KitMissionBar.Goal.Pending; break;
                    case Blindside.Flow.ChallengeKind.AllCameras:
                        st = cams == 0 ? KitMissionBar.Goal.Done : over ? KitMissionBar.Goal.Failed : KitMissionBar.Goal.Pending; break;
                    default:
                        st = foes == 0 ? KitMissionBar.Goal.Done : over ? KitMissionBar.Goal.Failed : KitMissionBar.Goal.Pending; break;
                }
                goalStates[i] = st;
            }
            missionBar.SetGoals(goalTexts, goalStates);
        }

        /// <summary>The side's clear condition in one or two lines (the old objective's ① / ② without the numbering).</summary>
        void ClearCondition(out string a, out string b)
        {
            b = null;
            int hostages = MatchBootstrap.HostageCount;
            var exfils = Blindside.Map.ExfilZone.All;
            string zone = exfils.Count == 1 ? $"救出地点(EXFIL {exfils[0].Name})" : $"救出地点{Blindside.Map.ExfilZone.RangeText}のいずれか";
            string who = hostages > 1 ? $"人質{hostages}人全員" : "人質";
            if (TutorialGate.Active) { a = "クラッシャーの指示に従って訓練を進める"; return; }
            if (side == OperatorTeam.Defense)
            {
                a = $"{TurnManager.MaxRound}ラウンド終了まで人質を死守";
                b = "または攻撃チームを全員制圧";
                return;
            }
            if (MatchBootstrap.RescueMatch) { a = "拘束された" + string.Join("・", MatchBootstrap.RescueKeys) + "を救出"; b = "または防衛チームを全員制圧"; return; }
            if (hostages == 0) { a = "敵を全員制圧"; return; }
            if (MatchBootstrap.WinRule == Blindside.Map.MapData.WinWipe) { a = "敵を全員制圧"; b = "人質を死なせない"; return; }
            a = $"{who}を{zone}まで護送";
            b = MatchBootstrap.WinRule == Blindside.Map.MapData.WinExtract ? "人質を死なせない" : "または敵を全員制圧";
        }
    }
}
