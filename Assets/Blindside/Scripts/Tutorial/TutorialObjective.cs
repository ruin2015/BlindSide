// The tutorial's "current objective" card on the right of the HUD: what to do now (「目標地点へ移動しよう」) and, under it, the
// controls that do it — a key chip (左クリック / ドラッグ / ESC …) and what it does, one row each. A new objective comes in through
// one run of the clock (ITimedEffect.Evaluate(t), unscaled): the card slides in from the right and the accent bar flashes; a change of
// rows only re-lays the card. Nothing allocates per frame (rows are pooled, laid out once in Set).
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Blindside.UI;

namespace Blindside.Tutorial
{
    public class TutorialObjective : MonoBehaviour, ITimedEffect
    {
        // right edge x20; under the illustration card (y76, 500 tall) so a selected unit's art never covers it
        public const float W = 320f, RightX = 20f, TopY = 590f, Pad = 14f, HeaderH = 22f, ObjSize = 19f, RowH = 28f, RowGap = 4f;
        public const float KeyW = 104f, KeyH = 22f, KeySize = 13f, DescSize = 14f, InTime = .42f, SlideFrom = 40f;
        public const int MaxRows = 5;

        public RectTransform card, accent;
        public TMP_Text objectiveText;
        public CanvasGroup group;
        Image accentImg;
        readonly RectTransform[] rows = new RectTransform[MaxRows];
        readonly TMP_Text[] keys = new TMP_Text[MaxRows], descs = new TMP_Text[MaxRows];
        float clock = 99f;
        bool shown;
        string current;

        public float Duration => InTime;

        /// <summary>Shows `objective` with its control rows (key, what it does). The same objective again only updates the rows.</summary>
        public void Set(string objective, params (string key, string what)[] controls)
        {
            bool fresh = !shown || objective != current;
            current = objective; shown = true;
            gameObject.SetActive(true);
            objectiveText.text = objective;
            float objH = Mathf.Ceil(objectiveText.GetPreferredValues(objective, W - Pad * 2f, 0f).y);
            Place(objectiveText.rectTransform, Pad, Pad + HeaderH + 2f, W - Pad * 2f, objH);
            float y = Pad + HeaderH + 2f + objH + 8f;
            for (int i = 0; i < rows.Length; i++)
            {
                bool on = controls != null && i < controls.Length;
                rows[i].gameObject.SetActive(on);
                if (!on) continue;
                keys[i].text = controls[i].key;
                descs[i].text = controls[i].what;
                Place(rows[i], Pad, y, W - Pad * 2f, RowH);
                y += RowH + RowGap;
            }
            card.sizeDelta = new Vector2(W, y + Pad - (controls != null && controls.Length > 0 ? RowGap : 10f));
            if (fresh) { clock = 0f; Evaluate(0f); }
        }

        public void Hide() { shown = false; current = null; gameObject.SetActive(false); }

        void Update()
        {
            if (clock > InTime + .5f) return;
            clock += Time.unscaledDeltaTime;
            Evaluate(clock);
        }

        public void Evaluate(float t)
        {
            float k = UIEase.Tactical(Mathf.Clamp01(t / InTime));
            group.alpha = k;
            card.anchoredPosition = new Vector2(-RightX + SlideFrom * (1f - k), -TopY);
            // the accent bar flashes white once as the new objective lands
            float f = Mathf.Clamp01(1f - (t - InTime * .5f) / .35f);
            accentImg.color = Color.Lerp(UIPalette.Accent, Color.white, t < InTime * .5f ? 0f : f);
        }

