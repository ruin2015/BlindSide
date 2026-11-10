// The HQ menu's コープ: FLARE's co-op with each squad member (Progress.Campaign), laid out like a confidant screen (menu canvas 1440 × 810).
//   List     ink ground, a wide band (blue) leaning across it, FLARE's illustration on the right (820², bleeding off the bottom).
//            Top left the tag 「コープ」 (ink plate 330 × 58, "CO-OP" blue). The members cascade down the left, each a little further
//            right and tilted 3°: the role (ink2 plate 150 × 24, white 13) over a slanted paper strip 400 × 42 — the codename 22 display +
//            kana 12, then "RANK" 11 and the rank 30 (blue; MAX at the top) — and a 400 × 5 bar of the way to the next rank under it. The
//            selected strip turns blue (white text), steps 18 right and gets ➜. Bottom left: 「誰の詳細を確認する？」 in a slanted paper box;
//            bottom right the 操作説明; the back bar top right.
//   Detail   the member's illustration on the left over a band in their colour; bottom left a paper plate: the codename 46 + kana, and
//            the description. Right: an ink card — "Q / E" to step through the members, the face on a paper card with the role, "ROLE"
//            and the role 30, "RANK" and the rank 64 (gold), ten diamonds (filled up to the rank), 親密度 points and a bar to the next
//            rank; under it the episode of this rank (CoopStories: 第N話 + its text, a placeholder until written); then the perks by rank
//            (CoopPerks — unlocked white, the rest dim with the rank that unlocks them).
// W / S (↑ ↓) or the pointer picks, F / Enter / a click opens the detail; Q / E (A / D, ← →) step through it; ESC goes back (detail →
// list → closed). Entrance: the screen fades in (.2) and the strips slide in from the left 50 ms apart (.35 each); the detail's art from
// the left and its card from the right. ITimedEffect.Evaluate(t), unscaled; texts change only when the member or the view changes.
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Blindside.Data;
using Blindside.Hub;
using Blindside.Progress;
using Blindside.Save;

namespace Blindside.UI
{
    public class HubCoopScreen : MonoBehaviour, ITimedEffect
    {
        public const float W = 1440f, H = 810f;
        public const float RowX = 70f, RowY = 112f, RowStep = 84f, RowSlant = 18f, StripW = 400f, StripH = 42f, RowTilt = 3f;
        public const float FadeIn = .2f, RowIn = .35f, RowGap = .05f, SideIn = .35f;
        const int MaxRows = 8, Stars = 10;

        HubScreen screen;
        CanvasGroup group;
        RectTransform listView, detailView, flareArt, card, epBox, perkBox, dArtRt;
        readonly List<OperatorData> ops = new List<OperatorData>(8);
        int sel;
        bool detail;
        float clock;

        sealed class Row
        {
            public RectTransform rt; public Image strip, bar; public TMP_Text name, kana, rankLbl, rank, role, arrow; public Vector2 home;
        }
        readonly Row[] rows = new Row[MaxRows];

        Image dArt, dBand, dFace;
        TMP_Text dName, dKana, dDesc, dRole, dRank, dCardRole, dPoints, dEpTitle, dEpText, dHeadName;
        Image dBar;
        readonly Image[] stars = new Image[Stars];
        readonly List<TMP_Text> perkTexts = new List<TMP_Text>(10);
        Vector2 cardHome, artHome, epHome, perkHome;

        public bool IsOpen => gameObject.activeSelf;
        SaveData D => screen.Data;

        // ---- build -------------------------------------------------------------------------------------------------------------------
        public static HubCoopScreen Build(Transform parent, HubScreen screen)
        {
            var theme = UITheme.Current;
            var root = HudKit.NewRect("HubCoop", parent);
            HudKit.Stretch(root);
            var s = root.gameObject.AddComponent<HubCoopScreen>();
            s.screen = screen;
            s.group = root.gameObject.AddComponent<CanvasGroup>();
            HudKit.AddImage(root, theme.ink, raycast: true);

            s.listView = HudKit.NewRect("List", root); HudKit.Stretch(s.listView);
            s.detailView = HudKit.NewRect("Detail", root); HudKit.Stretch(s.detailView);
            s.BuildList(theme);
            s.BuildDetail(theme);

            var back = HubTiles.Back(root, s.Back);
            UILayout.Place(back, W - 40f - 176f, 30f, 176f, 46f);
            root.gameObject.SetActive(false);
            return s;
        }

