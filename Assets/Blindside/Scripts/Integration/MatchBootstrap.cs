// Minimal concrete implementation of ITurnUnit/IVisionAgent plus a MonoBehaviour that wires up
// MapGrid + VisionSystem + CombatSystem + EnemyAI + TurnManager and runs one full AI-vs-AI match.
// This is the smallest harness that can drive RunMatch() to completion — no rendering/placement UI,
// just enough state for the turn/combat/vision systems already ported from the source to operate on.
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Blindside.Data;
using Blindside.Map;
using Blindside.Vision;
using Blindside.Combat;
using Blindside.Abilities;
using Blindside.Turns;
using Blindside.Rendering;
using Blindside.Movement;
using Blindside.UI;
using Blindside.Cinematic;

namespace Blindside.Integration
{
    public class SimUnit : ITurnUnit, IVisionAgent, IOperatorNamed, IFacingLockable, IRevealable, ISpeedRated, IAlertable, IJammable, Blindside.Combat.ICombatModifiers
    {
        // growth (Progress.ProgressBonus: the player's operators in a real match); neutral for everyone else
        public float DamageScale { get; set; } = 1f;
        public float AccuracyBonus { get; set; }
        public float DamageTakenScale { get; set; } = 1f;
        public float MoveBonus { get; set; }
        public string Id { get; set; }
        // X/Y/Facing/Alive raise Changed when their value actually changes — the cue for anything that
        // derives from them (the vision polygon) to recompute instead of polling every frame.
        float x, y, facing; bool alive = true;
        public float X { get => x; set { if (x != value) { x = value; Changed?.Invoke(this); } } }
        public float Y { get => y; set { if (y != value) { y = value; Changed?.Invoke(this); } } }
        public float Facing { get => facing; set { if (facing != value) { facing = value; Changed?.Invoke(this); } } }
        public OperatorTeam Team { get; set; }
        public bool Alive { get => alive; set { if (alive != value) { alive = value; Changed?.Invoke(this); } } }
        /// <summary>Position, facing or alive state changed.</summary>
        public event System.Action<SimUnit> Changed;
        /// <summary>Figure spawned from Unit_Simple.prefab (whatever IUnitView sits on its root) — kept
        /// in sync with X/Y/Facing/HP/Alive every frame by MatchBootstrap.Update() so the match is
        /// watchable, not just simulated.</summary>
        public IUnitView View { get; set; }
        public bool IsHostage => false;
        public int HP { get; set; } = 100;
        public int MaxHP { get; set; } = 100;
        public WeaponData Weapon { get; set; }
        public bool ShieldUp => Shield;

        public string AbilityKey { get; set; } = "";
        public int Mv { get; set; } = 3;
        public int Charges { get; set; }
        public bool AbilityUsed { get; set; }
        public float MoveLeft { get; set; }
        public bool Stim { get; set; }
        public bool Ghost { get; set; }
        public bool Shield { get; set; }

        public bool Acted { get; set; }
        public bool Ended { get; set; }
        // u.placed: false while an attacker is still aboard the helicopter (no figure, no sight — VisionSystem /
        // TeamVisionBuilder / VisionPolyController all read it). Raises Changed so fog and polygons refresh.
        bool placed = true;
        public bool Placed { get => placed; set { if (placed != value) { placed = value; Changed?.Invoke(this); } } }
        /// <summary>On the rope, between appearing and touching down: the figure is drawn but has no sight yet.</summary>
        public bool Airborne { get; set; }
        /// <summary>u.rz — height above the floor during the rope descent (0 otherwise).</summary>
        public float RZ { get; set; }

        /// <summary>Source operator record (OPS entry) — identity/illustration/ability text for the
        /// player-facing panels (showUnitPanel/showOpArt), and the source of HP/Mv/Weapon (mkUnit).</summary>
        public OperatorData Op { get; set; }
        /// <summary>mkUnit's `id:++UID` — creation order in startGame() (defenders 1-5, attackers
        /// 6-10), shown as OP-### in the unit panel and illustration card.</summary>
        public int Uid { get; set; }
        // Remaining per-unit flags commands() reads (mkUnit defaults). Nothing sets them yet — the
        // gadget/facing systems aren't wired into this harness — but the menu's enable rules use them.
        public bool GadgetUsed { get; set; }
        public bool DroneSpent { get; set; }
        public Blindside.Drones.Drone Drone { get; set; }
        public bool LockFacing { get; set; }
        /// <summary>u.revealT (mkUnit: 0).</summary>
        public float RevealT { get; set; }
        /// <summary>u.alertT (mkUnit: -1e9) — the overhead "!" after an encounter.</summary>
        public float AlertT { get; set; } = -1e9f;
        /// <summary>speedOf(u): `u.mv+(u.stim?1:0)`.</summary>
        public float Speed => Mv + (Stim ? 1 : 0) + WeaponData.SpeedBonusOf(Weapon);

        public float RX { get; set; }
        public float RY { get; set; }
        public string Kr => Op != null ? Op.kr : Id;

        public void MoveTo(float x, float y) { X = x; Y = y; RX = x; RY = y; }

        public override string ToString() => $"{Id}({Team}, hp={HP})";
    }

