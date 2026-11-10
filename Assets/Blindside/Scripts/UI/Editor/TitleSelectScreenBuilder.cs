// =====================================================================
//  BLINDSIDE UI porting — Title / faction-select screen build tool.
//  Menu: Blindside > UI > Build Title Select Screen
//  Builds the pre-match screen ported from #title in 00_markup_and_styles.html
//  and initTitle()/renderSquad()/startGame() in 05_ui_input_intro.js: attack/
//  defense side cards, that side's 5-operator squad list, the win-condition
//  box and the DEPLOY button — wired to the existing IntroSequence timeline
//  and MatchBootstrap so pressing DEPLOY starts the already-built game logic
//  instead of it auto-running on scene Start.
//  Editor-only: lives under an "Editor" folder so it never ships in builds.
// =====================================================================
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.UI;
using TMPro;
using Blindside.Data;
using Blindside.Integration;

namespace Blindside.UI.EditorTools
{
    public static class TitleSelectScreenBuilder
    {
        const string PanelSpritePath = "Assets/Blindside/Art/UI/Panel_DiagonalCut9.png";
        const string OperatorDir = "Assets/Blindside/Data/Operators/";
        static readonly string[] OperatorKeys =
        {
            "ARGUS", "BASTION", "CRUSHER", "FLARE", "IGNIS", "MIASMA", "OBSIDIAN", "SERUM", "VOLT", "WRAITH",
        };

        static readonly Color32 Bg = new Color32(0x0a, 0x0c, 0x0f, 0xff);       // --bg
        static readonly Color32 Ink = new Color32(0x14, 0x16, 0x1a, 0xff);      // --ink
        static readonly Color32 Ink2 = new Color32(0x23, 0x27, 0x2c, 0xf0);     // --ink2
        static readonly Color32 Paper = new Color32(0xec, 0xee, 0xf0, 0xff);    // --paper
        static readonly Color32 Txt = new Color32(0xee, 0xf1, 0xf4, 0xff);      // --txt
        static readonly Color32 Dim = new Color32(0x83, 0x8a, 0x93, 0xff);      // --dim
        static readonly Color32 Blu = new Color32(0x1f, 0x96, 0xe8, 0xff);      // --blu
        static readonly Color32 Acc = new Color32(0xff, 0xc8, 0x1e, 0xff);      // --acc

        const string DashSpritePath = "Assets/Blindside/Art/UI/Deploy_Dash.png";

        [MenuItem("Blindside/UI/Build Title Select Screen")]
        public static void Build()
        {
            var canvasGo = GameObject.Find("BlindsideCanvas");
            if (canvasGo == null) { Debug.LogError("[TitleSelectScreenBuilder] BlindsideCanvas not found in the open scene."); return; }

            var existing = canvasGo.transform.Find("TitleSelect");
            if (existing != null) Object.DestroyImmediate(existing.gameObject);

            var screen = BuildInto(canvasGo.transform);
            var root = screen.gameObject;
            root.transform.SetAsLastSibling(); // draw on top of the intro (Fade/Letterbox/Title/Caption)

            var introGo = GameObject.Find("IntroSequence");
            var director = introGo != null ? introGo.GetComponent<PlayableDirector>() : null;
            if (director != null) { director.playOnAwake = false; screen.introDirector = director; }
            else Debug.LogWarning("[TitleSelectScreenBuilder] IntroSequence PlayableDirector not found — DEPLOY will skip straight to the match.");

            var bootstrapGo = GameObject.Find("MatchBootstrap");
            var bootstrap = bootstrapGo != null ? bootstrapGo.GetComponent<MatchBootstrap>() : null;
            if (bootstrap != null)
            {
                bootstrap.runOnStart = false;
                bootstrapGo.SetActive(true);
                screen.matchBootstrap = bootstrap;
            }
            else Debug.LogWarning("[TitleSelectScreenBuilder] MatchBootstrap not found — DEPLOY will not start a match.");

            if (screen.operators.Length != 10)
                Debug.LogWarning($"[TitleSelectScreenBuilder] Expected 10 OperatorData assets, found {screen.operators.Length}.");

            EditorUtility.SetDirty(root);
            EditorSceneManager.MarkSceneDirty(canvasGo.scene);
            Debug.Log("[TitleSelectScreenBuilder] Built TitleSelect under BlindsideCanvas.");
        }

