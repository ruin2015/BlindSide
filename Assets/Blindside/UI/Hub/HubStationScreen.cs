// An operator's station at the HQ (F on SERUM / IGNIS / BASTION / CRUSHER / the supply clerk): a full screen in the house style —
//   DecoBackdrop   paper
//   DecoRing       a thick ink ring (900, ring 64) behind the art, off the top-left corner; DecoStripe a 150-wide ink band leaning
//                  right (/) behind the options
//   Art            the operator's full illustration (OperatorArt.Body, 640², bottom-left, bleeding off the bottom)
//   Tag            top left: ink plate 330 × 58 (slanted), the station's name 24 title (white) + its English 11 display (blue)
//   Bubble         ink box 380 × 108 by the head, a tail toward the art, the line 20 body (white) typed in at 45 characters a second
//   Options        right column (880, 480 wide): light gradient tiles (HubTiles: blue accent stripe, a faint glyph in the corner), the big word 38 title + what it does 15 body (ink / dim), a value on
//                  the right 18 display; a face (64) when it is a person. Up to 5 tall (108) — more are shorter
//   Money          blue bar 480 × 44 under the options: 所持金 13 + the amount 22 display (円)
//   Help           bottom right: HubTiles.Help (「操作説明」, the mouse and ESC caps); the back bar top right (HubTiles.Back)
// Motion (ITimedEffect.Evaluate, unscaled): the screen fades in (.2), the ring turns and grows in (.5), the band slides from the right,
// the art slides in from the left, the tag from the left, the bubble pops (.25) and types, the options slide in from the right one after
// another (.06 apart), the money bar last; the art breathes a little once settled. Hover (HubHover): an option slides 16 left, grows 3 %,
// brightens and shows ➜. When the options change (BASTION: a stat, then the partner) they slide in again.
// BASTION's training: the stats first (「体力 を鍛える」 …), then who trains with him — each squad member with that stat's points, sessions
// and price; a click trains (money → +1 point) and stays on the list. Without a save session nothing can be bought or trained.
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Blindside.Data;
using Blindside.Flow;
using Blindside.Hub;
using Blindside.Progress;
using Blindside.Save;
using Blindside.Tutorial;

namespace Blindside.UI
{
    public partial class HubStationScreen : MonoBehaviour, ITimedEffect
    {
        public const float OptX = 880f, OptW = 480f, OptTop = 150f, OptGap = 22f, OptMaxH = 108f, OptArea = 470f;
        public const float BubbleCps = 45f;

        public sealed class Option
        {
            public string title = "", sub = "", value = "";
            /// <summary>A BSTactical glyph drawn big and faint in the tile's corner.</summary>
            public string icon;
            public Sprite face;
            public bool enabled = true;
            public System.Action click;
            /// <summary>Shown in the bubble while the pointer is on it (null: the greeting stays).</summary>
            public string hoverLine;
        }

        HubScreen screen;
        RectTransform root, ring, stripe, art, tagPlate, bubble, optionsRoot, money, optHead;
        CanvasGroup group, artGroup, bubbleGroup, moneyGroup;
        Image artImage;
        TMP_Text tagText, tagEn, bubbleText, moneyText, optHeadText;
        Vector2 ringPos, stripePos, artPos, tagPos, moneyPos;
        readonly List<RectTransform> optRects = new List<RectTransform>(8);
        readonly List<CanvasGroup> optGroups = new List<CanvasGroup>(8);
        readonly List<HubHover> optHovers = new List<HubHover>(8);
        readonly List<Vector2> optPos = new List<Vector2>(8);
        float clock, optClock, typeFrom;
        string line = "";
        bool running;
        System.Action onBack;   // ESC / 戻る inside a sub step (BASTION's partner list → the stats)
        string stationKey;

        public bool IsOpen => gameObject.activeSelf;
        /// <summary>True while a panel opened from here (a shop list, the growth panel) is over it: it takes ESC.</summary>
        public System.Func<bool> Covered;
        SaveData D => screen.Data;
        bool Editable => screen.Editable;

