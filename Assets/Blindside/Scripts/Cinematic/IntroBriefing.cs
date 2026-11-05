// The intro's mission briefing, top left over the opening helicopter shot: a "MISSION BRIEF // 作戦概要" header and the facts of the
// operation, one row each — label in the display font, value in the body font — between corner brackets and an accent rule.
// One run of the clock (ITimedEffect.Evaluate(t), unscaled; Play starts it):
//   frame      the rule grows down and the brackets fade in over FrameIn
//   rows       row i slides in from the left starting RowStart + i·RowGap; its value types on (CharsPerSecond)
//   OPERATION  instead of typing, a glitch that resolves: at first every character is a random glyph, the line jitters and splits
//              into red / blue copies (ui_spec glitchRed / glitchBlue); over GlitchTime the glyphs settle into the real name
//   WEATHER    symbols, not words: sun-behind-cloud, thermometer + temperature, an arrow along the wind + its speed (the icons pop
//              in one after another; drawn by IntroBriefingIcons)
//   out        everything fades over the last FadeOut seconds of Duration
// The date and time are the real ones (the operation is "now"); nothing allocates per frame (texts are set in Play, the glitch
// rewrites a fixed char buffer only when its 20 fps step changes).
using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Blindside.UI;

namespace Blindside.Cinematic
{
    public class IntroBriefing : MonoBehaviour, ITimedEffect
    {
        public const float FrameIn = .35f, RowStart = .2f, RowGap = .16f, RowIn = .3f, CharsPerSecond = 40f, FadeOut = .5f;
        public const float GlitchTime = 1.4f, GlitchFps = 20f, IconGap = .12f;
        // (made smaller on request: about 0.8 of the first size, the long values still on one line)
        public const float Width = 480f, RowHeight = 27f, HeadHeight = 28f, LabelW = 106f, Left = 48f, Top = 84f;
        public const float HeadSize = 13f, LabelSize = 11f, ValueSize = 14.5f, OperationSize = 18f;
        static readonly string[] Days = { "日", "月", "火", "水", "木", "金", "土" };
        static readonly string[] Labels = { "OPERATION", "DATE", "WEATHER", "LOCATION", "UNIT", "THREAT" };
        const int Operation = 0, Weather = 2;
        const string GlitchPool = "アイウエオカキクケコサシスセソタチツテト#%&@$*!?<>/=+01ナニヌネノ";
        static readonly Color GlitchRed = UIPalette.Hex("#FF3E5C"), GlitchBlue = UIPalette.Hex("#27B4FF");

        // the weather (fixed for the scene: a bright, partly cloudy morning, light north-west wind)
        public const float TemperatureC = 14f, WindFromDeg = 315f, WindSpeed = 3f;

        public RectTransform root, rule;
        public CanvasGroup group, brackets;
        public RectTransform[] rows;
        public TMP_Text[] values;              // null for the weather row
        public CanvasGroup[] rowGroups;
        public TMP_Text opRed, opBlue;         // the operation name's split copies
        public RectTransform[] weatherIcons;   // pop in one after another
        public float duration = 5f;

        float clock; bool running;
        int[] chars = new int[0];
        char[] opTarget = new char[0], opBuf = new char[0];
        int lastStep = -1;

        public float Duration => duration;

        /// <summary>The typed rows' values (index = row; the weather row's entry is unused — it is symbols).</summary>
        public static string[] DefaultValues(DateTime now) => new[]
        {
            "作戦「ブラインドサイド」",
            $"{now:yyyy.MM.dd}（{Days[(int)now.DayOfWeek]}） {now:HH:mm} JST",
            "",
            "郊外住宅地 · 目標建物: 民家",
            "アルファチーム 5名 · ヘリボーン降下",
            "武装勢力 推定5名 · 人質1名",
        };

        public void Play(string[] texts, float seconds)
        {
            duration = seconds;
            root.gameObject.SetActive(true);   // first: an inactive text doesn't lay out, its character count would read 0
            for (int i = 0; i < values.Length; i++)
            {
                if (values[i] == null) continue;
                values[i].text = i < texts.Length ? texts[i] : "";
                values[i].maxVisibleCharacters = 99999;
                values[i].ForceMeshUpdate();
                chars[i] = values[i].textInfo.characterCount;
            }
            opTarget = values[Operation].text.ToCharArray();
            opBuf = (char[])opTarget.Clone();
            lastStep = -1;
            clock = 0f; running = true;
            Evaluate(0f);
        }

