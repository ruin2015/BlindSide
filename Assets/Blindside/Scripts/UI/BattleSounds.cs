// The battle's combat sounds, from CombatSystem's events (SoundBank; silent until their files exist):
//   every round   the shooter's gun: its own file first — gun_<the letters and digits of its name's model, lower case> (K-417 →
//                 gun_k417, M-870 / M870 → gun_m870, Vector → gun_vector …) — else its class's (gun_ar / gun_smg / gun_sg / gun_pst /
//                 gun_dmr); with a fitted suppressor gun_<model>_suppressed, else gun_suppressed. Then where it went: a headshot
//                 (HitHeadshot), a hit (HitBody), a raised shield (HitShield) or a miss (HitMiss); a round that went through a wall
//                 to get there also punches through it (HitWallPierce)
//   a kill        the body falling (Down); a unit of the player's side going down also gets AllyDown
//   a walk        the player's units' footsteps, a step every StepSeconds while they move (MovementSystem.OnStep)
// Timing: the sounds go with what is on screen. On the map that is the moment of the roll (the figure fires and flinches then).
// While a battle stage shows the engagement (BattleDirector.StageHoldsHp) the rounds are played back there later: the stage
// calls Gun when its muzzle flashes, Impact when a round lands and Fall when the body goes down, and the roll stays silent.
// MatchBootstrap binds it to each match's CombatSystem. Presentation only: it reads the events, never the rolls.
using System.Collections.Generic;
using Blindside.Combat;
using Blindside.Data;
using UnityEngine;

namespace Blindside.UI
{
    public static class BattleSounds
    {
        // the mix: a gun is the loudest thing on the field, the hit under it, footsteps far under
        // (the hits a step over the guns: a round that lands is what the player listens for)
        const float GunVol = .8f, HitVol = 1f, HeadVol = 1f, MissVol = .6f, ShieldVol = 1f, PierceVol = .6f, DownVol = .75f;
        const float StepVol = .55f, StepSeconds = .38f;   // (the step files sit 6 dB under the rest)
        /// <summary>The headshot's close-up (the stage's face cut before its critical round): its own sound from the cut on.</summary>
        public const string HeadshotCloseup = "hit_headshot_motion";

        // A gun whose file is a whole burst (one shot = several rounds): the rounds' starts in it, seconds. Its sound plays on the
        // burst's first round only, and the rounds are fired at the file's own spacing (RoundGap) so the flashes land on its reports.
        static readonly Dictionary<string, float[]> bursts = new Dictionary<string, float[]>
        {
            { "gun_m16", new[] { 0f, .072f, .136f } },   // three rounds, ~880 rpm
        };

        /// <summary>The seconds between rounds of `u`'s gun when its sound is a burst (−1: the usual spacing).</summary>
        public static float RoundGap(ICombatUnit u)
        {
            var f = FileOf(u != null ? u.Weapon : null);
            return f != null && bursts.TryGetValue(f, out var o) && o.Length > 1 ? o[1] - o[0] : -1f;
        }

        static OperatorTeam? side;
        static readonly Dictionary<ICombatUnit, int> wallsOf = new Dictionary<ICombatUnit, int>(8);   // the shooter's walls this volley
        static float lastStep = -1f;

        static bool Staged => Blindside.Cinematic.BattleDirector.StageHoldsHp;

        public static void Bind(CombatSystem combat, OperatorTeam? playerTeam)
        {
            if (combat == null) return;
            side = playerTeam;
            wallsOf.Clear();
            CombatSystem.RoundGap = RoundGap;   // (a burst gun's rounds at its sound's spacing on the map too)
            combat.OnShotStart += (a, t) =>
            {
                if (a == null || t == null) return;
                int walls = 0;
                try { walls = combat.TraceLine(a, t).Walls; } catch { }
                wallsOf[a] = walls;
            };
            combat.OnShot += e =>
            {
                // the stage plays them again as it shows them; while the map is still on screen (the opening shot) its gun is heard there too
                if (Staged)
                {
                    if (!Blindside.Cinematic.BattleDirector.StageOnScreen) Gun(e.Attacker, e.Index);
                    return;
                }
                Gun(e.Attacker, e.Index);
                Impact(e, false);
                // our first strike: its first round that lands hits with a heavy sub hit on top of the hit (the map only)
                if (preemptBy != null && e.Attacker == preemptBy && !preemptLanded && e.Hit && !e.ShieldBlocked && Mine(e.Attacker))
                {
                    preemptLanded = true;
                    SoundBank.Play(Snd.Preempt, PreemptVol);
                }
            };
            Blindside.Integration.GameplayUIBridge.VolleyShown -= OnVolley;
            Blindside.Integration.GameplayUIBridge.VolleyShown += OnVolley;
            combat.OnKill += (target, killer) =>
            {
                if (Staged && Blindside.Cinematic.BattleDirector.StageHandlesHeadshots) return;   // the stage's fall (once its first shot is in)
                Fall(target);
            };
        }

        // ---- first strikes ----
        const float PreemptVol = .9f, AmbushVol = .9f;
        static ICombatUnit preemptBy;   // the shooter of the volley now open when it is a first strike
        static bool preemptLanded;

        static bool Mine(ICombatUnit u) => u != null && (side.HasValue ? u.Team == side.Value : u.Team == OperatorTeam.Attack);

