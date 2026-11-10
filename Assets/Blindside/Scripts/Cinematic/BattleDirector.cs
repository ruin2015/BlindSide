// Battle cinematic: stages every engagement the player takes part in (CombatSystem.Exchange and
// EnemyAI.FireAt run through CombatSystem.Engagement → Direct below). Presentation only — the rolls in
// CombatSystem.Shoot happen in the same order and with the same odds whatever this does; it only moves
// the camera and stretches/shortens the waits between shots (never UnityEngine.Random: the rolls share it).
//
//   engagement start  two-shot from the side of the line of fire: shooter on the left third, target on
//                     the right third, 2.4m up, FOV 30; best of six candidate spots by line of sight
//   first hit         cut to the hit unit (weapons with 4+ shots: only on a headshot or a death)
//   headshot          close-up in front of the head anchor + slow motion, over the existing hit stop,
//                     #hsFlash and a zoom punch
//   death             slow motion + a 25° orbit around the body + "K.I.A" popup
//   return fire       reverse angle in 0.25s (no pull-back)
//   end               back to the exact game camera pose
// With profile.useBattleStage (full mode) the cuts after the opening shot are replaced by the battle stage: the
// opening shot plays on the map, the camera zooms on the unit shot at, the screen fades, and the rest of the
// engagement (the opening shot replayed first) is acted out by copies of the two units on BattleStage, an urban
// street cut like a firefight (each volley: the shooter alone, then the unit being hit); the end fades back to the map.
// plus letterbox, a battle-only Volume (saturation / vignette), HUD at 25%, overhead HP bars hidden, input
// locked, and Space/Enter/click to skip (the remaining waits collapse to ~0; results are already rolled).
// Everything is timed on unscaled time and restored in a finally block (plus a stall watchdog).
//
// All framing maths is done in grid coordinates (x right, y down the map rows); GridSpace.GridToWorld
// (z = -y) is applied only when a world position is built.
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;
using TMPro;
using Blindside.Combat;
using Blindside.Data;
using Blindside.Rendering;
using Blindside.UI;

namespace Blindside.Cinematic
{
    public enum BattleCinematicMode { Full = 0, Short = 1, Off = 2 }

    // After BattlefieldCamera (-100) so LateUpdate's occluder fade / popups use this frame's camera pose.
    [DefaultExecutionOrder(50)]
    public class BattleDirector : MonoBehaviour, ICombatDirector
    {
        public const string PrefKey = "Blindside.BattleCinematics";

        public BattleCamProfile profile;

        // ================================================================
        //  Option (フル / 短め / オフ), saved in PlayerPrefs
        // ================================================================
        static bool modeLoaded;
        static BattleCinematicMode mode;

        public static BattleCinematicMode Mode
        {
            get
            {
                if (!modeLoaded) { mode = (BattleCinematicMode)Mathf.Clamp(PlayerPrefs.GetInt(PrefKey, 0), 0, 2); modeLoaded = true; }
                return mode;
            }
            set
            {
                mode = value; modeLoaded = true;
                PlayerPrefs.SetInt(PrefKey, (int)value);
                PlayerPrefs.Save();
            }
        }

        public static string ModeLabel(BattleCinematicMode m) => m == BattleCinematicMode.Full ? "フル" : m == BattleCinematicMode.Short ? "短め" : "オフ";

        // ================================================================
        //  Binding
        // ================================================================
        CombatSystem combat;
        OperatorTeam? playerTeam;
        Func<IEnumerable<ICombatUnit>> allUnits;
        readonly List<CanvasGroup> dimTargets = new List<CanvasGroup>();

        /// <summary>true when a staged engagement starts (input locked, overhead labels should hide), false when it ends.</summary>
        public Action<bool> CinematicChanged;

        public void Bind(CombatSystem combat, OperatorTeam? playerTeam, Func<IEnumerable<ICombatUnit>> units)
        {
            End();
            Unbind();
            this.combat = combat; this.playerTeam = playerTeam; allUnits = units;
            if (profile == null) profile = ScriptableObject.CreateInstance<BattleCamProfile>();
            combat.Director = this;
            combat.OnShotStart += HandleShotStart;
            combat.OnShot += HandleShot;
            combat.OnKill += HandleKill;
            EnsureUi();
            EnsureVolume();
            RefreshOptionButton();
        }

        public void Unbind()
        {
            if (combat != null)
            {
                combat.OnShotStart -= HandleShotStart;
                combat.OnShot -= HandleShot;
                combat.OnKill -= HandleKill;
                if (ReferenceEquals(combat.Director, this)) combat.Director = null;
            }
            combat = null; allUnits = null; playerTeam = null;
            RefreshOptionButton();
        }

        /// <summary>A HUD root dimmed to profile.hudAlpha during a full cinematic (gets a CanvasGroup if it has none).</summary>
        public void AddDimTarget(GameObject go)
        {
            if (go == null) return;
            var g = go.GetComponent<CanvasGroup>();
            if (g == null) g = go.AddComponent<CanvasGroup>();
            if (!dimTargets.Contains(g)) dimTargets.Add(g);
        }

        public void ClearDimTargets()
        {
            dimTargets.Clear();
            if (optionGroup != null) dimTargets.Add(optionGroup);
        }

        /// <summary>Drops any running engagement staging (time scale, camera override, HUD dim, input lock).
        /// The match-end cleanup: safe to call when nothing is running.</summary>
        public void Abort() => End();

        void OnDisable() => End();
        void OnDestroy() { End(); Unbind(); }

        // ================================================================
        //  ICombatDirector
        // ================================================================
        enum Kind { None, Instant, Short, Full }

        Kind kind;
        bool skipping, degraded;
        int beginFrame, lastPumpFrame;
        float lastPump;
        float savedTimeScale = 1f;
        bool frozen;
        float slowHsUntil, slowKillUntil, holdUntil;

        ICombatUnit shooter, target;
        BattlefieldCamera bcam;
        Func<CamPose> basePose;
        // What the current shot is about (vignette centre): null = the shooter/target pair, else one unit's point.
        Func<Vector3> focus, baseFocus;
        bool hitCutDone, closeupActive;
        float closeupEnd;

        EventSystem savedEs; bool savedEsEnabled;
        float[] savedAlphas;
        bool overheadHidden;

        public bool Directing => kind != Kind.None;
        BattleCamProfile P => profile;

