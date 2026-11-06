// A talk at the HQ (会話: CRUSHER / SERUM / IGNIS / BASTION): a box across the bottom of the screen, the radio's look —
//   Box     1000 × 176, 40 from the bottom, centred: ink, 26 cut top right, DecoShadow; a 6 band in the operator's colour on the left
//   Face    136 × 136 (OperatorArt.Face) on a dark slot, 20 in
//   Name    the codename 22 display + kana · role 12 (dim)
//   Line    18 body, white, wrapped; typed in at 40 characters a second (maxVisibleCharacters — no new strings per frame)
//   Next    "F / クリック  次へ" 11 (dim), bottom right; the last line says 閉じる
// F / Enter / Space / a click: the whole line at once, then the next; after the last the box closes. ESC closes. Entrance .25s
// (slides up 24 and fades in). ITimedEffect.Evaluate(t).
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Blindside.UI
{
    public class HubTalkPanel : MonoBehaviour, ITimedEffect, IPointerClickHandler
    {
        public const float Width = 1000f, Height = 176f, Bottom = 40f, FaceS = 136f, Cut = 26f, InTime = .25f, CharsPerSecond = 40f;

        RectTransform pop;
        CanvasGroup group;
        Image face, band;
        TMP_Text nameText, kanaText, line, next;
        string[] lines;
        int index;
        float clock, typed;
        bool entering;

        public bool IsOpen => gameObject.activeSelf;
        public System.Action Closed;

        public static HubTalkPanel Build(Transform parent)
        {
            var theme = UITheme.Current;
            var root = HudKit.NewRect("HubTalk", parent);
            HudKit.Stretch(root);
            var p = root.gameObject.AddComponent<HubTalkPanel>();
            var catcher = HudKit.AddImage(root, new Color(0f, 0f, 0f, 0f), raycast: true);   // a click anywhere advances
            catcher.name = "Catcher";

            p.pop = HudKit.NewRect("Box", root);
            p.pop.anchorMin = p.pop.anchorMax = p.pop.pivot = new Vector2(.5f, 0f);
            p.pop.anchoredPosition = new Vector2(0f, Bottom);
            p.pop.sizeDelta = new Vector2(Width, Height);
            p.group = p.pop.gameObject.AddComponent<CanvasGroup>();
            var bg = HudKit.AddImage(p.pop, new Color(theme.ink.r, theme.ink.g, theme.ink.b, .94f));
            p.pop.gameObject.AddComponent<CutCornerGraphic>().Set(Cut, CutCornerGraphic.Corners.TopRight);
            HudKit.Stretch(KitShadow.Behind(bg.rectTransform, new Color(.03f, .04f, .06f), .6f, 14f, 30f).rectTransform);

            var b = HudKit.NewRect("DecoBand", p.pop);
            UILayout.Place(b, 0f, 0f, 6f, Height);
            p.band = HudKit.AddImage(b, theme.blue);

            var slot = HudKit.NewRect("Slot", p.pop);
            UILayout.Place(slot, 20f, (Height - FaceS) * .5f, FaceS, FaceS);
            HudKit.AddImage(slot, theme.ink3);
            slot.gameObject.AddComponent<RectMask2D>();
            var f = HudKit.NewRect("Face", slot);
            HudKit.Stretch(f);
            p.face = f.gameObject.AddComponent<Image>();
            p.face.preserveAspect = true; p.face.raycastTarget = false;

            float x = 20f + FaceS + 24f, w = Width - x - 28f;
            p.nameText = HudKit.AddText(p.pop, "", 22f, Color.white, true, TextAlignmentOptions.TopLeft, "Name", UITheme.TextRole.Display);
            p.nameText.textWrappingMode = TextWrappingModes.NoWrap;
            UILayout.Place(p.nameText.rectTransform, x, 18f, 220f, 28f);
            p.kanaText = HudKit.AddText(p.pop, "", 12f, Color.Lerp(theme.dim, Color.white, .3f), false, TextAlignmentOptions.BottomLeft, "Kana", UITheme.TextRole.Body);
            p.kanaText.textWrappingMode = TextWrappingModes.NoWrap;
            UILayout.Place(p.kanaText.rectTransform, x + 230f, 18f, 300f, 26f);
            p.line = HudKit.AddText(p.pop, "", 18f, Color.white, false, TextAlignmentOptions.TopLeft, "Line", UITheme.TextRole.Body);
            p.line.textWrappingMode = TextWrappingModes.Normal;
            UILayout.LineHeight(p.line, 1.5f);
            UILayout.Place(p.line.rectTransform, x, 58f, w, 84f);
            p.next = HudKit.AddText(p.pop, "", 11f, Color.Lerp(theme.dim, Color.white, .25f), true, TextAlignmentOptions.BottomRight, "Next", UITheme.TextRole.Body);
            p.next.textWrappingMode = TextWrappingModes.NoWrap;
            UILayout.Place(p.next.rectTransform, Width - 260f, Height - 34f, 236f, 20f);

            root.gameObject.SetActive(false);
            return p;
        }

        public void Open(Blindside.Data.OperatorData op, string[] say)
        {
            var theme = UITheme.Current;
            lines = say != null && say.Length > 0 ? say : new[] { "……" };
            index = 0;
            face.sprite = op != null ? OperatorArt.Face(op.key) : null;
            face.enabled = face.sprite != null;
            band.color = op != null ? op.color : theme.blue;
            nameText.SetText(op != null ? op.DisplayName : "");
            kanaText.SetText(op != null ? op.kr + " · " + op.role : "");
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            ShowLine();
            clock = 0f;
            entering = Application.isPlaying;
            Evaluate(entering ? 0f : Duration);
            if (!Application.isPlaying) SkipTyping();
        }

        public void Close()
        {
            if (!gameObject.activeSelf) return;
            gameObject.SetActive(false);
            Closed?.Invoke();
        }

        void ShowLine()
        {
            line.SetText(lines[index]);
            line.maxVisibleCharacters = 0;
            typed = 0f;
            next.SetText(index < lines.Length - 1 ? "F / クリック  次へ" : "F / クリック  閉じる");
        }

        /// <summary>Previews: the current line typed in full.</summary>
        public void SkipTyping() { typed = 9999f; line.maxVisibleCharacters = 99999; }

        bool Typing => line.maxVisibleCharacters < lines[index].Length;

        void Advance()
        {
            if (Typing) { SkipTyping(); return; }
            index++;
            if (index >= lines.Length) { Close(); return; }
            ShowLine();
        }

        public void OnPointerClick(PointerEventData e) => Advance();

        // ---- ITimedEffect -----------------------------------------------------------------------------------------------------
        public float Duration => InTime;

        public void Evaluate(float t)
        {
            float p = UIEase.Tactical(Mathf.Clamp01(t / InTime));
            float y = Bottom - (UIMotionSettings.reduceMotion ? 0f : (1f - p) * 24f);
            if (pop.anchoredPosition.y != y) pop.anchoredPosition = new Vector2(0f, y);
            if (group.alpha != p) group.alpha = p;
        }

        void Update()
        {
            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.escapeKey.wasPressedThisFrame && !EscapeGate.Consumed) { EscapeGate.Consume(); Close(); return; }
                if (clock > .05f && (kb.fKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame)) Advance();
                if (!gameObject.activeSelf) return;
            }
            clock += Time.unscaledDeltaTime;
            if (entering) { Evaluate(clock); if (clock >= InTime) entering = false; }
            if (Typing)
            {
                typed += Time.unscaledDeltaTime * CharsPerSecond;
                int n = Mathf.Min(lines[index].Length, (int)typed);
                if (line.maxVisibleCharacters != n) line.maxVisibleCharacters = n;
            }
        }
    }
}