        static RectTransform Band(RectTransform parent, string name, Color c, float x, float y, float w, float h, float angle)
        {
            var b = HudKit.NewRect(name, parent);
            UILayout.Place(b, x, y, w, h);
            b.localRotation = Quaternion.Euler(0f, 0f, angle);
            HudKit.AddImage(b, c);
            return b;
        }

        void BuildList(UITheme theme)
        {
            Band(listView, "DecoBand", new Color(theme.blue.r, theme.blue.g, theme.blue.b, .78f), 380f, 300f, 1500f, 300f, 24f);
            Band(listView, "DecoBandThin", new Color(1f, 1f, 1f, .08f), 260f, 560f, 1600f, 40f, 24f);
            flareArt = HudKit.NewRect("FlareArt", listView);
            UILayout.Place(flareArt, 640f, 40f, 820f, 820f);
            var fa = flareArt.gameObject.AddComponent<Image>();
            fa.sprite = OperatorArt.Body(Campaign.Commander); fa.preserveAspect = true; fa.raycastTarget = false; fa.enabled = fa.sprite != null;

            Tag(listView, theme, "コープ", "CO-OP");

            for (int i = 0; i < MaxRows; i++) rows[i] = BuildRow(theme, i);

            var ask = HudKit.NewRect("Ask", listView);
            UILayout.Place(ask, 60f, H - 104f, 440f, 50f);
            HudKit.AddImage(ask, theme.paper);
            ask.gameObject.AddComponent<SlantedGraphic>().Set(SlantedGraphic.Size.Custom, SlantedGraphic.Direction.Right, 14f);
            var at = HudKit.AddText(ask, "誰の詳細を確認する？", 18f, theme.ink, true, TextAlignmentOptions.MidlineLeft, "Text", UITheme.TextRole.Title);
            UILayout.Place(at.rectTransform, 24f, 0f, 400f, 50f);
            HubTiles.Help(listView, W - 40f - 240f, H - 18f - 30f - 3 * 34f, false, "W S", "選択", "F", "詳細", "ESC", "戻る");
        }

        void Tag(RectTransform parent, UITheme theme, string name, string en)
        {
            var plate = HudKit.NewRect("Tag", parent);
            UILayout.Place(plate, 0f, 24f, 330f, 58f);
            HudKit.AddImage(plate, theme.ink2);
            plate.gameObject.AddComponent<SlantedGraphic>().Set(SlantedGraphic.Size.Custom, SlantedGraphic.Direction.Right, 18f);
            var t = HudKit.AddText(plate, name, 24f, Color.white, true, TextAlignmentOptions.MidlineLeft, "Name", UITheme.TextRole.Title);
            UILayout.Place(t.rectTransform, 40f, 0f, 200f, 58f);
            var e = HudKit.AddText(plate, en, 11f, theme.blue, true, TextAlignmentOptions.MidlineRight, "En", UITheme.TextRole.Display);
            e.characterSpacing = 12f;
            UILayout.Place(e.rectTransform, 180f, 0f, 120f, 58f);
        }