    /// <summary>G.hostage — `{hp:100,maxHp:100,alive:true,team:'host',kr:'人質',key:'HOSTAGE',shield:false,facing:0}`.
    /// Also an ICombatUnit so fireCast() can put it on the line of fire (exchange(u,G.hostage,true)).</summary>
    public class SimHostage : ITurnHostage, IEscortableHostage, ICombatUnit, IOperatorNamed
    {
        public float X { get; set; }
        public float Y { get; set; }
        public bool Alive { get; set; } = true;
        public int HP { get; set; } = 100;
        public int MaxHP { get; set; } = 100;
        public float Facing { get; set; }
        public IUnitView View { get; set; }
        public bool IsHostage => true;
        public string Kr => "人質";
        public string Key => "HOSTAGE";
        // team:'host' isn't an OperatorTeam; CombatSystem branches on IsHostage wherever that matters and
        // never reads a hostage's Team (it never shoots back), so this value is never consulted.
        OperatorTeam ICombatUnit.Team => OperatorTeam.Defense;
        WeaponData ICombatUnit.Weapon => null;
        bool ICombatUnit.ShieldUp => false;
        public ITurnUnit Escort { get; private set; }
        public OperatorTeam? KilledByTeam { get; set; }
        /// <summary>h.trail — the escort's breadcrumbs that followHostage walks back along.</summary>
        public List<Vector2> Trail { get; } = new List<Vector2>();
        /// <summary>setEscort(u): `h.escort=u; h.trail=[{x:h.x,y:h.y},{x:u.x,y:u.y}]`.</summary>
        public void SetEscort(ITurnUnit u)
        {
            Escort = u;
            Trail.Clear();
            if (u != null) { Trail.Add(new Vector2(X, Y)); Trail.Add(new Vector2(u.X, u.Y)); }
        }
        /// <summary>followHostage's `h.x=…;h.y=…; tween(h,{rx:h.x,ry:h.y},90,true)`: the logical position jumps,
        /// the drawn one (rx/ry) glides there linearly over 90ms from wherever it currently is.</summary>
        public void SetPosition(float x, float y)
        {
            tweenFromX = RX; tweenFromY = RY; tweenT0 = Time.time;
            X = x; Y = y;
        }

        const float TweenSeconds = .09f;
        float tweenFromX, tweenFromY, tweenT0 = -1e9f;
        /// <summary>h.rx/h.ry — the drawn position (grid space); equals X/Y once the tween has finished.</summary>
        public float RX => Mathf.Lerp(tweenFromX, X, Mathf.Clamp01((Time.time - tweenT0) / TweenSeconds));
        public float RY => Mathf.Lerp(tweenFromY, Y, Mathf.Clamp01((Time.time - tweenT0) / TweenSeconds));
    }

    public partial class MatchBootstrap : MonoBehaviour
    {
        /// <summary>The attack side's operators in the order they are fielded (OPS.atk) — also the title's squad list order.</summary>
        public static readonly string[] AttackerKeys = { "CRUSHER", "FLARE", "IGNIS", "SERUM", "BASTION" };

        public GameplayUIBridge uiBridge;
        public bool runOnStart = true;

        /// <summary>Unit_Simple.prefab — instantiated once per SimUnit. Left empty in the Inspector,
        /// this falls back to loading the prefab straight from its known project path (same pattern as
        /// LoadWeapon below), so no manual scene wiring is required.</summary>
        public GameObject unitPrefab;

        List<SimUnit> activeUnits;
        SimHostage activeHostage;
        BreachAbilityPresenter breachPresenter;
        SupportAbilityPresenter supportPresenter;
        ElectricFieldView electricView;
        GadgetAbilityPresenter gadgetPresenter;
        GadgetViews gadgetViews;
        ReinforcedWallViews reinforcedViews;
        DroneViews droneViews;
        IUnitView hostageView;
        HostageAnimator hostageFigure;
        HeliDeployRig heliDeploy;

        /// <summary>Set (e.g. by TitleSelectScreen) before calling RunOneMatch to make this side
        /// wait for player input instead of running AI. Null runs the original AI-vs-AI match.</summary>
        public OperatorTeam? playerTeam;

        /// <summary>The next RunOneMatch is the tutorial (TutorialDirector): FLARE alone against CRUSHER (not placed), no defense
        /// preparation, no helicopter, straight into the attack phase, and the match never ends by itself (TurnManager.HoldResult).</summary>
        public bool tutorialMode;
        /// <summary>A match has been set up and is about to run (the tutorial's cue).</summary>
        public static event System.Action<MatchBootstrap> MatchReady;
        public IReadOnlyList<SimUnit> ActiveUnits => activeUnits;
        public SimHostage ActiveHostage => activeHostage;
        public TurnManager ActiveTurns { get; private set; }
        public MapGrid ActiveMap { get; private set; }
        public VisionSystem ActiveVision { get; private set; }
        public CombatSystem ActiveCombat { get; private set; }
        public AbilitySystem ActiveAbilities { get; private set; }
        /// <summary>The running match's record per operator (damage, attacks, walls, kills, assists) for the result screen.</summary>
        public Blindside.Progress.MatchRecord Record { get; private set; }
        /// <summary>The running match's experience as it is given (null: not a real match of the player's).</summary>
        public Blindside.Progress.ProgressLive Live { get; private set; }

        // the battle cinematic's beat after an exchange: what it gave the player's operators, shown before the camera leaves the stage
        IEnumerator ExperienceBeat(ICombatUnit first, ICombatUnit second)
        {
            if (Live == null) yield break;
            var gains = Live.Flush(first, second);
            if (gains.Count == 0) yield break;
            var popup = ExpGainPopup.Show(gains);
            if (battleDirector != null) yield return battleDirector.Wait(popup.Duration);
        }
        public AbilityContext ActiveAbilityCtx { get; private set; }
        public DroneOps ActiveDroneOps { get; private set; }

        /// <summary>Player-side selection + command menu (auto-added to this GameObject if left empty).</summary>
        public PlayerSelectionController selection;

        /// <summary>The scene's #topbar/#roster/#log HUD (found in the scene if left empty).</summary>
        public HudController hud;

        /// <summary>Player-side vision polygons (found in the scene if left empty).</summary>
        public VisionPolyController visionPolys;