        // Battle stage: built (hidden) when a full engagement begins, shown after the opening shot's zoom + fade.
        enum StagePhase { None, Zoom, FadeOut, FadeIn, Live }
        BattleStage stage;
        StagePhase stagePhase;
        float stagePhaseT0;
        readonly List<ShotEvent> stageReplay = new List<ShotEvent>();
        ICombatUnit stageDeadPending;
        bool StageShown => stage != null && (stagePhase == StagePhase.FadeIn || stagePhase == StagePhase.Live);
        static BattleDirector stageOwner;

        /// <summary>Where `u`'s overhead label is while the battle stage is on screen (null otherwise) — for the
        /// world-anchored popups (headshot badge) so they follow the stage copy instead of the unit on the map.</summary>
        public static Vector3? StageLabel(ICombatUnit u)
            => stageOwner != null && stageOwner.StageShown ? stageOwner.stage.LabelOf(u) : null;

        /// <summary>True from the opening shot of a staged engagement on: headshots are then shown by the stage when the
        /// round lands (through <see cref="HeadshotUi"/>), not when the roll happens.</summary>
        public static bool StageHandlesHeadshots
            => stageOwner != null && stageOwner.stage != null && stageOwner.stagePhase != StagePhase.None;

        /// <summary>Plays the headshot UI: unit, label point, head point, damage (set by GameplayUIBridge).</summary>
        public static Action<ICombatUnit, Vector3, Vector3, int> HeadshotUi;

        bool IsPlayer(ICombatUnit u) => u != null && !u.IsHostage && playerTeam.HasValue && u.Team == playerTeam.Value;

        /// <summary>A beat played after a staged exchange, before the camera leaves the stage (the experience it gave). Null: none.</summary>
        public System.Func<ICombatUnit, ICombatUnit, IEnumerator> AfterExchange;

        public IEnumerator Direct(ICombatUnit first, ICombatUnit second, IEnumerator engagement)
        {
            // the combat system's hint for this engagement (read once)
            bool mutual = combat != null && combat.NextMutual;
            if (combat != null) combat.NextMutual = false;
            // an encounter's first strike (a sure hit) that kills: the fight never starts — no battle stage, it plays on the map
            noStage = false;
            bool sure = combat != null && combat.ExchangeSureHit;
            if (combat != null) combat.ExchangeSureHit = false;
            if (sure && combat.Map != null)
            {
                CombatSystem.ForecastExchange(combat.Map, first, second, true, out _, out _, out _, out int secondAfter, true);
                noStage = secondAfter <= 0;
            }
            if (engagement == null || Directing || !isActiveAndEnabled) return engagement;
            // AI-vs-AI (no player unit on either side): no staging, no waits.
            if (!IsPlayer(first) && !IsPlayer(second)) return Run(Kind.Instant, first, second, engagement);
            switch (Mode)
            {
                case BattleCinematicMode.Off: return engagement;   // exactly the old in-place exchange
                case BattleCinematicMode.Short: return Run(Kind.Short, first, second, engagement);
                default:
                    mutualOpen = mutual;
                    return Run(Kind.Full, first, second, engagement);
            }
        }

        // A mutual encounter: no opening shot on the map — the camera closes in on the player's unit and the whole
        // fight plays on the battle stage.
        bool mutualOpen;
        // the opening first strike is going to kill: no battle stage for this engagement
        bool noStage;

        /// <summary>True while the battle stage is on screen, false when it goes (GameplayUIBridge: the #combat panel shows
        /// only the two HP bars on the stage).</summary>
        public static Action<bool> StageUi;

