// What the running (or last) match was started from: the side, the map it was built from, and — for a test play from the map editor —
// how to get back to the editor. The title's START and the editor's test buttons both go through TitleSelectScreen.Launch, which
// records it here; the result screen's [再挑戦] / [エディタへ] / [タイトルへ] read it.
//
// `Source` is never handed to the game: every launch gives the game its own clone (MapLibrary.SetCurrent(Source.Clone())), so walls
// broken during a match, or anything else a match might change, can never reach the map data behind it (the editor's model, a
// title selection).
using System;
using Blindside.Data;
using Blindside.Map;

namespace Blindside.Integration
{
    public static class PlaySession
    {
        public static bool Active { get; private set; }
        /// <summary>A test play started from the map editor (not the title's START).</summary>
        public static bool IsTest => Active && ReturnToEditor != null;
        public static OperatorTeam Team { get; private set; }
        public static MapData Source { get; private set; }
        /// <summary>Test play: resumes the editor exactly as it was left. Null when the match came from the title.</summary>
        public static Action ReturnToEditor { get; private set; }

        public static void Begin(OperatorTeam team, MapData source, Action returnToEditor)
        {
            Active = true; Team = team; Source = source; ReturnToEditor = returnToEditor;
        }

        public static void End()
        {
            Active = false; Source = null; ReturnToEditor = null;
        }
    }
}
