// Blindside > Debug > Co-op Ranks: FLARE's co-op rank with each operator set by hand (0 … 10), for testing the co-op perks.
//   [セーブに書き込む]  the save: the session's in Play mode (written at once), else the file on disk (loaded, changed, written)
//   [試合に反映]        the running match: each of the player's units takes its rank now (BASTION's move 2 too; the second gun and the
//                       gadget slots are set at a match's start, so those need the next match)
//   [デバッグ用に保存]  matches with no save session (debug scenes, test plays) start with these ranks (Campaign.DebugRank, PlayerPrefs)
// Editor-only tool; the game logic reads only Campaign.Rank / Campaign.DebugRank.
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using Blindside.Data;
using Blindside.Progress;
using Blindside.Save;

namespace Blindside.UITools.EditorTools
{
    public sealed class CoopRankDebugWindow : EditorWindow
    {
        readonly List<string> keys = new List<string>();
        readonly Dictionary<string, int> ranks = new Dictionary<string, int>();
        string status = "";
        Vector2 scroll;

        [MenuItem("Blindside/Debug/Co-op Ranks")]
        static void Open()
        {
            var w = GetWindow<CoopRankDebugWindow>("Co-op Ranks");
            w.minSize = new Vector2(360, 320);
            w.Reload();
        }

        void OnEnable() { if (keys.Count == 0) Reload(); }

        static int Max => Campaign.CoopNeed.Length;

        void Reload()
        {
            keys.Clear(); ranks.Clear();
            foreach (var guid in AssetDatabase.FindAssets("t:Blindside.Data.OperatorData"))
            {
                var op = AssetDatabase.LoadAssetAtPath<OperatorData>(AssetDatabase.GUIDToAssetPath(guid));
                if (op == null || string.IsNullOrEmpty(op.key) || op.key == Campaign.Commander || op.team != OperatorTeam.Attack) continue;
                if (!keys.Contains(op.key)) keys.Add(op.key);
            }
            keys.Sort(string.CompareOrdinal);
            var d = Source(out string from);
            foreach (var k in keys)
            {
                int r = d != null ? Campaign.Rank(d, k) : 0;
                if (d == null && Campaign.DebugRank(k, out int dr)) r = dr;
                ranks[k] = Mathf.Clamp(r, 0, Max);
            }
            status = "読み込み元: " + from;
        }

        // the save to read: the session's (Play), else the file; null with neither
        static SaveData Source(out string from)
        {
            if (SaveSystem.Current != null) { from = "セーブ (プレイ中のセッション)"; return SaveSystem.Current; }
            // playing with no session: the match uses the debug values, so show those
            if (Application.isPlaying) { from = "デバッグ用の値 (セーブなしでプレイ中)"; return null; }
            var d = SaveSystem.Peek();
            from = d != null ? "セーブファイル" : "セーブなし (デバッグ用の値)";
            return d;
        }

        void OnGUI()
        {
            EditorGUILayout.LabelField("FLARE との協力ランク (0 … " + Max + ")", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(status, MessageType.None);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("全員 0")) SetAll(0);
                if (GUILayout.Button("全員 3")) SetAll(3);
                if (GUILayout.Button("全員 6")) SetAll(6);
                if (GUILayout.Button("全員 8")) SetAll(8);
                if (GUILayout.Button("全員 " + Max)) SetAll(Max);
            }
            scroll = EditorGUILayout.BeginScrollView(scroll);
            foreach (var k in keys) ranks[k] = EditorGUILayout.IntSlider(k, ranks[k], 0, Max);
            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space();
            if (GUILayout.Button("セーブに書き込む")) WriteSave();
            using (new EditorGUI.DisabledScope(!Application.isPlaying))
                if (GUILayout.Button("試合に反映 (プレイ中)")) ApplyToMatch();
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("デバッグ用に保存 (セーブなしの試合)")) SaveDebug();
                if (GUILayout.Button("デバッグ値を消す")) { PlayerPrefs.DeleteKey(Campaign.DebugRanksPref); PlayerPrefs.Save(); status = "デバッグ用の値を消しました"; }
            }
            if (GUILayout.Button("再読み込み")) Reload();
        }

        void SetAll(int r) { foreach (var k in keys) ranks[k] = r; }

        static int PointsFor(int rank) => rank <= 0 ? 0 : Campaign.CoopNeed[Mathf.Min(rank, Max) - 1];

        void Put(SaveData d)
        {
            foreach (var k in keys) Campaign.LinkOf(d, Campaign.Commander, k).points = PointsFor(ranks[k]);
        }

        void WriteSave()
        {
            if (SaveSystem.Current != null)
            {
                Put(SaveSystem.Current);
                status = SaveSystem.Write(false, notify: false) ? "セッションのセーブに書き込みました" : "書き込みに失敗しました";
                return;
            }
            if (Application.isPlaying) { status = "プレイ中ですがセーブのセッションがありません (デバッグ用に保存を使ってください)"; return; }
            if (!SaveSystem.Exists) { status = "セーブファイルがありません"; return; }
            if (!SaveSystem.Load(out string why)) { status = "読み込めません: " + why; return; }
            Put(SaveSystem.Current);
            bool ok = SaveSystem.Write(false, notify: false);
            SaveSystem.EndSession();
            status = ok ? "セーブファイルに書き込みました" : "書き込みに失敗しました";
        }

        void ApplyToMatch()
        {
            var mb = Object.FindAnyObjectByType<Blindside.Integration.MatchBootstrap>();
            if (mb == null || mb.ActiveUnits == null) { status = "試合が見つかりません"; return; }
            int n = 0;
            foreach (var u in mb.ActiveUnits)
            {
                if (u == null || u.Op == null || !ranks.TryGetValue(u.Op.key, out int r)) continue;
                u.CoopRank = r;
                if (u.Op.key == "BASTION" && r >= CoopPerks.Unique3 && u.Mv < 2) u.Mv = 2;
                n++;
            }
            status = n + " 人に反映しました (二丁持ち・ガジェット枠は次の試合から)";
        }

        void SaveDebug()
        {
            var sb = new StringBuilder();
            foreach (var k in keys) { if (sb.Length > 0) sb.Append(','); sb.Append(k).Append('=').Append(ranks[k]); }
            PlayerPrefs.SetString(Campaign.DebugRanksPref, sb.ToString());
            PlayerPrefs.Save();
            status = "デバッグ用に保存しました (セーブなしの試合はこのランクで始まります)";
        }
    }
}