        public void Stop() { running = false; root.gameObject.SetActive(false); }

        void Update()
        {
            if (!running) return;
            clock += Time.unscaledDeltaTime;
            Evaluate(clock);
            if (clock >= duration) { running = false; root.gameObject.SetActive(false); }
        }

        public void Evaluate(float t)
        {
            float outA = Mathf.Clamp01((duration - t) / FadeOut);
            if (group.alpha != outA) group.alpha = outA;
            float f = UIEase.Tactical(t / FrameIn);
            rule.sizeDelta = new Vector2(rule.sizeDelta.x, root.rect.height * f);
            brackets.alpha = f;
            for (int i = 0; i < rows.Length; i++)
            {
                float local = t - (RowStart + i * RowGap);
                float p = UIEase.Tactical(local / RowIn);
                rowGroups[i].alpha = p;
                rows[i].anchoredPosition = new Vector2(18f - 26f * (1f - p), rows[i].anchoredPosition.y);
                if (i == Operation) { Glitch(local); continue; }
                if (i == Weather)
                {
                    for (int k = 0; k < weatherIcons.Length; k++)
                    {
                        float q = UIEase.Tactical((local - k * IconGap) / .25f);
                        weatherIcons[k].localScale = Vector3.one * (q <= 0f ? 0f : Mathf.LerpUnclamped(.4f, 1f, q));
                    }
                    continue;
                }
                int shown = Mathf.Clamp(Mathf.FloorToInt(local * CharsPerSecond), 0, chars[i]);
                if (values[i].maxVisibleCharacters != shown) values[i].maxVisibleCharacters = shown;
            }
        }

        static float Hash(int a, int b)
        {
            uint h = (uint)(a * 374761393 + b * 668265263);
            h = (h ^ (h >> 13)) * 1274126177u;
            return (h ^ (h >> 16)) / 4294967295f;
        }

        /// <summary>The operation name `local` seconds after its row starts: scrambled and split at first, settled after GlitchTime.</summary>
        void Glitch(float local)
        {
            var main = values[Operation];
            float g = local <= 0f ? 1f : 1f - Mathf.SmoothStep(0f, 1f, local / GlitchTime);
            int step = Mathf.FloorToInt(Mathf.Max(0f, local) * GlitchFps);
            // a flicker on top of the decay: some steps spike back up a little
            float gs = Mathf.Clamp01(g * (.75f + .5f * Hash(step, 7)));
            if (step != lastStep)
            {
                lastStep = step;
                for (int i = 0; i < opTarget.Length; i++)
                {
                    bool keep = char.IsWhiteSpace(opTarget[i]) || Hash(i, step) >= gs * .98f;
                    opBuf[i] = keep ? opTarget[i] : GlitchPool[(int)(Hash(i + 31, step * 3) * GlitchPool.Length) % GlitchPool.Length];
                }
                main.SetCharArray(opBuf, 0, opBuf.Length);
                opRed.SetCharArray(opBuf, 0, opBuf.Length);
                opBlue.SetCharArray(opBuf, 0, opBuf.Length);
            }
            float jx = (Hash(step, 3) - .5f) * 18f * gs, split = 2f + 9f * gs * Hash(step, 11);
            main.rectTransform.anchoredPosition = new Vector2(LabelW + jx, 0f);
            opRed.rectTransform.anchoredPosition = new Vector2(LabelW + jx - split, 0f);
            opBlue.rectTransform.anchoredPosition = new Vector2(LabelW + jx + split, 0f);
            float a = gs > .02f ? .85f * Mathf.Min(1f, gs * 1.5f) : 0f;
            opRed.alpha = a; opBlue.alpha = a;
        }

