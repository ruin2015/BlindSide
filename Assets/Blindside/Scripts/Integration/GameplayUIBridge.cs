// Connects PART A's gameplay events (CombatSystem, TurnManager, EnemyAI) to the PART C/D UI
// components (Blindside.UI's cinematic widgets). Example from the request:
// CombatSystem.OnShotFired(crit=true) -> UIHeadshotBadge.Play(...).
//
// The four widgets are built here at runtime (AddComponent, never saved to a prefab) rather than
// loaded from pre-authored prefabs: this editor session hit a persistent Unity bug where several
// of these exact class names, once ever compiled inside the old multi-class BlindsideUIAnim.cs,
// keep serializing as "Missing (Mono Script)" in any *saved* prefab even after the classes were
// split into their own files and the project recompiled — but plain runtime AddComponent (which
// never touches prefab/script-GUID serialization) is unaffected, so building live is bulletproof.
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Blindside.Combat;
using Blindside.Turns;
using Blindside.UI;
using Blindside.Rendering;
using Blindside.Data;

namespace Blindside.Integration
{
    public class GameplayUIBridge : MonoBehaviour, IShotPresenter
    {
        [Header("PART C/D widgets (auto-built at runtime if left empty)")]
        public UIHeadshotBadge headshotBadge;
        public UICombatPanel combatPanel;   // #combat cut-in
        public UIPhaseBanner phaseBanner;
        public UIContactAlert contactAlert;
        public UIPulse endTurnPulse;
        public Button endTurnButton;

        [Header("Camera used to project world hit points onto the headshot badge canvas")]
        public Camera worldCamera;

        Canvas runtimeCanvas;
        TurnManager boundTurns;
        TMP_Text endTurnMain, endTurnSub;   // #endBtn's b / small (START OP ↔ END TURN)

        /// <summary>#endBtn onclick. Set by PlayerSelectionController (marks the player's units ended,
        /// then ends the phase); falls back to ending the phase directly when nothing is bound.</summary>
        public System.Action EndTurnHandler;

        void Awake()
        {
            if (worldCamera == null) worldCamera = Camera.main;
            BuildRuntimeWidgets();
        }

        void BuildRuntimeWidgets()
        {
            var canvasGo = new GameObject("GameplayUIBridge_Canvas", typeof(RectTransform));
            canvasGo.transform.SetParent(transform, false);
            runtimeCanvas = canvasGo.AddComponent<Canvas>();
            runtimeCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            UITheme.Current.ApplyTo(canvasGo.AddComponent<CanvasScaler>(), UITheme.CanvasKind.Hud);
            canvasGo.AddComponent<GraphicRaycaster>();

            // the widgets themselves are HudWidgets' (Blindside.UI), shared with the editor previews
            if (phaseBanner == null) phaseBanner = HudWidgets.BuildPhaseBanner(runtimeCanvas.transform);
            if (contactAlert == null) contactAlert = HudWidgets.BuildContactAlert(runtimeCanvas.transform);
            if (headshotBadge == null) headshotBadge = HudWidgets.BuildHeadshotBadge(runtimeCanvas.transform, runtimeCanvas);
            if (endTurnPulse == null)
            {
                // #endBtn onclick — closes out the player's Attack/Defense phase. Hidden outside their phase.
                var end = HudWidgets.BuildEndTurn(runtimeCanvas.transform, () => { if (EndTurnHandler != null) EndTurnHandler(); else boundTurns?.EndPlayerTurn(); });
                endTurnPulse = end.pulse; endTurnButton = end.button; endTurnMain = end.main; endTurnSub = end.sub;
            }
            // #combat z-index 45 sits under the headshot badge/flash (58/59): build it first so the
            // badge stays the later sibling.
            if (combatPanel == null)
            {
                combatPanel = UICombatPanel.Build(runtimeCanvas.transform);
                combatPanel.transform.SetSiblingIndex(headshotBadge != null ? headshotBadge.transform.GetSiblingIndex() : 0);
            }
        }

        // ================================================================
        //  #combat — IShotPresenter (CombatSystem.Shoot drives these with the source's pacing)
        // ================================================================
        OperatorTeam side = OperatorTeam.Attack;   // G.side (AI-vs-AI reads as the attack side, like the HUD)
        static readonly Color HostCol = UIPalette.Hex("#FFB13B");

        static string KeyOf(ICombatUnit u) => u is SimUnit su ? (su.Op != null ? su.Op.DisplayName : su.Id) : u is SimHostage h ? h.Key : "?";   // (the name shown)
        static string KrOf(ICombatUnit u) => u is Blindside.Movement.IOperatorNamed n ? n.Kr : u.IsHostage ? "人質" : "?";
        static int MaxHpOf(ICombatUnit u) => u is SimUnit su ? su.MaxHP : u is SimHostage h ? h.MaxHP : 100;

