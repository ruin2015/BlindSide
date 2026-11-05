// The one seedable random source for everything that decides the outcome of a match: hit / headshot rolls,
// the AI's launcher choices, and the attackers' random drop spots. Presentation effects (sparks, camera shake,
// glitch text, ...) keep their own generators and never draw from here, so what is on screen cannot change a result.
// Seed(n) makes a match reproducible; without a call the stream is seeded from the clock.
using System;

namespace Blindside.Core
{
    public static class GameRng
    {
        static Random rng = new Random();

        /// <summary>The seed the current stream started from (for logs / reproducing a match).</summary>
        public static int CurrentSeed { get; private set; } = Environment.TickCount;

        public static void Seed(int seed)
        {
            CurrentSeed = seed;
            rng = new Random(seed);
        }

        /// <summary>[0, 1) — Random.value's stand-in.</summary>
        public static float Value => (float)rng.NextDouble();

        /// <summary>[min, max) floats — Random.Range(float,float)'s stand-in.</summary>
        public static float Range(float min, float max) => min + (float)rng.NextDouble() * (max - min);

        /// <summary>[min, max) ints — Random.Range(int,int)'s stand-in.</summary>
        public static int Range(int min, int max) => rng.Next(min, max);

        /// <summary>[0, max).</summary>
        public static int Next(int max) => rng.Next(max);

        public static double NextDouble() => rng.NextDouble();
    }
}
