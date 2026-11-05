// The title scene's main menu (in front of the side / map select screen, TitleSelectScreen), built at runtime on a full-screen object
// under the title canvas (1440×810):
//   DecoBg       the title background #0a0c0f (takes the clicks the select screen behind it must not get)
//   Background   the key art (`background`, Inspector) over the whole screen, cropped to fill (EnvelopeParent); the squad on the left,
//                the empty right half is where the menu sits
//   DecoDiag     the title's hazard stripes, 560×12 at the bottom right (acc .85, 14 / 28, scrolling 1.6s), left edge leaning 20
//   Column       the right side, x 820, 560 wide, centred vertically:
//     Logo       KitMenuLogo at 96: TAC-OS line, BLINDSIDE, the logo bar. The first time the menu shows in a run it plays its heavy
//                glitch intro (1.6s), then (and on every later show) the calm glitch loop
//     Entries    text only (KitTextButton, Title 26, #c7ced6 → white and ×1.18 under the pointer), centred in the column, 44 high,
//                14 apart, 48 under the logo:
//                  ニューゲーム   a fresh save (asks first when one exists: it is overwritten), then the tutorial
//                  読み込み       only when a save exists: it becomes the session's, the select screen opens on its last side and map
//                  ゲーム終了     asks, then quits (the editor leaves Play mode)
//                entrance: fade + 40 from the right, .5s each, 60ms apart, after the logo (1.2s after a heavy intro, .15s otherwise)
//   Dialog       DialogView (はい / いいえ) for the questions above and a save that cannot be read
// Screen changes play KitWipe (the title's wipe) and switch at its middle. The select screen comes back to this menu with ESC (when no
// window / dialog of its own is open). A title scene opened while a save session is on (back from a match) shows the select screen
// at once. Motion: ITimedEffect.Evaluate(t) on an unscaled clock; no allocation per frame.
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Blindside.Flow;
using Blindside.MapEditor;
using Blindside.Save;

namespace Blindside.UI
{
    [DefaultExecutionOrder(-900)]       // after SceneEntry (-1000); before the select screen's windows read ESC
    [AddComponentMenu("Blindside/UI/Main Menu Screen")]
    public class MainMenuScreen : MonoBehaviour, ITimedEffect
    {
        public const float ScreenW = 1440f, ScreenH = 810f, ColX = 820f, ColW = 560f, LogoSize = 96f;
        public const float EntryFont = 26f, EntryH = 44f, EntryGap = 14f, EntriesGap = 48f;
        public const float HeavyDelay = 1.2f, CalmDelay = .15f, EntryStep = .06f, EntryTime = .5f, EnterShift = 40f;
        const float Never = -100f;
        static readonly Color Bg = new Color32(0x0a, 0x0c, 0x0f, 0xff);
        /// <summary>Where the key art is expected (the scene builder and the previews take it from here).</summary>
        public const string BackgroundPath = "Assets/Blindside/UI/Title/TitleBackground.png";

        [Tooltip("The side / map select screen behind this menu (found in the scene when empty).")]
        public TitleSelectScreen title;
        [Tooltip("The key art behind the menu (cropped to fill the screen). Empty: the plain title background.")]
        public Texture background;
        [Tooltip("Screen changes play the title's wipe (KitWipe).")]
        public bool wipe = true;

        // built
        public RectTransform content;
        public RawImage backgroundImage;
        public KitMenuLogo logo;
        public KitTextButton newGameButton, loadButton, quitButton;
        public DialogView dialog;
        public HazardStripe diag;
        readonly RectTransform[] rows = new RectTransform[3];
        readonly Vector2[] homes = new Vector2[3];
        readonly CanvasGroup[] rowGroups = new CanvasGroup[3];
        int rowCount;

        float clock, enterStart = Never, entriesDelay = CalmDelay;
        bool busy;
        System.Action pendingYes;

