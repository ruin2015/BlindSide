// The game's sound effects by name (Docs/sound_list.md lists every one: where it plays, its length, whether it loops, candidates).
// A sound is a file dropped into Assets/Blindside/Resources/Audio/SFX/ named after its id in snake_case — GunAr → gun_ar.wav (any
// format Unity imports: .wav / .ogg / .mp3). Variations: gun_ar_1.wav, gun_ar_2.wav … (up to 8; one is picked at random each time;
// the plain name counts as one more). Each gun can have its own: gun_<its name's letters and digits> (K-417 → gun_k417, AK47 →
// gun_ak47 … BattleSounds), falling back to its class's (gun_ar …). An id with no file is silent — or, for the old synthesized ones (Sfx / SfxNoise), keeps its
// synthesized sound — so files can be added one by one with no code change.
//   Play(id)          one shot (2D), at the settings' SFX volume × the id's own volume
//   Loop(id, on)      a looping sound on its own source (helicopter, drone, rain …), started / stopped
// Clips are loaded once per id (Resources.Load) and kept. The variation pick uses its own System.Random — never UnityEngine.Random,
// whose stream the combat rolls share.
using System.Collections.Generic;
using UnityEngine;

namespace Blindside.UI
{
    public enum Snd
    {
        // ---- UI (the tactical set: one family of tones) ----
        UiHover, UiConfirm, UiCancel, UiDeny, UiOpen, UiClose, UiTab, UiScroll, UiHold, UiWipe, UiGlitch, UiSave,
        Hologram,   // the view turning into the hologram (the intro's, the deployment's Tab)
        // ---- turn and flow ----
        PhaseAlly, PhaseEnemy, EndTurn, MovePlan, MoveCancel, AimLock, Contact, Preempt, Ambush, ObjectiveFail,   // (Preempt: we strike first; Ambush: they do)
        Victory, Defeat, RankStamp, ExpGain, LevelUp, StatUp, CoopRankUp,
        // ---- the squad's insertion and radio ----
        HeliLoop, HeliFlyby, RadioOn, RadioOff, RadioStatic, Rappel, RappelLand, Footstep, FootstepHub,
        // ---- guns (by weapon class) ----
        GunAr, GunSmg, GunSg, GunPst, GunDmr, GunSuppressed, WeaponSwap,
        // ---- hits ----
        HitBody, HitHeadshot, HitMiss, HitWallPierce, HitReinforced, HitShield, Down, KillConfirm, AllyDown, Revive,
        // ---- walls and explosions ----
        WallBreak, WallBreakReinforced, WallBreakShotgun, Explosion, Hammer, Launcher, ThermiteBurn, BreachPlace, BreachBeep, Reinforce,
        // ---- gadgets and abilities ----
        Throw, GrenadeBounce, Flashbang, FlashRing, Smoke, ClaymorePlace, ClaymoreTrigger,
        DroneDeploy, DroneLoop, DronePing, DroneDestroyed, CameraSwitch, CameraDestroyed,
        Emp, Heartbeat, Heal, ItemUse, ShieldUp, ShieldDown, Gas, FireLoop, Zap,
        // ---- hostage ----
        HostageSecure, Extract,
        // ---- HQ ----
        TextBlip, TextBlipFemale, Choice, Purchase, Equip, Gift, Cook, TimePass,   // (TextBlip: a man's line typing on; …Female: a woman's)
        // ---- ambience (loops) ----
        AmbRain, AmbWind, AmbIndoor, AmbHub,
    }

    public class SoundBank : MonoBehaviour
    {
        public const string Folder = "Audio/SFX/";
        const int MaxVariants = 8;

        static SoundBank inst;
        static readonly Dictionary<int, AudioClip[]> clips = new Dictionary<int, AudioClip[]>(128);
        static readonly Dictionary<int, AudioSource> loops = new Dictionary<int, AudioSource>(8);
        static readonly System.Random pick = new System.Random(7177);
        static string[] names;
        AudioSource src;

        /// <summary>The file name of `id`: its name in snake_case (GunAr → gun_ar).</summary>
        public static string FileName(Snd id)
        {
            if (names == null)
            {
                var all = (Snd[])System.Enum.GetValues(typeof(Snd));
                names = new string[all.Length];
                foreach (var s in all) names[(int)s] = Snake(s.ToString());
            }
            return names[(int)id];
        }

        static string Snake(string n)
        {
            var sb = new System.Text.StringBuilder(n.Length + 4);
            for (int i = 0; i < n.Length; i++)
            {
                char c = n[i];
                if (char.IsUpper(c) && i > 0) sb.Append('_');
                sb.Append(char.ToLowerInvariant(c));
            }
            return sb.ToString();
        }

