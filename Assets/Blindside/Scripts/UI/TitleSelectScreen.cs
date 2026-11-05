// Title / faction-select screen shown before the match starts. Ports the functional pieces of
// initTitle()/renderSquad()/startGame() from blindside_source/05_ui_input_intro.js (~line 397-442):
// pick attack or defense, see that side's 5-operator roster and win condition, then deploy. The
// squad-list portrait (opArtSVG(o.key,true) + 2-letter tag in the source) is reused here as a flat
// color badge from OperatorData.color + the first two letters of the key, since no illustration
// sprites exist yet — same fallback the source itself used for this particular (small) portrait spot.
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Playables;
using TMPro;
using Blindside.Data;
using Blindside.Integration;
using Blindside.Rendering;
using Blindside.Map;
using Blindside.MapEditor;
using Blindside.Flow;

namespace Blindside.UI
{
    public class TitleSelectScreen : MonoBehaviour
    {
        [Header("Roster data (ported from OPS in 01_core_data_map_vision.js)")]
        public OperatorData[] operators;

        [Header("Side cards")]
        public Button attackCardButton;
        public Button defenseCardButton;
        public Image attackCardBg;
        public Image defenseCardBg;

        [Header("Panels")]
        public TMP_Text conditionsText;
        public TMP_Text footnoteText;
        public RectTransform squadListContent;
        public RectTransform mapRow;          // .row3: the map card and the editor button go here at runtime
        public Sprite panelSprite;

        [Header("Flow")]
        public Button startButton;
        public PlayableDirector introDirector;
        public MatchBootstrap matchBootstrap;
        public CanvasGroup screenGroup;

        [Header("Kit (the right column's upper half: side cards, map row, win condition, DEPLOY)")]
        [Tooltip("Prefabs/UI/TITLE_Top.prefab. It calls SelectSide / OpenMapList / OpenMapEditor / Deploy and redraws on Changed; the old " +
                 "Right/SideCards, Right/MapRow and Right/WinStartRow are kept (inactive) and still wired.")]
        public KitTitleTop kitTop;

        [Header("Kit (the squad roster under it and the spotlight at the bottom left)")]
        [Tooltip("Prefabs/UI/TITLE_Squad.prefab: header + the side's five rows; hover / click shows the operator in kitSpot. Redraws on Changed; " +
                 "the old Right/SquadHeader and Right/SquadList are kept (inactive) and still filled by RenderSquad.")]
        public KitTitleSquad kitSquad;
        [Tooltip("Prefabs/UI/TITLE_Spotlight.prefab (#spot, x56 y436).")]
        public KitSpotlight kitSpot;
        [Tooltip("Prefabs/UI/TITLE_Left.prefab: logo, subtitle, meta chips (values from the selected map / side / TurnManager.MaxRound), help " +
                 "line and the background decoration. Redraws on Changed; the old Left column is kept (inactive).")]
        public KitTitleLeft kitLeft;
        [Tooltip("DEPLOY plays the kit's screen wipe (KitWipe) and starts the match when the screen is covered.")]
        public bool deployWipe = true;
        bool deploying;

        /// <summary>Raised after the side or the map changed (the kit blocks redraw from SelectedTeam / SelectedMap / the condition texts).</summary>
        public event System.Action Changed;

        static readonly Color SelBlue = new Color32(0x1f, 0x96, 0xe8, 0xff);   // --blu
        static readonly Color SelRed = new Color32(0xe5, 0x36, 0x4d, 0xff);   // --foe
        static readonly Color Unselected = new Color32(0xec, 0xee, 0xf0, 0xff); // --paper

        public OperatorTeam SelectedTeam { get; private set; } = OperatorTeam.Attack;
        readonly List<GameObject> spawnedRows = new List<GameObject>();

        // the map to play: the default house or a saved map (remembered in PlayerPrefs), shown on the map card
        MapEntry selectedMap;
        RectTransform mapCard;
        TMP_Text mapNameText, mapSizeText, mapStatusText;
        RawImage mapPreviewImage; AspectRatioFitter mapPreviewFit;
        Texture2D mapPreviewTexture;
        /// <summary>The map the title will start (never null once the screen is awake).</summary>
        public MapEntry SelectedMap => selectedMap;
        public bool CanStart => selectedMap != null && !selectedMap.IsCorrupt && !selectedMap.HasRequiredError;