        /// <summary>The heavy logo intro plays once per run (the first time the menu shows).</summary>
        static bool heavyPlayed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => heavyPlayed = false;

        /// <summary>The menu is showing (false while the select screen is).</summary>
        public bool MenuVisible => content != null && content.gameObject.activeSelf;

        public float Duration => entriesDelay + 2f * EntryStep + EntryTime;

        float ColumnHeight(int entries) => (logo != null ? logo.Height : 154f) + EntriesGap + entries * EntryH + (entries - 1) * EntryGap;
        float ColumnTop(int entries) => Mathf.Round((ScreenH - ColumnHeight(entries)) * .5f);

        // ---- lifecycle ---------------------------------------------------------------------------------------------------------
        void Awake()
        {
            Build(SaveSystem.Peek());
            if (!Application.isPlaying) return;
            if (title == null) title = FindFirstObjectByType<TitleSelectScreen>(FindObjectsInactive.Include);
            if (SaveSystem.HasSession && title != null) ShowSelect();
            else ShowMenu();
        }

        void Update()
        {
            clock += Time.unscaledDeltaTime;
            if (enterStart > Never && clock - enterStart > Duration) enterStart = Never;
            Evaluate(clock);
            HandleEscape();
        }

        void HandleEscape()
        {
            var kb = Keyboard.current;
            if (kb == null || !kb.escapeKey.wasPressedThisFrame || busy || EscapeGate.Consumed) return;
            if (dialog != null && dialog.Visible) { EscapeGate.Consume(); CloseDialog(); return; }
            if (MenuVisible || title == null || !title.gameObject.activeInHierarchy) return;
            // the select screen's own windows take ESC first (they close themselves)
            if (ModalDialog.Any || (MapBrowserWindow.Instance != null && MapBrowserWindow.Instance.gameObject.activeInHierarchy)) return;
            var options = title.GetComponentInChildren<KitOptionsPanel>(false);
            if (options != null && options.IsOpen) return;
            if (title.screenGroup != null && !title.screenGroup.blocksRaycasts) return;    // a sortie / the tutorial is starting
            EscapeGate.Consume();
            BackToMenu();
        }

        // ---- screens -----------------------------------------------------------------------------------------------------------
        /// <summary>The menu (the select screen hidden behind it), its entrance from the start.</summary>
        public void ShowMenu()
        {
            Build(SaveSystem.Peek());
            RebuildEntries(SaveSystem.Peek());
            if (title != null) title.gameObject.SetActive(false);
            content.gameObject.SetActive(true);
            CloseDialog();
            busy = false;
            PlayEnter(!heavyPlayed);
            heavyPlayed = true;
        }

        /// <summary>The select screen on the session's last side and map (the menu hidden).</summary>
        public void ShowSelect()
        {
            if (title == null) return;
            CloseDialog();
            content.gameObject.SetActive(false);
            var d = SaveSystem.Current;
            if (d != null)
            {
                string stem = d.lastMap ?? "";
                if (stem.Length > 0 && MapStore.Find(stem) == null) stem = "";      // the map is gone: the default house
                MapStore.SelectedStem = stem;
            }
            title.gameObject.SetActive(true);        // (OnEnable re-reads the remembered map)
            if (d != null) title.SelectSide(d.LastTeam);
            busy = false;
        }

        void BackToMenu()
        {
            busy = true;
            Wipe(ShowMenu);
        }

        // ---- the three choices -------------------------------------------------------------------------------------------------
        public void NewGame()
        {
            if (busy) return;
            if (SaveSystem.Exists) AskOverwrite();
            else StartNewGame();
        }

        /// <summary>ニューゲーム over an existing save: はい overwrites it and starts the tutorial.</summary>
        public void AskOverwrite() => Ask("ニューゲーム", "NEW GAME", "セーブデータを上書きして、最初から始めますか？", StartNewGame);