        /// <summary>Fog / wall dimming / enemy show-hide (auto-added to this GameObject if left empty).</summary>
        public VisibilityController visibility;
        /// <summary>The player side's position marks (auto-added to this GameObject if left empty).</summary>
        public MarkViewController markViews;

        /// <summary>Battle cinematic (auto-added to this GameObject if left empty).</summary>
        public BattleDirector battleDirector;
        /// <summary>Its tuning (loaded from Assets/Blindside/Data/Cinematic/BattleCamProfile.asset if left empty).</summary>
        public BattleCamProfile battleCamProfile;

        /// <summary>Dev harness (SimBatch): in an AI-vs-AI match also run DefensePrep and the random attacker drop.</summary>
        public bool simSetup;
        /// <summary>What the defense preparation / the attackers' random drop did in the last match (null when the match had none).</summary>
        public PrepReport LastPrep { get; private set; }
        public SpawnPlan LastSpawn { get; private set; }
        /// <summary>Ids of the units that died in the last match, in order of death ("HOSTAGE" for the hostage).</summary>
        public readonly List<string> DeathOrder = new List<string>();
        public int RoundsPlayed { get; private set; }

        public bool Finished { get; private set; }
        public MatchResult Result { get; private set; }
        public string ResultReason { get; private set; }
        public System.Exception Error { get; private set; }

        IEnumerator Start()
        {
            if (runOnStart) yield return RunOneMatch();
        }

        void Update()
        {
            if (activeUnits == null) return;
            foreach (var u in activeUnits)
            {
                SyncView(u.View, u.RX, u.RY, u.Facing, u.Alive, u.HP, u.MaxHP);
                if (u.View == null) continue;
                u.View.SetHeight(u.RZ);
                // revealT (set by a shot or an encounter) doubles as "in combat": the model keeps its aim pose meanwhile
                u.View.SetCombatReady(u.Alive && Time.unscaledTime < u.RevealT);
                // syncUnits: `show=u.alive&&u.placed!==false&&…` — the player's own not-yet-dropped units aren't in
                // the scene (the enemy side is hidden by VisibilityController via the same Placed flag).
                if (playerTeam == OperatorTeam.Attack && u.Team == OperatorTeam.Attack && u.Alive) u.View.SetVisible(u.Placed || u.Airborne);
            }
            if (activeHostage != null)
            {
                // syncUnits: `g.position.set(h.rx,0,h.ry)`; `f=h.escort?atan2(escort.ry-h.ry,escort.rx-h.rx):PI/2` (UnitView eases the turn).
                var h = activeHostage;
                float rx = h.RX, ry = h.RY;
                float f = h.Escort is ITurnUnit es ? Mathf.Atan2(es.RY - ry, es.RX - rx) : Mathf.PI / 2f;
                h.Facing = f;
                SyncView(hostageView, rx, ry, f, h.Alive, h.HP, h.MaxHP);
                if (hostageFigure != null) hostageFigure.Escorted = h.Escort != null;
            }
        }

        // Catch-all for state changed outside CombatSystem/TurnManager (EnemyAI moves, ability facing,
        // gas damage, ...) — the same syncUnits() pass the source runs every frame.
        static void SyncView(IUnitView view, float x, float y, float facing, bool alive, int hp, int maxHp)
        {
            if (view == null) return;
            if (!alive) { view.PlayDeath(); return; }
            view.SetPosition(x, y);
            view.SetFacing(facing);
            view.SetHealth(hp, maxHp);
        }

        static void DestroyView(IUnitView view)
        {
            if (view is Component c && c != null) Destroy(c.gameObject);
        }

        /// <summary>
        /// Puts everything a match touched back to its pre-match state: input locks, time scale, engagement staging,
        /// the selection / HUD / vision / fog / mark / gadget / drone / electric-field / reinforced-wall views, the unit
        /// and hostage figures, every broken or reinforced wall block (a fresh MapGrid is bound on the next start),
        /// the log, and the pending result screen. Safe to call at any time, including with no match running.
        /// </summary>
        public void ClearMatch()
        {
            StopAllCoroutines();   // the running match (RunOneMatch → RunMatch → AI / combat / walks are nested in it) + a pending result
            Time.timeScale = 1f;   // HitStop / kill slow motion restore theirs in a finally that a stopped coroutine skips
            if (battleDirector != null) { battleDirector.Abort(); battleDirector.HideOption(); }
            // END TURN / START OP waits for the next match's player phase (it stayed up over the next intro otherwise)
            if (uiBridge != null && uiBridge.endTurnButton != null) uiBridge.endTurnButton.gameObject.SetActive(false);
            BattlefieldCamera.InputLocked = false;
            if (resultScreen != null) resultScreen.Hide();
            if (operationResult != null) operationResult.Hide();
            Record?.Unbind();

            if (selection == null) selection = GetComponent<PlayerSelectionController>();
            if (selection == null) selection = gameObject.AddComponent<PlayerSelectionController>();
            selection.Unbind();
            if (hud == null) hud = FindAnyObjectByType<HudController>(FindObjectsInactive.Include);
            if (hud != null) hud.Unbind();
            if (visionPolys == null) visionPolys = FindAnyObjectByType<VisionPolyController>(FindObjectsInactive.Include);
            if (visionPolys != null) visionPolys.Unbind();
            if (visibility == null) visibility = GetComponent<VisibilityController>();
            if (visibility == null) visibility = gameObject.AddComponent<VisibilityController>();
            visibility.Unbind();
            if (markViews == null) markViews = GetComponent<MarkViewController>();
            if (markViews == null) markViews = gameObject.AddComponent<MarkViewController>();
            markViews.Unbind();
            breachPresenter?.Unbind();
            supportPresenter?.Unbind();
            gadgetPresenter?.Unbind();
            electricView?.Unbind();
            gadgetViews?.Unbind();
            droneViews?.Unbind();
            reinforcedViews?.Unbind();
            MapWallSync.Bind(null);      // every wall block back on, dimming off
            BreachTileViews.Bind(null);  // rubble / holes gone
            HudLog.PlayerTeam = playerTeam;
            HudLog.Clear();

            if (activeUnits != null)
                foreach (var u in activeUnits) DestroyView(u.View);
            activeUnits = null;
            DestroyView(hostageView);
            hostageView = null;
            activeHostage = null;
            // The intro's helicopter stays hovering if a match was left before the deployment ended; every intro switches it on again itself.
            var heliRigRoot = FindAnyObjectByType<IntroCameraRig>(FindObjectsInactive.Include);
            if (heliRigRoot != null && heliRigRoot.heliTarget != null) heliRigRoot.heliTarget.gameObject.SetActive(false);
        }