        // ================================================================
        //  Construction
        // ================================================================
        public static TutorialObjective Build(Transform canvas)
        {
            var root = NewRect("TutorialObjective", canvas);
            root.anchorMin = root.anchorMax = new Vector2(1f, 1f); root.pivot = new Vector2(1f, 1f);   // top right
            root.sizeDelta = new Vector2(W, 120f);
            var o = root.gameObject.AddComponent<TutorialObjective>();
            o.card = root;
            o.group = root.gameObject.AddComponent<CanvasGroup>();
            o.group.blocksRaycasts = false; o.group.interactable = false;

            var bg = NewRect("DecoObjectivePanel", root); Stretch(bg);
            Img(bg, new Color(UIPalette.Ink.r, UIPalette.Ink.g, UIPalette.Ink.b, .9f));
            var acc = NewRect("DecoObjectiveAccent", root);
            acc.anchorMin = new Vector2(0f, 0f); acc.anchorMax = new Vector2(0f, 1f); acc.pivot = new Vector2(0f, .5f);
            acc.sizeDelta = new Vector2(4f, 0f); acc.anchoredPosition = Vector2.zero;
            o.accentImg = Img(acc, UIPalette.Accent);
            o.accent = acc;

            var tag = Text(root, "CURRENT OBJECTIVE", 12f, UIPalette.Accent, UITheme.TextRole.Display, "Tag");
            tag.characterSpacing = 6f;
            Place(tag.rectTransform, Pad, Pad, 190f, HeaderH);
            var tagJa = Text(root, "現在の目標", 13f, new Color(.62f, .64f, .68f), UITheme.TextRole.Body, "TagJa");
            tagJa.alignment = TextAlignmentOptions.TopRight;
            Place(tagJa.rectTransform, W - Pad - 110f, Pad - 1f, 110f, HeaderH);

            o.objectiveText = Text(root, "", ObjSize, Color.white, UITheme.TextRole.Body, "Objective");
            o.objectiveText.textWrappingMode = TextWrappingModes.Normal;

            for (int i = 0; i < o.rows.Length; i++)
            {
                var row = NewRect("Control" + i, root);
                var chip = NewRect("KeyChip", row);
                Place(chip, 0f, (RowH - KeyH) * .5f, KeyW, KeyH);
                Img(chip, new Color(1f, 1f, 1f, .12f));
                var edge = NewRect("DecoKeyEdge", chip);
                edge.anchorMin = new Vector2(0f, 0f); edge.anchorMax = new Vector2(1f, 0f); edge.pivot = new Vector2(.5f, 0f);
                edge.sizeDelta = new Vector2(0f, 2f); edge.anchoredPosition = Vector2.zero;
                Img(edge, UIPalette.Accent);
                var key = Text(chip, "", KeySize, UIPalette.Accent, UITheme.TextRole.Body, "Key");
                key.alignment = TextAlignmentOptions.Center;
                Stretch(key.rectTransform);
                var desc = Text(row, "", DescSize, new Color(.9f, .91f, .93f), UITheme.TextRole.Body, "What");
                desc.alignment = TextAlignmentOptions.Left;
                Place(desc.rectTransform, KeyW + 10f, 0f, W - Pad * 2f - KeyW - 10f, RowH);
                o.rows[i] = row; o.keys[i] = key; o.descs[i] = desc;
                row.gameObject.SetActive(false);
            }
            root.gameObject.SetActive(false);
            return o;
        }

        static RectTransform NewRect(string name, Transform parent)
        {
            var rt = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            return rt;
        }

        static void Stretch(RectTransform rt) { rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero; }

        /// <summary>Top-left placement in the parent (y down).</summary>
        static void Place(RectTransform rt, float x, float y, float w, float h)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f); rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(x, -y); rt.sizeDelta = new Vector2(w, h);
        }

        static Image Img(RectTransform rt, Color c)
        {
            var img = rt.gameObject.AddComponent<Image>();
            img.color = c; img.raycastTarget = false;
            return img;
        }

        static TMP_Text Text(RectTransform parent, string s, float size, Color c, UITheme.TextRole role, string name)
        {
            var rt = NewRect(name, parent);
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            t.text = s; t.fontSize = size; t.color = c; t.raycastTarget = false;
            t.alignment = TextAlignmentOptions.TopLeft;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            UIFonts.Apply(t, role);
            return t;
        }
    }
}
