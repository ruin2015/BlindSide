// The old sfx(type) calls from the web build ('alert', 'ui', 'step'). They used to bake synthesized tones; now they only play the
// project's recorded sounds (Resources/Audio/SFX through SoundBank) — no file, no sound.
using UnityEngine;

namespace Blindside.UI
{
    public static class Sfx
    {
        /// <summary>sfx('alert') — the drone's finds, the siren (an encounter plays Contact itself).</summary>
        public static void Alert() => SoundBank.Play(Snd.DronePing);

        /// <summary>sfx('ui') — a menu step (not UiConfirm: that is the buttons' own click, played by UiSounds).</summary>
        public static void Ui() => SoundBank.Play(Snd.UiOpen);

        /// <summary>sfx('step') — the landing touchdown, soft: well under the guns.</summary>
        public static void Step() => SoundBank.Play(Snd.RappelLand, .3f);
    }
}
