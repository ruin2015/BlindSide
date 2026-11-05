// The game menu (ESC during a match: battle, tutorial, the editor's test play), on its own DontDestroyOnLoad Screen Space Overlay canvas
// (menu reference 1440×810, sorting order 31000: over the HUD, under the save notice / ScreenFX / wipe), installed after the first scene.
//   DecoDim     rgba(3,5,8,.72) over the whole screen (takes every click)
//   Head        "PAUSE" 12 display (blue, spaced) over 「メニュー」 34 Title (white), a 240 logo bar under them, centred at y 190
//   Entries     text only (KitTextButton, Title 24, grows ×1.15 under the pointer), centred, 42 high, 6 apart, from y 300:
//                 再開          closes the menu
//                 保存          writes the progress save now (no session: dim) — the match in progress is not part of it
//                 読み込み       asks, then reads the save and goes back to the title (the select screen on the save's side / map)
//                 設定          the settings panel (KitOptionsPanel)
//                 タイトルへ     asks, then drops the match and goes back to the title  (test play: エディタへ — back to the editor)
//                 ゲーム終了     asks, then quits
//   Hint        "ESC  再開" 11 display / body, dim, under the entries
// It opens only while the game waits for the player (their turn or deployment, nothing moving, no dialog): an ESC pressed during the
// enemy's turn or a cinematic opens it as soon as that is over. ESC goes to whoever uses it first (EscapeGate: a selection to cancel,
// a dialog, the settings panel); this menu takes only a press nobody used. While it is open the game's time is stopped (timeScale 0)
// and the battlefield takes no input (BattlefieldCamera.InputLocked); both are given back as they were.
// Motion (ITimedEffect.Evaluate): the dim fades in .2s; the entries come in .32s each, 40ms apart (fade + 24 from the right).
// No allocation per frame.
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Blindside.Integration;
using Blindside.Rendering;
using Blindside.Save;

namespace Blindside.UI
{
    [DefaultExecutionOrder(1000)]          // after every ESC taker (the battlefield, the dialogs, the settings panel)
    [AddComponentMenu("Blindside/UI/Pause Menu")]
    public class PauseMenu : MonoBehaviour, ITimedEffect
    {
        public const float ScreenW = 1440f, HeadY = 190f, EntriesY = 300f, EntryFont = 24f, EntryH = 42f, EntryGap = 6f;
        public const float DimTime = .2f, EntryTime = .32f, EntryStep = .04f, EntryShift = 24f;
        static readonly Color DimColor = new Color(3f / 255f, 5f / 255f, 8f / 255f, .72f);

        public static PauseMenu Instance { get; private set; }
        public static bool IsOpen => Instance != null && Instance.root != null && Instance.root.gameObject.activeSelf;

        public RectTransform root;
        public Image dim;
        public KitTextButton resume, save, load, settings, title, quit;
        public KitOptionsPanel options;
        public DialogView dialog;
        readonly RectTransform[] rows = new RectTransform[6];
        readonly CanvasGroup[] rowGroups = new CanvasGroup[6];
        readonly Vector2[] homes = new Vector2[6];
        TMP_Text titleLabel;

        float clock; bool running, pending;
        float savedTimeScale = 1f; bool savedInputLock;
        System.Action pendingYes;

        public float Duration => Mathf.Max(DimTime, EntryTime + EntryStep * 5f);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            if (Instance != null) return;
            var go = new GameObject("BlindsidePauseMenu", typeof(RectTransform));
            DontDestroyOnLoad(go);
            var c = go.AddComponent<Canvas>();
            c.renderMode = RenderMode.ScreenSpaceOverlay;
            c.sortingOrder = 31000;
            go.AddComponent<CanvasScaler>();
            go.AddComponent<UIThemeCanvas>().kind = UITheme.CanvasKind.Menu;
            go.AddComponent<GraphicRaycaster>();
            Instance = BuildInto(go.transform);
        }

