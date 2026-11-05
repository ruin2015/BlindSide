// The map list window — shared by the editor ("読み込み": [開く] edits the map) and the title ("マップ選択": [開く] picks it to play).
// Left: every map — the built-in 基本邸宅 always first (read-only), then the saved ones, newest first — with name, size, time of
// the last save and a red mark for "has required errors" (a damaged file gets a dark × and "(破損)"). Right: the selected map's
// picture (walls of the three kinds in three colours), its state, and [開く] [複製] [削除]. A damaged map can't be opened: a window
// says "このマップを開けません: {理由}" instead; it can still be deleted.
// The window is self-contained (its own overlay canvas) and never throws: disk errors are shown in a message window.
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Blindside.UI;

namespace Blindside.MapEditor
{
    public sealed class MapBrowserWindow : MonoBehaviour
    {
        public enum Mode { Editor, Title }

        public static MapBrowserWindow Instance { get; private set; }

        static readonly Color Ink = MapEditorScreen.Ink, Paper = MapEditorScreen.Paper, Blue = MapEditorScreen.Blue, Foe = MapEditorScreen.Foe, PaperDark = MapEditorScreen.PaperDark;
        static readonly Color ErrText = new Color32(0xb3, 0x1b, 0x2b, 0xff), OkText = new Color32(0x1c, 0x7a, 0x3a, 0xff), Muted = new Color(Ink.r, Ink.g, Ink.b, .6f);

        Mode mode;
        Action<MapEntry> onOpen;
        Action onClosed;
        string inUseStem;                 // title: the map that is selected now ("" = default) — marked "使用中"
        Canvas canvas;
        RectTransform listContent;
        TMP_Text headline, previewName, previewInfo, previewStatus, pathText;
        RawImage previewImage; AspectRatioFitter previewFit;
        Button openButton, cloneButton, deleteButton;
        List<MapEntry> entries = new List<MapEntry>();
        readonly List<(MapEntry entry, Image bg, TMP_Text[] texts)> rows = new List<(MapEntry, Image, TMP_Text[])>();
        MapEntry selected;
        Texture2D previewTexture;
        bool closed;

        public MapEntry Selected => selected;
        public IReadOnlyList<MapEntry> Entries => entries;

        /// <summary>Opens the window. `select`: the stem ("" = default house) to highlight first. `open` gets the chosen map (never a damaged
        /// one); `closed` runs when the window goes away (after `open`, too).</summary>
        public static MapBrowserWindow Open(Mode mode, string select, Action<MapEntry> open, Action closed)
        {
            if (Instance != null) return Instance;
            var go = new GameObject("MapBrowser");
            var w = go.AddComponent<MapBrowserWindow>();
            w.mode = mode; w.onOpen = open; w.onClosed = closed;
            w.inUseStem = mode == Mode.Title ? MapStore.SelectedStem : null;
            w.Build();
            w.Reload(select);
            return w;
        }

        void Awake() { Instance = this; ModalDialog.Enter(); }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            ModalDialog.Exit();
            if (previewTexture != null) Destroy(previewTexture);
        }

        void Update()
        {
            var kb = Keyboard.current;
            if (kb != null && kb.escapeKey.wasPressedThisFrame && ModalDialog.Count <= 1) Close();
        }

        public void Close()
        {
            if (closed) return;
            closed = true;
            if (Instance == this) Instance = null;
            var cb = onClosed;
            Destroy(gameObject);
            cb?.Invoke();
        }

        // ---- data ---------------------------------------------------------------------------------------------------------------

        /// <summary>Reads the folder again and redraws the list, with `select` (a stem; "" = default) highlighted.</summary>
        public void Reload(string select)
        {
            entries = MapStore.List();
            selected = null;
            foreach (var e in entries) if (e.stem == (select ?? "") || (e.IsDefault && MapStore.IsDefaultStem(select))) { selected = e; break; }
            if (selected == null) selected = entries[0];
            BuildRows();
            ShowSelected();
        }

        // ---- building the window ---------------------------------------------------------------------------------------------