        public IEnumerator RunOneMatch()
        {
            Finished = false;
            Error = null;
            kills[0] = kills[1] = 0;
            DeathOrder.Clear();
            RoundsPlayed = 0;

            if (selection == null) selection = GetComponent<PlayerSelectionController>();
            if (selection == null) selection = gameObject.AddComponent<PlayerSelectionController>();
            selection.Unbind();
            if (hud == null) hud = FindAnyObjectByType<HudController>(FindObjectsInactive.Include);
            if (hud != null) hud.Unbind();
            if (visionPolys == null) visionPolys = FindAnyObjectByType<VisionPolyController>(FindObjectsInactive.Include);
            if (visionPolys != null) visionPolys.Unbind();
            if (visibility == null) visibility = GetComponent<VisibilityController>();
            if (visibility == null) visibility = gameObject.AddComponent<VisibilityController>();
            visibility.Unbind();
            if (markViews == null) markViews = GetComponent<MarkViewController>();
            if (markViews == null) markViews = gameObject.AddComponent<MarkViewController>();
            markViews.Unbind();
            HudLog.PlayerTeam = playerTeam;
            HudLog.Clear();

            if (activeUnits != null)
                foreach (var u in activeUnits) DestroyView(u.View);
            activeUnits = null;
            DestroyView(hostageView);
            hostageView = null;

            var mapData = MapLibrary.Current;
            var map = MapGrid.CreateDefault();
            MapFloorSync.Apply(mapData);   // the floor planes follow the map's size
            var vision = new VisionSystem(map);
            var combat = new CombatSystem(map);

            var weaponAr = LoadWeapon("Weapon_AR");
            var weaponPst = LoadWeapon("Weapon_PST");

            var units = new List<SimUnit>();
            // Attack team: the map's default drop points (ATK_SPAWN in 01_core_data_map_vision.js).
            var atkSpawn = mapData.anchors.attackerDrops;
            if (tutorialMode)
            {
                // the tutorial: FLARE alone, standing at the first drop point; CRUSHER (the instructor) on the other side at the first
                // defender start, kept off the map (not placed) until a lesson puts him somewhere
                var flare = MakeUnit("atk1", OperatorTeam.Attack, atkSpawn[0].x, atkSpawn[0].y, weaponAr);   // facing 0: east, along the training yard
                flare.Op = LoadOperator("FLARE"); flare.Uid = 2;
                var ds0 = mapData.defenderStarts[0];
                var crusher = MakeUnit("CRUSHER", OperatorTeam.Defense, ds0.cell.x, ds0.cell.y, weaponPst, ds0.facing);
                crusher.Op = LoadOperator("CRUSHER"); crusher.Uid = 1;
                units.Add(flare); units.Add(crusher);
            }
            else
            for (int i = 0; i < atkSpawn.Length; i++)
                units.Add(MakeUnit($"atk{i + 1}", OperatorTeam.Attack, atkSpawn[i].x, atkSpawn[i].y, weaponAr));

            // Defense team: DEF_POST from the same source file — each operator's (x,y,facing), now in the map data.
            // NOTE: ARGUS's post (13,16) replaces the doorway tile (13,14) the source itself doesn't use
            // for standing room; EnemyAI's pathing here treats a living enemy's cell as fully impassable,
            // so a defender exactly on the hostage room's one doorway used to freeze the whole attack
            // team in place (goal-distance field unreachable from every attacker-side cell). (13,16)
            // still fortifies the hostage's other flank without sealing the only way in.
            // (Read from MapData.defenderStarts, in VOLT, MIASMA, WRAITH, OBSIDIAN, ARGUS order.)
            if (!tutorialMode)
            foreach (var ds in mapData.defenderStarts)
                units.Add(MakeUnit(ds.operatorKey, OperatorTeam.Defense, ds.cell.x, ds.cell.y, weaponPst, ds.facing));

            // OPS records: attackers take OPS.atk in order (spawnAttackers: `OPS.atk.forEach((o,i)=>...
            // spots[i])`), defenders are already keyed by name. UIDs follow startGame()'s creation
            // order — OPS.def first (1-5), then spawnAttackers (6-10).
            var atkKeys = AttackerKeys;
            int uid = 0, atkIdx = 0;
            if (!tutorialMode)
            {
                foreach (var u in units)
                    if (u.Team == OperatorTeam.Defense) { u.Op = LoadOperator(u.Id); u.Uid = ++uid; }
                foreach (var u in units)
                    if (u.Team == OperatorTeam.Attack) { u.Op = LoadOperator(atkKeys[atkIdx++]); u.Uid = ++uid; }
            }
            // mkUnit(team,op,...): `Object.assign({},op,{... hp:op.hp, maxHp:op.hp ...})` — hp, mv and
            // weapon all come from the operator record (MakeUnit's 100/3/AR|PST are only fallbacks
            // for a missing asset).
            foreach (var u in units)
            {
                if (u.Op == null) continue;
                u.HP = u.MaxHP = u.Op.hp;
                u.Mv = u.Op.mv;
                // the gun picked before the operation (the player's own operators), else the operator's first / default gun
                var gun = Loadout.Of(u.Op, playerTeam.HasValue && u.Team == playerTeam.Value);
                if (gun != null) u.Weapon = gun;
                u.AbilityKey = u.Op.abilityKey; u.Charges = u.Op.charges;
            }
            // the player's operators grow (levels' stat points, weapon mastery) — a real match with a save session only
            if (playerTeam.HasValue && !tutorialMode && !PlaySession.IsTest)
                foreach (var u in units) if (u.Team == playerTeam.Value) Blindside.Progress.ProgressBonus.Apply(u);

            // startGame() (attack side): `for(const a of G.units) if(a.team==='atk') a.placed=false` — the squad is
            // still aboard the helicopter: not in the scene, no sight, until it is dropped in the deploy phase.
            if (playerTeam == OperatorTeam.Attack && !tutorialMode)
                foreach (var u in units) if (u.Team == OperatorTeam.Attack) u.Placed = false;
            if (tutorialMode)
                foreach (var u in units) if (u.Team == OperatorTeam.Defense) u.Placed = false;
            // startGame() (defense side): no attackers exist yet — they appear when the preparation ends ([作戦開始]).
            if (playerTeam == OperatorTeam.Defense)
                foreach (var u in units) if (u.Team == OperatorTeam.Attack) u.Placed = false;

            var prefab = unitPrefab != null ? unitPrefab : LoadUnitPrefab();
            foreach (var u in units) SpawnUnitView(u, prefab);
            activeUnits = units;
            vision.Agents = units;   // G.units for teamVision()/enemyVisible()

            foreach (var du in units)
            {
                var deadUnit = du;
                deadUnit.Changed += _ => { if (!deadUnit.Alive && !DeathOrder.Contains(deadUnit.Id)) DeathOrder.Add(deadUnit.Id); };
            }
            var turnUnits = units.ConvertAll(u => (ITurnUnit)u);
            var hostageCell = mapData.hostage;
            var hostage = new SimHostage { X = hostageCell.x, Y = hostageCell.y };
            activeHostage = hostage;
            hostageView = SpawnHostageView(hostage, prefab);
            hostage.View = hostageView;
            if (tutorialMode && hostageView != null) hostageView.SetVisible(false);   // (the tutorial map walls the hostage cell in; nobody is to see it)

            // Let at least one real frame render with all 11 at their spawn poses before any AI/turn
            // logic (which used to run to completion inside the same synchronous call) touches them.
            yield return null;

            var ai = new EnemyAI(map, vision, combat, turnUnits, hostage);
            var turns = new TurnManager(map, turnUnits, hostage) { HoldResult = tutorialMode };
            // exchange()'s `if(G.over) return` — the first shot's kill() has run checkWin() by then in the source.
            combat.IsOver = () => turns.CheckWin();

            // CombatSystem deliberately leaves ICombatUnit.Alive get-only and never flips it itself
            // (see the file header in CombatSystem.cs — kill/checkWin side effects are the caller's
            // job, same split VisionSystem uses). Nothing else in this project's wiring ever sets it,
            // so without this, a unit's HP can hit 0 and OnKill can fire while Alive stays true forever
            // — TurnManager.CheckWin's wipe detection never triggers (only the round-15 timeout can end
            // a match) and UnitView never hides a "dead" unit.
            // kill(): host → `t.killedBy=by.team` (checkWin reads it to pick the winner).
            combat.OnKill += (victim, killer) =>
            {
                if (victim is SimUnit su) { su.Alive = false; kills[killer.Team == OperatorTeam.Attack ? 0 : 1]++; }   // G.stats[by.team]++
                else if (victim is SimHostage sh) { sh.HP = 0; sh.Alive = false; sh.KilledByTeam = killer.Team; DeathOrder.Add("HOSTAGE"); }
                turns.CheckWin();   // kill() ends with checkWin()
            };

            // breakPanel()'s drawFloor()/refreshPanel(): the scene's static wall blocks follow the map.
            MapWallSync.Bind(map);
            BreachTileViews.Bind(map);   // …and cellMesh's rubble/hole in the broken cells
            // cellMesh's reinforced panel: steel shutter + frame + yellow rail (the attack side only once it has seen the wall).
            if (reinforcedViews == null) reinforcedViews = GetComponent<ReinforcedWallViews>();
            if (reinforcedViews == null) reinforcedViews = gameObject.AddComponent<ReinforcedWallViews>();
            reinforcedViews.Bind(map, vision, units, playerTeam);

            // G.marks + the revealT timers.
            var marks = new MarkBoard(() => turns.PhaseCount);
            // shoot(): a.revealT=now()+3500; t.revealT=Math.max(t.revealT||0,now()+2500);
            combat.OnShotStart += (a, t) =>
            {
                float now = Time.unscaledTime;
                if (a is IRevealable ra) ra.RevealT = now + 3.5f;
                if (t is IRevealable rt) rt.RevealT = Mathf.Max(rt.RevealT, now + 2.5f);
            };
            // shoot(): addMark(other(a.team),a.x,a.y,'shot') once the burst is done.
            combat.OnResolved += (a, t, r) => marks.Add(MarkBoard.Other(a.Team), a.X, a.Y, MarkKind.Shot);
            // resolveEncounter(): e.revealT=T+3000; u.revealT=T+3000 — EnemyAI's contact moment.
            ai.OnEncounter += (mover, enemy, mutual) =>
            {
                float now = Time.unscaledTime;
                if (mover is IRevealable rm) rm.RevealT = now + 3f;
                if (enemy is IRevealable re) re.RevealT = now + 3f;
            };
            // moveAlong's `show` for AI moves: unseen steps don't pull the camera.
            ai.PlayerSide = playerTeam;
            if (playerTeam.HasValue) ai.PanTo = (x, y, ms) => { if (BattlefieldCamera.Instance != null) BattlefieldCamera.Instance.PanTo(x, y, ms); };

            visibility.Bind(vision, map, units, turns, playerTeam);
            markViews.Bind(marks, visibility, units);

            if (uiBridge != null) uiBridge.Bind(combat, turns, ai, playerTeam);
            selection.Bind(turns, units, hostage, map, playerTeam, uiBridge, combat, vision, marks, visibility.ShownToPlayer);
            // aiMoveTo → moveAlong: the AI walks through the same MovementSystem (and its encounter checks)
            // as the player's MOVE command.
            ai.Movement = selection.Movement;
            // useAbility(): one dispatcher + context for the player's ABILITY command and aiAtk's breach
            // shortcuts; BreachAbilityPresenter turns the breach events into the source's fx/sfx/addMark/log.
            var abilities = AbilitySystem.CreateDefault();
            // G.drones: shock's drone kill reaches into it (drone deployment itself isn't connected yet).
            var abilityCtx = new AbilityContext { Map = map, Vision = vision, Drones = new Blindside.Drones.DroneController(), Units = units.ConvertAll(u => (IAbilityUnit)u) };
            breachPresenter?.Unbind();
            breachPresenter = new BreachAbilityPresenter(abilities, marks);
            // stim / shield / shock: popup · fxRing · sfx · log, and the G.electrics fields + electrified walls.
            supportPresenter?.Unbind();
            supportPresenter = new SupportAbilityPresenter(abilities);
            if (electricView == null) electricView = GetComponent<ElectricFieldView>();
            if (electricView == null) electricView = gameObject.AddComponent<ElectricFieldView>();
            electricView.Bind(abilityCtx.Electrics, map);
            // G.cams starts with the map's fixed cameras (startGame: placed:false); ARGUS adds placed ones.
            foreach (var fc in map.FixedCameras)
                abilityCtx.Cameras.Add(new SurveillanceCamera { x = fc.x, y = fc.y, placed = false });
            // startGame() (attack side): aiDefPrep() — the defense has already fortified before the squad drops in.
            LastPrep = null; LastSpawn = null;
            if (playerTeam == OperatorTeam.Attack && !tutorialMode) LastPrep = DefensePrep.Apply(map, vision, abilityCtx, units, hostage);
            // startGame() (defense side) → endPhase()'s prep branch: `spawnAttackers(true); G.round=1; phase('atk')`.
            if (playerTeam == OperatorTeam.Defense)
            {
                turns.PrepEnded += () => LastSpawn = AttackerSpawn.Spawn(map, abilityCtx, units);
            }
            // Dev harness only (SimBatch): an AI-vs-AI match that also runs the two map-dependent setup steps a
            // player match has — the defense's preparation and the attackers' random outside drop spots.
            if (!playerTeam.HasValue && simSetup)
            {
                LastPrep = DefensePrep.Apply(map, vision, abilityCtx, units, hostage);
                LastSpawn = AttackerSpawn.Spawn(map, abilityCtx, units);
            }
            // enemyVisible()'s gadgetSees(team,px,py): drones r 2.5 (atk) / cameras r 3.2 (def), LOS-gated.
            vision.GadgetSees = (team, px, py) =>
            {
                if (team == OperatorTeam.Attack)
                {
                    foreach (var d in abilityCtx.Drones.Drones)
                        if (d.alive && Mathf.Sqrt((d.x - px) * (d.x - px) + (d.y - py) * (d.y - py)) <= 2.5f && vision.LineOfSight(d.x, d.y, px, py, OperatorTeam.Attack)) return true;
                }
                else
                    foreach (var c in abilityCtx.Cameras)
                        if (c.alive && Mathf.Sqrt((c.x - px) * (c.x - px) + (c.y - py) * (c.y - py)) <= 3.2f && vision.LineOfSight(c.x, c.y, px, py, OperatorTeam.Defense)) return true;
                return false;
            };
            visibility.Gadgets = abilityCtx;
            // gas / ghost / mirror / cam: fx · sfx · log, gas damage + its per-phase tick; and their world views.
            gadgetPresenter?.Unbind();
            gadgetPresenter = new GadgetAbilityPresenter(abilities, abilityCtx, turns, units, hostage);
            if (gadgetViews == null) gadgetViews = GetComponent<GadgetViews>();
            if (gadgetViews == null) gadgetViews = gameObject.AddComponent<GadgetViews>();
            gadgetViews.Bind(abilityCtx, units);

            // deployDrone / moveDrone / aiDrone / enemyGadgets / destroyGadget / aiShootGadgets.
            var droneOps = new DroneOps(map, vision, abilityCtx, units)
            {
                PlayerSide = playerTeam,
                Movement = selection.Movement,
                Marks = marks,
                Hostage = hostage,
                Round = () => turns.Round,
                CellVisibleToPlayer = playerTeam.HasValue ? (System.Func<float, float, bool>)visibility.CellVisible : null,
                UnitsChanged = () => { },
            };
            if (playerTeam.HasValue) droneOps.PanTo = (x, y, ms) => { if (BattlefieldCamera.Instance != null) BattlefieldCamera.Instance.PanTo(x, y, ms); };
            selection.BindDrones(droneOps);
            ai.ShootGadgets = droneOps.AiShootGadgets;
            ai.DeployDrone = droneOps.AiDrone;
            if (droneViews == null) droneViews = GetComponent<DroneViews>();
            if (droneViews == null) droneViews = gameObject.AddComponent<DroneViews>();
            droneViews.Bind(abilityCtx.Drones.Drones, droneOps.CellVisibleToPlayer);
            // beginPhase(team): u.gadgetUsed=false for that team's units; that team's drones get their 8m back.
            turns.OnPhaseBegin += phase =>
            {
                var acting = TurnManager.ActingTeam(phase);
                foreach (var u in units) if (u.Team == acting) u.GadgetUsed = false;
                if (acting == OperatorTeam.Attack) abilityCtx.Drones.ResetMoveBudget();
            };
            selection.BindAbilities(abilities, abilityCtx);
            // Deploy phase (attack side): the helicopter the intro flew in stays in the scene and drops the squad.
            if (playerTeam == OperatorTeam.Attack && !tutorialMode)
            {
                if (heliDeploy == null) heliDeploy = GetComponent<HeliDeployRig>();
                if (heliDeploy == null) heliDeploy = gameObject.AddComponent<HeliDeployRig>();
                heliDeploy.Setup(FindAnyObjectByType<HeliRig>(FindObjectsInactive.Include));
                turns.OnPhaseBegin += phase => { if (phase == MatchPhase.Deploy) heliDeploy.Begin(); };
                selection.BindDeploy(heliDeploy);
            }
            else selection.BindDeploy(null);
            ai.Abilities = abilities; ai.AbilityCtx = abilityCtx;
            if (hud != null) hud.Bind(turns, combat, ai, units, selection, playerTeam);
            if (visionPolys != null) visionPolys.Bind(vision, map, units, playerTeam, selection);
            BindBattleDirector(combat, units, hostage);

            gadgetPresenter.OnGasKill = _ => kills[1]++;   // a gas death is the defense's kill
            turns.OnFinish += (result, reason) =>
            {
                Finished = true; Result = result; ResultReason = reason; RoundsPlayed = turns.Round;
                OnMatchFinished(turns, units, hostage, result, reason);
            };

            // what the tutorial's director works with (TutorialDirector waits for MatchReady)
            ActiveTurns = turns; ActiveMap = map; ActiveVision = vision; ActiveCombat = combat; ActiveAbilities = abilities; ActiveAbilityCtx = abilityCtx;
            ActiveDroneOps = droneOps;
            // the result screen's record (it only listens to the combat / ability events)
            Record?.Unbind();
            Record = null;
            if (Blindside.Progress.MatchRecord.Enabled)
            {
                Record = new Blindside.Progress.MatchRecord(units);
                Record.Bind(combat, abilities);
            }
            // experience as it is earned (the player's side in a real match): given after every staged exchange, shown on the stage
            Live = null;
            if (Record != null && playerTeam.HasValue && !tutorialMode && !PlaySession.IsTest)
            {
                var save = Blindside.Save.SaveSystem.Current;
                string stageMap = save != null ? save.lastMap ?? "" : "";
                Live = new Blindside.Progress.ProgressLive(Record, playerTeam.Value, save, Blindside.Progress.StageRewards.For(stageMap).expScale);
            }
            if (battleDirector != null) battleDirector.AfterExchange = Live != null ? ExperienceBeat : (System.Func<ICombatUnit, ICombatUnit, IEnumerator>)null;
            MatchReady?.Invoke(this);

            IEnumerator RunGuarded()
            {
                // startGame(): an attack-side player starts in the deployment; a defense-side player goes straight to the preparation.
                // (the tutorial: straight into round 1's attack phase, the squad already on the ground)
                yield return turns.RunMatch(ai, tutorialMode ? MatchPhase.Attack : playerTeam == OperatorTeam.Defense ? MatchPhase.Prep : MatchPhase.Deploy, playerTeam);
            }

            yield return StartCoroutine(SafeRun(RunGuarded()));

            if (Error != null)
                Debug.LogError($"[MatchBootstrap] Match ended with an exception: {Error}");
            else
                Debug.Log($"[MatchBootstrap] Match finished — result={Result}, reason=\"{ResultReason}\", round={RoundOf(turns)}, phaseCount={turns.PhaseCount}");
        }

