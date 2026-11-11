// Player-side unit selection + command menu. Ports the SELECTION / COMMANDS and INPUT blocks of
// blindside_source/05_ui_input_intro.js: selectUnit / deselect / clearUI (~156-161),
// showUnitPanel (~62-75), showOpArt / hideOpArt (~147-154), commands (~76-88),
// renderMenu / positionMenu / hideMenu (~89-97), doCommand (~166-186), cancelMode (~204-210),
// pickUnit (~285), onClick (~310-340) and the pointer/keyboard bindings in bindInput (~356-395).
//
// Scope: selection, the yellow selection ring, the unit detail panel, the operator illustration
// card, the command menu, and (PlayerSelectionController.Move.cs) the MOVE command's free-path
// drawing/confirm/execute flow plus END TURN.
//   - pickUnit skips enemies that aren't shownToPlayer() (VisibilityController, bound by MatchBootstrap).
//   - doCommand validates the entry exactly like the source (playerCanAct + disabled check); MOVE,
//     VIEW, FIRE and WAIT are connected (.Move.cs / .Fire.cs), and ABILITY for the three breach abilities
//     (.Ability.cs) — the other abilities and gadget/escort still just log.
//   - The deploy/prep/gadget branches of onClick and the camera panTo() are left out: this harness
//     never pauses Deploy/Prep for the player and has no enemy-gadget objects.
//   - Screen -> grid always goes through BattlefieldCamera.GroundAt (the source's groundAt); the
//     pointermove drag pan is ported here, while WASD/wheel live in BattlefieldCamera itself.
//
// The three widgets come from the existing prefabs (HUD_UnitPanel, HUD_CmdMenu,
// HUD_OperatorArtCard) instantiated into this component's own overlay canvas; their placeholder
// contents are filled in / replaced at runtime from the source's markup and CSS.
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using TMPro;
using Blindside.Abilities;
using Blindside.Data;
using Blindside.Map;
using Blindside.Movement;
using Blindside.Rendering;
using Blindside.Turns;
using Blindside.UI;

namespace Blindside.Integration
{
    public partial class PlayerSelectionController : MonoBehaviour
    {
        [Header("Existing HUD prefabs (loaded from Assets/Blindside/Prefabs/UI if left empty)")]
        public GameObject unitPanelPrefab;
        public GameObject cmdMenuPrefab;
        public GameObject artCardPrefab;

        [Header("Camera used for pickUnit / positionMenu projections")]
        public Camera worldCamera;

        /// <summary>pickUnit's `d<30` screen radius, in CSS px (= reference-canvas units here).</summary>
        public float pickRadius = 30f;

        /// <summary>G.reinforceLeft — starts at 10 (startGame). Only read by the prep-only REINFORCE
        /// entry, which this harness never reaches (Prep isn't player-interactive yet).</summary>
        public int reinforceLeft = 10;

        // UR in 01_core_data_map_vision.js — unit body radius, segFree's default freeAt radius.
        const float UnitRadius = 0.28f;

        public struct Command
        {
            public string id, kr, en, ic, sub;
            public bool ok;
        }

        enum Mode { Idle, Menu, Move, Facing, Fire, Ability, Busy, Cams, Deploy, Reinforce, Throw }

        // ---- bound match state ----
        TurnManager turns;
        IReadOnlyList<SimUnit> units;
        SimHostage hostage;
        /// <summary>Every civilian hostage (set by MatchBootstrap before Bind; null — just `hostage`).</summary>
        public IReadOnlyList<SimHostage> HostageList;
        readonly List<SimHostage> hostages = new List<SimHostage>();

        /// <summary>The hostage `u` escorts, or null.</summary>
        SimHostage EscortedBy(SimUnit u)
        {
            for (int i = 0; i < hostages.Count; i++) if (hostages[i].Alive && !hostages[i].Extracted && ReferenceEquals(hostages[i].Escort, u)) return hostages[i];
            return null;
        }

        /// <summary>The nearest free hostage within SECURE reach of `u` (none while `u` already escorts one).</summary>
        SimHostage FreeHostageNear(SimUnit u)
        {
            if (u == null || EscortedBy(u) != null) return null;
            SimHostage best = null; float bd = 1.6f;
            foreach (var h in hostages)
            {
                if (!h.Alive || h.Extracted || h.Escort != null) continue;
                float d = Vector2.Distance(new Vector2(u.X, u.Y), new Vector2(h.X, h.Y));
                if (d <= bd) { bd = d; best = h; }
            }
            return best;
        }
        MapGrid map;
        OperatorTeam side;
        bool hasSide;
        AbilityContext mapCtx;

        // ---- G.ui ----
        Mode mode = Mode.Idle;
        SimUnit uiUnit;       // G.ui.unit
        SimUnit panelUnit;    // G.ui.panelUnit

        public SimUnit SelectedUnit => uiUnit;
        /// <summary>The bound match's turn manager (null between matches) — read by tools such as the debug quick start.</summary>
        public TurnManager Turns => turns;
        public IReadOnlyList<SimUnit> AllUnits => units;

        /// <summary>G.ui.unit changed (select / deselect / clearUI) — HUD refresh cue.</summary>
        public event System.Action SelectionChanged;
        /// <summary>A player unit's HUD-visible state changed (a move step's moveLeft, END TURN's ended flags).</summary>
        public event System.Action UnitsChanged;
        SimUnit notifiedUnit;

        void NotifySelection()
        {
            if (notifiedUnit == uiUnit) return;
            notifiedUnit = uiUnit;
            SelectionChanged?.Invoke();
        }
        public bool MenuVisible => menuRoot != null && menuRoot.gameObject.activeSelf;

        // ---- pointer state (bindInput's `down`) ----
        bool downActive, downMoved, downOnWorld, downHasG0, downPath;
        int downButton;
        Vector2 downPos;
        GroundHit downG0;     // down.g0 = groundAt at press

