// Render Previews states for the intro's screen UI: the spoken line (IntroSubtitle) while it types on and once it is all shown, and
// the switch to the hologram (IntroHoloScreen) half way through its wipe. Both are built exactly as the intro builds them, on a stage
// canvas, and posed with ITimedEffect.Evaluate(t); the hologram picture is a stand-in gradient (the real one is a camera's).
using UnityEngine;
using Blindside.Cinematic;
using Blindside.UI;

namespace Blindside.UITools.EditorTools
{
    internal static class UIPreviewIntro
    {
        public const string SubtitleTyping = "intro_subtitle_typing", SubtitleFull = "intro_subtitle", HoloWipe = "intro_holo_wipe";
        public const string Briefing = "intro_briefing", BriefingIn = "intro_briefing_in", BriefingGlitch = "intro_briefing_glitch";
        public const string TitleWide = "intro_title_wide", TitleGather = "intro_title_gather";

        /// <summary>The OPERATION BLINDSIDE title (as the scene has it: BSTactical 48, centred at 60% height) `t` s into its gathering.</summary>
        public static void BuildTitle(UIPreview.Stage s, float t)
        {
            var canvas = s.NewCanvas("IntroTitle_Canvas", UITheme.CanvasKind.Menu, 0);
            var go = new GameObject("OperationTitle", typeof(RectTransform), typeof(TMPro.TextMeshProUGUI));
            var rt = (RectTransform)go.transform;
            rt.SetParent(canvas.transform, false);
            rt.anchorMin = rt.anchorMax = new Vector2(.5f, .6f); rt.sizeDelta = new Vector2(900f, 80f);
            var text = go.GetComponent<TMPro.TextMeshProUGUI>();
            text.text = "OPERATION BLINDSIDE"; text.fontSize = 48f; text.alignment = TMPro.TextAlignmentOptions.Center; text.color = Color.white;
            text.raycastTarget = false;
            UIFonts.Apply(text, UITheme.TextRole.Display);
            go.AddComponent<LintIgnoreClip>().reason = "the letters are flung past the box on purpose while they gather";
            var asm = go.AddComponent<IntroTitleAssemble>();
            asm.source = text; asm.enabled = false;
            Canvas.ForceUpdateCanvases();
            asm.Play();
            asm.Evaluate(t);
        }

        /// <summary>The mission briefing `t` seconds into its run, with the first line's subtitle under it (a fixed date, so the
        /// preview doesn't change from day to day).</summary>
        public static void BuildBriefing(UIPreview.Stage s, float t)
        {
            var canvas = s.NewCanvas("IntroBriefing_Canvas", UITheme.CanvasKind.Menu, 0);
            var b = IntroBriefing.Build(canvas.transform);
            b.enabled = false;
            b.Play(IntroBriefing.DefaultValues(new System.DateTime(2026, 11, 5, 10, 42, 0)), 5.05f);
            b.Evaluate(t);
            var sub = IntroSubtitle.Build(canvas.transform);
            sub.enabled = false;
            sub.Play("作戦「ブラインドサイド」を実施する", 5.6f);
            sub.Evaluate(Mathf.Min(t, .7f));
        }
        const string Line = "現在、民家にテロリストが侵入。民間人1名が人質に取られている";

        public static void BuildSubtitle(UIPreview.Stage s, float t)
        {
            var canvas = s.NewCanvas("IntroSubtitle_Canvas", UITheme.CanvasKind.Menu, 0);
            var sub = IntroSubtitle.Build(canvas.transform);
            sub.enabled = false;   // posed, not run
            sub.Play(Line, 4.9f);
            sub.Evaluate(t);
        }

        public static void BuildHoloWipe(UIPreview.Stage s)
        {
            var canvas = s.NewCanvas("IntroHolo_Canvas", UITheme.CanvasKind.Menu, 0);
            var screen = IntroHoloScreen.Build(canvas.transform, 0);
            screen.enabled = false;
            screen.Play(StandIn());
            Canvas.ForceUpdateCanvases();
            screen.Evaluate(IntroHoloScreen.WipeStart + IntroHoloScreen.WipeTime * .45f);
            var sub = IntroSubtitle.Build(canvas.transform);
            sub.enabled = false;
            sub.Play(Line, 4.9f);
            sub.Evaluate(.6f);
        }

        /// <summary>A dark blue field with a lighter band — enough to see the wipe's edge and the lines over it.</summary>
        static Texture2D StandIn()
        {
            var tex = new Texture2D(64, 36, TextureFormat.RGBA32, false) { name = "HoloStandIn", hideFlags = HideFlags.HideAndDontSave };
            for (int y = 0; y < 36; y++)
                for (int x = 0; x < 64; x++)
                {
                    float g = (x % 8 == 0 || y % 8 == 0) ? .35f : 0f;
                    tex.SetPixel(x, y, new Color(.02f + g * .3f, .08f + g * .6f, .2f + g, 1f));
                }
            tex.Apply();
            return tex;
        }
    }
}