        Row BuildRow(UITheme theme, int i)
        {
            var r = new Row();
            r.rt = HudKit.NewRect("Member" + i, listView);
            r.home = new Vector2(RowX + i * RowSlant, RowY + i * RowStep);
            UILayout.Place(r.rt, r.home.x, r.home.y, StripW + 40f, 76f);
            r.rt.localRotation = Quaternion.Euler(0f, 0f, RowTilt);
            var rolePlate = HudKit.NewRect("RolePlate", r.rt);
            UILayout.Place(rolePlate, 0f, 0f, 150f, 24f);
            HudKit.AddImage(rolePlate, theme.ink2);
            rolePlate.gameObject.AddComponent<SlantedGraphic>().Set(SlantedGraphic.Size.Custom, SlantedGraphic.Direction.Right, 8f);
            r.role = HudKit.AddText(rolePlate, "", 13f, Color.white, true, TextAlignmentOptions.MidlineLeft, "Role", UITheme.TextRole.Body);
            r.role.textWrappingMode = TextWrappingModes.NoWrap;
            UILayout.Place(r.role.rectTransform, 12f, 0f, 134f, 24f);

            var strip = HudKit.NewRect("Strip", r.rt);
            UILayout.Place(strip, 0f, 24f, StripW, StripH);
            r.strip = HudKit.AddImage(strip, theme.paper, raycast: true);
            strip.gameObject.AddComponent<SlantedGraphic>().Set(SlantedGraphic.Size.Custom, SlantedGraphic.Direction.Right, 12f);
            var btn = strip.gameObject.AddComponent<Button>(); btn.transition = Selectable.Transition.None; btn.targetGraphic = r.strip;
            int idx = i;
            btn.onClick.AddListener(() => { Sfx.Ui(); Select(idx); OpenDetail(); });
            strip.gameObject.AddComponent<PointerEnter>().onEnter = () => Select(idx);
            r.name = HudKit.AddText(strip, "", 22f, theme.ink, true, TextAlignmentOptions.MidlineLeft, "Name", UITheme.TextRole.Display);
            r.name.textWrappingMode = TextWrappingModes.NoWrap;
            UILayout.Place(r.name.rectTransform, 22f, 0f, 170f, StripH);
            r.kana = HudKit.AddText(strip, "", 12f, theme.dim, false, TextAlignmentOptions.MidlineLeft, "Kana", UITheme.TextRole.Body);
            r.kana.textWrappingMode = TextWrappingModes.NoWrap;
            UILayout.Place(r.kana.rectTransform, 190f, 2f, 100f, StripH);
            r.rankLbl = HudKit.AddText(strip, "RANK", 11f, theme.dim, true, TextAlignmentOptions.MidlineRight, "RankLbl", UITheme.TextRole.Display);
            UILayout.Place(r.rankLbl.rectTransform, StripW - 176f, 0f, 70f, StripH);
            r.rank = HudKit.AddText(strip, "", 30f, theme.blue, true, TextAlignmentOptions.MidlineRight, "Rank", UITheme.TextRole.Display);
            r.rank.textWrappingMode = TextWrappingModes.NoWrap;
            UILayout.Place(r.rank.rectTransform, StripW - 102f, 0f, 90f, StripH);
            r.arrow = HudKit.AddText(r.rt, "➜", 22f, theme.acc, true, TextAlignmentOptions.Center, "Arrow", UITheme.TextRole.Display);
            HudKit.UseSymbolFont(r.arrow);
            UILayout.Place(r.arrow.rectTransform, -34f, 24f, 30f, StripH);

            var track = HudKit.NewRect("Track", r.rt);
            UILayout.Place(track, 6f, 24f + StripH + 4f, StripW - 20f, 5f);
            HudKit.AddImage(track, new Color(1f, 1f, 1f, .18f));
            var fill = HudKit.NewRect("Fill", track);
            fill.anchorMin = Vector2.zero; fill.anchorMax = new Vector2(0f, 1f); fill.pivot = new Vector2(0f, .5f); fill.offsetMin = fill.offsetMax = Vector2.zero;
            r.bar = HudKit.AddImage(fill, theme.acc);
            return r;
        }

