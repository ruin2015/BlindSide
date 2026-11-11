// The tutorial's walk round the attack forecast (EngagePreview): before the first duel the panel is put up and each of its parts is
// picked out in turn — the rest of the screen darkens round it, the demonstration's pointer hovers on it, a framed chip names it and a
// note beside the panel says what it means. A click / Space / Enter goes on to the next part.
// Its own overlay canvas above the panel (order 62, the panel's scale). One unscaled clock (ITimedEffect.Evaluate(t)): the spotlight glides
// from the last part to this one; nothing is allocated per frame.
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Blindside.UI;

namespace Blindside.Tutorial
{
    public class TutorialForecastTour : MonoBehaviour, ITimedEffect
    {
        public const int SortingOrder = 62;
        public const float MoveTime = .28f, NoteW = 560f, NoteH = 230f, NoteX = EngagePreview.PanelX + EngagePreview.W + 70f, Margin = 8f, DimAlpha = .62f;

        struct Step
        {
            public string[] parts;
            public string title, body;
        }

        static readonly Step[] Steps =
        {
            new Step { parts = new[] { "TopPlate", "TopFace" }, title = "味方",
                       body = "上と左が、撃つ側のお前だ。\n名前と、持っている銃が書いてある。" },
            new Step { parts = new[] { "BottomPlate", "BottomFace" }, title = "敵",
                       body = "下と右が相手だ。\n敵が生き残れば反撃してくるから、\n両方の数字を見ろ。" },
            new Step { parts = new[] { "TopHp", "TopHpBar", "HpTag", "BottomHp", "BottomHpBar" }, title = "HP",
                       body = "左が味方、右が敵のHPだ。\n「100 > 64」は、交戦後に64まで減るという意味だ。\n点滅している赤い部分が失う量で、\n0になるならK.I.Aと表示される。" },
            new Step { parts = new[] { "DecoRow0" }, title = "ダメージ",
                       body = "一発当たったときのダメージだ。\n先に見つけて撃てば、「×1.4」の札が付いて\nダメージが増える。" },
            new Step { parts = new[] { "DecoRow1" }, title = "命中",
                       body = "一発が当たる確率だ。\n距離や武器で変わり、壁越しなら下がる。" },
            new Step { parts = new[] { "DecoRow2" }, title = "ヘッドショット",
                       body = "当たった弾がヘッドショットになる確率だ。\nヘッドショットはダメージが大きい。" },
            new Step { parts = new[] { "DecoRow3" }, title = "射撃回数",
                       body = "一回の交戦で撃つ弾の数だ。\n持っている銃によって変わる。" },
            new Step { parts = new[] { "DecoRow4" }, title = "結果",
                       body = "この交戦がどうなるかの答えだ。\n「2/3」は三発中二発が当たるという意味で、\n横の数字が合計ダメージ、\n「CRIT」はヘッドショットの数だ。" },
            new Step { parts = new[] { "TopPlate", "TopFace", "DecoRow4", "BottomPlate" }, title = "攻撃予測",
                       body = "この予測は、実際の交戦結果と同じになる。\n撃つ前によく見て、撃つか引くかを決めろ。" },
        };

        public static int StepCount => Steps.Length;

        Canvas canvas;
        RectTransform root, note, lead;
        readonly RectTransform[] dims = new RectTransform[4];
        TMP_Text titleText, bodyText, countText;
        TutorialGuide guide;
        Vector2 fromMin, fromMax, toMin, toMax;
        float clock;
        bool moving, advanced;

        public float Duration => MoveTime;
        public GameObject CanvasObject => canvas != null ? canvas.gameObject : null;

        /// <summary>Goes through every part of `p`; ends when the last is clicked on.</summary>
        public IEnumerator Run(EngagePreview p)
        {
            for (int i = 0; i < Steps.Length; i++)
            {
                Show(p, i, i == 0);
                float sf = canvas.scaleFactor > 0f ? canvas.scaleFactor : 1f;
                guide.CursorTo(new Vector2(toMax.x - 34f, (toMin.y + toMax.y) * .5f - 6f) * sf, i == 0);
                advanced = false;
                while (!advanced) yield return null;
                yield return null;   // (the press that advanced isn't the next step's)
            }
            guide.HideCursor(); guide.HideFrame();
        }

        /// <summary>Previews: part `i`, settled, the pointer on it.</summary>
        public void Pose(EngagePreview p, int i)
        {
            Show(p, i, true);
            moving = false;
            Evaluate(MoveTime);
            guide.CursorLocal(new Vector2(toMax.x - 34f, (toMin.y + toMax.y) * .5f - 6f));
            guide.Evaluate(.2f);
        }

        void Show(EngagePreview p, int i, bool snap)
        {
            var s = Steps[i];
            var min = new Vector3(float.MaxValue, float.MaxValue, 0f);
            var max = new Vector3(float.MinValue, float.MinValue, 0f);
            foreach (var part in s.parts) p.Encapsulate(part, ref min, ref max);
            fromMin = toMin; fromMax = toMax;
            toMin = ToLocal(min) - Vector2.one * Margin;
            toMax = ToLocal(max) + Vector2.one * Margin;
            if (snap) { fromMin = toMin; fromMax = toMax; }
            titleText.text = s.title;
            bodyText.text = s.body;
            countText.text = (i + 1) + " / " + Steps.Length;
            clock = 0f; moving = true;
            Evaluate(0f);
        }

