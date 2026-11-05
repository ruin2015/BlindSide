// The roster card (.rc, ui_spec "대원 카드"): 250×62 parallelogram (slant 14) built from the kit —
//   Body (TacticalPanel paper + SlantedGraphic 14, the raycast target; its Clip band = the operator-colour bar, 8 → 12 when selected)
//     Face        SlantedMask 62×62 (slant 14) at x 8: DecoBackdrop, Photo (UIGlitchImage, cover, top-pinned), RoleIcon chip 20×17
//     Name        English name, 18 display          Hp    "cur/max", 12 display, right
//     Bar         SegmentedBar card 6/2, height 7   Kana  katakana name, 12 body     Move  chip 11.5, right
//     EndChip     行動終了 (right 22, top 7)        KiaChip K.I.A
//     SelectLine  operator colour, 3 high at the bottom, drawn left → right
//     DecoHitFlash  foe "screen" flash over everything (KitTint)
// The card knows no game types: SetData takes plain values, and clicks / hovers come out as events.
// States and motion (ui_spec "모션"): hover +6 (.16s, not while selected) · selected +18 (.24s), ink background, white text, the move chip
// in the operator colour, the bottom line .45s, the face Select glitch .5s · ended: grayscale, brightness .72 (.3s) + 行動終了 · dead:
// grayscale, brightness .45, K.I.A, HP 0 · hit: shake −8 / 7 / −5 / 3 (.42s, steps), red flash .5s (face: UIGlitchImage Hit), ghost bar ·
// enter: from −46 and transparent in .5s, delayed index × 60ms.
// Every state is a tween on one card clock: a setter starts it from the current pose at the current clock, Evaluate(t) poses
// everything at clock t, and Update only advances the clock with unscaled time. A preview sets states in edit mode (the clock stays 0)
// and calls Evaluate(t). Translucent mockup colours are turned into the opaque sRGB colour they give over the card, so the Linear colour
// space shows them as the mockup does. No allocation per frame (texts change only in SetData / Hit / SetDead).
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Blindside.UI
{
    [AddComponentMenu("Blindside/UI/Selectable Card")]
    public class SelectableCard : MonoBehaviour, ITimedEffect, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
    {
        public const float Width = 250f, Height = 62f, FaceSize = 62f, BandNormal = 8f, BandSelected = 12f;
        public const float HoverShift = 6f, SelectShift = 18f, EnterShift = 46f;
        public const float HoverTime = .16f, SelectTime = .24f, LineTime = .45f, ToneTime = .3f, SelectGlitch = .5f;
        public const float HitShake = .42f, HitFlash = .5f, HitFlashAlpha = .85f;
        public const float EndedBrightness = .72f, DeadBrightness = .45f, LineHeight = 3f;
        const float InfoX = 80f, InfoRight = 232f, Never = -100000f;

        [Header("Parts")]
        public RectTransform body;
        public TacticalPanel panel;
        public SlantedMask face;
        public UIGlitchImage photo;
        public AspectRatioFitter photoFit;
        public Image faceBack;
        public Chip roleIcon, move, endChip, kiaChip;
        public TMP_Text nameText, hpText, kanaText;
        public SegmentedBar bar;
        public TacticalPanel line;
        public KitTint flash;
        public CanvasGroup group;

        [Header("Events")]
        public UnityEvent onClick = new UnityEvent();
        public UnityEvent<bool> onHover = new UnityEvent<bool>();

        // ---- data -------------------------------------------------------------------------------------------------------------
        Color opColor = Color.white;
        int hp = 1, maxHp = 1;
        bool hovered, selected, ended, dead;

        public bool Hovered => hovered;
        public bool Selected => selected;
        public bool Ended => ended;
        public bool Dead => dead;
        public int Hp => hp;

        // ---- tweens on the card clock ----------------------------------------------------------------------------------------
        struct Tween
        {
            public float from, to, start, dur;
            public float At(float t) => Mathf.LerpUnclamped(from, to, UIEase.Tactical(dur > 0f ? (t - start) / dur : 1f));
            public float End => start + dur;
            public void Settle(float value) { from = to = value; start = 0f; }
        }

        Tween hoverT, selectT, lineT, grayT, brightT = new Tween { from = 1f, to = 1f }, enterT = new Tween { from = 1f, to = 1f };
        float hitStart = Never, glitchStart = Never, clock;
        bool running;

        // ---- data API ---------------------------------------------------------------------------------------------------------
        /// <summary>Everything the card shows. `move` is the chip text as it should read (e.g. "移動 7.0m"), `roleIcon` one display
        /// glyph (▣ ◎ ✹ ✚ ⬢).</summary>
        public void SetData(Sprite faceSprite, string nameEn, string nameKana, Color color, int currentHp, int maxHpValue, string moveText, string roleIconGlyph)
        {
            opColor = color;
            maxHp = Mathf.Max(1, maxHpValue);
            hp = Mathf.Clamp(currentHp, 0, maxHp);
            if (photo != null)
            {
                photo.SetSprite(faceSprite);
                photo.color = faceSprite != null ? Color.white : new Color(1f, 1f, 1f, 0f);
                if (photoFit != null && faceSprite != null) photoFit.aspectRatio = faceSprite.rect.width / Mathf.Max(1f, faceSprite.rect.height);
            }
            if (nameText != null) nameText.text = nameEn;
            if (kanaText != null) kanaText.text = nameKana;
            if (roleIcon != null && roleIcon.label != null) roleIcon.label.text = roleIconGlyph;
            moveShown = null;
            SetMove(moveText);
            if (bar != null) bar.SetImmediate(dead ? 0f : (float)hp / maxHp);
            UpdateHpText();
            Evaluate(clock);
        }

        string moveShown;

        /// <summary>The move chip's text (e.g. "移動 7.0m"); empty hides the chip. Re-lays out only when the text changes.</summary>
        public void SetMove(string moveText)
        {
            if (move == null || moveText == moveShown) return;
            moveShown = moveText;
            bool on = !string.IsNullOrEmpty(moveText);
            move.gameObject.SetActive(on);
            float w = 0f;
            if (on)
            {
                move.SetText(moveText);
                var mr = (RectTransform)move.transform;
                w = mr.sizeDelta.x;
                float h = mr.sizeDelta.y;
                UILayout.Place(mr, InfoRight - w, 44f + (15f - h) * .5f, w, h);
            }
            if (kanaText != null)
            {
                float kw = Mathf.Max(10f, InfoRight - (on ? w + 6f : 0f) - InfoX);
                UILayout.Place(kanaText.rectTransform, InfoX, 44f, kw, 15f);
                FitKana(kw);
            }
        }

        /// <summary>The katakana name in the width the move chip leaves: tightened to −0.02em first, then smaller down to 11 (only when the
        /// name or the chip changes).</summary>
        void FitKana(float width)
        {
            kanaText.characterSpacing = 0f;
            kanaText.fontSize = KanaSize;
            if (string.IsNullOrEmpty(kanaText.text) || kanaText.GetPreferredValues(kanaText.text).x <= width) return;
            kanaText.characterSpacing = -2f;
            float w = kanaText.GetPreferredValues(kanaText.text).x;
            if (w > width) kanaText.fontSize = Mathf.Max(11f, Mathf.Floor(KanaSize * width / w * 4f) / 4f);
        }

        const float KanaSize = 12f;

        /// <summary>HP from outside: a drop plays Hit (shake, flash, ghost bar); a rise or a new max just sets the bar.</summary>
        public void SetHp(int currentHp, int maxHpValue)
        {
            int max = Mathf.Max(1, maxHpValue), cur = Mathf.Clamp(currentHp, 0, max);
            if (max == maxHp && cur == hp) return;
            if (max == maxHp && cur < hp && !dead) { Hit(cur); return; }
            maxHp = max; hp = cur;
            if (bar != null) bar.SetImmediate(dead ? 0f : (float)hp / maxHp);
            UpdateHpText();
        }

        /// <summary>Everything that changes during a match, in one call (each part does nothing when unchanged).</summary>
        public void SetState(int currentHp, int maxHpValue, string moveText, bool isSelected, bool isEnded, bool isDead)
        {
            SetHp(currentHp, maxHpValue);                       // a lethal hit still shakes and flashes, then the card goes dark
            SetDead(isDead);
            SetMove(moveText);
            SetEnded(isEnded);
            SetSelected(isSelected);
        }

        void UpdateHpText()
        {
            if (hpText != null) hpText.text = (dead ? 0 : hp) + "/" + maxHp;
        }

        // ---- states -----------------------------------------------------------------------------------------------------------
        public void SetHovered(bool on)
        {
            if (on == hovered) return;
            hovered = on;
            Begin(ref hoverT, on ? 1f : 0f, HoverTime);
        }

        public void SetSelected(bool on)
        {
            if (on == selected) return;
            selected = on;
            Rebase();
            Retarget(ref selectT, on ? 1f : 0f, SelectTime);
            if (on) { lineT.from = 0f; lineT.to = 1f; lineT.start = clock; lineT.dur = LineTime; glitchStart = clock; }
            else lineT.Settle(0f);                               // CSS: the ::after line exists only while .sel
            Run();
        }

        public void SetEnded(bool on)
        {
            if (on == ended) return;
            ended = on;
            ApplyTone();
        }

        /// <summary>Dead: darker than ended, K.I.A, HP 0 (the bar empties at once).</summary>
        public void SetDead(bool on)
        {
            if (on == dead) return;
            dead = on;
            if (bar != null) bar.SetImmediate(on ? 0f : (float)hp / maxHp);
            UpdateHpText();
            ApplyTone();
        }

        /// <summary>Takes a hit down to `newHp`: shake, red flash, the face Hit glitch and the bar's ghost.</summary>
        public void Hit(int newHp)
        {
            Rebase();
            float from = (float)hp / maxHp;
            hp = Mathf.Clamp(newHp, 0, maxHp);
            if (bar != null) bar.Pose(from, dead ? 0f : (float)hp / maxHp);
            UpdateHpText();
            hitStart = clock;
            Run();
        }

        /// <summary>The entrance (rcIn): slides in from −46 and fades in over .5s after index × 60ms.</summary>
        public void PlayEnter(int index)
        {
            Rebase();
            var m = UITheme.Current.motion;
            float stagger = m.cardInStagger > 0f ? m.cardInStagger : .06f, dur = m.cardIn > 0f ? m.cardIn : .5f;
            enterT.from = 0f; enterT.to = 1f; enterT.start = clock + Mathf.Max(0, index) * stagger; enterT.dur = dur;
            Run();
        }

        void ApplyTone()
        {
            Rebase();
            bool down = ended || dead;
            Retarget(ref grayT, down ? 1f : 0f, ToneTime);
            Retarget(ref brightT, dead ? DeadBrightness : ended ? EndedBrightness : 1f, ToneTime);
            if (endChip != null) endChip.gameObject.SetActive(ended && !dead);
            if (kiaChip != null) kiaChip.gameObject.SetActive(dead);
            Run();
        }

        void Begin(ref Tween tw, float to, float dur)
        {
            Rebase();
            Retarget(ref tw, to, dur);
            Run();
        }

        void Retarget(ref Tween tw, float to, float dur)
        {
            float now = tw.At(clock);
            tw.from = now; tw.to = to; tw.start = clock; tw.dur = dur;
        }

        // an idle card restarts its clock at 0 with every tween settled (keeps the numbers small; previews pose from t = 0)
        void Rebase()
        {
            if (running || clock == 0f) return;              // clock 0: nothing has elapsed (and a preview keeps every pose)
            float c = clock;
            hoverT.Settle(hoverT.At(c)); selectT.Settle(selectT.At(c)); lineT.Settle(lineT.At(c));
            grayT.Settle(grayT.At(c)); brightT.Settle(brightT.At(c)); enterT.Settle(enterT.At(c));
            hitStart = glitchStart = Never;
            if (bar != null && c != 0f) bar.Pose(bar.Fill, bar.Fill);
            clock = 0f;
        }

        void Run()
        {
            if (Application.isPlaying && isActiveAndEnabled) running = true;
            Evaluate(clock);
        }

        // ---- ITimedEffect -----------------------------------------------------------------------------------------------------
        public float Duration
        {
            get
            {
                float d = Mathf.Max(Mathf.Max(hoverT.End, selectT.End), Mathf.Max(lineT.End, grayT.End));   // two-argument Max: no params array
                d = Mathf.Max(d, Mathf.Max(brightT.End, enterT.End));
                d = Mathf.Max(d, glitchStart + SelectGlitch);
                if (bar != null) d = Mathf.Max(d, hitStart + Mathf.Max(HitFlash, bar.Duration));
                return Mathf.Max(0f, d);
            }
        }

        public void Evaluate(float t)
        {
            var theme = UITheme.Current;
            float h = hoverT.At(t), s = selectT.At(t), l = Mathf.Clamp01(lineT.At(t)), g = grayT.At(t), br = brightT.At(t);
            float e = Mathf.Clamp01(enterT.At(t));

            // position: hover (not while selected), selection, hit shake (steps), entrance
            float ht = t - hitStart, q = ht / HitShake;
            float shake = UIMotionSettings.reduceMotion || q < 0f ? 0f : q < .25f ? -8f : q < .5f ? 7f : q < .75f ? -5f : q < 1f ? 3f : 0f;
            if (body != null) body.anchoredPosition = new Vector2(HoverShift * h * (1f - s) + SelectShift * s + shake - EnterShift * (1f - e), 0f);
            if (group != null) group.alpha = e;

            // colours (translucent mockup colours as the opaque sRGB colour over the card)
            Color ink = theme.ink, paper = theme.paper, white = Color.white;
            Color bg = Color.Lerp(paper, ink, s);
            Color nameC = Color.Lerp(ink, white, s);
            Color hpC = Color.Lerp(theme.dim, Color.Lerp(ink, white, .7f), s);
            Color kanaC = Color.Lerp(theme.dim, Color.Lerp(ink, white, .75f), s);
            Color moveBg = Color.Lerp(ink, opColor, s), moveFg = Color.Lerp(white, ink, s);
            Color empty = Color.Lerp(Color.Lerp(paper, Color.black, .16f), Color.Lerp(ink, white, .14f), s);

            Color bgT = Tone(bg, g, br);
            if (panel != null)
            {
                panel.color = bgT;
                panel.SetBandColor(Tone(opColor, g, br));
                float bw = Mathf.Lerp(BandNormal, BandSelected, s);
                if (panel.bandWidth != bw) { panel.bandWidth = bw; panel.SetVerticesDirty(); }
            }
            if (nameText != null) nameText.color = Tone(nameC, g, br);
            if (hpText != null) hpText.color = Tone(hpC, g, br);
            if (kanaText != null) kanaText.color = Tone(kanaC, g, br);
            if (move != null) { move.box.color = Tone(moveBg, g, br); move.label.color = Tone(moveFg, g, br); }
            if (roleIcon != null) { roleIcon.box.color = Tone(opColor, g, br); roleIcon.label.color = Tone(ink, g, br); }
            if (faceBack != null) faceBack.color = Tone(HudKit.PaperGrad2, g, br);
            if (line != null)
            {
                line.color = Tone(opColor, g, br);
                var ls = line.rectTransform.localScale;
                if (ls.x != l) line.rectTransform.localScale = new Vector3(l, 1f, 1f);
            }
            if (bar != null)
            {
                Color f = Tone(opColor, g, br), gh = Tone(theme.foe, g, br), em = Tone(empty, g, br);
                if (bar.color != f || bar.ghostColor != gh || bar.emptyColor != em) bar.SetColors(f, gh, em);
                bar.Evaluate(ht);
            }

            // face: the newer of Select / Hit, plus the lasting grayscale / brightness
            if (photo != null)
            {
                bool hitNewer = hitStart >= glitchStart && hitStart > Never;
                photo.preset = hitNewer ? UIGlitchImage.Preset.Hit : UIGlitchImage.Preset.Select;
                photo.SetTone(1f - g, br);
                photo.Evaluate(hitNewer ? ht : t - glitchStart);
            }

            // card-wide red flash (CSS ::before foe, mix-blend-mode screen, opacity .85 → 0 over .5s; five steps like the face)
            if (flash != null)
            {
                float f = ht >= 0f && ht < HitFlash ? Mathf.Max(0f, 1f - Mathf.Floor(ht / HitFlash * 5f) * .2f) : 0f;
                Color foe = theme.foe;
                flash.Set(f > 0f ? new Color(foe.r, foe.g, foe.b, HitFlashAlpha * f) : Color.clear, bgT);
            }
        }

        /// <summary>CSS filter: grayscale(g) brightness(b), in sRGB values.</summary>
        static Color Tone(Color c, float gray, float bright)
        {
            if (gray <= 0f && bright == 1f) return c;
            float lum = .2126f * c.r + .7152f * c.g + .0722f * c.b;
            return new Color(Mathf.Lerp(c.r, lum, gray) * bright, Mathf.Lerp(c.g, lum, gray) * bright, Mathf.Lerp(c.b, lum, gray) * bright, c.a);
        }

        void Update()
        {
            if (!running) return;
            clock += Time.unscaledDeltaTime;
            Evaluate(clock);
            if (clock >= Duration) running = false;
        }

        // ---- pointer ----------------------------------------------------------------------------------------------------------
        public void OnPointerEnter(PointerEventData e) { SetHovered(true); onHover.Invoke(true); }
        public void OnPointerExit(PointerEventData e) { SetHovered(false); onHover.Invoke(false); }
        public void OnPointerClick(PointerEventData e) { if (e.button == PointerEventData.InputButton.Left) onClick.Invoke(); }

        // ---- build ------------------------------------------------------------------------------------------------------------
        /// <summary>A card `name` under `parent` (place it with UILayout.Place, 250×62). Fill it with SetData.</summary>
        public static SelectableCard Create(Transform parent, string name)
        {
            var theme = UITheme.Current;
            var root = HudKit.NewRect(name, parent);
            root.sizeDelta = new Vector2(Width, Height);
            var group = root.gameObject.AddComponent<CanvasGroup>();

            var bodyRt = HudKit.NewRect("Body", root);
            HudKit.Stretch(bodyRt);
            var panel = bodyRt.gameObject.AddComponent<TacticalPanel>();
            panel.SetPreset(KitColor.Paper);
            panel.raycastTarget = true;
            panel.SetBand(TacticalPanel.Band.Clip, BandNormal, KitColor.Custom, Color.white);
            bodyRt.gameObject.AddComponent<SlantedGraphic>().Set(SlantedGraphic.Size.CardMin);

            // face: the SlantedMask frame with a glitch photo instead of its RawImage
            var face = SlantedMask.Create(bodyRt, "Face", new Vector2(FaceSize, FaceSize), SlantedGraphic.Size.CardMin);
            UILayout.Place((RectTransform)face.transform, BandNormal, 0f, FaceSize, FaceSize);
            HudKit.DestroySafe(face.photo.gameObject);
            face.photo = null; face.fitter = null;
            var faceBack = face.transform.Find("DecoBackdrop").GetComponent<Image>();
            var pr = HudKit.NewRect("Photo", face.transform);
            pr.anchorMin = Vector2.zero; pr.anchorMax = Vector2.one;
            pr.pivot = new Vector2(.5f, 1f);
            pr.offsetMin = pr.offsetMax = Vector2.zero;
            var photo = pr.gameObject.AddComponent<UIGlitchImage>();
            photo.raycastTarget = false;
            photo.preset = UIGlitchImage.Preset.Select;
            photo.color = new Color(1f, 1f, 1f, 0f);
            var fit = pr.gameObject.AddComponent<AspectRatioFitter>();
            fit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            fit.aspectRatio = 1f;
            pr.gameObject.AddComponent<LintIgnoreClip>().reason = "face scaled to cover the frame, cropped at the bottom";

            var icon = Chip.Create(face.transform, "RoleIcon", "", KitColor.Custom, KitColor.Ink, 11f, 0f);
            icon.padX = 0f; icon.padY = 0f;
            UILayout.Place((RectTransform)icon.transform, 3f, FaceSize - 17f, 20f, 17f);
            icon.label.rectTransform.offsetMin = icon.label.rectTransform.offsetMax = Vector2.zero;
            UIFonts.Apply(icon.label, UITheme.TextRole.Display);              // the icon glyphs exist only in BSTactical

            // info
            var nm = HudKit.AddText(bodyRt, "", 18, theme.ink, true, TextAlignmentOptions.BottomLeft, "Name", UITheme.TextRole.Display);
            nm.characterSpacing = 6f;
            UILayout.Place(nm.rectTransform, InfoX, 7f, 98f, 20f);
            var hpT = HudKit.AddText(bodyRt, "", 12, theme.dim, true, TextAlignmentOptions.BottomRight, "Hp", UITheme.TextRole.Display);
            hpT.characterSpacing = 2f;
            UILayout.Place(hpT.rectTransform, 180f, 11f, InfoRight - 180f, 16f);

            var barRt = HudKit.NewRect("Bar", bodyRt);
            UILayout.Place(barRt, InfoX, 33f, InfoRight - InfoX, 7f);
            var bar = barRt.gameObject.AddComponent<SegmentedBar>();
            bar.style = SegmentedBar.Style.Card;
            bar.raycastTarget = false;

            var kana = HudKit.AddText(bodyRt, "", 12, theme.dim, true, TextAlignmentOptions.MidlineLeft, "Kana", UITheme.TextRole.Body);
            UILayout.Place(kana.rectTransform, InfoX, 44f, 80f, 15f);
            var mv = Chip.Create(bodyRt, "Move", "", KitColor.Ink, KitColor.White, 11.5f, 0f);
            mv.padX = 7f; mv.padY = 0f;

            var end = Chip.Create(bodyRt, "EndChip", "行動終了", KitColor.Paper, KitColor.Ink, 11f, 0f);
            end.padX = 6f; end.padY = 1f; end.Fit();
            var er = (RectTransform)end.transform;
            UILayout.Place(er, Width - 22f - er.sizeDelta.x, 7f, er.sizeDelta.x, er.sizeDelta.y);
            end.gameObject.AddComponent<LintIgnoreOverlap>().reason = "covers the HP number like the mockup's .rc.end .info::after";
            end.gameObject.SetActive(false);
            var kia = Chip.Create(bodyRt, "KiaChip", "K.I.A", KitColor.Foe, KitColor.White, 11f, 0f);
            kia.padX = 6f; kia.padY = 1f; kia.Fit();
            var kr = (RectTransform)kia.transform;
            UILayout.Place(kr, Width - 22f - kr.sizeDelta.x, 7f, kr.sizeDelta.x, kr.sizeDelta.y);
            kia.gameObject.AddComponent<LintIgnoreOverlap>().reason = "covers the HP number (dead)";
            kia.gameObject.SetActive(false);

            // the selection line (.rc.sel::after) inside the card's slant: 3 high at the bottom, scaled from the left
            float lineSlant = 14f * LineHeight / Height;
            var line = TacticalPanel.Create(bodyRt, "SelectLine", KitColor.Custom, SlantedGraphic.Size.Custom, lineSlant);
            line.color = Color.white;
            UILayout.Place(line.rectTransform, 0f, Height - LineHeight, Width - theme.slantCardMin + lineSlant, LineHeight);
            line.rectTransform.localScale = new Vector3(0f, 1f, 1f);

            var flRt = HudKit.NewRect("DecoHitFlash", bodyRt);
            HudKit.Stretch(flRt);
            var fl = flRt.gameObject.AddComponent<KitTint>();
            fl.raycastTarget = false;
            fl.Screen = true;
            fl.MatchShape(bodyRt.GetComponent<SlantedGraphic>());

            var card = root.gameObject.AddComponent<SelectableCard>();
            card.body = bodyRt; card.panel = panel; card.face = face; card.photo = photo; card.photoFit = fit; card.faceBack = faceBack;
            card.roleIcon = icon; card.move = mv; card.endChip = end; card.kiaChip = kia;
            card.nameText = nm; card.hpText = hpT; card.kanaText = kana; card.bar = bar; card.line = line; card.flash = fl; card.group = group;
            card.Evaluate(0f);
            return card;
        }
    }
}
