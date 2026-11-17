// The HQ's top HUD (menu canvas 1440 × 810), tactical: replaces HubScreen's old topbar (HubScreen.UseKitTopbar; the old one is still
// built when it is off).
//   Date      top left (24, 20) 400 × 104 ink plate (slant 22), a blue band 12 down its left: the date "4/12" 48 display, the weekday
//             "SAT" 20 display (gold) over 「土曜日」 15, a thin rule, then 午前 / 午後 26 title over the weather 「晴れ」 15 + "SUNNY" 15
//             display (dim). DecoStripe: hazard stripes along the bottom edge
//   Status    under it (24, 130) 400 × 34 paper2 (slant 12): "DAY n" 15 display · the money 18 display · the rank 15 body
//   Location  (24, 170) 300 × 36 ink .8: ◎ (gold) + the HubArea name 16 title
//   Objective top right (24 from the right, 20 down) 320 × ObjH: an acc chip "OBJECTIVE // 現在の目標" 15, the next main operation's
//             name 20 title (two lines at most, down to 15), the deadline 16 (gold), a third line 15 (dim): the leave days or where to go
// The minimap (HubMinimap) moves under the objective. Static: redrawn on Refresh / SetLocation only (no per-frame work).
using TMPro;
using UnityEngine;
using Blindside.Progress;
using Blindside.Save;

namespace Blindside.UI
{
    public class HubTopbar : MonoBehaviour
    {
        public const float X = 24f, Y = 20f, DateW = 400f, DateH = 104f, StatusH = 34f, LocW = 300f, LocH = 36f;
        public const float ObjW = 320f, ObjH = 150f, ObjRight = 24f, ObjTop = 20f;
        /// <summary>The minimap's top under the objective panel.</summary>
        public const float MapTop = ObjTop + ObjH + 12f;

        TMP_Text date, weekEn, weekJp, time, weather, weatherEn, day, money, rank, location, objName, objDue, objNote;
        string lastLocation;

