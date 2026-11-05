// The upper half of the title's right column (#title .tr: .sides, .row3, .row2 — ui_spec "타이틀 크기"), built from the kit at x720 y34
// on the 1440×810 title canvas, every block a slant-22 parallelogram (.slant):
//   SideA / SideB  343×176, 10 apart: paper, DecoWedge (side colour .13, skewX −14°), DecoFill (the side colour, comes in from the left
//                  over .5s when chosen), Photo (IGNIS / OBSIDIAN, top-pinned, 120% high, .42 and greyed → .95 when chosen), DecoScrim
//                  (paper → side colour when chosen; opaque to 56%, clear at 88%), Tag (SIDE-A / SIDE-B), Kr 54 (title role), En 19
//                  (display), Desc 13 (body, 3 lines). Hover: up 6.
//   MapCard 520×74 (8 below) + EditorButton 166×74: the minimap (92×70, slant 12, the selected map's own data), MAP // マップ + status
//                  (開始可能 #5DFFA2 or the required-error count), name 21, size · exfil zones · 標準搭載 12, a TacticalButton マップ選択;
//                  the editor button is paper and turns acc yellow, 8 to the left, on hover.
//   WinBox 343×106: VICTORY chip, the chosen side's two conditions 13 (the title's own text, EXFIL and the letter joined by a no-break
//                  space), the first part of its footnote 12.
//   Deploy 343×106: ink, DEPLOY 18 display (side colour), 作戦開始 38 (title role), the arrow block 86 wide in the side colour (brighter
//                  every 1.2s), DecoStripes 8 at the bottom (scrolling); hover: 10 to the left and a light sweep.
// The title's logic stays in TitleSelectScreen: the blocks call its SelectSide / OpenMapList / OpenMapEditor / Deploy and redraw from its
// state (SelectedTeam, SelectedMap, CanStart, the condition / footnote texts) whenever it raises Changed. The mockup's perspective
// rotateY(−7°) of the whole column is replaced by a drop shadow under each block. Entrance (sdIn): each block from +80 and transparent
// in .6s — side cards at 350 + i × 110ms, map 700, editor 800, win box 750, DEPLOY 850. Every motion is a tween on one clock
// (Evaluate(t)); Update advances it with unscaled time (and runs the arrow pulse). Texts change only when the title's state changes.
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Blindside.Data;
using Blindside.MapEditor;

namespace Blindside.UI
{
    [AddComponentMenu("Blindside/UI/Kit Title Top")]
    public class KitTitleTop : MonoBehaviour, ITimedEffect
    {
        public const float X = 720f, Y = 34f, W = 696f, H = 372f, Gap = 10f, RowGap = 8f;
        public const float SideW = 343f, SideH = 176f, MapW = 520f, EdW = 166f, RowH = 74f, BoxH = 106f, ArrowW = 86f;
        public const float EnterShift = 80f, EnterTime = .6f, SelectTime = .5f, FadeTime = .4f, SideHover = 6f, SideHoverTime = .25f;
        public const float EdShift = 8f, StartShift = 10f, HoverTime = .2f, SweepTime = .6f, PulseTime = 1.2f, PulseBright = 1.35f;
        const float Never = -100f, Slant = 22f;
        static readonly float[] Delays = { .35f, .46f, .70f, .80f, .75f, .85f };     // side A, side B, map, editor, win box, DEPLOY
        static readonly Color MenuBg = new Color32(0x0d, 0x10, 0x14, 0xff);
        static readonly Color Ok = new Color32(0x5d, 0xff, 0xa2, 0xff);
        static readonly Color MapInk = new Color32(0x0b, 0x0e, 0x12, 0xff);
        static readonly Color SpanC = new Color32(0xaa, 0xb3, 0xbc, 0xff);
        static readonly Color DescC = new Color32(0x2b, 0x30, 0x36, 0xff);
        static readonly string ExfilNoBreak = "EXFIL" + (char)0xA0;

        struct Tween
        {
            public float from, to, start, dur;
            public float At(float t) => Mathf.LerpUnclamped(from, to, UIEase.Tactical(dur > 0f ? (t - start) / dur : 1f));
            public float End => start + dur;
            public void Settle(float v) { from = to = v; start = 0f; dur = 0f; }
        }

