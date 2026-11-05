// (Blindside: which scene is which — Resources/BlindsideSceneFlow.asset, made by Blindside > Scenes > Build Scene Set.
//  Rename a scene here and in Build Settings together.)
using UnityEngine;

namespace Blindside.Flow
{
    [CreateAssetMenu(menuName = "Blindside/Scene Flow Settings", fileName = "BlindsideSceneFlow")]
    public class SceneFlowSettings : ScriptableObject
    {
        public const string ResourceName = "BlindsideSceneFlow";

        [Tooltip("タイトル: the first scene of the build.")]
        public string titleScene = "Title";
        [Tooltip("The tutorial match.")]
        public string tutorialScene = "Tutorial";
        [Tooltip("A match on the default house, or on whatever map the title hands over.")]
        public string battleScene = "Battle";
        [Tooltip("The map editor (play mode in the editor only; not in Build Settings).")]
        public string mapEditorScenePath = "Assets/Blindside/Scenes/Editor/MapEditor.unity";

        static SceneFlowSettings cached;

        public static SceneFlowSettings Instance
        {
            get
            {
                if (cached == null) cached = Resources.Load<SceneFlowSettings>(ResourceName);
                if (cached == null) cached = CreateInstance<SceneFlowSettings>();   // the defaults above
                return cached;
            }
        }
    }
}
