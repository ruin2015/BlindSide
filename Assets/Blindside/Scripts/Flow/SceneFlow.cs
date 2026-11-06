// (Blindside: the game's scenes and how it moves between them.
//   Title     — the faction / map select. START → Battle (or a map's own scene), チュートリアル → Tutorial, マップエディタ → MapEditor (editor only).
//   Tutorial  — the training match; its end → Battle on the default house.
//   Battle    — one match; [タイトルへ] → Title, [再挑戦] replays in place.
//   MapEditor — editor only, never in a build: authoring, test play in place; saved maps go out as map scenes
//               (Blindside > Scenes > Export Saved Map to Map Scene…).
//  A scene says what it is with its SceneEntry; the match a scene is asked to play travels across the load in Pending.
//  A scene with no SceneEntry (the old SampleScene) keeps the old one-scene flow.)
using UnityEngine;
using UnityEngine.SceneManagement;
using Blindside.Data;
using Blindside.Map;

namespace Blindside.Flow
{
    public enum SceneRole { Title, Battle, Tutorial, MapEditor, Hub }

    /// <summary>The match the next Battle scene is to play.</summary>
    public sealed class LaunchRequest
    {
        public OperatorTeam team = OperatorTeam.Attack;
        /// <summary>The map (a clone the battle may keep); null = the scene's own map (SceneEntry.mapJson, else the default house).</summary>
        public MapData map;
        public bool skipIntro;
        /// <summary>The scene to load; null = SceneFlowSettings.battleScene.</summary>
        public string sceneName;
    }

    public static class SceneFlow
    {
        /// <summary>Set before a scene load, taken by that scene's SceneEntry (TakePending).</summary>
        public static LaunchRequest Pending { get; private set; }

        public static LaunchRequest TakePending()
        {
            var r = Pending; Pending = null; return r;
        }

        /// <summary>The loaded scene's role, or null when it has no SceneEntry (the old one-scene flow).</summary>
        public static SceneRole? ActiveRole => SceneEntry.Active != null ? SceneEntry.Active.role : (SceneRole?)null;

        public static bool InRole(SceneRole role) => Application.isPlaying && ActiveRole == role;

        static SceneFlowSettings S => SceneFlowSettings.Instance;

        public static void GoTitle() => Load(S.titleScene);

        public static void GoTutorial() => Load(S.tutorialScene);

        /// <summary>The squad's HQ (the third-person hub between operations) is in the build.</summary>
        public static bool HubAvailable => !string.IsNullOrEmpty(S.hubScene) && Application.CanStreamedLevelBeLoaded(S.hubScene);

        /// <summary>The squad's HQ; the title when the hub scene isn't in the build yet.</summary>
        public static void GoHub()
        {
            if (HubAvailable) Load(S.hubScene);
            else { Debug.LogWarning("[SceneFlow] no hub scene in the build (Blindside > Scenes > Build Squad Hub) — the title instead"); GoTitle(); }
        }

        public static void GoBattle(LaunchRequest request)
        {
            Pending = request ?? new LaunchRequest();
            Load(string.IsNullOrEmpty(Pending.sceneName) ? S.battleScene : Pending.sceneName);
        }

        /// <summary>The map editor scene (play mode in the editor only — it is never part of a build).</summary>
        public static void GoMapEditor()
        {
#if UNITY_EDITOR
            Time.timeScale = 1f;
            Leaving = true;
            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(S.mapEditorScenePath, new LoadSceneParameters(LoadSceneMode.Single));
#else
            Debug.LogWarning("[SceneFlow] the map editor is not part of a build");
#endif
        }

        /// <summary>True from a scene change's start until the next scene has loaded: the old scene is going away (its stopping
        /// PlayableDirectors etc. must not start anything).</summary>
        public static bool Leaving { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Leaving = false;
            Pending = null;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        static void OnSceneLoaded(Scene s, LoadSceneMode m) => Leaving = false;

        static void Load(string scene)
        {
            Time.timeScale = 1f;
            if (!Application.CanStreamedLevelBeLoaded(scene))
            {
                Debug.LogError($"[SceneFlow] scene \"{scene}\" is not in the build (Blindside > Scenes > Apply Build Settings)");
                return;
            }
            Leaving = true;
            SceneManager.LoadScene(scene, LoadSceneMode.Single);
        }
    }
}