        /// <summary>Builds the (closed) menu under `parent` (a 1440×810 menu canvas); previews open it with PoseOpen.</summary>
        public static PauseMenu BuildInto(Transform parent)
        {
            var theme = UITheme.Current;
            var host = HudKit.NewRect("PauseMenu", parent);
            HudKit.Stretch(host);
            var m = host.gameObject.AddComponent<PauseMenu>();

            m.root = HudKit.NewRect("Root", host);
            HudKit.Stretch(m.root);
            var dimRt = HudKit.NewRect("DecoDim", m.root);
            HudKit.Stretch(dimRt);
            m.dim = HudKit.AddImage(dimRt, DimColor, raycast: true);

            // head
            var en = HudKit.AddText(m.root, "PAUSE", 12f, theme.blue, true, TextAlignmentOptions.Center, "En", UITheme.TextRole.Display);
            en.characterSpacing = 14f;
            UILayout.Place(en.rectTransform, 0f, HeadY, ScreenW, 16f);
            var head = HudKit.AddText(m.root, "メニュー", 34f, Color.white, true, TextAlignmentOptions.Center, "Title", UITheme.TextRole.Title);
            UILayout.Place(head.rectTransform, 0f, HeadY + 18f, ScreenW, 46f);
            var bar = HudKit.NewRect("DecoBar", m.root);
            bar.anchorMin = bar.anchorMax = new Vector2(.5f, 1f); bar.pivot = new Vector2(.5f, 1f);
            bar.anchoredPosition = new Vector2(0f, -(HeadY + 72f)); bar.sizeDelta = new Vector2(240f, 6f);
            HudKit.AddImage(bar, Color.white);
            bar.gameObject.AddComponent<SlantedGraphic>().Set(SlantedGraphic.Size.Custom, SlantedGraphic.Direction.RightEdge, 8f);
            bar.gameObject.AddComponent<Mask>().showMaskGraphic = false;
            BarPart(bar, 0f, .3f, theme.blue);
            BarPart(bar, .32f, 1f, new Color32(0xC2, 0xC2, 0xC3, 0xff));

            // entries
            m.resume = m.Entry(0, "Resume", "再開", m.Close);
            m.save = m.Entry(1, "Save", "保存", m.SaveNow);
            m.load = m.Entry(2, "Load", "読み込み", m.AskLoad);
            m.settings = m.Entry(3, "Settings", "設定", m.OpenSettings);
            m.title = m.Entry(4, "Title", "タイトルへ", m.AskTitle);
            m.titleLabel = m.title.label;
            m.quit = m.Entry(5, "Quit", "ゲーム終了", m.AskQuit);

            // hint
            var hint = HudKit.NewRect("Hint", m.root);
            UILayout.Place(hint, 0f, EntriesY + 6f * (EntryH + EntryGap) + 18f, ScreenW, 18f);
            var esc = HudKit.AddText(hint, "ESC", 11f, theme.dim, true, TextAlignmentOptions.MidlineRight, "Key", UITheme.TextRole.Display);
            esc.characterSpacing = 10f;
            UILayout.Place(esc.rectTransform, 0f, 0f, ScreenW * .5f - 4f, 18f);
            var ht = HudKit.AddText(hint, "再開", 12f, theme.dim, false, TextAlignmentOptions.MidlineLeft, "Text", UITheme.TextRole.Body);
            UILayout.Place(ht.rectTransform, ScreenW * .5f + 4f, 0f, 200f, 18f);

            m.options = KitOptionsPanel.Build(m.root);
            m.dialog = DialogView.Build(m.root, m.OnDialogYes, m.CloseDialog);

            m.root.gameObject.SetActive(false);
            return m;
        }

        static void BarPart(RectTransform bar, float from, float to, Color c)
        {
            var rt = HudKit.NewRect("Part", bar);
            rt.anchorMin = new Vector2(from, 0f); rt.anchorMax = new Vector2(to, 1f);
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            HudKit.AddImage(rt, c);
        }

        KitTextButton Entry(int i, string name, string label, UnityEngine.Events.UnityAction onClick)
        {
            var row = HudKit.NewRect(name + "Row", root);
            UILayout.Place(row, 0f, EntriesY + i * (EntryH + EntryGap), ScreenW, EntryH);
            homes[i] = row.anchoredPosition;
            rowGroups[i] = row.gameObject.AddComponent<CanvasGroup>();
            rows[i] = row;
            var b = KitTextButton.Create(row, name, label, EntryFont, EntryH, .5f, onClick);
            b.hoverScale = 1.15f;
            var rt = (RectTransform)b.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(.5f, .5f);
            rt.anchoredPosition = Vector2.zero;
            return b;
        }

        // ---- the match it belongs to -------------------------------------------------------------------------------------------
        static MatchBootstrap cachedMatch;

        static MatchBootstrap Match()
        {
            if (!PlaySession.Active) return null;
            if (cachedMatch == null) cachedMatch = FindFirstObjectByType<MatchBootstrap>();   // (once per scene: a destroyed one reads null)
            return cachedMatch;
        }

        /// <summary>The game waits for the player: the menu may open now.</summary>
        static bool CanOpenNow(MatchBootstrap mb)
        {
            if (mb == null || mb.selection == null || !mb.selection.WaitingForPlayer) return false;
            if (BattlefieldCamera.InputLocked) return false;                     // a cinematic / the result
            if (Blindside.MapEditor.ModalDialog.Any) return false;
            return true;
        }

        // ---- open / close ------------------------------------------------------------------------------------------------------
        public void Open()
        {
            var mb = Match();
            bool test = PlaySession.IsTest;
            titleLabel.SetText(test ? "エディタへ" : "タイトルへ");
            save.SetInteractable(SaveSystem.HasSession && !test);
            load.SetInteractable(SaveSystem.Exists && !test);
            savedTimeScale = Time.timeScale;
            savedInputLock = BattlefieldCamera.InputLocked;
            Time.timeScale = 0f;
            BattlefieldCamera.InputLocked = true;
            root.gameObject.SetActive(true);
            CloseDialog();
            if (options.IsOpen) options.Close();
            clock = 0f; running = Application.isPlaying;
            Evaluate(running ? 0f : Duration);
        }

