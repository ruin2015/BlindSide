// The in-game map editor screen: the "new map / clone the default house" window, then the editor — top bar (map name, size, and
// disabled Save / Load / Test Play slots), tool palette on the left, properties + validation panels on the right (placeholders),
// a hint / coordinate bar at the bottom — and the input that drives MapEditModel / MapCellView.
//
// Input: the camera is the game's own BattlefieldCamera (WASD, wheel) kept over the map (Limit); right-drag pans it and a right
// click without a drag erases. Left click = one cell, left drag = a straight line locked to the longer axis, Shift+drag = a
// rectangle (walls: border only), 0-6 pick tools, Ctrl+Z / Ctrl+Y undo / redo, G toggles the grid. Typing in the map name box
// takes the keyboard (no hotkeys, no camera keys).
// Objects: 7 hostage (click = place / move) · 8 defender start (click = next number 1→5, then the 5th moves; drag = facing) ·
// 9 camera (≤ 8) · - exfil zone (drag a rectangle, ≤ 8) · S select. With these tools a right click deletes the object under the
// cursor instead of erasing terrain. A refused placement shows a red X (on the cell and as the mouse cursor) and the reason in the
// bottom bar. S is taken from the camera's WASD (↓ pans down). The right panels — properties, validation (clicking a row flies the
// camera there), wall statistics — and the top bar's overlay toggles (drop zone, camera sight, grid) follow the model's Version.
// Files: [保存] (Ctrl+S) writes Maps/{name}.json — also with required errors (work in progress), asking first when another map's file
// would be overwritten; [読み込み] opens the map list (MapBrowserWindow); the built-in 基本邸宅 opens read-only. Leaving, loading
// another map or starting a new one with unsaved changes asks 保存 / 破棄 / キャンセル. [攻撃でテスト] / [防衛でテスト] (disabled with the
// reasons on hover while there are required errors) start a match on a copy of the map being edited (unsaved changes included)
// through TitleSelectScreen.Launch; the editor is only suspended meanwhile — map, undo history, selection and camera stay — and the
// result screen's [エディタへ] resumes it.
// The editor never touches the game's state: it edits its own copy of a map (never MapLibrary.Current), hides the scene's default
// map while it is open and puts everything back — camera, the map, the title — when it closes.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Blindside.Data;
using Blindside.Integration;
using Blindside.Map;
using Blindside.Rendering;
using Blindside.UI;

namespace Blindside.MapEditor
{
    public sealed class MapEditorScreen : MonoBehaviour
    {
        public const int MinSize = 16, MaxSize = 40, DefaultWidth = 26, DefaultHeight = 20;
        internal static readonly Color Paper = UIPalette.Paper, Ink = UIPalette.Ink;
        internal static readonly Color Blue = new Color32(0x1f, 0x96, 0xe8, 0xff);
        internal static readonly Color Foe = new Color32(0xe5, 0x36, 0x4d, 0xff);
        internal static readonly Color PaperDark = new Color32(0xc9, 0xce, 0xd4, 0xff);
        internal static readonly Color InkLight = new Color32(0x26, 0x2a, 0x31, 0xff);

        public enum Phase { Dialog, Editing, Testing, Closed }

        public static MapEditorScreen Instance { get; private set; }

        public Phase State { get; private set; } = Phase.Dialog;
        public MapEditModel Model { get; private set; }
        public MapCellView View { get; private set; }
        public EditTool Tool { get; private set; } = EditTool.BreakableWall;
        /// <summary>Milliseconds the last stroke took to apply and redraw, and the last build of the whole map view (for the 40×40 check).</summary>
        public float LastStrokeMs { get; private set; }
        public float BuildMs { get; private set; }

        Action onClosed;
        Canvas canvas;
        RectTransform dialogRoot, editorRoot;
        TMP_InputField widthInput, heightInput, nameInput;
        TMP_Text sizeLabel, coordLabel, hintLabel;
        readonly Dictionary<EditTool, (Image bg, TMP_Text name, TMP_Text key)> palette = new Dictionary<EditTool, (Image, TMP_Text, TMP_Text)>();

        // objects, validation and their panels
        ObjRef selected;
        int shownVersion = -1;
        TMP_Text propTitle, propBody, validationSummary, statsBody;
        RectTransform validationList;
        readonly List<(Image bg, Func<bool> on)> toggles = new List<(Image, Func<bool>)>();
        public ValidationReport Validation { get; private set; }
        string clickWarning; float clickWarningUntil;   // a refused click: shown for a moment
        string hoverWarning;                            // the cursor is over a cell the tool can't use: shown while it stays
        float defenderFacing;
        Texture2D xCursor; bool xCursorOn;
        public const int MaxValidationRows = 14;
        GameObject mapRoot;
        CanvasGroup fade;      // the scene's black start-of-intro overlay: 1 on the title, and it would hide the map
        float fadeWas;
        readonly List<GameObject> hiddenExfil = new List<GameObject>();   // the game's own exfil zone labels (default map) while editing
        MapCellTemplates templates;                                        // the scene's blocks and floor materials, read once before the default map is hidden

        // files
        string fileStem;                 // the file this map is saved as (null: not saved yet); "" never
        string savedJson;                // the map's content at the last save / load / start (the baseline "unsaved changes" is measured from)
        string currentJson; int jsonVersion = -1; string jsonName;         // cache of the map's content as it is now
        bool dirty;
        bool readOnly;                   // the built-in default house: viewable and testable, not editable or savable
        string toast; float toastUntil;
        TMP_Text stateLabel;
        Button saveButton, testAttackButton, testDefenseButton;
        RectTransform tipRoot; TMP_Text tipText;
        Vector2 testCamXY; float testCamH;   // the camera when a test play started, restored on the way back
        const string ReadOnlyMessage = "基本邸宅は読み取り専用です — 読み込みウィンドウで[複製]してから編集してください";
        public bool IsDirty => dirty;
        public bool IsReadOnly => readOnly;
        public string FileStem => fileStem;

        // pointer state
        bool leftDown, rightDown, rightPanning;
        Vector2Int strokeStart, strokeEnd, rightCell;
        bool rightCellValid;
        Vector2 rightDownPos;
        GroundHit rightG0; bool rightG0Valid;

        // ---- opening / closing ---------------------------------------------------------------------------------------------

        /// <summary>Opens the editor over the title: first the new-map window. `onClosed` runs when the editor is left (the title
        /// shows itself again).</summary>
        public static MapEditorScreen Open(Action onClosed)
        {
            if (Instance != null) return Instance;
            var go = new GameObject("MapEditor");
            var s = go.AddComponent<MapEditorScreen>();
            s.onClosed = onClosed;
            return s;
        }

        void Awake()
        {
            Instance = this;
            BuildUI();
            ShowDialog();
        }

        void OnDestroy() { if (Instance == this) Instance = null; }

        /// <summary>Leaves the editor at once (no question asked) and gives the title back.</summary>
        public void Close()
        {
            if (State == Phase.Closed || State == Phase.Testing) return;
            State = Phase.Closed;
            if (Instance == this) Instance = null;   // (Destroy below is deferred; the next Open() must not find this one)
            View?.Dispose(); View = null;
            Model = null;
            SetXCursor(false);
            if (xCursor != null) Destroy(xCursor);
            if (mapRoot != null) mapRoot.SetActive(true);
            foreach (var g in hiddenExfil) if (g != null) g.SetActive(true);
            hiddenExfil.Clear();
            templates = null;
            if (fade != null) fade.alpha = fadeWas;
            BattlefieldCamera.SKeyReserved = false;
            BattlefieldCamera.InputLocked = false;
            BattlefieldCamera.Deactivate();
            if (canvas != null) Destroy(canvas.gameObject);
            onClosed?.Invoke();
            Destroy(gameObject);
        }

        /// <summary>The exit button / Esc: asks 保存 / 破棄 / キャンセル first when there are unsaved changes.</summary>
        public void RequestExit()
        {
            if (State == Phase.Testing || ModalDialog.Any) return;
            if (State == Phase.Editing) GuardUnsaved(Close); else Close();
        }

        /// <summary>Runs `proceed` at once when nothing would be lost; otherwise asks: [保存] saves first (proceed only if the save went through),
        /// [破棄] proceeds, [キャンセル] stays.</summary>
        void GuardUnsaved(Action proceed)
        {
            RefreshDirty();
            if (Model == null || !dirty || readOnly) { proceed(); return; }
            ModalDialog.Show(canvas.transform, "未保存の変更があります", $"'{Model.Data.name}'の変更内容を保存しますか？",
                ModalDialog.Choice.Primary("保存", () => RequestSave(proceed)),
                ModalDialog.Choice.Danger("破棄", proceed),
                ModalDialog.Choice.Plain("キャンセル"));
        }