        // TurnManager doesn't expose Round publicly beyond the getter already there; kept as a tiny
        // helper so the log line above stays a one-liner.
        static int RoundOf(TurnManager tm) => tm.Round;

        IEnumerator SafeRun(IEnumerator routine)
        {
            while (true)
            {
                object current;
                try
                {
                    if (!routine.MoveNext()) yield break;
                    current = routine.Current;
                }
                catch (System.Exception ex)
                {
                    Error = ex;
                    yield break;
                }
                yield return current;
            }
        }

        /// <summary>Stages the player's engagements (camera, letterbox, Volume, HUD dim, input lock); AI-vs-AI
        /// engagements resolve instantly. Presentation only — CombatSystem's rolls don't change.</summary>
        void BindBattleDirector(CombatSystem combat, List<SimUnit> units, SimHostage hostage)
        {
            if (battleDirector == null) battleDirector = GetComponent<BattleDirector>();
            if (battleDirector == null) battleDirector = gameObject.AddComponent<BattleDirector>();
            if (battleCamProfile == null) battleCamProfile = LoadBattleCamProfile();
            if (battleCamProfile != null) battleDirector.profile = battleCamProfile;

            IEnumerable<ICombatUnit> All()
            {
                foreach (var u in units) yield return u;
                if (hostage != null) yield return hostage;
            }
            battleDirector.Bind(combat, playerTeam, All);

            battleDirector.ClearDimTargets();
            if (hud != null) battleDirector.AddDimTarget(hud.gameObject);
            if (selection != null && selection.OverlayCanvas != null) battleDirector.AddDimTarget(selection.OverlayCanvas.gameObject);
            if (markViews != null) battleDirector.AddDimTarget(markViews.LabelCanvas);
            if (uiBridge != null && uiBridge.endTurnPulse != null) battleDirector.AddDimTarget(uiBridge.endTurnPulse.gameObject);
            var sel = selection;
            battleDirector.CinematicChanged = on => { if (sel != null) sel.SetCinematic(on); };
        }

