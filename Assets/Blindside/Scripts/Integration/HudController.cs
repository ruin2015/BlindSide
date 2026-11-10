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
        readonly Dictionary<SelectableCard, SimUnit> kitUnits = new Dictionary<SelectableCard, SimUnit>();
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
        void BuildRoster()
        {
            cardUnits.Clear();
            kitUnits.Clear();
            if ((roster == null && kitRoster == null) || units == null) return;
            var mine = new List<SimUnit>();
            foreach (var u in units) if (u.Team == side) mine.Add(u);   // G.units.filter(u=>u.team===G.side)
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

        // SelectableCards: identity from the operator data, the click goes to the same RosterClick as the legacy card
        void BuildKitRoster(List<SimUnit> mine)
        {
            var cards = kitRoster.Build(mine.Count);
            for (int i = 0; i < cards.Count; i++)
            {
                var u = mine[i]; var card = cards[i];
                var op = u.Op;
                string key = op != null ? op.key : u.Id;
                card.SetData(OperatorArt.Face(key), op != null ? op.DisplayName : key, op != null ? op.kr : u.Id, op != null ? op.color : UIPalette.Blue,
                    u.HP, u.MaxHP, $"移動 {M1(u.MoveLeft)}m", RoleIcons.TryGetValue(key, out var icon) ? icon : "◎");
                var c = card;
                card.onClick.AddListener(() => OnKitCardClicked(c));
                kitUnits[card] = u;
            }
            if (Application.isPlaying) kitRoster.PlayEnter();
        }

        // d.onclick=()=>{ if(!u.alive) return; sfx('click'); if(playerCanAct()&&!u.ended) selectUnit(u); else { panTo(u.x,u.y,400); showUnitPanel(u); } }
        void OnCardClicked(HudRosterCard card)
        {
            if (selection == null || !cardUnits.TryGetValue(card, out var u)) return;
            selection.RosterClick(u);
        }

        void OnKitCardClicked(SelectableCard card)
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

            // ---- SelectableCard (.rc) ---- same rules; 行動終了 is the card's own chip, so the move chip keeps the distance
            foreach (var kv in kitUnits)
            {
                var card = kv.Key; var u = kv.Value;
                if (card == null) continue;
                if (phase == MatchPhase.Deploy)
                {
                    card.SetState(u.HP, u.MaxHP, u.Captive ? "拘束中" : u.Placed ? "降下完了" : "ヘリ搭乗中", selected == u, false, card.Dead);
                    continue;
                }
                bool my = PhaseTeam(phase) == u.Team || (phase == MatchPhase.Prep && u.Team == OperatorTeam.Defense);
                string st = !u.Alive ? "" : u.Captive ? "拘束中" : my ? $"移動 {M1(u.MoveLeft)}m" : u.Shield ? "シールド" : u.Ghost ? "妨害" : "";
                card.SetState(u.HP, u.MaxHP, st, selected == u, u.Alive && u.Ended && my, !u.Alive);
            }

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
                if (TutorialGate.Active) body = "① クラッシャーの指示に従って訓練を進める\n② 移動・射撃・視界・ドローン・壁の破壊を習得";
                if (MatchBootstrap.RescueMatch && side == OperatorTeam.Attack) body = "① 拘束された" + string.Join("・", MatchBootstrap.RescueKeys) + "を救出 (救出すると分隊に合流)\n② 防衛チームを全員制圧";
                if (objectivePhaseLine && phase == MatchPhase.Prep) body += $"\n<color=#A37200><b>残り強化 {(selection != null ? selection.reinforceLeft : 10)}/10</b></color>";
                if (objectivePhaseLine && phase == MatchPhase.Deploy) body += "\n<color=#A37200><b>建物の外の好きな位置に隊員を配置</b></color>";

                var ally = new List<bool>(); var enemy = new List<bool>();
                // G.units.some(u=>u.team===ft) — attackers don't exist yet during a defense-side preparation.
                foreach (var u in units) { if (u.Team == side) ally.Add(u.Alive); else if (u.Placed || u.Airborne) enemy.Add(u.Alive); }
                if (enemy.Count == 0) for (int i = 0; i < 5; i++) enemy.Add(false);   // '<i class="off"></i>'.repeat(5)
                if (kitTopbar != null) kitTopbar.SetObjective(body, ally, enemy);
                else objective.Set(body, ally, enemy);
            }
            if (kitTopbar == null && topbar != null) topbar.Relayout();
        }
    }
}
