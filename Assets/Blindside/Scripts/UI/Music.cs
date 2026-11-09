// Background music: one looping 2D AudioSource that outlives scene loads (DontDestroyOnLoad), with fades on the unscaled clock (hit-stop
// and pauses don't touch it). SceneEntry asks for each scene's track: the title plays TitleTrack (Resources/Music/TacticalBlue, "Tactical
// Blue"); every other scene fades it out (1 s) — the battle then starts BattleTrack itself once the placing is done (MatchBootstrap). The same clip asked for again keeps playing (no restart). Volume: Base × the master
// volume (AudioListener). Nothing is allocated per frame.
using UnityEngine;

namespace Blindside.UI
{
    public class Music : MonoBehaviour
    {
        public const string TitleTrack = "Music/TacticalBlue";
        /// <summary>The battle's track ("Tactical Grid"): from the first attack phase, once the deployment / preparation is done
        /// (MatchBootstrap); not in the tutorial.</summary>
        public const string BattleTrack = "Music/TacticalGrid";
        public const float Base = .7f, FadeIn = 1.5f, FadeOut = 1f;

        static Music inst;
        AudioSource src;
        float level, target, speed;
        AudioClip next;

        static Music Instance()
        {
            if (inst != null) return inst;
            var go = new GameObject("Music");
            DontDestroyOnLoad(go);
            inst = go.AddComponent<Music>();
            inst.src = go.AddComponent<AudioSource>();
            inst.src.loop = true; inst.src.playOnAwake = false; inst.src.spatialBlend = 0f;
            inst.src.ignoreListenerPause = true;
            inst.src.volume = 0f;
            return inst;
        }

        /// <summary>Plays the clip at `resource` (Resources path) looped, fading in; the one playing fades out first.</summary>
        public static void Play(string resource)
        {
            if (!Application.isPlaying) return;
            var clip = Resources.Load<AudioClip>(resource);
            if (clip == null) { Debug.LogWarning("[Music] missing " + resource); return; }
            var m = Instance();
            if (m.src.clip == clip && m.src.isPlaying && m.next == null) { m.target = 1f; m.speed = 1f / FadeIn; return; }
            if (m.src.isPlaying && m.level > .01f) { m.next = clip; m.target = 0f; m.speed = 1f / FadeOut; return; }
            m.Begin(clip);
        }

        /// <summary>Fades the music out (it stops when silent).</summary>
        public static void Stop()
        {
            if (inst == null) return;
            inst.next = null; inst.target = 0f; inst.speed = 1f / FadeOut;
        }

        void Begin(AudioClip clip)
        {
            next = null;
            src.clip = clip;
            level = 0f; target = 1f; speed = 1f / FadeIn;
            src.volume = 0f;
            src.Play();
        }

        void Update()
        {
            if (level != target)
            {
                level = Mathf.MoveTowards(level, target, speed * Time.unscaledDeltaTime);
                src.volume = Base * level;
            }
            if (level <= 0f && target <= 0f)
            {
                if (next != null) Begin(next);
                else if (src.isPlaying) src.Stop();
            }
        }
    }
}
