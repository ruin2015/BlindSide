// The abilities' sfx(type) branches from blindside_source/05_ui_input_intro.js (~5-19), kept apart
// from Sfx.cs (which only bakes 'alert'):
//   noise=(len,freq,vol,type='bandpass') => noiseBuf → BiquadFilter(type,freq) → gain vol→.001 (exponential) over len
//   tone=(f0,f1,len,vol,wave='square')   => oscillator f0→f1 (exponential), gain vol→.001 (exponential) over len
//   'shot': noise(.09,1800,.22)   'boom': noise(.55,300,.5,'lowpass')   'hit': noise(.12,600,.3,'lowpass')
//   'click': tone(900,700,.04,.05)   'ui': tone(1200,1800,.06,.05)   'zap': noise(.3,3500,.18,'highpass')
//   'gas': noise(.5,900,.12,'lowpass')
// Each type is baked once into an AudioClip and played through one hidden 2D AudioSource. The white noise
// comes from a fixed-seed System.Random — never UnityEngine.Random, whose stream the combat rolls share.
using UnityEngine;

namespace Blindside.UI
{
    public class SfxNoise : MonoBehaviour
    {
        const int Rate = 44100;
        static SfxNoise inst;
        static AudioClip shotClip, boomClip, hitClip, clickClip, uiClip, zapClip, gasClip;
        AudioSource src;

        /// <summary>sfx('shot').</summary>
        public static void Shot() => Play(ref shotClip, () => BuildNoise("sfx_shot", 1, .09f, 1800f, .22f, false));
        /// <summary>sfx('boom') — breakPanel.</summary>
        public static void Boom() => Play(ref boomClip, () => BuildNoise("sfx_boom", 2, .55f, 300f, .5f, true));
        /// <summary>sfx('hit') — hammer swing / thermite ring.</summary>
        public static void Hit() => Play(ref hitClip, () => BuildNoise("sfx_hit", 3, .12f, 600f, .3f, true));
        /// <summary>sfx('click') — e.g. an ability click that lands on no valid target.</summary>
        public static void Click() => Play(ref clickClip, () => BuildTone("sfx_click", 900f, 700f, .04f, .05f));
        /// <summary>sfx('ui') — tone(1200,1800,.06,.05): stim / shield.</summary>
        public static void Ui() => Play(ref uiClip, () => BuildTone("sfx_ui", 1200f, 1800f, .06f, .05f));
        /// <summary>sfx('zap') — noise(.3,3500,.18,'highpass'): shock wire.</summary>
        public static void Zap() => Play(ref zapClip, () => BuildNoise("sfx_zap", 4, .3f, 3500f, .18f, false, highpass: true));
        /// <summary>sfx('gas') — noise(.5,900,.12,'lowpass'): toxic gas grenade.</summary>
        public static void Gas() => Play(ref gasClip, () => BuildNoise("sfx_gas", 5, .5f, 900f, .12f, true));

        static void Play(ref AudioClip clip, System.Func<AudioClip> build)
        {
            if (clip == null) clip = build();
            Instance().src.PlayOneShot(clip, Blindside.Settings.GameSettings.SfxVolume);
        }

        static SfxNoise Instance()
        {
            if (inst != null) return inst;
            var go = new GameObject("SfxNoise");
            DontDestroyOnLoad(go);
            inst = go.AddComponent<SfxNoise>();
            inst.src = go.AddComponent<AudioSource>();
            inst.src.playOnAwake = false;
            inst.src.spatialBlend = 0f;
            return inst;
        }

        // WebAudio BiquadFilterNode (RBJ cookbook) with its default Q=1; 'bandpass' is the constant 0 dB
        // peak gain form, 'lowpass' the usual 2nd-order low-pass.
        static AudioClip BuildNoise(string name, int seed, float len, float freq, float vol, bool lowpass, bool highpass = false)
        {
            int n = Mathf.RoundToInt(len * Rate);
            var data = new float[n];
            var rng = new System.Random(seed);
            float w0 = 2f * Mathf.PI * freq / Rate, cs = Mathf.Cos(w0), alpha = Mathf.Sin(w0) / 2f;   // Q = 1
            float b0, b1, b2, a0 = 1f + alpha, a1 = -2f * cs, a2 = 1f - alpha;
            if (lowpass) { b0 = (1f - cs) / 2f; b1 = 1f - cs; b2 = (1f - cs) / 2f; }
            else if (highpass) { b0 = (1f + cs) / 2f; b1 = -(1f + cs); b2 = (1f + cs) / 2f; }
            else { b0 = alpha; b1 = 0f; b2 = -alpha; }
            float x1 = 0, x2 = 0, y1 = 0, y2 = 0;
            for (int i = 0; i < n; i++)
            {
                float x = (float)(rng.NextDouble() * 2 - 1);
                float y = (b0 * x + b1 * x1 + b2 * x2 - a1 * y1 - a2 * y2) / a0;
                x2 = x1; x1 = x; y2 = y1; y1 = y;
                float t = (float)i / Rate;
                data[i] = y * vol * Mathf.Pow(.001f / vol, t / len);
            }
            var clip = AudioClip.Create(name, n, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        // tone(f0,f1,len,vol,'square'): exponential sweep f0→f1, gain vol→.001 over len.
        static AudioClip BuildTone(string name, float f0, float f1, float len, float vol)
        {
            int n = Mathf.RoundToInt(len * Rate);
            var data = new float[n];
            float phase = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / Rate;
                float f = f0 * Mathf.Pow(f1 / f0, t / len);
                phase += 2f * Mathf.PI * f / Rate;
                float sq = Mathf.Sin(phase) >= 0f ? 1f : -1f;
                data[i] = sq * vol * Mathf.Pow(.001f / vol, t / len);
            }
            var clip = AudioClip.Create(name, n, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
