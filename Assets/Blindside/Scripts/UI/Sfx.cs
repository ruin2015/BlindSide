// Port of the 'alert' / 'alert2' branch of sfx(type) in blindside_source/05_ui_input_intro.js (~15):
//   tone=(f0,f1,len,vol,wave='square')=> oscillator f0→f1 (exponential), gain vol→.001 (exponential) over len
//   'alert':  tone(880,880,.12,.07); setTimeout(()=>sfx('alert2'),110)
//   'alert2': tone(1320,1320,.14,.07)
// The WebAudio graph is baked once into an AudioClip (both tones, the second starting 110ms in) and
// played through one hidden 2D AudioSource. Other sfx types aren't ported yet.
using UnityEngine;

namespace Blindside.UI
{
    public class Sfx : MonoBehaviour
    {
        const int Rate = 44100;
        static Sfx inst;
        static AudioClip alertClip;
        AudioSource src;

        /// <summary>sfx('alert') — the two-tone encounter beep.</summary>
        public static void Alert()
        {
            if (alertClip == null) alertClip = BuildAlert();
            Instance().src.PlayOneShot(alertClip);
        }

        static AudioClip uiClip, stepClip;

        /// <summary>sfx('ui') — `tone(1200,1800,.06,.05)`, the rope-lowering chirp.</summary>
        public static void Ui()
        {
            if (uiClip == null) uiClip = BuildSweep("sfx_ui", 1200f, 1800f, .06f, .05f, false);
            Instance().src.PlayOneShot(uiClip);
        }

        /// <summary>sfx('step') — `tone(180,120,.03,.02,'triangle')`, the landing thud.
        /// The source's gain is very low (.02); the clip is boosted so it is audible over the mix.</summary>
        public static void Step()
        {
            if (stepClip == null) stepClip = BuildSweep("sfx_step", 180f, 120f, .03f, .02f, true);
            Instance().src.PlayOneShot(stepClip, 4f);
        }

        // tone(f0,f1,len,vol,wave): frequency f0→f1 exponentially, gain vol→.001 exponentially over len.
        static AudioClip BuildSweep(string name, float f0, float f1, float len, float vol, bool triangle)
        {
            var data = new float[Mathf.CeilToInt(len * Rate)];
            float phase = 0f;
            for (int i = 0; i < data.Length; i++)
            {
                float t = (float)i / Rate, k = t / len;
                float f = f0 * Mathf.Pow(f1 / f0, k);
                phase += f / Rate;
                float frac = phase - Mathf.Floor(phase);
                float wave = triangle ? 4f * Mathf.Abs(frac - .5f) - 1f : (frac < .5f ? 1f : -1f);
                data[i] = wave * vol * Mathf.Pow(.001f / vol, k);
            }
            var clip = AudioClip.Create(name, data.Length, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        static Sfx Instance()
        {
            if (inst != null) return inst;
            var go = new GameObject("Sfx");
            DontDestroyOnLoad(go);
            inst = go.AddComponent<Sfx>();
            inst.src = go.AddComponent<AudioSource>();
            inst.src.playOnAwake = false;
            inst.src.spatialBlend = 0f;
            return inst;
        }

        static AudioClip BuildAlert()
        {
            float total = .11f + .14f;
            var data = new float[Mathf.CeilToInt(total * Rate)];
            AddTone(data, 0f, 880f, .12f, .07f);
            AddTone(data, .11f, 1320f, .14f, .07f);
            var clip = AudioClip.Create("sfx_alert", data.Length, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        // Square wave; gain.setValueAtTime(vol,t); gain.exponentialRampToValueAtTime(.001,t+len).
        static void AddTone(float[] data, float start, float freq, float len, float vol)
        {
            int s0 = Mathf.RoundToInt(start * Rate), n = Mathf.RoundToInt(len * Rate);
            for (int i = 0; i < n && s0 + i < data.Length; i++)
            {
                float t = (float)i / Rate;
                float g = vol * Mathf.Pow(.001f / vol, t / len);
                float sq = Mathf.Sin(2f * Mathf.PI * freq * t) >= 0f ? 1f : -1f;
                data[s0 + i] += sq * g;
            }
        }
    }
}
