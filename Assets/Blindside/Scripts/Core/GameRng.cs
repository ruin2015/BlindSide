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

        /// <summary>A copy of the stream as it stands: it draws exactly the numbers the real stream will draw next, and drawing from it
        /// leaves the real stream untouched (the engagement forecast rolls the coming exchange on it).</summary>
        public static Random Peek() => (Random)DeepCopy(rng, new System.Collections.Generic.Dictionary<object, object>(ReferenceComparer.Instance));

        // System.Random keeps its state in private fields (a seed array and two indices; on some runtimes inside an impl object that
        // points back at its Random): a member-wise copy with every array and nested object copied too, each object once.
        static object DeepCopy(object o, System.Collections.Generic.Dictionary<object, object> done)
        {
            if (o == null) return null;
            var t = o.GetType();
            if (t.IsPrimitive || t.IsEnum || o is string || o is Delegate) return o;
            if (done.TryGetValue(o, out var seen)) return seen;
            if (o is Array a) { var ac = a.Clone(); done[o] = ac; return ac; }   // (the seed array holds ints: a shallow copy is a full copy)
            var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public;
            var copy = typeof(object).GetMethod("MemberwiseClone", flags).Invoke(o, null);
            done[o] = copy;
            for (var ct = t; ct != null && ct != typeof(object); ct = ct.BaseType)
                foreach (var f in ct.GetFields(flags | System.Reflection.BindingFlags.DeclaredOnly))
                {
                    if (f.FieldType.IsPrimitive || f.FieldType.IsEnum || f.FieldType == typeof(string)) continue;
                    var v = f.GetValue(o);
                    if (v != null) f.SetValue(copy, DeepCopy(v, done));
                }
            return copy;
        }

        sealed class ReferenceComparer : System.Collections.Generic.IEqualityComparer<object>
        {
            public static readonly ReferenceComparer Instance = new ReferenceComparer();
            public new bool Equals(object x, object y) => ReferenceEquals(x, y);
            public int GetHashCode(object o) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(o);
        }
    }
}