        /// <summary>
        /// Pumps the engagement (flattening nested coroutines so an exception anywhere inside is caught here),
        /// then the exit blend; the finally block puts time scale, camera, Volume, letterbox, HUD, walls and
        /// input back no matter how it ends. The exception, if any, is rethrown afterwards.
        /// </summary>
        IEnumerator Run(Kind k, ICombatUnit first, ICombatUnit second, IEnumerator body)
        {
            if (Directing) { yield return body; yield break; }   // nested: the outer engagement already paces it

            var stack = new Stack<IEnumerator>();
            Exception error = null;
            // The engagement's own UnityEngine.Random stream. Something outside this project's scripts draws
            // from the global stream every frame, so without this the rolls would depend on how many frames
            // the (skippable) waits last. Each step runs on this state and hands the global one back after.
            var engagementRng = UnityEngine.Random.state;
            try
            {
                Begin(k, first, second);
                stack.Push(ExitPhase());
                // after the exchange, still on the stage: the experience it gave (MatchBootstrap → ExpGainPopup); not for AI-only ones
                if (AfterExchange != null && k != Kind.Instant) stack.Push(AfterExchange(first, second));
                stack.Push(body);
                while (stack.Count > 0)
                {
                    lastPump = Time.unscaledTime; lastPumpFrame = Time.frameCount;
                    var top = stack.Peek();
                    bool moved = false; object cur = null;
                    var outsideRng = UnityEngine.Random.state;
                    UnityEngine.Random.state = engagementRng;
                    try
                    {
                        moved = top.MoveNext();
                        if (moved) cur = top.Current;
                    }
                    catch (Exception e) { error = e; }
                    finally
                    {
                        engagementRng = UnityEngine.Random.state;
                        UnityEngine.Random.state = outsideRng;
                    }
                    if (error != null) break;
                    if (!moved) { stack.Pop(); continue; }
                    if (cur is IEnumerator nested) { stack.Push(nested); continue; }
                    yield return cur;
                }
            }
            finally
            {
                while (stack.Count > 0)
                {
                    var e = stack.Pop();
                    try { (e as IDisposable)?.Dispose(); } catch (Exception ex) { Debug.LogException(ex); }
                }
                End();
            }
            if (error != null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(error).Throw();
        }

        /// <summary>The shot pacing's sleep(): unscaled, stretched to cover a running close-up/orbit, ~0 once skipped.</summary>
        public IEnumerator Wait(float seconds)
        {
            float end = Time.unscaledTime + seconds;
            while (Directing && !skipping && (Time.unscaledTime < end || Time.unscaledTime < holdUntil)) yield return null;
        }

        /// <summary>hitStop(): timeScale 0 for `seconds` of real time (layered with any slow motion).</summary>
        public IEnumerator Freeze(float seconds)
        {
            if (!Directing || skipping) yield break;
            frozen = true; ApplyTimeScale();
            try
            {
                float end = Time.unscaledTime + seconds;
                while (Directing && !skipping && Time.unscaledTime < end) yield return null;
            }
            finally
            {
                frozen = false;
                if (Directing) ApplyTimeScale();
            }
        }

        void Begin(Kind k, ICombatUnit first, ICombatUnit second)
        {
            kind = k; degraded = false;
            skipping = k == Kind.Instant;
            beginFrame = lastPumpFrame = Time.frameCount; lastPump = Time.unscaledTime;
            savedTimeScale = Time.timeScale; frozen = false;
            slowHsUntil = slowKillUntil = holdUntil = 0f;
            shooter = first; target = second;
            hitCutDone = false; closeupActive = false; basePose = null;
            focus = baseFocus = null;
            if (k == Kind.Instant) return;

            BattlefieldCamera.InputLocked = true;
            savedEs = EventSystem.current;
            if (savedEs != null) { savedEsEnabled = savedEs.enabled; savedEs.enabled = false; }
            Safe(() => CinematicChanged?.Invoke(true));

            try
            {
                bcam = BattlefieldCamera.Instance;
                if (k == Kind.Full)
                {
                    SetOverhead(false);
                    savedAlphas = new float[dimTargets.Count];
                    for (int i = 0; i < dimTargets.Count; i++) savedAlphas[i] = dimTargets[i] != null ? dimTargets[i].alpha : 1f;
                    EnsureVolume();
                    StartWeight(1f, P.enterSeconds);
                    if (bcam != null) { basePose = TwoShot(first, second); bcam.SetOverride(basePose, P.enterSeconds); }
                    if (bcam != null && P.useBattleStage && !noStage)
                    {
                        try
                        {
                            stage = BattleStage.TryBuild(first, second, second, P.stageFov);
                            if (stage != null)
                            {
                                stageOwner = this;
                                stage.Kick = m => { if (bcam != null && !skipping) bcam.Kick(m); };
                                stage.Kia = p => { if (!skipping) ShowKia(p); };
                                stage.Headshot = (u, label, head, dmg) => { if (!skipping) HeadshotUi?.Invoke(u, label, head, dmg); };
                                stage.IsAlly = IsPlayer;   // the player's critical rounds open on the shooter's face
                            }
                        }
                        catch (Exception e) { Debug.LogException(e); CleanupStage(); }
                        if (stage != null && mutualOpen)
                        {
                            // straight in on our unit: zoom, fade, and every shot (the first included) on the stage — the
                            // waits hold the first shot until the stage is up
                            var ally = IsPlayer(first) ? first : second;
                            float now = Time.unscaledTime;
                            stagePhase = StagePhase.Zoom; stagePhaseT0 = now;
                            basePose = ZoomOn(ally);
                            focus = baseFocus = () => Torso(ally);
                            bcam.SetOverride(basePose, P.stageZoomSeconds);
                            holdUntil = now + P.stageZoomHold + P.stageFadeOutSeconds + P.stageFadeInSeconds + .1f;
                        }
                    }
                    mutualOpen = false;
                }
                else if (bcam != null)
                {
                    var cam = bcam;
                    bcam.SetOverride(() => { var g = cam.GamePose; g.fov = P.shortZoomFov; return g; }, P.enterSeconds);
                }
            }
            catch (Exception e)
            {
                // Presentation failed to start: log it, drop the staging, keep the fight going.
                Debug.LogException(e);
                degraded = true;
                RestoreVisuals();
            }
        }

        IEnumerator ExitPhase()
        {
            if (kind == Kind.Instant) yield break;
            float dur = skipping ? 0f : P.exitSeconds;
            closeupActive = false;
            if (StageShown && !skipping)
            {
                // Fade out on the stage, come back to the map under black, fade in.
                float t0 = Time.unscaledTime;
                while (Directing && !skipping && Time.unscaledTime < t0 + P.stageFadeOutSeconds)
                {
                    SetFade((Time.unscaledTime - t0) / P.stageFadeOutSeconds);
                    yield return null;
                }
                SetFade(1f);
                CleanupStage();
                if (bcam != null) bcam.EndOverride();
                StartWeight(0f, 0f);
                t0 = Time.unscaledTime;
                while (Directing && !skipping && Time.unscaledTime < t0 + P.stageFadeInSeconds)
                {
                    SetFade(1f - (Time.unscaledTime - t0) / P.stageFadeInSeconds);
                    yield return null;
                }
                SetFade(0f);
                yield break;
            }
            CleanupStage();
            SetFade(0f);
            if (bcam != null && bcam.IsOverridden) bcam.ReleaseOverride(dur);
            if (kind == Kind.Full && !degraded) StartWeight(0f, dur);
            float end = Time.unscaledTime + dur;
            while (Directing && !skipping && Time.unscaledTime < end) yield return null;
        }

        /// <summary>Restores everything the engagement changed. Idempotent; each step isolated so one failure can't skip the rest.</summary>
        void End()
        {
            if (kind == Kind.None) return;
            var k = kind;
            kind = Kind.None;
            frozen = false;
            Safe(() => Time.timeScale = savedTimeScale);
            if (k != Kind.Instant)
            {
                RestoreVisuals();
                Safe(() => BattlefieldCamera.InputLocked = false);
                Safe(() => { if (savedEs != null) savedEs.enabled = savedEsEnabled; });
                savedEs = null;
                Safe(() => CinematicChanged?.Invoke(false));
            }
            shooter = target = null; basePose = null; bcam = null;
            focus = baseFocus = null;
            skipping = false; closeupActive = false;
        }

        void RestoreVisuals()
        {
            Safe(() => { if (bcam != null) bcam.EndOverride(); });
            weightDur = 0f; weightTo = 0f;
            Safe(() => ApplyWeight(0f));
            savedAlphas = null;
            Safe(() => { if (volume != null) volume.weight = 0f; });
            Safe(MapWallSync.ClearOccluderFade);
            occluders.Clear();
            Safe(() => SetOverhead(true));
            Safe(HideKia);
            Safe(CleanupStage);
            Safe(() => SetFade(0f));
        }

        void CleanupStage()
        {
            if (stagePhase == StagePhase.FadeIn || stagePhase == StagePhase.Live) Safe(() => StageUi?.Invoke(false));
            stagePhase = StagePhase.None;
            stageReplay.Clear();
            stageDeadPending = null;
            if (stageOwner == this) stageOwner = null;
            if (stage != null) Destroy(stage.gameObject);
            stage = null;
        }

        static void Safe(Action a)
        {
            try { a(); } catch (Exception e) { Debug.LogException(e); }
        }

        // ================================================================
        //  Frame loop: skip input, time scale, cinematic weight, close-up timeout, watchdog
        // ================================================================
        void Update()
        {
            if (kind == Kind.None) return;
            // Watchdog: the coroutine that owns the engagement was stopped/destroyed (finally never runs).
            // Needs both real time and rendered frames, so an editor pause or an unfocused window (no
            // frames, unscaled time still running) doesn't trip it.
            if (Time.unscaledTime - lastPump > 5f && Time.frameCount - lastPumpFrame > 60)
            {
                Debug.LogWarning("[BattleDirector] Engagement stopped advancing — restoring camera/time/input.");
                End();
                return;
            }
            if (kind != Kind.Instant && !skipping && Time.frameCount > beginFrame && SkipPressed()) Skip();
            ApplyTimeScale();
            TickWeight();
            if (stage != null && stagePhase != StagePhase.None && !skipping) Safe(TickStage);

            if (closeupActive && Time.unscaledTime >= closeupEnd)
            {
                closeupActive = false;
                // A killing headshot stays on the close-up; the death orbit starts from it.
                bool killed = target != null && target.HP <= 0;
                if (!killed && bcam != null && basePose != null && kind == Kind.Full)
                {
                    bcam.SetOverride(basePose, P.hitCutSeconds);
                    focus = baseFocus;
                }
            }
        }

        static bool SkipPressed()
        {
            var kb = Keyboard.current;
            if (kb != null && (kb.spaceKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame)) return true;
            var m = Mouse.current;
            return m != null && (m.leftButton.wasPressedThisFrame || m.rightButton.wasPressedThisFrame);
        }

        void Skip()
        {
            skipping = true;
            holdUntil = slowHsUntil = slowKillUntil = 0f;
            frozen = false;
            closeupActive = false;
            // A skip mid-transition leaves the stage as it is (shown or not) until the exit drops it; only the fade goes.
            if (stagePhase == StagePhase.Zoom || stagePhase == StagePhase.FadeOut) CleanupStage();
            SetFade(0f);
            if (bcam != null) bcam.CompleteBlend();
            weightDur = 0f;
            ApplyTimeScale();
            TickWeight();
        }

        void ApplyTimeScale()
        {
            if (kind == Kind.None || kind == Kind.Instant) return;
            float s = savedTimeScale;
            if (kind == Kind.Full && !skipping && !degraded)
            {
                float now = Time.unscaledTime;
                if (now < slowHsUntil) s = Mathf.Min(s, savedTimeScale * P.headshotSlowmoScale);
                if (now < slowKillUntil) s = Mathf.Min(s, savedTimeScale * P.deathSlowmoScale);
            }
            if (frozen) s = 0f;
            Time.timeScale = s;
        }

        // Cinematic weight 0..1: letterbox, battle Volume and HUD dimming all follow it.
        float weight, weightFrom, weightTo, weightT0, weightDur;

        void StartWeight(float to, float seconds)
        {
            weightFrom = weight; weightTo = to; weightT0 = Time.unscaledTime; weightDur = seconds;
            TickWeight();
        }

        void TickWeight()
        {
            if (kind != Kind.Full || degraded) return;
            float p = weightDur <= 0f ? 1f : Mathf.Clamp01((Time.unscaledTime - weightT0) / weightDur);
            float e = p < .5f ? 2f * p * p : 1f - Mathf.Pow(-2f * p + 2f, 2f) / 2f;
            ApplyWeight(Mathf.Lerp(weightFrom, weightTo, e));
        }

        LetterboxBars letterbox;

        void ApplyWeight(float w)
        {
            weight = w;
            if (letterbox == null) letterbox = FindAnyObjectByType<LetterboxBars>(FindObjectsInactive.Include);
            if (letterbox != null)
            {
                if (w <= 0f) letterbox.ClearOverride();
                else letterbox.SetOverride(w, P.letterboxHeight);
            }
            if (volume != null) volume.weight = w;
            if (savedAlphas != null)
                for (int i = 0; i < dimTargets.Count && i < savedAlphas.Length; i++)
                    // on the battle stage the HUD goes entirely (only the #combat HP bars stay)
                    if (dimTargets[i] != null) dimTargets[i].alpha = Mathf.Lerp(savedAlphas[i], StageShown ? 0f : P.hudAlpha, w);
        }

        void SetOverhead(bool on)
        {
            if (on == !overheadHidden) return;
            overheadHidden = !on;
            if (allUnits == null) return;
            foreach (var u in allUnits())
                if (HasView(u)) u.View.SetOverheadVisible(on);
        }

        // ================================================================
        //  Combat events → cuts
        // ================================================================
        void HandleShotStart(ICombatUnit a, ICombatUnit t)
        {
            if (kind == Kind.None || kind == Kind.Instant) return;
            if (a == shooter && t == target) return;   // the opening volley: framed in Begin
            shooter = a; target = t;
            hitCutDone = false; closeupActive = false;
            if (kind != Kind.Full || degraded || skipping || bcam == null) return;
            if (stage != null) return;   // the stage cuts to the new shooter on its first shot
            // Return fire: reverse angle (the new shooter on the left third), no pull-back.
            Safe(() => { basePose = TwoShot(a, t); focus = baseFocus = null; bcam.SetOverride(basePose, P.counterSeconds); });
        }

        void HandleShot(ShotEvent e)
        {
            if (kind != Kind.Full || degraded || skipping || bcam == null) return;
            if (stage != null) { Safe(() => StageShot(e)); return; }
            Safe(() =>
            {
                bcam.Kick(e.Headshot ? P.headshotShake : P.shotShake);
                bool fewShots = e.Shots < P.manyShotThreshold;
                var hitUnit = e.Target;
                if (e.Headshot)
                {
                    if (fewShots && !hitCutDone) { hitCutDone = true; basePose = HitCut(hitUnit); baseFocus = () => Torso(hitUnit); }
                    focus = () => Head(hitUnit);
                    float now = Time.unscaledTime;
                    closeupActive = true;
                    closeupEnd = now + P.headshotSeconds;
                    holdUntil = Mathf.Max(holdUntil, closeupEnd);
                    slowHsUntil = now + P.headshotSlowmoSeconds;
                    bcam.SetOverride(Closeup(e.Target), P.headshotBlendIn);
                    bcam.ZoomPunch(P.zoomPunchFactor, P.zoomPunchSeconds);
                    ApplyTimeScale();
                }
                else if (e.Damage > 0 && fewShots && !hitCutDone)
                {
                    hitCutDone = true;
                    basePose = HitCut(hitUnit);
                    baseFocus = () => Torso(hitUnit);
                    if (!closeupActive) { bcam.SetOverride(basePose, P.hitCutSeconds); focus = baseFocus; }
                }
            });
        }

        void HandleKill(ICombatUnit victim, ICombatUnit killer)
        {
            if (kind == Kind.None || kind == Kind.Instant) return;
            if (stage != null && kind == Kind.Full && !degraded && !skipping && stagePhase != StagePhase.None)
            {
                Safe(() => StageKill(victim));
                return;
            }
            Safe(() =>
            {
                if (!skipping && !degraded) ShowKia(Label(victim));
                if (kind != Kind.Full || degraded || skipping || bcam == null) return;
                float now = Time.unscaledTime;
                closeupActive = false;
                slowKillUntil = now + P.deathSlowmoSeconds;
                holdUntil = Mathf.Max(holdUntil, now + P.deathOrbitSeconds);
                var start = bcam.ShownPose;
                var pivot = Torso(victim);
                basePose = () => Orbit(start, pivot, now);
                focus = baseFocus = () => pivot;
                bcam.SetOverride(basePose, 0f);
                ApplyTimeScale();
            });
        }

        // ================================================================
        //  Battle stage: opening shot on the map → zoom on the unit shot at → fade → the fight on BattleStage
        // ================================================================
        float ShotHold(ShotEvent e) => e.Shots >= P.manyShotThreshold ? P.stageBurstShotSeconds : P.stageShotSeconds;

        void StageShot(ShotEvent e)
        {
            float now = Time.unscaledTime;
            if (stagePhase == StagePhase.None)
            {
                // The opening shot stays on the map; then zoom on the unit it was aimed at.
                stagePhase = StagePhase.Zoom; stagePhaseT0 = now;
                stageReplay.Add(e);
                bcam.Kick(e.Headshot ? P.headshotShake : P.shotShake);
                var unit = e.Target;
                basePose = ZoomOn(unit);
                focus = baseFocus = () => Torso(unit);
                bcam.SetOverride(basePose, P.stageZoomSeconds);
                if (e.Damage > 0) bcam.ZoomPunch(P.zoomPunchFactor, P.zoomPunchSeconds);
                holdUntil = Mathf.Max(holdUntil, now + P.stageZoomHold + P.stageFadeOutSeconds + P.stageFadeInSeconds + ShotHold(e));
                return;
            }
            if (stagePhase != StagePhase.Live)
            {
                // Still zooming/fading (a burst's later shots): played back once the stage is up.
                stageReplay.Add(e);
                holdUntil = Mathf.Max(holdUntil, now + ShotHold(e));
                return;
            }
            // The stage says how long this shot needs (a volley's first shot carries the shooter's cut).
            holdUntil = Mathf.Max(holdUntil, now + stage.PlayShot(e) + .05f);
        }

        void StageKill(ICombatUnit victim)
        {
            if (stagePhase != StagePhase.Live)
            {
                stageDeadPending = victim;   // played after the replayed shots
                return;
            }
            // K.I.A comes from the stage (stage.Kia) when the fall starts, after the killing round has landed.
            holdUntil = Mathf.Max(holdUntil, Time.unscaledTime + stage.PlayDeath(victim) + .05f);
        }

        void TickStage()
        {
            float now = Time.unscaledTime, t = now - stagePhaseT0;
            switch (stagePhase)
            {
                case StagePhase.Zoom:
                    if (t >= P.stageZoomHold) { stagePhase = StagePhase.FadeOut; stagePhaseT0 = now; }
                    break;
                case StagePhase.FadeOut:
                    SetFade(t / Mathf.Max(.01f, P.stageFadeOutSeconds));
                    if (t >= P.stageFadeOutSeconds) { ShowStage(); stagePhase = StagePhase.FadeIn; stagePhaseT0 = now; }
                    break;
                case StagePhase.FadeIn:
                    SetFade(1f - t / Mathf.Max(.01f, P.stageFadeInSeconds));
                    if (t >= P.stageFadeInSeconds) { SetFade(0f); stagePhase = StagePhase.Live; ReplayPending(); }
                    break;
            }
        }

        void ShowStage()
        {
            HideKia();
            Safe(() => StageUi?.Invoke(true));
            stage.Show(stageReplay.Count > 0 ? stageReplay[0].Attacker : null);
            var s = stage;
            basePose = s.CameraPose;
            focus = baseFocus = null;
            bcam.SetOverride(basePose, 0f);
            MapWallSync.ClearOccluderFade();
            occluders.Clear();
            if (vignette != null) vignette.center.value = new Vector2(.5f, .5f);
        }

        // The shots taken while zooming/fading, played one after another (each as long as the stage says), then a death if one is due.
        void ReplayPending()
        {
            var list = new List<ShotEvent>(stageReplay);
            stageReplay.Clear();
            var dead = stageDeadPending;
            stageDeadPending = null;
            ReplayStep(list, 0, dead);
        }

        void ReplayStep(List<ShotEvent> list, int i, ICombatUnit dead)
        {
            if (stage == null) return;
            float now = Time.unscaledTime;
            if (i < list.Count)
            {
                float d = stage.PlayShot(list[i]);
                holdUntil = Mathf.Max(holdUntil, now + d + .15f);
                stage.After(d, () => ReplayStep(list, i + 1, dead));
                return;
            }
            if (dead != null) holdUntil = Mathf.Max(holdUntil, now + stage.PlayDeath(dead) + .05f);
        }

        /// <summary>Close on the unit shot at, from the side the camera is already on.</summary>
        Func<CamPose> ZoomOn(ICombatUnit t)
        {
            Vector2 T = new Vector2(t.X, t.Y);
            Vector2 dir = GridOf(bcam.ShownPose.position) - T;
            if (dir.sqrMagnitude < 1e-4f) dir = Vector2.down;
            dir.Normalize();
            Vector2 c2 = T + dir * P.stageZoomDistance;
            var c = GridSpace.GridToWorld(c2.x, c2.y, P.stageZoomHeight);
            var look = Torso(t) + Vector3.up * .15f;
            var pose = new CamPose(c, Quaternion.LookRotation(look - c, Vector3.up), P.stageZoomFov);
            return () => pose;
        }

        // ================================================================
        //  Framing
        // ================================================================
        static bool HasView(ICombatUnit u) => u != null && u.View != null && !(u.View is UnityEngine.Object o && o == null);
        static Vector3 Torso(ICombatUnit u) => HasView(u) ? u.View.TorsoPoint : GridSpace.GridToWorld(u.X, u.Y, .5f);
        static Vector3 Head(ICombatUnit u) => HasView(u) ? u.View.HeadPoint : GridSpace.GridToWorld(u.X, u.Y, .8f);
        static Vector3 Label(ICombatUnit u) => HasView(u) ? u.View.LabelPoint : GridSpace.GridToWorld(u.X, u.Y, 1.2f);
        static Vector2 GridOf(Vector3 world) => new Vector2(world.x, -world.z);

        float TanHalfV(float fov) => Mathf.Tan(fov * .5f * Mathf.Deg2Rad);

        /// <summary>Normalised device coords (-1..1) of `p` for a camera at `c` with rotation `q`.</summary>
        Vector2 Ndc(Vector3 c, Quaternion q, float fov, Vector3 p)
        {
            var v = Quaternion.Inverse(q) * (p - c);
            float z = Mathf.Max(v.z, 1e-4f), tv = TanHalfV(fov), th = tv * (bcam != null ? bcam.AspectRatio : 16f / 9f);
            return new Vector2(v.x / (z * th), v.y / (z * tv));
        }

        /// <summary>Yaw/pitch (no roll) that centres the pair a,b on screen.</summary>
        Quaternion AimAtPair(Vector3 c, Vector3 a, Vector3 b, float fov)
        {
            var e = Quaternion.LookRotation((a + b) * .5f - c, Vector3.up).eulerAngles;
            float yaw = e.y, pitch = e.x > 180f ? e.x - 360f : e.x;
            float tv = TanHalfV(fov), th = tv * (bcam != null ? bcam.AspectRatio : 16f / 9f);
            for (int i = 0; i < 8; i++)
            {
                var q = Quaternion.Euler(pitch, yaw, 0f);
                var na = Ndc(c, q, fov, a); var nb = Ndc(c, q, fov, b);
                yaw += Mathf.Atan((na.x + nb.x) * .5f * th) * Mathf.Rad2Deg;
                pitch -= Mathf.Atan((na.y + nb.y) * .5f * tv) * Mathf.Rad2Deg;
            }
            return Quaternion.Euler(pitch, yaw, 0f);
        }

        /// <summary>
        /// Two-shot of shooter s → target t: camera on the side of the line of fire from which s reads on the
        /// left, `dist` from the midpoint at cameraHeight, swung toward the shooter (−1) or the target (+1)
        /// until the pair sits exactly on the thirds (FOV fixed at profile.fov). Six candidates (two swings ×
        /// three distances) are scored by blocked sight lines; walls still in the way are faded in LateUpdate.
        /// </summary>
        Func<CamPose> TwoShot(ICombatUnit s, ICombatUnit t)
        {
            Vector2 S = new Vector2(s.X, s.Y), T = new Vector2(t.X, t.Y);
            Vector2 d = T - S;
            float gap = d.magnitude;
            Vector2 u = gap > 1e-3f ? d / gap : Vector2.right;
            Vector2 n = new Vector2(-u.y, u.x);
            Vector2 M = (S + T) * .5f;
            float L0 = Mathf.Clamp(gap * P.distanceFactor + P.distanceOffset, P.minDistance, P.maxDistance);
            Vector3 sAim = GridSpace.GridToWorld(S.x, S.y, P.frameHeight), tAim = GridSpace.GridToWorld(T.x, T.y, P.frameHeight);

            // Which perpendicular puts the shooter on the left — checked, not assumed.
            var probe = PoseOnCircle(M, n, u, L0, 0f, sAim, tAim);
            if (Ndc(probe.position, probe.rotation, P.fov, sAim).x > Ndc(probe.position, probe.rotation, P.fov, tAim).x) n = -n;

            float targetSep = (1f - 2f * P.shooterScreenX) * 2f;   // in NDC (viewport ⅓ → ⅔)
            CamPose best = probe; float bestScore = float.MaxValue;
            var scales = P.candidateDistanceScales != null && P.candidateDistanceScales.Length > 0 ? P.candidateDistanceScales : new[] { 1f };
            foreach (float k in scales)
                foreach (int swing in new[] { -1, 1 })
                {
                    float L = Mathf.Clamp(L0 * k, P.minDistance, P.maxDistance);
                    var pose = SolveThirds(M, n, u, L, swing, sAim, tAim, targetSep);
                    float score = SightScore(pose.position, new[] { Torso(s), Head(s), Torso(t), Head(t) })
                                  + (Mathf.Approximately(k, 1f) ? 0f : .1f) + (swing > 0 ? .05f : 0f);
                    if (score < bestScore) { bestScore = score; best = pose; }
                }
            var chosen = best;
            return () => chosen;
        }

        CamPose PoseOnCircle(Vector2 M, Vector2 n, Vector2 u, float L, float alphaRad, Vector3 a, Vector3 b)
        {
            Vector2 c2 = M + L * (n * Mathf.Cos(alphaRad) + u * Mathf.Sin(alphaRad));
            var c = GridSpace.GridToWorld(c2.x, c2.y, P.cameraHeight);
            return new CamPose(c, AimAtPair(c, a, b, P.fov), P.fov);
        }

        CamPose SolveThirds(Vector2 M, Vector2 n, Vector2 u, float L, int swing, Vector3 a, Vector3 b, float targetSep)
        {
            float Sep(CamPose p) => Ndc(p.position, p.rotation, P.fov, b).x - Ndc(p.position, p.rotation, P.fov, a).x;
            var at0 = PoseOnCircle(M, n, u, L, 0f, a, b);
            if (Sep(at0) <= targetSep) return at0;   // pair already narrower than the thirds from straight side-on
            float lo = 0f, hi = 85f * Mathf.Deg2Rad;
            for (int i = 0; i < 24; i++)
            {
                float mid = (lo + hi) * .5f;
                if (Sep(PoseOnCircle(M, n, u, L, swing * mid, a, b)) > targetSep) lo = mid; else hi = mid;
            }
            return PoseOnCircle(M, n, u, L, swing * (lo + hi) * .5f, a, b);
        }

        /// <summary>Cut to the hit unit: same side as the current shot, hitCutDistance away, unit centred.</summary>
        Func<CamPose> HitCut(ICombatUnit t)
        {
            Vector2 T = new Vector2(t.X, t.Y);
            Vector2 dir = GridOf(bcam.ShownPose.position) - T;
            if (dir.sqrMagnitude < 1e-4f) dir = Vector2.down;
            dir.Normalize();
            Vector2 c2 = T + dir * P.hitCutDistance;
            var c = GridSpace.GridToWorld(c2.x, c2.y, P.hitCutHeight);
            var look = GridSpace.GridToWorld(T.x, T.y, P.frameHeight);
            var pose = new CamPose(c, Quaternion.LookRotation(look - c, Vector3.up), P.fov);
            return () => pose;
        }

        /// <summary>Headshot close-up: headshotDistance in front of the head anchor (its facing), FOV 22;
        /// swung up to ±60° if the straight-on spot has no clear view of the head. Aimed a little above the
        /// head so it sits just below centre and the headshot badge (over the label anchor) stays in frame.</summary>
        Func<CamPose> Closeup(ICombatUnit t)
        {
            var head = Head(t);
            var aim = head + Vector3.up * .15f;
            Vector2 H = GridOf(head);
            CamPose best = default; float bestScore = float.MaxValue;
            foreach (float offDeg in new[] { 0f, 30f, -30f, 60f, -60f })
            {
                float a = t.Facing + offDeg * Mathf.Deg2Rad;
                Vector2 c2 = H + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * P.headshotDistance;
                var c = GridSpace.GridToWorld(c2.x, c2.y, head.y + .08f);
                float score = SightScore(c, new[] { head }) + Mathf.Abs(offDeg) / 180f;
                if (score < bestScore) { bestScore = score; best = new CamPose(c, Quaternion.LookRotation(aim - c, Vector3.up), P.headshotFov); }
            }
            var chosen = best;
            return () => chosen;
        }

        /// <summary>Death: orbit `deathOrbitDegrees` around the body (ease-out), keeping the framing of `start`.</summary>
        CamPose Orbit(CamPose start, Vector3 pivot, float t0)
        {
            float p = skipping ? 1f : Mathf.Clamp01((Time.unscaledTime - t0) / Mathf.Max(.01f, P.deathOrbitSeconds));
            float e = 1f - Mathf.Pow(1f - p, 3f);
            var rot = Quaternion.AngleAxis(P.deathOrbitDegrees * e, Vector3.up);
            return new CamPose(pivot + rot * (start.position - pivot), rot * start.rotation, start.fov);
        }

        // ---- line of sight ----
        static readonly RaycastHit[] hitBuf = new RaycastHit[32];
        readonly HashSet<Transform> rayBlocks = new HashSet<Transform>();

        /// <summary>Blocked sight lines from `c` to the points (1 each), +10 if `c` is inside a map block.</summary>
        float SightScore(Vector3 c, Vector3[] points)
        {
            float score = 0f;
            foreach (var p in points)
            {
                rayBlocks.Clear();
                CollectBlocks(c, p, rayBlocks);
                if (rayBlocks.Count > 0) score += 1f + rayBlocks.Count * .01f;
            }
            foreach (var col in Physics.OverlapSphere(c, .3f, ~0, QueryTriggerInteraction.Ignore))
                if (MapWallSync.BlockOf(col.transform) != null) { score += 10f; break; }
            return score;
        }

        static void CollectBlocks(Vector3 from, Vector3 to, HashSet<Transform> into)
        {
            var dir = to - from;
            float len = dir.magnitude;
            if (len < 1e-3f) return;
            int n = Physics.RaycastNonAlloc(from, dir / len, hitBuf, len, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                var b = MapWallSync.BlockOf(hitBuf[i].collider.transform);
                if (b != null) into.Add(b);
            }
        }

        // ================================================================
        //  LateUpdate: wall fade, vignette centre, K.I.A popup (after the camera has been posed)
        // ================================================================
        readonly HashSet<Transform> occluders = new HashSet<Transform>();
        readonly HashSet<Transform> occludersNow = new HashSet<Transform>();

        void LateUpdate()
        {
            if (kind == Kind.Full && !degraded && bcam != null && bcam.IsOverridden && !StageShown)
            {
                Safe(UpdateOccluders);
                Safe(UpdateVignetteCentre);
            }
            TickKia();
        }

        void UpdateOccluders()
        {
            occludersNow.Clear();
            var c = bcam.transform.position;
            foreach (var u in new[] { shooter, target })
            {
                if (u == null) continue;
                CollectBlocks(c, Torso(u), occludersNow);
                CollectBlocks(c, Head(u), occludersNow);
            }
            if (occludersNow.SetEquals(occluders)) return;
            occluders.Clear();
            occluders.UnionWith(occludersNow);
            MapWallSync.SetOccluderFade(occluders, P.occluderAlpha);
        }

        void UpdateVignetteCentre()
        {
            if (vignette == null || shooter == null || target == null) return;
            var cam = bcam.GetComponent<Camera>();
            Vector3 c;
            if (focus != null) c = cam.WorldToViewportPoint(focus());
            else
            {
                var a = cam.WorldToViewportPoint(Torso(shooter));
                var b = cam.WorldToViewportPoint(Torso(target));
                if (a.z <= 0f || b.z <= 0f) return;
                c = (a + b) * .5f;
            }
            if (c.z <= 0f) return;
            vignette.center.value = new Vector2(Mathf.Clamp01(c.x), Mathf.Clamp01(c.y));
        }

        // ================================================================
        //  Battle-only Volume (weight 0 outside a cinematic)
        // ================================================================
        Volume volume;
        Vignette vignette;

        void EnsureVolume()
        {
            if (volume != null) return;
            var go = new GameObject("BattleDirector_Volume");
            go.transform.SetParent(transform, false);
            volume = go.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 500f;   // over the scene's Global Volume; under BattlefieldCamera_NoBlur (DoF/blur only)
            volume.weight = 0f;
            var prof = ScriptableObject.CreateInstance<VolumeProfile>();
            prof.name = "BattleDirector_Profile";
            var ca = prof.Add<ColorAdjustments>(false);
            ca.saturation.Override(P.saturation);
            vignette = prof.Add<Vignette>(false);
            vignette.intensity.Override(P.mapDarkness);
            vignette.smoothness.Override(.45f);
            vignette.color.Override(Color.black);
            vignette.center.Override(new Vector2(.5f, .5f));
            volume.sharedProfile = prof;
        }

        // ================================================================
        //  Overlay: K.I.A popup + the 戦闘演出 option button
        // ================================================================
        Canvas uiCanvas;
        RectTransform kiaRoot;
        CanvasGroup kiaGroup;
        Vector3 kiaWorld;
        float kiaT0;
        bool kiaOn;
        RectTransform optionRoot;
        CanvasGroup optionGroup;
        TMP_Text optionText;
        Chip optionChip;
        const float OptionSlant = 10f, OptionH = 28f;

        void EnsureUi()
        {
            if (uiCanvas != null) return;
            var go = new GameObject("BattleDirector_Canvas", typeof(RectTransform));
            go.transform.SetParent(transform, false);
            uiCanvas = go.AddComponent<Canvas>();
            uiCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            uiCanvas.sortingOrder = 6;
            UITheme.Current.ApplyTo(go.AddComponent<CanvasScaler>(), UITheme.CanvasKind.Hud);
            go.AddComponent<GraphicRaycaster>();

            // Full-screen black for the fades into / out of the battle stage (under the K.I.A popup and the option chip).
            var fadeRt = Rect("DecoBattleFade", go.transform, Vector2.zero, Vector2.one, new Vector2(.5f, .5f), Vector2.zero, Vector2.zero);
            fadeImage = Img(fadeRt, new Color(0f, 0f, 0f, 0f));
            fadeImage.raycastTarget = false;
            fadeImage.enabled = false;

            // K.I.A —a foe-red plate with the ink underline, same visual language as the headshot badge.
            kiaRoot = Rect("KIA", go.transform, Vector2.zero, Vector2.zero, new Vector2(.5f, 0f), Vector2.zero, new Vector2(190, 52));
            Img(kiaRoot, UIPalette.Foe);
            var line = Rect("Underline", kiaRoot, Vector2.zero, new Vector2(1, 0), new Vector2(.5f, 0f), Vector2.zero, new Vector2(0, 5));
            Img(line, UIPalette.Ink);
            var t = Text(kiaRoot, "K.I.A", 34, Color.white);
            t.characterSpacing = 14;
            kiaGroup = kiaRoot.gameObject.AddComponent<CanvasGroup>();
            kiaGroup.blocksRaycasts = false; kiaGroup.interactable = false;
            kiaRoot.gameObject.SetActive(false);

            // 戦闘演出 option (フル → 短め → オフ): a Chip (ink, acc band 4 on the left, slant 10, 12px) right above END TURN
            // (right 20, END TURN is 20 from the bottom and 56 high, 8 gap)
            var chip = Chip.Create(go.transform, "BattleCinematicOption", "", KitColor.Ink, KitColor.White, 12f, OptionSlant);
            chip.padX = 14f;
            optionRoot = (RectTransform)chip.transform;
            optionRoot.anchorMin = optionRoot.anchorMax = new Vector2(1f, 0f);
            optionRoot.pivot = new Vector2(1f, 0f);
            optionRoot.anchoredPosition = new Vector2(-20f, 20f + KitEndButton.H + 8f);
            var bg = chip.box;
            var bar = Rect("DecoAccent", optionRoot, new Vector2(0, 0), new Vector2(0, 1), new Vector2(0, .5f), Vector2.zero, new Vector2(4f + OptionSlant, 0));
            var barImg = Img(bar, UIPalette.Accent);
            barImg.raycastTarget = false;
            bar.gameObject.AddComponent<SlantedGraphic>().Set(SlantedGraphic.Size.Custom, SlantedGraphic.Direction.Right, OptionSlant);
            optionChip = chip;
            optionText = chip.label;
            optionText.raycastTarget = false;
            var btn = optionRoot.gameObject.AddComponent<Button>();
            btn.targetGraphic = bg;
            btn.onClick.AddListener(CycleMode);
            optionGroup = optionRoot.gameObject.AddComponent<CanvasGroup>();
            if (!dimTargets.Contains(optionGroup)) dimTargets.Add(optionGroup);
        }

        void CycleMode()
        {
            if (Directing) return;
            Mode = (BattleCinematicMode)(((int)Mode + 1) % 3);
            RefreshOptionButton();
        }

        /// <summary>The 戦闘演出 chip off until the next player match binds it again (a cleared match, an intro playing).</summary>
        public void HideOption() { if (optionRoot != null) optionRoot.gameObject.SetActive(false); }

        void RefreshOptionButton()
        {
            if (optionRoot == null) return;
            optionRoot.gameObject.SetActive(playerTeam.HasValue);
            optionChip.SetText($"戦闘演出 · <color=#FFC81E><b>{ModeLabel(Mode)}</b></color>");
            optionRoot.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, OptionH);
        }