        void Build()
        {
            var cgo = new GameObject("MapBrowserCanvas");
            cgo.transform.SetParent(transform, false);
            canvas = cgo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 220;
            Blindside.UI.UITheme.Current.ApplyTo(cgo.AddComponent<CanvasScaler>(), Blindside.UI.UITheme.CanvasKind.Menu);
            cgo.AddComponent<GraphicRaycaster>();

            var root = MapEditorScreen.NewRect("Root", cgo.transform);
            MapEditorScreen.Stretch(root);
            var dim = MapEditorScreen.NewImage("Dim", root, new Color(Ink.r, Ink.g, Ink.b, .78f));
            MapEditorScreen.Stretch(dim.rectTransform);

            var frame = MapEditorScreen.NewImage("Window", root, Ink);
            frame.rectTransform.anchorMin = frame.rectTransform.anchorMax = frame.rectTransform.pivot = new Vector2(.5f, .5f);
            frame.rectTransform.sizeDelta = new Vector2(1240, 780);
            var paper = MapEditorScreen.NewImage("Paper", frame.transform, Paper);
            MapEditorScreen.Stretch(paper.rectTransform, 3, 3, 3, 3);

            // header
            var head = MapEditorScreen.NewRect("Header", paper.transform);
            head.anchorMin = new Vector2(0, 1); head.anchorMax = new Vector2(1, 1); head.pivot = new Vector2(.5f, 1); head.sizeDelta = new Vector2(0, 70);
            headline = MapEditorScreen.NewText(head, mode == Mode.Title ? "マップ選択" : "マップ読み込み", 32, FontStyles.Bold, Ink);
            MapEditorScreen.Stretch(headline.rectTransform, 28, 0, 28, 0);
            var sub = MapEditorScreen.NewText(head, mode == Mode.Title ? "MAP SELECT // プレイするマップを選んでください" : "LOAD MAP // 編集するマップを選んでください", 14, FontStyles.Bold, Blue, TextAlignmentOptions.MidlineRight);
            MapEditorScreen.Stretch(sub.rectTransform, 28, 0, 28, 0);

            // left: list
            var left = MapEditorScreen.NewRect("Left", paper.transform);
            left.anchorMin = new Vector2(0, 0); left.anchorMax = new Vector2(0, 1); left.pivot = new Vector2(0, .5f);
            left.offsetMin = new Vector2(24, 58); left.offsetMax = new Vector2(24 + 640, -76);
            var cols = MapEditorScreen.NewRect("Columns", left);
            cols.anchorMin = new Vector2(0, 1); cols.anchorMax = new Vector2(1, 1); cols.pivot = new Vector2(.5f, 1); cols.sizeDelta = new Vector2(0, 30);
            MapEditorScreen.HBox(cols, 8, new RectOffset(10, 18, 0, 0));
            MapEditorScreen.Size(MapEditorScreen.NewRect("IconGap", cols), 32, 28);
            var cName = MapEditorScreen.NewText(cols, "名前", 14, FontStyles.Bold, Muted); MapEditorScreen.Size(cName, -1, 28, 1);
            var cSize = MapEditorScreen.NewText(cols, "サイズ", 14, FontStyles.Bold, Muted); MapEditorScreen.Size(cSize, 80, 28);
            var cTime = MapEditorScreen.NewText(cols, "更新日時", 14, FontStyles.Bold, Muted); MapEditorScreen.Size(cTime, 170, 28);

            var listBg = MapEditorScreen.NewImage("List", left, new Color32(0xdd, 0xe1, 0xe6, 0xff));
            listBg.rectTransform.anchorMin = Vector2.zero; listBg.rectTransform.anchorMax = Vector2.one;
            listBg.rectTransform.offsetMin = Vector2.zero; listBg.rectTransform.offsetMax = new Vector2(0, -32);
            var viewport = MapEditorScreen.NewRect("Viewport", listBg.transform);
            MapEditorScreen.Stretch(viewport, 4, 4, 4, 4);
            viewport.gameObject.AddComponent<RectMask2D>();
            listContent = MapEditorScreen.NewRect("Content", viewport);
            listContent.anchorMin = new Vector2(0, 1); listContent.anchorMax = new Vector2(1, 1); listContent.pivot = new Vector2(.5f, 1);
            MapEditorScreen.VBox(listContent, 4, new RectOffset(0, 0, 0, 0));
            var fit = listContent.gameObject.AddComponent<ContentSizeFitter>();
            fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var scroll = listBg.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport; scroll.content = listContent; scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped; scroll.scrollSensitivity = 40f;

            // right: preview
            var right = MapEditorScreen.NewRect("Right", paper.transform);
            right.anchorMin = new Vector2(0, 0); right.anchorMax = new Vector2(1, 1);
            right.offsetMin = new Vector2(24 + 640 + 24, 58); right.offsetMax = new Vector2(-24, -76);
            MapEditorScreen.VBox(right, 8, new RectOffset(0, 0, 0, 0));
            previewName = MapEditorScreen.NewText(right, "", 26, FontStyles.Bold, Ink, role: UITheme.TextRole.Title); MapEditorScreen.Size(previewName, -1, 36);
            previewInfo = MapEditorScreen.NewText(right, "", 15, FontStyles.Normal, Muted, role: UITheme.TextRole.Body); MapEditorScreen.Size(previewInfo, -1, 22);
            var picFrame = MapEditorScreen.NewImage("PicFrame", right, Ink); MapEditorScreen.Size(picFrame, -1, 318);
            var picHolder = MapEditorScreen.NewRect("Pic", picFrame.transform);
            MapEditorScreen.Stretch(picHolder, 4, 4, 4, 4);
            var picBg = MapEditorScreen.NewImage("Bg", picHolder, new Color32(0x10, 0x12, 0x15, 0xff), false);
            MapEditorScreen.Stretch(picBg.rectTransform);
            var picGo = MapEditorScreen.NewRect("Image", picHolder);
            picGo.anchorMin = picGo.anchorMax = picGo.pivot = new Vector2(.5f, .5f);
            previewImage = picGo.gameObject.AddComponent<RawImage>(); previewImage.raycastTarget = false;
            previewFit = picGo.gameObject.AddComponent<AspectRatioFitter>(); previewFit.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            BuildLegend(right);
            previewStatus = MapEditorScreen.NewText(right, "", 15, FontStyles.Bold, Ink, TextAlignmentOptions.TopLeft, UITheme.TextRole.Title);
            previewStatus.textWrappingMode = TextWrappingModes.Normal;
            MapEditorScreen.Size(previewStatus, -1, 96);
            var buttons = MapEditorScreen.NewRect("Buttons", right);
            MapEditorScreen.Size(buttons, -1, 54);
            MapEditorScreen.HBox(buttons, 10, new RectOffset(0, 0, 4, 0), TextAnchor.MiddleLeft);
            openButton = MapEditorScreen.NewButton(buttons, "開く", 150, 48, Blue, Color.white, 20, OnOpenClicked);
            cloneButton = MapEditorScreen.NewButton(buttons, "複製", 150, 48, MapEditorScreen.InkLight, Paper, 20, OnCloneClicked);
            deleteButton = MapEditorScreen.NewButton(buttons, "削除", 150, 48, Foe, Color.white, 20, OnDeleteClicked);

            // bottom
            var bottom = MapEditorScreen.NewRect("Bottom", paper.transform);
            bottom.anchorMin = new Vector2(0, 0); bottom.anchorMax = new Vector2(1, 0); bottom.pivot = new Vector2(.5f, 0); bottom.sizeDelta = new Vector2(0, 52);
            MapEditorScreen.HBox(bottom, 12, new RectOffset(24, 24, 4, 8));
            pathText = MapEditorScreen.NewText(bottom, "保存先: " + MapStore.Directory, 13, FontStyles.Normal, Muted);
            pathText.textWrappingMode = TextWrappingModes.Normal;   // a long path takes the second line instead of shrinking
            MapEditorScreen.Size(pathText, -1, 36, 1);
            MapEditorScreen.NewButton(bottom, "閉じる", 140, 40, PaperDark, Ink, 18, Close);
        }