        // ---- starting a map ------------------------------------------------------------------------------------------------

        public void ChooseNew(int w, int h)
        {
            w = Mathf.Clamp(w, MinSize, MaxSize); h = Mathf.Clamp(h, MinSize, MaxSize);
            // a new map starts with no objects at all
            StartSession(new MapEditModel(MapData.CreateBlank("新規マップ", w, h), false), null, false);
        }

        public void ChooseClone()
        {
            var d = MapLibrary.LoadDefault();   // a fresh copy: the game's own map is never edited
            d.name = "基本邸宅 (複製)";
            d.useHouseIntro = false;            // the house intro flies over the baked house; an edited copy gets the short intro
            StartSession(new MapEditModel(d, true), null, false);
        }

        /// <summary>Opens a listed map for editing (the default house read-only).</summary>
        public void OpenEntry(MapEntry e)
        {
            if (e == null || e.IsCorrupt) return;
            if (e.IsDefault) StartSession(e.ToModel(), "", true);
            else StartSession(e.ToModel(), e.stem, false);
        }

        /// <summary>The 読み込み window; a map picked there replaces the one being edited (after the unsaved-changes question).</summary>
        public void OpenBrowser()
        {
            if (ModalDialog.Any) return;
            MapBrowserWindow.Open(MapBrowserWindow.Mode.Editor, fileStem ?? "", e => GuardUnsaved(() => OpenEntry(e)), null);
        }

        /// <summary>Back to the "new map / clone / load" window (the map being edited goes; ask first via GuardUnsaved).</summary>
        void ReturnToDialog()
        {
            EndSession();
            editorRoot.gameObject.SetActive(false);
            ShowDialog();
        }

        void EndSession()
        {
            leftDown = false; rightDown = false;
            if (View != null) { View.Dispose(); View = null; }
            Model = null;
            selected = default; shownVersion = -1; jsonVersion = -1;
            SetXCursor(false);
        }