        // ca=a.team===G.side?ally:foe; ct=t.team==='host'?'#ffb13b':t.team===G.side?ally:foe
        /// <summary>Every volley as it opens: shooter, target, whether it is a preemptive one (the squad radio listens).</summary>
        public static System.Action<ICombatUnit, ICombatUnit, bool> VolleyShown;

        public void Show(ICombatUnit a, ICombatUnit t, int chance, int dmg, int shots, bool preempt, int walls, bool blocked, int hsChance)
        {
            VolleyShown?.Invoke(a, t, preempt);
            if (combatPanel == null) return;
            Color ca = a.Team == side ? UIPalette.Blue : UIPalette.Foe;
            Color ct = t.IsHostage ? HostCol : t.Team == side ? UIPalette.Blue : UIPalette.Foe;
            string wName = a.Weapon != null ? a.Weapon.weaponName : "";
            combatPanel.Show(KeyOf(a), $"{KrOf(a)} · {wName}", ca, a.HP, MaxHpOf(a),
                             KeyOf(t), KrOf(t), ct, t.HP, MaxHpOf(t),
                             chance, dmg, shots, preempt, walls, blocked, hsChance);
        }

        public void UpdateHp(ICombatUnit t) { if (combatPanel != null) combatPanel.UpdateTarget(t.HP, MaxHpOf(t)); }
        public void Crit(int headshots) { if (combatPanel != null) combatPanel.Crit(); }
        public void Result(int hits, int shots, int total, bool blocked, int headshots) { if (combatPanel != null) combatPanel.Result(hits, shots, total, blocked, headshots); }
        public void Hide() { if (combatPanel != null) combatPanel.Hide(); }

        /// <summary>showContact's banner: `colorKind` 1 = blue (.m1), 2 = yellow (.m2), 3 = red (no class); 1.1s.</summary>
        public System.Collections.IEnumerator PlayContact(int colorKind, string sub)
        {
            if (contactAlert == null) { yield return new WaitForSecondsRealtime(1.1f); yield break; }
            yield return contactAlert.Play(colorKind, sub);
        }


        // Hit points come from the unit's own view (its art owns where the torso/head are); the
        // fallbacks are the source's fixed heights for a unit without one.
        static Vector3 TorsoOf(ICombatUnit u) => u.View != null ? u.View.TorsoPoint : GridSpace.GridToWorld(u.X, u.Y, 0.5f);
        static Vector3 LabelOf(ICombatUnit u) => Blindside.Cinematic.BattleDirector.StageLabel(u)
            ?? (u.View != null ? u.View.LabelPoint : GridSpace.GridToWorld(u.X, u.Y, 1.2f));

        static Vector3 HeadOf(ICombatUnit u) => u.View != null ? u.View.HeadPoint : GridSpace.GridToWorld(u.X, u.Y, .8f);