        static void BuildLegend(Transform parent)
        {
            var items = new (string, Color)[]
            {
                ("屋外", MapPreview.Outdoor), ("屋内", MapPreview.Indoor), ("通常の壁", MapPreview.Solid), ("破壊可能な壁", MapPreview.Breakable),
                ("強化壁", MapPreview.Reinforced), ("木箱", MapPreview.Crate), ("救出地点", MapPreview.Exfil), ("人質", MapPreview.Hostage),
            };
            for (int rowIndex = 0; rowIndex < 2; rowIndex++)
            {
                var row = MapEditorScreen.NewRect("Legend" + rowIndex, parent);
                MapEditorScreen.Size(row, -1, 22);
                MapEditorScreen.HBox(row, 6, new RectOffset(0, 0, 2, 2));
                for (int i = rowIndex * 4; i < rowIndex * 4 + 4; i++)
                {
                    var sw = MapEditorScreen.NewImage("Swatch", row, items[i].Item2, false); MapEditorScreen.Size(sw, 16, 16);
                    var tx = MapEditorScreen.NewText(row, items[i].Item1, 13, FontStyles.Normal, Ink); MapEditorScreen.Size(tx, i % 4 == 3 ? 100 : 92, 20);
                }
            }
        }

        // ---- the list ---------------------------------------------------------------------------------------------------------------