        void Awake()
        {
            attackCardButton.onClick.AddListener(() => SelectSide(OperatorTeam.Attack));
            defenseCardButton.onClick.AddListener(() => SelectSide(OperatorTeam.Defense));
            startButton.onClick.AddListener(Deploy);
            BuildMapCard();
            BuildMapEditorButton();
            BuildSettings();
            BuildTutorialButton();
            if (kitTop != null) kitTop.Bind(this);
            if (kitSquad != null) kitSquad.Bind(this, kitSpot);
            if (kitLeft != null) kitLeft.Bind(this);
        }

        /// <summary>The マップエディタ entry (.ed, UITheme.title.editorButton) beside the map card in the .row3 row, built here so the scene needs no edit.</summary>
        void BuildMapEditorButton()
        {
            if (Application.isPlaying && !Application.isEditor) return;   // a build has no map editor (its scene is editor-only)
            var T = UITheme.Current.title;
            var go = new GameObject("MapEditorButton", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            go.transform.SetParent(MapRow, false);
            UILayout.Fixed(go.transform as RectTransform, T.editorButton.x, T.editorButton.y);
            var bg = go.GetComponent<Image>();
            bg.color = new Color32(0x14, 0x16, 0x1a, 0xff);   // ink, like the DEPLOY block
            var btn = go.GetComponent<Button>();
            btn.targetGraphic = bg;
            var cb = btn.colors; cb.highlightedColor = new Color(.82f, .9f, 1f); cb.pressedColor = new Color(.65f, .75f, .9f); btn.colors = cb;

            // .ed{padding:14px 0 0 32px} small{12px/1.25} b{17px/1.25}
            var tag = CreateText(go.transform, "MAP EDITOR", 12, FontStyles.Bold, TextAlignmentOptions.TopLeft, SelBlue);
            UILayout.Place(tag.rectTransform, 32, 14, T.editorButton.x - 32, 15);
            var label = CreateText(go.transform, "マップエディタ", 17, FontStyles.Bold, TextAlignmentOptions.TopLeft, Color.white);
            UILayout.Place(label.rectTransform, 32, 29, T.editorButton.x - 32, 21);
            btn.onClick.AddListener(OpenMapEditor);
        }

        /// <summary>The .row3 row (map card + editor button): the builder's MapRow, or one made here for a scene built before it existed.</summary>
        RectTransform MapRow
        {
            get
            {
                if (mapRow != null) return mapRow;
                var parent = startButton.transform.parent;
                var go = new GameObject("MapRow", typeof(RectTransform));
                go.transform.SetParent(parent, false);
                go.transform.SetSiblingIndex(conditionsText.transform.parent.GetSiblingIndex());
                mapRow = (RectTransform)go.transform;
                var T = UITheme.Current.title;
                UILayout.Fixed(mapRow, T.squadRow.x, T.mapCard.y);
                UILayout.Row(mapRow, 10);
                return mapRow;
            }
        }

        // ---- settings (Phase 5-2): the 48×48 "SET" button at the bottom right and the options panel it opens, built under the screen root ----
        KitOptionsPanel optionsPanel;

        void BuildSettings()
        {
            if (optionsPanel != null || screenGroup == null) return;
            optionsPanel = KitOptionsPanel.Build(screenGroup.transform);
            KitOptionsPanel.CreateOpener(screenGroup.transform, optionsPanel);
            optionsPanel.transform.SetAsLastSibling();
        }

        public void OpenMapEditor()
        {
            if (SceneFlow.InRole(SceneRole.Title)) { SceneFlow.GoMapEditor(); return; }   // the editor is its own (editor-only) scene
            screenGroup.blocksRaycasts = false;
            gameObject.SetActive(false);
            Blindside.MapEditor.MapEditorScreen.Open(ShowAgain);   // the title comes back when the editor is left
        }

        void OnEnable()
        {
            screenGroup.alpha = 1f;
            screenGroup.blocksRaycasts = true;
            screenGroup.interactable = true;
            RefreshMapSelection();           // the editor may have saved or deleted maps meanwhile
            SelectSide(SelectedTeam);
        }

        void OnDestroy()
        {
            if (mapPreviewTexture != null) HudKit.DestroySafe(mapPreviewTexture);
        }

        /// <summary>Editor previews (edit mode, where Awake / OnEnable don't run): builds the map card and the editor button, shows the
        /// default house and `team`'s squad. Reads no remembered selection, so every preview looks the same.</summary>
        public void ShowPreview(OperatorTeam team)
        {
            if (mapCard == null) { BuildMapCard(); BuildMapEditorButton(); }
            BuildSettings();
            BuildTutorialButton();
            if (kitTop != null) kitTop.Bind(this);
            if (kitSquad != null) kitSquad.Bind(this, kitSpot);
            if (kitLeft != null) kitLeft.Bind(this);
            ShowMap(MapStore.DefaultEntry());
            SelectSide(team);
        }

        public void SelectSide(OperatorTeam team)
        {
            SelectedTeam = team;
            attackCardBg.color = team == OperatorTeam.Attack ? SelBlue : Unselected;
            defenseCardBg.color = team == OperatorTeam.Defense ? SelRed : Unselected;
            RenderSquad();
            Changed?.Invoke();
        }

        /// <summary>The roster order the match fields them in (attack: MatchBootstrap.AttackerKeys — CRUSHER, FLARE, IGNIS, SERUM, BASTION;
        /// defense: MapData.DefenderKeys); anyone else after them.</summary>
        static int SquadOrder(OperatorData o)
        {
            var keys = o.team == OperatorTeam.Attack ? MatchBootstrap.AttackerKeys : MapData.DefenderKeys;
            int i = System.Array.IndexOf(keys, o.key);
            return i < 0 ? keys.Length : i;
        }

        /// <summary>`team`'s operators in roster order (SquadOrder), into `into` (cleared first) — what RenderSquad lists.</summary>
        public void GetSquad(OperatorTeam team, List<OperatorData> into)
        {
            into.Clear();
            if (operators == null) return;
            foreach (var op in operators.Where(o => o != null && o.team == team).OrderBy(o => SquadOrder(o))) into.Add(op);
        }

        // Port of renderSquad() (05_ui_input_intro.js ~397-399).
        void RenderSquad()
        {
            foreach (var row in spawnedRows) HudKit.DestroySafe(row);
            spawnedRows.Clear();

            if (operators != null)
                foreach (var op in operators.Where(o => o != null && o.team == SelectedTeam).OrderBy(o => SquadOrder(o)))
                    spawnedRows.Add(BuildSquadRow(op));

            if (SelectedTeam == OperatorTeam.Attack)
            {
                conditionsText.text = $"① 人質を生かしたまま{ExfilGoalText(selectedMap)}\n② 防衛チームを全員制圧";
                footnoteText.text = "開始前に建物の外の好きな位置へ配置 · 敗北: 15ラウンド超過 · 全滅 · 味方の射撃で人質死亡";
            }
            else
            {
                conditionsText.text = "① 15ラウンド終了まで人質を奪われない\n② 攻撃チームを全員制圧";
                footnoteText.text = "全員が人質のいる部屋から開始 · 敗北: 人質を救出される · 全滅 · 味方の射撃で人質死亡";
            }
        }

        // .sq{height:60px;width:696px} .cb{width:10px} .por{width:72px;height:60px} .m{padding:5px 14px 0 12px} b{21px/1.25} b small{12.5px}
        // p{12.5px/1.4;nowrap;ellipsis} .s{width:196px;padding:9px 20px 0 28px} .s div{grid 30px 88px 1fr;gap:6px;height:14px;margin-bottom:5px}
        GameObject BuildSquadRow(OperatorData op)
        {
            var T = UITheme.Current.title;
            float rowW = T.squadRow.x, rowH = T.squadRow.y;
            var row = new GameObject(op.key, typeof(RectTransform));
            row.transform.SetParent(squadListContent, false);
            UILayout.Fixed((RectTransform)row.transform, rowW, rowH);
            var rowBg = row.AddComponent<Image>();
            rowBg.sprite = panelSprite; rowBg.type = Image.Type.Sliced;
            rowBg.color = new Color32(0x1a, 0x1c, 0x21, 0xe6);

            // .por: the face (or the monogram on the operator colour) in a 72×60 slot after the 10px colour bar
            var badge = new GameObject("Badge", typeof(RectTransform));
            badge.transform.SetParent(row.transform, false);
            UILayout.Place((RectTransform)badge.transform, 10, 0, T.squadFace.x, T.squadFace.y);
            var badgeImg = badge.AddComponent<Image>(); badgeImg.color = op.color;
            // The face on a paper-gradient slot when the operator has art; otherwise the monogram on the operator colour.
            if (OperatorArt.ApplyFace(badgeImg, op.key) == null)
            {
                var badgeLabel = CreateText(badge.transform, op.key.Length >= 2 ? op.key.Substring(0, 2) : op.key, 15, FontStyles.Bold, TextAlignmentOptions.Center, Color.black);
                StretchFull(badgeLabel.rectTransform);
            }

            // .m: name (+ kr · role, 12.5px) / description, one line each
            float infoX = 10 + T.squadFace.x + 12, infoW = rowW - infoX - T.squadStats - 14;
            var nameText = CreateText(row.transform, $"{op.DisplayName}  <size=12.5><color=#9aa0a8>{op.kr} · {op.role}</color></size>", 21, FontStyles.Bold, TextAlignmentOptions.TopLeft, Color.white, UITheme.TextRole.Display);
            nameText.textWrappingMode = TextWrappingModes.NoWrap; nameText.overflowMode = TextOverflowModes.Ellipsis;
            UILayout.Place(nameText.rectTransform, infoX, 5, infoW, 26);
            var descText = CreateText(row.transform, op.description, 12.5f, FontStyles.Normal, TextAlignmentOptions.TopLeft, new Color32(0xc4, 0xcb, 0xd3, 0xff));
            descText.textWrappingMode = TextWrappingModes.NoWrap; descText.overflowMode = TextOverflowModes.Ellipsis;
            UILayout.Place(descText.rectTransform, infoX, 33, infoW, 18);

            // .s: HP / MOV / WPN rows, bars on the 88px track (HP of 140, MOV of 10m)
            var stats = new GameObject("Stats", typeof(RectTransform));
            stats.transform.SetParent(row.transform, false);
            UILayout.Place((RectTransform)stats.transform, rowW - T.squadStats, 0, T.squadStats, rowH);

            string wpnName = op.weapon != null ? op.weapon.weaponName : "-";
            var wpnParts = wpnName.Split(' ');
            string wpnShort = wpnParts.Length > 1 ? wpnParts[1] : wpnName;
            int movMeters = op.mv * 3 + 1;
            float statW = T.squadStats - 28 - 20;
            BuildStatBarRow(stats.transform, 0, statW, "HP", op.hp.ToString(), Mathf.Clamp01(op.hp / 140f), op.color);
            BuildStatBarRow(stats.transform, 1, statW, "MOV", movMeters + "m", Mathf.Clamp01(movMeters / 10f), op.color);
            BuildStatBarRow(stats.transform, 2, statW, "WPN", wpnShort, -1f, op.color);

            return row;
        }

        static void BuildStatBarRow(Transform parent, int index, float width, string label, string value, float fill, Color barColor)
        {
            const float Top = 9f, RowH = 14f, RowGap = 4f, LabelW = 30f, TrackW = 88f, Gap = 6f;
            var row = new GameObject(label, typeof(RectTransform));
            row.transform.SetParent(parent, false);
            UILayout.Place((RectTransform)row.transform, 28, Top + index * (RowH + RowGap), width, RowH);

            var labelText = CreateText(row.transform, label, 12, FontStyles.Normal, TextAlignmentOptions.MidlineLeft, new Color(1f, 1f, 1f, 0.5f));
            UILayout.Place(labelText.rectTransform, 0, 0, LabelW, RowH);

            float valueX = LabelW + Gap;
            if (fill >= 0f)
            {
                var barGo = new GameObject("Bar", typeof(RectTransform));
                barGo.transform.SetParent(row.transform, false);
                barGo.AddComponent<Image>().color = barColor;
                UILayout.Place((RectTransform)barGo.transform, LabelW + Gap, 3, TrackW * fill, 8);
                valueX += TrackW + Gap;
            }

            var valueText = CreateText(row.transform, value, 12, FontStyles.Normal, TextAlignmentOptions.MidlineRight, new Color32(0xc4, 0xcb, 0xd3, 0xff));
            valueText.textWrappingMode = TextWrappingModes.NoWrap;
            UILayout.Place(valueText.rectTransform, valueX, 0, width - valueX, RowH);
        }

        static TMP_Text CreateText(Transform parent, string text, float size, FontStyles style, TextAlignmentOptions align, Color color, UITheme.TextRole? role = null)
        {
            var go = new GameObject("Text", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            go.AddComponent<LayoutElement>();
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text; tmp.fontSize = size; tmp.alignment = align; tmp.color = color;
            if (role.HasValue) UIFonts.Apply(tmp, role.Value); else UIFonts.Auto(tmp, (style & FontStyles.Bold) != 0);
            tmp.raycastTarget = false;
            return tmp;
        }

        static void StretchFull(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        }

        // ---- the map to play -------------------------------------------------------------------------------------------------

        /// <summary>The exfil zone letters of a listed map ("A", "B" …), in order.</summary>
        static List<string> ExfilNames(MapEntry map)
        {
            var names = new List<string>();
            if (map != null && map.file != null) foreach (var e in map.file.exfils) names.Add(e.name);
            return names;
        }

        /// <summary>The attack side's first win condition for this map: "救出地点Nか所(EXFIL A〜…)のいずれかまで護送" (one zone: "救出地点1か所(EXFIL A)まで護送").
        /// EXFIL and the letter are joined by a no-break space.</summary>
        public static string ExfilGoalText(MapEntry map)
        {
            var names = ExfilNames(map);
            if (names.Count == 0) return "救出地点0か所 (マップに救出地点なし)";
            if (names.Count == 1) return $"救出地点1か所(EXFIL {names[0]})まで護送";
            return $"救出地点{names.Count}か所(EXFIL {names[0]}〜{names[names.Count - 1]})のいずれかまで護送";
        }

        /// <summary>The map card (.mp, UITheme.title.mapCard), built here (like the editor button) so the scene needs no edit: a picture of the
        /// map, its name and size, whether it can be started; a click opens the map list.</summary>
        // .mp{width:520px;height:74px} canvas{92×70;margin-left:10px} .mi{padding:0 8px 0 14px} small{11px} b{21px/1.15} span{12px} em{12px;ml 8}
        // .mb{margin-right:34px;13px;padding:6px 14px}
        void BuildMapCard()
        {
            var T = UITheme.Current.title;
            float W = T.mapCard.x, H = T.mapCard.y;
            var go = new GameObject("MapCard", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            go.transform.SetParent(MapRow, false);
            mapCard = (RectTransform)go.transform;
            UILayout.Fixed(mapCard, W, H);
            var bg = go.GetComponent<Image>();
            bg.color = new Color32(0x14, 0x16, 0x1a, 0xff);
            var btn = go.GetComponent<Button>();
            btn.targetGraphic = bg;
            var cb = btn.colors; cb.highlightedColor = new Color(.82f, .9f, 1f); cb.pressedColor = new Color(.65f, .75f, .9f); btn.colors = cb;

            const float PicW = 92f, PicH = 70f, PicX = 10f;
            var frame = new GameObject("Preview", typeof(RectTransform), typeof(Image));
            frame.transform.SetParent(go.transform, false);
            frame.GetComponent<Image>().color = new Color32(0x26, 0x2a, 0x31, 0xff);
            UILayout.Place((RectTransform)frame.transform, PicX, (H - PicH) / 2f, PicW, PicH);
            var pic = new GameObject("Image", typeof(RectTransform), typeof(RawImage), typeof(AspectRatioFitter));
            pic.transform.SetParent(frame.transform, false);
            StretchFull((RectTransform)pic.transform);
            mapPreviewImage = pic.GetComponent<RawImage>(); mapPreviewImage.raycastTarget = false;
            mapPreviewFit = pic.GetComponent<AspectRatioFitter>(); mapPreviewFit.aspectMode = AspectRatioFitter.AspectMode.FitInParent;

            var hint = CreateText(go.transform, "マップ選択 ▶", 13, FontStyles.Bold, TextAlignmentOptions.Center, SelBlue);
            float hintW = Mathf.Ceil(hint.GetPreferredValues(hint.text).x) + 28f;
            UILayout.Place(hint.rectTransform, W - 34f - hintW, (H - 28f) / 2f, hintW, 28f);

            float infoX = PicX + PicW + 14f, infoW = W - 34f - hintW - 8f - infoX;
            var info = new GameObject("Info", typeof(RectTransform));
            info.transform.SetParent(go.transform, false);
            UILayout.Place((RectTransform)info.transform, infoX, (H - 54f) / 2f, infoW, 54f);
            var small = CreateText(info.transform, "MAP // マップ", 11, FontStyles.Bold, TextAlignmentOptions.TopLeft, SelBlue);
            UILayout.Place(small.rectTransform, 0, 0, infoW, 14);
            mapNameText = CreateText(info.transform, "", 21, FontStyles.Bold, TextAlignmentOptions.TopLeft, Color.white, UITheme.TextRole.Title);
            mapNameText.textWrappingMode = TextWrappingModes.NoWrap; mapNameText.overflowMode = TextOverflowModes.Ellipsis;
            UILayout.LineHeight(mapNameText, 1.15f);
            UILayout.Place(mapNameText.rectTransform, 0, 14, infoW, 25);
            mapSizeText = CreateText(info.transform, "", 12, FontStyles.Normal, TextAlignmentOptions.TopLeft, new Color32(0xc4, 0xcb, 0xd3, 0xff), UITheme.TextRole.Display);
            mapSizeText.textWrappingMode = TextWrappingModes.NoWrap;
            UILayout.Place(mapSizeText.rectTransform, 0, 39, infoW, 15);
            mapStatusText = CreateText(info.transform, "", 12, FontStyles.Bold, TextAlignmentOptions.TopLeft, Color.white, UITheme.TextRole.Title);
            mapStatusText.textWrappingMode = TextWrappingModes.NoWrap;
            UILayout.Place(mapStatusText.rectTransform, 0, 39, infoW, 15);   // after the size text (x set in RefreshMapSelection)

            btn.onClick.AddListener(OpenMapList);
        }

        public void OpenMapList()
        {
            MapBrowserWindow.Open(MapBrowserWindow.Mode.Title, MapStore.SelectedStem, OnMapChosen, null);
        }

        void OnMapChosen(MapEntry entry)
        {
            MapStore.SelectedStem = entry.IsDefault ? "" : entry.stem;
            RefreshMapSelection();
        }

        /// <summary>Re-reads the remembered map (the default house when it is gone or damaged) and redraws the card, the win condition and START.</summary>
        void RefreshMapSelection() => ShowMap(MapStore.LoadSelected());

        /// <summary>Makes `entry` the map to play and redraws the card, the win condition and START.</summary>
        void ShowMap(MapEntry entry)
        {
            selectedMap = entry;
            if (mapPreviewTexture != null) { HudKit.DestroySafe(mapPreviewTexture); mapPreviewTexture = null; }
            var e = selectedMap;
            mapNameText.text = e.displayName;
            mapSizeText.text = $"{e.SizeText} · 救出地点{ExfilNames(e).Count}か所" + (e.IsDefault ? " · 標準搭載" : "");
            // .mp em{margin-left:8px}: the status follows the size line
            var sizeRt = mapSizeText.rectTransform; var statusRt = mapStatusText.rectTransform;
            float statusX = Mathf.Ceil(mapSizeText.GetPreferredValues(mapSizeText.text).x) + 8f;
            statusRt.anchoredPosition = new Vector2(statusX, sizeRt.anchoredPosition.y);
            statusRt.sizeDelta = new Vector2(Mathf.Max(0f, ((RectTransform)sizeRt.parent).rect.width - statusX), sizeRt.sizeDelta.y);
            mapPreviewTexture = MapPreview.Make(e.file);
            mapPreviewImage.texture = mapPreviewTexture;
            mapPreviewFit.aspectRatio = e.width / (float)e.height;
            if (e.HasRequiredError) { mapStatusText.color = SelRed; mapStatusText.text = $"必須エラー{e.errors.Count}件 — 開始できません"; }
            else { mapStatusText.color = new Color32(0x5c, 0xd0, 0x7a, 0xff); mapStatusText.text = "開始可能"; }
            startButton.interactable = CanStart;
            if (conditionsText != null) RenderSquad();
            Changed?.Invoke();
        }

        // ---- starting ---------------------------------------------------------------------------------------------------------------

        OperatorTeam launchTeam;
        string deployScene;   // Deploy → Launch: the scene the chosen map plays in (null = the battle scene)

        /// <summary>[再挑戦]: the same side on the same map again (a test play stays a test play, back to the editor from its result).</summary>
        public void Restart()
        {
            if (PlaySession.Source != null) Launch(PlaySession.Team, PlaySession.Source, PlaySession.ReturnToEditor);
            else Deploy();
        }

        /// <summary>[タイトルへ]: the faction-select screen is back (OnEnable re-arms it; the chosen side is kept).</summary>
        public void ShowAgain() => gameObject.SetActive(true);

        /// <summary>The title's START: the selected map (nothing happens when it has required errors or can't be read).</summary>
        public void Deploy()
        {
            if (!CanStart || deploying) return;
            var data = selectedMap.BuildGameData();
            if (data == null) { RefreshMapSelection(); return; }
            var cat = MapCatalog.Find(MapCatalog.IdOf(selectedMap.stem));   // an exported map plays in its own scene
            deployScene = cat != null && !string.IsNullOrEmpty(cat.sceneName) ? cat.sceneName : null;
            if (!deployWipe || !Application.isPlaying) { Launch(SelectedTeam, data, null); return; }
            // the wipe covers the screen, then the match starts as before (Launch: title away, intro); no clicks meanwhile
            deploying = true;
            screenGroup.blocksRaycasts = false;
            var team = SelectedTeam;
            KitWipe.Play(() =>
            {
                deploying = false;
                if (!Launch(team, data, null)) screenGroup.blocksRaycasts = true;
            });
        }

        /// <summary>Starts a match of `team` on `source` (the title's START, the editor's test buttons, [再挑戦]): the game is handed a clone of
        /// the map, the title goes away, the intro plays and the match begins. `returnToEditor` is set for a test play. `skipIntro` (debug quick
        /// start) goes straight to the match as the defense side does. False (nothing started) when the game refuses the map.</summary>
        public bool Launch(OperatorTeam team, MapData source, System.Action returnToEditor, bool skipIntro = false)
        {
            bool tutorial = launchingTutorial;
            launchingTutorial = false;
            string scene = deployScene;
            deployScene = null;
            if (!MapLibrary.SetCurrent(source.Clone())) return false;      // (logs why)
            if (!tutorial && SceneFlow.InRole(SceneRole.Title))
            {
                // the title scene plays no match: the battle scene (or the chosen map's own scene) does, with this request
                SceneFlow.GoBattle(new LaunchRequest { team = team, map = source.Clone(), skipIntro = skipIntro, sceneName = scene });
                return true;
            }
            // every launch but the tutorial's is a normal match
            if (!tutorial) Blindside.Tutorial.TutorialDirector.Disarm(matchBootstrap);
            else if (matchBootstrap != null) matchBootstrap.tutorialMode = true;
            PlaySession.Begin(team, source, returnToEditor);
            launchTeam = team;
            startButton.interactable = false;
            screenGroup.blocksRaycasts = false;
            screenGroup.interactable = false;
            gameObject.SetActive(false);

            // startGame(): only an attack-side player gets playIntro() (helicopter + rope drop); the defense goes
            // straight to the PREPARATION banner.
            // (the Timeline intro only on a map that has the house — MapData.useHouseIntro; any other map gets the short SimpleIntro).
            var mapData = MapLibrary.Current;
            if (!skipIntro && introDirector != null && team != OperatorTeam.Defense && mapData.useHouseIntro)
            {
                introDirector.stopped += OnIntroStopped;
                introDirector.time = 0;
                introDirector.Play();
            }
            else if (!skipIntro && team != OperatorTeam.Defense && !mapData.useHouseIntro)
            {
                Blindside.Cinematic.SimpleIntro.Play(StartMatch);
            }
            else
            {
                // The intro Timeline opens on a black Fade over the world and clears it at its end; with no intro it
                // has to be cleared here (fade(false) in the source's non-intro path).
                var fade = GameObject.Find("BlindsideCanvas/Fade");
                var cg = fade != null ? fade.GetComponent<CanvasGroup>() : null;
                if (cg != null) cg.alpha = tutorial ? 1f : 0f;   // (the tutorial opens on black: its entrance shot fades in itself)
                StartMatch();
            }
            return true;
        }

        bool launchingTutorial;

        /// <summary>The tutorial's last step: the attack side on the default house, intro and all (the remembered map choice is left alone).</summary>
        public void LaunchMainOperation()
        {
            var data = MapStore.DefaultEntry().BuildGameData();
            if (data == null || !Launch(OperatorTeam.Attack, data, null)) ShowAgain();
        }

        /// <summary>The title's チュートリアル: the wipe, then the training map (TutorialMap) in tutorial mode — FLARE alone, no intro;
        /// TutorialDirector takes it from there.</summary>
        public void LaunchTutorial()
        {
            if (deploying) return;
            // the title scene: the tutorial is its own scene (SceneFlow), its SceneEntry calls StartTutorialHere
            bool ownScene = SceneFlow.InRole(SceneRole.Title);
            if (!ownScene && matchBootstrap == null) return;
            System.Action go = () =>
            {
                if (ownScene) { SceneFlow.GoTutorial(); return; }
                if (!StartTutorialHere()) screenGroup.blocksRaycasts = true;
            };
            if (!deployWipe || !Application.isPlaying) { go(); return; }
            deploying = true;
            screenGroup.blocksRaycasts = false;
            KitWipe.Play(() => { deploying = false; go(); });
        }

        /// <summary>The tutorial match in this scene (the tutorial scene's SceneEntry, or the one-scene flow): the training map (TutorialMap)
        /// in tutorial mode, no intro. False when it could not start.</summary>
        public bool StartTutorialHere()
        {
            if (matchBootstrap == null) return false;
            Blindside.Tutorial.TutorialDirector.Prepare(matchBootstrap);
            launchingTutorial = true;
            if (Launch(OperatorTeam.Attack, Blindside.Tutorial.TutorialMap.Build(), null, skipIntro: true)) return true;
            Blindside.Tutorial.TutorialDirector.Disarm(matchBootstrap);
            return false;
        }

        /// <summary>The チュートリアル button: bottom right of the screen, left of the settings button.</summary>
        void BuildTutorialButton()
        {
            if (screenGroup == null || screenGroup.transform.Find("TutorialButton") != null) return;
            var theme = UITheme.Current;
            var rt = HudKit.NewRect("TutorialButton", screenGroup.transform);
            rt.anchorMin = rt.anchorMax = new Vector2(1f, 0f); rt.pivot = new Vector2(1f, 0f);
            rt.anchoredPosition = new Vector2(-20f - 48f - 10f, 2f); rt.sizeDelta = new Vector2(200f, 36f);   // (36 high: clear of the roster's last row)
            var img = HudKit.AddImage(rt, theme.ink, raycast: true);
            rt.gameObject.AddComponent<SlantedGraphic>().Set(SlantedGraphic.Size.Custom, SlantedGraphic.Direction.Right, 10f);
            var b = rt.gameObject.AddComponent<Button>();
            b.transition = Selectable.Transition.None; b.targetGraphic = img;
            b.onClick.AddListener(LaunchTutorial);
            rt.gameObject.AddComponent<KitBrighten>().image = img;
            rt.gameObject.AddComponent<LintIgnoreOverlap>().reason = "sits in the title's free bottom-right corner, beside the settings button";
            var tag = CreateText(rt, "TUTORIAL", 11, FontStyles.Bold, TextAlignmentOptions.MidlineLeft, SelBlue, UITheme.TextRole.Display);
            UILayout.Place(tag.rectTransform, 20, 0, 70, 36);
            var label = CreateText(rt, "チュートリアル", 15, FontStyles.Bold, TextAlignmentOptions.MidlineLeft, Color.white, UITheme.TextRole.Body);
            UILayout.Place(label.rectTransform, 80, 0, 110, 36);
        }

        void OnIntroStopped(PlayableDirector d)
        {
            d.stopped -= OnIntroStopped;
            if (SceneFlow.Leaving) return;   // the scene is being unloaded (the director stops with it): no match here
            StartMatch();
        }

        void StartMatch()
        {
            // startGame(): G.cam.h=27; atk → the map's attack camera start (11,6 on the default map, also where playIntro() leaves it);
            // def → its defense camera start (13,12).
            var mapData = MapLibrary.Current;
            var start = launchTeam == OperatorTeam.Defense ? mapData.DefenseCamStart : mapData.AttackCamStart;
            BattlefieldCamera.Activate(start.x, start.y);
            if (matchBootstrap == null) return;
            matchBootstrap.playerTeam = launchTeam;
            matchBootstrap.StartCoroutine(matchBootstrap.RunOneMatch());
        }
    }
}