        /// <summary>Starts editing `model`: `stem` is the file it came from (null: none), `ro` the read-only default house. The baseline for
        /// "unsaved changes" is the map as it is now.</summary>
        void StartSession(MapEditModel model, string stem, bool ro)
        {
            EndSession();
            dialogRoot.gameObject.SetActive(false);
            var sw = Stopwatch.StartNew();
            if (templates == null)
            {
                templates = MapCellTemplates.Capture();       // before the default map is hidden (once)
                mapRoot = GameObject.Find("Blindside_MapRoot");
                if (mapRoot != null) mapRoot.SetActive(false);
                var fadeGo = GameObject.Find("BlindsideCanvas/Fade");
                fade = fadeGo != null ? fadeGo.GetComponent<CanvasGroup>() : null;
                if (fade != null) { fadeWas = fade.alpha; fade.alpha = 0f; }
                foreach (var ev in FindObjectsByType<ExfilZoneView>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                {
                    ev.gameObject.SetActive(false);     // it draws the default map's zones and labels
                    hiddenExfil.Add(ev.gameObject);
                }
            }

            var data = model.Data;
            Model = model;
            View = new MapCellView(Model, templates);
            Model.CellsChanged += View.Refresh;
            Model.PanelsChanged += View.RebuildPanelLines;
            Model.ObjectsChanged += View.RebuildObjects;
            Model.ObjectsChanged += OnObjectsChanged;
            Model.HistoryChanged += UpdateBottom;
            selected = default; shownVersion = -1;
            fileStem = stem; readOnly = ro;
            savedJson = SnapshotJson(Model); currentJson = savedJson; jsonVersion = Model.Version; jsonName = data.name; dirty = false;
            BattlefieldCamera.SKeyReserved = true;
            BuildMs = (float)sw.Elapsed.TotalMilliseconds;

            var cam = BattlefieldCamera.Activate((data.width - 1) / 2f, (data.height - 1) / 2f);
            if (cam != null)
            {
                cam.h = Mathf.Clamp(Mathf.Max(data.width, data.height) * 1.05f, 20f, BattlefieldCamera.MaxH);
                cam.Limit = new Rect(0, 0, data.width - 1, data.height - 1);
                cam.Apply();
            }
            nameInput.text = data.name;
            nameInput.interactable = !ro;
            sizeLabel.text = $"{data.width} × {data.height}";
            editorRoot.gameObject.SetActive(true);
            SelectTool(ro ? EditTool.Select : Tool);
            UpdateBottom();
            State = Phase.Editing;
            RefreshPanels();
        }

        // ---- saving ----------------------------------------------------------------------------------------------------------

        static string SnapshotJson(MapEditModel m) => MapFile.FromModel(m).ToJson();

        /// <summary>Recomputes "unsaved changes" (only when the map or its name changed since the last look).</summary>
        void RefreshDirty()
        {
            if (Model == null) return;
            if (Model.Version != jsonVersion || Model.Data.name != jsonName)
            {
                currentJson = SnapshotJson(Model); jsonVersion = Model.Version; jsonName = Model.Data.name;
            }
            dirty = !readOnly && currentJson != savedJson;
            if (stateLabel != null)
            {
                stateLabel.text = readOnly ? "読み取り専用" : dirty ? "● 未保存" : fileStem != null ? "保存済み" : "新規マップ (未保存)";
                stateLabel.color = readOnly ? new Color(1, 1, 1, .6f) : dirty ? new Color(1f, .72f, .2f) : new Color(1, 1, 1, .6f);
            }
            if (saveButton != null) saveButton.interactable = !readOnly;
        }

        /// <summary>The 保存 button / Ctrl+S: writes Maps/{name}.json. Characters a file name can't hold are replaced by "_"; a file of another map
        /// with the same name is overwritten only after asking. Works with required errors too. `after` runs once the file is written.</summary>
        public void RequestSave(Action after = null)
        {
            if (Model == null || readOnly || State != Phase.Editing) return;
            string name = (Model.Data.name ?? "").Trim();
            if (name.Length == 0) name = "新規マップ";
            Model.Data.name = name; nameInput.text = name;
            string stem = MapStore.SanitizeFileName(name);
            if (MapStore.IsDefaultStem(stem))
            {
                Message("保存できません", $"'{MapStore.DefaultName}'は標準搭載マップの名前です。別の名前で保存してください。");
                return;
            }
            bool sameFile = fileStem != null && string.Equals(fileStem, stem, StringComparison.OrdinalIgnoreCase);
            if (!sameFile && MapStore.Exists(stem))
            {
                ModalDialog.Show(canvas.transform, "上書き", $"'{stem}.json' ファイルがすでにあります。\n上書きすると、そのマップは現在編集中のマップに置き換わります。上書きしますか？",
                    ModalDialog.Choice.Plain("キャンセル"), ModalDialog.Choice.Danger("上書き", () => DoSave(stem, after)));
                return;
            }
            DoSave(stem, after);
        }

        void DoSave(string stem, Action after)
        {
            string path = MapStore.PathFor(stem);
            try { MapStore.Save(MapFile.FromModel(Model), stem); }
            catch (Exception ex)
            {
                Message("保存できません", "マップファイルを書き込めませんでした: " + ex.Message + "\n" + path);
                return;
            }
            fileStem = stem;
            savedJson = SnapshotJson(Model); currentJson = savedJson; jsonVersion = Model.Version; jsonName = Model.Data.name;
            RefreshDirty();
            string renamed = stem != Model.Data.name ? $" (ファイル名に使えない文字を置き換えました: {stem}.json)" : "";
            int errors = Validation != null ? Validation.ErrorCount : 0;
            Toast($"保存しました: {path}{renamed}" + (errors > 0 ? $" — 必須エラーが{errors}件あるため、ゲーム開始には使えません" : ""));
            after?.Invoke();
        }

        void Toast(string text) { toast = text; toastUntil = Time.unscaledTime + 5f; }

        void Message(string title, string body)
        {
            ModalDialog.Show(canvas.transform, title, body, ModalDialog.Choice.Primary("確認"));
        }

        // ---- test play -------------------------------------------------------------------------------------------------------

        /// <summary>Why a test play can't start now (the required errors), or null.</summary>
        string TestBlockReason()
        {
            if (Validation == null || Validation.CanTestPlay) return null;
            var sb = new System.Text.StringBuilder($"必須エラー{Validation.ErrorCount}件 — 修正してからテストできます:");
            foreach (var i in Validation.issues) if (i.error) sb.Append("\n· ").Append(i.text);
            return sb.ToString();
        }

        /// <summary>Starts a match of `team` on the map as it is now, unsaved changes included. The game gets a copy; the editor is only
        /// suspended (its map, undo history and camera stay) and resumes when the result screen's [エディタへ] is pressed.</summary>
        public void StartTest(OperatorTeam team)
        {
            if (State != Phase.Editing || Model == null || ModalDialog.Any) return;
            if (TestBlockReason() != null) return;
            MapData data;
            try
            {
                data = Model.BuildGameData();
                var problems = data.Validate();
                if (problems.Count > 0) throw new InvalidOperationException(string.Join("; ", problems));
            }
            catch (Exception ex) { Message("テストできません", ex.Message); return; }
            var title = FindFirstObjectByType<TitleSelectScreen>(FindObjectsInactive.Include);
            if (title == null) { Message("テストできません", "タイトル画面が見つかりません。"); return; }
            Suspend();
            if (!title.Launch(team, data, ResumeFromTest)) { ResumeFromTest(); Message("テストできません", "ゲームがこのマップを受け付けませんでした (Console参照)。"); }
        }

        /// <summary>The editor steps aside: its screen and its drawing of the map are hidden, the scene's own map is back for the game.</summary>
        void Suspend()
        {
            var cam = BattlefieldCamera.Instance;
            if (cam != null) { testCamXY = new Vector2(cam.x, cam.y); testCamH = cam.h; }
            State = Phase.Testing;
            leftDown = false; rightDown = false;
            View.SetInvalid(null); View.SetHover(null); View.ClearPreview(); View.SetDefenderPreview(null, 0, 0);
            SetXCursor(false);
            tipRoot.gameObject.SetActive(false);
            BattlefieldCamera.SKeyReserved = false;
            BattlefieldCamera.InputLocked = false;
            canvas.gameObject.SetActive(false);
            View.Root.SetActive(false);
            if (mapRoot != null) mapRoot.SetActive(true);
            foreach (var g in hiddenExfil) if (g != null) g.SetActive(true);
        }

        /// <summary>The way back from a test play ([エディタへ]): everything as it was left, history included.</summary>
        void ResumeFromTest()
        {
            if (State != Phase.Testing || Model == null) return;
            if (mapRoot != null) mapRoot.SetActive(false);
            foreach (var g in hiddenExfil) if (g != null) g.SetActive(false);
            if (fade != null) fade.alpha = 0f;
            View.Root.SetActive(true);
            canvas.gameObject.SetActive(true);
            var d = Model.Data;
            var cam = BattlefieldCamera.Activate(testCamXY.x, testCamXY.y);
            if (cam != null) { cam.h = testCamH; cam.Limit = new Rect(0, 0, d.width - 1, d.height - 1); cam.Apply(); }
            BattlefieldCamera.SKeyReserved = true;
            BattlefieldCamera.InputLocked = false;
            State = Phase.Editing;
            shownVersion = -1;
            RefreshPanels();
            UpdateBottom();
        }

        // ---- drawing (also the path the input takes) -----------------------------------------------------------------------

        /// <summary>Applies one stroke: model change, then a redraw of the changed cells only.</summary>
        public void ApplyStroke(EditTool tool, IEnumerable<Vector2Int> cells)
        {
            var sw = Stopwatch.StartNew();
            Model.Apply(tool, cells);    // CellsChanged / PanelsChanged redraw exactly what changed
            LastStrokeMs = (float)sw.Elapsed.TotalMilliseconds;
        }

        public void SelectTool(EditTool t)
        {
            Tool = t;
            leftDown = false;
            if (View != null) { View.ClearPreview(); View.SetDefenderPreview(null, 0, 0); }
            foreach (var kv in palette)
            {
                bool on = kv.Key == t;
                kv.Value.bg.color = on ? Blue : Paper;
                kv.Value.name.color = on ? Color.white : Ink;   // (the hotkey badge stays ink with paper text)
            }
        }

        void UpdateBottom()
        {
            if (coordLabel == null) return;
            coordLabel.text = $"{ToolName(Tool)}   ·   元に戻す {(Model != null ? Model.UndoDepth : 0)}";
        }

        // ---- per-frame input -----------------------------------------------------------------------------------------------

        // 0-9 pick the tools whose enum value is that digit (erase, floors, walls, crate, hostage, defender start, camera)
        static readonly Key[] DigitKeys = { Key.Digit0, Key.Digit1, Key.Digit2, Key.Digit3, Key.Digit4, Key.Digit5, Key.Digit6, Key.Digit7, Key.Digit8, Key.Digit9 };
        static readonly Key[] PadKeys = { Key.Numpad0, Key.Numpad1, Key.Numpad2, Key.Numpad3, Key.Numpad4, Key.Numpad5, Key.Numpad6, Key.Numpad7, Key.Numpad8, Key.Numpad9 };

        static bool Typing()
        {
            var es = EventSystem.current;
            if (es == null || es.currentSelectedGameObject == null) return false;
            var f = es.currentSelectedGameObject.GetComponent<TMP_InputField>();
            return f != null && f.isFocused;
        }

        void Update()
        {
            if (State != Phase.Editing) { if (State == Phase.Dialog) BattlefieldCamera.InputLocked = false; return; }
            if (ModalDialog.Any) { BattlefieldCamera.InputLocked = true; SetXCursor(false); View.SetInvalid(null); return; }

            bool typing = Typing();
            BattlefieldCamera.InputLocked = typing;
            var kb = Keyboard.current;
            if (kb != null && !typing) HandleKeys(kb);
            if (State != Phase.Editing) return;     // (Esc closed the editor)
            HandlePointer(kb);
            if (State != Phase.Editing) return;
            if (Model.Version != shownVersion) RefreshPanels();
            RefreshDirty();
            foreach (var t in toggles) t.bg.color = t.on() ? Blue : InkLight;
            UpdateHint();
        }

        void HandleKeys(Keyboard kb)
        {
            for (int i = 0; i < DigitKeys.Length; i++)
                if (kb[DigitKeys[i]].wasPressedThisFrame || kb[PadKeys[i]].wasPressedThisFrame) { SelectTool((EditTool)i); UpdateBottom(); }
            if (kb.minusKey.wasPressedThisFrame || kb.numpadMinusKey.wasPressedThisFrame) { SelectTool(EditTool.Exfil); UpdateBottom(); }
            if (kb.sKey.wasPressedThisFrame && !kb.ctrlKey.isPressed) { SelectTool(EditTool.Select); UpdateBottom(); }
            if (kb.gKey.wasPressedThisFrame) View.GridVisible = !View.GridVisible;
            if (kb.ctrlKey.isPressed)
            {
                if (kb.sKey.wasPressedThisFrame) RequestSave();
                if (kb.zKey.wasPressedThisFrame) { if (kb.shiftKey.isPressed) Model.Redo(); else Model.Undo(); }
                if (kb.yKey.wasPressedThisFrame) Model.Redo();
            }
            if (kb.escapeKey.wasPressedThisFrame) RequestExit();
        }

        bool CellUnderPointer(Vector2 pos, bool clamp, out Vector2Int cell)
        {
            cell = default;
            if (!BattlefieldCamera.GroundAt(pos, out var g)) return false;
            int x = g.x, y = g.y;
            if (clamp) { x = Mathf.Clamp(x, 0, Model.Width - 1); y = Mathf.Clamp(y, 0, Model.Height - 1); }
            else if (!Model.InBounds(x, y)) return false;
            cell = new Vector2Int(x, y);
            return true;
        }

        void HandlePointer(Keyboard kb)
        {
            var mouse = Mouse.current;
            if (mouse == null) return;
            Vector2 pos = mouse.position.ReadValue();
            bool overUi = OverEditorUi(pos);
            bool shift = kb != null && kb.shiftKey.isPressed;
            bool typing = Typing();

            bool hasCell = CellUnderPointer(pos, false, out var hover);
            bool onMap = !overUi && hasCell;
            View.SetHover(onMap ? hover : (Vector2Int?)null);
            if (onMap) coordLabel.text = $"({hover.x}, {hover.y})   ·   {ToolName(Tool)}   ·   元に戻す {Model.UndoDepth}";
            else if (!leftDown) UpdateBottom();

            // An object tool over a cell it can't use: the red X and the reason (a drag below may replace this).
            string refused = null; Vector2Int refusedCell = hover;
            if (IsObjectTool(Tool) && onMap && !leftDown && !rightDown && !readOnly) refused = CheckFor(Tool, hover);

            // ---- left: terrain = a click, a line, a rectangle; objects = see BeginLeft / ContinueLeft ----
            if (mouse.leftButton.wasPressedThisFrame && !overUi && !typing && !rightDown && onMap)
            {
                if (readOnly && Tool != EditTool.Select) Warn(ReadOnlyMessage);
                else if (refused != null) Warn(refused);
                else BeginLeft(hover);
            }
            if (leftDown)
            {
                if (CellUnderPointer(pos, true, out var cur)) strokeEnd = cur;
                bool released = mouse.leftButton.wasReleasedThisFrame;
                if (released) leftDown = false;
                refused = ContinueLeft(pos, shift, released);
                refusedCell = strokeEnd;
            }

            View.SetInvalid(refused != null ? refusedCell : (Vector2Int?)null);
            SetXCursor(refused != null);
            hoverWarning = refused;

            // ---- right: drag pans the camera, a click without a drag erases (terrain tools) or deletes the object there ----
            if (mouse.rightButton.wasPressedThisFrame && !overUi && !typing && !leftDown)
            {
                rightDown = true; rightPanning = false; rightDownPos = pos;
                rightG0Valid = BattlefieldCamera.GroundAt(pos, out rightG0);
                rightCellValid = onMap; rightCell = hover;
            }
            if (rightDown)
            {
                if (!rightPanning && (pos - rightDownPos).magnitude > 8f) rightPanning = true;
                if (rightPanning && rightG0Valid && BattlefieldCamera.Instance != null && BattlefieldCamera.GroundAt(pos, out var g1))
                    BattlefieldCamera.Instance.Pan(rightG0.fx - g1.fx, rightG0.fy - g1.fy);
                if (mouse.rightButton.wasReleasedThisFrame)
                {
                    if (!rightPanning && rightCellValid) RightClick(rightCell);
                    rightDown = false; rightPanning = false;
                }
            }
        }

        // Only the editor's own canvas counts as "over the UI": the game's HUD canvases stay in the scene under the editor, and one of
        // them (the headshot badge) has a full-screen raycast-target text in the middle of the screen — EventSystem's
        // IsPointerOverGameObject would see that and swallow the clicks on the centre of the map.
        GraphicRaycaster uiRaycaster;
        PointerEventData uiPointer;
        readonly List<RaycastResult> uiHits = new List<RaycastResult>();

        bool OverEditorUi(Vector2 pos)
        {
            if (uiRaycaster == null || EventSystem.current == null) return false;
            if (uiPointer == null) uiPointer = new PointerEventData(EventSystem.current);
            uiPointer.position = pos;
            uiHits.Clear();
            uiRaycaster.Raycast(uiPointer, uiHits);
            return uiHits.Count > 0;
        }

        static bool IsObjectTool(EditTool t) => t >= EditTool.Hostage && t <= EditTool.Exfil;

        /// <summary>Why the current object tool can't be used on `c` (null: it can).</summary>
        string CheckFor(EditTool t, Vector2Int c)
        {
            switch (t)
            {
                case EditTool.Hostage: return Model.CheckHostage(c);
                case EditTool.Defender: return Model.CheckDefender(c);
                case EditTool.Camera: return Model.CheckCamera(c);
                case EditTool.Exfil: return Model.CheckExfil(c, c);
                default: return null;
            }
        }

        static readonly Color InvalidTint = new Color(.96f, .13f, .2f, .55f);

        /// <summary>A left press on a cell the tool accepts. Terrain tools start a stroke; hostage and camera place at once; the
        /// defender start and the exfil zone start a drag (facing / rectangle) that ContinueLeft finishes; S selects.</summary>
        void BeginLeft(Vector2Int cell)
        {
            switch (Tool)
            {
                case EditTool.Hostage:
                    if (Placed(Model.PlaceHostage(cell))) SelectObject(new ObjRef(ObjKind.Hostage, 0));
                    break;
                case EditTool.Camera:
                    if (Placed(Model.PlaceCamera(cell))) SelectObject(new ObjRef(ObjKind.Camera, Model.Objects.cameras.Count - 1));
                    break;
                case EditTool.Defender:
                    int slot = Model.DefenderTargetSlot();
                    
                    defenderFacing = Model.Objects.defPlaced[slot] ? Model.Objects.defFacing[slot] : 0f;   // a plain click keeps a moved one's facing
                    leftDown = true; strokeStart = strokeEnd = cell;
                    break;
                case EditTool.Exfil:
                    leftDown = true; strokeStart = strokeEnd = cell;
                    break;
                case EditTool.Select:
                    SelectObject(Model.ObjectAt(cell));
                    break;
                default:
                    leftDown = true; strokeStart = strokeEnd = cell;
                    break;
            }
        }

        /// <summary>While the left button is down (or the frame it is released): preview, and on release apply. Returns the reason the
        /// drag can't be applied (for the red X), or null.</summary>
        string ContinueLeft(Vector2 pos, bool shift, bool released)
        {
            if (!IsObjectTool(Tool))
            {
                var cells = MapEditModel.StrokeCells(Tool, strokeStart, strokeEnd, shift);
                if (released) { View.ClearPreview(); ApplyStroke(Tool, cells); }
                else View.SetPreview(cells, MapCellView.TintFor(Tool));
                return null;
            }
            if (Tool == EditTool.Defender)
            {
                // the cursor's direction from the cell's centre, once it has left the middle of the cell, is the facing
                if (BattlefieldCamera.GroundAt(pos, out var g))
                {
                    float dx = g.fx - strokeStart.x, dy = g.fy - strokeStart.y;
                    if (dx * dx + dy * dy > .35f * .35f) { defenderFacing = Mathf.Atan2(dy, dx); }
                }
                if (!released) { View.SetDefenderPreview(strokeStart, Model.DefenderTargetSlot(), defenderFacing); return null; }
                View.SetDefenderPreview(null, 0, 0);
                if (Placed(Model.PlaceDefender(strokeStart, defenderFacing, out int slot))) SelectObject(new ObjRef(ObjKind.Defender, slot));
                return null;
            }
            // exfil zone: a rectangle from the press cell to the cursor cell
            string why = Model.CheckExfil(strokeStart, strokeEnd);
            if (!released)
            {
                View.SetPreview(MapEditModel.RectCells(strokeStart, strokeEnd, false), why != null ? InvalidTint : MapCellView.TintFor(Tool));
                return why;
            }
            View.ClearPreview();
            if (Placed(Model.AddExfil(strokeStart, strokeEnd))) SelectObject(new ObjRef(ObjKind.Exfil, Model.Objects.exfils.Count - 1));
            return null;
        }

        /// <summary>A placement result: null = done; otherwise the refusal goes to the bar. Returns whether it was done.</summary>
        bool Placed(string refusal)
        {
            if (refusal != null) Warn(refusal);
            return refusal == null;
        }

        void Warn(string text) { clickWarning = text; clickWarningUntil = Time.unscaledTime + 2.5f; }

        void RightClick(Vector2Int cell)
        {
            if (readOnly) { Warn(ReadOnlyMessage); return; }
            if (!IsObjectTool(Tool) && Tool != EditTool.Select) { ApplyStroke(EditTool.Erase, new[] { cell }); return; }
            var r = Model.ObjectAt(cell);
            if (!r.IsNone) Model.Delete(r);
        }

        public void SelectObject(ObjRef r)
        {
            selected = r;
            View.SetSelection(r);
            RefreshProperties();
        }

        void OnObjectsChanged() { selected = default; }     // indices shift when one goes; a placement selects its new object right after

        // A 32×32 red X with a dark outline — the mouse cursor while a placement is refused.
        void SetXCursor(bool on)
        {
            if (on == xCursorOn) return;
            xCursorOn = on;
            if (!on) { Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto); return; }
            if (xCursor == null)
            {
                xCursor = new Texture2D(32, 32, TextureFormat.RGBA32, false) { name = "RedXCursor" };
                var px = new Color32[32 * 32];
                for (int y = 0; y < 32; y++)
                    for (int x = 0; x < 32; x++)
                    {
                        float d = Mathf.Min(Mathf.Abs(x - y), Mathf.Abs(x - (31 - y)));
                        bool inside = x >= 4 && x <= 27 && y >= 4 && y <= 27;
                        px[y * 32 + x] = !inside || d > 3.4f ? new Color32(0, 0, 0, 0) : d <= 1.7f ? new Color32(245, 35, 50, 255) : new Color32(12, 12, 16, 255);
                    }
                xCursor.SetPixels32(px); xCursor.Apply();
            }
            Cursor.SetCursor(xCursor, new Vector2(16, 16), CursorMode.Auto);
        }