        static Sprite ringSprite;

        // ---- build -------------------------------------------------------------------------------------------------------------
        public static HubStationScreen Build(Transform parent, HubScreen screen)
        {
            var theme = UITheme.Current;
            var r = HudKit.NewRect("HubStation", parent);
            HudKit.Stretch(r);
            var s = r.gameObject.AddComponent<HubStationScreen>();
            s.screen = screen; s.root = r;
            s.group = r.gameObject.AddComponent<CanvasGroup>();
            var bg = HudKit.NewRect("DecoBackdrop", r);
            HudKit.Stretch(bg);
            HudKit.AddImage(bg, theme.paper, raycast: true);

            s.ring = Centered(r, "DecoRing", new Vector2(300f, 170f), new Vector2(900f, 900f));
            var ri = s.ring.gameObject.AddComponent<Image>(); ri.sprite = Ring(); ri.color = theme.ink; ri.raycastTarget = false;
            Bounds(s.ring);
            s.ringPos = s.ring.anchoredPosition;

            s.stripe = Centered(r, "DecoStripe", new Vector2(1040f, 420f), new Vector2(150f, 1400f));
            s.stripe.localRotation = Quaternion.Euler(0f, 0f, -22f);
            HudKit.AddImage(s.stripe, theme.ink);
            Bounds(s.stripe);
            s.stripePos = s.stripe.anchoredPosition;

            s.art = Centered(r, "Art", new Vector2(340f, 520f), new Vector2(640f, 640f));
            s.artImage = s.art.gameObject.AddComponent<Image>(); s.artImage.preserveAspect = true; s.artImage.raycastTarget = false;
            s.artGroup = s.art.gameObject.AddComponent<CanvasGroup>();
            Bounds(s.art);
            s.artPos = s.art.anchoredPosition;

            // the tag
            s.tagPlate = HudKit.NewRect("Tag", r);
            UILayout.Place(s.tagPlate, 0f, 24f, 330f, 58f);
            HudKit.AddImage(s.tagPlate, theme.ink);
            s.tagPlate.gameObject.AddComponent<SlantedGraphic>().Set(SlantedGraphic.Size.Custom, SlantedGraphic.Direction.Right, 22f);
            s.tagText = HudKit.AddText(s.tagPlate, "", 24f, Color.white, true, TextAlignmentOptions.MidlineLeft, "Name", UITheme.TextRole.Title);
            s.tagText.textWrappingMode = TextWrappingModes.NoWrap;
            UILayout.Place(s.tagText.rectTransform, 36f, 0f, 200f, 58f);
            s.tagEn = HudKit.AddText(s.tagPlate, "", 11f, theme.blue, true, TextAlignmentOptions.MidlineRight, "En", UITheme.TextRole.Display);
            s.tagEn.characterSpacing = 8f;
            UILayout.Place(s.tagEn.rectTransform, 150f, 0f, 140f, 58f);
            s.tagPos = s.tagPlate.anchoredPosition;

            // the speech bubble (pivot at its bottom-left: it pops from the speaker)
            s.bubble = HudKit.NewRect("Bubble", r);
            s.bubble.anchorMin = s.bubble.anchorMax = new Vector2(0f, 1f); s.bubble.pivot = new Vector2(0f, 0f);
            s.bubble.anchoredPosition = new Vector2(380f, -176f); s.bubble.sizeDelta = new Vector2(380f, 108f);
            s.bubbleGroup = s.bubble.gameObject.AddComponent<CanvasGroup>();
            var bi = HudKit.AddImage(s.bubble, theme.ink);
            var tail = HudKit.NewRect("DecoTail", s.bubble);
            tail.anchorMin = tail.anchorMax = new Vector2(0f, 0f); tail.pivot = new Vector2(.5f, .5f);
            tail.anchoredPosition = new Vector2(34f, 0f); tail.sizeDelta = new Vector2(26f, 26f);
            tail.localRotation = Quaternion.Euler(0f, 0f, 45f);
            HudKit.AddImage(tail, theme.ink);
            tail.SetAsFirstSibling();
            s.bubbleText = HudKit.AddText(s.bubble, "", 20f, Color.white, false, TextAlignmentOptions.MidlineLeft, "Line", UITheme.TextRole.Body);
            s.bubbleText.textWrappingMode = TextWrappingModes.Normal;
            UILayout.Place(s.bubbleText.rectTransform, 22f, 10f, 380f - 44f, 88f);

            // options
            s.optHead = HudKit.NewRect("OptionsHead", r);
            UILayout.Place(s.optHead, OptX, OptTop - 44f, OptW, 32f);
            s.optHeadText = HudKit.AddText(s.optHead, "", 16f, theme.ink, true, TextAlignmentOptions.MidlineLeft, "Text", UITheme.TextRole.Title);
            s.optHeadText.textWrappingMode = TextWrappingModes.NoWrap; s.optHeadText.overflowMode = TextOverflowModes.Ellipsis;
            HudKit.Stretch(s.optHeadText.rectTransform);
            s.optionsRoot = HudKit.NewRect("Options", r);
            HudKit.Stretch(s.optionsRoot);

            // money
            s.money = HudKit.NewRect("Money", r);
            UILayout.Place(s.money, OptX, OptTop + OptArea + 24f, OptW, 44f);
            HudKit.AddImage(s.money, theme.blue);
            s.money.gameObject.AddComponent<SlantedGraphic>().Set(SlantedGraphic.Size.Custom, SlantedGraphic.Direction.Right, 12f);
            s.moneyGroup = s.money.gameObject.AddComponent<CanvasGroup>();
            var ml = HudKit.AddText(s.money, "所持金", 13f, Color.white, true, TextAlignmentOptions.MidlineLeft, "Label", UITheme.TextRole.Body);
            UILayout.Place(ml.rectTransform, 24f, 0f, 120f, 44f);
            s.moneyText = HudKit.AddText(s.money, "", 22f, Color.white, true, TextAlignmentOptions.MidlineRight, "Amount", UITheme.TextRole.Body);
            s.moneyText.textWrappingMode = TextWrappingModes.NoWrap;
            UILayout.Place(s.moneyText.rectTransform, 140f, 0f, OptW - 170f, 44f);
            s.moneyPos = s.money.anchoredPosition;

            // help + back
            HubTiles.Help(r, 1440f - 40f - 240f, 810f - 18f - 30f - 2 * 34f, true, "MOUSE", "選択", "ESC", "戻る");
            var back = HubTiles.Back(r, s.Back);
            back.anchorMin = back.anchorMax = back.pivot = new Vector2(1f, 1f);
            back.anchoredPosition = new Vector2(-32f, -28f);

            r.gameObject.SetActive(false);
            return s;
        }