        [System.Serializable]
        public class Side
        {
            public RectTransform body;
            public Image bg;
            public KitTint wedge;
            public RectTransform fill;
            public TacticalPanel fillPanel;
            public UIGlitchImage photo;
            public KitGradient scrim;
            public float photoCover, photoLum;      // the picture under the scrim's ramp: mean alpha and luminance of its opaque part
            public Image tagBox;
            public TMP_Text tag, kr, en, desc;
            public Color color;
        }

        public TitleSelectScreen screen;
        public RectTransform[] blocks = new RectTransform[6], bodies = new RectTransform[6];
        public CanvasGroup[] groups = new CanvasGroup[6];
        public Side[] sides = new Side[2];
        public RawImage minimap;
        public AspectRatioFitter minimapFit;
        public TMP_Text mapTag, mapStatus, mapName, mapSize;
        public TacticalButton mapButton;
        public RectTransform edBody, startBody;
        public Image edBg, arrow;
        public TMP_Text edTag, startEn, winLines, winNote;
        public KitSweep startSweep;
        public KitPointer startPointer;

        Tween[] selT = new Tween[2], hoverT = new Tween[2];
        Tween edT, startT;
        float clock, enterStart = Never, sweepStart = Never;
        bool running, bound;
        Texture2D mapTex;
        MapEntry mapShown;
        string linesShown, noteShown;
        OperatorTeam teamShown = (OperatorTeam)(-1);
        bool canStart = true;

        public float Duration
        {
            get
            {
                float d = enterStart + Delays[5] + EnterTime;
                for (int i = 0; i < 2; i++) d = Mathf.Max(d, Mathf.Max(selT[i].End, hoverT[i].End));
                d = Mathf.Max(d, Mathf.Max(edT.End, startT.End));
                return Mathf.Max(d, sweepStart + SweepTime);
            }
        }

        // ---- the title's state -----------------------------------------------------------------------------------------------
        /// <summary>Hooks the blocks to `title` (its Changed event) and draws its current state.</summary>
        public void Bind(TitleSelectScreen title)
        {
            Build();
            if (bound && screen == title) { Redraw(); return; }
            if (screen != null) screen.Changed -= Redraw;
            screen = title;
            screen.Changed += Redraw;
            bound = true;
            Redraw();
        }

        void OnDestroy()
        {
            if (screen != null) screen.Changed -= Redraw;
            if (mapTex != null) HudKit.DestroySafe(mapTex);
        }

        /// <summary>Everything that follows the title's state: the chosen side, the map, the win condition, START.</summary>
        public void Redraw()
        {
            if (screen == null) return;
            var team = screen.SelectedTeam;
            if (team != teamShown)
            {
                bool first = (int)teamShown < 0;
                teamShown = team;
                Rebase();
                for (int i = 0; i < 2; i++)
                {
                    float to = (i == 0) == (team == OperatorTeam.Attack) ? 1f : 0f;
                    if (first || !Application.isPlaying) selT[i].Settle(to);
                    else { selT[i].from = selT[i].At(clock); selT[i].to = to; selT[i].start = clock; selT[i].dur = SelectTime; }
                }
                Run();
            }

            string lines = screen.conditionsText != null ? screen.conditionsText.text : "";
            if (lines != linesShown) { linesShown = lines; winLines.text = lines.Replace("EXFIL ", ExfilNoBreak); }
            string note = screen.footnoteText != null ? screen.footnoteText.text : "";
            if (note != noteShown)
            {
                noteShown = note;
                int cut = note.IndexOf(" · ", System.StringComparison.Ordinal);      // the mockup's small line: the first part only
                winNote.text = cut > 0 ? note.Substring(0, cut) : note;
            }

            var map = screen.SelectedMap;
            if (map != null && map != mapShown)
            {
                mapShown = map;
                if (mapTex != null) HudKit.DestroySafe(mapTex);
                mapTex = map.file != null ? MapPreview.Make(map.file) : null;
                minimap.texture = mapTex;
                minimap.color = mapTex != null ? Color.white : new Color(1f, 1f, 1f, 0f);
                minimapFit.aspectRatio = map.height > 0 ? map.width / (float)map.height : 1f;
                int exfils = 0;
                if (map.file != null) foreach (var e in map.file.exfils) exfils++;
                mapName.text = map.displayName;
                mapSize.text = map.SizeText + " · 救出地点" + exfils + "か所" + (map.IsDefault ? " · 標準搭載" : "");
                if (map.HasRequiredError) { mapStatus.color = UITheme.Current.foe; mapStatus.text = "必須エラー" + map.errors.Count + "件 — 開始できません"; }
                else { mapStatus.color = Ok; mapStatus.text = "開始可能"; }
                float tagW = Mathf.Ceil(mapTag.GetPreferredValues(mapTag.text).x);
                var sr = mapStatus.rectTransform;
                UILayout.Place(sr, mapTag.rectTransform.anchoredPosition.x + tagW + 10f, -mapTag.rectTransform.anchoredPosition.y,
                    Mathf.Max(10f, InfoRight - (mapTag.rectTransform.anchoredPosition.x + tagW + 10f)), 15f);
            }

            canStart = screen.CanStart;
            if (startPointer != null) startPointer.interactable = canStart;
            Evaluate(clock);
        }