        void BuildDetail(UITheme theme)
        {
            dBand = Band(detailView, "DecoBand", theme.blue, -120f, 260f, 1100f, 360f, -20f).GetComponent<Image>();
            dArtRt = HudKit.NewRect("Art", detailView);
            UILayout.Place(dArtRt, -40f, 40f, 760f, 760f);
            artHome = dArtRt.anchoredPosition;
            dArt = dArtRt.gameObject.AddComponent<Image>(); dArt.preserveAspect = true; dArt.raycastTarget = false;
            Tag(detailView, theme, "コープ", "CO-OP");

            // the name plate (bottom left)
            var plate = HudKit.NewRect("NamePlate", detailView);
            UILayout.Place(plate, 40f, H - 196f, 660f, 168f);
            HudKit.AddImage(plate, theme.paper);
            plate.gameObject.AddComponent<SlantedGraphic>().Set(SlantedGraphic.Size.Custom, SlantedGraphic.Direction.Right, 20f);
            HudKit.Stretch(KitShadow.Behind(plate.GetComponent<Image>().rectTransform, new Color(.02f, .03f, .05f), .5f, 10f, 18f).rectTransform);
            dName = HudKit.AddText(plate, "", 46f, theme.ink, true, TextAlignmentOptions.BottomLeft, "Name", UITheme.TextRole.Display);
            dName.textWrappingMode = TextWrappingModes.NoWrap;
            UILayout.Place(dName.rectTransform, 30f, 8f, 360f, 56f);
            dKana = HudKit.AddText(plate, "", 16f, theme.dim, true, TextAlignmentOptions.BottomLeft, "Kana", UITheme.TextRole.Body);
            dKana.textWrappingMode = TextWrappingModes.NoWrap;
            UILayout.Place(dKana.rectTransform, 300f, 16f, 300f, 40f);
            var rule = HudKit.NewRect("DecoRule", plate);
            UILayout.Place(rule, 30f, 70f, 560f, 3f);
            HudKit.AddImage(rule, theme.ink);
            dDesc = HudKit.AddText(plate, "", 15f, theme.ink, false, TextAlignmentOptions.TopLeft, "Desc", UITheme.TextRole.Body);
            dDesc.textWrappingMode = TextWrappingModes.Normal;
            UILayout.Place(dDesc.rectTransform, 30f, 82f, 590f, 80f);

            // the card (top right)
            card = HudKit.NewRect("Card", detailView);
            UILayout.Place(card, 800f, 96f, 600f, 300f);
            cardHome = card.anchoredPosition;
            HudKit.AddImage(card, new Color(theme.ink2.r, theme.ink2.g, theme.ink2.b, .96f));
            card.gameObject.AddComponent<CutCornerGraphic>().Set(24f, CutCornerGraphic.Corners.TopRight);
            var head = HudKit.NewRect("Head", card);
            UILayout.Place(head, 0f, 0f, 600f, 36f);
            HudKit.AddImage(head, theme.ink);
            var q = HudKit.AddText(head, "Q", 14f, theme.blue, true, TextAlignmentOptions.Center, "Q", UITheme.TextRole.Display);
            UILayout.Place(q.rectTransform, 12f, 0f, 30f, 36f);
            var e = HudKit.AddText(head, "E", 14f, theme.blue, true, TextAlignmentOptions.Center, "E", UITheme.TextRole.Display);
            UILayout.Place(e.rectTransform, 558f, 0f, 30f, 36f);
            dHeadName = HudKit.AddText(head, "", 15f, Color.white, true, TextAlignmentOptions.Center, "Name", UITheme.TextRole.Body);
            UILayout.Place(dHeadName.rectTransform, 60f, 0f, 480f, 36f);

            var faceCard = HudKit.NewRect("FaceCard", card);
            UILayout.Place(faceCard, 24f, 54f, 150f, 210f);
            HudKit.AddImage(faceCard, theme.paper);
            faceCard.localRotation = Quaternion.Euler(0f, 0f, -3f);
            var face = HudKit.NewRect("Face", faceCard);
            UILayout.Place(face, 8f, 8f, 134f, 150f);
            HudKit.AddImage(face, theme.ink3);
            face.gameObject.AddComponent<RectMask2D>();
            var fi = HudKit.NewRect("FaceImage", face); HudKit.Stretch(fi);
            dFace = fi.gameObject.AddComponent<Image>(); dFace.preserveAspect = true; dFace.raycastTarget = false;
            dCardRole = HudKit.AddText(faceCard, "", 14f, theme.ink, true, TextAlignmentOptions.Center, "Role", UITheme.TextRole.Title);
            dCardRole.textWrappingMode = TextWrappingModes.NoWrap;
            UILayout.Place(dCardRole.rectTransform, 4f, 164f, 142f, 40f);

            var rl = HudKit.AddText(card, "ROLE", 11f, theme.blue, true, TextAlignmentOptions.TopLeft, "RoleEn", UITheme.TextRole.Display);
            rl.characterSpacing = 12f;
            UILayout.Place(rl.rectTransform, 200f, 56f, 120f, 16f);
            dRole = HudKit.AddText(card, "", 30f, Color.white, true, TextAlignmentOptions.TopLeft, "Role", UITheme.TextRole.Title);
            dRole.textWrappingMode = TextWrappingModes.NoWrap;
            UILayout.Place(dRole.rectTransform, 200f, 72f, 230f, 44f);
            var rk = HudKit.AddText(card, "RANK", 14f, theme.acc, true, TextAlignmentOptions.BottomRight, "RankEn", UITheme.TextRole.Display);
            UILayout.Place(rk.rectTransform, 430f, 70f, 70f, 40f);
            dRank = HudKit.AddText(card, "", 64f, theme.acc, true, TextAlignmentOptions.MidlineRight, "Rank", UITheme.TextRole.Display);
            dRank.textWrappingMode = TextWrappingModes.NoWrap;
            UILayout.Place(dRank.rectTransform, 490f, 52f, 96f, 76f);
            for (int i = 0; i < Stars; i++)
            {
                var st = HudKit.NewRect("Star" + i, card);
                UILayout.Place(st, 206f + i * 36f, 146f, 18f, 18f);
                st.localRotation = Quaternion.Euler(0f, 0f, 45f);
                stars[i] = HudKit.AddImage(st, theme.ink3);
            }
            var pl = HudKit.AddText(card, "親密度", 13f, Color.white, true, TextAlignmentOptions.MidlineLeft, "PointsLabel", UITheme.TextRole.Body);
            UILayout.Place(pl.rectTransform, 200f, 196f, 80f, 22f);
            dPoints = HudKit.AddText(card, "", 13f, Color.Lerp(theme.dim, Color.white, .5f), true, TextAlignmentOptions.MidlineRight, "Points", UITheme.TextRole.Body);
            dPoints.textWrappingMode = TextWrappingModes.NoWrap;
            UILayout.Place(dPoints.rectTransform, 280f, 196f, 300f, 22f);
            var track = HudKit.NewRect("Track", card);
            UILayout.Place(track, 200f, 224f, 380f, 8f);
            HudKit.AddImage(track, new Color(1f, 1f, 1f, .14f));
            var fill = HudKit.NewRect("Fill", track);
            fill.anchorMin = Vector2.zero; fill.anchorMax = new Vector2(0f, 1f); fill.pivot = new Vector2(0f, .5f); fill.offsetMin = fill.offsetMax = Vector2.zero;
            dBar = HudKit.AddImage(fill, theme.acc);
            var hint = HudKit.AddText(card, "Q / E  切替   ·   ESC  戻る", 11f, theme.dim, true, TextAlignmentOptions.MidlineRight, "Hint", UITheme.TextRole.Body);
            UILayout.Place(hint.rectTransform, 200f, 262f, 380f, 22f);

            // the episode of this rank
            epBox = HudKit.NewRect("Episode", detailView);
            UILayout.Place(epBox, 800f, 408f, 600f, 150f);
            epHome = epBox.anchoredPosition;
            HudKit.AddImage(epBox, theme.paper);
            epBox.gameObject.AddComponent<SlantedGraphic>().Set(SlantedGraphic.Size.Custom, SlantedGraphic.Direction.Right, 14f);
            var band = HudKit.NewRect("DecoBand", epBox);
            UILayout.Place(band, 0f, 0f, 8f, 150f);
            HudKit.AddImage(band, theme.foe);
            dEpTitle = HudKit.AddText(epBox, "", 18f, theme.ink, true, TextAlignmentOptions.TopLeft, "Title", UITheme.TextRole.Title);
            dEpTitle.textWrappingMode = TextWrappingModes.NoWrap;
            UILayout.Place(dEpTitle.rectTransform, 26f, 14f, 540f, 26f);
            dEpText = HudKit.AddText(epBox, "", 15f, theme.ink, false, TextAlignmentOptions.TopLeft, "Text", UITheme.TextRole.Body);
            dEpText.textWrappingMode = TextWrappingModes.Normal;
            UILayout.Place(dEpText.rectTransform, 26f, 48f, 540f, 92f);

            // the perks by rank
            perkBox = HudKit.NewRect("Perks", detailView);
            UILayout.Place(perkBox, 800f, 572f, 600f, 226f);
            perkHome = perkBox.anchoredPosition;
            HudKit.AddImage(perkBox, new Color(theme.ink2.r, theme.ink2.g, theme.ink2.b, .9f));
            var ph = HudKit.AddText(perkBox, "コープ能力", 13f, theme.blue, true, TextAlignmentOptions.TopLeft, "Head", UITheme.TextRole.Title);
            UILayout.Place(ph.rectTransform, 18f, 8f, 300f, 20f);
            for (int i = 0; i < 9; i++)
            {
                var t = HudKit.AddText(perkBox, "", 12.5f, Color.white, true, TextAlignmentOptions.TopLeft, "Perk" + i, UITheme.TextRole.Body);
                t.textWrappingMode = TextWrappingModes.NoWrap; t.overflowMode = TextOverflowModes.Ellipsis;
                UILayout.Place(t.rectTransform, 18f, 32f + i * 21f, 566f, 20f);
                perkTexts.Add(t);
            }
        }