        /// <summary>Builds the TitleSelect hierarchy (and its TitleSelectScreen, operators loaded) under `canvas` — no scene wiring
        /// (intro, MatchBootstrap). Build() uses it for the scene; the UI previews use it for a throw-away canvas.</summary>
        public static TitleSelectScreen BuildInto(Transform canvas)
        {
            var panelSprite = AssetDatabase.LoadAssetAtPath<Sprite>(PanelSpritePath);

            var root = new GameObject("TitleSelect", typeof(RectTransform));
            root.transform.SetParent(canvas, false);
            Stretch(root.GetComponent<RectTransform>());
            var screenGroup = root.AddComponent<CanvasGroup>();

            var bg = NewImage(root.transform, "BG", Bg);
            Stretch(bg.rectTransform);

            BuildLeftColumn(root.transform);
            BuildRightColumn(root.transform, panelSprite,
                out Button attackBtn, out Button defenseBtn, out Image attackBg, out Image defenseBg,
                out TMP_Text conditionsText, out TMP_Text footnoteText, out RectTransform squadList, out Button startBtn, out RectTransform mapRow);

            var screen = root.AddComponent<TitleSelectScreen>();
            screen.operators = OperatorKeys
                .Select(k => AssetDatabase.LoadAssetAtPath<OperatorData>($"{OperatorDir}Operator_{k}.asset"))
                .Where(o => o != null).ToArray();
            screen.attackCardButton = attackBtn;
            screen.defenseCardButton = defenseBtn;
            screen.attackCardBg = attackBg;
            screen.defenseCardBg = defenseBg;
            screen.conditionsText = conditionsText;
            screen.footnoteText = footnoteText;
            screen.squadListContent = squadList;
            screen.mapRow = mapRow;
            screen.panelSprite = panelSprite;
            screen.startButton = startBtn;
            screen.screenGroup = screenGroup;
            AttachKitTop(screen);
            AttachKitSquad(screen);
            AttachKitLeft(screen);
            return screen;
        }

        /// <summary>The kit's left column (only read here — this builder never writes it).</summary>
        public const string KitLeftPrefab = "Assets/Blindside/Prefabs/UI/TITLE_Left.prefab";

        /// <summary>Puts the kit left column + background decoration (TITLE_Left.prefab) right after the old Left column, which stays
        /// (inactive). Idempotent.</summary>
        public static void AttachKitLeft(TitleSelectScreen screen)
        {
            var root = screen.transform;
            var left = root.Find("Left");
            if (left != null) left.gameObject.SetActive(false);
            if (screen.kitLeft != null) return;
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(KitLeftPrefab);
            if (asset == null) { Debug.LogWarning("[TitleSelectScreenBuilder] " + KitLeftPrefab + " not found — the old left column stays hidden."); return; }
            var go = (GameObject)PrefabUtility.InstantiatePrefab(asset, root);
            go.transform.SetSiblingIndex(left != null ? left.GetSiblingIndex() + 1 : 1);
            screen.kitLeft = go.GetComponent<KitTitleLeft>();
        }

        /// <summary>The kit's squad roster and spotlight (only read here — this builder never writes them).</summary>
        public const string KitSquadPrefab = "Assets/Blindside/Prefabs/UI/TITLE_Squad.prefab";
        public const string KitSpotPrefab = "Assets/Blindside/Prefabs/UI/TITLE_Spotlight.prefab";