        void StartNewGame()
        {
            busy = true;
            SaveSystem.NewGame();
            MapStore.SelectedStem = "";
            // the tutorial: the title's own launch (its wipe, then the tutorial scene)
            if (title != null) title.LaunchTutorial();
            else Wipe(SceneFlow.GoTutorial);
        }

        public void Load()
        {
            if (busy) return;
            if (!SaveSystem.Load(out string why))
            {
                Debug.LogWarning("[MainMenu] the save could not be read: " + why);
                Ask("読み込み", "LOAD", "セーブデータを読み込めませんでした。", null, "確認", "キャンセル");
                RebuildEntries(SaveSystem.Peek());
                return;
            }
            busy = true;
            Wipe(ShowSelect);
        }

        public void Quit()
        {
            if (busy) return;
            Ask("ゲーム終了", "EXIT", "ゲームを終了しますか？", QuitNow);
        }

        public static void QuitNow()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.ExitPlaymode();
#else
            Application.Quit();
#endif
        }

        void Wipe(System.Action middle)
        {
            if (wipe && Application.isPlaying) KitWipe.Play(middle);
            else middle();
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

        // ---- motion ------------------------------------------------------------------------------------------------------------
        /// <summary>The entrance from the start: heavy = the logo's strong glitch intro (the first show in a run).</summary>
        public void PlayEnter(bool heavy)
        {
            entriesDelay = heavy ? HeavyDelay : CalmDelay;
            clock = 0f; enterStart = 0f;
            if (logo != null) logo.PlayEnter(heavy);
            Evaluate(0f);
        }

        /// <summary>Previews (edit mode): the menu `t` seconds into its entrance (the logo too).</summary>
        public void PoseAt(float t, bool heavy)
        {
            entriesDelay = heavy ? HeavyDelay : CalmDelay;
            clock = t; enterStart = 0f;
            if (logo != null) logo.PoseAt(t, heavy);
            Evaluate(t);
        }

        public void Evaluate(float t)
        {
            bool reduce = UIMotionSettings.reduceMotion;
            bool entering = enterStart > Never;
            for (int i = 0; i < rowCount; i++)
            {
                float p = t - enterStart - entriesDelay - i * EntryStep;
                float e = !entering ? 1f : p <= 0f ? 0f : reduce ? 1f : UIEase.Tactical(p / EntryTime);
                if (rowGroups[i].alpha != e) rowGroups[i].alpha = e;
                bool on = e >= 1f;
                if (rowGroups[i].blocksRaycasts != on) rowGroups[i].blocksRaycasts = on;
                var pos = homes[i] + new Vector2(reduce ? 0f : EnterShift * (1f - e), 0f);
                if (rows[i].anchoredPosition != pos) rows[i].anchoredPosition = pos;
            }
        }

        // ---- build -------------------------------------------------------------------------------------------------------------
        /// <summary>A menu under `canvas` (a 1440×810 menu canvas), built for `save` (null: no save, no 読み込み) over `art`.</summary>
        public static MainMenuScreen Create(Transform canvas, SaveData save, Texture art)
        {
            var root = HudKit.NewRect("MainMenu", canvas);
            HudKit.Stretch(root);
            var m = root.gameObject.AddComponent<MainMenuScreen>();      // (Play mode: Awake builds from the save on disk)
            m.background = art;
            m.Build(save);
            return m;
        }

        /// <summary>Builds the menu once; the entries follow `save`.</summary>
        public void Build(SaveData save)
        {
            if (content != null) return;
            var theme = UITheme.Current;
            var root = (RectTransform)transform;

            content = HudKit.NewRect("Content", root);
            HudKit.Stretch(content);

            var bg = HudKit.NewRect("DecoBg", content);
            HudKit.Stretch(bg);
            HudKit.AddImage(bg, Bg, raycast: true);

            // the key art, cropped to fill
            var art = HudKit.NewRect("Background", content);
            art.anchorMin = art.anchorMax = art.pivot = new Vector2(.5f, .5f);
            art.sizeDelta = new Vector2(ScreenW, ScreenH);
            backgroundImage = art.gameObject.AddComponent<RawImage>();
            backgroundImage.raycastTarget = false;
            backgroundImage.texture = background;
            backgroundImage.enabled = background != null;
            var fit = art.gameObject.AddComponent<AspectRatioFitter>();
            fit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            fit.aspectRatio = background != null && background.height > 0 ? (float)background.width / background.height : 16f / 9f;
            art.gameObject.AddComponent<LintIgnoreOverlap>().reason = "the full-screen key art; the menu sits on its empty right half";
            art.gameObject.AddComponent<LintIgnoreClip>().reason = "the key art is cropped to fill the screen on purpose";

            var dm = HudKit.NewRect("DecoDiag", content);
            UILayout.Place(dm, ScreenW - 560f, ScreenH - 12f, 560f, 12f);
            HudKit.AddImage(dm, Color.white);
            dm.gameObject.AddComponent<SlantedGraphic>().Set(SlantedGraphic.Size.Custom, SlantedGraphic.Direction.LeftEdge, 20f);
            dm.gameObject.AddComponent<Mask>().showMaskGraphic = false;
            var acc = theme.acc;
            diag = HazardStripe.Create(dm, "DecoDiagStripes", new Color(acc.r, acc.g, acc.b, .85f), new Color(0f, 0f, 0f, 0f), 14f, 28f, 1.6f);
            diag.over = Bg;

            var lr = HudKit.NewRect("Logo", content);
            UILayout.Place(lr, ColX, 0f, ColW, 160f);                      // (moved once the entries are known)
            logo = lr.gameObject.AddComponent<KitMenuLogo>();
            logo.size = LogoSize;
            logo.Build();

            dialog = DialogView.Build(root, OnDialogYes, CloseDialog);

            RebuildEntries(save);
            enterStart = Never;
            Evaluate(0f);
        }

        /// <summary>The entries for `save` (読み込み only when there is one), the column re-centred for their count.</summary>
        void RebuildEntries(SaveData save)
        {
            for (int i = 0; i < rows.Length; i++)
                if (rows[i] != null)
                {
                    rows[i].gameObject.SetActive(false);         // (DestroySafe may wait for the frame's end in Play mode)
                    HudKit.DestroySafe(rows[i].gameObject);
                    rows[i] = null;
                }
            rowCount = 0;
            int n = save != null ? 3 : 2;
            float top = ColumnTop(n);
            UILayout.Place((RectTransform)logo.transform, ColX, top, ColW, logo.Height);
            float y0 = top + logo.Height + EntriesGap;
            newGameButton = AddRow("NewGame", "ニューゲーム", y0, NewGame);
            loadButton = save != null ? AddRow("Load", "読み込み", y0 + (EntryH + EntryGap), Load) : null;
            quitButton = AddRow("Quit", "ゲーム終了", y0 + (n - 1) * (EntryH + EntryGap), Quit);
            Evaluate(enterStart > Never ? clock : 0f);
        }

        // a row (the entrance moves / fades it) holding the text entry (its own scale is the hover's)
        KitTextButton AddRow(string name, string label, float y, UnityEngine.Events.UnityAction onClick)
        {
            int i = rowCount++;
            var row = HudKit.NewRect(name + "Row", content);
            UILayout.Place(row, ColX, y, ColW, EntryH);
            homes[i] = row.anchoredPosition;
            rowGroups[i] = row.gameObject.AddComponent<CanvasGroup>();
            var b = KitTextButton.Create(row, name, label, EntryFont, EntryH, .5f, onClick);
            var rt = (RectTransform)b.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(.5f, .5f);
            rt.anchoredPosition = Vector2.zero;
            rows[i] = row;
            return b;
        }
    }
}