        // a world point → this canvas's units, from its bottom left
        Vector2 ToLocal(Vector3 world)
        {
            var l = (Vector2)root.InverseTransformPoint(world);
            var r = root.rect;
            return l - r.min;
        }

        public void Evaluate(float t)
        {
            float k = UIMotionSettings.reduceMotion ? 1f : UIEase.Tactical(Mathf.Clamp01(t / MoveTime));
            var min = Vector2.Lerp(fromMin, toMin, k);
            var max = Vector2.Lerp(fromMax, toMax, k);
            var size = root.rect.size;
            SetDim(dims[0], 0f, 0f, size.x, min.y);                       // below
            SetDim(dims[1], 0f, max.y, size.x, size.y - max.y);           // above
            SetDim(dims[2], 0f, min.y, min.x, max.y - min.y);             // left
            SetDim(dims[3], max.x, min.y, size.x - max.x, max.y - min.y); // right
            guide.FrameLocal(min, max, titleText.text);
            // the line from the part to the note
            var from = new Vector2(max.x, (min.y + max.y) * .5f);
            var to = new Vector2(NoteX, size.y * .5f);
            var d = to - from;
            lead.anchoredPosition = from;
            lead.sizeDelta = new Vector2(d.magnitude, 2f);
            lead.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
        }

        static void SetDim(RectTransform r, float x, float y, float w, float h)
        {
            r.anchoredPosition = new Vector2(x, y);
            r.sizeDelta = new Vector2(Mathf.Max(0f, w), Mathf.Max(0f, h));
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            if (moving)
            {
                clock += dt;
                Evaluate(clock);
                if (clock >= MoveTime) moving = false;
            }
            var kb = Keyboard.current; var mouse = Mouse.current;
            if ((mouse != null && mouse.leftButton.wasPressedThisFrame)
                || (kb != null && (kb.spaceKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame)))
                advanced = true;
        }

        /// <param name="onCanvas">build onto this canvas instead of a new overlay one (Render Previews' stage)</param>
        public static TutorialForecastTour Build(Transform parent, Canvas onCanvas = null)
        {
            var theme = UITheme.Current;
            var c = onCanvas;
            if (c == null)
            {
                var cgo = new GameObject("ForecastTour_Canvas", typeof(RectTransform));
                cgo.transform.SetParent(parent, false);
                c = cgo.AddComponent<Canvas>();
                c.renderMode = RenderMode.ScreenSpaceOverlay;
                c.sortingOrder = SortingOrder;
                theme.ApplyTo(cgo.AddComponent<CanvasScaler>(), UITheme.CanvasKind.Hud);
            }
            var root = HudKit.NewRect("ForecastTour", c.transform);
            HudKit.Stretch(root);
            var t = root.gameObject.AddComponent<TutorialForecastTour>();
            t.canvas = c; t.root = root;

            for (int i = 0; i < 4; i++)
            {
                var d = HudKit.NewRect("DecoDim" + i, root);
                d.anchorMin = d.anchorMax = d.pivot = Vector2.zero;
                HudKit.AddImage(d, new Color(.03f, .035f, .045f, DimAlpha));
                t.dims[i] = d;
            }
            t.lead = HudKit.NewRect("DecoLead", root);
            t.lead.anchorMin = t.lead.anchorMax = Vector2.zero; t.lead.pivot = new Vector2(0f, .5f);
            HudKit.AddImage(t.lead, theme.acc);

            var note = HudKit.NewRect("Note", root);
            note.anchorMin = note.anchorMax = new Vector2(0f, .5f); note.pivot = new Vector2(0f, .5f);
            note.anchoredPosition = new Vector2(NoteX, 0f); note.sizeDelta = new Vector2(NoteW, NoteH);
            t.note = note;
            HudKit.AddImage(note, new Color(theme.ink.r, theme.ink.g, theme.ink.b, .97f));
            var edge = HudKit.NewRect("DecoEdge", note);
            UILayout.Place(edge, 0f, 0f, 6f, NoteH);
            HudKit.AddImage(edge, theme.acc);
            t.titleText = HudKit.AddText(note, "", 28f, Color.white, true, TextAlignmentOptions.TopLeft, "Title", UITheme.TextRole.Title);
            t.titleText.textWrappingMode = TextWrappingModes.NoWrap;
            UILayout.Place(t.titleText.rectTransform, 28f, 18f, NoteW - 150f, 38f);
            t.countText = HudKit.AddText(note, "", 16f, theme.acc, true, TextAlignmentOptions.TopRight, "Count", UITheme.TextRole.Body);
            t.countText.textWrappingMode = TextWrappingModes.NoWrap;
            UILayout.Place(t.countText.rectTransform, NoteW - 110f, 24f, 90f, 22f);
            t.bodyText = HudKit.AddText(note, "", 20f, Color.white, false, TextAlignmentOptions.TopLeft, "Body", UITheme.TextRole.Body);
            t.bodyText.textWrappingMode = TextWrappingModes.Normal;
            UILayout.Place(t.bodyText.rectTransform, 28f, 62f, NoteW - 52f, 128f);
            var hint = HudKit.AddText(note, "クリック / Space で次へ", 14f, new Color(.7f, .73f, .78f), false, TextAlignmentOptions.TopRight, "Hint", UITheme.TextRole.Body);
            hint.textWrappingMode = TextWrappingModes.NoWrap;
            UILayout.Place(hint.rectTransform, 28f, NoteH - 32f, NoteW - 48f, 20f);

            t.guide = TutorialGuide.Build(c.transform, c);
            return t;
        }
    }
}
