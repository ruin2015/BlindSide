// The abilities' old sfx(type) calls from the web build. They used to bake synthesized noise / tones; now they only play the
// project's recorded sounds (Resources/Audio/SFX through SoundBank) — no file, no sound.
namespace Blindside.UI
{
    public static class SfxNoise
    {
        /// <summary>sfx('shot').</summary>
        public static void Shot() => SoundBank.Play(Snd.GunAr);
        /// <summary>sfx('boom') — breakPanel, a blast.</summary>
        public static void Boom() => SoundBank.Play(Snd.Explosion);
        /// <summary>sfx('hit') — hammer swing / thermite ring.</summary>
        public static void Hit() => SoundBank.Play(Snd.Hammer);
        /// <summary>sfx('click') — an ability click that lands on no valid target.</summary>
        public static void Click() => SoundBank.Play(Snd.UiDeny);
        /// <summary>sfx('ui') — stim / shield / gadgets.</summary>
        public static void Ui() => SoundBank.Play(Snd.ItemUse);
        /// <summary>sfx('zap') — shock wire.</summary>
        public static void Zap() => SoundBank.Play(Snd.Zap);
        /// <summary>sfx('gas') — toxic gas grenade.</summary>
        public static void Gas() => SoundBank.Play(Snd.Gas);
    }
}