        // ================================================================
        //  Construction (runtime, under the intro's canvas)
        // ================================================================
        public static IntroBriefing Build(Transform canvas)
        {
            int n = Labels.Length;
            var root = Rect("IntroBriefing", canvas);
            root.anchorMin = root.anchorMax = new Vector2(0f, 1f); root.pivot = new Vector2(0f, 1f);
            root.anchoredPosition = new Vector2(Left, -Top);
            root.sizeDelta = new Vector2(Width, HeadHeight + RowHeight * n + 14f);
            var b = root.gameObject.AddComponent<IntroBriefing>();
            b.root = root;
            b.group = root.gameObject.AddComponent<CanvasGroup>();
            b.group.blocksRaycasts = false; b.group.interactable = false;
            root.gameObject.AddComponent<RectMask2D>();   // the rows slide in from under the plate's left edge, not over it

            // a faint ink plate, the accent rule down the left, corner brackets
            var plate = Rect("DecoBriefPlate", root); Stretch(plate);
            Img(plate, new Color(UIPalette.Ink.r, UIPalette.Ink.g, UIPalette.Ink.b, .45f));
            b.rule = Rect("DecoBriefRule", root);
            b.rule.anchorMin = b.rule.anchorMax = new Vector2(0f, 1f); b.rule.pivot = new Vector2(0f, 1f);
            b.rule.sizeDelta = new Vector2(4f, 0f); b.rule.anchoredPosition = Vector2.zero;
            Img(b.rule, UIPalette.Accent);
            var br = Rect("DecoBriefBrackets", root); Stretch(br);
            b.brackets = br.gameObject.AddComponent<CanvasGroup>();
            Bracket(br, new Vector2(1f, 1f)); Bracket(br, new Vector2(1f, 0f));

            var head = Text(root, "MISSION BRIEF <color=#FFFFFF>//</color> <size=13.5>作戦概要</size>", HeadSize, UIPalette.Accent, UITheme.TextRole.Display, "Header");
            head.characterSpacing = 8f;
            var hr = head.rectTransform;
            hr.anchorMin = new Vector2(0f, 1f); hr.anchorMax = new Vector2(1f, 1f); hr.pivot = new Vector2(0f, 1f);
            hr.offsetMin = new Vector2(18f, -HeadHeight - 4f); hr.offsetMax = new Vector2(-14f, -4f);

            b.rows = new RectTransform[n]; b.values = new TMP_Text[n]; b.rowGroups = new CanvasGroup[n]; b.chars = new int[n];
            for (int i = 0; i < n; i++)
            {
                var row = Rect("Row_" + Labels[i], root);
                row.anchorMin = row.anchorMax = new Vector2(0f, 1f); row.pivot = new Vector2(0f, 1f);
                row.sizeDelta = new Vector2(Width - 32f, RowHeight);
                row.anchoredPosition = new Vector2(18f, -(HeadHeight + RowHeight * i + 6f));
                b.rows[i] = row;
                b.rowGroups[i] = row.gameObject.AddComponent<CanvasGroup>();
                var label = Text(row, Labels[i], LabelSize, new Color(.66f, .7f, .75f), UITheme.TextRole.Display, "Label");
                label.characterSpacing = 10f;
                Place(label.rectTransform, 0f, LabelW - 6f);
                if (i == Weather) { b.weatherIcons = BuildWeather(row); continue; }
                bool op = i == Operation;
                var value = Text(row, "", op ? OperationSize : ValueSize, Color.white, op ? UITheme.TextRole.Title : UITheme.TextRole.Body, "Value");
                value.textWrappingMode = TextWrappingModes.NoWrap;
                Place(value.rectTransform, LabelW, Width - 32f - LabelW);
                b.values[i] = value;
                if (op)
                {
                    // the split copies, behind the name
                    b.opRed = Copy(row, value, GlitchRed, "DecoOpSplitRed");
                    b.opBlue = Copy(row, value, GlitchBlue, "DecoOpSplitBlue");
                    value.transform.SetAsLastSibling();
                }
            }
            root.gameObject.SetActive(false);
            return b;
        }