        void BuildRows()
        {
            for (int i = listContent.childCount - 1; i >= 0; i--) { var c = listContent.GetChild(i).gameObject; c.SetActive(false); Destroy(c); }
            rows.Clear();
            foreach (var e in entries)
            {
                var entry = e;
                var bg = MapEditorScreen.NewImage("Row_" + entry.displayName, listContent, Paper);
                var btn = bg.gameObject.AddComponent<Button>();
                btn.targetGraphic = bg;
                var cb = btn.colors; cb.normalColor = Color.white; cb.highlightedColor = new Color(.88f, .94f, 1f); cb.pressedColor = new Color(.75f, .85f, .95f); btn.colors = cb;
                MapEditorScreen.HBox(bg.transform, 8, new RectOffset(10, 10, 6, 6));
                MapEditorScreen.Size(bg, -1, 50);

                var badge = MapEditorScreen.NewImage("Badge", bg.transform, Color.clear, false); MapEditorScreen.Size(badge, 32, 32);
                TMP_Text badgeText = MapEditorScreen.NewText(badge.transform, "", 20, FontStyles.Bold, Color.white, TextAlignmentOptions.Center, UITheme.TextRole.Display);
                MapEditorScreen.Stretch(badgeText.rectTransform);
                if (entry.IsCorrupt) { badge.color = new Color32(0x5a, 0x10, 0x18, 0xff); badgeText.text = "×"; }
                else if (entry.HasRequiredError) { badge.color = Foe; badgeText.text = "!"; }

                string label = entry.displayName + (entry.IsCorrupt ? "  (破損)" : "");
                var name = MapEditorScreen.NewText(bg.transform, label, 20, FontStyles.Bold, Ink);
                name.textWrappingMode = TextWrappingModes.NoWrap; name.overflowMode = TextOverflowModes.Ellipsis;
                MapEditorScreen.Size(name, -1, 36, 1);
                var size = MapEditorScreen.NewText(bg.transform, entry.SizeText, 17, FontStyles.Normal, Ink); MapEditorScreen.Size(size, 80, 36);
                string tag = entry.IsDefault ? "読み取り専用 · 標準搭載" : entry.modified.ToString("yyyy-MM-dd HH:mm");
                var time = MapEditorScreen.NewText(bg.transform, tag, 15, FontStyles.Normal, Ink); MapEditorScreen.Size(time, 170, 36);
                time.textWrappingMode = TextWrappingModes.NoWrap;

                if (inUseStem != null && string.Equals(entry.stem, inUseStem, StringComparison.OrdinalIgnoreCase))
                    name.text += "  <size=70%><color=#1f96e8>● 使用中</color></size>";

                btn.onClick.AddListener(() => { selected = entry; ShowSelected(); });
                rows.Add((entry, bg, new TMP_Text[] { name, size, time }));
            }
        }