        // ---- open / close --------------------------------------------------------------------------------------------------------
        public void Open()
        {
            ops.Clear();
            foreach (var o in HubCatalog.Squad) if (o != null && o.key != Campaign.Commander && ops.Count < MaxRows) ops.Add(o);
            sel = Mathf.Clamp(sel, 0, Mathf.Max(0, ops.Count - 1));
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            ShowList();
        }

        public void Close() { gameObject.SetActive(false); }

        void Back()
        {
            if (detail) ShowList(); else Close();
        }

        void ShowList()
        {
            detail = false;
            listView.gameObject.SetActive(true); detailView.gameObject.SetActive(false);
            for (int i = 0; i < MaxRows; i++)
            {
                var r = rows[i];
                bool on = i < ops.Count;
                r.rt.gameObject.SetActive(on);
                if (!on) continue;
                var o = ops[i];
                int pts = Campaign.Points(D, Campaign.Commander, o.key), rank = Campaign.Level(pts);
                r.role.SetText(o.role ?? "");
                r.name.SetText(o.DisplayName);
                r.kana.SetText(o.kr ?? "");
                r.rank.SetText(rank >= Campaign.CoopNeed.Length ? "MAX" : rank.ToString());
                r.bar.rectTransform.anchorMax = new Vector2(Campaign.LevelProgress(pts), 1f);
            }
            PaintSelection();
            clock = 0f;
            Evaluate(Application.isPlaying ? 0f : Duration);
        }