        // ---- the bottom bar's hint / warning ------------------------------------------------------------------------------

        static readonly Color HintColor = new Color(1f, 1f, 1f, .75f), WarnColor = new Color(1f, .42f, .42f, 1f);
        const string TerrainHint = "左クリックで描画 · ドラッグで直線 · Shift+ドラッグで四角形(壁は外周) · 右クリックで消去 · 右ドラッグで移動 · WASD(S除く)/方向キー/ホイール · Ctrl+Z/Y · G グリッド";

        static string HintFor(EditTool t)
        {
            switch (t)
            {
                case EditTool.Hostage: return "人質: 屋内の床をクリックして配置 (マップに1個、もう一度クリックで移動) · 右クリックで削除 · Ctrl+Z 元に戻す";
                case EditTool.Defender: return "防衛開始位置: クリック = 1→5番の順に配置 (5個そろうと5番を移動) · クリックしてドラッグ = 向く方向 · 右クリックで削除";
                case EditTool.Camera: return "監視カメラ: 床のマスをクリック (最大8個) · 右クリックで削除";
                case EditTool.Exfil: return "救出地点: 屋外の床でドラッグして四角形 (最大8個、A·B·C… のラベル) · 右クリックで削除";
                case EditTool.Select: return "選択: オブジェクトをクリックでプロパティを表示 · 右クリックで削除 · カメラの下移動は↓方向キー";
                default: return TerrainHint;
            }
        }