        // (the registry first — Resources/BlindsideAssetRegistry, what a player build has; the project path only in the editor before it exists)
        static BattleCamProfile LoadBattleCamProfile()
        {
            var r = BuildAssetRegistry.CamProfile;
            if (r != null) return r;
#if UNITY_EDITOR
            return UnityEditor.AssetDatabase.LoadAssetAtPath<BattleCamProfile>("Assets/Blindside/Data/Cinematic/BattleCamProfile.asset");
#else
            Debug.LogError("[Blindside] BattleCamProfile missing from BlindsideAssetRegistry");
            return null;
#endif
        }

        static WeaponData LoadWeapon(string assetName)
        {
            var r = BuildAssetRegistry.Weapon(assetName);
            if (r != null) return r;
#if UNITY_EDITOR
            return UnityEditor.AssetDatabase.LoadAssetAtPath<WeaponData>($"Assets/Blindside/Data/Weapons/{assetName}.asset");
#else
            Debug.LogError($"[Blindside] weapon {assetName} missing from BlindsideAssetRegistry");
            return null;
#endif
        }

        static OperatorData LoadOperator(string key)
        {
            var r = BuildAssetRegistry.Operator(key);
            if (r != null) return r;
#if UNITY_EDITOR
            return UnityEditor.AssetDatabase.LoadAssetAtPath<OperatorData>($"Assets/Blindside/Data/Operators/Operator_{key}.asset");
#else
            Debug.LogError($"[Blindside] operator {key} missing from BlindsideAssetRegistry");
            return null;
#endif
        }