        /// <summary>Puts the kit squad roster (TITLE_Squad.prefab, header + rows) and the spotlight (TITLE_Spotlight.prefab) on the title:
        /// the old Right/SquadHeader and Right/SquadList stay (inactive, still wired: RenderSquad keeps filling the list). Idempotent.</summary>
        public static void AttachKitSquad(TitleSelectScreen screen)
        {
            var root = screen.transform;
            var right = root.Find("Right");
            if (right != null)
                foreach (var n in new[] { "SquadHeader", "SquadList" })
                {
                    var t = right.Find(n);
                    if (t != null) t.gameObject.SetActive(false);
                }
            int at = screen.kitTop != null ? screen.kitTop.transform.GetSiblingIndex() + 1 : root.childCount;
            if (screen.kitSquad == null)
            {
                var asset = AssetDatabase.LoadAssetAtPath<GameObject>(KitSquadPrefab);
                if (asset == null) Debug.LogWarning("[TitleSelectScreenBuilder] " + KitSquadPrefab + " not found — the old squad list stays hidden.");
                else
                {
                    var go = (GameObject)PrefabUtility.InstantiatePrefab(asset, root);
                    go.transform.SetSiblingIndex(at);
                    screen.kitSquad = go.GetComponent<KitTitleSquad>();
                }
            }
            if (screen.kitSpot == null)
            {
                var asset = AssetDatabase.LoadAssetAtPath<GameObject>(KitSpotPrefab);
                if (asset == null) Debug.LogWarning("[TitleSelectScreenBuilder] " + KitSpotPrefab + " not found — no spotlight.");
                else
                {
                    var go = (GameObject)PrefabUtility.InstantiatePrefab(asset, root);
                    go.transform.SetSiblingIndex(Mathf.Min(at + 1, root.childCount - 1));
                    screen.kitSpot = go.GetComponent<KitSpotlight>();
                }
            }
        }

        /// <summary>The kit's upper right column (only read here — this builder never writes it).</summary>
        public const string KitTopPrefab = "Assets/Blindside/Prefabs/UI/TITLE_Top.prefab";

        /// <summary>Puts the kit side cards / map row / win condition / DEPLOY (TITLE_Top.prefab) over the right column: the old
        /// Right/SideCards, Right/MapRow and Right/WinStartRow stay (inactive, still wired to the screen) and a KitTopSpacer of the same
        /// height keeps the squad header and list where they were. Idempotent.</summary>
        public static void AttachKitTop(TitleSelectScreen screen)
        {
            var root = screen.transform;
            var right = root.Find("Right") as RectTransform;
            if (right == null) return;
            int first = -1;
            foreach (var n in new[] { "SideCards", "MapRow", "WinStartRow" })
            {
                var t = right.Find(n);
                if (t == null) continue;
                if (first < 0) first = t.GetSiblingIndex();
                t.gameObject.SetActive(false);
            }
            var spacer = right.Find("KitTopSpacer") as RectTransform;
            if (spacer == null) { spacer = NewRect(right, "KitTopSpacer"); spacer.SetSiblingIndex(Mathf.Max(0, first)); }
            UILayout.Fixed(spacer, KitTitleTop.W, KitTitleTop.H);     // 176 + 8 + 74 + 8 + 106: the three rows it stands in for
            if (screen.kitTop != null) return;
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(KitTopPrefab);
            if (asset == null) { Debug.LogWarning("[TitleSelectScreenBuilder] " + KitTopPrefab + " not found — the old right column stays hidden."); return; }
            var go = (GameObject)PrefabUtility.InstantiatePrefab(asset, root);
            go.transform.SetSiblingIndex(right.GetSiblingIndex() + 1);
            screen.kitTop = go.GetComponent<KitTitleTop>();
        }

        // Geometry follows the mockup's #title at the menu reference (UITheme.menuReference, 1440×810) and UITheme.title: every box
        // has a fixed size, and the layout groups only stack (UILayout.Free — no child size control, no force expand).