        void UpdateHint()
        {
            if (hintLabel == null) return;
            string warn = clickWarning != null && Time.unscaledTime < clickWarningUntil ? clickWarning : hoverWarning;
            if (warn != null) { hintLabel.text = warn == ReadOnlyMessage ? warn : "配置不可 · " + warn; hintLabel.color = WarnColor; return; }
            if (toast != null && Time.unscaledTime < toastUntil) { hintLabel.text = toast; hintLabel.color = new Color(.45f, .92f, .55f); return; }
            hintLabel.text = readOnly && Tool != EditTool.Select ? ReadOnlyMessage : HintFor(Tool);
            hintLabel.color = HintColor;
        }

        // ---- the right panels ------------------------------------------------------------------------------------------------

        static readonly Color ErrText = new Color32(0xb3, 0x1b, 0x2b, 0xff), WarnText = new Color32(0x7a, 0x57, 0x00, 0xff);
        static readonly Color ErrBg = new Color32(0xfb, 0xdd, 0xe0, 0xff), WarnBg = new Color32(0xff, 0xf1, 0xb8, 0xff), OkText = new Color32(0x1c, 0x7a, 0x3a, 0xff);

        /// <summary>Validates the map again and redraws everything that shows it (validation list, statistics, properties, overlays).</summary>
        void RefreshPanels()
        {
            shownVersion = Model.Version;
            Validation = MapValidator.Validate(Model);
            RefreshTestButtons();
            if (View.ShowDrop || View.ShowCameraFov) View.RebuildOverlays();
            View.SetSelection(selected);
            RefreshProperties();
            RefreshValidation();
            RefreshStats();
        }

        void RefreshTestButtons()
        {
            bool ok = Validation.CanTestPlay;
            if (testAttackButton != null) testAttackButton.interactable = ok;
            if (testDefenseButton != null) testDefenseButton.interactable = ok;
        }

        void RefreshProperties()
        {
            var o = Model.Objects;
            string title, body;
            switch (selected.kind)
            {
                case ObjKind.Hostage when o.hostage.HasValue:
                    title = "人質"; body = $"座標  ({o.hostage.Value.x}, {o.hostage.Value.y})";
                    break;
                case ObjKind.Defender when selected.index >= 0 && selected.index < ObjectState.DefenderSlots && o.defPlaced[selected.index]:
                    {
                        int i = selected.index; var c = o.defCell[i];
                        float deg = Mathf.Repeat(o.defFacing[i] * Mathf.Rad2Deg, 360f);
                        title = $"防衛開始位置{i + 1}番";
                        body = $"隊員  {MapData.DefenderKeys[i]}\n座標  ({c.x}, {c.y})\n向き  {deg:0}° ({Compass(deg)})";
                        break;
                    }
                case ObjKind.Camera when selected.index >= 0 && selected.index < o.cameras.Count:
                    {
                        var c = o.cameras[selected.index];
                        title = $"監視カメラ{selected.index + 1}番"; body = $"座標  ({c.x}, {c.y})\n視界半径  {MapSight.CameraRadius:0.0}m";
                        break;
                    }
                case ObjKind.Exfil when selected.index >= 0 && selected.index < o.exfils.Count:
                    {
                        var e = o.exfils[selected.index];
                        title = $"救出地点{MapEditModel.ExfilLabel(selected.index)}";
                        body = $"座標  ({e.x0}, {e.y0}) – ({e.x1}, {e.y1})\nサイズ  {e.Width} × {e.Height}マス";
                        break;
                    }
                default:
                    selected = default;
                    title = "選択なし"; body = "オブジェクトを選ぶには、S(選択)ツールでクリックしてください。";
                    break;
            }
            propTitle.text = title; propBody.text = body;
        }

        // The grid's y axis points down the screen, so 90° faces south.
        static string Compass(float deg)
        {
            string[] names = { "東", "南東", "南", "南西", "西", "北西", "北", "北東" };
            return names[Mathf.RoundToInt(deg / 45f) % 8];
        }

        void RefreshValidation()
        {
            for (int i = validationList.childCount - 1; i >= 0; i--)
            {
                var c = validationList.GetChild(i).gameObject;
                c.SetActive(false);
                Destroy(c);
            }
            var r = Validation;
            int errors = r.ErrorCount, warnings = r.WarningCount;
            validationSummary.text = errors > 0
                ? $"<color=#{ColorUtility.ToHtmlStringRGB(ErrText)}>必須エラー {errors}</color> · 警告 {warnings}\nテストプレイ不可"
                : $"<color=#{ColorUtility.ToHtmlStringRGB(OkText)}>必須条件クリア</color> · 警告 {warnings}\nテストプレイ可能";
            int shown = 0;
            foreach (var issue in r.issues)
            {
                if (shown++ >= MaxValidationRows) break;
                AddIssueRow(issue);
            }
            if (r.issues.Count > MaxValidationRows)
            {
                var more = NewText(validationList, $"… ほか{r.issues.Count - MaxValidationRows}件", 14, FontStyles.Italic, new Color(Ink.r, Ink.g, Ink.b, .6f)); Size(more, -1, 24);
            }
            if (r.issues.Count == 0)
            {
                var ok = NewText(validationList, "問題はありません。", 15, FontStyles.Normal, OkText); Size(ok, -1, 28);
            }
        }

        void AddIssueRow(ValidationIssue issue)
        {
            var bg = NewImage(issue.error ? "Error" : "Warning", validationList, issue.error ? ErrBg : WarnBg);
            var btn = bg.gameObject.AddComponent<Button>();
            btn.targetGraphic = bg;
            var cb = btn.colors; cb.normalColor = Color.white; cb.highlightedColor = new Color(.88f, .94f, 1f); cb.pressedColor = new Color(.75f, .85f, .95f); btn.colors = cb;
            VBox(bg.transform, 0, new RectOffset(10, 8, 6, 6));
            var t = NewText(bg.transform, (issue.error ? "[必須] " : "[警告] ") + issue.text, 14, FontStyles.Bold, issue.error ? ErrText : WarnText, TextAlignmentOptions.TopLeft);
            var focus = issue.focus; var sel = issue.select;
            btn.onClick.AddListener(() => FocusOn(focus, sel));
        }

        /// <summary>A validation row was clicked: the camera flies to the place and the object there (if any) is selected.</summary>
        public void FocusOn(Vector2 cell, ObjRef select)
        {
            if (BattlefieldCamera.Instance != null) BattlefieldCamera.Instance.PanTo(cell.x, cell.y, 380f);
            SelectObject(select);
        }

        void RefreshStats()
        {
            var r = Validation;
            statsBody.text = $"通常の壁 {r.solidCells}マス\n破壊可能な壁 {r.breakableCells}マス (パネル {r.breakablePanels}枚)\n強化壁 {r.reinforcedCells}マス (パネル {r.reinforcedPanels}枚)";
        }

        static string ToolName(EditTool t)
        {
            switch (t)
            {
                case EditTool.Erase: return "消去";
                case EditTool.OutFloor: return "屋外の床";
                case EditTool.InFloor: return "屋内の床";
                case EditTool.SolidWall: return "通常の壁";
                case EditTool.BreakableWall: return "破壊可能な壁";
                case EditTool.ReinforcedWall: return "強化壁";
                case EditTool.Hostage: return "人質";
                case EditTool.Defender: return "防衛開始位置";
                case EditTool.Camera: return "監視カメラ";
                case EditTool.Exfil: return "救出地点";
                case EditTool.Select: return "選択";
                default: return "木箱";
            }
        }

