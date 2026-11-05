// The title scene's main menu (in front of the side / map select screen, TitleSelectScreen), built at runtime on a full-screen object
// under the title canvas (1440×810):
//   DecoBg      the title background #0a0c0f over the whole screen (takes the clicks the select screen behind it must not get)
//   DecoDiag    the title's hazard stripes, 560×12 at the bottom left (acc .85, 14 / 28, scrolling 1.6s), right edge leaning 20
//   Logo        KitMenuLogo, centred: TAC-OS line, BLINDSIDE 150 as a glitch, the logo bar (y 200, 205 high)
//   Buttons     TacticalButton 340×48, centred, 10 apart, from y 445:
//                 01 ニューゲーム / NEW GAME   a fresh save (asks first when one exists: it is overwritten), then the tutorial
//                 02 読み込み / LOAD          only when a save exists; its date on the right (yyyy.MM.dd HH:mm). The save becomes the
//                                             session's and the select screen opens on its last side and map
//                 03 ゲーム終了 / EXIT         asks, then quits (the editor leaves Play mode)
//               entrance: .5s each from .7s, 60ms apart — fade + 80 from the right (the title blocks' sdIn)
//   Dialog      DialogView (はい / いいえ) for the questions above and a save that cannot be read
// Screen changes play KitWipe (the title's wipe) and switch at its middle. The select screen comes back to this menu with ESC (when no
// window / dialog of its own is open). A title scene opened while a save session is on (back from a match) shows the select screen
// at once. Motion: ITimedEffect.Evaluate(t) on an unscaled clock; no allocation per frame.
using TMPro;
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
        public const float ScreenW = 1440f, ScreenH = 810f, LogoTop = 200f, ButtonW = 340f, ButtonH = 48f, ButtonGap = 10f, ButtonsGap = 40f;
        public const float ButtonsDelay = .7f, ButtonStep = .06f, ButtonTime = .5f, EnterShift = 80f;
        const float Never = -100f;
        static readonly Color Bg = new Color32(0x0a, 0x0c, 0x0f, 0xff);

        [Tooltip("The side / map select screen behind this menu (found in the scene when empty).")]
        public TitleSelectScreen title;
        [Tooltip("Screen changes play the title's wipe (KitWipe).")]
        public bool wipe = true;

        // built
        public RectTransform content;
        public KitMenuLogo logo;
        public TacticalButton newGameButton, loadButton, quitButton;
        public DialogView dialog;
        public HazardStripe diag;
        readonly RectTransform[] rows = new RectTransform[3];
        readonly Vector2[] homes = new Vector2[3];
        readonly CanvasGroup[] rowGroups = new CanvasGroup[3];
        int rowCount;

        float clock, enterStart = Never;
        bool busy;
        System.Action pendingYes;

        /// <summary>The menu is showing (false while the select screen is).</summary>
        public bool MenuVisible => content != null && content.gameObject.activeSelf;

        public float Duration => ButtonsDelay + 2f * ButtonStep + ButtonTime;

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
            if (kb == null || !kb.escapeKey.wasPressedThisFrame || busy) return;
            if (dialog != null && dialog.Visible) { CloseDialog(); return; }
            if (MenuVisible || title == null || !title.gameObject.activeInHierarchy) return;
            // the select screen's own windows take ESC first (they close themselves)
            if (ModalDialog.Any || (MapBrowserWindow.Instance != null && MapBrowserWindow.Instance.gameObject.activeInHierarchy)) return;
            var options = title.GetComponentInChildren<KitOptionsPanel>(false);
            if (options != null && options.IsOpen) return;
            if (title.screenGroup != null && !title.screenGroup.blocksRaycasts) return;    // a sortie / the tutorial is starting
            BackToMenu();
        }

        // ---- screens -----------------------------------------------------------------------------------------------------------
        /// <summary>The menu (the select screen hidden behind it), its entrance from the start.</summary>
        public void ShowMenu()
        {
            Build(SaveSystem.Peek());
            RebuildButtons(SaveSystem.Peek());
            if (title != null) title.gameObject.SetActive(false);
            content.gameObject.SetActive(true);
            CloseDialog();
            busy = false;
            PlayEnter();
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
                RebuildButtons(SaveSystem.Peek());
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

        static void QuitNow()
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
        public void PlayEnter()
        {
            clock = 0f; enterStart = 0f;
            if (logo != null) logo.PlayEnter();
            Evaluate(0f);
        }

        /// <summary>Previews (edit mode): the menu `t` seconds into its entrance (the logo too).</summary>
        public void PoseAt(float t)
        {
            clock = t; enterStart = 0f;
            if (logo != null) logo.PoseAt(t);
            Evaluate(t);
        }

        public void Evaluate(float t)
        {
            bool reduce = UIMotionSettings.reduceMotion;
            bool entering = enterStart > Never;
            for (int i = 0; i < rowCount; i++)
            {
                float p = t - enterStart - ButtonsDelay - i * ButtonStep;
                float e = !entering ? 1f : p <= 0f ? 0f : reduce ? 1f : UIEase.Tactical(p / ButtonTime);
                if (rowGroups[i].alpha != e) rowGroups[i].alpha = e;
                bool on = e >= 1f;
                if (rowGroups[i].blocksRaycasts != on) rowGroups[i].blocksRaycasts = on;
                var pos = homes[i] + new Vector2(reduce ? 0f : EnterShift * (1f - e), 0f);
                if (rows[i].anchoredPosition != pos) rows[i].anchoredPosition = pos;
            }
        }

        // ---- build -------------------------------------------------------------------------------------------------------------
        /// <summary>A menu under `canvas` (a 1440×810 menu canvas), built for `save` (null: no save, no 読み込み).</summary>
        public static MainMenuScreen Create(Transform canvas, SaveData save)
        {
            var root = HudKit.NewRect("MainMenu", canvas);
            HudKit.Stretch(root);
            var m = root.gameObject.AddComponent<MainMenuScreen>();      // (Play mode: Awake builds from the save on disk)
            m.Build(save);
            return m;
        }

        /// <summary>Builds the menu once; the button rows follow `save`.</summary>
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

            var dm = HudKit.NewRect("DecoDiag", content);
            UILayout.Place(dm, 0f, ScreenH - 12f, 560f, 12f);
            HudKit.AddImage(dm, Color.white);
            dm.gameObject.AddComponent<SlantedGraphic>().Set(SlantedGraphic.Size.Custom, SlantedGraphic.Direction.RightEdge, 20f);
            dm.gameObject.AddComponent<Mask>().showMaskGraphic = false;
            var acc = theme.acc;
            diag = HazardStripe.Create(dm, "DecoDiagStripes", new Color(acc.r, acc.g, acc.b, .85f), new Color(0f, 0f, 0f, 0f), 14f, 28f, 1.6f);
            diag.over = Bg;

            var lr = HudKit.NewRect("Logo", content);
            UILayout.Place(lr, 0f, LogoTop, ScreenW, KitMenuLogo.Height);
            logo = lr.gameObject.AddComponent<KitMenuLogo>();
            logo.Build();

            dialog = DialogView.Build(root, OnDialogYes, CloseDialog);

            RebuildButtons(save);
            enterStart = Never;
            Evaluate(0f);
        }

        /// <summary>The button column for `save`: 読み込み only when there is one (with its date).</summary>
        void RebuildButtons(SaveData save)
        {
            for (int i = 0; i < rows.Length; i++)
                if (rows[i] != null)
                {
                    rows[i].gameObject.SetActive(false);         // (DestroySafe may wait for the frame's end in Play mode)
                    HudKit.DestroySafe(rows[i].gameObject);
                    rows[i] = null;
                }
            rowCount = 0;
            newGameButton = AddRow("NewGame", "ニューゲーム", "NEW GAME", null, NewGame);
            loadButton = save != null ? AddRow("Load", "読み込み", "LOAD", save.SavedLocal.ToString("yyyy.MM.dd HH:mm"), Load) : null;
            quitButton = AddRow("Quit", "ゲーム終了", "EXIT", null, Quit);
            Evaluate(enterStart > Never ? clock : 0f);
        }

        // a row (the entrance moves / fades it) holding the button (its own CanvasGroup stays the button's: disabled look)
        TacticalButton AddRow(string name, string label, string sub, string value, UnityEngine.Events.UnityAction onClick)
        {
            int i = rowCount++;
            var row = HudKit.NewRect(name + "Row", content);
            float y = LogoTop + KitMenuLogo.Height + ButtonsGap + i * (ButtonH + ButtonGap);
            UILayout.Place(row, Mathf.Round((ScreenW - ButtonW) * .5f), y, ButtonW, ButtonH);
            homes[i] = row.anchoredPosition;
            rowGroups[i] = row.gameObject.AddComponent<CanvasGroup>();
            var b = TacticalButton.Create(row, name, (i + 1).ToString("00"), label, sub, value, new Vector2(ButtonW, ButtonH));
            UILayout.Place((RectTransform)b.transform, 0f, 0f, ButtonW, ButtonH);
            b.onClick.AddListener(onClick);
            rows[i] = row;
            return b;
        }
    }
}
