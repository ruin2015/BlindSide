// Blindside > UI > Setup Kit Hint and Log (4-3): one-time, idempotent swap of the HUD log to the kit.
//   1. Prefabs/UI/HUD_Log.prefab (HudLogView) moves to UI/Legacy/HUD_Log.prefab (GUID kept: the scene's old instance keeps pointing at it).
//   2. A new Prefabs/UI/HUD_Log.prefab holds only the root with a KitLogView (the lines are pooled at first use).
//   3. In the open scene: the old log object under BlindsideHUD/HUD is renamed HUD_Log_Legacy and set inactive (kept, so HudController.log
//      still points at it), the kit log is instantiated at the same sibling index and HudController.kitLog is wired.
// The hint bar has no prefab (PlayerSelectionController builds KitHintBar in code, like the old HintView); the old HintView.cs sits in
// UI/Legacy/HintView.cs.txt. The report is written to the console.
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Blindside.Integration;
using Blindside.UI;

namespace Blindside.UITools.EditorTools
{
    public static class KitHintLogSetup
    {
        const string KitPath = "Assets/Blindside/Prefabs/UI/HUD_Log.prefab";
        const string LegacyPath = "Assets/Blindside/UI/Legacy/HUD_Log.prefab";

        [MenuItem("Blindside/UI/Setup Kit Hint and Log (4-3)")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) { Debug.LogWarning("[KitHintLog] edit mode only — stop Play first."); return; }
            var rep = new StringBuilder("[KitHintLog] setup\n");

            // 1 + 2: prefabs
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(KitPath);
            bool kitAlready = existing != null && existing.GetComponent<KitLogView>() != null;
            if (!kitAlready)
            {
                if (existing != null)
                {
                    string err = AssetDatabase.MoveAsset(KitPath, LegacyPath);
                    rep.AppendLine(string.IsNullOrEmpty(err) ? $"moved {KitPath} -> {LegacyPath}" : "MOVE FAILED: " + err);
                    if (!string.IsNullOrEmpty(err)) { Debug.LogError(rep.ToString()); return; }
                }
                var go = new GameObject("HUD_Log", typeof(RectTransform)) { layer = 5 };
                var rt = (RectTransform)go.transform;
                rt.anchorMin = rt.anchorMax = new Vector2(.5f, 0f); rt.pivot = Vector2.zero;
                rt.anchoredPosition = new Vector2(-KitLogView.FromCentre, KitLogView.BaseBottom);
                rt.sizeDelta = new Vector2(KitLogView.Width, 0f);
                go.AddComponent<KitLogView>();
                PrefabUtility.SaveAsPrefabAsset(go, KitPath);
                Object.DestroyImmediate(go);
                rep.AppendLine("created " + KitPath);
            }
            else rep.AppendLine("kit prefab already in place");

            // 3: scene
            var hud = Object.FindAnyObjectByType<HudController>(FindObjectsInactive.Include);
            if (hud == null) { rep.AppendLine("no HudController in the open scene — prefabs only"); Debug.Log(rep.ToString()); return; }
            if (hud.kitLog != null) { rep.AppendLine("scene already wired (HudController.kitLog set)"); Debug.Log(rep.ToString()); return; }

            var old = hud.log;
            Transform parent = old != null ? old.transform.parent : hud.content != null ? hud.content.transform : hud.transform;
            int index = old != null ? old.transform.GetSiblingIndex() : 0;
            if (old != null)
            {
                rep.AppendLine($"old log: {PathOf(old.transform)} (sibling {index}, active {old.gameObject.activeSelf}, prefab {LegacyPath})");
                Undo.RecordObject(old.gameObject, "Hide legacy log");
                old.gameObject.name = "HUD_Log_Legacy";
                old.gameObject.SetActive(false);
            }
            var kit = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(KitPath), parent);
            kit.transform.SetSiblingIndex(index);
            Undo.RegisterCreatedObjectUndo(kit, "Kit log");
            Undo.RecordObject(hud, "Wire kit log");
            hud.kitLog = kit.GetComponent<KitLogView>();
            EditorUtility.SetDirty(hud);
            EditorSceneManager.MarkSceneDirty(hud.gameObject.scene);
            EditorSceneManager.SaveScene(hud.gameObject.scene);
            rep.AppendLine($"kit log: {PathOf(kit.transform)} (sibling {index}); HudController.kitLog wired; scene saved");
            Debug.Log(rep.ToString());
        }

        static string PathOf(Transform t)
        {
            string p = t.name;
            while (t.parent != null) { t = t.parent; p = t.name + "/" + p; }
            return p;
        }

        /// <summary>Blindside > UI > Measure Kit Hint and Log GC: in the HUD stage, re-poses the hint bar and the log for 300 frames
        /// (Evaluate at moving times, with the log re-meshing its graphics) and logs the managed bytes allocated after 30 warm-up frames;
        /// then adds 7 more lines to the log and logs what that allocated (TMP text generation) — the poses themselves must be 0.</summary>
        [MenuItem("Blindside/UI/Measure Kit Hint and Log GC")]
        public static void MeasureGc()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) { Debug.LogWarning("[KitHintLogGC] edit mode only."); return; }
            var state = new UIPreview.State { name = "hud_log_full", label = "HUD log", build = s => UIPreview.BuildHudLogFull(s) };
            using (var stage = UIPreview.Build(state))
            {
                var hint = Object.FindAnyObjectByType<KitHintBar>(FindObjectsInactive.Exclude);
                var log = Object.FindAnyObjectByType<KitLogView>(FindObjectsInactive.Exclude);
                if (hint == null || log == null) { Debug.LogWarning("[KitHintLogGC] hint or log missing in the stage."); return; }
                long start = 0;
                for (int frame = 0; frame < 330; frame++)
                {
                    if (frame == 30) start = System.GC.GetAllocatedBytesForCurrentThread();
                    float t = (frame % 40) * .01f;
                    hint.Evaluate(t); log.Evaluate(t);
                    Canvas.ForceUpdateCanvases();
                }
                long poses = System.GC.GetAllocatedBytesForCurrentThread() - start;
                Debug.Log($"[KitHintLogGC] 300 frames of hint + log poses: {poses} bytes ({poses / 300f:0.##} per frame)");

                string[] lines = { "壁を破壊 (2,9)", "敵の銃声を確認 — 北東の部屋", "CAM-02: 異常なし", "壁を強化 — この壁 2/3 強化完了 (残り強化 8)",
                    "射撃可能な味方がいません (行動力の消費なし · 視界内に隊員が必要)", "アルゴス → フレア: 2/3 命中, 36 ダメージ", "作戦開始" };
                long a = System.GC.GetAllocatedBytesForCurrentThread();
                for (int i = 0; i < lines.Length; i++) log.PreviewLine(lines[i], (HudLogType)(i % 4));
                long adds = System.GC.GetAllocatedBytesForCurrentThread() - a;
                Debug.Log($"[KitHintLogGC] {lines.Length} log lines added (pooled, settled): {adds} bytes ({adds / (float)lines.Length:0.#} per line)");
            }
        }
    }
}
