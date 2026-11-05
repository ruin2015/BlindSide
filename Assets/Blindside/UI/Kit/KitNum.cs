// Cached number strings for HUD texts: Of(n) builds the string once per number (0..999) and hands the same one back afterwards, so
// setting a counted-down value never allocates once the numbers have been used.
namespace Blindside.UI
{
    public static class KitNum
    {
        static readonly string[] cache = new string[1000];

        public static string Of(int n)
        {
            if (n < 0 || n >= cache.Length) return n.ToString();
            return cache[n] ?? (cache[n] = n.ToString());
        }
    }
}