        public void Close()
        {
            if (!root.gameObject.activeSelf) return;
            CloseDialog();
            if (options.IsOpen) options.Close();
            root.gameObject.SetActive(false);
            running = false;
            Time.timeScale = savedTimeScale;
            BattlefieldCamera.InputLocked = savedInputLock;
        }

        /// <summary>Closes without giving time / input back (the match is being dropped; it resets them itself).</summary>
        void CloseForLeave()
        {
            CloseDialog();
            if (options.IsOpen) options.Close();
            root.gameObject.SetActive(false);
            running = false;
            Time.timeScale = 1f;
        }

        /// <summary>Previews: the menu open and settled (`test`: an editor test play).</summary>
        public void PoseOpen(bool test, bool canSave, bool canLoad)
        {
            titleLabel.SetText(test ? "エディタへ" : "タイトルへ");
            save.SetInteractable(canSave);
            load.SetInteractable(canLoad);
            root.gameObject.SetActive(true);
            Evaluate(Duration);
        }

        void Update()
        {
            var kb = Keyboard.current;
            bool esc = kb != null && kb.escapeKey.wasPressedThisFrame && !EscapeGate.Consumed;
            if (IsOpen)
            {
                if (esc)
                {
                    EscapeGate.Consume();
                    if (dialog.Visible) CloseDialog(); else Close();
                }
                if (running)
                {
                    clock += Time.unscaledDeltaTime;
                    Evaluate(clock);
                    if (clock >= Duration) running = false;
                }
                if (IsOpen && Match() == null) CloseForLeave();         // the match went away under it
                return;
            }

            var mb = Match();
            if (mb == null) { pending = false; return; }
            if (esc) { EscapeGate.Consume(); pending = true; }
            if (pending && CanOpenNow(mb)) { pending = false; Open(); }
        }

        // ---- entries -----------------------------------------------------------------------------------------------------------
        void SaveNow()
        {
            if (!SaveSystem.HasSession) return;
            SaveSystem.Write(false);          // (the notice shows SAVE 保存しました)
        }

        void AskLoad()
        {
            Ask("読み込み", "LOAD", "現在の作戦を中断して、セーブデータを読み込みますか？\n作戦の途中経過は保存されません。", () =>
            {
                if (!SaveSystem.Load(out string why))
                {
                    Debug.LogWarning("[PauseMenu] the save could not be read: " + why);
                    Ask("読み込み", "LOAD", "セーブデータを読み込めませんでした。", null, "確認", "キャンセル");
                    return;
                }
                Leave(false);
            });
        }

        void OpenSettings() => options.Open();

        void AskTitle()
        {
            if (PlaySession.IsTest) Ask("エディタへ", "EDITOR", "テストプレイを終了して、エディタへ戻りますか？", () => Leave(true));
            else Ask("タイトルへ", "TITLE", "作戦を中断して、タイトルへ戻りますか？\n作戦の途中経過は保存されません。", () => Leave(false));
        }

        void AskQuit() => Ask("ゲーム終了", "EXIT", "ゲームを終了しますか？", MainMenuScreen.QuitNow);

        void Leave(bool toEditor)
        {
            var mb = Match();
            CloseForLeave();
            if (mb == null) return;
            if (toEditor) mb.AbandonToEditor(); else mb.AbandonToTitle();
        }

        void Ask(string head, string en, string body, System.Action yes, string yesLabel = "はい", string noLabel = "いいえ")
        {
            pendingYes = yes;
            dialog.Open(head, en, body, yesLabel, noLabel);
        }

        void CloseDialog()
        {
            pendingYes = null;
            if (dialog != null && dialog.Visible) dialog.Close();
        }

        void OnDialogYes()
        {
            var a = pendingYes;
            CloseDialog();
            a?.Invoke();
        }

        // ---- ITimedEffect ------------------------------------------------------------------------------------------------------
        public void Evaluate(float t)
        {
            bool reduce = UIMotionSettings.reduceMotion;
            var c = DimColor; c.a *= Mathf.Clamp01(t / DimTime);
            if (dim.color != c) dim.color = c;
            for (int i = 0; i < rows.Length; i++)
            {
                float p = t - i * EntryStep;
                float e = p <= 0f ? 0f : reduce ? 1f : UIEase.Tactical(Mathf.Clamp01(p / EntryTime));
                if (rowGroups[i].alpha != e) rowGroups[i].alpha = e;
                bool on = e >= 1f;
                if (rowGroups[i].blocksRaycasts != on) rowGroups[i].blocksRaycasts = on;
                var pos = homes[i] + new Vector2(reduce ? 0f : EntryShift * (1f - e), 0f);
                if (rows[i].anchoredPosition != pos) rows[i].anchoredPosition = pos;
            }
        }
    }
}