        void ShowSelected()
        {
            foreach (var r in rows)
            {
                bool on = r.entry == selected;
                r.bg.color = on ? Blue : Paper;
                for (int i = 0; i < r.texts.Length; i++) r.texts[i].color = on ? Color.white : (r.entry.IsCorrupt && i == 0 ? ErrText : Ink);
            }
            var e = selected;
            previewName.text = e.displayName;
            if (previewTexture != null) { Destroy(previewTexture); previewTexture = null; }
            if (e.IsCorrupt)
            {
                previewInfo.text = "破損したファイル · " + e.modified.ToString("yyyy-MM-dd HH:mm");
                previewImage.texture = null; previewImage.color = Color.clear;
                previewStatus.color = ErrText;
                previewStatus.text = "このマップを開けません: " + e.corruptReason;
            }
            else
            {
                previewInfo.text = $"{e.SizeText} · " + (e.IsDefault ? "読み取り専用 (編集するには複製)" : "更新 " + e.modified.ToString("yyyy-MM-dd HH:mm"));
                previewTexture = MapPreview.Make(e.file);
                previewImage.texture = previewTexture; previewImage.color = Color.white;
                previewFit.aspectRatio = e.width / (float)e.height;
                if (e.HasRequiredError)
                {
                    previewStatus.color = ErrText;
                    int shown = Mathf.Min(3, e.errors.Count);
                    var sb = new System.Text.StringBuilder($"必須エラー{e.errors.Count}件 — ゲーム開始には使えません (編集は可能)");
                    for (int i = 0; i < shown; i++) sb.Append("\n· ").Append(e.errors[i]);
                    if (e.errors.Count > shown) sb.Append($"\n· … ほか{e.errors.Count - shown}件");
                    previewStatus.text = sb.ToString();
                }
                else { previewStatus.color = OkText; previewStatus.text = "必須エラーなし — ゲーム開始に使えます"; }
            }
            pathText.text = e.IsDefault || string.IsNullOrEmpty(e.stem) ? "保存先: " + MapStore.Directory : MapStore.PathFor(e.stem);
            cloneButton.interactable = !e.IsCorrupt;
            deleteButton.interactable = !e.IsDefault;
            openButton.interactable = true;
        }

        // ---- the buttons ---------------------------------------------------------------------------------------------------------

        void OnOpenClicked()
        {
            var e = selected;
            if (e == null) return;
            if (e.IsCorrupt) { ShowMessage("マップを開けません", "このマップを開けません: " + e.corruptReason); return; }
            var cb = onOpen;
            Close();
            cb?.Invoke(e);
        }

        void OnCloneClicked()
        {
            var e = selected;
            if (e == null || e.IsCorrupt) return;
            try
            {
                string stem = MapStore.Duplicate(e);
                Reload(stem);
            }
            catch (Exception ex) { ShowMessage("複製できません", "マップを複製できませんでした: " + ex.Message); }
        }

        void OnDeleteClicked()
        {
            var e = selected;
            if (e == null || e.IsDefault) return;
            ModalDialog.Show(canvas.transform, "マップ削除", $"'{e.displayName}' ファイルを削除しますか？\n削除したマップは元に戻せません。\n{MapStore.PathFor(e.stem)}",
                ModalDialog.Choice.Plain("キャンセル"),
                ModalDialog.Choice.Danger("削除", () =>
                {
                    try
                    {
                        MapStore.Delete(e.stem);
                        if (string.Equals(MapStore.SelectedStem, e.stem, StringComparison.OrdinalIgnoreCase)) MapStore.SelectedStem = "";
                        inUseStem = mode == Mode.Title ? MapStore.SelectedStem : null;
                        Reload("");
                    }
                    catch (Exception ex) { ShowMessage("削除できません", "ファイルを削除できませんでした: " + ex.Message); }
                }));
        }

        public void ShowMessage(string title, string body)
        {
            ModalDialog.Show(canvas.transform, title, body, ModalDialog.Choice.Primary("確認"));
        }
    }
}