        // .tl{left:64px;top:92px;width:600px} — .tcode, .logo, .lbar, .tsub, .meta 16px apart; .help at top:676px
        static Transform BuildLeftColumn(Transform parent)
        {
            var left = NewRect(parent, "Left");
            UILayout.Place(left, 64, 92, 600, 690);
            UILayout.Column(left, 16);

            BuildCodeBadge(left);

            var logo = Text(left, "BLINDSIDE", 118, FontStyles.Bold, TextAlignmentOptions.TopLeft, Txt);
            UILayout.LineHeight(logo, .95f);
            Size(logo, 600, 112);                                       // .logo{font:700 118px/.95}

            BuildLogoBar(left);

            var sub = Text(left, "壁の向こうは見えない。視界を開き、先に発見し、先に撃て。\n上から見下ろす3D戦場で繰り広げられる、5対5の人質救出作戦。", 16, FontStyles.Normal, TextAlignmentOptions.TopLeft, new Color32(0xc4, 0xcb, 0xd3, 0xff));
            sub.textWrappingMode = TextWrappingModes.Normal;
            UILayout.LineHeight(sub, 1.75f);                             // .tsub{font:500 16px/1.75}
            Size(sub, 600, 56);

            var meta = NewRect(left, "Meta");
            UILayout.Fixed(meta, 600, 65);
            UILayout.Row(meta, 10);                                      // .meta{display:flex;gap:10px}
            BuildStatBlock(meta, "SECTOR", "SUB-07");
            BuildStatBlock(meta, "HOSTAGE", "01");
            BuildStatBlock(meta, "HOSTILES", "05");
            BuildStatBlock(meta, "TIME LIMIT", "15R");

            var help = Text(left, "操作 — 左クリック: 隊員の選択・コマンド / 移動は押したままドラッグで経路を描く(クリックで自動経路) / 何もない所のドラッグ・右ドラッグ・WASD: カメラ移動 / ホイール: ズーム / 数字キー: コマンド / ESC・右クリック: キャンセル / Q: 次の隊員", 12.5f, FontStyles.Normal, TextAlignmentOptions.TopLeft, Dim);
            help.textWrappingMode = TextWrappingModes.Normal;
            UILayout.LineHeight(help, 1.5f);
            UILayout.Place(help.rectTransform, 15, 676, 645, 40);         // .help{top:676px;width:660px;padding-left:12px + 3px border}

            return left;
        }

        // .tcode{gap:10px;font:700 12px/1.25} span{padding:3px 10px} em{padding:2px 8px}
        static void BuildCodeBadge(Transform parent)
        {
            var row = NewRect(parent, "Code");
            UILayout.Fixed(row, 600, 21);
            UILayout.Row(row, 10, null, TextAnchor.MiddleLeft);

            var tac = NewImage(row, "Tac", Blu);
            var tacText = Text(tac.transform, "TAC-OS", 12, FontStyles.Bold, TextAlignmentOptions.Center, Color.white);
            UILayout.Fixed(tac.rectTransform, Mathf.Ceil(Width(tacText)) + 20, 21);
            Stretch(tacText.rectTransform);

            var mid = Text(row, "TACTICAL BREACH SIMULATION", 12, FontStyles.Normal, TextAlignmentOptions.MidlineLeft, Dim);
            Size(mid, Mathf.Ceil(Width(mid)) + 2, 21);

            var build = NewImage(row, "Build", new Color(1f, 1f, 1f, 0.08f));
            var buildText = Text(build.transform, "BUILD 0.6", 12, FontStyles.Normal, TextAlignmentOptions.Center, Txt);
            UILayout.Fixed(build.rectTransform, Mathf.Ceil(Width(buildText)) + 16, 21);
            Stretch(buildText.rectTransform);
        }

        // .lbar{height:10px} grows to width:400px (blue 30% | gap 2% | white)
        static void BuildLogoBar(Transform parent)
        {
            var bar = NewRect(parent, "LogoBar");
            UILayout.Fixed(bar, 400, 10);
            var blueSeg = NewImage(bar, "Blue", Blu);
            UILayout.Place(blueSeg.rectTransform, 0, 0, 120, 10);
            var whiteSeg = NewImage(bar, "White", new Color(1f, 1f, 1f, 0.7f));
            UILayout.Place(whiteSeg.rectTransform, 128, 0, 272, 10);
        }