        void Select(int i)
        {
            if (detail || i == sel || i < 0 || i >= ops.Count) return;
            sel = i;
            PaintSelection();
        }

        void PaintSelection()
        {
            var theme = UITheme.Current;
            for (int i = 0; i < ops.Count && i < MaxRows; i++)
            {
                var r = rows[i];
                bool s = i == sel;
                r.strip.color = s ? theme.blue : theme.paper;
                r.name.color = s ? Color.white : theme.ink;
                r.kana.color = s ? new Color(1f, 1f, 1f, .8f) : theme.dim;
                r.rankLbl.color = s ? new Color(1f, 1f, 1f, .8f) : theme.dim;
                r.rank.color = s ? theme.acc : theme.blue;
                r.arrow.gameObject.SetActive(s);
            }
        }

        void OpenDetail()
        {
            if (ops.Count == 0) return;
            detail = true;
            listView.gameObject.SetActive(false); detailView.gameObject.SetActive(true);
            FillDetail();
            clock = 0f;
            Evaluate(Application.isPlaying ? 0f : Duration);
        }

        void Step(int dir)
        {
            if (ops.Count == 0) return;
            sel = (sel + dir + ops.Count) % ops.Count;
            FillDetail();
            clock = FadeIn;   // (the art and the card slide in again, no fade)
        }

        void FillDetail()
        {
            var theme = UITheme.Current;
            var o = ops[sel];
            int pts = Campaign.Points(D, Campaign.Commander, o.key), rank = Campaign.Level(pts);
            bool max = rank >= Campaign.CoopNeed.Length;
            dArt.sprite = OperatorArt.Body(o.key); dArt.enabled = dArt.sprite != null;
            dFace.sprite = OperatorArt.Face(o.key); dFace.enabled = dFace.sprite != null;
            dBand.color = new Color(o.color.r, o.color.g, o.color.b, .55f);
            dName.SetText(o.DisplayName);
            dKana.SetText(o.kr ?? "");
            dDesc.SetText(o.description ?? "");
            dHeadName.SetText(o.DisplayName + "  ·  " + (sel + 1) + " / " + ops.Count);
            dRole.SetText(o.role ?? "");
            dCardRole.SetText(o.role ?? "");
            dRank.SetText(max ? "MAX" : rank.ToString());
            for (int i = 0; i < Stars; i++) stars[i].color = i < rank ? theme.acc : theme.ink3;
            int next = max ? pts : Campaign.CoopNeed[rank];
            dPoints.SetText(max ? pts + " pt  ·  MAX" : pts + " / " + next + " pt  ·  次のランクまで " + (next - pts));
            dBar.rectTransform.anchorMax = new Vector2(Campaign.LevelProgress(pts), 1f);
            if (rank == 0)
            {
                dEpTitle.SetText("まだ物語は始まっていない");
                dEpText.SetText("交流・贈り物・休暇・共同訓練でコープランクを上げると、" + o.DisplayName + "との物語が進む。");
            }
            else
            {
                var ep = CoopStories.Of(o.key, rank);
                dEpTitle.SetText("RANK " + rank + "  —  " + ep.title);
                dEpText.SetText(ep.text);
            }
            var perks = CoopPerks.Lines(o.key);
            for (int i = 0; i < perkTexts.Count; i++)
            {
                var t = perkTexts[i];
                if (i >= perks.Count) { t.SetText(""); continue; }
                var p = perks[i];
                bool open = rank >= p.rank;
                t.SetText("Rank " + p.rank + "   " + p.name + "  —  " + p.text);
                t.color = open ? Color.white : new Color(1f, 1f, 1f, .32f);
            }
        }