        public static HubTopbar Build(Transform parent)
        {
            var theme = UITheme.Current;
            var root = HudKit.NewRect("KitTopbar", parent);
            HudKit.Stretch(root);
            var s = root.gameObject.AddComponent<HubTopbar>();

            // ---- the date block ----
            var plate = HudKit.NewRect("Date", root);
            UILayout.Place(plate, X, Y, DateW, DateH);
            HudKit.AddImage(plate, new Color(theme.ink.r, theme.ink.g, theme.ink.b, .94f));
            plate.gameObject.AddComponent<SlantedGraphic>().Set(SlantedGraphic.Size.Custom, SlantedGraphic.Direction.Right, 22f);
            var band = HudKit.NewRect("DecoBand", plate);
            UILayout.Place(band, 0f, 0f, 12f, DateH);
            HudKit.AddImage(band, theme.blue);
            var stripe = HudKit.NewRect("DecoStripe", plate);
            UILayout.Place(stripe, 12f, DateH - 6f, 200f, 6f);
            var hz = HazardStripe.Create(stripe, "DecoHazard", theme.acc, theme.ink);
            hz.scroll = false;

            s.date = Text(plate, "4/1", 48f, Color.white, TextAlignmentOptions.MidlineLeft, "DateNum", UITheme.TextRole.Display, 28f, 8f, 140f, 80f);
            s.weekEn = Text(plate, "THU", 20f, theme.acc, TextAlignmentOptions.BottomLeft, "WeekEn", UITheme.TextRole.Display, 170f, 18f, 90f, 28f);
            s.weekEn.characterSpacing = 6f;
            s.weekJp = Text(plate, "木曜日", 15f, Color.Lerp(theme.paper2, theme.ink, .1f), TextAlignmentOptions.TopLeft, "WeekJp", UITheme.TextRole.Body, 170f, 50f, 90f, 24f);
            var rule = HudKit.NewRect("DecoRule", plate);
            UILayout.Place(rule, 262f, 18f, 2f, 62f);
            HudKit.AddImage(rule, new Color(1f, 1f, 1f, .25f));
            s.time = Text(plate, "午前", 26f, Color.white, TextAlignmentOptions.BottomLeft, "Time", UITheme.TextRole.Title, 278f, 10f, 110f, 40f);
            s.weather = Text(plate, "晴れ", 15f, Color.white, TextAlignmentOptions.TopLeft, "Weather", UITheme.TextRole.Body, 278f, 54f, 44f, 24f);
            s.weatherEn = Text(plate, "SUNNY", 15f, Color.Lerp(theme.dim, Color.white, .35f), TextAlignmentOptions.TopLeft, "WeatherEn", UITheme.TextRole.Display, 324f, 55f, 70f, 22f);

            // ---- status: day · money · rank ----
            var status = HudKit.NewRect("Status", root);
            UILayout.Place(status, X, Y + DateH + 6f, DateW, StatusH);
            HudKit.AddImage(status, new Color(theme.paper2.r, theme.paper2.g, theme.paper2.b, .95f));
            status.gameObject.AddComponent<SlantedGraphic>().Set(SlantedGraphic.Size.Custom, SlantedGraphic.Direction.Right, 12f);
            s.day = Text(status, "DAY 1", 15f, theme.ink, TextAlignmentOptions.MidlineLeft, "Day", UITheme.TextRole.Display, 16f, 0f, 90f, StatusH);
            s.money = Text(status, "¥0", 18f, theme.ink, TextAlignmentOptions.MidlineLeft, "Money", UITheme.TextRole.Display, 108f, 0f, 150f, StatusH);
            s.rank = Text(status, "", 15f, theme.ink, TextAlignmentOptions.MidlineRight, "Rank", UITheme.TextRole.Body, 250f, 0f, 130f, StatusH);

            // ---- where the player is ----
            var loc = HudKit.NewRect("Location", root);
            UILayout.Place(loc, X, Y + DateH + 6f + StatusH + 6f, LocW, LocH);
            HudKit.AddImage(loc, new Color(theme.ink.r, theme.ink.g, theme.ink.b, .8f));
            loc.gameObject.AddComponent<SlantedGraphic>().Set(SlantedGraphic.Size.Custom, SlantedGraphic.Direction.Right, 12f);
            var pin = Text(loc, "◎", 15f, theme.acc, TextAlignmentOptions.Center, "Pin", UITheme.TextRole.Display, 10f, 0f, 24f, LocH);
            HudKit.UseSymbolFont(pin);
            s.location = Text(loc, "—", 16f, Color.white, TextAlignmentOptions.MidlineLeft, "Name", UITheme.TextRole.Title, 40f, 0f, LocW - 60f, LocH);
            s.location.overflowMode = TextOverflowModes.Ellipsis;

            // ---- the objective, top right ----
            var obj = HudKit.NewRect("Objective", root);
            obj.anchorMin = obj.anchorMax = obj.pivot = new Vector2(1f, 1f);
            obj.anchoredPosition = new Vector2(-ObjRight, -ObjTop);
            obj.sizeDelta = new Vector2(ObjW, ObjH);
            HudKit.AddImage(obj, new Color(theme.ink.r, theme.ink.g, theme.ink.b, .9f));
            obj.gameObject.AddComponent<SlantedGraphic>().Set(SlantedGraphic.Size.Custom, SlantedGraphic.Direction.Right, 14f);
            var objLine = HudKit.NewRect("DecoTopLine", obj);
            UILayout.Place(objLine, 14f, 0f, ObjW - 14f, 4f);
            HudKit.AddImage(objLine, theme.acc);
            var chip = HudKit.NewRect("Chip", obj);
            UILayout.Place(chip, 18f, 14f, 230f, 26f);
            HudKit.AddImage(chip, theme.acc);
            chip.gameObject.AddComponent<SlantedGraphic>().Set(SlantedGraphic.Size.Custom, SlantedGraphic.Direction.Right, 8f);
            Text(chip, "OBJECTIVE // 現在の目標", 15f, theme.ink, TextAlignmentOptions.MidlineLeft, "Label", UITheme.TextRole.Body, 10f, 0f, 215f, 26f);
            s.objName = Text(obj, "", 20f, Color.white, TextAlignmentOptions.TopLeft, "Name", UITheme.TextRole.Title, 18f, 48f, ObjW - 40f, 52f);
            s.objName.textWrappingMode = TextWrappingModes.Normal;
            s.objName.enableAutoSizing = true; s.objName.fontSizeMin = 15f; s.objName.fontSizeMax = 20f;
            s.objName.overflowMode = TextOverflowModes.Ellipsis;
            s.objDue = Text(obj, "", 16f, theme.acc, TextAlignmentOptions.MidlineLeft, "Due", UITheme.TextRole.Body, 18f, 100f, ObjW - 40f, 22f);
            s.objNote = Text(obj, "", 15f, Color.Lerp(theme.dim, Color.white, .45f), TextAlignmentOptions.MidlineLeft, "Note", UITheme.TextRole.Body, 18f, 122f, ObjW - 40f, 22f);
            s.objNote.overflowMode = TextOverflowModes.Ellipsis;
            return s;
        }