        static Sprite EnsureDashSprite()
        {
            var existing = AssetDatabase.LoadAssetAtPath<Sprite>(DashSpritePath);
            if (existing != null) return existing;

            const int w = 18, h = 4;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            var px = new Color32[w * h];
            var clear = new Color32(0, 0, 0, 0);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    px[y * w + x] = x < 12 ? Acc : clear;
            tex.SetPixels32(px);
            tex.Apply();

            var absPath = Application.dataPath + "/" + DashSpritePath.Substring("Assets/".Length);
            System.IO.File.WriteAllBytes(absPath, tex.EncodeToPNG());
            AssetDatabase.ImportAsset(DashSpritePath);

            var importer = (TextureImporter)AssetImporter.GetAtPath(DashSpritePath);
            importer.textureType = TextureImporterType.Sprite;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Point;
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.SaveAndReimport();

            return AssetDatabase.LoadAssetAtPath<Sprite>(DashSpritePath);
        }

        // .meta div{padding:8px 22px 8px 16px;border-top:3px} label{11px/1.25} b{26px/1.25}
        static void BuildStatBlock(Transform parent, string label, string value)
        {
            var block = NewRect(parent, label);
            var labelText = Text(block, label, 11, FontStyles.Normal, TextAlignmentOptions.TopLeft, Dim);
            var valueText = Text(block, value, 26, FontStyles.Bold, TextAlignmentOptions.TopLeft, Txt);
            float w = Mathf.Ceil(Mathf.Max(Width(labelText), Width(valueText))) + 2;
            UILayout.Fixed(block, 16 + w + 22, 65);
            UILayout.Place(labelText.rectTransform, 16, 11, w, 14);
            UILayout.Place(valueText.rectTransform, 16, 25, w, 32);
        }