        // ================================================================================================================
        //  UI construction
        // ================================================================================================================

        internal static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        internal static void Stretch(RectTransform rt, float l = 0, float b = 0, float r = 0, float t = 0)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(l, b); rt.offsetMax = new Vector2(-r, -t);
        }

        internal static Image NewImage(string name, Transform parent, Color c, bool raycast = true)
        {
            var rt = NewRect(name, parent);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = c; img.raycastTarget = raycast;
            return img;
        }

        internal static TextMeshProUGUI NewText(Transform parent, string text, float size, FontStyles style, Color color, TextAlignmentOptions align = TextAlignmentOptions.MidlineLeft, UITheme.TextRole? role = null)
        {
            var rt = NewRect("Text", parent);
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            t.text = text; t.fontSize = size; t.color = color; t.alignment = align;
            if (role.HasValue) UIFonts.Apply(t, role.Value); else UIFonts.Auto(t, (style & FontStyles.Bold) != 0);
            t.raycastTarget = false;
            return t;
        }

        internal static LayoutElement Size(Component c, float w = -1, float h = -1, float flexW = -1)
        {
            var le = c.gameObject.GetComponent<LayoutElement>() ?? c.gameObject.AddComponent<LayoutElement>();
            if (w >= 0) le.preferredWidth = w;
            if (h >= 0) le.preferredHeight = h;
            if (flexW >= 0) le.flexibleWidth = flexW;
            return le;
        }

        internal static HorizontalLayoutGroup HBox(Transform t, float spacing, RectOffset pad, TextAnchor align = TextAnchor.MiddleLeft)
        {
            var h = t.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.spacing = spacing; h.padding = pad; h.childAlignment = align;
            h.childControlWidth = h.childControlHeight = true; h.childForceExpandWidth = h.childForceExpandHeight = false;
            return h;
        }

        internal static VerticalLayoutGroup VBox(Transform t, float spacing, RectOffset pad, TextAnchor align = TextAnchor.UpperLeft, bool expandWidth = true)
        {
            var v = t.gameObject.AddComponent<VerticalLayoutGroup>();
            v.spacing = spacing; v.padding = pad; v.childAlignment = align;
            v.childControlWidth = v.childControlHeight = true; v.childForceExpandWidth = expandWidth; v.childForceExpandHeight = false;
            return v;
        }

        internal static Button NewButton(Transform parent, string label, float w, float h, Color bg, Color fg, float fontSize, Action onClick)
        {
            var img = NewImage("Button_" + label, parent, bg);
            var btn = img.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;
            var cb = btn.colors;
            cb.normalColor = Color.white; cb.highlightedColor = new Color(.82f, .9f, 1f); cb.pressedColor = new Color(.65f, .75f, .9f);
            cb.disabledColor = new Color(.55f, .55f, .55f, .55f);
            btn.colors = cb;
            var t = NewText(img.transform, label, fontSize, FontStyles.Bold, fg, TextAlignmentOptions.Center);
            Stretch(t.rectTransform);
            Size(img, w, h);
            if (onClick != null) btn.onClick.AddListener(() => onClick());
            return btn;
        }

        static TMP_InputField NewInput(Transform parent, string text, float w, float h, TMP_InputField.ContentType type, int maxChars, Color? background = null)
        {
            var bg = NewImage("Input", parent, background ?? Paper);
            var area = NewRect("Text Area", bg.transform);
            Stretch(area, 10, 4, 10, 4);
            area.gameObject.AddComponent<RectMask2D>();
            var ph = NewText(area, "", 18, FontStyles.Italic, new Color(Ink.r, Ink.g, Ink.b, .4f), role: UITheme.TextRole.Body);
            Stretch(ph.rectTransform);
            var txt = NewText(area, text, 20, FontStyles.Bold, Ink);
            Stretch(txt.rectTransform);
            var f = bg.gameObject.AddComponent<TMP_InputField>();
            f.targetGraphic = bg; f.textViewport = area; f.textComponent = txt; f.placeholder = ph;
            f.contentType = type; f.characterLimit = maxChars; f.text = text;
            Size(bg, w, h);
            return f;
        }

        void BuildUI()
        {
            var cgo = new GameObject("MapEditorCanvas");
            canvas = cgo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 30;
            UITheme.Current.ApplyTo(cgo.AddComponent<CanvasScaler>(), UITheme.CanvasKind.Menu);
            uiRaycaster = cgo.AddComponent<GraphicRaycaster>();

            BuildEditorUI(cgo.transform);
            BuildDialog(cgo.transform);
            editorRoot.gameObject.SetActive(false);
        }

        // ---- the editor screen ----------------------------------------------------------------------------------------------