        static TMP_Text Text(Transform parent, string text, float size, Color color, TextAlignmentOptions align, string name, UITheme.TextRole role,
                             float x, float y, float w, float h)
        {
            var t = HudKit.AddText(parent, text, size, color, true, align, name, role);
            t.textWrappingMode = TextWrappingModes.NoWrap;
            UILayout.Place(t.rectTransform, x, y, w, h);
            return t;
        }

        /// <summary>Everything from the save (`live`: a real session; false: placeholders, nothing to show).</summary>
        public void Refresh(SaveData d, bool live)
        {
            if (!live || d == null)
            {
                date.SetText("—"); weekEn.SetText(""); weekJp.SetText(""); time.SetText("—"); weather.SetText(""); weatherEn.SetText("");
                day.SetText("DAY —"); money.SetText("¥ —"); rank.SetText("—");
                objName.SetText("セーブデータがありません"); objDue.SetText(""); objNote.SetText("");
                return;
            }
            var dt = Campaign.Date(d);
            date.SetText("{0}/{1}", (float)dt.Month, (float)dt.Day);
            weekEn.SetText(Campaign.WeekdayEn(d));
            weekJp.SetText(Campaign.WeekdayJp(d));
            time.SetText(Campaign.TimeName(d));
            weather.SetText(Campaign.WeatherJpOf(d));
            weatherEn.SetText(Campaign.WeatherEnOf(d));
            day.SetText("DAY {0}", (float)Campaign.DayNumber(d));
            money.SetText("¥" + d.money.ToString("N0"));
            rank.SetText(RankTable.Name(RankTable.RankOf(d.rankPoints)));

            var q = Campaign.NextMain(d);
            if (q == null)
            {
                objName.SetText("次の指令を待て");
                objDue.SetText("");
                objNote.SetText("サブ任務は作戦室で受けられる");
                return;
            }
            objName.SetText("メイン作戦 — " + q.name);
            int left = Campaign.DaysLeft(d);
            if (Campaign.DeadlineReached(d)) objDue.SetText("出撃の時 — 作戦室へ");
            else if (left >= 0) objDue.SetText("出撃まで あと {0}日", (float)left);
            else objDue.SetText("作戦室からいつでも出撃できる");
            objNote.SetText(d.leaveDays > 0 ? "褒賞休暇 残り " + d.leaveDays + "日" : "それまでに訓練・交流で準備を整えろ");
        }

        public void SetLocation(string name)
        {
            if (name == lastLocation) return;
            lastLocation = name;
            location.SetText(string.IsNullOrEmpty(name) ? "—" : name);
        }
    }
}