        // ---- ITimedEffect ---------------------------------------------------------------------------------------------------------
        public float Duration => FadeIn + RowIn + RowGap * MaxRows;

        public void Evaluate(float t)
        {
            bool reduce = UIMotionSettings.reduceMotion;
            float a = Mathf.Clamp01(t / FadeIn);
            if (group.alpha != a) group.alpha = a;
            if (!detail)
            {
                for (int i = 0; i < ops.Count && i < MaxRows; i++)
                {
                    float p = reduce ? 1f : UIEase.Tactical(Mathf.Clamp01((t - i * RowGap) / RowIn));
                    var r = rows[i];
                    float dx = i == sel ? RowSlant : 0f;
                    var pos = new Vector2(r.home.x + dx - (1f - p) * 260f, -r.home.y);
                    if (r.rt.anchoredPosition != pos) r.rt.anchoredPosition = pos;
                }
                return;
            }
            float s = reduce ? 1f : UIEase.Tactical(Mathf.Clamp01((t - FadeIn * .5f) / SideIn));
            var ap = artHome + new Vector2(-(1f - s) * 160f, 0f);
            if (dArtRt.anchoredPosition != ap) dArtRt.anchoredPosition = ap;
            var cp = cardHome + new Vector2((1f - s) * 220f, 0f);
            if (card.anchoredPosition != cp) card.anchoredPosition = cp;
            var ep = epHome + new Vector2((1f - s) * 280f, 0f);
            if (epBox.anchoredPosition != ep) epBox.anchoredPosition = ep;
            var pp = perkHome + new Vector2((1f - s) * 340f, 0f);
            if (perkBox.anchoredPosition != pp) perkBox.anchoredPosition = pp;
        }

        void Update()
        {
            clock += Time.unscaledDeltaTime;
            if (clock <= Duration + .05f) Evaluate(clock);
            else if (!detail) Evaluate(Duration + 1f);   // (the selection's step right)
            var kb = Keyboard.current;
            if (kb == null) return;
            if (kb.escapeKey.wasPressedThisFrame && !EscapeGate.Consumed) { EscapeGate.Consume(); Back(); return; }
            if (!detail)
            {
                if (kb.wKey.wasPressedThisFrame || kb.upArrowKey.wasPressedThisFrame) Select((sel - 1 + ops.Count) % Mathf.Max(1, ops.Count));
                if (kb.sKey.wasPressedThisFrame || kb.downArrowKey.wasPressedThisFrame) Select((sel + 1) % Mathf.Max(1, ops.Count));
                if (kb.fKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame) OpenDetail();
            }
            else
            {
                if (kb.qKey.wasPressedThisFrame || kb.aKey.wasPressedThisFrame || kb.leftArrowKey.wasPressedThisFrame) Step(-1);
                if (kb.eKey.wasPressedThisFrame || kb.dKey.wasPressedThisFrame || kb.rightArrowKey.wasPressedThisFrame) Step(1);
            }
        }

        /// <summary>The pointer over a strip selects it.</summary>
        class PointerEnter : MonoBehaviour, IPointerEnterHandler
        {
            public System.Action onEnter;
            public void OnPointerEnter(PointerEventData e) => onEnter?.Invoke();
        }
    }
}