        // ---- widgets ----
        Canvas canvas;
        bool built;

        // palette (00_markup_and_styles.html :root)
        static readonly Color Paper = UIPalette.Paper;
        static readonly Color Ink = UIPalette.Ink;
        static readonly Color Ink2 = UIPalette.Ink2;
        static readonly Color Blu = UIPalette.Blue;
        static readonly Color Foe = UIPalette.Foe;
        static readonly Color Acc = UIPalette.Accent;
        static readonly Color Dimp = UIPalette.Dim;

        // ================================================================
        //  Binding
        // ================================================================
        /// <summary>shownToPlayer(u) (null: every unit counts as shown).</summary>
        System.Func<Blindside.Combat.ICombatUnit, bool> shownToPlayer;
        bool Shown(Blindside.Combat.ICombatUnit u) => shownToPlayer == null || shownToPlayer(u);
        Blindside.Vision.VisionSystem vision;
        Blindside.Vision.MarkBoard marks;

        public void Bind(TurnManager turns, IReadOnlyList<SimUnit> units, SimHostage hostage, MapGrid map, OperatorTeam? playerTeam, GameplayUIBridge bridge = null, Blindside.Combat.CombatSystem combat = null,
                         Blindside.Vision.VisionSystem vision = null, Blindside.Vision.MarkBoard marks = null, System.Func<Blindside.Combat.ICombatUnit, bool> shownToPlayer = null)
        {
            this.turns = turns; this.units = units; this.hostage = hostage; this.map = map; this.combat = combat;
            hostages.Clear();
            if (HostageList != null) hostages.AddRange(HostageList); else if (hostage != null) hostages.Add(hostage);
            this.vision = vision; this.marks = marks; this.shownToPlayer = shownToPlayer;
            hasSide = playerTeam.HasValue;
            side = playerTeam ?? OperatorTeam.Attack;
            mapCtx = new AbilityContext { Map = map };
            reinforceLeft = 10;
            busy = false; modal = false;
            wallBounds.Clear();   // the blocks' size can differ from match to match (the tutorial's training hall raises them)
            ResetCallouts();
            intelOn = false;
            if (combat != null) { combat.OnKill -= OnKillCallout; combat.OnKill += OnKillCallout; combat.OnSupportFire -= OnSupportCallout; combat.OnSupportFire += OnSupportCallout; }
            GameplayUIBridge.VolleyShown -= OnVolleyCallout;
            GameplayUIBridge.VolleyShown += OnVolleyCallout;
            BindMovement(bridge);
            EnsureBuilt();
            EnsureDebugButton();   // 自動クリア (editor / development builds) — .DebugClear.cs
            Deselect();
        }

        /// <summary>This component's overlay canvas (unit panel, menu, hint, overhead tags) — dimmed as HUD during a battle cinematic.</summary>
        public Canvas OverlayCanvas { get { EnsureCanvas(); return canvas; } }

        /// <summary>
        /// Battle cinematic on/off (BattleDirector): hides the overhead tags (LOCK / 行動終了 / "!") and drops
        /// any half-finished press so the release after the cinematic isn't read as a click or drag.
        /// </summary>
        public void SetCinematic(bool on)
        {
            cinematicOn = on;
            if (on) { downActive = false; downPath = false; downMoved = false; HideTip(); }
            if (endedTagRoot != null) endedTagRoot.gameObject.SetActive(!on);
        }

        public void Unbind()
        {
            if (built) Deselect();
            ResetCallouts();
            if (combat != null) { combat.OnKill -= OnKillCallout; combat.OnSupportFire -= OnSupportCallout; }
            GameplayUIBridge.VolleyShown -= OnVolleyCallout;
            turns = null; units = null; hostage = null; map = null; hasSide = false; hostages.Clear(); HostageList = null;
            vision = null; marks = null; shownToPlayer = null;
            movement = null; combat = null; busy = false; modal = false;
            abilities = null; abilityCtx = null; droneOps = null;
        }

        // (a scene left mid-match never reaches Unbind: a destroyed controller left on this static hook would throw on the next shot and stop the match)
        void OnDestroy() => GameplayUIBridge.VolleyShown -= OnVolleyCallout;

        // Port of playerCanAct(): G.state==='game' && !G.busy && !G.over && !G.modal && phase is the
        // player's. TurnManager only parks for the player during their own Attack/Defense phase.
        // (`||(G.phase==='deploy'&&G.side==='atk')` — the deployment is the attack player's turn too, see .Deploy.cs.)
        bool PhaseIsPlayers() => hasSide && turns != null && !turns.Over && (turns.AwaitingPlayer || turns.AwaitingDeploy);
        bool PlayerCanAct() => PhaseIsPlayers() && !busy && !modal;