        // ---- motion ------------------------------------------------------------------------------------------------------------
        void OnEnable()
        {
            Build();
            if (Application.isPlaying) PlayEnter();
        }

        /// <summary>sdIn for every block, staggered as in the mockup.</summary>
        public void PlayEnter()
        {
            Rebase();
            enterStart = clock;
            Run();
        }

        void Rebase()
        {
            if (running) return;
            float c = clock;
            for (int i = 0; i < 2; i++) { selT[i].Settle(selT[i].At(c)); hoverT[i].Settle(hoverT[i].At(c)); }
            edT.Settle(edT.At(c)); startT.Settle(startT.At(c));
            enterStart = Never; sweepStart = Never;
            clock = 0f;
        }

        void Run()
        {
            running = Application.isPlaying && isActiveAndEnabled;
            Evaluate(clock);
        }

        void Hover(ref Tween tw, bool on, float dur)
        {
            Rebase();
            tw.from = tw.At(clock); tw.to = on ? 1f : 0f; tw.start = clock; tw.dur = dur;
            Run();
        }

        public void Evaluate(float t)
        {
            var theme = UITheme.Current;
            Color paper = theme.paper, ink = theme.ink, white = Color.white;
            Color sc = teamShown == OperatorTeam.Defense ? theme.foe : theme.blue;

            for (int b = 0; b < 6; b++)
            {
                float p = t - enterStart - Delays[b];
                float e = enterStart <= Never ? 1f : p <= 0f ? 0f : UIEase.Tactical(p / EnterTime);
                if (UIMotionSettings.reduceMotion && enterStart > Never) e = p <= 0f ? 0f : 1f;
                float a = b == 5 && !canStart ? e * .45f : e;
                if (groups[b].alpha != a) groups[b].alpha = a;
                var bp = bodies[b];
                float dx = EnterShift * (1f - e), dy = 0f;
                if (b < 2) dy = SideHover * Mathf.Clamp01(hoverT[b].At(t));
                if (b == 3) dx -= EdShift * Mathf.Clamp01(edT.At(t));
                if (b == 5) dx -= StartShift * Mathf.Clamp01(startT.At(t));
                var pos = new Vector2(dx, dy);
                if (bp.anchoredPosition != pos) bp.anchoredPosition = pos;
            }

            for (int i = 0; i < 2; i++)
            {
                var s = sides[i];
                float k = Mathf.Clamp01(selT[i].At(t)), on = k;
                // .fill: translateX(−101% → 0) skewX(−14°)
                float fx = -1.01f * SideW * (1f - k);
                if (s.fill.anchoredPosition.x != FillX + fx) s.fill.anchoredPosition = new Vector2(FillX + fx, s.fill.anchoredPosition.y);
                // the photo's opacity and the scrim's ramp blend as in sRGB over what lies under them: the paper, or the side colour
                // once the fill is in; under the ramp, the picture too (its measured cover and luminance)
                float pa = Mathf.Lerp(.42f, .95f, on);
                Color under = Color.Lerp(paper, s.color, on);
                s.photo.color = new Color(1f, 1f, 1f, pa);
                s.photo.SetOver(under);
                s.photo.SetTone(Mathf.Lerp(.4f, 1f, on), 1f);
                float pc = s.photoCover * pa;
                var rampUnder = new Color(under.r + (s.photoLum - under.r) * pc, under.g + (s.photoLum - under.g) * pc, under.b + (s.photoLum - under.b) * pc, 1f);
                s.scrim.Set(Color.Lerp(paper, s.color, on), rampUnder);
                s.kr.color = Color.Lerp(ink, white, on);
                s.en.color = Color.Lerp(theme.dim, Color.Lerp(s.color, white, .88f), on);
                s.desc.color = Color.Lerp(DescC, Color.Lerp(s.color, white, .88f), on);
                s.tagBox.color = Color.Lerp(ink, white, on);
                s.tag.color = Color.Lerp(white, ink, on);
            }

            float ed = Mathf.Clamp01(edT.At(t));
            Color edc = Color.Lerp(paper, theme.acc, ed);
            if (edBg.color != edc) edBg.color = edc;

            // .start: the side colour on DEPLOY and the arrow; arPulse (brightness 1.35 at the half of every 1.2s, ease-in-out)
            if (startEn.color != sc) startEn.color = sc;
            float ph = Mathf.Repeat(t, PulseTime) / PulseTime;
            float pulse = Mathf.Lerp(1f, PulseBright, .5f - .5f * Mathf.Cos(ph * Mathf.PI * 2f));
            var ac = new Color(Mathf.Min(1f, sc.r * pulse), Mathf.Min(1f, sc.g * pulse), Mathf.Min(1f, sc.b * pulse), 1f);
            if (arrow.color != ac) arrow.color = ac;
            float sw = t - sweepStart;
            if (sw >= 0f && sw < SweepTime) startSweep.Evaluate(sw); else startSweep.Rest();
        }

