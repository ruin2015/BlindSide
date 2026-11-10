// The engagement panel, a status comparison in the style of a tactics game's battle forecast: on the left of the screen, turned a
// little away (UIPerspective), the player's operator on top and the enemy at the bottom, both faces, an HP bar each running out from
// the middle, and the two sides' numbers row by row (damage, hit, headshot, rounds) on either side of the labels.
//   forecast (Open)  the preemptive attack's question (askEngage): the rest of the HUD steps aside, each HP bar shows what the
//                    exchange is expected to leave (the part it would take blinks red), one expected to fall is drawn grey with a red
//                    X; 攻撃 [Enter/Y] / 無視して移動 [Esc/N] under the panel (not bent: the buttons' hit areas stay true).
//   aim (OpenAim)    the same forecast while FIRE's aim is on an enemy (the click fires): no dimming, no buttons, a little higher;
//                    the HUD steps aside except the canvases the caller keeps (selection UI, tutorial). Not shown in the battle scene
//                    itself — there the #combat HP bars stay as before.
// One run of the clock (ITimedEffect.Evaluate(t), unscaled): the panel slides in (0 – InTime), the X marks stamp on (XFrom – XTo), the
// HP loss keeps blinking. Nothing allocates per frame. Built at runtime on its own overlay canvas, above every other one.
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Blindside.UI
{
    public class EngagePreview : MonoBehaviour, ITimedEffect
    {
        public const float W = 440f, H = 662f, PanelX = 70f, Pad = 22f, Face = 168f, BarH = 14f, RowH = 42f;
        public const float InTime = .3f, XFrom = .32f, XTo = .5f, BlinkPeriod = .7f, BtnW = 188f, BtnH = 50f, SlideFrom = 60f;
        public const int SortingOrder = 60;
        public const float LiveRaise = 64f;
        const float ValW = 138f, LabelX = Pad + ValW + 2f, LabelW = W - 2f * (Pad + ValW + 2f);
        // the last row is the forecast's result: rounds that hit of those fired, the headshots among them on a red chip
        static readonly string[] RowLabels = { "ダメージ", "命中", "ヘッドショット", "射撃回数", "結果" };

        /// <summary>One side of the exchange as the panel shows it. The numbers are display strings ("34", "78%", "—").</summary>
        public struct Side
        {
            public string key, en, kr, weapon;
            public Color color;
            public bool ally;
            public int hp, maxHp, after;   // HP now and as the exchange is expected to leave it (live: the same)
            public string dmg, hit, hs, shots;
            public bool preempt;           // its volley is the preemptive one (×1.4 chip by the damage)
            public string result;          // the forecast: "2/3" rounds hit ("BLOCK": the shield takes them), null: no volley
            public int crits, resultTotal; // headshots among the hits (CRIT ×n chip), and the damage they all make
            public bool Dies => after <= 0;
        }

        class Column
        {
            public UIGlitchImage face;
            public RectTransform xMark, kiaChip, preChip, critChip, lossRt, afterRt;
            public TMP_Text critLabel;
            public Image fillAfter, fillLoss, plate;
            public TMP_Text en, weapon, hpText;
            public TMP_Text[] vals = new TMP_Text[5];
            public bool left;
            public Side side;
        }

        public Canvas canvas;
        public RectTransform panel, buttons;
        public CanvasGroup group;
        public TMP_Text tag, title, note;
        Image dim;
        Column top, bottom;
        float clock = 99f;
        bool open, live;
        System.Action onYes, onNo;
        readonly List<Canvas> hidden = new List<Canvas>();

        public float Duration => XTo;
        public bool IsOpen => open;
        public bool IsLive => live;

        // ================================================================
        //  Use
        // ================================================================
        /// <summary>The forecast of `a` (the one who fires first, the player's) against `b`; every other overlay canvas is switched off
        /// until Close.</summary>
        public void Open(Side a, Side b, System.Action yes, System.Action no, bool hideOthers = true)
        {
            onYes = yes; onNo = no;
            live = false;
            tag.text = "AMBUSH // ENGAGEMENT FORECAST";
            title.text = "敵を発見 — 攻撃しますか？";
            Fill(top, a); Fill(bottom, b);
            dim.enabled = true;
            buttons.gameObject.SetActive(true);
            note.gameObject.SetActive(true);
            if (open) RestoreOthers();
            Show(true);
            if (hideOthers) HideOthers(); else hidden.Clear();
        }

        /// <summary>FIRE's aim on an enemy: the same forecast while the player picks the target — no buttons (the click fires), no
        /// dimming, a little higher (clear of the unit panel). The HUD canvases step aside except the ones named in `keep` (the
        /// selection UI with its aim tip and hint bar, the tutorial's).</summary>
        public void OpenAim(Side a, Side b, bool hideOthers, params string[] keep)
        {
            onYes = onNo = null;
            live = true;
            tag.text = "FIRE // ENGAGEMENT FORECAST";
            title.text = "攻撃予測";
            Fill(top, a); Fill(bottom, b);
            dim.enabled = false;
            buttons.gameObject.SetActive(false);
            note.gameObject.SetActive(false);
            bool was = open;
            Show(!was);   // (re-aimed at another enemy: the numbers change, no new entrance)
            if (!was && hideOthers) HideOthers(keep);
        }

        void Show(bool entrance)
        {
            gameObject.SetActive(true);
            canvas.enabled = true;
            open = true;
            if (entrance) clock = 0f;
            Evaluate(clock);
        }

        public void Close()
        {
            if (!open) return;
            open = false; live = false;
            RestoreOthers();
            gameObject.SetActive(false);
        }

        void RestoreOthers()
        {
            foreach (var c in hidden) if (c != null) c.enabled = true;
            hidden.Clear();
        }

        void HideOthers(params string[] keep)
        {
            hidden.Clear();
            foreach (var c in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
            {
                if (c == canvas || !c.enabled || !c.isRootCanvas || c.renderMode != RenderMode.ScreenSpaceOverlay) continue;
                if (c.name == "BlindsideCanvas") continue;   // (the fade and letterbox)
                if (keep != null && System.Array.IndexOf(keep, c.name) >= 0) continue;
                c.enabled = false; hidden.Add(c);
            }
        }

        static void Fill(Column c, Side s)
        {
            c.side = s;
            var face = OperatorArt.Face(s.key);
            if (face != null)
            {
                // cover the square slot: the middle of a wide face, the top of a tall one
                var tex = face.texture; var r = face.textureRect;
                float a = r.width / Mathf.Max(1f, r.height);
                Rect uv = a >= 1f ? new Rect(r.x + (r.width - r.height) * .5f, r.y, r.height, r.height)
                                  : new Rect(r.x, r.y + r.height - r.width, r.width, r.width);
                c.face.SetTexture(tex, new Rect(uv.x / tex.width, uv.y / tex.height, uv.width / tex.width, uv.height / tex.height));
            }
            c.face.enabled = face != null;
            var team = s.ally ? UIPalette.Blue : UIPalette.Foe;
            c.plate.color = new Color(team.r, team.g, team.b, .92f);
            c.en.text = s.en;
            c.weapon.text = string.IsNullOrEmpty(s.weapon) ? s.kr : $"{s.kr}  <color=#FFFFFFB0>{s.weapon}</color>";
            FillHp(c);
            FillVals(c);
        }

        static void FillHp(Column c)
        {
            var s = c.side;
            bool dies = s.Dies;
            c.face.SetTone(dies ? 0f : 1f, dies ? .55f : 1f);
            c.xMark.gameObject.SetActive(dies);
            c.kiaChip.gameObject.SetActive(dies);
            float barW = W * .5f - Pad - 24f;
            float now = s.maxHp > 0 ? Mathf.Clamp01((float)s.hp / s.maxHp) : 0f;
            float after = s.maxHp > 0 ? Mathf.Clamp01((float)Mathf.Max(0, s.after) / s.maxHp) : 0f;
            c.afterRt.sizeDelta = new Vector2(barW * after, BarH);
            c.fillAfter.color = s.ally ? UIPalette.Blue : s.color;
            // the loss: from where the HP will be to where it is now, on the outer side of the remaining bar
            c.lossRt.anchoredPosition = new Vector2((c.left ? -1f : 1f) * barW * after, 0f);
            c.lossRt.sizeDelta = new Vector2(barW * Mathf.Max(0f, now - after), BarH);
            int a = Mathf.Max(0, s.after);
            c.hpText.text = a == s.hp ? s.hp.ToString() : $"{s.hp}<size=70%><color=#6A7078> > </color></size><color=#{(dies ? "E5364D" : "FFFFFF")}>{a}</color>";
        }

        static void FillVals(Column c)
        {
            var s = c.side;
            c.vals[0].text = s.dmg ?? "—";
            c.vals[1].text = s.hit ?? "—";
            c.vals[2].text = s.hs ?? "—";
            c.vals[3].text = s.shots ?? "—";
            c.vals[4].text = s.result == null ? "—" : s.resultTotal > 0 ? $"{s.result} <size=60%><color=#9AA0A8>-{s.resultTotal}</color></size>" : s.result;
            c.preChip.gameObject.SetActive(s.preempt);
            c.critChip.gameObject.SetActive(s.crits > 0);
            if (s.crits > 0) c.critLabel.text = "CRIT ×" + s.crits;
        }

        void Update()
        {
            if (!open) return;
            if (!live)
            {
                var kb = UnityEngine.InputSystem.Keyboard.current;
                if (kb != null)
                {
                    if (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame || kb.yKey.wasPressedThisFrame) { onYes?.Invoke(); return; }
                    if (kb.escapeKey.wasPressedThisFrame || kb.nKey.wasPressedThisFrame) { onNo?.Invoke(); return; }
                }
            }
            clock += Time.unscaledDeltaTime;
            Evaluate(clock);
        }

        public void Evaluate(float t)
        {
            float k = UIEase.Tactical(Mathf.Clamp01(t / InTime));
            group.alpha = k;
            // (aiming: a little higher, clear of the unit panel bottom left)
            panel.anchoredPosition = new Vector2(PanelX - SlideFrom * (1f - k), live ? LiveRaise : 0f);
            // the X marks stamp on: big and faint → their size
            float x = Mathf.Clamp01((t - XFrom) / (XTo - XFrom));
            float xs = Mathf.Lerp(1.6f, 1f, UIEase.Tactical(x));
            Stamp(top, x, xs); Stamp(bottom, x, xs);
            // the HP the exchange would take blinks
            float b = .45f + .55f * (.5f + .5f * Mathf.Cos(Mathf.Repeat(t, BlinkPeriod) / BlinkPeriod * Mathf.PI * 2f));
            var lc = new Color(UIPalette.Foe.r, UIPalette.Foe.g, UIPalette.Foe.b, b);
            top.fillLoss.color = lc; bottom.fillLoss.color = lc;
        }

        static void Stamp(Column c, float p, float scale)
        {
            if (!c.xMark.gameObject.activeSelf) return;
            c.xMark.localScale = new Vector3(scale, scale, 1f);
            var g = c.xMark.GetComponent<CanvasGroup>();
            if (g != null) g.alpha = p;
        }

        // ================================================================
        //  Construction
        // ================================================================
        /// <param name="onCanvas">build onto this (HUD-scaled) canvas instead of a new overlay one (Render Previews' stage)</param>
        public static EngagePreview Build(Transform parent, Canvas onCanvas = null)
        {
            var c = onCanvas;
            if (c == null)
            {
                var cgo = new GameObject("EngagePreview_Canvas", typeof(RectTransform));
                cgo.transform.SetParent(parent, false);
                c = cgo.AddComponent<Canvas>();
                c.renderMode = RenderMode.ScreenSpaceOverlay;
                c.sortingOrder = SortingOrder;
                UITheme.Current.ApplyTo(cgo.AddComponent<CanvasScaler>(), UITheme.CanvasKind.Hud);
                cgo.AddComponent<GraphicRaycaster>();
            }
            var root = NewRect("EngagePreview", c.transform);
            Stretch(root);
            var p = root.gameObject.AddComponent<EngagePreview>();
            p.canvas = c;
            p.group = root.gameObject.AddComponent<CanvasGroup>();

            // the field behind, darkened (and every press caught) — forecast only
            var dim = NewRect("DecoDim", root); Stretch(dim);
            p.dim = Img(dim, new Color(.03f, .035f, .045f, .62f));
            p.dim.raycastTarget = true;

            // the panel: left middle of the screen, its pivot on its left edge (the bend turns about it)
            var panel = NewRect("Panel", root);
            panel.anchorMin = panel.anchorMax = new Vector2(0f, .5f); panel.pivot = new Vector2(0f, .5f);
            panel.sizeDelta = new Vector2(W, H); panel.anchoredPosition = new Vector2(PanelX, 0f);
            p.panel = panel;
            var bg = NewRect("DecoPanel", panel); Stretch(bg);
            Img(bg, new Color(UIPalette.Ink.r, UIPalette.Ink.g, UIPalette.Ink.b, .94f));
            var topLine = NewRect("DecoTopLine", panel); Place(topLine, 0, 0, W, 4); Img(topLine, UIPalette.Foe);

            p.tag = Text(panel, "AMBUSH // ENGAGEMENT FORECAST", 12f, UIPalette.Foe, UITheme.TextRole.Display, "Tag");
            p.tag.characterSpacing = 5f;
            Place(p.tag.rectTransform, Pad, 14, W - Pad * 2f, 16);
            p.title = Text(panel, "敵を発見 — 攻撃しますか？", 24f, Color.white, UITheme.TextRole.Title, "Title");
            Place(p.title.rectTransform, Pad, 32, W - Pad * 2f, 34);

            p.top = BuildPlate(panel, 76f, true);
            // the faces, side by side
            p.top.left = true;
            p.bottom = BuildPlate(panel, H - 44f - 14f, false);
            BuildFace(panel, p.top, Pad, 130f, true);
            BuildFace(panel, p.bottom, W - Pad - Face, 130f, false);
            var vs = Text(panel, "VS", 30f, UIPalette.Accent, UITheme.TextRole.Display, "Vs");
            vs.alignment = TextAlignmentOptions.Center;
            Place(vs.rectTransform, Pad + Face, 130f + Face * .5f - 20f, W - 2f * (Pad + Face), 40);

            // HP: the numbers over the bars, "HP" in the middle, each bar running out from the middle
            float hy = 130f + Face + 16f;
            var hpTag = NewRect("HpTag", panel); Place(hpTag, W * .5f - 24f, hy + 6f, 48, 26);
            Img(hpTag, UIPalette.Ink2);
            var hpLabel = Text(hpTag, "HP", 15f, Color.white, UITheme.TextRole.Display, "Label");
            hpLabel.alignment = TextAlignmentOptions.Center; Stretch(hpLabel.rectTransform);
            BuildHp(panel, p.top, hy, true);
            BuildHp(panel, p.bottom, hy, false);

            // the comparison rows
            float ry = hy + 62f;
            for (int i = 0; i < RowLabels.Length; i++)
            {
                float y = ry + i * RowH;
                var row = NewRect("DecoRow" + i, panel); Place(row, Pad, y, W - Pad * 2f, RowH - 4f);
                Img(row, new Color(1f, 1f, 1f, i % 2 == 0 ? .05f : .02f));
                var lab = NewRect("RowLabel" + i, panel); Place(lab, LabelX, y + 6f, LabelW, RowH - 16f);
                Img(lab, UIPalette.Ink2);
                var lt = Text(lab, RowLabels[i], 13f, new Color(.78f, .8f, .84f), UITheme.TextRole.Body, "Label");
                lt.alignment = TextAlignmentOptions.Center; Stretch(lt.rectTransform);
                p.top.vals[i] = Value(panel, Pad, y, UIPalette.Blue);
                p.bottom.vals[i] = Value(panel, W - Pad - ValW, y, UIPalette.Foe);
            }
            p.top.preChip = PreChip(panel, Pad + ValW - 50f, ry + 2f);
            p.bottom.preChip = PreChip(panel, W - Pad - 50f, ry + 2f);
            float resY = ry + 4f * RowH;
            p.top.critChip = CritChip(panel, Pad + ValW - 70f, resY + 2f, out p.top.critLabel);
            p.bottom.critChip = CritChip(panel, W - Pad - 70f, resY + 2f, out p.bottom.critLabel);

            // bend the whole panel (buttons stay outside it)
            var warp = panel.gameObject.AddComponent<UIPerspective>();
            warp.Apply();

            // under it: what happens either way, and the two answers (forecast only)
            var btns = NewRect("Buttons", root);
            btns.anchorMin = btns.anchorMax = new Vector2(0f, .5f); btns.pivot = new Vector2(0f, 1f);
            btns.sizeDelta = new Vector2(W, BtnH + 66f); btns.anchoredPosition = new Vector2(PanelX, -H * .5f - 14f);
            p.buttons = btns;
            Button(btns, "Attack", "攻撃", "ENTER", UIPalette.Foe, 0f, () => p.onYes?.Invoke());
            Button(btns, "Ignore", "無視して移動", "ESC", UIPalette.Ink2, BtnW + 14f, () => p.onNo?.Invoke());
            p.note = Text(btns, "攻撃すると、この場で移動を終了します。\n無視すると、描いた経路のまま移動を続けます。\n<color=#6A7078>予測はクリティカルを含め、実際の交戦結果と同じです</color>", 12f, new Color(.62f, .65f, .7f), UITheme.TextRole.Body, "Note");
            p.note.textWrappingMode = TextWrappingModes.Normal;
            Place(p.note.rectTransform, 0, BtnH + 10f, W, 54);

            root.gameObject.SetActive(false);
            return p;
        }

        // the name plate: codename + kana / weapon, in the side's colour (top: the player's, bottom: the enemy)
        static Column BuildPlate(RectTransform panel, float y, bool isTop)
        {
            var c = new Column();
            var plate = NewRect(isTop ? "TopPlate" : "BottomPlate", panel); Place(plate, Pad, y, W - Pad * 2f, 44);
            c.plate = Img(plate, UIPalette.Blue);
            c.en = Text(plate, "", 22f, Color.white, UITheme.TextRole.Display, "En");
            Place(c.en.rectTransform, 12, 9, 170, 28);
            c.weapon = Text(plate, "", 13f, Color.white, UITheme.TextRole.Body, "Weapon");
            c.weapon.alignment = TextAlignmentOptions.MidlineRight;
            Place(c.weapon.rectTransform, 170, 0, W - Pad * 2f - 182f, 44);
            return c;
        }

        static void BuildFace(RectTransform panel, Column c, float x, float y, bool isTop)
        {
            var frame = NewRect(isTop ? "TopFace" : "BottomFace", panel); Place(frame, x, y, Face, Face);
            Img(frame, isTop ? UIPalette.Blue : UIPalette.Foe);
            var paper = NewRect("Paper", frame); Stretch(paper);
            paper.offsetMin = new Vector2(4f, 4f); paper.offsetMax = new Vector2(-4f, -4f);
            var pi = Img(paper, Color.white); pi.sprite = OperatorArt.PaperGradient;
            var f = NewRect("Face", paper); Stretch(f);
            c.face = f.gameObject.AddComponent<UIGlitchImage>();
            c.face.raycastTarget = false;
            // the X (expected to fall)
            var xm = NewRect("KiaMark", frame);
            xm.anchorMin = xm.anchorMax = new Vector2(.5f, .5f); xm.pivot = new Vector2(.5f, .5f);
            xm.sizeDelta = new Vector2(Face, Face); xm.anchoredPosition = Vector2.zero;
            xm.gameObject.AddComponent<CanvasGroup>();
            for (int i = 0; i < 2; i++)
            {
                var bar = NewRect("XBar" + i, xm);
                bar.anchorMin = bar.anchorMax = new Vector2(.5f, .5f); bar.pivot = new Vector2(.5f, .5f);
                bar.sizeDelta = new Vector2(14f, Face * 1.2f); bar.anchoredPosition = Vector2.zero;
                bar.localRotation = Quaternion.Euler(0f, 0f, i == 0 ? 45f : -45f);
                Img(bar, new Color(UIPalette.Foe.r, UIPalette.Foe.g, UIPalette.Foe.b, .92f));
            }
            c.xMark = xm;
            xm.gameObject.SetActive(false);
            var kia = NewRect("KiaChip", frame);
            kia.anchorMin = kia.anchorMax = new Vector2(.5f, 0f); kia.pivot = new Vector2(.5f, 0f);
            kia.sizeDelta = new Vector2(150f, 26f); kia.anchoredPosition = new Vector2(0f, 8f);
            Img(kia, UIPalette.Foe);
            var kiaEn = Text(kia, "K.I.A", 13f, Color.white, UITheme.TextRole.Display, "KiaEn");
            Place(kiaEn.rectTransform, 10, 5, 52, 18);
            var kiaJa = Text(kia, "撃破予測", 13f, Color.white, UITheme.TextRole.Body, "KiaJa");
            Place(kiaJa.rectTransform, 62, 3, 82, 20);
            c.kiaChip = kia;
            kia.gameObject.SetActive(false);
        }

        static void BuildHp(RectTransform panel, Column c, float y, bool left)
        {
            c.left = left;
            float barW = W * .5f - Pad - 24f;
            c.hpText = Text(panel, "", 22f, Color.white, UITheme.TextRole.Display, left ? "TopHp" : "BottomHp");
            c.hpText.alignment = left ? TextAlignmentOptions.TopLeft : TextAlignmentOptions.TopRight;
            Place(c.hpText.rectTransform, left ? Pad : W * .5f + 24f, y - 2f, barW, 28);
            var track = NewRect(left ? "TopHpBar" : "BottomHpBar", panel);
            Place(track, left ? Pad : W * .5f + 24f, y + 30f, barW, BarH);
            Img(track, UIPalette.Ink2);
            // fills start at the middle end of the track
            var after = NewRect("HpAfter", track);
            after.anchorMin = after.anchorMax = new Vector2(left ? 1f : 0f, .5f); after.pivot = new Vector2(left ? 1f : 0f, .5f);
            after.anchoredPosition = Vector2.zero;
            c.fillAfter = Img(after, UIPalette.Blue);
            c.afterRt = after;
            var loss = NewRect("HpLoss", track);
            loss.anchorMin = loss.anchorMax = new Vector2(left ? 1f : 0f, .5f); loss.pivot = new Vector2(left ? 1f : 0f, .5f);
            c.fillLoss = Img(loss, UIPalette.Foe);
            c.lossRt = loss;
            for (int i = 1; i < 8; i++)
            {
                var tick = NewRect("DecoTick" + i, track);
                tick.anchorMin = tick.anchorMax = new Vector2(0f, .5f); tick.pivot = new Vector2(.5f, .5f);
                tick.sizeDelta = new Vector2(2f, BarH); tick.anchoredPosition = new Vector2(barW * i / 8f, 0f);
                Img(tick, new Color(UIPalette.Ink.r, UIPalette.Ink.g, UIPalette.Ink.b, .7f));
            }
        }

        static TMP_Text Value(RectTransform panel, float x, float y, Color accent)
        {
            var t = Text(panel, "—", 22f, Color.white, UITheme.TextRole.Display, "Value");
            t.alignment = TextAlignmentOptions.Center;
            Place(t.rectTransform, x, y + 6f, ValW, RowH - 14f);
            var edge = NewRect("DecoValueEdge", panel); Place(edge, x, y + RowH - 7f, ValW, 2f);
            Img(edge, new Color(accent.r, accent.g, accent.b, .55f));
            return t;
        }

        static RectTransform PreChip(RectTransform panel, float x, float y)
        {
            var chip = NewRect("PreemptChip", panel); Place(chip, x, y, 48, 16);
            Img(chip, UIPalette.Accent);
            var t = Text(chip, "×1.4", 11f, UIPalette.Ink, UITheme.TextRole.Display, "Label");
            t.alignment = TextAlignmentOptions.Center; Stretch(t.rectTransform);
            chip.gameObject.SetActive(false);
            return chip;
        }

        static RectTransform CritChip(RectTransform panel, float x, float y, out TMP_Text label)
        {
            var chip = NewRect("CritChip", panel); Place(chip, x, y, 68, 16);
            Img(chip, UIPalette.Foe);
            label = Text(chip, "CRIT ×1", 11f, Color.white, UITheme.TextRole.Display, "Label");
            label.alignment = TextAlignmentOptions.Center; Stretch(label.rectTransform);
            chip.gameObject.SetActive(false);
            return chip;
        }

        static void Button(RectTransform parent, string name, string label, string key, Color bg, float x, System.Action click)
        {
            var b = NewRect(name, parent); Place(b, x, 0f, BtnW, BtnH);
            var img = Img(b, bg); img.raycastTarget = true;
            var btn = b.gameObject.AddComponent<UnityEngine.UI.Button>();
            btn.targetGraphic = img;
            btn.onClick.AddListener(() => click());
            var t = Text(b, label, 17f, Color.white, UITheme.TextRole.Body, "Label");
            t.alignment = TextAlignmentOptions.MidlineLeft;
            Place(t.rectTransform, 14, 0, BtnW - 80, BtnH);
            var k = Text(b, key, 12f, new Color(1f, 1f, 1f, .7f), UITheme.TextRole.Display, "Key");
            k.alignment = TextAlignmentOptions.MidlineRight;
            Place(k.rectTransform, BtnW - 70, 0, 58, BtnH);
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
