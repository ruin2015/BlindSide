// A small modal question / message window for the editor's screens: dimmed full-screen backdrop that swallows clicks, a card with
// a title, a body text and a row of buttons (any number, any colours). `Count` tells the screens underneath that something modal is
// open, so they ignore keys and mouse meanwhile (the map browser counts itself in too).
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Blindside.MapEditor
{
    public static class ModalDialog
    {
        public readonly struct Choice
        {
            public readonly string label; public readonly Color bg, fg; public readonly Action action;
            public Choice(string label, Color bg, Color fg, Action action) { this.label = label; this.bg = bg; this.fg = fg; this.action = action; }
            public static Choice Primary(string label, Action a = null) => new Choice(label, MapEditorScreen.Blue, Color.white, a);
            public static Choice Danger(string label, Action a = null) => new Choice(label, MapEditorScreen.Foe, Color.white, a);
            public static Choice Plain(string label, Action a = null) => new Choice(label, MapEditorScreen.PaperDark, MapEditorScreen.Ink, a);
        }

        sealed class Marker : MonoBehaviour { void OnDestroy() => Exit(); }

        /// <summary>How many modal things are open (dialogs and the map browser).</summary>
        public static int Count { get; private set; }
        public static bool Any => Count > 0;
        public static void Enter() => Count++;
        public static void Exit() => Count = Mathf.Max(0, Count - 1);

        /// <summary>Opens the dialog on `canvas` (above everything on it). A button closes it first, then runs its action.</summary>
        public static GameObject Show(Transform canvas, string title, string body, params Choice[] choices)
        {
            var root = MapEditorScreen.NewRect("Modal_" + title, canvas);
            MapEditorScreen.Stretch(root);
            root.gameObject.AddComponent<Marker>();
            Enter();
            var dim = MapEditorScreen.NewImage("Dim", root, new Color(MapEditorScreen.Ink.r, MapEditorScreen.Ink.g, MapEditorScreen.Ink.b, .72f));
            MapEditorScreen.Stretch(dim.rectTransform);

            var window = MapEditorScreen.NewImage("Window", root, MapEditorScreen.Ink);
            window.rectTransform.anchorMin = window.rectTransform.anchorMax = window.rectTransform.pivot = new Vector2(.5f, .5f);
            window.rectTransform.sizeDelta = new Vector2(640, 0);
            MapEditorScreen.VBox(window.transform, 0, new RectOffset(3, 3, 3, 3));
            var fit = window.gameObject.AddComponent<ContentSizeFitter>();
            fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var paper = MapEditorScreen.NewImage("Paper", window.transform, MapEditorScreen.Paper);
            MapEditorScreen.VBox(paper.transform, 14, new RectOffset(30, 30, 26, 24));
            var t = MapEditorScreen.NewText(paper.transform, title, 28, FontStyles.Bold, MapEditorScreen.Ink);
            MapEditorScreen.Size(t, -1, 40);
            var b = MapEditorScreen.NewText(paper.transform, body, 18, FontStyles.Normal, new Color(MapEditorScreen.Ink.r, MapEditorScreen.Ink.g, MapEditorScreen.Ink.b, .85f), TextAlignmentOptions.TopLeft);
            b.textWrappingMode = TextWrappingModes.Normal;
            var row = MapEditorScreen.NewRect("Buttons", paper.transform);
            MapEditorScreen.Size(row, -1, 52);
            MapEditorScreen.HBox(row, 12, new RectOffset(0, 0, 4, 0), TextAnchor.MiddleRight);
            foreach (var c in choices)
            {
                var choice = c;
                MapEditorScreen.NewButton(row, choice.label, Mathf.Max(130, choice.label.Length * 24 + 40), 48, choice.bg, choice.fg, 19, () =>
                {
                    UnityEngine.Object.Destroy(root.gameObject);
                    choice.action?.Invoke();
                });
            }
            root.SetAsLastSibling();
            return root.gameObject;
        }
    }
}