        void BuildEditorUI(Transform canvasT)
        {
            editorRoot = NewRect("Editor", canvasT);
            Stretch(editorRoot);

            // top bar
            var top = NewImage("TopBar", editorRoot, Ink);
            top.rectTransform.anchorMin = new Vector2(0, 1); top.rectTransform.anchorMax = new Vector2(1, 1);
            top.rectTransform.pivot = new Vector2(.5f, 1); top.rectTransform.sizeDelta = new Vector2(0, 56);
            HBox(top.transform, 10, new RectOffset(12, 12, 8, 8));
            NewButton(top.transform, "← 戻る", 130, 40, InkLight, Paper, 18, RequestExit);
            var tag = NewText(top.transform, "MAP EDITOR", 14, FontStyles.Bold, Blue); Size(tag, 110, 40);
            var nameLabel = NewText(top.transform, "マップ名", 16, FontStyles.Normal, new Color(1, 1, 1, .6f)); Size(nameLabel, 60, 40);
            nameInput = NewInput(top.transform, "新規マップ", 230, 40, TMP_InputField.ContentType.Standard, 30);
            nameInput.onEndEdit.AddListener(s => { if (Model != null) Model.Data.name = s; });
            sizeLabel = NewText(top.transform, "26 × 20", 20, FontStyles.Bold, Paper, TextAlignmentOptions.Center); Size(sizeLabel, 90, 40);
            stateLabel = NewText(top.transform, "", 15, FontStyles.Bold, Color.white, TextAlignmentOptions.Center, UITheme.TextRole.Title); Size(stateLabel, 130, 40);
            var viewTag = NewText(top.transform, "OVERLAY", 12, FontStyles.Bold, Blue, TextAlignmentOptions.Center); viewTag.textWrappingMode = TextWrappingModes.NoWrap; Size(viewTag, 80, 40);
            BuildToggle(top.transform, "降下区域", "drop", () => View != null && View.ShowDrop, () => { if (View != null) View.ShowDrop = !View.ShowDrop; });
            BuildToggle(top.transform, "カメラ視界", "cam", () => View != null && View.ShowCameraFov, () => { if (View != null) View.ShowCameraFov = !View.ShowCameraFov; });
            BuildToggle(top.transform, "グリッド", "grid", () => View != null && View.GridVisible, () => { if (View != null) View.GridVisible = !View.GridVisible; });
            var spacer = NewRect("Spacer", top.transform); Size(spacer, -1, 40, 1);
            NewButton(top.transform, "新規", 76, 40, InkLight, Paper, 17, () => { if (!ModalDialog.Any) GuardUnsaved(ReturnToDialog); });
            saveButton = NewButton(top.transform, "保存", 76, 40, InkLight, Paper, 17, () => RequestSave());
            NewButton(top.transform, "読み込み", 110, 40, InkLight, Paper, 17, OpenBrowser);
            testAttackButton = NewButton(top.transform, "攻撃でテスト", 166, 40, Blue, Color.white, 16, () => StartTest(OperatorTeam.Attack));
            testDefenseButton = NewButton(top.transform, "防衛でテスト", 150, 40, Foe, Color.white, 16, () => StartTest(OperatorTeam.Defense));
            AttachTip(testAttackButton.gameObject); AttachTip(testDefenseButton.gameObject);

            // left palette
            var left = NewImage("Palette", editorRoot, Paper);
            left.rectTransform.anchorMin = new Vector2(0, 0); left.rectTransform.anchorMax = new Vector2(0, 1);
            left.rectTransform.pivot = new Vector2(0, .5f);
            left.rectTransform.offsetMin = new Vector2(0, 44); left.rectTransform.offsetMax = new Vector2(216, -56);
            VBox(left.transform, 6, new RectOffset(10, 10, 12, 10));
            var head = NewText(left.transform, "TOOLS // ツール", 15, FontStyles.Bold, Blue); Size(head, -1, 24);
            foreach (var t in new[] { EditTool.OutFloor, EditTool.InFloor, EditTool.SolidWall, EditTool.BreakableWall, EditTool.ReinforcedWall, EditTool.Crate, EditTool.Erase })
                BuildPaletteRow(left.transform, t);
            var head2 = NewText(left.transform, "OBJECTS // オブジェクト", 15, FontStyles.Bold, Blue); Size(head2, -1, 30);
            foreach (var t in new[] { EditTool.Hostage, EditTool.Defender, EditTool.Camera, EditTool.Exfil, EditTool.Select })
                BuildPaletteRow(left.transform, t);

            // right panels: properties (top), validation (middle), wall statistics (bottom)
            var right = NewImage("Side", editorRoot, Paper);
            right.rectTransform.anchorMin = new Vector2(1, 0); right.rectTransform.anchorMax = new Vector2(1, 1);
            right.rectTransform.pivot = new Vector2(1, .5f);
            right.rectTransform.offsetMin = new Vector2(-330, 44); right.rectTransform.offsetMax = new Vector2(0, -56);

            var props = NewImage("Properties", right.transform, Paper, false);
            props.rectTransform.anchorMin = new Vector2(0, 1); props.rectTransform.anchorMax = new Vector2(1, 1);
            props.rectTransform.pivot = new Vector2(.5f, 1); props.rectTransform.sizeDelta = new Vector2(0, 172);
            VBox(props.transform, 6, new RectOffset(14, 14, 12, 8));
            NewText(props.transform, "PROPERTIES // プロパティ", 15, FontStyles.Bold, Blue).gameObject.AddComponent<LayoutElement>().preferredHeight = 24;
            propTitle = NewText(props.transform, "", 21, FontStyles.Bold, Ink, role: UITheme.TextRole.Title); Size(propTitle, -1, 30);
            propBody = NewText(props.transform, "", 16, FontStyles.Normal, new Color(Ink.r, Ink.g, Ink.b, .85f), TextAlignmentOptions.TopLeft, UITheme.TextRole.Body); Size(propBody, -1, 84);

            var stats = NewImage("Stats", right.transform, new Color32(0xdd, 0xe1, 0xe6, 0xff), false);
            stats.rectTransform.anchorMin = new Vector2(0, 0); stats.rectTransform.anchorMax = new Vector2(1, 0);
            stats.rectTransform.pivot = new Vector2(.5f, 0); stats.rectTransform.sizeDelta = new Vector2(0, 124);
            VBox(stats.transform, 6, new RectOffset(14, 14, 10, 8));
            NewText(stats.transform, "WALLS // 統計", 15, FontStyles.Bold, Blue).gameObject.AddComponent<LayoutElement>().preferredHeight = 24;
            statsBody = NewText(stats.transform, "", 16, FontStyles.Bold, Ink, TextAlignmentOptions.TopLeft, UITheme.TextRole.Title); Size(statsBody, -1, 76);

            var val = NewImage("Validation", right.transform, Paper, false);
            val.rectTransform.anchorMin = Vector2.zero; val.rectTransform.anchorMax = Vector2.one;
            val.rectTransform.offsetMin = new Vector2(0, 124); val.rectTransform.offsetMax = new Vector2(0, -172);
            VBox(val.transform, 6, new RectOffset(14, 14, 10, 6));
            var sepTop = NewImage("Sep", val.transform, PaperDark, false); Size(sepTop, -1, 2);
            NewText(val.transform, "VALIDATION // 検証", 15, FontStyles.Bold, Blue).gameObject.AddComponent<LayoutElement>().preferredHeight = 24;
            validationSummary = NewText(val.transform, "", 16, FontStyles.Bold, Ink, TextAlignmentOptions.TopLeft, UITheme.TextRole.Title); Size(validationSummary, -1, 48);
            validationList = NewRect("Rows", val.transform);
            VBox(validationList, 6, new RectOffset(0, 0, 0, 0));

            // bottom bar
            var bottom = NewImage("BottomBar", editorRoot, Ink);
            bottom.rectTransform.anchorMin = new Vector2(0, 0); bottom.rectTransform.anchorMax = new Vector2(1, 0);
            bottom.rectTransform.pivot = new Vector2(.5f, 0); bottom.rectTransform.sizeDelta = new Vector2(0, 44);
            HBox(bottom.transform, 16, new RectOffset(16, 16, 6, 6));
            hintLabel = NewText(bottom.transform, TerrainHint, 12, FontStyles.Normal, HintColor);   // 12: the longest hint (TerrainHint) fits beside the 420px coordinates
            hintLabel.overflowMode = TextOverflowModes.Ellipsis;   // one line; no Auto Size (the size stays the theme's)
            Size(hintLabel, -1, 32, 1);
            coordLabel = NewText(bottom.transform, "", 17, FontStyles.Bold, Paper, TextAlignmentOptions.MidlineRight, UITheme.TextRole.Display); Size(coordLabel, 420, 32);

            // the reason a disabled test button is disabled, shown under the top bar while the cursor is on it
            var tip = NewImage("TestTip", editorRoot, new Color32(0x14, 0x16, 0x1a, 0xf2), false);
            tipRoot = tip.rectTransform;
            tipRoot.anchorMin = tipRoot.anchorMax = tipRoot.pivot = new Vector2(1, 1);
            tipRoot.anchoredPosition = new Vector2(-14, -62);
            VBox(tip.transform, 0, new RectOffset(16, 16, 12, 12));
            var tipFit = tip.gameObject.AddComponent<ContentSizeFitter>();
            tipFit.verticalFit = ContentSizeFitter.FitMode.PreferredSize; tipFit.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            tipText = NewText(tip.transform, "", 16, FontStyles.Normal, Color.white, TextAlignmentOptions.TopLeft, UITheme.TextRole.Body);
            tipText.textWrappingMode = TextWrappingModes.Normal;
            Size(tipText, 470, -1);
            tipRoot.gameObject.SetActive(false);
        }

        // Shows TestBlockReason() while the cursor rests on `go` (only when the test is blocked).
        void AttachTip(GameObject go)
        {
            var h = go.AddComponent<HoverTip>();
            h.onEnter = () => { var r = TestBlockReason(); if (r == null) return; tipText.text = r; tipRoot.gameObject.SetActive(true); };
            h.onExit = () => tipRoot.gameObject.SetActive(false);
        }

        sealed class HoverTip : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
        {
            public Action onEnter, onExit;
            public void OnPointerEnter(PointerEventData e) => onEnter?.Invoke();
            public void OnPointerExit(PointerEventData e) => onExit?.Invoke();
            void OnDisable() => onExit?.Invoke();
        }

        static string HotKey(EditTool t)
        {
            if (t == EditTool.Exfil) return "-";
            if (t == EditTool.Select) return "S";
            return ((int)t).ToString();
        }

        // A top-bar icon button that switches one overlay on and off; lit (blue) while it is on.
        void BuildToggle(Transform parent, string label, string icon, Func<bool> isOn, Action toggle)
        {
            var img = NewImage("Toggle_" + icon, parent, InkLight);
            var btn = img.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;
            var cb = btn.colors; cb.normalColor = Color.white; cb.highlightedColor = new Color(.85f, .92f, 1f); cb.pressedColor = new Color(.7f, .8f, .95f); btn.colors = cb;
            HBox(img.transform, 7, new RectOffset(6, 12, 4, 4));
            Size(img, -1, 40);
            var frame = NewImage("Icon", img.transform, new Color(.08f, .09f, .11f), false); Size(frame, 28, 28);
            Image Fill(string n, Color c, float l, float b, float r, float t)
            {
                var im = NewImage(n, frame.transform, c, false);
                Stretch(im.rectTransform, l, b, r, t);
                return im;
            }
            var sky = new Color(.35f, .78f, 1f); var pink = MapCellView.CameraColor; var line = new Color(1f, 1f, 1f, .8f);
            switch (icon)
            {
                case "drop":
                    Fill("a", sky, 3, 15, 15, 3); Fill("b", sky, 15, 15, 3, 3); Fill("c", sky, 3, 3, 15, 15); Fill("d", sky, 15, 3, 3, 15);
                    break;
                case "cam":    // a wedge of sight opening up from a camera dot
                    Fill("s1", pink, 3, 3, 3, 20); Fill("s2", pink, 7, 8, 7, 15); Fill("s3", pink, 11, 13, 11, 10); Fill("dot", Color.white, 12, 19, 12, 4);
                    break;
                default:       // grid
                    Fill("h1", line, 3, 9, 3, 18); Fill("h2", line, 3, 18, 3, 9); Fill("v1", line, 9, 3, 18, 3); Fill("v2", line, 18, 3, 9, 3);
                    break;
            }
            var t = NewText(img.transform, label, 15, FontStyles.Bold, Color.white); Size(t, -1, 32);
            btn.onClick.AddListener(() => toggle());
            toggles.Add((img, isOn));
        }