        /// <summary>Sun-behind-cloud · thermometer 14°C · wind arrow (pointing where it blows) NW 3m/s, left to right.</summary>
        static RectTransform[] BuildWeather(RectTransform row)
        {
            var icons = new RectTransform[3];
            float x = LabelW;
            icons[0] = Icon(row, "WeatherSky", IntroBriefingIcons.PartlyCloudy, x, 24f, 0f); x += 24f + 13f;
            icons[1] = Icon(row, "WeatherTemp", IntroBriefingIcons.Thermometer, x, 19f, 0f); x += 19f + 3f;
            // the body font, like the other values (the display font sets everything in capitals: "3M/S")
            var temp = Text(row, $"{TemperatureC:0}°C", ValueSize, Color.white, UITheme.TextRole.Body, "WeatherTempValue");
            Place(temp.rectTransform, x, 52f); x += 52f + 8f;
            // the arrow points where the wind goes (from WindFromDeg, compass degrees): up = north, clockwise
            icons[2] = Icon(row, "WeatherWind", IntroBriefingIcons.Arrow, x, 19f, -(WindFromDeg + 180f)); x += 19f + 5f;
            var wind = Text(row, $"{Compass(WindFromDeg)} {WindSpeed:0}m/s", ValueSize, Color.white, UITheme.TextRole.Body, "WeatherWindValue");
            Place(wind.rectTransform, x, 96f);
            return icons;
        }

        static string Compass(float deg)
        {
            string[] pts = { "N", "NE", "E", "SE", "S", "SW", "W", "NW" };
            return pts[Mathf.RoundToInt(Mathf.Repeat(deg, 360f) / 45f) % 8];
        }

        static RectTransform Icon(RectTransform row, string name, Sprite sprite, float x, float size, float rotZ)
        {
            var rt = Rect(name, row);
            rt.anchorMin = rt.anchorMax = new Vector2(0f, .5f); rt.pivot = new Vector2(.5f, .5f);
            rt.sizeDelta = new Vector2(size, size); rt.anchoredPosition = new Vector2(x + size * .5f, 0f);
            rt.localRotation = Quaternion.Euler(0f, 0f, rotZ);
            var img = rt.gameObject.AddComponent<Image>(); img.sprite = sprite; img.raycastTarget = false; img.preserveAspect = true;
            return rt;
        }

        static TMP_Text Copy(RectTransform row, TMP_Text src, Color c, string name)
        {
            var t = Text(row, "", src.fontSize, c, UITheme.TextRole.Title, name);
            t.textWrappingMode = TextWrappingModes.NoWrap;
            Place(t.rectTransform, LabelW, Width - 32f - LabelW);
            t.alpha = 0f;
            return t;
        }

        static void Bracket(RectTransform parent, Vector2 corner)
        {
            // an L of two 2px strokes in the right-hand corner given (x = 1: right; y = 1: top, 0: bottom)
            var h = Rect("DecoBracketH", parent); var v = Rect("DecoBracketV", parent);
            foreach (var s in new[] { h, v }) { s.anchorMin = s.anchorMax = corner; s.pivot = corner; s.anchoredPosition = Vector2.zero; Img(s, new Color(1f, 1f, 1f, .55f)); }
            h.sizeDelta = new Vector2(18f, 2f); v.sizeDelta = new Vector2(2f, 18f);
        }

        static void Place(RectTransform rt, float x, float w)
        {
            rt.anchorMin = new Vector2(0f, 0f); rt.anchorMax = new Vector2(0f, 1f); rt.pivot = new Vector2(0f, .5f);
            rt.anchoredPosition = new Vector2(x, 0f); rt.sizeDelta = new Vector2(w, 0f);
        }

        static RectTransform Rect(string name, Transform parent)
        {
            var rt = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            return rt;
        }

        static void Stretch(RectTransform rt) { rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero; }

        static Image Img(RectTransform rt, Color c)
        {
            var i = rt.gameObject.AddComponent<Image>(); i.color = c; i.raycastTarget = false;
            return i;
        }

        static TMP_Text Text(RectTransform parent, string s, float size, Color c, UITheme.TextRole role, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var t = go.AddComponent<TextMeshProUGUI>();
            t.text = s; t.fontSize = size; t.color = c; t.alignment = TextAlignmentOptions.MidlineLeft; t.raycastTarget = false;
            UIFonts.Apply(t, role);
            return t;
        }
    }
}
