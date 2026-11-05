// A UI effect whose look at any moment is a function of time alone: Evaluate(t) poses it as it is `t` seconds into its run. The
// effect's own coroutine / Update only advances the clock and calls Evaluate, so an editor preview can pose it at a fixed t.
namespace Blindside.UI
{
    public interface ITimedEffect
    {
        /// <summary>Length of one run in seconds (a looping effect: one period).</summary>
        float Duration { get; }

        /// <summary>Poses the effect `t` seconds into its run (t ≥ Duration = the settled end state).</summary>
        void Evaluate(float t);
    }
}