        /// <summary>The unit figure the match spawns: unitPrefab, else Unit_Simple (the intro's rappellers use it too).</summary>
        public GameObject UnitPrefab => unitPrefab != null ? unitPrefab : LoadUnitPrefab();

        static GameObject LoadUnitPrefab()
        {
            var r = BuildAssetRegistry.UnitPrefab;
            if (r != null) return r;
#if UNITY_EDITOR
            return UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Blindside/Prefabs/Units/Unit_Simple.prefab");
#else
            Debug.LogError("[Blindside] Unit_Simple missing from BlindsideAssetRegistry");
            return null;
#endif
        }

        /// <summary>Instantiates the unit prefab and returns whatever IUnitView sits on its root — the
        /// only thing that needs to change to swap the capsule for a sprite or a 3D model.</summary>
        IUnitView SpawnView(GameObject prefab, string name)
        {
            if (prefab == null) return null;
            var go = Instantiate(prefab, transform);
            go.name = name;
            var view = go.GetComponent<IUnitView>();
            if (view == null) { Debug.LogError($"[MatchBootstrap] {prefab.name} has no IUnitView component on its root."); Destroy(go); }
            return view;
        }

        void SpawnUnitView(SimUnit u, GameObject prefab)
        {
            var view = SpawnView(prefab, $"Unit_{u.Id}");
            if (view == null) return;
            // the operator's own model / clips, when set (else the unit prefab's)
            if (view is UnitView uv && u.Op != null && (u.Op.modelPrefab != null || u.Op.animationProfile != null))
                uv.ApplyModel(u.Op.modelPrefab, u.Op.animationProfile);
            if (view is UnitView gv) gv.SetWeapon(u.Weapon);   // the gun in the model's hand
            // Ally = the player's side (source: `team===G.side`); AI-vs-AI keeps attack as "ally".
            view.SetIdentity(u.Team == (playerTeam ?? OperatorTeam.Attack), false);
            SyncView(view, u.RX, u.RY, u.Facing, u.Alive, u.HP, u.MaxHP);
            if (!u.Placed) view.SetVisible(false);   // not aboard-and-drawn: hidden from the very first frame
            u.View = view;
        }

