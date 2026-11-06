// (Blindside: one per scene — says what the scene is (SceneFlow) and starts it.
//  Battle plays SceneFlow.Pending when the title sent one, else the match set up here — so a planner can open Battle (or a map scene)
//  and press Play. The title UI stays in every scene (the match start, the intro's squad and [再挑戦] read it) but only Title and
//  MapEditor show it.)
using System.Collections;
using UnityEngine;
using Blindside.Data;
using Blindside.Map;
using Blindside.MapEditor;
using Blindside.UI;

namespace Blindside.Flow
{
    [DefaultExecutionOrder(-1000)]
    public class SceneEntry : MonoBehaviour
    {
        public static SceneEntry Active { get; private set; }

        [Tooltip("What this scene is.")]
        public SceneRole role = SceneRole.Battle;

        [Header("Battle — when this scene is played directly (the title's choice wins otherwise)")]
        [Tooltip("The player's side.")]
        public OperatorTeam playerTeam = OperatorTeam.Attack;
        [Tooltip("The map (a MapFile JSON exported from the editor). Empty = the default house.")]
        public TextAsset mapJson;
        [Tooltip("The map's catalog id (set by the export; informational).")]
        public string mapId;
        [Tooltip("Start without the intro (the defense side never has one).")]
        public bool skipIntro;

        TitleSelectScreen title;

        void Awake()
        {
            Active = this;
            title = FindAnyObjectByType<TitleSelectScreen>(FindObjectsInactive.Include);
            // only the title and the editor show the title UI; elsewhere it is the match starter, hidden before it ever draws
            if (title != null && role != SceneRole.Title && role != SceneRole.MapEditor) title.gameObject.SetActive(false);
        }

        void OnDestroy()
        {
            if (Active == this) Active = null;
        }

        IEnumerator Start()
        {
            yield return null;   // every Awake / OnEnable / Start of the scene has run
            if (role == SceneRole.Hub) yield break;   // the hub runs itself (Blindside.Hub.HubDirector); it has no title UI
            if (title == null) { if (role != SceneRole.Title) Debug.LogError("[SceneEntry] no TitleSelectScreen in the scene"); yield break; }
            switch (role)
            {
                case SceneRole.Battle:
                    var req = SceneFlow.TakePending() ?? new LaunchRequest { team = playerTeam, skipIntro = skipIntro };
                    var map = req.map ?? OwnMap();
                    if (map == null || !title.Launch(req.team, map, null, req.skipIntro))
                    {
                        Debug.LogError("[SceneEntry] the battle could not start — back to the title");
                        SceneFlow.GoTitle();
                    }
                    break;
                case SceneRole.Tutorial:
                    if (!title.StartTutorialHere()) SceneFlow.GoTitle();
                    break;
                case SceneRole.MapEditor:
                    title.gameObject.SetActive(false);
                    MapEditorScreen.Open(SceneFlow.GoTitle);   // leaving the editor goes back to the title scene
                    break;
            }
        }

        /// <summary>This scene's own map: mapJson when set (null when it can't be played), else the default house.</summary>
        public MapData OwnMap()
        {
            if (mapJson == null) return MapLibrary.LoadDefault();
            if (!MapFile.TryParse(mapJson.text, out var file, out var why)) { Debug.LogError($"[SceneEntry] {mapJson.name}: {why}"); return null; }
            try { return file.ToModel().BuildGameData(); }
            catch (System.Exception ex) { Debug.LogError($"[SceneEntry] {mapJson.name}: {ex.Message}"); return null; }
        }
    }
}
