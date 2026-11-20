// The battle's roster card (the user's design, replacing SelectableCard — now UI/Legacy): a parallelogram 300×68 (slant 14).
//   unselected  white; a slanted band in the operator colour at the left; the face (a cut-out in a frame at the card's slant)
//   selected    ink, with dark diagonal bands scrolling across it the whole time it stays selected; a wider colour band, a colour line
//               along the foot, the face growing towards the upper right; texts white, the status chip in the operator colour
//   Body
//     Panel           the white parallelogram (the click target)
//     DecoInk         ink over it, wiping in from the left on selection (.28s)
//     DecoStripes     the scrolling bands (RawImage, StripeTexture, its uvRect slid every frame while selected)
//     DecoBand        the operator-colour band at the left (10 → 16 wide when selected)
//     FaceMask        SlantedMask at the card's angle, from the card's foot to PopRoom over its top (the face never leaves it)
//       FaceRoot      the card's height, pivot bottom-left: on selection it rises 12 and grows ×1.12 towards the upper right, so the
//                     cut-out hair stands over the card (only the selected one; KitRoster opens PopOffset above it, moving the
//                     cards below down with it, so no two cards overlap)
//         Face        UIGlitchImage (Select / Hit glitches), cover, top-pinned
//     DecoFootLine    the operator-colour line along the foot (over the face), drawn left → right on selection (.45s)
//     Name            English name, Chakra Petch Bold Italic 20
//     Hp              "cur/max", Chakra Petch Bold Italic 14, right
//     Bar             SegmentedBar (6 / 3) sheared by a SlantedGraphic: slanted segments in the operator colour, the hit's ghost in red
//     Weapon          the gun's short name, Chakra Petch Bold Italic 15     Gun   its silhouette (GunIcons), tinted like the text
//     Status          a slanted chip: 行動中 / 待機 / 行動終了 / K.I.A / the HUD's own status (拘束中, 降下完了 …)
//     DecoHitFlash    the red screen flash of a hit
// States and motion: hover +6 (.16s) · selected +18 (.24s), ink wipe .28s, face ×1.12 towards the upper right (.3s), the foot line .45s, the bands
// scrolling · ended: grayscale, brightness .72 · dead: grayscale, brightness .45, K.I.A, HP 0 · hit: shake −8 / 7 / −5 / 3 (.42s,
// steps), red flash .5s, the bar's ghost · enter: from −46 and transparent in .5s, delayed index × 60ms.
// The card knows no game types (SetData / SetWeapon / SetState take plain values; clicks and hovers come out as events). Every state is a
// tween on one card clock: Evaluate(t) poses everything at t and Update only advances the clock with unscaled time (it keeps running
// while selected, for the bands). Texts change only in the setters; nothing is allocated per frame.
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Blindside.UI
{
    [AddComponentMenu("Blindside/UI/Tactical Roster Card")]
    public class TacticalRosterCard : MonoBehaviour, ITimedEffect, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
    {
        public const float Width = 300f, Height = 68f, Slant = 14f;
        public const float BandNormal = 10f, BandSelected = 16f, FootLine = 3f, FaceX = 12f, FaceW = 84f, FaceGrow = .12f;
        /// <summary>How far the selected face may stand over the card's top (FaceRise + its growth stay inside it).</summary>
        public const float PopRoom = 26f, FaceRise = 12f;

        /// <summary>The room the face takes over the card's top right now (0 … PopRoom, following the selection's face tween):
        /// KitRoster opens that much space above the card, so the cards move together and never overlap.</summary>
        public float PopOffset { get; private set; }
        public const float HoverShift = 6f, SelectShift = 18f, EnterShift = 46f;
        public const float HoverTime = .16f, SelectTime = .24f, WipeTime = .28f, FaceTime = .3f, LineTime = .45f, ToneTime = .3f, SelectGlitch = .5f;
        public const float HitShake = .42f, HitFlash = .5f, HitFlashAlpha = .85f;
        public const float EndedBrightness = .72f, DeadBrightness = .45f;
        public const float StripeSpeed = .35f;           // the bands' slide, texture widths per second
        const float InfoX = 104f, InfoRight = 286f, Never = -100000f;   // (the face frame ends at FaceX + FaceW = 96 on its top edge)

        public const string StatusActing = "行動中", StatusWaiting = "待機", StatusEnded = "行動終了", StatusDead = "K.I.A";

        [Header("Parts")]
        public RectTransform body, faceRt;
        public Image panel, ink, band, footLine, gun, statusBox;
        public RawImage stripes;
        public UIGlitchImage face;
        public TMP_Text nameText, hpText, weaponText, statusText;
        public SegmentedBar bar;
        public KitTint flash;
        public CanvasGroup group;

        [Header("Events")]
        public UnityEvent onClick = new UnityEvent();
        public UnityEvent<bool> onHover = new UnityEvent<bool>();

        // ---- data -------------------------------------------------------------------------------------------------------------
        Color opColor = Color.white;
        int hp = 1, maxHp = 1;
        bool hovered, selected, ended, dead;
        string statusShown;

        public bool Hovered => hovered;
        public bool Selected => selected;
        public bool Ended => ended;
        public bool Dead => dead;
        public int Hp => hp;

        struct Tween
        {
            public float from, to, start, dur;
            public float At(float t) => Mathf.LerpUnclamped(from, to, UIEase.Tactical(dur > 0f ? (t - start) / dur : 1f));
            public float End => start + dur;
            public void Settle(float value) { from = to = value; start = 0f; }
        }

        Tween hoverT, selectT, wipeT, faceT, lineT, grayT, brightT = new Tween { from = 1f, to = 1f }, enterT = new Tween { from = 1f, to = 1f };
        float hitStart = Never, glitchStart = Never, clock;
        bool running;

        // ---- data API ---------------------------------------------------------------------------------------------------------
        /// <summary>Who the card is: the face cut-out, the English name and the operator colour, and the HP.</summary>
        public void SetData(Sprite faceSprite, string nameEn, Color color, int currentHp, int maxHpValue)
        {
            opColor = color;
            maxHp = Mathf.Max(1, maxHpValue);
            hp = Mathf.Clamp(currentHp, 0, maxHp);
            if (face != null)
            {
                face.SetSprite(faceSprite);
                face.color = faceSprite != null ? Color.white : new Color(1f, 1f, 1f, 0f);
            }
            if (nameText != null) { nameText.text = nameEn ?? ""; Fit(nameText, NameW, NameSize, 14f); }
            if (bar != null) bar.SetImmediate(dead ? 0f : (float)hp / maxHp);
            UpdateHpText();
            Evaluate(clock);
        }

        /// <summary>The weapon line: its short name and, right after it, the silhouette (null: no picture) — both kept left of the
        /// status chip (the gun narrows, then goes, before anything touches).</summary>
        public void SetWeapon(string label, Sprite silhouette)
        {
            float textW = 0f;
            if (weaponText != null)
            {
                weaponText.text = label ?? "";
                Fit(weaponText, StatusX - Gap - InfoX - 24f, WeaponSize, 11f);
                textW = Mathf.Ceil(weaponText.GetPreferredValues(weaponText.text).x);
            }
            if (gun != null)
            {
                gun.sprite = silhouette;
                float x = InfoX + textW + Gap, room = StatusX - Gap - x;
                bool on = silhouette != null && room >= 24f;
                gun.enabled = on;
                if (on) UILayout.Place(gun.rectTransform, x, 48f, Mathf.Min(GunW, room), 14f);
            }
        }

        const float NameSize = 20f, WeaponSize = 15f, NameW = 210f - 6f - InfoX;   // the name stops short of the HP number

        /// <summary>One line in `width`: as it is, else smaller down to `min` (build / data time only).</summary>
        static void Fit(TMP_Text t, float width, float size, float min)
        {
            t.fontSize = size;
            if (string.IsNullOrEmpty(t.text)) return;
            float w = t.GetPreferredValues(t.text).x;
            if (w > width) t.fontSize = Mathf.Max(min, Mathf.Floor(size * width / w * 4f) / 4f);
        }

        const float GunW = 58f, StatusX = 212f, Gap = 6f;

        /// <summary>Everything that changes during a match (each part does nothing when unchanged). `status` is the HUD's word for the
        /// unit's state: "移動 …" (its side is acting) shows 行動中, empty shows 待機, anything else (拘束中, 降下完了 …) as it is.</summary>
        public void SetState(int currentHp, int maxHpValue, string status, bool isSelected, bool isEnded, bool isDead)
        {
            SetHp(currentHp, maxHpValue);
            SetDead(isDead);
            SetEnded(isEnded);
            SetStatus(dead ? StatusDead : ended ? StatusEnded : string.IsNullOrEmpty(status) ? StatusWaiting : status.StartsWith("移動") ? StatusActing : status);
            SetSelected(isSelected);
        }

        void SetStatus(string s)
        {
            if (s == statusShown || statusText == null) return;
            statusShown = s;
            statusText.text = s;
            UIFonts.Apply(statusText, UIFonts.HasJapanese(s) ? UITheme.TextRole.Body : UITheme.TextRole.Display);
            if (!UIFonts.HasJapanese(s) && UITheme.Current.cardFontItalic != null) statusText.font = UITheme.Current.cardFontItalic;
            Evaluate(clock);
        }

        /// <summary>HP from outside: a drop plays Hit (shake, flash, ghost); a rise or a new max just sets the bar.</summary>
        public void SetHp(int currentHp, int maxHpValue)
        {
            int max = Mathf.Max(1, maxHpValue), cur = Mathf.Clamp(currentHp, 0, max);
            if (max == maxHp && cur == hp) return;
            if (max == maxHp && cur < hp && !dead) { Hit(cur); return; }
            maxHp = max; hp = cur;
            if (bar != null) bar.SetImmediate(dead ? 0f : (float)hp / maxHp);
            UpdateHpText();
        }

        int hpShown = -1;
        void UpdateHpText()
        {
            int v = dead ? 0 : hp;
            if (hpText == null || (v == hpShown && hpText.text.Length > 0)) return;
            hpShown = v;
            hpText.SetText("{0:0}/{1:0}", (float)v, (float)maxHp);
        }

        // ---- states -----------------------------------------------------------------------------------------------------------
        public void SetHovered(bool on)
        {
            if (on == hovered) return;
            hovered = on;
            Rebase(); Retarget(ref hoverT, on ? 1f : 0f, HoverTime); Run();
        }

        public void SetSelected(bool on)
        {
            if (on == selected) return;
            selected = on;
            Rebase();
            Retarget(ref selectT, on ? 1f : 0f, SelectTime);
            Retarget(ref wipeT, on ? 1f : 0f, on ? WipeTime : SelectTime);
            Retarget(ref faceT, on ? 1f : 0f, FaceTime);
            if (on) { lineT.from = 0f; lineT.to = 1f; lineT.start = clock; lineT.dur = LineTime; glitchStart = clock; }
            else lineT.Settle(0f);
            Run();
        }

        public void SetEnded(bool on)
        {
            if (on == ended) return;
            ended = on;
            ApplyTone();
        }

        public void SetDead(bool on)
        {
            if (on == dead) return;
            dead = on;
            if (bar != null) bar.SetImmediate(on ? 0f : (float)hp / maxHp);
            UpdateHpText();
            ApplyTone();
        }

        /// <summary>Takes a hit down to `newHp`: shake, red flash, the face's Hit glitch and the bar's ghost.</summary>
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

        /// <summary>The entrance: slides in from −46 and fades in over .5s after index × 60ms.</summary>
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
            Run();
        }

        void Retarget(ref Tween tw, float to, float dur)
        {
            float now = tw.At(clock);
            tw.from = now; tw.to = to; tw.start = clock; tw.dur = dur;
        }

        // an idle card restarts its clock at 0 with every tween settled (a preview at clock 0 keeps every pose)
        void Rebase()
        {
            if (running || clock == 0f) return;
            float c = clock;
            hoverT.Settle(hoverT.At(c)); selectT.Settle(selectT.At(c)); wipeT.Settle(wipeT.At(c)); faceT.Settle(faceT.At(c));
            lineT.Settle(lineT.At(c)); grayT.Settle(grayT.At(c)); brightT.Settle(brightT.At(c)); enterT.Settle(enterT.At(c));
            hitStart = glitchStart = Never;
            if (bar != null) bar.Pose(bar.Fill, bar.Fill);
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
                float d = Mathf.Max(Mathf.Max(hoverT.End, selectT.End), Mathf.Max(wipeT.End, faceT.End));
                d = Mathf.Max(d, Mathf.Max(lineT.End, grayT.End));
                d = Mathf.Max(d, Mathf.Max(brightT.End, enterT.End));
                d = Mathf.Max(d, glitchStart + SelectGlitch);
                if (bar != null) d = Mathf.Max(d, hitStart + Mathf.Max(HitFlash, bar.Duration));
                return Mathf.Max(0f, d);
            }
        }

        public void Evaluate(float t)
        {
            var theme = UITheme.Current;
            float h = hoverT.At(t), s = Mathf.Clamp01(selectT.At(t)), w = Mathf.Clamp01(wipeT.At(t)), fu = faceT.At(t);
            float l = Mathf.Clamp01(lineT.At(t)), g = grayT.At(t), br = brightT.At(t), e = Mathf.Clamp01(enterT.At(t));
            bool reduce = UIMotionSettings.reduceMotion;

            // position: hover (not while selected), selection, hit shake (steps), entrance
            float ht = t - hitStart, q = ht / HitShake;
            float shake = reduce || q < 0f ? 0f : q < .25f ? -8f : q < .5f ? 7f : q < .75f ? -5f : q < 1f ? 3f : 0f;
            if (body != null) body.anchoredPosition = new Vector2(HoverShift * h * (1f - s) + SelectShift * s + shake - EnterShift * (1f - e), 0f);
            if (group != null && group.alpha != e) group.alpha = e;

            Color inkC = theme.ink, white = Color.white;
            Color textC = Color.Lerp(inkC, white, w);
            Color empty = Color.Lerp(new Color(inkC.r, inkC.g, inkC.b, .13f), new Color(1f, 1f, 1f, .16f), w);
            if (panel != null) panel.color = Tone(white, g, br);
            if (ink != null)
            {
                ink.color = Tone(inkC, g, br);
                var sc = ink.rectTransform.localScale;
                if (sc.x != w) ink.rectTransform.localScale = new Vector3(w, 1f, 1f);
                if (ink.enabled != w > .001f) ink.enabled = w > .001f;
            }
            if (stripes != null)
            {
                bool on = w > .001f;
                if (stripes.enabled != on) stripes.enabled = on;
                if (on)
                {
                    stripes.color = Tone(new Color(1f, 1f, 1f, w), g, br);
                    var r = stripes.uvRect;
                    float x = reduce ? 0f : -Mathf.Repeat(t * StripeSpeed, 1f);
                    if (r.x != x) stripes.uvRect = new Rect(x, 0f, r.width, r.height);
                }
            }
            if (band != null)
            {
                band.color = Tone(opColor, g, br);
                float bw = Mathf.Lerp(BandNormal, BandSelected, s) + Slant;
                var sd = band.rectTransform.sizeDelta;
                if (sd.x != bw) band.rectTransform.sizeDelta = new Vector2(bw, sd.y);
            }
            if (footLine != null)
            {
                footLine.color = Tone(opColor, g, br);
                var ls = footLine.rectTransform.localScale;
                if (ls.x != l) footLine.rectTransform.localScale = new Vector3(l, 1f, 1f);
            }
            if (faceRt != null)
            {
                // selected: the face rises and grows from its bottom-left towards the upper right, its hair standing over the card
                float k = reduce ? (selected ? 1f : 0f) : fu;
                PopOffset = PopRoom * Mathf.Clamp01(k);
                float z = 1f + FaceGrow * k, rise = FaceRise * k;
                if (faceRt.localScale.x != z) faceRt.localScale = new Vector3(z, z, 1f);
                if (faceRt.anchoredPosition.y != rise) faceRt.anchoredPosition = new Vector2(0f, rise);
            }
            if (face != null)
            {
                bool hitNewer = hitStart >= glitchStart && hitStart > Never;
                face.preset = hitNewer ? UIGlitchImage.Preset.Hit : UIGlitchImage.Preset.Select;
                face.SetTone(1f - g, br);
                face.Evaluate(hitNewer ? ht : t - glitchStart);
            }
            if (nameText != null) nameText.color = Tone(textC, g, br);
            if (hpText != null) hpText.color = Tone(textC, g, br);
            if (weaponText != null) weaponText.color = Tone(textC, g, br);
            if (gun != null) gun.color = Tone(textC, g, br);
            if (statusBox != null)
            {
                Color sb = dead ? theme.foe : Color.Lerp(inkC, opColor, w), st = dead ? white : Color.Lerp(white, inkC, w);
                statusBox.color = Tone(sb, dead ? 0f : g, dead ? 1f : br);
                if (statusText != null) statusText.color = Tone(st, dead ? 0f : g, dead ? 1f : br);
            }
            if (bar != null)
            {
                Color f = Tone(opColor, g, br), gh = Tone(theme.foe, g, br), em = Tone(empty, g, br);
                if (bar.color != f || bar.ghostColor != gh || bar.emptyColor != em) bar.SetColors(f, gh, em);
                bar.Evaluate(ht);
            }
            if (flash != null)
            {
                float fl = ht >= 0f && ht < HitFlash ? Mathf.Max(0f, 1f - Mathf.Floor(ht / HitFlash * 5f) * .2f) : 0f;
                Color foe = theme.foe;
                flash.Set(fl > 0f ? new Color(foe.r, foe.g, foe.b, HitFlashAlpha * fl) : Color.clear, Color.Lerp(white, inkC, w));
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
            if (!selected && clock >= Duration) running = false;   // (selected: the bands keep sliding)
        }

        // ---- pointer ----------------------------------------------------------------------------------------------------------
        public void OnPointerEnter(PointerEventData ev) { SetHovered(true); onHover.Invoke(true); }
        public void OnPointerExit(PointerEventData ev) { SetHovered(false); onHover.Invoke(false); }
        public void OnPointerClick(PointerEventData ev) { if (ev.button == PointerEventData.InputButton.Left) onClick.Invoke(); }

        // ---- the bands' picture: dark diagonal bands on clear, tiled ----------------------------------------------------------
        static Texture2D stripeTex;
        public static Texture2D StripeTexture
        {
            get
            {
                if (stripeTex != null) return stripeTex;
                const int N = 32;
                var px = new Color32[N * N];
                Color32 a = new Color32(0x2b, 0x2f, 0x36, 0xff), b = new Color32(0x1a, 0x1c, 0x21, 0xff);
                for (int y = 0; y < N; y++) for (int x = 0; x < N; x++) px[y * N + x] = ((x + y) % N) < N / 2 ? a : b;
                stripeTex = new Texture2D(N, N, TextureFormat.RGBA32, false) { name = "RosterStripes", wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Bilinear };
                stripeTex.SetPixels32(px);
                stripeTex.Apply(false, true);
                return stripeTex;
            }
        }

        // ---- build ------------------------------------------------------------------------------------------------------------
        static TMP_Text CardText(Transform parent, string name, float size, TextAlignmentOptions align, TMP_FontAsset font)
        {
            var t = HudKit.AddText(parent, "", size, UITheme.Current.ink, true, align, name, UITheme.TextRole.Display);
            if (font != null) t.font = font;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            return t;
        }

        static Image SlantedImage(Transform parent, string name, Color c, float slant, bool raycast = false)
        {
            var rt = HudKit.NewRect(name, parent);
            var img = HudKit.AddImage(rt, c, raycast);
            rt.gameObject.AddComponent<SlantedGraphic>().Set(SlantedGraphic.Size.Custom, SlantedGraphic.Direction.Right, slant);
            return img;
        }

        /// <summary>A card `name` under `parent` (place it with UILayout.Place, 300×68). Fill it with SetData / SetWeapon.</summary>
        public static TacticalRosterCard Create(Transform parent, string name)
        {
            var theme = UITheme.Current;
            var italic = theme.cardFontItalic;
            var root = HudKit.NewRect(name, parent);
            root.sizeDelta = new Vector2(Width, Height);
            var group = root.gameObject.AddComponent<CanvasGroup>();

            var bodyRt = HudKit.NewRect("Body", root);
            HudKit.Stretch(bodyRt);

            var panel = SlantedImage(bodyRt, "Panel", Color.white, Slant, true);
            HudKit.Stretch(panel.rectTransform);

            var ink = SlantedImage(bodyRt, "DecoInk", theme.ink, Slant);
            HudKit.Stretch(ink.rectTransform);
            ink.rectTransform.pivot = new Vector2(0f, .5f);

            var srt = HudKit.NewRect("DecoStripes", bodyRt);
            HudKit.Stretch(srt);
            var stripes = srt.gameObject.AddComponent<RawImage>();
            stripes.texture = StripeTexture;
            stripes.raycastTarget = false;
            stripes.uvRect = new Rect(0f, 0f, Width / 26f, Height / 26f);
            srt.gameObject.AddComponent<SlantedGraphic>().Set(SlantedGraphic.Size.Custom, SlantedGraphic.Direction.Right, Slant);

            var band = SlantedImage(bodyRt, "DecoBand", Color.white, Slant);
            UILayout.Place(band.rectTransform, 0f, 0f, BandNormal + Slant, Height);

            var foot = SlantedImage(bodyRt, "DecoFootLine", Color.white, Slant * FootLine / Height);
            UILayout.Place(foot.rectTransform, 0f, Height - FootLine, Width - Slant + Slant * FootLine / Height, FootLine);
            foot.rectTransform.pivot = new Vector2(0f, .5f);
            foot.rectTransform.anchoredPosition = new Vector2(0f, -(Height - FootLine * .5f));
            foot.rectTransform.localScale = new Vector3(0f, 1f, 1f);

            // the face: cut by a parallelogram at the card's own slant, down to the card's foot (the foot line, shown only when
            // selected, is drawn over it) and up to PopRoom over its top — only the selected face rises into that room (its cut-out
            // hair stands over the card, as in the design); KitRoster opens that room above the card, so it never reaches the card above
            float frameH = Height + PopRoom, extra = Slant * PopRoom / Height, frameW = FaceW + extra;
            var mask = SlantedMask.Create(bodyRt, "FaceMask", new Vector2(frameW, frameH), SlantedGraphic.Size.Custom);
            mask.slant.Set(SlantedGraphic.Size.Custom, SlantedGraphic.Direction.Right, Slant + extra);   // the card's angle, carried up
            UILayout.Place((RectTransform)mask.transform, FaceX, -PopRoom, frameW, frameH);
            UIUtil.GetOrAdd<LintIgnoreOverlap>(mask.gameObject).bounds = true;     // reaches PopRoom over the card by design
            HudKit.DestroySafe(mask.photo.gameObject);
            mask.photo = null; mask.fitter = null;
            var back = mask.transform.Find("DecoBackdrop");
            if (back != null) HudKit.DestroySafe(back.gameObject);              // (the card shows through: white, or the bands)
            // FaceRoot: the card's own height at the frame's foot; on selection it rises FaceRise and grows from its bottom-left corner
            var frt = HudKit.NewRect("FaceRoot", mask.transform);
            frt.anchorMin = Vector2.zero; frt.anchorMax = new Vector2(1f, Height / frameH);
            frt.offsetMin = frt.offsetMax = Vector2.zero;
            frt.pivot = Vector2.zero;
            frt.anchoredPosition = Vector2.zero;
            var fpr = HudKit.NewRect("Face", frt);
            fpr.anchorMin = Vector2.zero; fpr.anchorMax = Vector2.one;
            fpr.pivot = new Vector2(.5f, 1f);                                     // top-pinned: the chin is what gets cut
            fpr.offsetMin = fpr.offsetMax = Vector2.zero;
            var face = fpr.gameObject.AddComponent<UIGlitchImage>();
            face.raycastTarget = false;
            face.preset = UIGlitchImage.Preset.Select;
            face.color = new Color(1f, 1f, 1f, 0f);
            var fit = fpr.gameObject.AddComponent<AspectRatioFitter>();
            fit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            fit.aspectRatio = 1f;
            fpr.gameObject.AddComponent<LintIgnoreClip>().reason = "the square face covers the slanted frame, cropped at the bottom";
            UIUtil.GetOrAdd<LintIgnoreClip>(frt.gameObject).reason = "grows inside the face frame on selection (the frame's mask cuts it)";
            foot.transform.SetAsLastSibling();                                    // the foot line over the face

            var nm = CardText(bodyRt, "Name", 20f, TextAlignmentOptions.BottomLeft, italic);
            nm.characterSpacing = 4f;
            UILayout.Place(nm.rectTransform, InfoX, 1f, NameW, 27f);
            var hpT = CardText(bodyRt, "Hp", 14f, TextAlignmentOptions.BottomRight, italic);
            UILayout.Place(hpT.rectTransform, 210f, 7f, InfoRight - 210f, 18f);

            var barRt = HudKit.NewRect("Bar", bodyRt);
            UILayout.Place(barRt, InfoX - 2f, 30f, InfoRight - InfoX - 10f, 12f);
            var bar = barRt.gameObject.AddComponent<SegmentedBar>();
            bar.style = SegmentedBar.Style.Custom;
            bar.customSegment = 6f; bar.customGap = 3f;
            bar.raycastTarget = false;
            barRt.gameObject.AddComponent<SlantedGraphic>().Set(SlantedGraphic.Size.Custom, SlantedGraphic.Direction.Right, 4f);

            var wp = CardText(bodyRt, "Weapon", 15f, TextAlignmentOptions.MidlineLeft, italic);
            UILayout.Place(wp.rectTransform, InfoX, 45f, StatusX - Gap - InfoX, 21f);
            var grt = HudKit.NewRect("Gun", bodyRt);
            var gun = grt.gameObject.AddComponent<Image>();
            gun.preserveAspect = true; gun.raycastTarget = false; gun.enabled = false;
            UILayout.Place(grt, InfoX + 50f, 48f, GunW, 14f);

            var stBox = SlantedImage(bodyRt, "Status", theme.ink, 6f);
            UILayout.Place(stBox.rectTransform, StatusX, 47f, InfoRight - StatusX, 17f);
            var stT = HudKit.AddText(stBox.rectTransform, StatusWaiting, 12f, Color.white, true, TextAlignmentOptions.Center, "Text", UITheme.TextRole.Body);
            HudKit.Stretch(stT.rectTransform);

            var flRt = HudKit.NewRect("DecoHitFlash", bodyRt);
            HudKit.Stretch(flRt);
            var fl = flRt.gameObject.AddComponent<KitTint>();
            fl.raycastTarget = false;
            fl.Screen = true;
            fl.MatchShape(panel.GetComponent<SlantedGraphic>());

            var card = root.gameObject.AddComponent<TacticalRosterCard>();
            card.body = bodyRt; card.faceRt = frt; card.panel = panel; card.ink = ink; card.band = band; card.footLine = foot;
            card.gun = gun; card.statusBox = stBox; card.stripes = stripes; card.face = face;
            card.nameText = nm; card.hpText = hpT; card.weaponText = wp; card.statusText = stT; card.bar = bar; card.flash = fl; card.group = group;
            card.statusShown = StatusWaiting;
            card.Evaluate(0f);
            return card;
        }
    }
}