        void Update()
        {
            clock += Time.unscaledDeltaTime;          // always: the arrow pulses as long as the title is up
            Evaluate(clock);
            if (running && clock >= Duration) running = false;
            if (!running && clock > 600f) { Rebase(); }  // keep the clock small on a title left open
        }

        // ---- build -------------------------------------------------------------------------------------------------------------
        const float FillX = 0f, InfoX = 116f;
        float InfoRight => MapW - 34f - MapButtonW - 10f;
        const float MapButtonW = 140f, MapButtonH = 40f;

        /// <summary>Builds the blocks on this object (once; the prefab holds only the root).</summary>
        public void Build()
        {
            if (blocks[0] != null) return;
            var theme = UITheme.Current;
            var root = (RectTransform)transform;
            UILayout.Place(root, X, Y, W, H);

            blocks[0] = Block("SideA", 0f, 0f, SideW, SideH, 0);
            blocks[1] = Block("SideB", SideW + Gap, 0f, SideW, SideH, 1);
            blocks[2] = Block("MapCard", 0f, SideH + RowGap, MapW, RowH, 2);
            blocks[3] = Block("EditorButton", MapW + Gap, SideH + RowGap, EdW, RowH, 3);
            blocks[4] = Block("WinBox", 0f, SideH + RowH + 2f * RowGap, SideW, BoxH, 4);
            blocks[5] = Block("Deploy", SideW + Gap, SideH + RowH + 2f * RowGap, SideW, BoxH, 5);

            sides[0] = BuildSide(blocks[0], 0, "SIDE-A", "攻撃", "ASSAULT TEAM", "建物の外へロープで降下し、壁を破壊し、ドローンで偵察しながら人質を護送します。", "IGNIS", theme.blue);
            sides[1] = BuildSide(blocks[1], 1, "SIDE-B", "防衛", "DEFENSE TEAM", "人質のいる部屋で一緒に開始し、壁を強化して制限時間まで人質を守り抜きます。", "OBSIDIAN", theme.foe);
            BuildMapCard(blocks[2]);
            BuildEditor(blocks[3]);
            BuildWinBox(blocks[4]);
            BuildDeploy(blocks[5]);
        }

