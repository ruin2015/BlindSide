// The briefing's weather symbols (IntroBriefing), drawn once into small textures from distance functions (anti-aliased over a
// pixel) — the UI fonts have no weather glyphs. Colours from ui_spec: accent #FFC81E sun, paper white cloud / outlines,
// glitchRed #FF3E5C mercury.
//   PartlyCloudy  a sun with eight rays, a cloud in front of it (lower right)
//   Thermometer   a stem and bulb outline, the mercury half way up
//   Arrow         a filled arrow pointing up (IntroBriefing turns it along the wind)
using UnityEngine;
using Blindside.UI;

namespace Blindside.Cinematic
{
    public static class IntroBriefingIcons
    {
        const int Size = 64;
        static Sprite partlyCloudy, thermometer, arrow;

        public static Sprite PartlyCloudy => partlyCloudy != null ? partlyCloudy : partlyCloudy = Make("IconPartlyCloudy", DrawPartlyCloudy);
        public static Sprite Thermometer => thermometer != null ? thermometer : thermometer = Make("IconThermometer", DrawThermometer);
        public static Sprite Arrow => arrow != null ? arrow : arrow = Make("IconArrow", DrawArrow);

        delegate Color Painter(float x, float y);

        static Sprite Make(string name, Painter paint)
        {
            var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false) { name = name, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontSave };
            var px = new Color[Size * Size];
            for (int y = 0; y < Size; y++)
                for (int x = 0; x < Size; x++)
                    px[y * Size + x] = paint(x + .5f, y + .5f);
            tex.SetPixels(px);
            tex.Apply(false, true);
            var s = Sprite.Create(tex, new Rect(0, 0, Size, Size), new Vector2(.5f, .5f), 100f);
            s.name = name; s.hideFlags = HideFlags.DontSave;
            return s;
        }

        // coverage of a shape whose signed distance is d (negative inside), over one pixel
        static float Cov(float d) => Mathf.Clamp01(.5f - d);

        static float Circle(float x, float y, float cx, float cy, float r) => Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy)) - r;

        static float Segment(float x, float y, float ax, float ay, float bx, float by, float r)
        {
            float px = x - ax, py = y - ay, dx = bx - ax, dy = by - ay;
            float h = Mathf.Clamp01((px * dx + py * dy) / (dx * dx + dy * dy));
            float ex = px - dx * h, ey = py - dy * h;
            return Mathf.Sqrt(ex * ex + ey * ey) - r;
        }

        static float Box(float x, float y, float cx, float cy, float hx, float hy, float r)
        {
            float qx = Mathf.Abs(x - cx) - hx + r, qy = Mathf.Abs(y - cy) - hy + r;
            float ox = Mathf.Max(qx, 0f), oy = Mathf.Max(qy, 0f);
            return Mathf.Sqrt(ox * ox + oy * oy) + Mathf.Min(Mathf.Max(qx, qy), 0f) - r;
        }

        static Color Over(Color under, Color c, float a)
        {
            float oa = a + under.a * (1f - a);
            if (oa <= 0f) return new Color(0f, 0f, 0f, 0f);
            var rgb = (new Color(c.r, c.g, c.b) * a + new Color(under.r, under.g, under.b) * under.a * (1f - a)) / oa;
            return new Color(rgb.r, rgb.g, rgb.b, oa);
        }

        static Color DrawPartlyCloudy(float x, float y)
        {
            var c = new Color(0f, 0f, 0f, 0f);
            // the sun, upper left: a disc and eight rays
            float sx = 24f, sy = 40f, sun = Circle(x, y, sx, sy, 10f);
            float rays = 99f;
            for (int i = 0; i < 8; i++)
            {
                float a = i * Mathf.PI / 4f, ca = Mathf.Cos(a), sa = Mathf.Sin(a);
                rays = Mathf.Min(rays, Segment(x, y, sx + ca * 14f, sy + sa * 14f, sx + ca * 19f, sy + sa * 19f, 1.6f));
            }
            c = Over(c, UIPalette.Accent, Cov(Mathf.Min(sun, rays)));
            // the cloud, lower right, in front: three bumps on a rounded base, with a dark rim so it stands off the sun
            float cloud = Mathf.Min(Mathf.Min(Circle(x, y, 33f, 27f, 11f), Circle(x, y, 44f, 24f, 9f)),
                                    Mathf.Min(Circle(x, y, 23f, 22f, 8f), Box(x, y, 34f, 18f, 18f, 6f, 6f)));
            c = Over(c, UIPalette.Ink, Cov(cloud - 2.5f));
            c = Over(c, new Color(.93f, .94f, .95f), Cov(cloud));
            return c;
        }

        static Color DrawThermometer(float x, float y)
        {
            var c = new Color(0f, 0f, 0f, 0f);
            float stem = Box(x, y, 32f, 38f, 6f, 19f, 6f), bulb = Circle(x, y, 32f, 15f, 10f);
            float outer = Mathf.Min(stem, bulb);
            c = Over(c, new Color(.93f, .94f, .95f), Cov(outer));
            c = Over(c, UIPalette.Ink, Cov(outer + 2.6f));                  // hollow it out
            float merc = Mathf.Min(Box(x, y, 32f, 28f, 2.6f, 10f, 2.6f), Circle(x, y, 32f, 15f, 6.5f));
            c = Over(c, UIPalette.Hex("#FF3E5C"), Cov(merc));
            return c;
        }

        static Color DrawArrow(float x, float y)
        {
            // a shaft and a head pointing up (+y)
            float shaft = Box(x, y, 32f, 24f, 4f, 16f, 1.5f);
            // the head: a triangle (apex 58, base 34, half width 16)
            float t = Mathf.Clamp01((58f - y) / 24f), half = 16f * t;
            float head = Mathf.Max(Mathf.Abs(x - 32f) - half, Mathf.Max(y - 58f, 34f - y));
            return new Color(.93f, .94f, .95f, Cov(Mathf.Min(shaft, head)));
        }
    }
}
