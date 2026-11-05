// Dev harness: runs AI-vs-AI matches back to back with a fixed GameRng seed per match and writes one line per
// match — (winner, reason, rounds, order of death) — so a refactor can be checked against a recorded baseline
// (Baselines/default_map.txt). Play mode only; nothing in the game references it. Combat staging is off in
// AI-vs-AI (no player side → BattleDirector resolves engagements instantly), and Time.timeScale is raised so 20
// matches finish quickly — game logic never reads the clock, only waits on it.
// StartMaps runs the same loop on other maps (MapLibrary.SetCurrent for the duration, the default map restored after): each
// line then also lists what the defense preparation placed and where the attackers dropped.
using System.Collections;
using System.Text;
using UnityEngine;
using Blindside.Core;
using Blindside.Map;

namespace Blindside.Integration
{
    public class SimBatch : MonoBehaviour
    {
        public bool Done { get; private set; }
        public string Summary { get; private set; }
        public int Exceptions { get; private set; }

        public static SimBatch Start(string outPath, int games, int firstSeed, float timeScale = 40f)
            => StartMaps(outPath, games, firstSeed, timeScale, null, null);

        /// <param name="maps">null: the current map, in both modes (the baseline format). Otherwise each of these maps in turn.</param>
        /// <param name="setupModes">which modes to run: false = plain AI-vs-AI, true = with DefensePrep + the random attacker drop. Null = both
        /// (the baseline) for the current map, only `true` for `maps`.</param>
        public static SimBatch StartMaps(string outPath, int games, int firstSeed, float timeScale, MapData[] maps, bool[] setupModes)
        {
            var go = new GameObject("SimBatch");
            var sb = go.AddComponent<SimBatch>();
            sb.StartCoroutine(sb.RunAll(outPath, games, firstSeed, timeScale, maps, setupModes));
            return sb;
        }

        IEnumerator RunAll(string outPath, int games, int firstSeed, float timeScale, MapData[] maps, bool[] setupModes)
        {
            var mb = FindFirstObjectByType<MatchBootstrap>(FindObjectsInactive.Include);
            if (mb == null) { Summary = "no MatchBootstrap"; Done = true; yield break; }
            var title = FindFirstObjectByType<Blindside.UI.TitleSelectScreen>(FindObjectsInactive.Include);
            if (title != null) title.gameObject.SetActive(false);
            mb.ClearMatch();   // stops the match MatchBootstrap.Start began, if any
            Blindside.Rendering.BattlefieldCamera.Activate(11f, 6f);
            mb.playerTeam = null;
            Application.runInBackground = true;
            // Unity clamps a frame's scaled delta to maximumDeltaTime, which would cap the speed-up; lift it and the frame limiters.
            float oldMaxDt = Time.maximumDeltaTime; int oldVsync = QualitySettings.vSyncCount, oldFps = Application.targetFrameRate;
            Time.maximumDeltaTime = 5f; QualitySettings.vSyncCount = 0; Application.targetFrameRate = -1;
            Time.timeScale = timeScale;
            // Nothing needs drawing: switching the cameras off roughly doubles the frame rate the waits are counted in.
            var cams = new System.Collections.Generic.List<Camera>();
            foreach (var c in Camera.allCameras) if (c.enabled) { cams.Add(c); c.enabled = false; }

            var original = MapLibrary.Current;
            bool otherMaps = maps != null;
            var mapList = otherMaps ? maps : new MapData[] { original };
            var modes = setupModes ?? (otherMaps ? new[] { true } : new[] { false, true });

            var text = new StringBuilder();
            foreach (var map in mapList)
            {
                if (otherMaps)
                {
                    if (!MapLibrary.SetCurrent(map)) { text.AppendLine($"# map={map.name} INVALID"); Exceptions++; continue; }
                    text.AppendLine($"# map={map.name} {map.width}x{map.height}");
                }
                foreach (bool setup in modes)
                {
                    text.AppendLine(setup ? "# mode=AIvsAI+DefensePrep+RandomAttackerSpawn" : "# mode=AIvsAI");
                    var wins = new int[2];
                    int unfinished = 0, maxRounds = 0, over15 = 0, exceptions = 0;
                    for (int i = 0; i < games; i++)
                    {
                        int seed = firstSeed + i;
                        GameRng.Seed(seed);
                        mb.simSetup = setup;
                        float t0 = Time.realtimeSinceStartup;
                        var run = mb.StartCoroutine(mb.RunOneMatch());
                        while (!mb.Finished && mb.Error == null && Time.realtimeSinceStartup - t0 < 180f) yield return null;
                        string line;
                        if (mb.Error != null) { Exceptions++; exceptions++; line = $"seed={seed} EXCEPTION {mb.Error.GetType().Name}: {mb.Error.Message}"; }
                        else if (!mb.Finished) { unfinished++; line = $"seed={seed} TIMEOUT"; }
                        else
                        {
                            wins[mb.Result == Blindside.Turns.MatchResult.AttackWins ? 0 : 1]++;
                            maxRounds = Mathf.Max(maxRounds, mb.RoundsPlayed);
                            if (mb.RoundsPlayed > Blindside.Turns.TurnManager.MaxRound) over15++;
                            line = $"seed={seed} winner={mb.Result} reason=\"{mb.ResultReason}\" rounds={mb.RoundsPlayed} deaths=[{string.Join(",", mb.DeathOrder)}]";
                        }
                        if (otherMaps && setup) line += $" prep=[{mb.LastPrep}] drop=[{mb.LastSpawn}]";
                        text.AppendLine(line);
                        mb.ClearMatch();
                        Time.timeScale = timeScale;   // ClearMatch puts the clock back to 1 (it undoes HitStop / slow motion)
                        yield return null;
                    }
                    text.AppendLine($"# attackWins={wins[0]} defenseWins={wins[1]} exceptions={(otherMaps ? exceptions : Exceptions)}"
                        + (otherMaps ? $" unfinished={unfinished} maxRounds={maxRounds} over15={over15}" : ""));
                }
            }
            if (otherMaps) MapLibrary.SetCurrent(original);
            foreach (var c in cams) if (c != null) c.enabled = true;
            Time.timeScale = 1f;
            Time.maximumDeltaTime = oldMaxDt; QualitySettings.vSyncCount = oldVsync; Application.targetFrameRate = oldFps;
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(outPath));
            System.IO.File.WriteAllText(outPath, text.ToString(), new UTF8Encoding(false));
            Summary = text.ToString();
            Done = true;
        }
    }
}