        // ================================================================
        //  Frame loop — bindInput()
        // ================================================================
        void Update()
        {
            if (turns == null) return;

            // Source clears the selection UI at every phase change (phase()/endPhase() → clearUI()).
            if (!PhaseIsPlayers() && (mode != Mode.Idle || uiUnit != null)) ClearUI();

            // Battle cinematic: every pointer/keyboard input waits until the camera is back (BattleDirector).
            if (BattlefieldCamera.InputLocked) return;
            // the tutorial's radio is talking / showing something: the battlefield waits
            if (TutorialGate.Holding) { downActive = false; downPath = false; return; }

            // G.modal: the dialog's own capture-phase keydown is the only keyboard input; the
            // full-screen .modal backdrop swallows every pointer press (PointerOverVisibleUI).
            if (modal) { DialogKeys(); return; }
            // the full battle log (Tab) is open over the battlefield: it takes the keys and its backdrop the clicks
            if (KitLogHistory.IsOpen) { downActive = false; downPath = false; return; }

            var mouse = Mouse.current;
            if (mouse != null)
            {
                Vector2 pos = mouse.position.ReadValue();
                if (mouse.leftButton.wasPressedThisFrame) PointerDown(0, pos);
                else if (mouse.rightButton.wasPressedThisFrame) PointerDown(2, pos);
                if (downActive && (pos - downPos).magnitude > 6f) downMoved = true;
                // pointermove: if(down.path){ if(down.moved) extendPath(G.hover); }
                if (downActive && downPath)
                {
                    if (downMoved && BattlefieldCamera.GroundAt(pos, out var hv)) ExtendPath(hv);
                }
                // else if(down.moved&&(down.b!==0||['idle','menu','cams'].includes(G.ui.mode))&&down.g0&&G.hover)
                //   { G.cam.x+=down.g0.fx-G.hover.fx; G.cam.y+=down.g0.fy-G.hover.fy; }
                // — keeps the grabbed ground point pinned under the cursor.
                else if (downActive && downOnWorld && downMoved && downHasG0 && (downButton != 0 || mode == Mode.Idle || mode == Mode.Menu)
                    && BattlefieldCamera.Instance != null && BattlefieldCamera.GroundAt(pos, out var hover))
                    BattlefieldCamera.Instance.Pan(downG0.fx - hover.fx, downG0.fy - hover.fy);
                // pointermove's trailing updateAim() (facing / fire modes follow the cursor).
                PointerMoveAim(pos);
                if (downActive && downButton == 0 && mouse.leftButton.wasReleasedThisFrame) PointerUp(pos);
                else if (downActive && downButton == 2 && mouse.rightButton.wasReleasedThisFrame) PointerUp(pos);
            }

            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.escapeKey.wasPressedThisFrame)
                {
                    // something to cancel: ESC is the cancel's; nothing (or not the player's turn): it is left to the game menu (PauseMenu)
                    if (PlayerCanAct() && HasSomethingToCancel) { CancelMode(); Blindside.UI.EscapeGate.Consume(); }
                }
                else if (kb.qKey.wasPressedThisFrame) CycleUnit();          // (Tab is the full battle log, KitLogHistory)
                else if (mode == Mode.Menu && uiUnit != null)
                {
                    for (int k = 1; k <= 9; k++)
                    {
                        if (!kb[Key.Digit1 + (k - 1)].wasPressedThisFrame && !kb[Key.Numpad1 + (k - 1)].wasPressedThisFrame) continue;
                        var L = Commands(uiUnit);
                        if (k - 1 < L.Count) DoCommand(uiUnit, L[k - 1].id);
                        break;
                    }
                }
            }
        }

        void LateUpdate()
        {
            SyncRings();
            SyncFigureOpacity();
            PositionMenu();
            UpdateMenuHover();
            SyncMoveHelpers();
            SyncAimHelpers();
            SyncAbilityHelpers();
            SyncThrowHelpers();
            SyncLockLabels();
            SyncEndedLabels();
            SyncAlertLabels();
            SyncEscortViews();
            SyncEndButton();
            SyncCallouts();
            SyncWallIntel();
            SyncAimForecast();
        }

        // cv.addEventListener('pointerdown', ...) — only presses that land on the battlefield count.
        void PointerDown(int button, Vector2 pos)
        {
            downActive = true; downButton = button; downPos = pos; downMoved = false; downPath = false;
            downOnWorld = !PointerOverVisibleUI(pos);
            downHasG0 = downOnWorld && BattlefieldCamera.GroundAt(pos, out downG0);
            // if(e.button===0&&G.ui.mode==='move'&&playerCanAct()){ G.ui.path=[{x:m.x,y:m.y}]; G.ui.dragging=true; down.path=true; }
            if (button == 0 && downOnWorld && mode == Mode.Move && PlayerCanAct() && MoverPos().HasValue)
            {
                movePath = new List<Vector2> { MoverPos().Value };   // G.ui.path=[{x:m.x,y:m.y}] — the unit, or the drone
                downPath = true;
            }
        }

        // window.addEventListener('pointerup', ...)
        void PointerUp(Vector2 pos)
        {
            downActive = false;
            if (downButton == 2) { if (!downMoved) CancelMode(); return; }
            if (downPath) { downPath = false; StartCoroutine(OnMoveRelease(pos)); return; }
            if (!downMoved && downOnWorld && !PointerOverVisibleUI(pos)) OnClick(pos);
        }

        // Q: cycle through this side's living, not-yet-ended units.
        void CycleUnit()
        {
            if (!PlayerCanAct()) return;
            var my = new List<SimUnit>();
            foreach (var u in units) if (u.Alive && u.Team == side && !u.Ended && !u.Captive) my.Add(u);
            if (my.Count == 0) return;
            int i = my.IndexOf(uiUnit);
            SelectUnit(my[(i + 1) % my.Count]);
        }

        /// <summary>
        /// Web equivalent of `e.target===cv`: the pointer is over the battlefield unless it's over a
        /// UI graphic that is actually visible. Hidden-by-alpha widgets (phase banner, headshot badge
        /// between plays, …) don't swallow clicks — they're pointer-events:none in the source.
        /// </summary>
        static bool PointerOverVisibleUI(Vector2 screenPos)
        {
            var es = EventSystem.current;
            if (es == null) return false;
            var ped = new PointerEventData(es) { position = screenPos };
            var hits = new List<RaycastResult>();
            es.RaycastAll(ped, hits);
            foreach (var h in hits)
            {
                if (!(h.module is GraphicRaycaster)) continue;   // only UI counts, never 3D colliders
                var g = h.gameObject.GetComponent<Graphic>();
                if (g == null) return true;
                if (g.color.a * g.canvasRenderer.GetInheritedAlpha() > 0.01f) return true;
            }
            return false;
        }

        // ================================================================
        //  pickUnit / onClick / cancelMode
        // ================================================================
        Camera Cam => worldCamera != null ? worldCamera : Camera.main;

        /// <summary>
        /// Port of pickUnit(sx,sy): nearest living, shownToPlayer() unit whose projected (rx,.5,ry) is
        /// within 30 CSS px.
        /// </summary>
        SimUnit PickUnit(Vector2 screenPos)
        {
            var cam = Cam;
            if (cam == null || units == null) return null;
            float r = pickRadius * (canvas != null ? canvas.scaleFactor : 1f);
            SimUnit best = null; float bd = float.MaxValue;
            foreach (var u in units)
            {
                if (!u.Alive || !u.Placed || !Shown(u)) continue;   // a unit still aboard the helicopter isn't on the map to click
                var s = cam.WorldToScreenPoint(GridSpace.GridToWorld(u.X, u.Y, 0.5f));
                if (s.z < 0f) continue;
                float d = Vector2.Distance(new Vector2(s.x, s.y), screenPos);
                if (d < r && d < bd) { bd = d; best = u; }
            }
            return best;
        }

        /// <summary>groundAt(sx,sy): the click's point on the ground plane (null if it misses).</summary>
        static bool GroundAt(Vector2 screenPos) => BattlefieldCamera.GroundAt(screenPos, out _);

        /// <summary>Public for tests/tools: same path a real left click on the battlefield takes.</summary>
        public void ClickScreen(Vector2 screenPos) => OnClick(screenPos);

        // Port of onClick(g,sx,sy) — facing/fire/idle/menu branches (see file header for what's skipped).
        void OnClick(Vector2 screenPos)
        {
            if (!PlayerCanAct() || !GroundAt(screenPos)) return;
            if (turns.Phase == MatchPhase.Deploy) { OnClickDeploy(screenPos); return; }   // G.phase==='deploy' (.Deploy.cs)
            if (OnClickThrowMode()) return;   // a grenade being aimed (.Grenade.cs)
            if (OnClickAimModes()) return;   // ui.mode==='facing' | 'fire'
            if (OnClickAbilityMode(screenPos)) return;   // ui.mode==='ability' (.Ability.cs)
            if (OnClickReinforceMode(screenPos)) return;   // ui.mode==='reinforce' (.Reinforce.cs)
            if (mode == Mode.Move || mode == Mode.Cams) return;   // if(ui.mode==='move'||ui.mode==='cams') return;
            var u = PickUnit(screenPos);
            if (u != null && u.Team == side)
            {
                if (!u.Ended) SelectUnit(u);
                else { ShowUnitPanel(u, false); mode = Mode.Idle; uiUnit = null; panelUnit = u; HideMenu(); NotifySelection(); }
                return;
            }
            // enemyGadgets branch: a click on a visible enemy drone / camera shoots it (no action spent) — .Drone.cs
            if (OnClickGadget(screenPos)) return;
            if (u != null && u.Team != side)
            {
                ShowUnitPanel(u, false); HideMenu(); mode = Mode.Idle; uiUnit = null; panelUnit = u;
                NotifySelection();
                return;
            }
            Deselect();
        }

        /// <summary>Port of cancelMode(): facing → restore the facing (and end the unit if it was the
        /// end-of-action prompt); menu/idle → deselect; fire → restore the facing; move/fire → back to
        /// the unit's command menu (deselect if it can no longer act).</summary>
        /// <summary>ESC would cancel something now: a selected unit, a command mode, a press in progress.</summary>
        public bool HasSomethingToCancel => mode != Mode.Idle || uiUnit != null || downActive;

        /// <summary>The game waits for the player's input (their turn or their deployment), nothing busy, no dialog: the game menu may open.</summary>
        public bool WaitingForPlayer => PlayerCanAct();

        public void CancelMode()
        {
            if (!PlayerCanAct()) return;
            if (mode == Mode.Facing && uiUnit != null)
            {
                uiUnit.Facing = facingBackup;
                if (facingEndAfter) { EndUnit(uiUnit); return; }
                SelectUnit(uiUnit);
                return;
            }
            if (mode == Mode.Menu || mode == Mode.Idle || mode == Mode.Deploy) { Deselect(); return; }   // ui.mode==='menu'||'deploy'
            if (mode == Mode.Fire && uiUnit != null) { uiUnit.Facing = fireBackup; HideTip(); }
            if (uiUnit != null && uiUnit.Alive && !uiUnit.Ended) SelectUnit(uiUnit); else Deselect();
        }

        // ================================================================
        //  selectUnit / deselect / clearUI
        // ================================================================
        public void SelectUnit(SimUnit u)
        {
            if (u == null || !u.Alive) return;
            if (turns != null && turns.Phase == MatchPhase.Deploy) { EnterDeploy(u); return; }   // selectUnit → enterDeploy
            bool changed = uiUnit != u;
            mode = Mode.Menu; uiUnit = u; panelUnit = u;
            ResetMoveState(); HideHint(); HideTip(); hasFireCast = false;
            ShowUnitPanel(u, !changed);
            ShowOpArt(u);
            RenderMenu(u);
            SyncRings();
            NotifySelection();
            // the camera goes to the unit picked (the tutorial's scripted cameras stay as they are)
            if (changed && u.Placed && !TutorialGate.Active && BattlefieldCamera.Instance != null) BattlefieldCamera.Instance.PanTo(u.X, u.Y, 350f);
        }

        /// <summary>
        /// A #roster card click — buildRoster()'s d.onclick:
        /// `if(!u.alive) return; if(playerCanAct()&&!u.ended) selectUnit(u); else { panTo(u.x,u.y,400); showUnitPanel(u); }`
        /// with selectUnit's `if(changed) panTo(u.x,u.y,350)`.
        /// </summary>
        public void RosterClick(SimUnit u)
        {
            if (u == null || !u.Alive || TutorialGate.Holding) return;
            if (u.Captive) { var cc = BattlefieldCamera.Instance; if (cc != null) cc.PanTo(u.X, u.Y, 400f); ShowUnitPanel(u, false); return; }   // (held: where)
            if (turns != null && turns.Phase == MatchPhase.Deploy && !PlayerCanAct()) return;   // rope drop in progress: input is locked
            var cam = BattlefieldCamera.Instance;
            if (PlayerCanAct() && !u.Ended)
            {
                bool changed = uiUnit != u;
                SelectUnit(u);
                if (changed && cam != null && u.Placed && TutorialGate.Active) cam.PanTo(u.X, u.Y, 350f);   // (SelectUnit pans outside the tutorial; enterDeploy pans only for a dropped unit)
            }
            else
            {
                if (cam != null) cam.PanTo(u.X, u.Y, 400f);
                ShowUnitPanel(u, false);
            }
        }

        void ClearUI()
        {
            mode = Mode.Idle; uiUnit = null; panelUnit = null;
            ResetMoveState(); hasFireCast = false;
            HideMenu(); HideOpArt(); HideHint(); HideTip();
            SyncRings();
            NotifySelection();
        }

        public void Deselect()
        {
            ClearUI();
            if (kitPanel != null) kitPanel.Hide();
            else if (panelRoot != null) panelRoot.gameObject.SetActive(false);
            if (panelOver != null) panelOver.gameObject.SetActive(false);
        }

        // m.sel.visible = G.ui.unit === u
        void SyncRings()
        {
            if (units == null) return;
            foreach (var u in units)
                if (u.View != null) u.View.SetSelected(u == uiUnit && u.Alive);
        }

        // ================================================================
        //  commands(u)
        // ================================================================
        static string M1(float v) => (Mathf.Floor(v * 10f + 0.5f) / 10f).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture);
        static string MoveRange(SimUnit u) => M1(Blindside.Combat.CombatModifiers.MoveRange(u.Mv, u.Weapon, u)).Replace(".0", "");
        static string SpeedOf(SimUnit u) => M1(u.Speed).Replace(".0", "");

        public List<Command> Commands(SimUnit u)
        {
            bool prep = turns != null && turns.Phase == MatchPhase.Prep;
            var L = new List<Command>();
            L.Add(new Command { id = "move", kr = "移動", en = "MOVE", ic = "➜", ok = u.MoveLeft > .3f, sub = M1(u.MoveLeft) + "m" });
            L.Add(new Command { id = "face", kr = "視界変更", en = "VIEW", ic = "◐", ok = true, sub = u.LockFacing ? "固定" : "" });
            // a raised shield (BASTION) leaves no hand for the gun
            bool shieldStops = u.Shield && !Blindside.Combat.CoopRules.CanFireShielded(u);
            // 二丁持ち (co-op rank 6): swap to the other gun — free, any time before acting
            if (!prep && u.Weapon2 != null)
                L.Add(new Command { id = "swap", kr = "持ち替え", en = "SWAP", ic = "⌖", ok = !u.Acted, sub = u.Weapon2.weaponName });
            if (!prep) L.Add(new Command { id = "fire", kr = "攻撃", en = "FIRE", ic = "✛", ok = !u.Acted && !shieldStops, sub = shieldStops ? "シールド中" : "" });
            string ab = u.AbilityKey, abName = u.Op != null ? u.Op.abilityName : ab;
            if (!MatchBootstrap.NoAbilities)
            L.Add(new Command
            {
                id = "skill", kr = abName, en = "ABILITY", ic = "✦",
                ok = u.Charges > 0 && (prep || !u.AbilityUsed) && !(ab == "shield" && u.Shield) && !(ab == "ghost" && u.Ghost),
                sub = u.Charges >= 99 ? "∞" : u.Charges + "回",
            });
            if (u.Team == OperatorTeam.Attack && !HasDrone(u)) { }   // (past the first main quest a drone is equipment from the shop)
            else if (u.Team == OperatorTeam.Attack)
            {
                bool droneAlive = u.Drone != null && u.Drone.alive;
                L.Add(new Command
                {
                    id = "gadget", kr = u.ShopDrone ? "偵察ドローン" : "ドローン", en = "DRONE", ic = "⌖",
                    ok = !u.GadgetUsed && (!u.DroneSpent || (droneAlive && u.Drone.moveLeft > .3f)),
                    sub = droneAlive ? "操縦" : u.DroneSpent ? "喪失" : "展開",
                });
            }
            else
            {
                // G.cams.filter(c=>c.alive): the map's fixed cameras + ARGUS placements (AbilityContext.Cameras).
                int cams = AliveCameraCount();
                L.Add(new Command { id = "gadget", kr = "監視カメラ", en = "CAMERA", ic = "◉", ok = cams > 0, sub = cams + "台" });
            }
            if (u.Team == OperatorTeam.Defense && prep)
                L.Add(new Command { id = "reinforce", kr = "壁強化", en = "REINFORCE", ic = "▦", ok = reinforceLeft > 0 && AnyReinforceablePanelNear(u), sub = reinforceLeft + "/10" });
            var fh = u.Team == OperatorTeam.Attack ? FreeHostageNear(u) : null;
            if (fh != null)
                L.Add(new Command { id = "escort", kr = "人質確保", en = "SECURE", ic = "♥", ok = SegFree(u.X, u.Y, fh.X, fh.Y) });
            AddItemCommand(u, L, prep);   // the HQ item (破片手榴弾) — .Grenade.cs
            AddRescueCommand(u, L);       // the rescue operation: 救出 next to a captive — .Rescue.cs
            L.Add(new Command { id = "wait", kr = "待機", en = "END", ic = "■", ok = true });
            // the tutorial offers only the command it is teaching
            if (TutorialGate.Active)
                for (int i = 0; i < L.Count; i++)
                    if (!TutorialGate.Allows(L[i].id)) { var c = L[i]; c.ok = false; L[i] = c; }
            return L;
        }

        // panels.some(p => canReinforce(p) && panelNear(p,u)); canReinforce: intact && !reinforced && !mirror,
        // panelNear(p,u,r=.85): some cell within .85 of the unit (distToCell).
        bool AnyReinforceablePanelNear(SimUnit u) => ReinforceablePanelsNear(u).Count > 0;   // (.Reinforce.cs)

        // segFree(a,b,false): every .08 step along a→b is freeAt(…, UR).
        bool SegFree(float ax, float ay, float bx, float by)
        {
            if (mapCtx == null) return false;
            float d = Mathf.Sqrt((bx - ax) * (bx - ax) + (by - ay) * (by - ay));
            int n = Mathf.Max(1, Mathf.CeilToInt(d / .08f));
            for (int i = 1; i <= n; i++)
            {
                float t = (float)i / n;
                if (!AbilitySystem.FreeAt(mapCtx, ax + (bx - ax) * t, ay + (by - ay) * t, false, UnitRadius)) return false;
            }
            return true;
        }

        // ================================================================
        //  doCommand
        // ================================================================
        /// <summary>
        /// Port of doCommand(u,c): playerCanAct + the entry must exist and be enabled, then move / face /
        /// fire / wait, escort (SECURE), and the breach abilities (hammer/launcher/thermite).
        /// </summary>
        public void DoCommand(SimUnit u, string c)
        {
            if (!PlayerCanAct()) return;
            var list = Commands(u);
            int i = list.FindIndex(x => x.id == c);
            if (i < 0 || !list[i].ok) return;
            if (c == "move")
            {
                HideMenu();
                StartMoveMode(u, u);
                SetHint("移動経路を自由に描いてください", TutorialGate.Active && TutorialGate.NoAutoPath
                    ? $"左クリックを押したままドラッグして経路を描く · 移動力 {M1(u.MoveLeft)}m"
                    : $"左クリックを押したままドラッグして経路を描く · 1回クリックで自動経路 · 移動力 {M1(u.MoveLeft)}m", ("cancel", "ESC キャンセル", false));
                return;
            }
            if (c == "face") { EnterFacing(u, false); return; }                      // enterFacing hides the menu
            if (c == "fire" && combat != null) { HideMenu(); StartFire(u); return; }
            if (c == "wait") { EnterFacing(u, true); return; }
            if (c == "escort") { DoEscort(u); return; }                          // 人質確保 (.Escort.cs)
            if (c == "reinforce") { StartReinforce(u); return; }
            if (DoItemCommand(u, c)) return;                                      // the HQ's items (.Items.cs)
            if (c == "swap") { SwapGun(u); return; }                             // 持ち替え (co-op rank 6)
            if (c == "rescue") { DoRescue(u); return; }                          // 救出 (.Rescue.cs)                  // 壁強化 (.Reinforce.cs)
            if (c == "skill" && StartAbility(u)) return;                         // all ten abilities (.Ability.cs / .SupportAbility.cs)
            if (c == "gadget" && u.Team == OperatorTeam.Defense) { HideMenu(); OpenCamPanel(u); return; }   // openCamPanel(u) (.Cameras.cs)
            if (c == "gadget" && droneOps != null) { StartDrone(u); return; }                                // deploy / steer the drone (.Drone.cs)
            Debug.Log($"[PlayerSelection] {u.Op?.key ?? u.Id}: '{list[i].kr}' ({c}) コマンド — 実行ロジックはまだ接続されていません。");
        }

        // ================================================================
        //  Widget construction
        // ================================================================
        // The canvas is created on Awake and the widgets only at Bind (≥1 frame later, after
        // MatchBootstrap's first `yield return null`), so the overlay canvas already has its real
        // screen size when UIOperatorArtCard.Awake caches its basePos.
        void Awake() => EnsureCanvas();

        void EnsureBuilt()
        {
            if (built) return;
            built = true;
            if (worldCamera == null) worldCamera = Camera.main;
            EnsureCanvas();
            Canvas.ForceUpdateCanvases();

            // Sibling order = paint order: opArt (z 12) < unitPanel (z 16) < cmdMenu (z 30).
            BuildArtCard(canvas.transform);
            BuildUnitPanel(canvas.transform);
            BuildMoveHud(canvas.transform);   // log, ended tags, path label, hint (below the menu)
            BuildMenu(canvas.transform);
            BuildTip(canvas.transform);       // #tip z-index 40: above the menu (30), below the dialog (60)
            BuildDialog(canvas.transform);    // .modal z-index 60: above everything
        }

        void EnsureCanvas()
        {
            if (canvas != null) return;
            var canvasGo = new GameObject("PlayerSelection_Canvas", typeof(RectTransform));
            canvasGo.transform.SetParent(transform, false);
            canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 5; // above GameplayUIBridge's banners (cmdMenu z-index 30 in the source)
            UITheme.Current.ApplyTo(canvasGo.AddComponent<CanvasScaler>(), UITheme.CanvasKind.Hud);
            canvasGo.AddComponent<GraphicRaycaster>();
        }

        static GameObject LoadPrefab(GameObject assigned, string name)
        {
            if (assigned != null) return assigned;
            var r = Blindside.Data.BuildAssetRegistry.UiPrefab(name);
            if (r != null) return r;
#if UNITY_EDITOR
            return UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Blindside/Prefabs/UI/{name}.prefab");
#else
            Debug.LogError($"[Blindside] UI prefab {name} missing from BlindsideAssetRegistry");
            return null;
#endif
        }

        // ---- small uGUI helpers (shared with the Blindside.UI views: HudKit) ----
        static RectTransform NewRect(string name, Transform parent) => HudKit.NewRect(name, parent);
        static void Stretch(RectTransform rt) => HudKit.Stretch(rt);
        static Image AddImage(RectTransform rt, Color c, bool raycast = false) => HudKit.AddImage(rt, c, raycast);

        /// <param name="role">the font role; null = picked from the text (UIFonts.Auto)</param>
        static TextMeshProUGUI AddText(Transform parent, string text, float size, Color color, bool bold, TextAlignmentOptions align, string name = "Text", UITheme.TextRole? role = null)
            => HudKit.AddText(parent, text, size, color, bold, align, name, role);

        static LayoutElement LE(Component c, float prefW = -1, float prefH = -1, float flexW = -1, float flexH = -1, float minW = -1, float minH = -1)
            => HudKit.LE(c, prefW, prefH, flexW, flexH, minW, minH);

        static HorizontalLayoutGroup HLG(Component c, float spacing, RectOffset pad, TextAnchor align = TextAnchor.MiddleLeft, bool expandH = false)
            => HudKit.HLG(c, spacing, pad, align, expandH);

        static VerticalLayoutGroup VLG(Component c, float spacing, RectOffset pad) => HudKit.VLG(c, spacing, pad);

        // ---- icon glyph font (HudKit.SymbolFont: the OS "Segoe UI Symbol" for ✦ ➜ ✛ ⌖ ◉) ----
        internal static TMP_FontAsset SymbolFont => HudKit.SymbolFont;
        static void UseSymbolFont(TMP_Text t) => HudKit.UseSymbolFont(t);

        // ================================================================
        //  showOpArt / hideOpArt — HUD_OperatorArtCard + UIOperatorArtCard
        // ================================================================
        // Prefabs/UI/HUD_OperatorArtCard.prefab is the kit card (KitArtCard). The old card is UI/Legacy/HUD_OperatorArtCard.prefab: assigned
        // to artCardPrefab (no KitArtCard on it) it is used as before.
        KitArtCard kitArt;
        UIOperatorArtCard artCard;

        void BuildArtCard(Transform parent)
        {
            var prefab = LoadPrefab(artCardPrefab, "HUD_OperatorArtCard");
            if (prefab == null) { Debug.LogWarning("[PlayerSelection] HUD_OperatorArtCard prefab not found."); return; }
            var go = Instantiate(prefab, parent, false);
            go.name = "OpArt";
            kitArt = go.GetComponent<KitArtCard>();
            if (kitArt != null) { kitArt.Build(); return; }
            artCard = go.GetComponent<UIOperatorArtCard>();
            if (artCard == null) { Debug.LogWarning("[PlayerSelection] HUD_OperatorArtCard has no UIOperatorArtCard."); return; }
            // #opArt{pointer-events:none}: never swallows battlefield clicks.
            if (artCard.group != null) { artCard.group.blocksRaycasts = false; artCard.group.interactable = false; }
            artCard.ApplyLayout();
            UseSymbolFont(artCard.abilityText);   // "✦ " + abName
        }

        void ShowOpArt(SimUnit u)
        {
            if (u == null || u.Team != side || u.Op == null) { HideOpArt(); return; }
            if (artCard == null && kitArt == null) return;
            var op = u.Op;
            string tag = $"OPERATOR FILE // OP-{u.Uid:000} // {(u.Team == OperatorTeam.Attack ? "ASSAULT" : "DEFENSE")}";
            // The upper-body illustration (OperatorArt.Body); a key without art keeps the placeholder fill.
            // UIOperatorArtCard.Show / KitArtCard.Show skip the replay when the same key is visible.
            if (kitArt != null) kitArt.Show(OperatorArt.Body(op.key), op.DisplayName, $"{op.kr} · {op.role}", op.abilityName, tag, op.color);
            else artCard.Show(OperatorArt.Body(op.key), op.DisplayName, $"{op.kr} · {op.role}", op.abilityName, tag, op.color);
        }

        void HideOpArt()
        {
            if (kitArt != null) kitArt.Hide();
            if (artCard != null) artCard.Hide();
        }

        // ================================================================
        //  showUnitPanel — HUD_UnitPanel filled with the source's #unitPanel markup
        // ================================================================
        // Prefabs/UI/HUD_UnitPanel.prefab is the kit panel (KitUnitPanel). The old panel is UI/Legacy/HUD_UnitPanel.prefab: assigned to
        // unitPanelPrefab (no KitUnitPanel on it) it is built by UnitPanelView as before.
        KitUnitPanel kitPanel;                 // the kit panel (Blindside.UI) — this controller only fills its model
        UnitPanelView panelView;               // the legacy panel
        RectTransform panelRoot;
        RectTransform panelOver;              // legacy: the portrait + status tags above the panel (outside its reveal mask)

        void BuildUnitPanel(Transform parent)
        {
            var prefab = LoadPrefab(unitPanelPrefab, "HUD_UnitPanel");
            if (prefab == null) { Debug.LogWarning("[PlayerSelection] HUD_UnitPanel prefab not found."); return; }
            if (prefab.GetComponent<KitUnitPanel>() != null)
            {
                var go = Instantiate(prefab, parent, false);
                go.name = "UnitPanel";
                kitPanel = go.GetComponent<KitUnitPanel>();
                kitPanel.Build();
                panelRoot = kitPanel.Root;
                panelOver = null;
                return;
            }
            panelView = UnitPanelView.Build(parent, prefab);
            panelView.CoroutineHost = this;
            panelRoot = panelView.Root;
            panelOver = panelView.Over;
        }

        /// <summary>Port of showUnitPanel(u,noAnim).</summary>
        void ShowUnitPanel(SimUnit u, bool noAnim)
        {
            if ((kitPanel == null && panelView == null) || u == null) return;
            panelUnit = u;
            if (kitPanel != null) kitPanel.Show(PanelModel(u), noAnim);
            else panelView.Show(PanelModel(u), noAnim);
        }

        /// <summary>showUnitPanel's texts for `u`.</summary>
        UnitPanelModel PanelModel(SimUnit u)
        {
            bool foe = u.Team != side;
            var op = u.Op;
            var w = u.Weapon;
            var m = new UnitPanelModel { key = op != null ? op.key : u.Id, name = op != null ? op.DisplayName : null, color = op != null ? op.color : Blu, foe = foe };
            string wName = w != null ? w.weaponName : "-";
            m.code = $"OP-{u.Uid:000} // {(u.Team == OperatorTeam.Attack ? "ASSAULT" : "DEFENSE")} // {wName.Split(' ')[0]}";
            m.krRich = $"{(op != null ? op.kr : "")}<font-weight=500><color=#FFFFFFB3>  {(op != null ? op.role : "")}</color></font-weight>";
            m.hp = u.HP; m.maxHp = u.MaxHP;

            // the current move 16 + the maximum (MoveRange) 11, white at .55 over the ink2 cell as its opaque sRGB colour
            m.grid[0] = $"{(foe ? MoveRange(u) : M1(u.MoveLeft))}<size=11><color=#9A9C9E>/{MoveRange(u)}m</color></size>";
            m.grid[1] = SpeedOf(u);
            m.grid[2] = w != null ? $"{w.damage}×{Blindside.Combat.CombatModifiers.ShotsOf(u)}" : "-";
            m.grid[3] = w != null ? w.accuracy.ToString() : "-";
            m.grid[4] = w != null ? w.range.ToString() : "-";

            m.weapon = wName + (w != null && w.breach ? " · 近接射撃で通常の壁を破壊" : "");

            m.abilityName = op != null ? op.abilityName : u.AbilityKey;
            int max = op != null ? op.charges : u.Charges;
            m.charges = foe ? "" : u.Charges >= 99 ? "∞" : new string('●', Mathf.Max(0, u.Charges)) + new string('○', Mathf.Max(0, max - u.Charges));
            m.description = op != null ? op.description : "";

            if (u.Shield) m.AddTag("シールド展開", Blu, Color.white);
            if (u.Ghost) m.AddTag("電波妨害", Blu, Color.white);
            if (u.Stim) m.AddTag("スティム", Acc, Ink);
            if (u.LockFacing) m.AddTag("視界固定", Acc, Ink);
            if (EscortedBy(u) != null) m.AddTag("人質護送", Acc, Ink);
            if (u.Ended) m.AddTag("行動終了", Ink, Color.white);
            return m;
        }

        // ================================================================
        //  renderMenu / positionMenu / hideMenu — HUD_CmdMenu + UIStaggerIn
        // ================================================================
        KitCommandMenu kitMenu;                // the kit menu (Blindside.UI) — this controller fills it with commands(u)
        CommandMenuView menuView;              // the legacy menu
        RectTransform menuRoot;
        int menuUid = -1;

        void BuildMenu(Transform parent)
        {
            var prefab = LoadPrefab(cmdMenuPrefab, "HUD_CmdMenu");
            if (prefab == null) { Debug.LogWarning("[PlayerSelection] HUD_CmdMenu prefab not found."); return; }
            // Prefabs/UI/HUD_CmdMenu.prefab is the kit menu (KitCommandMenu). The old one is UI/Legacy/HUD_CmdMenu.prefab: assigned to
            // cmdMenuPrefab (no KitCommandMenu on it) it is built by CommandMenuView as before.
            if (prefab.GetComponent<KitCommandMenu>() != null)
            {
                kitMenu = KitCommandMenu.Build(parent, prefab);
                menuRoot = kitMenu.Root;
                return;
            }
            menuView = CommandMenuView.Build(parent, prefab);
            menuRoot = menuView.Root;
        }

        /// <summary>Port of renderMenu(u).</summary>
        void RenderMenu(SimUnit u)
        {
            if (menuView == null && kitMenu == null) return;
            var L = Commands(u);
            bool fresh = menuUid != u.Uid || !menuRoot.gameObject.activeSelf;
            menuUid = u.Uid;
            var models = new List<CommandModel>(L.Count);
            foreach (var c in L) models.Add(new CommandModel { id = c.id, kr = c.kr, en = c.en, ic = c.ic, sub = c.sub, ok = c.ok });
            string header = $"{(u.Op != null ? u.Op.DisplayName : u.Id)} // COMMAND";
            if (kitMenu != null) kitMenu.Render(header, u.Op != null ? u.Op.color : Blu, models, fresh, id => DoCommand(u, id));
            else menuView.Render(header, models, fresh, id => DoCommand(u, id));
            PositionMenu();
        }

        void UpdateMenuHover()
        {
            if (menuView == null || !MenuVisible) return;   // the kit menu's buttons take their own pointer events
            var mouse = Mouse.current;
            Vector2 pos = mouse != null ? mouse.position.ReadValue() : new Vector2(-1, -1);
            menuView.UpdateHover(pos, Time.unscaledDeltaTime);
        }

        /// <summary>
        /// Port of positionMenu(): s = proj(u.rx,.5,u.ry);
        /// left = clamp(s.x+60, 230, VW-w-12); top = clamp(s.y-h/2, 90, VH-h-90) — in canvas units.
        /// </summary>
        void PositionMenu()
        {
            if (!MenuVisible || uiUnit == null || canvas == null) return;
            var cam = Cam;
            if (cam == null) return;
            float sf = canvas.scaleFactor > 0 ? canvas.scaleFactor : 1f;
            var s = cam.WorldToScreenPoint(GridSpace.GridToWorld(uiUnit.X, uiUnit.Y, 0.5f));
            float sx = s.x / sf, sy = (Screen.height - s.y) / sf;
            float vw = Screen.width / sf, vh = Screen.height / sf;
            float w = menuRoot.rect.width, h = menuRoot.rect.height;
            float left = Clamp(sx + 60f, 230f, vw - w - 12f);
            float top = Clamp(sy - h / 2f, 90f, vh - h - 90f);
            menuRoot.anchoredPosition = new Vector2(left, -top);
        }

        // JS clamp(v,a,b) = Math.max(a, Math.min(b, v)) — `a` wins when the range is inverted.
        static float Clamp(float v, float a, float b) => Mathf.Max(a, Mathf.Min(b, v));

        void HideMenu()
        {
            if (kitMenu != null) kitMenu.Hide();
            if (menuView != null) menuView.Hide();
        }
    }
}
