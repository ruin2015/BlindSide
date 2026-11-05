// The HUD's bottom detail panel (#up, ui_spec "하단 상세 패널"): 430×190 at left 20 / bottom 20 of the HUD canvas, built from the kit —
//   Reveal       RectMask2D 60 larger than the panel on every side (room for the rising face, the tags and the shadows); the entrance
//                narrows its right padding (left → right) while the CanvasGroup goes .4 → 1 (panelIn .5s)
//     Content    the panel's rect
//       DecoShadowBody  KitShadow 0 12 30 .5
//       Body     TacticalPanel paper (the raycast target: the panel isn't the battlefield)
//         Head   68 high, RectMask2D (.hd overflow hidden): DecoHeadBg ink (foe: dark red), DecoStripes (right 96, 56 wide),
//                Code 11 display (ellipsis), Name 26 display (UIGlitchName), Kana 12.5 body (+ role · team), HpBox (92 wide, slant 18,
//                HP 28 display + "HP /max" 11)
//         Grid   5 cells MOVE SPD DMG ACC RNG: ink2, label 11, value 16 (shrinks to 11 when a value is wider than its cell), DecoDot
//         Ability  y118 h62, DecoBand 4 in the operator colour: AbilityIcon ✦ (display), AbilityName 12.5 (body), Charges, Desc 12 (2 lines,
//                ellipsis)
//       DecoShadowFace  KitShadow 0 8 20 .4
//       Face     SlantedMask 104×104 (slant 20) at x14, 30 above the panel: DecoBackdrop, Photo (UIGlitchImage Select), Monogram,
//                DecoLine 5 in the operator colour at the bottom
//       Tags     status chips (シールド展開 · 行動終了 …) beside the face, above the panel
// The panel knows no game types: Show takes the UnitPanelModel the controller fills (the same model UnitPanelView took). Show with
// noAnim false (another operator) plays the entrance, the face Select glitch (.5s) and the name glitch (.8s) on one panel clock:
// Evaluate(t) poses everything at t, Update only advances the clock with unscaled time. No allocation per frame (texts change only in Show).
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Blindside.UI
{
    [AddComponentMenu("Blindside/UI/Kit Unit Panel")]
    public class KitUnitPanel : MonoBehaviour, ITimedEffect
    {
        public const float Width = 430f, Height = 190f, Left = 20f, Bottom = 20f;
        public const float HeadH = 68f, TextX = 140f, HpW = 92f, HpSlant = 18f;
        public const float FaceX = 14f, FaceSize = 104f, FaceRise = 30f, FaceSlant = 20f, FaceLine = 5f;
        public const float AbX = 14f, AbY = 118f, AbR = 16f, AbB = 10f, AbBand = 4f;
        public const float RevealTime = .5f, RevealAlpha = .4f, Margin = 60f;
        const int MaxTags = 6;
        static readonly string[] StatLabels = { "MOVE", "SPD", "DMG", "ACC", "RNG" };
        // fixed column ratio (MOVE carries "4.2/7m", SPD / RNG one or two digits): every value fits at 16 without shrinking
        static readonly float[] StatColumns = { 1.5f, .7f, 1.1f, .8f, .7f };

        [Header("Parts")]
        public RectMask2D reveal;
        public CanvasGroup group;
        public RectTransform content;
        public TacticalPanel body;
        public Image headBg;
        public TMP_Text codeText, nameText, kanaText, hpText, hpMaxText;
        public UIGlitchName nameGlitch;
        public SlantedMask face;
        public UIGlitchImage photo;
        public AspectRatioFitter photoFit;
        public Image faceBack, faceLine;
        public TMP_Text monogram;
        public TMP_Text[] statValues = new TMP_Text[5];
        public Image[] statDots = new Image[5];
        public Image abilityBand;
        public TMP_Text abilityIcon, abilityName, abilityCharges, abilityDesc;
        public RectTransform tagsRow;
        public Chip[] tags = new Chip[MaxTags];

        float clock, enterStart = -100f;
        bool running;

        public RectTransform Root => (RectTransform)transform;
        public bool Visible => gameObject.activeSelf;

        // ---- data -------------------------------------------------------------------------------------------------------------
        /// <summary>Shows `m` (port of showUnitPanel(u, noAnim)). noAnim false: the entrance, the face Select glitch and the name glitch.</summary>
        public void Show(UnitPanelModel m, bool noAnim)
        {
            if (m == null) return;
            gameObject.SetActive(true);
            var theme = UITheme.Current;
            Color c = m.color;

            headBg.color = m.foe ? HudKit.FoeHead : theme.ink;
            codeText.text = m.code;
            nameText.text = m.key;
            kanaText.text = m.krRich;
            hpText.text = m.hp.ToString();
            hpMaxText.text = "HP\n/" + m.maxHp;
            LayoutHp();

            // face: the operator's face on the paper backdrop, or the two-letter monogram on the operator colour
            var sprite = OperatorArt.Face(m.key);
            photo.SetSprite(sprite);
            photo.color = sprite != null ? Color.white : new Color(1f, 1f, 1f, 0f);
            if (sprite != null) photoFit.aspectRatio = sprite.rect.width / Mathf.Max(1f, sprite.rect.height);
            faceBack.color = sprite != null ? HudKit.PaperGrad2 : c;
            monogram.text = m.key.Length >= 2 ? m.key.Substring(0, 2) : m.key;
            monogram.gameObject.SetActive(sprite == null);
            faceLine.color = c;

            for (int i = 0; i < 5; i++)
            {
                statDots[i].color = c;
                var v = statValues[i];
                v.fontSize = 16f;
                v.text = m.grid[i];
                FitWidth(v, 11f);
            }

            abilityBand.color = c;
            abilityName.text = m.abilityName;
            abilityCharges.text = m.charges;
            abilityDesc.text = m.description;

            int n = 0;
            for (int i = 0; i < m.tags.Count && n < MaxTags; i++, n++)
            {
                var chip = tags[n];
                var tg = m.tags[i];
                chip.gameObject.SetActive(true);
                chip.box.color = tg.bg; chip.label.color = tg.fg;
                chip.SetText(tg.text);
            }
            for (int i = n; i < MaxTags; i++) tags[i].gameObject.SetActive(false);
            float x = 0f;
            for (int i = 0; i < n; i++)
            {
                var r = (RectTransform)tags[i].transform;
                float w = r.sizeDelta.x;
                UILayout.Place(r, x, 0f, w, r.sizeDelta.y);
                x += w + 4f;
            }

            if (!noAnim)
            {
                clock = 0f; enterStart = 0f;
                if (Application.isPlaying && isActiveAndEnabled) running = true;
            }
            Evaluate(running ? clock : Duration);
        }

        public void Hide()
        {
            running = false;
            gameObject.SetActive(false);
        }

        /// <summary>A safety net only (only in Show, never per frame): the column widths are fixed so every value fits at 16. A wider
        /// value shrinks as a whole, down to `min`; nothing is cut, so MOVE's "/7m" maximum always stays.</summary>
        static void FitWidth(TMP_Text t, float min)
        {
            float avail = t.rectTransform.rect.width + 1f;
            for (int k = 0; k < 6; k++)
            {
                float w = t.GetPreferredValues(t.text).x;
                if (w <= avail || t.fontSize <= min) return;
                t.fontSize = Mathf.Max(min, Mathf.Floor(t.fontSize * avail / w * 4f) / 4f);
            }
        }

        /// <summary>.hp{display:flex;justify-content:center;gap:3px}: the number and "HP /max" centred in the box, kept right of the
        /// slanted edge at mid-height.</summary>
        void LayoutHp()
        {
            float wb = Mathf.Ceil(hpText.GetPreferredValues(hpText.text).x), ws = Mathf.Ceil(hpMaxText.GetPreferredValues(hpMaxText.text).x);
            float x = Mathf.Max(HpSlant * .5f + 2f, (HpW - wb - 3f - ws) * .5f);
            UILayout.Place(hpText.rectTransform, x, 0f, wb + 1f, HeadH);
            UILayout.Place(hpMaxText.rectTransform, x + wb + 3f, 0f, Mathf.Max(1f, ws + 1f), HeadH);
        }

        // ---- ITimedEffect -----------------------------------------------------------------------------------------------------
        public float Duration => Mathf.Max(RevealTime, UIGlitchName.CardDuration);

        public void Evaluate(float t)
        {
            float e = t - enterStart;
            float p = UIEase.Tactical(e / RevealTime);
            if (UIMotionSettings.reduceMotion) p = 1f;
            float full = Width + Margin * 2f;
            var pad = new Vector4(0f, 0f, full * (1f - p), 0f);
            if (reveal.padding != pad) reveal.padding = pad;
            float a = Mathf.Lerp(RevealAlpha, 1f, UIEase.Tactical(e / RevealTime));
            if (group.alpha != a) group.alpha = a;

            photo.preset = UIGlitchImage.Preset.Select;
            photo.Evaluate(e);
            nameGlitch.Evaluate(e);
        }

        void Update()
        {
            if (!running) return;
            clock += Time.unscaledDeltaTime;
            Evaluate(clock);
            if (clock >= Duration) running = false;
        }

        // ---- build ------------------------------------------------------------------------------------------------------------
        /// <summary>Builds the panel's parts on this object (once; the prefab holds only the root, like HUD_Roster) and hides it.</summary>
        public void Build()
        {
            if (content != null) return;
            var theme = UITheme.Current;
            var root = Root;
            UILayout.Anchor(root, Vector2.zero, Left, Bottom, Width, Height);
            group = UIUtil.GetOrAdd<CanvasGroup>(gameObject);

            var rv = HudKit.NewRect("Reveal", root);
            rv.anchorMin = Vector2.zero; rv.anchorMax = Vector2.one; rv.pivot = new Vector2(.5f, .5f);
            rv.offsetMin = new Vector2(-Margin, -Margin); rv.offsetMax = new Vector2(Margin, Margin);
            reveal = rv.gameObject.AddComponent<RectMask2D>();

            content = HudKit.NewRect("Content", rv);
            content.anchorMin = Vector2.zero; content.anchorMax = Vector2.one; content.pivot = new Vector2(.5f, .5f);
            content.offsetMin = new Vector2(Margin, Margin); content.offsetMax = new Vector2(-Margin, -Margin);

            // ---- body ----
            var bodyRt = HudKit.NewRect("Body", content);
            HudKit.Stretch(bodyRt);
            body = bodyRt.gameObject.AddComponent<TacticalPanel>();
            body.SetPreset(KitColor.Paper);
            body.raycastTarget = true;
            KitShadow.Behind(bodyRt, MapFloor, .5f, 12f, 30f);

            // ---- .hd ----
            var head = HudKit.NewRect("Head", bodyRt);
            UILayout.Place(head, 0f, 0f, Width, HeadH);
            head.gameObject.AddComponent<RectMask2D>();
            var hbg = HudKit.NewRect("DecoHeadBg", head);
            HudKit.Stretch(hbg);
            headBg = HudKit.AddImage(hbg, theme.ink);
            var stripes = HudKit.NewRect("DecoStripes", head);
            UILayout.Place(stripes, Width - 96f - 56f, 0f, 56f, HeadH);
            var st = HazardStripe.Create(stripes, "DecoStripeFill", Color.Lerp(theme.ink, Color.white, .07f), theme.ink);
            st.SetScroll(false);

            float textW = Width - TextX - HpW - 8f;
            codeText = HudKit.AddText(head, "", 11f, Color.Lerp(theme.ink, Color.white, .55f), true, TextAlignmentOptions.TopLeft, "Code", UITheme.TextRole.Display);
            codeText.characterSpacing = 10f;
            codeText.overflowMode = TextOverflowModes.Ellipsis;
            codeText.gameObject.AddComponent<LintIgnoreClip>().reason = "a long code line ends in an ellipsis (max-width 190, text-overflow)";
            UILayout.Place(codeText.rectTransform, TextX, 7f, Mathf.Min(190f, textW), 14f);

            nameText = HudKit.AddText(head, "", 26f, Color.white, true, TextAlignmentOptions.TopLeft, "Name", UITheme.TextRole.Display);
            nameText.characterSpacing = 10f;
            nameText.overflowMode = TextOverflowModes.Ellipsis;
            UILayout.LineHeight(nameText, 1.05f);
            UILayout.Place(nameText.rectTransform, TextX, 21f, textW, 28f);
            nameGlitch = nameText.gameObject.AddComponent<UIGlitchName>();

            kanaText = HudKit.AddText(head, "", 12.5f, Color.white, true, TextAlignmentOptions.TopLeft, "Kana", UITheme.TextRole.Body);
            kanaText.overflowMode = TextOverflowModes.Ellipsis;
            kanaText.gameObject.AddComponent<LintIgnoreClip>().reason = "kana + role · team ends in an ellipsis before the HP box";
            UILayout.Place(kanaText.rectTransform, TextX, 48f, textW, 16f);

            // .hp: clip-path polygon(18px 0, 100% 0, 100% 100%, 0 100%) — a slant-18 parallelogram whose sheared top-right corner is cut
            // off by the header's mask, so its right edge stays vertical at the panel's edge
            var hp = TacticalPanel.Create(head, "HpBox", KitColor.Paper, SlantedGraphic.Size.Custom, HpSlant);
            UILayout.Place(hp.rectTransform, Width - HpW, 0f, HpW + HpSlant, HeadH);
            hp.gameObject.AddComponent<LintIgnoreClip>().reason = "the slanted box's top-right corner runs past the header and is masked (CSS clip-path)";
            hpText = HudKit.AddText(hp.transform, "", 28f, theme.ink, true, TextAlignmentOptions.MidlineLeft, "HpValue", UITheme.TextRole.Display);
            UILayout.Place(hpText.rectTransform, 0f, 0f, 56f, HeadH);
            hpMaxText = HudKit.AddText(hp.transform, "", 11f, theme.dim, true, TextAlignmentOptions.MidlineLeft, "HpMax", UITheme.TextRole.Display);
            UILayout.LineHeight(hpMaxText, 1.3f);
            UILayout.Place(hpMaxText.rectTransform, 59f, 0f, HpW - 59f, HeadH);

            // ---- .bd .grid ----
            const float GridY = HeadH + 6f, GridH = 41f, GridGap = 3f;
            float gridW = Width - TextX - 16f, colSum = 0f, cellX = 0f;
            foreach (var r in StatColumns) colSum += r;
            var grid = HudKit.NewRect("Grid", bodyRt);
            UILayout.Place(grid, TextX, GridY, gridW, GridH);
            Color labelC = Color.Lerp(theme.ink2, Color.white, .6f);
            for (int i = 0; i < 5; i++)
            {
                float cellW = (gridW - 4f * GridGap) * StatColumns[i] / colSum;
                var cell = HudKit.NewRect(StatLabels[i], grid);
                HudKit.AddImage(cell, theme.ink2);
                UILayout.Place(cell, cellX, 0f, cellW, GridH);
                cellX += cellW + GridGap;
                var dot = HudKit.NewRect("DecoDot", cell);
                UILayout.Place(dot, cellW - 3f - 4f, 3f, 4f, 4f);
                statDots[i] = HudKit.AddImage(dot, theme.blue);
                var lab = HudKit.AddText(cell, StatLabels[i], 11f, labelC, true, TextAlignmentOptions.TopLeft, "Label", UITheme.TextRole.Display);
                lab.characterSpacing = 5f;
                UILayout.Place(lab.rectTransform, 6f, 3f, cellW - 6f - 8f, 14f);
                var val = HudKit.AddText(cell, "", 16f, Color.white, true, TextAlignmentOptions.BottomLeft, "Value", UITheme.TextRole.Display);
                UILayout.Place(val.rectTransform, 6f, 17f, cellW - 6f - 3f, 20f);
                statValues[i] = val;
            }

            // ---- .ab ----
            float abW = Width - AbX - AbR, abH = Height - AbY - AbB;
            var ab = HudKit.NewRect("Ability", bodyRt);
            HudKit.AddImage(ab, Color.Lerp(theme.paper, Color.black, .05f));
            UILayout.Place(ab, AbX, AbY, abW, abH);
            var band = HudKit.NewRect("DecoBand", ab);
            UILayout.Place(band, 0f, 0f, AbBand, abH);
            abilityBand = HudKit.AddImage(band, theme.blue);
            float innerX = AbBand + 10f, innerW = abW - innerX - 10f, chW = 60f;   // ●●●●● at 11px in NotoJP is 55 wide
            // BSTactical's ✦ is about half the mockup's fallback-font star at the same size (ink 6×7 vs 11×11 at 12.5px): drawn at 19 so
            // its ink matches, centred on the name line, in ink like the mockup (.an inherits color:var(--ink))
            abilityIcon = HudKit.AddText(ab, "✦", 19f, theme.ink, true, TextAlignmentOptions.MidlineLeft, "AbilityIcon", UITheme.TextRole.Display);
            UILayout.Place(abilityIcon.rectTransform, innerX, 3f, 14f, 20f);
            abilityName = HudKit.AddText(ab, "", 12.5f, theme.ink, true, TextAlignmentOptions.TopLeft, "AbilityName", UITheme.TextRole.Body);
            abilityName.overflowMode = TextOverflowModes.Ellipsis;
            UILayout.Place(abilityName.rectTransform, innerX + 14f, 5f, innerW - 14f - chW - 6f, 16f);
            // ● ○ ∞ are native glyphs of NotoJP-Medium (BSTactical has none of them), so the body role; the dynamic OS symbol font it used
            // before could lose its asset and leave the text without a font ("Please assign a Font Asset")
            abilityCharges = HudKit.AddText(ab, "", 11f, theme.dim, true, TextAlignmentOptions.TopRight, "Charges", UITheme.TextRole.Body);
            UILayout.Place(abilityCharges.rectTransform, innerX + innerW - chW, 5f, chW, 16f);
            abilityDesc = HudKit.AddText(ab, "", 12f, UIPalette.Hex("#2B3036"), false, TextAlignmentOptions.TopLeft, "Desc", UITheme.TextRole.Body);
            abilityDesc.textWrappingMode = TextWrappingModes.Normal;
            abilityDesc.overflowMode = TextOverflowModes.Ellipsis;
            abilityDesc.gameObject.AddComponent<LintIgnoreClip>().reason = "a long description stops at 2 lines with an ellipsis";
            UILayout.LineHeight(abilityDesc, 1.4f);
            UILayout.Place(abilityDesc.rectTransform, innerX, 22f, innerW, 34f);

            // ---- .pt (above the panel, over the header) ----
            face = SlantedMask.Create(content, "Face", new Vector2(FaceSize, FaceSize), SlantedGraphic.Size.Custom);
            face.slant.Set(SlantedGraphic.Size.Custom, SlantedGraphic.Direction.Right, FaceSlant);
            var faceRt = (RectTransform)face.transform;
            UILayout.Place(faceRt, FaceX, -FaceRise, FaceSize, FaceSize);
            face.gameObject.AddComponent<LintIgnoreClip>().reason = "the portrait rises 30 above the panel (.pt top:-30px)";
            HudKit.DestroySafe(face.photo.gameObject);
            face.photo = null; face.fitter = null;
            faceBack = face.transform.Find("DecoBackdrop").GetComponent<Image>();
            var pr = HudKit.NewRect("Photo", face.transform);
            pr.anchorMin = Vector2.zero; pr.anchorMax = Vector2.one; pr.pivot = new Vector2(.5f, 1f);
            pr.offsetMin = new Vector2(0f, FaceLine); pr.offsetMax = Vector2.zero;
            photo = pr.gameObject.AddComponent<UIGlitchImage>();
            photo.raycastTarget = false;
            photo.preset = UIGlitchImage.Preset.Select;
            photo.color = new Color(1f, 1f, 1f, 0f);
            photoFit = pr.gameObject.AddComponent<AspectRatioFitter>();
            photoFit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            photoFit.aspectRatio = 1f;
            pr.gameObject.AddComponent<LintIgnoreClip>().reason = "face scaled to cover the frame, cropped at the bottom";
            monogram = HudKit.AddText(face.transform, "", 19f, theme.ink, true, TextAlignmentOptions.Center, "Monogram", UITheme.TextRole.Display);
            HudKit.Stretch(monogram.rectTransform);
            var line = HudKit.NewRect("DecoLine", face.transform);
            line.anchorMin = Vector2.zero; line.anchorMax = new Vector2(1f, 0f); line.pivot = new Vector2(.5f, 0f);
            line.anchoredPosition = Vector2.zero; line.sizeDelta = new Vector2(0f, FaceLine);
            faceLine = HudKit.AddImage(line, theme.blue);
            KitShadow.Behind(faceRt, theme.ink, .4f, 8f, 20f);

            // ---- status tags beside the face ----
            tagsRow = HudKit.NewRect("Tags", content);
            float tagsX = FaceX + FaceSize + 8f;
            UILayout.Place(tagsRow, tagsX, -22f, Width - tagsX, 18f);
            tagsRow.gameObject.AddComponent<LintIgnoreClip>().reason = "status tags sit above the panel, beside the rising portrait";
            for (int i = 0; i < MaxTags; i++)
            {
                var chip = Chip.Create(tagsRow, "Tag" + i, "", KitColor.Ink, KitColor.White, 11f);
                chip.padX = 7f; chip.padY = 1f;
                chip.gameObject.SetActive(false);
                tags[i] = chip;
            }

            enterStart = -100f;
            gameObject.SetActive(false);
        }

        static readonly Color MapFloor = new Color32(0x2b, 0x2f, 0x35, 0xff);   // what lies under the panel (the shadow's backdrop)

        /// <summary>A panel object named `name` under `parent`, built and hidden (the prefab root is the same object without parts).</summary>
        public static KitUnitPanel Create(Transform parent, string name)
        {
            var rt = HudKit.NewRect(name, parent);
            var p = rt.gameObject.AddComponent<KitUnitPanel>();
            p.Build();
            return p;
        }
    }
}