        // Block (placed, faded) → Body (moved: entrance, hover) → DecoShadow… + Panel (the content)
        RectTransform Block(string name, float x, float y, float w, float h, int i)
        {
            var rt = HudKit.NewRect(name, transform);
            UILayout.Place(rt, x, y, w, h);
            groups[i] = rt.gameObject.AddComponent<CanvasGroup>();
            var body = HudKit.NewRect("Body", rt);
            HudKit.Stretch(body);
            bodies[i] = body;
            return rt;
        }

        // the block's panel: a slant-22 parallelogram that is also the stencil mask its layers are clipped to, with a shadow under it
        // (the shadow stands in for the mockup's perspective rotateY(−7°) of the column)
        Image Clip(RectTransform body, Color c)
        {
            var panel = HudKit.NewRect("Panel", body);
            HudKit.Stretch(panel);
            var img = panel.gameObject.AddComponent<Image>();
            img.color = c; img.raycastTarget = true;
            panel.gameObject.AddComponent<SlantedGraphic>().Set(SlantedGraphic.Size.Large);
            panel.gameObject.AddComponent<Mask>().showMaskGraphic = true;
            KitShadow.Behind(panel, MenuBg, .45f, 10f, 24f);
            return img;
        }

        Side BuildSide(RectTransform block, int index, string tag, string kr, string en, string desc, string photoKey, Color sc)
        {
            var theme = UITheme.Current;
            var s = new Side { color = sc };
            s.bg = Clip(bodies[index], theme.paper);
            var body = s.bg.rectTransform;
            s.body = body;

            // .wedge{left:-40px;width:70%;opacity:.13;skewX(-14deg)}: a parallelogram 240 wide per row around its centre
            float sk = Mathf.Tan(14f * Mathf.Deg2Rad) * SideH, ww = SideW * .7f;
            var wr = HudKit.NewRect("DecoWedge", body);
            UILayout.Place(wr, -40f + ww * .5f - (ww + sk) * .5f, 0f, ww + sk, SideH);
            s.wedge = wr.gameObject.AddComponent<KitTint>();
            s.wedge.raycastTarget = false;
            s.wedge.slant = sk;
            s.wedge.Set(new Color(sc.r, sc.g, sc.b, .13f), theme.paper);

            // .fill{inset:0;skewX(-14deg)} moved by translateX
            var fr = HudKit.NewRect("DecoFill", body);
            UILayout.Place(fr, SideW * .5f - (SideW + sk) * .5f, 0f, SideW + sk, SideH);
            s.fill = fr;
            s.fillPanel = fr.gameObject.AddComponent<TacticalPanel>();
            s.fillPanel.SetPreset(KitColor.Custom); s.fillPanel.color = sc; s.fillPanel.raycastTarget = false;
            fr.gameObject.AddComponent<SlantedGraphic>().Set(SlantedGraphic.Size.Custom, SlantedGraphic.Direction.Right, sk);

            // .bgimg{right:-56px;top:-8px;height:120%}: the whole picture, top-pinned, at its own aspect ratio
            var sprite = OperatorArt.Body(photoKey);
            float ph = SideH * 1.2f, pw = sprite != null ? ph * sprite.rect.width / Mathf.Max(1f, sprite.rect.height) : ph * .75f;
            var pr = HudKit.NewRect("Photo", body);
            UILayout.Place(pr, SideW + 56f - pw, -8f, pw, ph);
            s.photo = pr.gameObject.AddComponent<UIGlitchImage>();
            s.photo.raycastTarget = false;
            s.photo.preset = UIGlitchImage.Preset.Select;
            s.photo.SetSprite(sprite);
            s.photo.Evaluate(10f);
            pr.gameObject.AddComponent<LintIgnoreClip>().reason = "the illustration runs past the card (.bgimg right:-56px, 120% high) and is masked";

            // .scrim: linear-gradient(90deg, C 0 56%, transparent 88%) — the ramp matched to sRGB blending over the picture under it
            // (measured over the 56–88% band, weighted by the ramp: IGNIS cover .421 / luminance .365, OBSIDIAN .666 / .169)
            var scr = HudKit.NewRect("DecoScrim", body);
            HudKit.Stretch(scr);
            s.scrim = scr.gameObject.AddComponent<KitGradient>();
            s.scrim.raycastTarget = false;
            s.scrim.solidTo = .56f; s.scrim.clearAt = .88f;
            s.scrim.Set(theme.paper, theme.paper);
            bool ignis = photoKey == "IGNIS";
            s.photoCover = ignis ? .421f : .666f;
            s.photoLum = ignis ? .365f : .169f;

            // .in{padding:16px 26px 0 40px}
            float tw = SideW - 40f - 26f;
            var tagChip = Chip.Create(body, "Tag", tag, KitColor.Ink, KitColor.White, 11f, 0f);
            tagChip.padX = 10f; tagChip.padY = 3f;
            UIFonts.Apply(tagChip.label, UITheme.TextRole.Display);
            tagChip.label.characterSpacing = 10f;
            tagChip.Fit();
            var tr = (RectTransform)tagChip.transform;
            UILayout.Place(tr, SideW - 38f - tr.sizeDelta.x, 14f, tr.sizeDelta.x, tr.sizeDelta.y);
            s.tagBox = tagChip.box; s.tag = tagChip.label;
            s.kr = HudKit.AddText(body, kr, 54f, theme.ink, true, TextAlignmentOptions.TopLeft, "Kr", UITheme.TextRole.Title);
            s.kr.characterSpacing = -2f;
            UILayout.LineHeight(s.kr, 1f);
            UILayout.Place(s.kr.rectTransform, 40f, 16f, tw, 56f);
            // .kr{54px/1}: the line box is the font size, so the Black face's ascent / descent run past it (the glyphs don't reach En)
            s.kr.gameObject.AddComponent<LintIgnoreClip>().reason = "54px/1 line box: TMP's preferred height counts the font's full ascent and descent";
            s.kr.gameObject.AddComponent<LintIgnoreOverlap>().reason = "54px/1 line box over the 3px gap to En, as in the mockup; the glyphs stay clear";
            s.en = HudKit.AddText(body, en, 19f, theme.dim, true, TextAlignmentOptions.TopLeft, "En", UITheme.TextRole.Display);
            s.en.characterSpacing = 14f;
            UILayout.Place(s.en.rectTransform, 40f, 73f, tw, 24f);
            s.desc = HudKit.AddText(body, desc, 13f, DescC, false, TextAlignmentOptions.TopLeft, "Desc", UITheme.TextRole.Body);
            s.desc.textWrappingMode = TextWrappingModes.Normal;
            UILayout.LineHeight(s.desc, 1.5f);
            UILayout.Place(s.desc.rectTransform, 40f, 105f, 214f, 60f);

            var pt = body.gameObject.AddComponent<KitPointer>();
            pt.onHover = on => Hover(ref hoverT[index], on, SideHoverTime);
            pt.onClick = () => { if (screen != null) screen.SelectSide(index == 0 ? OperatorTeam.Attack : OperatorTeam.Defense); };
            return s;
        }