        /// <summary>Visual-only marker for the hostage (SimHostage isn't an ITurnUnit/SimUnit, so it
        /// gets its own view instance instead of going through SpawnUnitView).</summary>
        IUnitView SpawnHostageView(SimHostage hostage, GameObject prefab)
        {
            var view = SpawnView(prefab, "Unit_Hostage");
            if (view == null) return null;
            // its own model, sitting while captive and walking with the escort (HostageLook / HostageAnimator)
            hostageFigure = null;
            var look = HostageLook.Current;
            if (look != null && look.model != null && view is UnitView uv)
            {
                uv.ApplyModel(look.model, look.profile);
                var an = uv.ModelRoot != null ? uv.ModelRoot.GetComponentInChildren<Animator>() : null;
                if (an != null)
                {
                    hostageFigure = an.gameObject.AddComponent<HostageAnimator>();
                    hostageFigure.Init(an, look, uv.ModelRoot);
                }
            }
            view.SetIdentity(false, true);
            SyncView(view, hostage.X, hostage.Y, Mathf.PI / 2f, hostage.Alive, 1, 1);   // makeFigure/facing:0 → syncUnits eases to PI/2 (no escort)
            return view;
        }

        static SimUnit MakeUnit(string id, OperatorTeam team, int x, int y, WeaponData weapon, float facing = 0f) => new SimUnit
        {
            Id = id, Team = team, X = x, Y = y, RX = x, RY = y, Facing = facing, Weapon = weapon, HP = 100, MaxHP = 100, Mv = 3,
        };
    }
}