        // .tr{left:720px;top:34px;width:696px}: .sides (176) · .row3 (74) · .row2 (106) · .sqh · #squad, 8px apart
        static Transform BuildRightColumn(Transform parent, Sprite panelSprite,
            out Button attackBtn, out Button defenseBtn, out Image attackBg, out Image defenseBg,
            out TMP_Text conditionsText, out TMP_Text footnoteText, out RectTransform squadList, out Button startBtn, out RectTransform mapRow)
        {
            var T = UITheme.Current.title;
            var right = NewRect(parent, "Right");
            UILayout.Place(right, 720, 34, T.squadRow.x, 760);
            UILayout.Column(right, 8);

            // -- .sides --
            var cardRow = NewRect(right, "SideCards");
            UILayout.Fixed(cardRow, T.squadRow.x, T.sideCard.y);
            UILayout.Row(cardRow, 10);
            attackBg = BuildSideCard(cardRow, panelSprite, "SIDE-A", "攻撃", "ASSAULT TEAM",
                "建物の外へロープで降下し、壁を破壊し、ドローンで偵察しながら人質を護送します。", out attackBtn);
            defenseBg = BuildSideCard(cardRow, panelSprite, "SIDE-B", "防衛", "DEFENSE TEAM",
                "人質のいる部屋で一緒に開始し、壁を強化して制限時間まで人質を守り抜きます。", out defenseBtn);

            // -- .row3: the map card + the editor button (both built at runtime by TitleSelectScreen) --
            mapRow = NewRect(right, "MapRow");
            UILayout.Fixed(mapRow, T.squadRow.x, T.mapCard.y);
            UILayout.Row(mapRow, 10);

            // -- .row2: win condition + DEPLOY --
            var row2 = NewRect(right, "WinStartRow");
            UILayout.Fixed(row2, T.squadRow.x, T.winCondition.y);
            UILayout.Row(row2, 10);

            // .wb{width:343px;height:106px;padding:9px 30px 0 34px} .chip{12px; padding 3px 10px} div.l{13px/1.4} small{12px/1.3}
            var winBoxGo = NewImage(row2, "WinBox", Ink2);
            winBoxGo.sprite = panelSprite; winBoxGo.type = Image.Type.Sliced;
            UILayout.Fixed(winBoxGo.rectTransform, T.winCondition.x, T.winCondition.y);
            float wbW = T.winCondition.x - 34 - 30;

            var badgeGo = NewImage(winBoxGo.transform, "Badge", Ink);
            var badgeText = Text(badgeGo.transform, "VICTORY // どちらか一方を達成", 12, FontStyles.Bold, TextAlignmentOptions.Center, Color.white);
            UILayout.Place(badgeGo.rectTransform, 34, 9, Mathf.Ceil(Width(badgeText)) + 20, 21);
            Stretch(badgeText.rectTransform);

            conditionsText = Text(winBoxGo.transform, "", 13, FontStyles.Normal, TextAlignmentOptions.TopLeft, Txt);
            conditionsText.textWrappingMode = TextWrappingModes.Normal;
            UILayout.LineHeight(conditionsText, 1.4f);
            UILayout.Place(conditionsText.rectTransform, 34, 31, wbW, 37);

            footnoteText = Text(winBoxGo.transform, "", 12, FontStyles.Normal, TextAlignmentOptions.TopLeft, new Color32(0x9a, 0xa0, 0xa8, 0xff));
            footnoteText.textWrappingMode = TextWrappingModes.Normal;
            UILayout.LineHeight(footnoteText, 1.3f);
            UILayout.Place(footnoteText.rectTransform, 34, 69, wbW, 32);

            // .start{width:343px;height:106px} .t{padding:16px 0 0 42px} .en{18px/1.25} .kr{38px/1.1} .ar{width:86px;34px}
            var startGo = NewImage(row2, "StartButton", Ink);
            UILayout.Fixed(startGo.rectTransform, T.startButton.x, T.startButton.y);
            startBtn = startGo.gameObject.AddComponent<Button>();
            var startColors = startBtn.colors; startColors.highlightedColor = new Color(1, 1, 1, 0.9f); startBtn.colors = startColors;
            const float ArrowW = 86f;

            var dash = NewImage(startGo.transform, "Dash", Color.white);
            dash.sprite = EnsureDashSprite(); dash.type = Image.Type.Tiled; dash.raycastTarget = false;
            var dashRt = dash.rectTransform;                              // .start::before{left:0;right:86px;bottom:0;height:8px}
            dashRt.anchorMin = new Vector2(0, 0); dashRt.anchorMax = new Vector2(1, 0); dashRt.pivot = new Vector2(0.5f, 0);
            dashRt.offsetMin = new Vector2(0, 0); dashRt.offsetMax = new Vector2(-ArrowW, 8);

            var sbLabel = NewRect(startGo.transform, "Label");
            UILayout.Place(sbLabel, 0, 0, T.startButton.x - ArrowW, T.startButton.y);
            var sbEn = Text(sbLabel, "DEPLOY", 18, FontStyles.Bold, TextAlignmentOptions.TopLeft, Blu);
            UILayout.Place(sbEn.rectTransform, 42, 16, T.startButton.x - ArrowW - 42, 23);
            var sbKr = Text(sbLabel, "作戦開始", 38, FontStyles.Bold, TextAlignmentOptions.TopLeft, Color.white);
            UILayout.LineHeight(sbKr, 1.1f);
            UILayout.Place(sbKr.rectTransform, 42, 39, T.startButton.x - ArrowW - 42, 42);

            var sbArrow = NewImage(startGo.transform, "Arrow", Blu);
            UILayout.Place(sbArrow.rectTransform, T.startButton.x - ArrowW, 0, ArrowW, T.startButton.y);
            Stretch(Text(sbArrow.transform, "▶", 34, FontStyles.Bold, TextAlignmentOptions.Center, Color.white).rectTransform);

            // -- .sqh{padding:7px 18px 7px 30px;font:900 14px/1.25} em{11px} --
            var header = NewImage(right, "SquadHeader", Ink);
            UILayout.Fixed(header.rectTransform, T.squadRow.x, 32);
            var headerBar = NewImage(header.transform, "Bar", Blu);
            UILayout.Place(headerBar.rectTransform, 14, 7, 6, 18);
            var headerCol = NewRect(header.transform, "Text");
            UILayout.Place(headerCol, 30, 0, T.squadRow.x - 30 - 18, 32);
            var headerMain = Text(headerCol, "投入隊員", 14, FontStyles.Bold, TextAlignmentOptions.MidlineLeft, Txt);
            Stretch(headerMain.rectTransform);
            var headerSub = Text(headerCol, "SQUAD ROSTER // 05", 11, FontStyles.Normal, TextAlignmentOptions.MidlineRight, Dim);
            Stretch(headerSub.rectTransform);

            // -- #squad{gap:5px}: five .sq rows (built at runtime) --
            var listGo = NewRect(right, "SquadList");
            UILayout.Fixed(listGo, T.squadRow.x, 5 * T.squadRow.y + 4 * 5);
            UILayout.Column(listGo, 5);
            squadList = listGo;

            return right;
        }