        void BuildMapCard(RectTransform block)
        {
            var theme = UITheme.Current;
            var body = Clip(bodies[2], theme.ink2).rectTransform;

            // canvas{92×70;margin-left:10px;background:#0b0e12;border-bottom:3px acc;clip-path slant 12}
            var mm = SlantedMask.Create(body, "Minimap", new Vector2(92f, 70f), SlantedGraphic.Size.Custom);
            mm.slant.Set(SlantedGraphic.Size.Custom, SlantedGraphic.Direction.Right, 12f);
            UILayout.Place((RectTransform)mm.transform, 10f, (RowH - 70f) * .5f, 92f, 70f);
            mm.transform.Find("DecoBackdrop").GetComponent<Image>().color = MapInk;
            minimap = mm.photo; minimapFit = mm.fitter;
            minimapFit.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            var pr = minimap.rectTransform; pr.pivot = new Vector2(.5f, .5f);
            var line = HudKit.NewRect("DecoLine", mm.transform);
            line.anchorMin = Vector2.zero; line.anchorMax = new Vector2(1f, 0f); line.pivot = new Vector2(.5f, 0f);
            line.anchoredPosition = Vector2.zero; line.sizeDelta = new Vector2(0f, 3f);
            HudKit.AddImage(line, theme.acc);

            // .mi: small (11) + status · b (21) · span (12), centred
            float iw = InfoRight - InfoX, top = (RowH - 54f) * .5f;
            mapTag = HudKit.AddText(body, "MAP // マップ", 11f, theme.blue, true, TextAlignmentOptions.TopLeft, "MapTag", UITheme.TextRole.Display);
            mapTag.characterSpacing = 10f;
            UILayout.Place(mapTag.rectTransform, InfoX, top, 90f, 15f);
            mapStatus = HudKit.AddText(body, "", 12f, Ok, true, TextAlignmentOptions.TopLeft, "MapStatus", UITheme.TextRole.Title);
            mapStatus.textWrappingMode = TextWrappingModes.NoWrap;
            mapStatus.overflowMode = TextOverflowModes.Ellipsis;
            UILayout.Place(mapStatus.rectTransform, InfoX + 100f, top, iw - 100f, 15f);
            mapName = HudKit.AddText(body, "", 21f, Color.white, true, TextAlignmentOptions.TopLeft, "MapName", UITheme.TextRole.Title);
            mapName.textWrappingMode = TextWrappingModes.NoWrap; mapName.overflowMode = TextOverflowModes.Ellipsis;
            UILayout.LineHeight(mapName, 1.15f);
            UILayout.Place(mapName.rectTransform, InfoX, top + 14f, iw, 25f);
            mapSize = HudKit.AddText(body, "", 12f, SpanC, false, TextAlignmentOptions.TopLeft, "MapSize", UITheme.TextRole.Body);
            mapSize.textWrappingMode = TextWrappingModes.NoWrap; mapSize.overflowMode = TextOverflowModes.Ellipsis;
            UILayout.Place(mapSize.rectTransform, InfoX, top + 39f, iw, 16f);

            mapButton = TacticalButton.Create(body, "MapSelect", "▣", "マップ選択", "MAP", null, new Vector2(MapButtonW, MapButtonH));
            UILayout.Place((RectTransform)mapButton.transform, MapW - 34f - MapButtonW, (RowH - MapButtonH) * .5f, MapButtonW, MapButtonH);
            mapButton.onClick.AddListener(() => { if (screen != null) screen.OpenMapList(); });

            var pt = body.gameObject.AddComponent<KitPointer>();
            pt.onClick = () => { if (screen != null) screen.OpenMapList(); };
        }

