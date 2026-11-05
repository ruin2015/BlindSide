// The intro's "OPERATION BLINDSIDE" title coming together (replaces UIGlitchText's plain burst on it): at first the letters are flung
// wide to the sides, stretched, flickering and split into red / blue copies; in glitchy steps they pull in until they sit as the word,
// with one last hard twitch as they lock. One run of the clock (ITimedEffect.Evaluate(t), unscaled; Play starts it). It works on the
// text's own mesh — every letter's quad moved from its laid-out place — so nothing is re-laid out and nothing allocates per frame
// (the laid-out vertices are copied once, in Play).
//   0 – Gather       spread Spread× the letters' distance from the centre → 1, letter width Stretch → 1, in steps of 1/Fps
//   Lock …           a short twitch, then the copies fade by Settle
using UnityEngine;
using TMPro;
using Blindside.UI;

namespace Blindside.Cinematic
{
    public class IntroTitleAssemble : MonoBehaviour, ITimedEffect
    {
        public const float Gather = .85f, LockAt = .87f, LockTime = .07f, Settle = 1.15f, Fps = 30f;
        public const float Spread = 2.7f, Stretch = 2.6f, Jitter = 40f, SplitMax = 22f;

        public TMP_Text source;
        TMP_Text red, blue;
        Vector3[][] baseVerts = new Vector3[0][];
        float clock, posed = -1f; bool running, listening;

        public float Duration => Settle;

        public void Play()
        {
            Setup();
            clock = 0f; running = true;
            Evaluate(0f);
        }

        void Setup()
        {
            if (source == null) source = GetComponent<TMP_Text>();
            if (red == null) red = MakeCopy("DecoTitleSplitRed", UIPalette.GlitchRed);
            if (blue == null) blue = MakeCopy("DecoTitleSplitBlue", UIPalette.GlitchBlue);
            foreach (var t in new[] { source, red, blue }) { t.text = source.text; t.ForceMeshUpdate(); }
            CaptureBase();
            // TMP rebuilds a text's mesh whenever its layout or the canvas scale changes: pose it again on top of the new one
            if (!listening) { TMPro_EventManager.TEXT_CHANGED_EVENT.Add(OnTextChanged); listening = true; }
        }

        void CaptureBase()
        {
            var ti = source.textInfo;
            if (baseVerts.Length != ti.meshInfo.Length) baseVerts = new Vector3[ti.meshInfo.Length][];
            for (int m = 0; m < ti.meshInfo.Length; m++) baseVerts[m] = (Vector3[])ti.meshInfo[m].vertices.Clone();
        }

        void OnTextChanged(Object o)
        {
            if (posed < 0f || (o != source && o != red && o != blue)) return;
            if (o == source) CaptureBase();
            Evaluate(posed);
        }

        void OnDestroy() { if (listening) TMPro_EventManager.TEXT_CHANGED_EVENT.Remove(OnTextChanged); }

        TMP_Text MakeCopy(string name, Color c)
        {
            // a sibling just behind the title, on the same rect (a child would draw over it)
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            var rt = (RectTransform)go.transform;
            var srt = source.rectTransform;
            rt.SetParent(srt.parent, false);
            rt.anchorMin = srt.anchorMin; rt.anchorMax = srt.anchorMax; rt.pivot = srt.pivot;
            rt.anchoredPosition = srt.anchoredPosition; rt.sizeDelta = srt.sizeDelta;
            rt.SetSiblingIndex(srt.GetSiblingIndex());
            var t = go.GetComponent<TextMeshProUGUI>();
            t.font = source.font; t.fontSize = source.fontSize; t.fontStyle = source.fontStyle; t.alignment = source.alignment;
            t.characterSpacing = source.characterSpacing; t.textWrappingMode = source.textWrappingMode; t.overflowMode = TextOverflowModes.Overflow;
            t.color = c; t.raycastTarget = false;
            go.AddComponent<LintIgnoreClip>().reason = "the red / blue copy is offset on purpose";
            return t;
        }

        void Update()
        {
            if (!running) return;
            clock += Time.unscaledDeltaTime;
            Evaluate(clock);
            if (clock >= Settle) running = false;
        }

        static float Hash(int a, int b)
        {
            uint h = (uint)(a * 374761393 + b * 668265263);
            h = (h ^ (h >> 13)) * 1274126177u;
            return (h ^ (h >> 16)) / 4294967295f;
        }

        public void Evaluate(float t)
        {
            if (source == null || baseVerts.Length == 0) return;
            posed = t;
            int step = Mathf.FloorToInt(Mathf.Max(0f, t) * Fps);
            float tq = step / Fps;                                   // the glitch moves in steps, not smoothly
            // 1 = flung wide … 0 = in place: holds wide a moment, then pulls in harder and harder, snapping shut at the end
            float g = Mathf.Clamp01(tq / Gather);
            float s = 1f - g * g * (3f - 2f * g);
            bool lockTwitch = t >= LockAt && t < LockAt + LockTime;
            float split = s > .001f ? 3f + SplitMax * s * (.5f + .5f * Hash(step, 5)) : lockTwitch ? 7f : 0f;
            float copyA = s > .001f ? .85f : lockTwitch ? .9f : Mathf.Clamp01(1f - (t - LockAt - LockTime) / (Settle - LockAt - LockTime)) * .6f;

            Pose(source, 0f, s, step, lockTwitch, 1f);
            Pose(red, -split, s, step + 101, lockTwitch, copyA);
            Pose(blue, split, s, step + 211, lockTwitch, copyA);
        }

        void Pose(TMP_Text text, float shift, float s, int step, bool twitch, float alpha)
        {
            var ti = text.textInfo;
            if (ti.characterCount == 0) return;
            float cx = 0f; int n = 0;
            for (int i = 0; i < ti.characterCount; i++) { var ci = ti.characterInfo[i]; if (!ci.isVisible) continue; cx += (ci.bottomLeft.x + ci.topRight.x) * .5f; n++; }
            if (n == 0) return;
            cx /= n;
            for (int i = 0; i < ti.characterCount; i++)
            {
                var ci = ti.characterInfo[i];
                if (!ci.isVisible) continue;
                int m = ci.materialReferenceIndex, v = ci.vertexIndex;
                if (m >= baseVerts.Length) continue;
                var src = baseVerts[m]; var dst = ti.meshInfo[m].vertices;
                float mid = (src[v].x + src[v + 2].x) * .5f;
                float jx = (Hash(i, step) - .5f) * Jitter * s, jy = (Hash(i + 57, step) - .5f) * 14f * s;
                if (twitch) jx += (Hash(i, 999) - .5f) * 10f;
                float dx = (mid - cx) * (Spread - 1f) * s + jx + shift;
                float wide = 1f + (Stretch - 1f) * s * (.6f + .4f * Hash(i + 13, step));
                for (int k = 0; k < 4; k++)
                {
                    var p = src[v + k];
                    dst[v + k] = new Vector3(mid + (p.x - mid) * wide + dx, p.y + jy, p.z);
                }
                // flicker: while scattered, some letters drop out for a step
                byte a = (byte)(255f * alpha * (s > .001f && Hash(i + 7, step) < s * .35f ? .15f : 1f));
                var cols = ti.meshInfo[m].colors32;
                for (int k = 0; k < 4; k++) cols[v + k].a = a;
            }
            text.UpdateVertexData(TMP_VertexDataUpdateFlags.Vertices | TMP_VertexDataUpdateFlags.Colors32);
        }
    }
}