        public void Bind(CombatSystem combat, TurnManager turns, EnemyAI ai, OperatorTeam? playerTeam = null)
        {
            boundTurns = turns;
            side = playerTeam ?? OperatorTeam.Attack;
            if (combatPanel != null) combatPanel.Hide();
            combat.Presenter = this;   // shoot()'s combatShow/…/combatHide

            // 射撃 (shot fired): tracer + muzzle flash at every shot, hit or miss.
            // a miss flies wide of the body and on past it (CombatFx.Tracer)
            combat.OnShotFired += (attacker, target, hit, crit) =>
                CombatFx.Tracer(TorsoOf(attacker), attacker.Facing, TorsoOf(target), hit, attacker.View != null ? attacker.View.MuzzlePoint : (Vector3?)null);

            // ヘッドショット (headshot): headshotFx(a,t,d) — badge "ヘッドショット · クリティカル ×2 · -{d}" + #hsFlash. OnDamage carries
            // the crit's damage; the hit stop that follows is CombatSystem.Shoot's own HitStop(150).
            combat.OnDamage += (target, amount, crit) =>
            {
                if (!crit || headshotBadge == null || worldCamera == null) return;
                // On the battle stage the shot lands later (after the shooter's cut): the stage plays the badge then.
                if (Blindside.Cinematic.BattleDirector.StageHandlesHeadshots) return;
                // el.className='hs '+(mine?'':'foe') with mine=a.team===G.side ⇔ the target isn't ours.
                // AI-vs-AI (no G.side) keeps reading the attack side as "ours".
                bool enemyScored = target.Team == side;
                // Source: proj(t.rx,1.05,t.ry) — just over the head; the view's overhead label anchor. The reticle locks on the head.
                headshotBadge.Play(LabelOf(target), worldCamera, amount, enemyScored, HeadOf(target));
            };
            // on the battle stage only the HP bars of the #combat panel stay
            // on the battle stage only the HP bars of the #combat panel stay
            Blindside.Cinematic.BattleDirector.StageUi = on => { if (combatPanel != null) combatPanel.SetHpOnly(on); };
            Blindside.Cinematic.BattleDirector.HeadshotUi = (target, label, head, amount) =>
            {
                if (headshotBadge != null && worldCamera != null) headshotBadge.Play(label, worldCamera, amount, target.Team == side, head);
            };

            // Blast / kill aftermath (optional): a collapse effect where the target falls.
            combat.OnKill += (target, killer) =>
                CombatFx.Boom(GridSpace.GridToWorld(target.X, target.Y), new Color32(0x8a, 0x93, 0x9e, 0xff), 14);

            // フェーズ切替 (phase change): TurnManager.OnPhaseBegin -> UIPhaseBanner.Play(...).
            turns.OnPhaseBegin += phase =>
            {
                if (phaseBanner == null) return;
                if (TutorialGate.Active) return;   // the tutorial opens with its own entrance, no phase banner
                switch (phase)
                {
                    case MatchPhase.Deploy:
                        // showBanner('deploy'): #ffc81e, "DEPLOYMENT", kr line, sub 'BEFORE BREACH'.
                        if (playerTeam == OperatorTeam.Attack) phaseBanner.Play("DEPLOYMENT", "攻撃チーム配置 — 建物の外の好きな位置に隊員を配置", "BEFORE BREACH", UIPalette.Accent);
                        else phaseBanner.Play("DEPLOY", "配置フェーズ", "攻撃チームの侵入地点を選択してください", UIPalette.Blue);
                        break;
                    case MatchPhase.Prep:
                        // showBanner('prep'): #ffc81e, "PREPARATION", kr line, sub 'BEFORE BREACH'.
                        if (playerTeam == OperatorTeam.Defense) phaseBanner.Play("PREPARATION", "防衛準備フェーズ — 壁の強化・装置の設置・配置", "BEFORE BREACH", UIPalette.Accent);
                        else phaseBanner.Play("PREPARE", "準備フェーズ", "防衛チームの要塞化を進めてください", UIPalette.Accent);
                        break;
                    case MatchPhase.Attack: phaseBanner.Play("ATTACKER PHASE", "攻撃ターン", "部隊を展開してください", UIPalette.Blue); break;
                    case MatchPhase.Defense: phaseBanner.Play("DEFENDER PHASE", "防衛ターン", "区域を死守してください", UIPalette.Foe); break;
                }
            };

            // Player-turn gate: END TURN shows only in the chosen side's Attack/Defense phase
            // (TurnManager.RunMatch holds that phase until EndPlayerTurn() is called).
            if (endTurnButton != null)
            {
                turns.OnPhaseBegin += phase =>
                {
                    bool isDeploy = (phase == MatchPhase.Deploy && playerTeam == OperatorTeam.Attack)
                                    || (phase == MatchPhase.Prep && playerTeam == OperatorTeam.Defense);   // both read START OP / 作戦開始
                    bool isPlayerPhase = isDeploy || (playerTeam.HasValue
                        && (phase == MatchPhase.Attack || phase == MatchPhase.Defense)
                        && TurnManager.ActingTeam(phase) == playerTeam.Value);
                    endTurnButton.gameObject.SetActive(isPlayerPhase);
                    endTurnButton.interactable = isPlayerPhase;
                    // updateHUD: pre-round phases read "START OP / 作戦開始", the rest "END TURN / ターン終了".
                    if (endTurnMain != null) endTurnMain.text = isDeploy ? "START OP" : "END TURN";
                    if (endTurnSub != null) endTurnSub.text = isDeploy ? "作戦開始" : "ターン終了";
                };
            }

            // 遭遇 (encounter/contact): MovementSystem.ResolveEncounter → PlayerSelectionController's
            // ShowContact → PlayContact. aiFire() from a standing position shows no banner (as in the source).

            // 勝利 (victory): TurnManager.OnFinish -> result banner + END TURN pulse stop.
            turns.OnFinish += (result, reason) =>
            {
                if (endTurnPulse != null) endTurnPulse.SetOn(false);
                if (endTurnButton != null) endTurnButton.gameObject.SetActive(false);
                if (phaseBanner == null) return;
                // the player's own win: the radio and 作戦成功 (OperationResultScreen) take its place
                var mb = FindAnyObjectByType<MatchBootstrap>();
                if (mb != null && mb.PlayerWonRealMatch(result)) return;
                string main = result == MatchResult.AttackWins ? "ATTACKER VICTORY" : "DEFENDER VICTORY";
                phaseBanner.Play(main, result == MatchResult.AttackWins ? "攻撃チーム勝利" : "防衛チーム勝利", reason, UIPalette.Accent);
            };
        }
    }
}