        void BuildEditor(RectTransform block)
        {
            var theme = UITheme.Current;
            edBg = Clip(bodies[3], theme.paper);
            var body = edBg.rectTransform;
            edBody = body;
            edTag = HudKit.AddText(body, "MAP EDITOR", 12f, theme.blue, true, TextAlignmentOptions.TopLeft, "EdTag", UITheme.TextRole.Display);
            edTag.characterSpacing = 14f;
            UILayout.Place(edTag.rectTransform, 32f, 14f, EdW - 32f - 16f, 15f);
            var label = HudKit.AddText(body, "マップエディタ", 17f, theme.ink, true, TextAlignmentOptions.TopLeft, "EdLabel", UITheme.TextRole.Title);
            label.textWrappingMode = TextWrappingModes.NoWrap;
            UILayout.Place(label.rectTransform, 32f, 29f, EdW - 32f - 12f, 22f);     // to x154: the slanted right edge at that height
            var pt = body.gameObject.AddComponent<KitPointer>();
            pt.onHover = on => Hover(ref edT, on, HoverTime);
            pt.onClick = () => { if (screen != null) screen.OpenMapEditor(); };
        }

        void BuildWinBox(RectTransform block)
        {
            var theme = UITheme.Current;
            var body = Clip(bodies[4], theme.paper).rectTransform;
            // .wb{padding:9px 30px 0 34px} .chip{12px;padding:3px 10px;letter-spacing:.06em} div.l{13px/1.4} small{12px/1.3}
            float cw = SideW - 34f - 30f;
            var chip = Chip.Create(body, "VictoryChip", "VICTORY // どちらか一方を達成", KitColor.Ink, KitColor.White, 12f, 0f);
            chip.padX = 10f; chip.padY = 3f;
            UIFonts.Apply(chip.label, UITheme.TextRole.Display);
            chip.label.characterSpacing = 6f;
            chip.Fit();
            var cr = (RectTransform)chip.transform;
            UILayout.Place(cr, 34f, 9f, cr.sizeDelta.x, cr.sizeDelta.y);
            float ly = 9f + cr.sizeDelta.y + 1f;
            winLines = HudKit.AddText(body, "", 13f, theme.ink, true, TextAlignmentOptions.TopLeft, "Conditions", UITheme.TextRole.Body);
            winLines.textWrappingMode = TextWrappingModes.Normal;
            winLines.overflowMode = TextOverflowModes.Ellipsis;
            UILayout.LineHeight(winLines, 1.4f);
            UILayout.Place(winLines.rectTransform, 34f, ly, cw, 55f);
            winNote = HudKit.AddText(body, "", 12f, theme.dim, false, TextAlignmentOptions.TopLeft, "Note", UITheme.TextRole.Body);
            winNote.textWrappingMode = TextWrappingModes.NoWrap;
            winNote.overflowMode = TextOverflowModes.Ellipsis;
            UILayout.LineHeight(winNote, 1.3f);
            UILayout.Place(winNote.rectTransform, 34f, ly + 55f + 1f, cw - 12f, 16f);
        }