        /// <summary>The clips found for `id` (empty: none; loaded once).</summary>
        public static AudioClip[] Clips(Snd id)
        {
            int k = (int)id;
            if (clips.TryGetValue(k, out var found)) return found;
            var arr = Load(FileName(id));
            clips[k] = arr;
            return arr;
        }

        static readonly Dictionary<string, AudioClip[]> named = new Dictionary<string, AudioClip[]>(32);

        /// <summary>The clips of a file named in code rather than by an id (a gun's own sound: gun_ak47 …); loaded once.</summary>
        public static AudioClip[] Clips(string file)
        {
            if (string.IsNullOrEmpty(file)) return System.Array.Empty<AudioClip>();
            if (named.TryGetValue(file, out var found)) return found;
            var arr = Load(file);
            named[file] = arr;
            return arr;
        }

        static AudioClip[] Load(string f)
        {
            var list = new List<AudioClip>(2);
            var one = Resources.Load<AudioClip>(Folder + f);
            if (one != null) list.Add(one);
            for (int i = 1; i <= MaxVariants; i++)
            {
                var v = Resources.Load<AudioClip>(Folder + f + "_" + i);
                if (v != null) list.Add(v);
            }
            return list.ToArray();
        }

        /// <summary>Plays the file `file` (no id) once; false when there is none.</summary>
        public static bool Play(string file, float volume = 1f) => PlayFrom(Clips(file), volume);

        // ---- a line typing on: one blip per character shown, in the speaker's voice (a woman's or a man's) ----
        const float BlipGap = .055f, BlipVol = .5f;   // (at 40 characters a second a blip every other character or so: a patter, not a buzz)
        static float lastBlip = -1f;
        static readonly HashSet<string> women = new HashSet<string> { "SERUM", "SILENCE", "ARGUS", "MIASMA" };

        /// <summary>True when `key` (an operator) speaks in a woman's voice.</summary>
        public static bool Female(string key) => !string.IsNullOrEmpty(key) && women.Contains(key);

        /// <summary>The typing blip for `shown` characters of `text` now on screen (was `before`): one blip when a new visible character
        /// came in (not a space or a line break), at most one every BlipGap seconds. `speaker`: the operator talking (null: a man's voice).</summary>
        public static void Blip(string speaker, string text, int before, int shown)
        {
            if (shown <= before || string.IsNullOrEmpty(text) || !Application.isPlaying) return;
            float now = Time.unscaledTime;
            if (lastBlip >= 0f && now - lastBlip < BlipGap) return;
            // (the newest character, read off the string: a line with no markup lines up exactly; markup only shifts the space check)
            int i = Mathf.Min(shown, text.Length) - 1;
            if (i < 0 || char.IsWhiteSpace(text[i])) return;
            lastBlip = now;
            Play(Female(speaker) ? Snd.TextBlipFemale : Snd.TextBlip, BlipVol);
        }

        /// <summary>True when `id` has a file.</summary>
        public static bool Has(Snd id) => Clips(id).Length > 0;

        /// <summary>Plays `id` once; false when it has no file (nothing played).</summary>
        public static bool Play(Snd id, float volume = 1f) => PlayFrom(Clips(id), volume);

        static bool PlayFrom(AudioClip[] c, float volume)
        {
            if (c.Length == 0 || !Application.isPlaying) return false;   // (edit mode — Render Previews — stays silent)
            var clip = c.Length == 1 ? c[0] : c[pick.Next(c.Length)];
            Instance().src.PlayOneShot(clip, volume * Blindside.Settings.GameSettings.SfxVolume);
            return true;
        }

        // A playing loop's volume = its volume (× the SFX setting) × its fade (0…1, heading for its target at its rate per second)
        // × its distance (a followed source: full within `near` metres of the listener, falling off as near / distance beyond).
        static readonly List<int> playing = new List<int>(8);
        static float[] loopVol, loopFade, loopFadeTo, loopRate, loopNear, loopDist;
        static Transform[] loopFollow;
        static Transform listener;
        static float listenerSearch;
        const float FarFloor = .06f, DistSmooth = 6f;

        static void EnsureLoopArrays()
        {
            if (loopVol != null) return;
            int n = System.Enum.GetValues(typeof(Snd)).Length;
            loopVol = new float[n]; loopFade = new float[n]; loopFadeTo = new float[n]; loopRate = new float[n];
            loopNear = new float[n]; loopDist = new float[n]; loopFollow = new Transform[n];
        }

