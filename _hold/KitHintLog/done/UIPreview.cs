// Blindside > UI > Render Previews — draws the title and the HUD in edit mode (no Play, no game logic: TurnManager, units and AI are
// never created) from sample data, through the same builders the game uses (TitleSelectScreenBuilder + TitleSelectScreen, the HUD
// prefabs, UnitPanelView / CommandMenuView / HintView / DialogView, HudWidgets). Each state is built in a throw-away additive scene
// on Screen Space Camera canvases, rendered into a RenderTexture at 1440×810 (menu canvases at UITheme.menuReference, HUD canvases at
// UITheme.hudReference — the scaler does the rest, as in the game) and saved as D:\Blind\UIPreview\{state}_1440x810.png, plus one
// sheet of all states (sheet_1440x810.png). Timed effects are posed with ITimedEffect.Evaluate(t).
// UILint builds the same states (UIPreview.States) and measures them.
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Blindside.Data;
using Blindside.UI;
using Blindside.UI.EditorTools;

namespace Blindside.UITools.EditorTools
{
    public static class UIPreview
    {
        public const int W = 1440, H = 810;
        public static string OutDir => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "UIPreview"));
        const string PrefabDir = "Assets/Blindside/Prefabs/UI/";
        const string OperatorDir = "Assets/Blindside/Data/Operators/";
        static readonly Color Backdrop = new Color32(0x2b, 0x2f, 0x35, 0xff);   // a plain floor-coloured field behind the HUD

        // ================================================================
        //  States
        // ================================================================
        public class State
        {
            public string name, label;
            public Action<Stage> build;
        }

        public static readonly State[] States =
        {
            new State { name = "title_attack", label = "タイトル — 攻撃選択", build = s => BuildTitle(s, OperatorTeam.Attack) },
            new State { name = "title_defense", label = "タイトル — 防衛選択", build = s => BuildTitle(s, OperatorTeam.Defense) },
            new State { name = UIPreviewKitTitleSquad.Name, label = "タイトル — スポットライト グリッチ (t=0.21s)", build = UIPreviewKitTitleSquad.Build },
            new State { name = UIPreviewKitTitleLeft.Name, label = "タイトル — ロゴ演出 (t=0.3s)", build = UIPreviewKitTitleLeft.Build },
            new State { name = "hud_selected", label = "HUD — 隊員選択 (CRUSHER)", build = s => BuildHud(s, HudState.Selected) },
            new State { name = "hud_unselected", label = "HUD — 選択解除", build = s => BuildHud(s, HudState.Unselected) },
            new State { name = "hud_hit", label = "HUD — 被弾直後 (FLARE, t=0.35s)", build = s => BuildHud(s, HudState.Hit) },
            new State { name = "hud_ended", label = "HUD — 全員行動終了", build = s => BuildHud(s, HudState.Ended) },
            new State { name = "hud_moved", label = "HUD — 移動後 (CRUSHER 4.2/7m)", build = s => BuildHud(s, HudState.Moved) },
            new State { name = "hud_prep", label = "HUD — 防衛準備 (PREPARATION, ROUND 00)", build = s => BuildHud(s, HudState.Prep) },
            new State { name = "hud_cmd_long", label = "HUD — 命令メニュー 最長の能力名 (FLARE ブリーチングランチャー)", build = s => BuildHud(s, HudState.CmdLong) },
            new State { name = "hud_cmd_disabled", label = "HUD — 命令メニュー 無効ボタン (CRUSHER 移動力0・能力0回)", build = s => BuildHud(s, HudState.CmdDisabled) },
            new State { name = "hud_longdesc", label = "HUD — 最長の能力説明 (VOLT)", build = s => BuildHud(s, HudState.LongDesc) },
            new State { name = "hud_hint_move", label = "HUD — 案内バー 移動中 + ログ", build = s => BuildHud(s, HudState.HintMove) },
            new State { name = "hud_hint_fire", label = "HUD — 案内バー 攻撃選択中", build = s => BuildHud(s, HudState.HintFire) },
            new State { name = "hud_hint_long", label = "HUD — 案内バー 長い補助文 (2行・高さ拡張)", build = s => BuildHud(s, HudState.HintLong) },
            new State { name = "hud_log_full", label = "HUD — ログ7行 (間隔詰め・2行折返し含む)", build = s => BuildHud(s, HudState.LogFull) },
            new State { name = "dialog_move", label = "移動確認ダイアログ", build = s => BuildHud(s, HudState.MoveDialog) },
            new State { name = "phase_banner", label = "フェーズバナー (t=0.6s)", build = s => BuildHud(s, HudState.Banner) },
            new State { name = UIPreviewKitShapes.Name, label = "KitShapes — 斜め図形 · 分節バー", build = UIPreviewKitShapes.Build },
            new State { name = UIPreviewKitGlitch.Name, label = "KitGlitch — カード出現グリッチ", build = UIPreviewKitGlitch.Build },
            new State { name = UIPreviewKitScreenFX.Name, label = "KitScreenFX — 画面効果", build = UIPreviewKitScreenFX.Build },
            new State { name = UIPreviewKitComponents.Name, label = "KitComponents — 共通コンポーネント", build = UIPreviewKitComponents.Build },
            new State { name = UIPreviewKitCards.Name, label = "KitCards — 隊員カード", build = UIPreviewKitCards.Build },
            new State { name = UIPreviewKitArtCard.Name, label = "KitArtCard — イラストカード", build = UIPreviewKitArtCard.Build },
        };

        /// <summary>The title (attack side) and the HUD (CRUSHER selected) as backgrounds for other previews.</summary>
        internal static void BuildTitleAttack(Stage s) => BuildTitle(s, OperatorTeam.Attack);
        internal static void BuildHudSelected(Stage s) => BuildHud(s, HudState.Selected);
        internal static void BuildHudLogFull(Stage s) => BuildHud(s, HudState.LogFull);
        internal static Color BackdropColor => Backdrop;

        public static string FileName(State st) => $"{st.name}_{W}x{H}.png";

        // ================================================================
        //  Stage: a throw-away additive scene, a camera with a RenderTexture, Screen Space Camera canvases
        // ================================================================
        public sealed class Stage : IDisposable
        {
            public readonly Scene scene;
            public readonly Camera cam;
            public readonly RenderTexture rt;
            public readonly List<Canvas> canvases = new List<Canvas>();
            readonly Dictionary<Canvas, UITheme.CanvasKind> kinds = new Dictionary<Canvas, UITheme.CanvasKind>();
            readonly Scene previousActive;
            public readonly int width, height;

            readonly List<GameObject> roots = new List<GameObject>();   // what Dispose removes when no scene of our own could be made

            /// <param name="ownScene">false: build as temporary objects in the active scene (a stage rendered inside another stage's
            /// build, where a second additive scene can't be made next to the first, unsaved one).</param>
            public Stage(int w, int h, Color clear, bool ownScene = true)
            {
                width = w; height = h;
                previousActive = SceneManager.GetActiveScene();
                if (ownScene)
                {
                    try
                    {
                        scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
                        SceneManager.SetActiveScene(scene);
                    }
                    catch (Exception e)
                    {
                        // e.g. an unsaved untitled scene is open: build in the open scene instead, as temporary objects removed on Dispose
                        Debug.LogWarning("[UIPreview] no additive scene (" + e.Message + ") — using temporary objects in the open scene.");
                        scene = default;
                    }
                }
                rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32) { antiAliasing = 1, name = "UIPreview" };
                var camGo = new GameObject("PreviewCamera", typeof(Camera));
                Track(camGo);
                cam = camGo.GetComponent<Camera>();
                cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = clear;
                cam.orthographic = true; cam.cullingMask = 1 << UILayer; cam.targetTexture = rt;
                // far from everything in the open scenes (a camera sees every loaded scene; only the UI layer is drawn, and nothing
                // of the game's sits out here) — the Screen Space Camera canvases follow the camera
                cam.transform.position = new Vector3(0f, -20000f, -10f);
            }

            void Track(GameObject go)
            {
                if (scene.IsValid()) return;
                go.hideFlags = HideFlags.DontSave;
                roots.Add(go);
            }

            public const int UILayer = 5;

            public Canvas NewCanvas(string name, UITheme.CanvasKind kind, int sortingOrder)
            {
                var go = new GameObject(name, typeof(RectTransform));
                Track(go);
                var c = go.AddComponent<Canvas>();
                c.renderMode = RenderMode.ScreenSpaceCamera; c.worldCamera = cam; c.planeDistance = 5f; c.sortingOrder = sortingOrder;
                UITheme.Current.ApplyTo(go.AddComponent<CanvasScaler>(), kind);
                canvases.Add(c); kinds[c] = kind;
                Scale(c);
                return c;
            }

            /// <summary>The scale factor the game's CanvasScaler gives this canvas at the stage size (Scale With Screen Size, match).</summary>
            public float ExpectedScale(Canvas c)
            {
                var theme = UITheme.Current;
                var refRes = theme.Reference(kinds[c]);
                float logW = Mathf.Log(width / refRes.x, 2f), logH = Mathf.Log(height / refRes.y, 2f);
                return Mathf.Pow(2f, Mathf.Lerp(logW, logH, theme.canvasMatch));
            }

            static readonly MethodInfo ScalerHandle = typeof(CanvasScaler).GetMethod("Handle", BindingFlags.Instance | BindingFlags.NonPublic);

            readonly HashSet<Canvas> fixedScale = new HashSet<Canvas>();

            /// <summary>Pins a canvas to a constant factor that Settle() leaves alone (the sheet: 1 canvas unit = 1 pixel).</summary>
            public void PinScale(Canvas c, float factor)
            {
                fixedScale.Add(c);
                var scaler = c.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize; scaler.scaleFactor = factor;
                if (ScalerHandle != null) ScalerHandle.Invoke(scaler, null);
            }

            void Scale(Canvas c)
            {
                if (fixedScale.Contains(c)) return;
                var scaler = c.GetComponent<CanvasScaler>();
                if (ScalerHandle != null) ScalerHandle.Invoke(scaler, null);
                float want = ExpectedScale(c);
                if (Mathf.Abs(c.scaleFactor - want) > .0005f)
                {
                    // edit mode, off-screen target: pin the factor the scaler would compute in the game
                    scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
                    scaler.scaleFactor = want;
                    if (ScalerHandle != null) ScalerHandle.Invoke(scaler, null);
                }
            }

            /// <summary>Puts everything on the UI layer and settles scale, layout and text meshes.</summary>
            public void Settle()
            {
                foreach (var c in canvases)
                {
                    SetLayer(c.transform, UILayer);
                    Scale(c);
                }
                for (int pass = 0; pass < 2; pass++)
                {
                    Canvas.ForceUpdateCanvases();
                    foreach (var c in canvases)
                    {
                        LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)c.transform);
                        foreach (var t in c.GetComponentsInChildren<TMP_Text>()) t.ForceMeshUpdate(true, true);
                    }
                }
                Canvas.ForceUpdateCanvases();
            }

            static void SetLayer(Transform t, int layer)
            {
                t.gameObject.layer = layer;
                foreach (Transform ch in t) SetLayer(ch, layer);
            }

            public Texture2D Render()
            {
                cam.Render();
                var prev = RenderTexture.active;
                RenderTexture.active = rt;
                var tex = new Texture2D(width, height, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                tex.Apply();
                RenderTexture.active = prev;
                return tex;
            }

            public void Dispose()
            {
                cam.targetTexture = null;
                rt.Release();
                UnityEngine.Object.DestroyImmediate(rt);
                foreach (var go in roots) if (go != null) UnityEngine.Object.DestroyImmediate(go);
                roots.Clear();
                if (!scene.IsValid()) return;
                if (previousActive.IsValid()) SceneManager.SetActiveScene(previousActive);
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        /// <summary>Builds `st` on a fresh stage and settles it (the caller renders / measures, then disposes).</summary>
        public static Stage Build(State st)
        {
            var stage = new Stage(W, H, Backdrop);
            try { st.build(stage); stage.Settle(); }
            catch { stage.Dispose(); throw; }
            return stage;
        }

        // ================================================================
        //  Render Previews
        // ================================================================
        [MenuItem("Blindside/UI/Render Previews")]
        public static void RenderMenu()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) { Debug.LogWarning("[UIPreview] edit mode only — stop Play first."); return; }
            var files = RenderAll();
            Debug.Log($"[UIPreview] {files.Count} files → {OutDir}\n" + string.Join("\n", files));
        }

        /// <summary>Renders every state and the sheet; returns the written paths.</summary>
        public static List<string> RenderAll()
        {
            Directory.CreateDirectory(OutDir);
            var written = new List<string>();
            var shots = new List<(State st, Texture2D tex)>();
            try
            {
                foreach (var st in States)
                {
                    using (var stage = Build(st))
                    {
                        var tex = stage.Render();
                        string path = Path.Combine(OutDir, FileName(st));
                        File.WriteAllBytes(path, tex.EncodeToPNG());
                        written.Add(path);
                        shots.Add((st, tex));
                    }
                }
                foreach (var size in new[] { new Vector2Int(W, H), new Vector2Int(2560, 1440) })
                {
                    using (var stage = new Stage(size.x, size.y, Backdrop))
                    {
                        UIPreviewFontTest.Build(stage); stage.Settle();
                        var tex = stage.Render();
                        string path = Path.Combine(OutDir, $"{UIPreviewFontTest.Name}_{size.x}x{size.y}.png");
                        File.WriteAllBytes(path, tex.EncodeToPNG());
                        written.Add(path);
                        UnityEngine.Object.DestroyImmediate(tex);
                    }
                }
                string sheet = Path.Combine(OutDir, $"sheet_{W}x{H}.png");
                File.WriteAllBytes(sheet, MakeSheet(shots).EncodeToPNG());
                written.Add(sheet);
            }
            finally { foreach (var s in shots) UnityEngine.Object.DestroyImmediate(s.tex); }
            return written;
        }

        /// <summary>All states on one page: two columns of half-size shots, each under its label.</summary>
        static Texture2D MakeSheet(List<(State st, Texture2D tex)> shots)
        {
            const int Cols = 2, LabelH = 30, Pad = 8;
            int cw = W / 2, ch = H / 2, rows = (shots.Count + Cols - 1) / Cols;
            int sw = Cols * cw + (Cols + 1) * Pad, sh = rows * (ch + LabelH) + (rows + 1) * Pad;
            using (var stage = new Stage(sw, sh, new Color32(0x11, 0x13, 0x16, 0xff)))
            {
                var canvas = stage.NewCanvas("Sheet", UITheme.CanvasKind.Menu, 0);
                stage.PinScale(canvas, 1f);
                for (int i = 0; i < shots.Count; i++)
                {
                    int col = i % Cols, row = i / Cols;
                    float x = Pad + col * (cw + Pad), y = Pad + row * (ch + LabelH + Pad);
                    var label = HudKit.AddText(canvas.transform, $"{shots[i].st.label}   ·   {FileName(shots[i].st)}", 14, Color.white, false, TextAlignmentOptions.MidlineLeft, "Label", UITheme.TextRole.Body);
                    UILayout.Place(label.rectTransform, x + 4, y, cw - 8, LabelH);
                    var img = new GameObject("Shot", typeof(RectTransform), typeof(RawImage));
                    img.transform.SetParent(canvas.transform, false);
                    img.GetComponent<RawImage>().texture = shots[i].tex;
                    UILayout.Place((RectTransform)img.transform, x, y + LabelH, cw, ch);
                }
                stage.Settle();
                return stage.Render();
            }
        }

        // ================================================================
        //  Title
        // ================================================================
        static void BuildTitle(Stage s, OperatorTeam team)
        {
            var canvas = s.NewCanvas("BlindsideCanvas", UITheme.CanvasKind.Menu, 0);
            var screen = TitleSelectScreenBuilder.BuildInto(canvas.transform);
            screen.ShowPreview(team);
        }

        // ================================================================
        //  HUD (sample data: the attack squad of the default match, round 1)
        // ================================================================
        enum HudState { Selected, Unselected, Hit, Ended, MoveDialog, Banner, LongDesc, Moved, Prep, CmdLong, CmdDisabled, HintMove, HintFire, HintLong, LogFull }

        // the operator with the longest ability description (VOLT, a defender), shown in the panel of the LongDesc state
        const string LongestDesc = "VOLT";

        static readonly string[] Squad = { "CRUSHER", "FLARE", "IGNIS", "SERUM", "BASTION" };
        static readonly string[] RosterIcons = { "▣", "◎", "✹", "✚", "⬢" };
        // the defenders (Prep state: the player plays the defense) and HudController's icons for them
        static readonly string[] DefenseSquad = { "VOLT", "MIASMA", "WRAITH", "OBSIDIAN", "ARGUS" };
        static readonly string[] DefenseIcons = { "✦", "✹", "⌖", "⬢", "◎" };

        class Unit
        {
            public OperatorData op; public int uid, hp; public float moveLeft; public bool ended;
            public int MoveRange => op.mv * 3 + 1;
        }

        static List<Unit> SampleSquad(HudState state)
        {
            var list = new List<Unit>();
            var keys = state == HudState.Prep ? DefenseSquad : Squad;
            for (int i = 0; i < keys.Length; i++)
            {
                var op = AssetDatabase.LoadAssetAtPath<OperatorData>($"{OperatorDir}Operator_{keys[i]}.asset");
                if (op == null) throw new Exception("missing operator asset " + keys[i]);
                var u = new Unit { op = op, uid = (state == HudState.Prep ? 11 : 6) + i, hp = op.hp, ended = state == HudState.Ended };
                u.moveLeft = u.MoveRange;
                list.Add(u);
            }
            if (state == HudState.Prep) return list;             // the defenders, untouched before the first round
            list[1].hp = state == HudState.Hit ? 64 : 82;      // FLARE: hurt in an earlier round (and just hit, in the hit state)
            list[3].hp = 88;                                   // SERUM: hurt
            return list;
        }

        static string M1(float v) => (Mathf.Floor(v * 10f + .5f) / 10f).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture);

        static GameObject Prefab(string name)
        {
            var p = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabDir + name + ".prefab");
            if (p == null) throw new Exception("missing prefab " + name);
            return p;
        }

        static void BuildHud(Stage s, HudState state)
        {
            var squad = SampleSquad(state);
            Unit selected = state == HudState.Selected || state == HudState.MoveDialog || state == HudState.HintMove || state == HudState.HintFire || state == HudState.HintLong || state == HudState.LogFull || state == HudState.Moved || state == HudState.CmdDisabled ? squad[0]
                : state == HudState.Hit || state == HudState.CmdLong ? squad[1] : null;
            if (state == HudState.Moved) squad[0].moveLeft = 4.2f;                // CRUSHER after a 2.8m move
            if (state == HudState.CmdDisabled) squad[0].moveLeft = 0f;            // moved all the way: 移動 / 攻撃 / 能力 are disabled
            Unit paneled = selected ?? (state == HudState.Ended ? squad[0] : null);
            if (state == HudState.LongDesc)
            {
                var op = AssetDatabase.LoadAssetAtPath<OperatorData>($"{OperatorDir}Operator_{LongestDesc}.asset");
                if (op == null) throw new Exception("missing operator asset " + LongestDesc);
                paneled = new Unit { op = op, uid = 16, hp = op.hp };
                paneled.moveLeft = paneled.MoveRange;
            }

            // ---- BlindsideHUD (topbar, roster, log) ----
            var hudCanvas = s.NewCanvas("BlindsideHUD", UITheme.CanvasKind.Hud, -1);
            var content = HudKit.NewRect("HUD", hudCanvas.transform);
            HudKit.Stretch(content);
            var log = ((GameObject)PrefabUtility.InstantiatePrefab(Prefab("HUD_Log"), content)).GetComponent<Blindside.UI.KitLogView>();
            var roster = ((GameObject)PrefabUtility.InstantiatePrefab(Prefab("HUD_Roster"), content)).GetComponent<KitRoster>();
            var topbar = ((GameObject)PrefabUtility.InstantiatePrefab(Prefab("HUD_Topbar"), content)).GetComponent<KitTopbar>();
            topbar.Build();
            var icons = state == HudState.Prep ? DefenseIcons : RosterIcons;

            // the same calls HudController makes (SetData once, SetState on every refresh); the hit: FLARE 82 → 64, posed .35s in
            var cards = roster.Build(squad.Count);
            for (int i = 0; i < cards.Count; i++)
            {
                var u = squad[i]; var op = u.op;
                bool my = state != HudState.Hit;               // HudController: the move chip only in the squad's own phase
                int before = state == HudState.Hit && i == 1 ? 82 : u.hp;
                cards[i].SetData(OperatorArt.Face(op.key), op.key, op.kr, op.color, before, op.hp, $"移動 {M1(u.moveLeft)}m", icons[i]);
                cards[i].SetState(u.hp, op.hp, my ? $"移動 {M1(u.moveLeft)}m" : "", u == selected && my, u.ended && my, false);
                cards[i].Evaluate(state == HudState.Hit ? .35f : 1f);
            }

            // the hit lands in the defenders' phase (HudController: "防衛チームの番 · 敵", foe tone); every other state is the player's phase
            // the same calls HudController makes; the defense preparation: round 00, yellow marker, no attackers yet (5 dark pips)
            var five = new List<bool> { true, true, true, true, true };
            if (state == HudState.Prep)
            {
                topbar.SetPhase("PREPARATION", "防衛準備フェーズ", KitTopbar.Tone.Prep);
                topbar.SetRound(0, 15);
                topbar.SetMode("防衛チーム · 人質死守作戦");
                topbar.SetObjective("① 15ラウンド終了まで人質を死守\n② または攻撃チームを全員制圧", five, new List<bool> { false, false, false, false, false });
            }
            else
            {
                if (state == HudState.Hit) topbar.SetPhase("DEFENDER PHASE", "防衛チームの番 · 敵", KitTopbar.Tone.Foe);
                else topbar.SetPhase("ATTACKER PHASE", "攻撃チームの番 · 味方", KitTopbar.Tone.Ally);
                topbar.SetRound(1, 15);
                topbar.SetMode("攻撃チーム · 人質救出作戦");
                topbar.SetObjective("① 人質を救出地点4か所(EXFIL A〜D)のいずれかまで護送\n② または防衛チームを全員制圧", five, five);
            }

            log.PreviewLine("作戦開始 — 攻撃チームが建物の外に降下", Blindside.UI.HudLogType.Ally);
            log.PreviewLine("クラッシャー: 壁を破壊 (2,9)", Blindside.UI.HudLogType.Ally);
            if (state == HudState.Hit) log.PreviewLine("アルゴス → フレア: 2/3 命中, 36 ダメージ (ヘッドショット ×1)", Blindside.UI.HudLogType.Foe);
            else log.PreviewLine("敵の銃声を確認 — 北東の部屋", Blindside.UI.HudLogType.Warn);
            if (state == HudState.LogFull)
            {
                // a full log: a line that only fits with the spacing narrowed, and two that wrap to two lines
                log.PreviewLine("壁を強化 — この壁 2/3 強化完了 (残り強化 8)", Blindside.UI.HudLogType.Ally);
                log.PreviewLine("射撃可能な味方がいません (行動力の消費なし · 視界内に隊員が必要)", Blindside.UI.HudLogType.Warn);
                log.PreviewLine("ヴァイパー → クラッシャー: 3/3 命中, 54 ダメージ (ヘッドショット ×2)", Blindside.UI.HudLogType.Foe);
                log.PreviewLine("CAM-02: 敵 1名を捕捉", Blindside.UI.HudLogType.Normal);
            }

            // ---- GameplayUIBridge_Canvas (banner, contact, headshot, END TURN) ----
            var bridgeCanvas = s.NewCanvas("GameplayUIBridge_Canvas", UITheme.CanvasKind.Hud, 0);
            var banner = HudWidgets.BuildPhaseBanner(bridgeCanvas.transform);
            HudWidgets.BuildContactAlert(bridgeCanvas.transform).group.alpha = 0f;
            var headshot = HudWidgets.BuildHeadshotBadge(bridgeCanvas.transform, bridgeCanvas);
            headshot.group.alpha = 0f;
            var end = HudWidgets.BuildEndTurn(bridgeCanvas.transform, null);
            end.button.gameObject.SetActive(true);
            end.main.text = "END TURN"; end.sub.text = "ターン終了";
            end.pulse.SetOn(state == HudState.Ended);
            banner.group.alpha = 0f;

            if (state == HudState.Banner)
            {
                banner.SetContent("ATTACKER PHASE", "攻撃ターン", "部隊を展開してください", UIPalette.Blue);
                banner.Evaluate(.6f);
            }
            if (state == HudState.Hit)
            {
                headshot.SetContent(36, true);
                headshot.badge.anchoredPosition = new Vector2(-120f, 60f);
                headshot.Evaluate(.35f);
            }

            // ---- PlayerSelection_Canvas (art card, unit panel, hint, menu, dialog) ----
            var selCanvas = s.NewCanvas("PlayerSelection_Canvas", UITheme.CanvasKind.Hud, 5);
            // the same calls PlayerSelectionController makes: instantiate the prefab, Build, Show (posed at the end of the entrance)
            var art = UnityEngine.Object.Instantiate(Prefab("HUD_OperatorArtCard"), selCanvas.transform, false).GetComponent<KitArtCard>();
            art.name = "OpArt";
            art.Build();
            if (selected != null && state != HudState.Hit)
            {
                var op = selected.op;
                art.Show(OperatorArt.Body(op.key), op.key, $"{op.kr} · {op.role}", op.abilityName, $"OPERATOR FILE // OP-{selected.uid:000} // ASSAULT", op.color);
                art.Evaluate(art.Duration);
            }

            // the same calls PlayerSelectionController makes: instantiate the prefab, Build, Show (posed at the end of the entrance)
            var panel = UnityEngine.Object.Instantiate(Prefab("HUD_UnitPanel"), selCanvas.transform, false).GetComponent<KitUnitPanel>();
            panel.name = "UnitPanel";
            panel.Build();
            if (paneled != null) panel.Show(PanelModel(paneled, state == HudState.LongDesc), false);

            var moveHud = HudKit.NewRect("MoveHud", selCanvas.transform);
            HudKit.Stretch(moveHud);
            var hint = KitHintBar.Build(moveHud);
            // the same calls PlayerSelectionController makes: instantiate the prefab, Build, Render (posed at the end of the entrance)
            var menu = KitCommandMenu.Build(selCanvas.transform, Prefab("HUD_CmdMenu"));
            var dialog = DialogView.Build(selCanvas.transform, null, null);
            var cancelChip = new List<HintButton> { new HintButton("cancel", "ESC キャンセル", false) };
            if (state == HudState.HintMove)
                hint.Set("移動経路を自由に描いてください", $"左クリックを押したままドラッグして経路を描く · 1回クリックで自動経路 · 移動力 {M1(selected.moveLeft)}m", cancelChip, null);
            if (state == HudState.HintFire)
                hint.Set("攻撃方向を選択してください", "マウスで狙いを定め、左クリックで射撃 · 強化壁を除く壁は貫通 (命中率低下)", cancelChip, null);
            if (state == HudState.HintLong)
                hint.Set("ドローンの移動経路を描いてください", $"ドローン周囲の円形視界 · 発見した敵は今ターンの間マップに表示 · 移動力 {M1(selected.moveLeft)}m", cancelChip, null);
            if (state == HudState.LogFull)
                hint.Set("配置を確認したら[作戦開始]を押してください", "配置した隊員をクリックすると、再度降下させられます", new List<HintButton>(), null);

            if (state == HudState.Selected || state == HudState.CmdLong || state == HudState.CmdDisabled)
            {
                menu.Render($"{selected.op.key} // COMMAND", selected.op.color, Commands(selected, state == HudState.CmdDisabled), false, null);
                menu.Root.anchoredPosition = new Vector2(340f, -150f);     // the mockup's #cmd{left:340px;top:150px}; in the game it follows the unit
            }
            if (state == HudState.MoveDialog)
            {
                hint.Set("移動経路を自由に描いてください", $"左クリックを押したままドラッグして経路を描く · 1回クリックで自動経路 · 移動力 {M1(selected.moveLeft)}m",
                    new List<HintButton> { new HintButton("cancel", "ESC キャンセル", false) }, null);
                dialog.Open("移動しますか？", "CONFIRM MOVE", $"{selected.op.kr} · 経路 <b>4.2</b>m · 移動後の残り移動力 {M1(selected.moveLeft - 4.2f)}m", "はい", "いいえ");
            }
        }

        /// <summary>The unit panel's texts for a sample unit — the same formatting as PlayerSelectionController.PanelModel.</summary>
        static UnitPanelModel PanelModel(Unit u, bool defense = false)
        {
            var op = u.op; var w = op.weapon;
            var m = new UnitPanelModel { key = op.key, color = op.color, foe = false };
            string wName = w != null ? w.weaponName : "-";
            m.code = $"OP-{u.uid:000} // {(defense ? "DEFENSE" : "ASSAULT")} // {wName.Split(' ')[0]}";
            m.krRich = $"{op.kr}<font-weight=500><color=#FFFFFF99>  {op.role} · {(defense ? "防衛チーム" : "攻撃チーム")}</color></font-weight>";
            m.hp = u.hp; m.maxHp = op.hp;
            m.grid[0] = $"{M1(u.moveLeft)}<size=11><color=#9A9C9E>/{u.MoveRange}m</color></size>";
            m.grid[1] = op.mv.ToString();
            m.grid[2] = w != null ? $"{w.damage}×{w.shots}" : "-";
            m.grid[3] = w != null ? w.accuracy.ToString() : "-";
            m.grid[4] = w != null ? w.range.ToString() : "-";
            m.weapon = wName + (w != null && w.breach ? " · 近接射撃で通常の壁を破壊" : "");
            m.abilityName = op.abilityName;
            m.charges = op.charges >= 99 ? "∞" : new string('●', Mathf.Max(0, op.charges));
            m.description = op.description;
            if (u.ended) m.AddTag("行動終了", UIPalette.Ink, Color.white);
            return m;
        }

        /// <summary>commands(u) for a fresh attacker in round 1 (nothing used yet) — the entries PlayerSelectionController.Commands lists.</summary>
        static List<CommandModel> Commands(Unit u, bool spent = false)
        {
            var op = u.op;
            int charges = spent ? 0 : op.charges;                 // spent: the attack and the ability are used up
            return new List<CommandModel>
            {
                new CommandModel { id = "move", kr = "移動", en = "MOVE", ic = "➜", ok = u.moveLeft > .3f, sub = M1(u.moveLeft) + "m" },
                new CommandModel { id = "face", kr = "視界変更", en = "VIEW", ic = "◐", ok = true, sub = "" },
                new CommandModel { id = "fire", kr = "攻撃", en = "FIRE", ic = "✛", ok = !spent },
                new CommandModel { id = "skill", kr = op.abilityName, en = "ABILITY", ic = "✦", ok = charges > 0, sub = charges >= 99 ? "∞" : charges + "回" },
                new CommandModel { id = "gadget", kr = "ドローン", en = "DRONE", ic = "⌖", ok = true, sub = "展開" },
                new CommandModel { id = "wait", kr = "待機", en = "END", ic = "■", ok = true },
            };
        }
    }
}