        void BuildDeploy(RectTransform block)
        {
            var theme = UITheme.Current;
            var body = Clip(bodies[5], theme.ink).rectTransform;
            startBody = body;

            var stripes = HudKit.NewRect("DecoStripes", body);
            UILayout.Place(stripes, 0f, BoxH - 8f, SideW - ArrowW, 8f);
            var hs = HazardStripe.Create(stripes, "DecoStripeFill", theme.acc, theme.ink, 12f, 24f, 1.4f);
            hs.over = theme.ink;

            var ar = HudKit.NewRect("Arrow", body);
            UILayout.Place(ar, SideW - ArrowW, 0f, ArrowW, BoxH);
            arrow = HudKit.AddImage(ar, theme.blue);
            var at = HudKit.AddText(ar, "➜", 34f, Color.white, true, TextAlignmentOptions.Center, "ArrowIcon", UITheme.TextRole.Display);
            HudKit.Stretch(at.rectTransform);
            at.rectTransform.offsetMax = new Vector2(-Slant * .5f, 0f);          // centred in the part left of the slanted edge

            // .t{padding:16px 0 0 42px} .en{18px;letter-spacing:.32em} .kr{38px/1.1}
            float tw = SideW - ArrowW - 42f;
            startEn = HudKit.AddText(body, "DEPLOY", 18f, theme.blue, true, TextAlignmentOptions.TopLeft, "En", UITheme.TextRole.Display);
            startEn.characterSpacing = 32f;
            UILayout.Place(startEn.rectTransform, 42f, 16f, tw, 23f);
            var kr = HudKit.AddText(body, "作戦開始", 38f, Color.white, true, TextAlignmentOptions.TopLeft, "Kr", UITheme.TextRole.Title);
            UILayout.LineHeight(kr, 1.1f);
            UILayout.Place(kr.rectTransform, 42f, 39f, tw, 46f);

            var swr = HudKit.NewRect("DecoSweep", body);
            HudKit.Stretch(swr);
            startSweep = swr.gameObject.AddComponent<KitSweep>();
            startSweep.raycastTarget = false;
            startSweep.lightColor = new Color(1f, 1f, 1f, .4f);
            startSweep.duration = SweepTime;
            startSweep.over = theme.ink;
            startSweep.Rest();

            startPointer = body.gameObject.AddComponent<KitPointer>();
            startPointer.onHover = on =>
            {
                Hover(ref startT, on, HoverTime);
                if (on) { sweepStart = clock; Run(); }
            };
            startPointer.onClick = () => { if (screen != null) screen.Deploy(); };
        }
    }
}