        /// <summary>Starts (`on`) or stops a looping `id` on its own source, over `fade` seconds (0: at once). False when it has no file.</summary>
        public static bool Loop(Snd id, bool on, float volume = 1f, float fade = 0f)
        {
            int k = (int)id;
            EnsureLoopArrays();
            loops.TryGetValue(k, out var s);
            if (!on)
            {
                if (s == null || !s.isPlaying) return s != null;
                if (fade <= 0f) { s.Stop(); playing.Remove(k); return true; }
                FadeTo(k, 0f, fade);
                return true;
            }
            var c = Clips(id);
            if (c.Length == 0 || !Application.isPlaying) return false;
            if (s == null)
            {
                s = Instance().gameObject.AddComponent<AudioSource>();
                s.playOnAwake = false; s.spatialBlend = 0f; s.loop = true;
                loops[k] = s;
            }
            loopVol[k] = volume * Blindside.Settings.GameSettings.SfxVolume;
            if (!(s.isPlaying && s.clip == c[0]))
            {
                s.clip = c[0];
                loopFade[k] = fade > 0f ? 0f : 1f;
                loopDist[k] = -1f;   // (the distance is taken as it is on the first frame, not eased in)
                s.volume = 0f;
                s.Play();
            }
            if (fade > 0f) FadeTo(k, 1f, fade);
            else { loopFade[k] = loopFadeTo[k] = 1f; loopRate[k] = 0f; }
            if (!playing.Contains(k)) playing.Add(k);
            Apply(k, s, 0f);
            return true;
        }

        /// <summary>A loop that sounds from `source`: full within `near` metres of the listener (the camera), quieter as it goes
        /// away (near / distance, never quite silent). Null: no longer followed (full volume).</summary>
        public static void Follow(Snd id, Transform source, float near = 15f)
        {
            EnsureLoopArrays();
            int k = (int)id;
            loopFollow[k] = source;
            loopNear[k] = Mathf.Max(.5f, near);
            loopDist[k] = -1f;
        }

        static void FadeTo(int k, float to, float seconds)
        {
            loopFadeTo[k] = to;
            loopRate[k] = Mathf.Abs(to - loopFade[k]) / Mathf.Max(.01f, seconds);
        }

        static float DistanceGain(int k, float dt)
        {
            var f = loopFollow[k];
            if (f == null) return 1f;
            // the ear is the camera that draws the shot (an AudioListener when there is no main camera; looked up at most twice a second)
            var cam = Camera.main;
            if (cam != null) listener = cam.transform;
            else if (listener == null || !listener.gameObject.activeInHierarchy)
            {
                if (Time.unscaledTime < listenerSearch) return loopDist[k] < 0f ? 1f : loopDist[k];
                listenerSearch = Time.unscaledTime + .5f;
                var l = FindAnyObjectByType<AudioListener>();
                listener = l != null ? l.transform : null;
                if (listener == null) return 1f;
            }
            float d = Vector3.Distance(listener.position, f.position);
            float want = Mathf.Clamp(loopNear[k] / Mathf.Max(d, .01f), FarFloor, 1f);
            // eased, so a camera cut doesn't jump the level
            loopDist[k] = loopDist[k] < 0f || dt <= 0f ? want : Mathf.Lerp(loopDist[k], want, 1f - Mathf.Exp(-DistSmooth * dt));
            return loopDist[k];
        }

        static void Apply(int k, AudioSource s, float dt) => s.volume = loopVol[k] * loopFade[k] * DistanceGain(k, dt);

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            for (int i = playing.Count - 1; i >= 0; i--)
            {
                int k = playing[i];
                if (!loops.TryGetValue(k, out var s) || s == null || !s.isPlaying) { playing.RemoveAt(i); continue; }
                if (loopRate[k] > 0f) loopFade[k] = Mathf.MoveTowards(loopFade[k], loopFadeTo[k], loopRate[k] * dt);
                if (loopFadeTo[k] <= 0f && loopFade[k] <= 0f) { s.Stop(); playing.RemoveAt(i); continue; }
                Apply(k, s, dt);
            }
        }

        /// <summary>Every loop stopped (a scene change, the end of a match).</summary>
        public static void StopLoops()
        {
            foreach (var s in loops.Values) if (s != null) s.Stop();
            playing.Clear();
        }

        static SoundBank Instance()
        {
            if (inst != null) return inst;
            var go = new GameObject("SoundBank");
            DontDestroyOnLoad(go);
            inst = go.AddComponent<SoundBank>();
            inst.src = go.AddComponent<AudioSource>();
            inst.src.playOnAwake = false;
            inst.src.spatialBlend = 0f;
            return inst;
        }
    }
}