        /// <summary>A rect with its centre at `center` (canvas units from the top-left), for rotating / scaling about it.</summary>
        static RectTransform Centered(RectTransform parent, string name, Vector2 center, Vector2 size)
        {
            var rt = HudKit.NewRect(name, parent);
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f); rt.pivot = new Vector2(.5f, .5f);
            rt.anchoredPosition = new Vector2(center.x, -center.y); rt.sizeDelta = size;
            return rt;
        }

        /// <summary>Decoration / the art bleeding off the screen by design: not the lint's off-screen / overlap concern.</summary>
        static void Bounds(RectTransform rt)
        {
            var ig = rt.gameObject.AddComponent<LintIgnoreOverlap>();
            ig.bounds = true; ig.reason = "the station screen's layered art: bleeds off the screen and sits under the panels by design";
        }

        /// <summary>A ring (made once): 512², the band 64/512 of the radius thick, anti-aliased edges.</summary>
        static Sprite Ring()
        {
            if (ringSprite != null) return ringSprite;
            const int S = 512; const float R = S * .5f, band = S * .07f;
            var tex = new Texture2D(S, S, TextureFormat.RGBA32, false) { name = "HubRing", wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[S * S];
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    float d = Mathf.Sqrt((x + .5f - R) * (x + .5f - R) + (y + .5f - R) * (y + .5f - R));
                    float a = Mathf.Clamp01(R - 1f - d) * Mathf.Clamp01(d - (R - 1f - band));
                    px[y * S + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                }
            tex.SetPixels32(px); tex.Apply();
            ringSprite = Sprite.Create(tex, new Rect(0f, 0f, S, S), new Vector2(.5f, .5f));
            return ringSprite;
        }

        // ---- open ----------------------------------------------------------------------------------------------------------------
        bool forced;   // the main quest's day (OpenDeadline): no way back

        /// <summary>Opens on `key`'s station (an operator key, HubCatalog.ClerkKey or KitchenKey); `talk`: the operator's 会話 options instead.</summary>
        public void Open(string key, bool talk = false)
        {
            stationKey = key;
            forced = false;
            var op = BuildAssetRegistry.Operator(key);
            artImage.sprite = op != null ? OperatorArt.Body(key) : null;
            artImage.enabled = artImage.sprite != null;
            var lines = HubCatalog.StationLines(key);
            SetLine(lines[Random.Range(0, lines.Length)]);
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            onBack = null;
            if (talk || (op != null && key != "SERUM" && key != "IGNIS" && key != "BASTION" && key != "CRUSHER"))
            {
                Tag("会話", "TALK");
                TalkOptions(key);
            }
            else
            switch (key)
            {
                case "SERUM": Tag("医務室", "MEDICAL"); SerumOptions(); break;
                case "IGNIS": Tag("隊員・装備管理", "LOADOUT"); IgnisOptions(); break;
                case "BASTION": Tag("訓練所", "TRAINING"); BastionStats(); break;
                case "CRUSHER": Tag("作戦室", "OPERATIONS"); CampaignOps(); break;
                case KitchenKey: Tag("炊事場", "KITCHEN"); KitchenOptions(); break;
                default: Tag("装備購入", "SUPPLY"); ClerkOptions(); break;
            }
            RefreshMoney();
            clock = 0f;
            running = Application.isPlaying;
            Evaluate(running ? 0f : Duration);
        }

        public void Close()
        {
            running = false;
            gameObject.SetActive(false);
        }

        void Back()
        {
            if (forced) return;
            if (onBack != null) { var b = onBack; onBack = null; b(); return; }
            Close();
        }

        void Tag(string name, string en) { tagText.SetText(name); tagEn.SetText(en); }

        /// <summary>The bubble's line, typed in again.</summary>
        public void SetLine(string text)
        {
            if (text == line) return;
            line = text ?? "";
            bubbleText.SetText(line);
            bubbleText.maxVisibleCharacters = 0;
            typeFrom = clock < BubbleIn ? BubbleIn : clock;
            if (!Application.isPlaying) bubbleText.maxVisibleCharacters = 99999;
        }

        /// <summary>Back from a screen opened here (the shop): the options slide in again.</summary>
        public void Reenter()
        {
            if (!IsOpen || !Application.isPlaying) return;
            optClock = 0f;
            EvaluateOptions(0f);
        }

        public void RefreshMoney()
        {
            if (!Editable) { moneyText.SetText("— 円"); return; }
            moneyText.SetText(D.money.ToString("N0") + " 円");
        }

        // ---- the stations' options -------------------------------------------------------------------------------------------------
        void SerumOptions()
        {
            SetOptions(null, new Option { title = "医薬品", sub = "を買う", icon = "✚", click = () => screen.OpenStation(HubStation.Medical) },
                             new Option { title = "ガジェット", sub = "を買う", icon = "✹", click = () => screen.OpenStation(HubStation.Shop, 2) });
        }

        void IgnisOptions()
        {
            SetOptions(null, new Option { title = "ロードアウト", sub = "武器とパーツを装着する", icon = "⌖", click = () => screen.OpenLoadout() },
                             new Option { title = "アイテム", sub = "を持たせる", icon = "▣", click = () => screen.OpenItems() },
                             new Option { title = "装備", sub = "を買う", icon = "⬢", click = () => screen.OpenStation(HubStation.Shop, 0) });
        }

        void ClerkOptions()
        {
            SetOptions(null, new Option { title = "武器", sub = "を買う", icon = "⌖", click = () => screen.OpenStation(HubStation.Shop, 0) },
                             new Option { title = "アタッチメント", sub = "を買う", icon = "⬢", click = () => screen.OpenStation(HubStation.Shop, 1) },
                             new Option { title = "ガジェット", sub = "を買う", icon = "✹", click = () => screen.OpenStation(HubStation.Shop, 2) },
                             new Option { title = "贈り物", sub = "を仲間に贈る", icon = "✚", click = GiftTargets });
            onBack = null;
        }

        readonly List<HubCatalog.Operation> ops = new List<HubCatalog.Operation>(8);

        void CrusherOptions()
        {
            HubCatalog.Operations(ops);
            var list = new Option[ops.Count];
            for (int i = 0; i < ops.Count; i++)
            {
                var o = ops[i];
                var rec = D.RecordOf(o.stem);
                list[i] = new Option
                {
                    title = o.name, icon = "✦", sub = "報酬 " + o.money.ToString("N0") + " 円 · 貢献度 " + o.contribution,
                    value = "出撃 " + rec.plays + " · 成功 " + rec.wins, click = () => screen.Sortie(o),
                };
            }
            SetOptions("作戦を選んで出撃", list);
        }

        static readonly string[] Drills = { "持久走", "防御訓練", "射撃訓練", "機動訓練", "精密射撃訓練" };
        static readonly string[] StatIcons = { "✚", "▣", "✹", "➜", "◎" };

        void BastionStats()
        {
            var list = new Option[6];
            for (int i = 0; i < 5; i++)
            {
                int stat = i;
                list[i] = new Option { title = KitGrowthPanel.StatNames[i], sub = "を鍛える  ·  " + Drills[i], icon = StatIcons[i], click = () => BastionPartner(stat) };
            }
            list[5] = new Option { title = "訓練プログラム", sub = "これまでの訓練をもう一度", icon = "⌖", click = BastionPrograms };
            SetOptions("鍛える能力を選ぶ", list);
            onBack = null;
        }

        // the tutorial's lessons, played again: each runs on the training map and comes back to the HQ
        void BastionPrograms()
        {
            SetOptions("訓練プログラムを選ぶ",
                Program("基礎訓練", "移動・射撃・視界", "➜", TutorialSegment.Basics),
                Program("壁の破壊", "壁の種類と破壊", "▣", TutorialSegment.Walls),
                new Option { title = "ガジェット訓練", sub = "ドローン・投擲物・爆薬", icon = "✹", click = BastionGadgetPrograms });
            SetLine("何を訓練する？");
            onBack = () => { BastionStats(); var l = HubCatalog.StationLines("BASTION"); SetLine(l[0]); };
        }

        void BastionGadgetPrograms()
        {
            SetOptions("ガジェットを選ぶ",
                Program("ドローン", "建物内の偵察", "⌖", TutorialSegment.Drone),
                Program("破片手榴弾", "爆風で攻撃", "✹", TutorialSegment.Frag),
                Program("閃光弾", "敵の視界を奪う", "✦", TutorialSegment.Flash),
                Program("スモーク弾", "煙幕で視線を切る", "◎", TutorialSegment.Smoke),
                Program("粘着爆薬", "壁を吹き飛ばす", "▣", TutorialSegment.Breach));
            SetLine("どのガジェットを試す？");
            onBack = BastionPrograms;
        }

        Option Program(string title, string sub, string icon, TutorialSegment segment) =>
            new Option { title = title, sub = sub, icon = icon, click = () => StartProgram(segment) };

        void StartProgram(TutorialSegment segment)
        {
            if (SceneFlow.Leaving) return;
            TutorialDirector.Segment = segment;
            SceneFade.Out(SceneFade.DefaultOut, SceneFlow.GoTutorial);
        }

        void BastionPartner(int stat)
        {
            var squad = HubCatalog.Squad;
            var list = new Option[squad.Count];
            for (int i = 0; i < squad.Count; i++)
            {
                var op = squad[i];
                var p = D.OperatorOf(op.key);
                int done = p.trained[stat];
                bool max = done >= HubCatalog.MaxTrain;
                int cost = HubCatalog.TrainCost(done);
                list[i] = new Option
                {
                    title = op.DisplayName, face = OperatorArt.Face(op.key),
                    sub = KitGrowthPanel.StatNames[stat] + " " + p.StatPoints(stat) + " pt  ·  訓練 " + done + " / " + HubCatalog.MaxTrain + "  ·  ½日",
                    value = max ? "上限" : cost.ToString("N0") + " 円",
                    enabled = Editable && !max && D.money >= cost,
                    click = () => Train(op, stat),
                };
            }
            SetOptions(KitGrowthPanel.StatNames[stat] + "を鍛える — 隊員を選ぶ", list);
            SetLine(HubCatalog.TrainPartnerLine);
            onBack = () => { BastionStats(); var l = HubCatalog.StationLines("BASTION"); SetLine(l[0]); };
        }

        void Train(OperatorData op, int stat)
        {
            if (!Editable) return;
            var p = D.OperatorOf(op.key);
            if (p.trained[stat] >= HubCatalog.MaxTrain) return;
            int cost = HubCatalog.TrainCost(p.trained[stat]);
            if (D.money < cost) { SetLine("金が足りないようだな。"); return; }
            D.money -= cost;
            p.trained[stat]++;
            p.AddStat(stat, 1);
            // half a day at the gym: FLARE trains alongside (co-op), and the partner gets on with BASTION a little
            Campaign.Spend(D, Campaign.CostTraining);
            bool up = Campaign.AddCoop(D, Campaign.Commander, op.key, Campaign.CoopTraining);
            Campaign.AddCoop(D, "BASTION", op.key, Campaign.CoopTraining);
            SaveSystem.Write(false);
            RefreshMoney();
            BastionPartner(stat);
            SetLine(op.DisplayName + "、" + KitGrowthPanel.StatNames[stat] + "が上がったぞ。その調子だ。" + (up ? "  【コープランク UP】" : ""));
            screen.TimeSpent();
        }

        // ---- the option buttons ----------------------------------------------------------------------------------------------------
        void SetOptions(string head, params Option[] list)
        {
            foreach (var r in optRects) if (r != null) HudKit.DestroySafe(r.gameObject);
            optRects.Clear(); optGroups.Clear(); optHovers.Clear(); optPos.Clear();
            optHeadText.SetText(head ?? "");
            optHead.gameObject.SetActive(!string.IsNullOrEmpty(head));
            var theme = UITheme.Current;
            int n = Mathf.Max(1, list.Length);
            float h = Mathf.Min(OptMaxH, (OptArea - OptGap * (n - 1)) / n);
            for (int i = 0; i < list.Length; i++)
            {
                var o = list[i];
                var rt = HudKit.NewRect("Option" + i, optionsRoot);
                rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f); rt.pivot = new Vector2(.5f, .5f);
                var pos = new Vector2(OptX + OptW * .5f, -(OptTop + i * (h + OptGap) + h * .5f));
                rt.anchoredPosition = pos; rt.sizeDelta = new Vector2(OptW, h);
                var tile = HubTiles.Make(rt, false, o.enabled, o.icon, Mathf.Min(120f, h * 1.1f));
                var img = tile.face;
                var g = rt.gameObject.AddComponent<CanvasGroup>();
                var btn = rt.gameObject.AddComponent<Button>();
                btn.transition = Selectable.Transition.None; btn.targetGraphic = img; btn.interactable = o.enabled;
                var click = o.click;
                btn.onClick.AddListener(() => { Sfx.Ui(); click?.Invoke(); });

                float x = 30f;
                if (o.face != null)
                {
                    var f = HudKit.NewRect("Face", rt);
                    float fs = Mathf.Min(64f, h - 12f);
                    f.anchorMin = f.anchorMax = new Vector2(0f, .5f); f.pivot = new Vector2(0f, .5f);
                    f.anchoredPosition = new Vector2(24f, 0f); f.sizeDelta = new Vector2(fs, fs);
                    HudKit.AddImage(f, Color.Lerp(theme.paper2, theme.ink, .1f));
                    f.gameObject.AddComponent<RectMask2D>();
                    var ph = HudKit.NewRect("Photo", f); HudKit.Stretch(ph);
                    var im = ph.gameObject.AddComponent<Image>(); im.sprite = o.face; im.preserveAspect = true; im.raycastTarget = false;
                    x = 24f + fs + 16f;
                }
                // the arrow shown on hover, left of the words
                var mk = HudKit.NewRect("Marker", rt);
                mk.anchorMin = mk.anchorMax = new Vector2(0f, .5f); mk.pivot = new Vector2(1f, .5f);
                mk.anchoredPosition = new Vector2(-6f, 0f); mk.sizeDelta = new Vector2(30f, 30f);
                var mkg = mk.gameObject.AddComponent<CanvasGroup>(); mkg.alpha = 0f;
                var arrow = HudKit.AddText(mk, "➜", 22f, theme.blue, true, TextAlignmentOptions.Center, "Arrow", UITheme.TextRole.Display);
                HudKit.UseSymbolFont(arrow);
                HudKit.Stretch(arrow.rectTransform);

                float big = o.face != null ? 24f : Mathf.Min(38f, h * .4f);
                var title = HudKit.AddText(rt, o.title, big, o.enabled ? theme.ink : theme.dim, true, TextAlignmentOptions.BottomLeft, "Title", null);
                title.textWrappingMode = TextWrappingModes.NoWrap; title.overflowMode = TextOverflowModes.Ellipsis;
                Place(title.rectTransform, x, h * .08f, OptW - x - 150f, h * .52f);
                var sub = HudKit.AddText(rt, o.sub, 15f, o.enabled ? theme.dim : Color.Lerp(theme.dim, theme.paper2, .4f), true, TextAlignmentOptions.TopLeft, "Sub", UITheme.TextRole.Body);
                sub.textWrappingMode = TextWrappingModes.NoWrap; sub.overflowMode = TextOverflowModes.Ellipsis;
                Place(sub.rectTransform, x + 2f, h * .6f, OptW - x - 40f, h * .34f);
                if (!string.IsNullOrEmpty(o.value))
                {
                    var v = HudKit.AddText(rt, o.value, 17f, o.enabled ? theme.blue : theme.dim, true, TextAlignmentOptions.MidlineRight, "Value", UITheme.TextRole.Body);
                    v.textWrappingMode = TextWrappingModes.NoWrap;
                    Place(v.rectTransform, OptW - 170f, 0f, 146f, h * .6f);
                }

                var hv = rt.gameObject.AddComponent<HubHover>();
                hv.target = rt;
                HubTiles.Hover(hv, tile, 16f);
                hv.shift = new Vector2(-16f, 0f); hv.scale = 1.03f; hv.marker = mkg; hv.interactable = o.enabled;
                var hl = o.hoverLine;
                if (!string.IsNullOrEmpty(hl)) hv.Entered = () => SetLine(hl);
                hv.SetBase(pos);

                optRects.Add(rt); optGroups.Add(g); optHovers.Add(hv); optPos.Add(pos);
            }
            float bottom = OptTop + list.Length * (h + OptGap) - OptGap;
            moneyPos = new Vector2(OptX, -(Mathf.Max(bottom, OptTop + 140f) + 26f));
            if (!running) money.anchoredPosition = moneyPos;
            optClock = 0f;
            if (!Application.isPlaying) EvaluateOptions(99f);
        }

