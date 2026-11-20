// Background music: a 2D AudioSource that outlives scene loads (DontDestroyOnLoad), with fades on the unscaled clock (hit-stop
// and pauses don't touch it). SceneEntry asks for each scene's track: the title plays TitleTrack (Resources/Music/TacticalBlue, "Tactical
// Blue"); every other scene fades it out (1 s) — the battle then starts BattleTrack itself once the placing is done (MatchBootstrap). The same clip asked for again keeps playing (no restart). Volume: Base × the master
// volume (AudioListener). A track may come in two parts (PlayThen): its body once, then a tail looped for as long as it lasts — the second
// source is scheduled on the audio clock to start the sample the body ends, so there is no gap. Nothing is allocated per frame.
using UnityEngine;

namespace Blindside.UI
{
    public class Music : MonoBehaviour
    {
        public const string TitleTrack = "Music/TacticalBlue";
        /// <summary>The battle's track ("Tactical Grid"): from the first attack phase, once the deployment / preparation is done
        /// (MatchBootstrap); not in the tutorial.</summary>
        public const string BattleTrack = "Music/TacticalGrid";
        /// <summary>The operation's insertion ("Night Operation Briefing", 0:48–1:22) from the intro's start, then its held last chord
        /// (IntroTail, 1:22 on, looped) through the deployment — until the battle's track takes over (or, with none, the first attack phase
        /// fades it: MatchBootstrap).</summary>
        public const string IntroTrack = "Music/IntroBriefing", IntroTail = "Music/IntroBriefingTail";
        public const float Base = .7f, FadeIn = 1.5f, FadeOut = 1f;

        static Music inst;
        AudioSource src, tailSrc;
        float level, target, speed;
        AudioClip next, nextTail;

        bool Playing => src.isPlaying || tailSrc.isPlaying;

        static Music Instance()
        {
            if (inst != null) return inst;
            var go = new GameObject("Music");
            DontDestroyOnLoad(go);
            inst = go.AddComponent<Music>();
            inst.src = NewSource(go);
            inst.tailSrc = NewSource(go);
            return inst;
        }

        static AudioSource NewSource(GameObject go)
        {
            var s = go.AddComponent<AudioSource>();
            s.loop = true; s.playOnAwake = false; s.spatialBlend = 0f;
            s.ignoreListenerPause = true;
            s.volume = 0f;
            return s;
        }

        /// <summary>Plays the clip at `resource` (Resources path) looped, fading in; the one playing fades out first.</summary>
        public static void Play(string resource) => PlayThen(resource, null);

        /// <summary>Plays `resource` once, then `loopResource` looped seamlessly after it (null: `resource` itself looped).</summary>
        public static void PlayThen(string resource, string loopResource)
        {
            if (!Application.isPlaying) return;
            var clip = Resources.Load<AudioClip>(resource);
            if (clip == null) { Debug.LogWarning("[Music] missing " + resource); return; }
            var tail = string.IsNullOrEmpty(loopResource) ? null : Resources.Load<AudioClip>(loopResource);
            var m = Instance();
            if (m.src.clip == clip && m.Playing && m.next == null) { m.target = 1f; m.speed = 1f / FadeIn; return; }
            if (m.Playing && m.level > .01f) { m.next = clip; m.nextTail = tail; m.target = 0f; m.speed = 1f / FadeOut; return; }
            m.Begin(clip, tail);
        }

        /// <summary>Fades the music out (it stops when silent).</summary>
        public static void Stop()
        {
            if (inst == null) return;
            inst.next = null; inst.nextTail = null; inst.target = 0f; inst.speed = 1f / FadeOut;
        }

        void Begin(AudioClip clip, AudioClip tail)
        {
            next = null; nextTail = null;
            src.Stop(); tailSrc.Stop();
            src.clip = clip;
            src.loop = tail == null;
            level = 0f; target = 1f; speed = 1f / FadeIn;
            src.volume = 0f; tailSrc.volume = 0f;
            if (tail == null) { src.Play(); return; }
            // the body, then the tail from the very sample it ends on
            double at = AudioSettings.dspTime + .05;
            src.PlayScheduled(at);
            tailSrc.clip = tail;
            tailSrc.loop = true;
            tailSrc.PlayScheduled(at + (double)clip.samples / clip.frequency);
        }

        void Update()
        {
            if (src == null) return;
            if (level != target)
            {
                level = Mathf.MoveTowards(level, target, speed * Time.unscaledDeltaTime);
                src.volume = tailSrc.volume = Base * level;
            }
            if (level <= 0f && target <= 0f)
            {
                if (next != null) Begin(next, nextTail);
                else if (Playing) { src.Stop(); tailSrc.Stop(); }
            }
        }
    }
}