        Image fadeImage;

        void SetFade(float a)
        {
            if (fadeImage == null) return;
            a = Mathf.Clamp01(a);
            fadeImage.color = new Color(0f, 0f, 0f, a);
            fadeImage.enabled = a > .001f;
        }

        void ShowKia(Vector3 world)
        {
            if (kiaRoot == null) return;
            kiaWorld = world; kiaT0 = Time.unscaledTime; kiaOn = true;
            kiaRoot.gameObject.SetActive(true);
            TickKia();
        }

        void HideKia()
        {
            kiaOn = false;
            if (kiaRoot != null) kiaRoot.gameObject.SetActive(false);
        }

        void TickKia()
        {
            if (!kiaOn) return;
            float t = Time.unscaledTime - kiaT0, dur = P != null ? P.kiaSeconds : 1.3f;
            if (t >= dur) { HideKia(); return; }
            var cam = Camera.main;
            if (cam == null) return;
            var s = cam.WorldToScreenPoint(kiaWorld);
            if (s.z <= 0f) { kiaGroup.alpha = 0f; return; }
            float sf = uiCanvas.scaleFactor > 0f ? uiCanvas.scaleFactor : 1f;
            kiaRoot.anchoredPosition = new Vector2(s.x / sf, s.y / sf + 8f);
            // Stamp in (1.8× → 1 over .15s), hold, fade over the last .3s.
            float k = Mathf.Clamp01(t / .15f);
            kiaRoot.localScale = Vector3.one * Mathf.Lerp(1.8f, 1f, 1f - (1f - k) * (1f - k));
            kiaRoot.localRotation = Quaternion.Euler(0, 0, Mathf.Lerp(-8f, -3f, k));
            kiaGroup.alpha = Mathf.Min(Mathf.Clamp01(t / .08f), Mathf.Clamp01((dur - t) / .3f));
        }

        static RectTransform Rect(string name, Transform parent, Vector2 aMin, Vector2 aMax, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            var rt = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
            rt.SetParent(parent, false);
            rt.anchorMin = aMin; rt.anchorMax = aMax; rt.pivot = pivot;
            rt.anchoredPosition = pos; rt.sizeDelta = size;
            return rt;
        }

        static Image Img(RectTransform rt, Color c)
        {
            var img = rt.gameObject.AddComponent<Image>();
            img.color = c;
            return img;
        }

        static TMP_Text Text(RectTransform parent, string s, float size, Color c)
        {
            var rt = Rect("Text", parent, Vector2.zero, Vector2.one, new Vector2(.5f, .5f), Vector2.zero, Vector2.zero);
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            t.text = s; t.fontSize = size; t.color = c;
            UIFonts.Auto(t, true);
            t.alignment = TextAlignmentOptions.Center;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.raycastTarget = false;
            return t;
        }
    }
}