        // .sd{width:343px;height:176px} .in{padding:16px 26px 0 40px} .tag{right:38px;top:14px;11px} .kr{54px/1} .en{19px/1.25;mt 3}
        // p{13px/1.5;mt 8;max-width:214px}
        static Image BuildSideCard(Transform parent, Sprite panelSprite, string tag, string big, string sub, string desc, out Button button)
        {
            var T = UITheme.Current.title;
            var go = NewImage(parent, "Side_" + tag, Paper);
            go.sprite = panelSprite; go.type = Image.Type.Sliced;
            UILayout.Fixed(go.rectTransform, T.sideCard.x, T.sideCard.y);
            button = go.gameObject.AddComponent<Button>();
            float w = T.sideCard.x - 40 - 26;

            var tagText = Text(go.transform, tag, 11, FontStyles.Bold, TextAlignmentOptions.MidlineRight, Ink);
            var tagRt = tagText.rectTransform;
            tagRt.anchorMin = tagRt.anchorMax = new Vector2(1, 1); tagRt.pivot = new Vector2(1, 1);
            tagRt.anchoredPosition = new Vector2(-38, -14); tagRt.sizeDelta = new Vector2(Mathf.Ceil(Width(tagText)) + 20, 20);
            var bigText = Text(go.transform, big, 54, FontStyles.Bold, TextAlignmentOptions.TopLeft, Ink);
            UILayout.LineHeight(bigText, 1f);
            UILayout.Place(bigText.rectTransform, 40, 16, w, 54);
            var subText = Text(go.transform, sub, 19, FontStyles.Bold, TextAlignmentOptions.TopLeft, Dim);
            UILayout.Place(subText.rectTransform, 40, 73, w, 24);
            var descText = Text(go.transform, desc, 13, FontStyles.Normal, TextAlignmentOptions.TopLeft, new Color32(0x23, 0x27, 0x2c, 0xff));
            descText.textWrappingMode = TextWrappingModes.Normal;
            UILayout.LineHeight(descText, 1.5f);
            UILayout.Place(descText.rectTransform, 40, 105, 214, 60);

            return go;
        }

        static RectTransform NewRect(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        static float Width(TMP_Text t) => t.GetPreferredValues(t.text).x;

        static void Size(TMP_Text t, float w, float h) => UILayout.Fixed(t.rectTransform, w, h);

        static Image NewImage(Transform parent, string name, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.color = color;
            return img;
        }

        static TMP_Text Text(Transform parent, string text, float size, FontStyles style, TextAlignmentOptions align, Color color)
        {
            var go = new GameObject("Text", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            go.AddComponent<LayoutElement>();
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text; tmp.fontSize = size; tmp.alignment = align; tmp.color = color;
            UIFonts.Auto(tmp, (style & FontStyles.Bold) != 0);
            tmp.raycastTarget = false;
            Stretch((RectTransform)go.transform);
            return tmp;
        }

        static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        }
    }
}