        void BuildPaletteRow(Transform parent, EditTool tool)
        {
            var row = NewImage("Tool_" + tool, parent, Paper);
            var btn = row.gameObject.AddComponent<Button>();
            btn.targetGraphic = row;
            var cb = btn.colors; cb.normalColor = Color.white; cb.highlightedColor = new Color(.86f, .92f, 1f); cb.pressedColor = new Color(.7f, .8f, .95f); btn.colors = cb;
            HBox(row.transform, 10, new RectOffset(8, 8, 6, 6));
            Size(row, -1, 52);
            BuildIcon(row.transform, tool);
            var name = NewText(row.transform, ToolName(tool), 15, FontStyles.Bold, Ink); Size(name, -1, 40, 1);   // 15: "破壊可能な壁" fits between the icon and the key
            name.textWrappingMode = TextWrappingModes.NoWrap;   // "破壊可能な壁" must stay on one line
            var keyBg = NewImage("Key", row.transform, Ink, false); Size(keyBg, 28, 28);
            var key = NewText(keyBg.transform, HotKey(tool), 17, FontStyles.Bold, Paper, TextAlignmentOptions.Center);
            Stretch(key.rectTransform);
            palette[tool] = (row, name, key);
            btn.onClick.AddListener(() => { SelectTool(tool); UpdateBottom(); });
        }

        // A small picture of each tool: the same colours the map uses, so the palette reads like the drawing.
        static void BuildIcon(Transform parent, EditTool tool)
        {
            var frame = NewImage("Icon", parent, Ink, false); Size(frame, 36, 36);
            Image Fill(string n, Color c, float l, float b, float r, float t)
            {
                var im = NewImage(n, frame.transform, c, false);
                Stretch(im.rectTransform, l, b, r, t);
                return im;
            }
            switch (tool)
            {
                case EditTool.OutFloor: Fill("f", new Color(.22f, .3f, .22f), 2, 2, 2, 2); break;
                case EditTool.InFloor: Fill("f", new Color(.62f, .66f, .74f), 2, 2, 2, 2); Fill("g", new Color(.5f, .54f, .62f), 17, 2, 2, 17); Fill("g2", new Color(.5f, .54f, .62f), 2, 17, 17, 2); break;
                case EditTool.SolidWall: Fill("f", new Color(.29f, .32f, .37f), 2, 2, 2, 2); Fill("t", new Color(.45f, .48f, .54f), 2, 22, 2, 2); break;
                case EditTool.BreakableWall: Fill("f", new Color(.62f, .43f, .24f), 2, 2, 2, 2); Fill("l1", new Color(.4f, .26f, .13f), 2, 11, 2, 23); Fill("l2", new Color(.4f, .26f, .13f), 2, 23, 2, 11); break;
                case EditTool.ReinforcedWall: Fill("f", ReinforcedWallViews.Steel, 2, 2, 2, 2); Fill("fr", ReinforcedWallViews.Frame, 2, 30, 2, 2); Fill("rl", ReinforcedWallViews.Rail, 2, 2, 2, 30); break;
                case EditTool.Crate: Fill("f", new Color(.22f, .3f, .22f), 2, 2, 2, 2); Fill("c", new Color(.55f, .52f, .34f), 8, 8, 8, 8); Fill("b", new Color(.72f, .6f, .16f), 8, 16, 8, 16); break;
                case EditTool.Hostage: Fill("f", new Color(.62f, .66f, .74f), 2, 2, 2, 2); Fill("b", MapCellView.HostageColor, 7, 7, 7, 7); Letter("H"); break;
                case EditTool.Defender: Fill("f", new Color(.62f, .66f, .74f), 2, 2, 2, 2); Fill("b", MapCellView.DefenderColor, 7, 7, 7, 7); Letter("1"); break;
                case EditTool.Camera: Fill("f", new Color(.62f, .66f, .74f), 2, 2, 2, 2); Fill("b", MapCellView.CameraColor, 7, 7, 7, 7); Letter("C"); break;
                case EditTool.Exfil: Fill("f", new Color(.9f, .7f, .1f), 2, 2, 2, 2); Fill("g", new Color(.22f, .3f, .22f), 5, 5, 5, 5); Letter("A", new Color32(0xff, 0xd2, 0x1f, 0xff)); break;
                case EditTool.Select: Fill("f", new Color(.16f, .18f, .21f), 2, 2, 2, 2); Fill("r", new Color(1f, 1f, 1f, .9f), 6, 6, 6, 6); Fill("r2", new Color(.16f, .18f, .21f), 8, 8, 8, 8); Letter("S", Color.white); break;
                default:
                    Fill("f", new Color(.16f, .18f, .21f), 2, 2, 2, 2);
                    var x = NewText(frame.transform, "X", 24, FontStyles.Bold, Foe, TextAlignmentOptions.Center); Stretch(x.rectTransform);
                    break;
            }
            void Letter(string s, Color? c = null) { var tx = NewText(frame.transform, s, 17, FontStyles.Bold, c ?? Color.white, TextAlignmentOptions.Center); Stretch(tx.rectTransform); }
        }

        // ---- the "new map" window ------------------------------------------------------------------------------------------

        RectTransform Modal(string name, Transform parent, float w, float h, out RectTransform content)
        {
            var root = NewRect(name, parent);
            Stretch(root);
            var dim = NewImage("Dim", root, new Color(Ink.r, Ink.g, Ink.b, .72f));
            Stretch(dim.rectTransform);
            var outer = NewImage("Window", root, Ink);
            outer.rectTransform.sizeDelta = new Vector2(w, h);
            var inner = NewImage("Paper", outer.transform, Paper);
            Stretch(inner.rectTransform, 3, 3, 3, 3);
            content = inner.rectTransform;
            return root;
        }

        void BuildDialog(Transform canvasT)
        {
            dialogRoot = Modal("NewMapDialog", canvasT, 560, 548, out var content);
            VBox(content, 12, new RectOffset(30, 30, 26, 24));
            var title = NewText(content, "マップエディタ", 34, FontStyles.Bold, Ink); Size(title, -1, 46);
            var sub = NewText(content, "MAP EDITOR // 新規マップ、基本邸宅の複製、保存済みマップのいずれかから開始します。", 15, FontStyles.Normal, new Color(Ink.r, Ink.g, Ink.b, .65f)); Size(sub, -1, 24);
            var sect = NewText(content, "新規マップ · サイズ (16〜40)", 17, FontStyles.Bold, Blue); Size(sect, -1, 28);

            var row = NewRect("SizeRow", content); Size(row, -1, 46);
            HBox(row, 10, new RectOffset(0, 0, 0, 0));
            var wl = NewText(row, "横", 19, FontStyles.Bold, Ink); Size(wl, 48, 44);
            widthInput = NewInput(row, DefaultWidth.ToString(), 96, 44, TMP_InputField.ContentType.IntegerNumber, 2, PaperDark);
            var hl = NewText(row, "縦", 19, FontStyles.Bold, Ink); Size(hl, 48, 44);
            heightInput = NewInput(row, DefaultHeight.ToString(), 96, 44, TMP_InputField.ContentType.IntegerNumber, 2, PaperDark);
            var spacer = NewRect("Spacer", row); Size(spacer, -1, 44, 1);
            foreach (var f in new[] { widthInput, heightInput }) f.onEndEdit.AddListener(_ => ClampSizeFields());

            NewButton(content, "新規マップを作成", -1, 56, Ink, Paper, 22, () => { ClampSizeFields(); ChooseNew(ParseSize(widthInput, DefaultWidth), ParseSize(heightInput, DefaultHeight)); });
            var sep = NewImage("Sep", content, PaperDark, false); Size(sep, -1, 2);
            NewButton(content, "基本邸宅を複製", -1, 56, Blue, Color.white, 22, ChooseClone);
            NewButton(content, "保存済みマップを読み込む", -1, 56, InkLight, Paper, 22, OpenBrowser);
            NewButton(content, "キャンセル", -1, 44, PaperDark, Ink, 18, Close);
        }

        static int ParseSize(TMP_InputField f, int fallback) => int.TryParse(f.text, out int v) ? Mathf.Clamp(v, MinSize, MaxSize) : fallback;

        void ClampSizeFields()
        {
            widthInput.text = ParseSize(widthInput, DefaultWidth).ToString();
            heightInput.text = ParseSize(heightInput, DefaultHeight).ToString();
        }

        void ShowDialog()
        {
            dialogRoot.gameObject.SetActive(true);
            State = Phase.Dialog;
        }
    }
}