        static void Place(RectTransform rt, float x, float y, float w, float h)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f); rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(x, -y); rt.sizeDelta = new Vector2(w, h);
        }

        // ---- motion -------------------------------------------------------------------------------------------------------------
        public const float BubbleIn = .45f;
        public float Duration => .9f;   // (settled after: Update stops posing the screen)

        static float Ease(float t, float from, float len) => UIEase.Tactical(Mathf.Clamp01((t - from) / len));

        public void Evaluate(float t)
        {
            bool still = UIMotionSettings.reduceMotion;
            group.alpha = Mathf.Clamp01(t / .2f);
            float p = Ease(t, 0f, .5f);
            ring.localScale = Vector3.one * (still ? 1f : .85f + .15f * p);
            ring.localRotation = Quaternion.Euler(0f, 0f, still ? 0f : -25f * (1f - p));
            p = Ease(t, .05f, .4f);
            stripe.anchoredPosition = stripePos + new Vector2(still ? 0f : 320f * (1f - p), 0f);
            p = Ease(t, .08f, .45f);
            art.anchoredPosition = artPos + new Vector2(still ? 0f : -180f * (1f - p), 0f);
            artGroup.alpha = p;
            p = Ease(t, .12f, .3f);
            tagPlate.anchoredPosition = tagPos + new Vector2(still ? 0f : -220f * (1f - p), 0f);
            p = Ease(t, .35f, .25f);
            bubble.localScale = Vector3.one * (still ? 1f : .6f + .4f * p);
            bubbleGroup.alpha = p;
            p = Ease(t, .4f, .3f);
            money.anchoredPosition = moneyPos + new Vector2(still ? 0f : 160f * (1f - p), 0f);
            moneyGroup.alpha = p;
        }

        /// <summary>The options' entrance, `t` after they were set (the first set waits for the screen's own entrance).</summary>
        void EvaluateOptions(float t)
        {
            bool still = UIMotionSettings.reduceMotion;
            for (int i = 0; i < optRects.Count; i++)
            {
                float p = Ease(t, .06f * i, .35f);
                optGroups[i].alpha = p;
                optHovers[i].SetBase(optPos[i] + new Vector2(still ? 0f : 140f * (1f - p), 0f));
            }
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            clock += dt; optClock += dt;
            if (running) { Evaluate(clock); if (clock >= Duration) running = false; }
            // the options wait for the screen's entrance the first time (.25 in), then slide in whenever they change
            float optT = optClock - (clock - optClock < .01f ? .25f : 0f);
            if (optT < .8f) EvaluateOptions(optT);
            // the line types in
            int n = Mathf.Clamp((int)((clock - typeFrom) * BubbleCps), 0, line.Length);
            if (bubbleText.maxVisibleCharacters != n && clock >= typeFrom) bubbleText.maxVisibleCharacters = n;

            var kb = Keyboard.current;
            if (kb != null && kb.escapeKey.wasPressedThisFrame && !EscapeGate.Consumed && (Covered == null || !Covered()))
            { EscapeGate.Consume(); Back(); }
        }

        /// <summary>Previews: posed settled on `key` (BASTION's partner list for `stat` ≥ 0).</summary>
        public void Pose(string key, int stat = -1)
        {
            Open(key);
            if (stat >= 0) BastionPartner(stat);
            Evaluate(Duration);
            EvaluateOptions(99f);
            bubbleText.maxVisibleCharacters = 99999;
        }

        /// <summary>Previews: BASTION's list of gadget lessons.</summary>
        public void PoseGadgetPrograms()
        {
            Open("BASTION");
            BastionGadgetPrograms();
            Evaluate(Duration);
            EvaluateOptions(99f);
            bubbleText.maxVisibleCharacters = 99999;
        }
    }
}