        /// <summary>A volley opening (GameplayUIBridge): a first strike by us is marked for its landing round; one by them booms at once
        /// (we have been caught — the map and the battle stage alike).</summary>
        static void OnVolley(ICombatUnit a, ICombatUnit t, bool preempt)
        {
            preemptBy = preempt ? a : null;
            preemptLanded = false;
            if (preempt && a != null && !Mine(a)) SoundBank.Play(Snd.Ambush, AmbushVol);
        }

        /// <summary>The player's units' footsteps while they walk.</summary>
        public static void BindSteps(Blindside.Movement.MovementSystem movement, OperatorTeam? playerTeam)
        {
            if (movement == null) return;
            movement.OnStep += u =>
            {
                if (u == null || !u.Alive) return;
                if (playerTeam.HasValue && u.Team != playerTeam.Value) return;
                Step();
            };
        }

        /// <summary>One footstep (at most one every StepSeconds): the moves' steps, and the tutorial's scripted walks.</summary>
        public static void Step()
        {
            float now = Time.unscaledTime;
            if (lastStep >= 0f && now - lastStep < StepSeconds) return;
            lastStep = now;
            SoundBank.Play(Snd.Footstep, StepVol);
        }

        /// <summary>The shooter's gun going off (the map: at the roll; the stage: with its muzzle flash).</summary>
        public static void Gun(ICombatUnit u, int round = 0)
        {
            var w = u != null ? u.Weapon : null;
            var g = CombatModifiers.GearOf(u);
            bool silenced = g != null && g.silenced;
            string own = FileOf(w);
            // a burst file sounds once a burst: on its first round (the others are in it)
            if (own != null && bursts.TryGetValue(own, out var offs) && round % offs.Length != 0 && SoundBank.Clips(own).Length > 0) return;
            if (own != null && SoundBank.Play(silenced ? own + "_suppressed" : own, GunVol)) return;
            if (silenced && SoundBank.Play(Snd.GunSuppressed, GunVol)) return;
            if (SoundBank.Play(ClassOf(w), GunVol)) return;
            // a gun with no file of its own and no class file: its class's stand-in (AR → AK47, SMG → P90 …)
            SoundBank.Play(ClassStandIn(w), GunVol);
        }

        /// <summary>The file a gun of this class sounds with when it has none of its own (the class's best recording).</summary>
        static string ClassStandIn(WeaponData w)
        {
            switch (w != null ? w.ClassKey : "")
            {
                case "SMG": return "gun_p90";
                case "SG": return "gun_m870";
                case "DMR": return "gun_mx50";
                case "PST": return "gun_p12";
            }
            return "gun_ak47";
        }

        /// <summary>Where the round went, as it lands. `closeup`: the stage's slowed critical round (its own sound when there is one).</summary>
        public static void Impact(ShotEvent e, bool closeup)
        {
            if (e.ShieldBlocked) { SoundBank.Play(Snd.HitShield, ShieldVol); return; }
            if (!e.Hit) { SoundBank.Play(Snd.HitMiss, MissVol); return; }
            if (e.Attacker != null && wallsOf.TryGetValue(e.Attacker, out int walls) && walls > 0) SoundBank.Play(Snd.HitWallPierce, PierceVol);
            SoundBank.Play(Snd.HitBody, HitVol);   // (a headshot is a hit too: the body's sound under its own)
            if (e.Headshot) SoundBank.Play(Snd.HitHeadshot, HeadVol);   // (a close-up's own sound started with its cut: StageCloseup)
        }

        /// <summary>A body going down.</summary>
        public static void Fall(ICombatUnit target)
        {
            SoundBank.Play(Snd.Down, DownVol);
            if (side.HasValue && target != null && target.Team == side.Value && !target.IsHostage) SoundBank.Play(Snd.AllyDown);
        }

        /// <summary>The battle stage's calls: silent once the engagement is skipped (the stage then plays nothing out).</summary>
        public static void StageGun(ICombatUnit u, int round) { if (Staged) Gun(u, round); }
        /// <summary>The headshot close-up opening (the shooter's face): its sound starts with the cut.</summary>
        public static void StageCloseup() { if (Staged) SoundBank.Play(HeadshotCloseup, HeadVol); }
        public static void StageImpact(ShotEvent e, bool closeup) { if (Staged) Impact(e, closeup); }
        public static void StageFall(ICombatUnit u) { if (Staged) Fall(u); }

        static readonly Dictionary<WeaponData, string> files = new Dictionary<WeaponData, string>(16);

        /// <summary>The gun's own file: "gun_" + the model in its name ("K-417 突撃銃" → gun_k417), made once a gun.</summary>
        public static string FileOf(WeaponData w)
        {
            if (w == null) return null;
            if (files.TryGetValue(w, out var f)) return f;
            string name = string.IsNullOrEmpty(w.weaponName) ? w.key : w.weaponName;
            int sp = name.IndexOf(' ');
            if (sp > 0) name = name.Substring(0, sp);
            var sb = new System.Text.StringBuilder("gun_", 16);
            foreach (char c in name) if (c < 128 && char.IsLetterOrDigit(c)) sb.Append(char.ToLowerInvariant(c));
            f = sb.Length > 4 ? sb.ToString() : null;
            files[w] = f;
            return f;
        }

        static Snd ClassOf(WeaponData w)
        {
            switch (w != null ? w.ClassKey : "")
            {
                case "SMG": return Snd.GunSmg;
                case "SG": return Snd.GunSg;
                case "PST": return Snd.GunPst;
                case "DMR": return Snd.GunDmr;
            }
            return Snd.GunAr;
        }
    }
}
