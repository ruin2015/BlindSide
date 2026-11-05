// =====================================================================
//  BLINDSIDE — HUD build tool (#topbar / #objective / #roster / #log).
//  Menu: Blindside > UI > Build HUD (Topbar, Objective, Roster, Log)
//  1. Bakes the CSS backgrounds the HUD needs into small PNGs under Art/UI/Hud
//     (--pp gradient, --hatch, .bc barcode, .bar tick marks, .pips skewed pip, .rc-por shade,
//      .rcard.sel::before arrow, and two box-shadow falloffs).
//  2. (Re)builds HUD_Objective / HUD_Topbar (with HUD_Objective nested as .tb-right) /
//     HUD_Roster / HUD_Log under Prefabs/UI — existing assets are overwritten in place (GUIDs kept).
//  3. Places them in the open scene under a "BlindsideHUD" overlay canvas with a HudController,
//     wires MatchBootstrap.hud, and saves the scene.
//  Sizes/colours are the CSS values from blindside_source/00_markup_and_styles.html (see each
//  runtime component's header for the rules).
// =====================================================================
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Blindside.Integration;

namespace Blindside.UI.EditorTools
{
    public static class HudPrefabBuilder
    {
        const string ArtDir = "Assets/Blindside/Art/UI/Hud";
        const string PrefabDir = "Assets/Blindside/Prefabs/UI";
        // the legacy bar and its objective (replaced by the kit's Prefabs/UI/HUD_Topbar.prefab — this builder never writes that one)
        const string ObjectivePath = "Assets/Blindside/UI/Legacy/HUD_Objective.prefab";
        const string TopbarPath = "Assets/Blindside/UI/Legacy/HUD_Topbar.prefab";
        // the legacy list (moved to UI/Legacy in Phase 3-1; Prefabs/UI/HUD_Roster.prefab is now the SelectableCard list)
        const string RosterPath = "Assets/Blindside/UI/Legacy/HUD_Roster.prefab";
        const string LogPath = PrefabDir + "/HUD_Log.prefab";

        static readonly Color Ink = UIPalette.Ink, Ink2 = UIPalette.Ink2, Blu = UIPalette.Blue, Foe = UIPalette.Foe, Dimp = UIPalette.Dim;
        static readonly Color Pp0 = UIPalette.Hex("#F4F5F6"), Pp1 = UIPalette.Hex("#E3E6E9"), Pp2 = UIPalette.Hex("#D6D9DD");

        static Sprite sPp, sHatch, sBarcode, sTick, sPip, sShade, sArrow, sShadow26, sShadow12;

        // box-shadow blur radius R → Gaussian σ = R/2; the texture ramps 3σ outside the edge and
        // PadIn inside it (the element itself covers the rest).
        const int Sh26Out = 39, Sh26In = 20, Sh12Out = 18, Sh12In = 10;

        [MenuItem("Blindside/UI/Build HUD (Topbar, Objective, Roster, Log)")]
        public static void Build()
        {
            BakeTextures();
            BuildObjective();
            BuildTopbar();
            BuildRoster();
            BuildLog();
            PlaceInScene();
            AssetDatabase.SaveAssets();
            Debug.Log("[HudPrefabBuilder] HUD_Objective / HUD_Topbar / HUD_Roster / HUD_Log built and placed under BlindsideHUD.");
        }

        // =================================================================
        //  Textures
        // =================================================================
        static void BakeTextures()
        {
            if (!AssetDatabase.IsValidFolder(ArtDir)) AssetDatabase.CreateFolder("Assets/Blindside/Art/UI", "Hud");

            // --pp: linear-gradient(160deg,#f4f5f6 0%,#e3e6e9 70%,#d6d9dd 100%)
            sPp = Bake("hud_pp", 64, 64, (x, y) =>
            {
                float a = 160f * Mathf.Deg2Rad; var dir = new Vector2(Mathf.Sin(a), Mathf.Cos(a));   // CSS 0deg = up
                var p = new Vector2((x + .5f) / 64f - .5f, (y + .5f) / 64f - .5f);                   // texture y is up
                float half = Mathf.Abs(.5f * dir.x) + Mathf.Abs(.5f * dir.y);
                float t = Mathf.Clamp01((Vector2.Dot(p, dir) + half) / (2f * half));
                return t < .7f ? Color.Lerp(Pp0, Pp1, t / .7f) : Color.Lerp(Pp1, Pp2, (t - .7f) / .3f);
            }, TextureWrapMode.Clamp, FilterMode.Bilinear);

            // --hatch: repeating-linear-gradient(135deg,rgba(0,0,0,.035) 0 1px,transparent 1px 5px)
            // 5px period across the 135° axis ≈ 7px along x; bands run bottom-left→top-right ("/").
            sHatch = Bake("hud_hatch", 7, 7, (x, y) => ((x - y) % 7 + 7) % 7 == 0 ? new Color(0, 0, 0, .035f) : Color.clear,
                TextureWrapMode.Repeat, FilterMode.Point);

            // .bc: ink 0-2, 4-5, 8-11 of every 13px (white here, tinted ink @ .75 by the Image).
            sBarcode = Bake("hud_barcode", 13, 4, (x, y) => (x < 2 || x == 4 || (x >= 8 && x < 11)) ? Color.white : Color.clear,
                TextureWrapMode.Repeat, FilterMode.Point);

            // .bar::after: repeating-linear-gradient(90deg,transparent 0 9px,rgba(0,0,0,.25) 9px 10px)
            sTick = Bake("hud_bartick", 10, 2, (x, y) => x == 9 ? new Color(0, 0, 0, .25f) : Color.clear,
                TextureWrapMode.Repeat, FilterMode.Point);

            // .pips i: 7x12 box, transform:skewX(-18deg) → 11x12 parallelogram (4x supersampled, 4 px per unit).
            float k = Mathf.Tan(18f * Mathf.Deg2Rad);
            sPip = Bake("hud_pip", 44, 48, (x, y) => Coverage(x, y, 4, (X, Y) =>
            {
                float cx = X - 5.5f, cy = 6f - Y;             // CSS coords about the box centre, y down
                return Mathf.Abs(cy) <= 6f && Mathf.Abs(cx + k * cy) <= 3.5f;
            }), TextureWrapMode.Clamp, FilterMode.Bilinear);

            // .rc-por background: linear-gradient(135deg,transparent 0 60%,rgba(0,0,0,.25) 60%) on its 42x54 padding box.
            const float PW = 42f, PH = 54f;
            sShade = Bake("hud_por_shade", 42, 54, (x, y) => Coverage(x, y, 1, (X, Y) =>
            {
                float u = X / PW, v = 1f - Y / PH;             // v: CSS y down
                float t = ((u - .5f) * PW + (v - .5f) * PH) / (PW + PH) + .5f;
                return t >= .6f;
            }) * new Color(0, 0, 0, .25f), TextureWrapMode.Clamp, FilterMode.Bilinear);

            // .rcard.sel::before: 6px transparent borders + border-left-color → a 6x12 right-pointing triangle.
            sArrow = Bake("hud_sel_arrow", 24, 48, (x, y) => Coverage(x, y, 4, (X, Y) => X <= 6f && Mathf.Abs(Y - 6f) <= 6f - X),
                TextureWrapMode.Clamp, FilterMode.Bilinear);

            sShadow26 = BakeShadow("hud_shadow26", Sh26Out, Sh26In, 13f);
            sShadow12 = BakeShadow("hud_shadow12", Sh12Out, Sh12In, 6f);
            AssetDatabase.Refresh();

            sPp = LoadSprite("hud_pp", Vector4.zero);
            sHatch = LoadSprite("hud_hatch", Vector4.zero);
            sBarcode = LoadSprite("hud_barcode", Vector4.zero);
            sTick = LoadSprite("hud_bartick", Vector4.zero);
            sPip = LoadSprite("hud_pip", Vector4.zero, 4f);
            sShade = LoadSprite("hud_por_shade", Vector4.zero);
            sArrow = LoadSprite("hud_sel_arrow", Vector4.zero, 4f);
            int b26 = Sh26Out + Sh26In, b12 = Sh12Out + Sh12In;
            sShadow26 = LoadSprite("hud_shadow26", new Vector4(b26, b26, b26, b26));
            sShadow12 = LoadSprite("hud_shadow12", new Vector4(b12, b12, b12, b12));
        }

        static Color Coverage(int px, int py, int unitsPerPx, System.Func<float, float, bool> inside)
        {
            const int SS = 4; int hit = 0;
            for (int i = 0; i < SS; i++)
                for (int j = 0; j < SS; j++)
                    if (inside((px + (i + .5f) / SS) / unitsPerPx, (py + (j + .5f) / SS) / unitsPerPx)) hit++;
            return new Color(1, 1, 1, hit / (float)(SS * SS));
        }

        static Sprite BakeShadow(string name, int padOut, int padIn, float sigma)
        {
            int n = (padOut + padIn) * 2 + 2;
            float Edge(int i)
            {
                // signed distance inside the box edge (box spans padOut .. n-padOut)
                float c = i + .5f, d = Mathf.Min(c - padOut, (n - padOut) - c);
                return d >= padIn ? 1f : Phi(d / sigma);
            }
            return Bake(name, n, n, (x, y) => new Color(0, 0, 0, Edge(x) * Edge(y)), TextureWrapMode.Clamp, FilterMode.Bilinear);
        }

        static float Phi(float z)   // standard normal CDF (Abramowitz–Stegun erf)
        {
            float x = Mathf.Abs(z) / Mathf.Sqrt(2f), t = 1f / (1f + .3275911f * x);
            float erf = 1f - (((((1.061405429f * t - 1.453152027f) * t) + 1.421413741f) * t - .284496736f) * t + .254829592f) * t * Mathf.Exp(-x * x);
            return .5f * (1f + Mathf.Sign(z) * erf);
        }

        static Sprite Bake(string name, int w, int h, System.Func<int, int, Color> px, TextureWrapMode wrap, FilterMode filter)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            var data = new Color[w * h];
            for (int y = 0; y < h; y++) for (int x = 0; x < w; x++) data[y * w + x] = px(x, y);
            tex.SetPixels(data); tex.Apply();
            string path = $"{ArtDir}/{name}.png";
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path);
            var imp = (TextureImporter)AssetImporter.GetAtPath(path);
            imp.textureType = TextureImporterType.Sprite;
            imp.spriteImportMode = SpriteImportMode.Single;
            imp.mipmapEnabled = false;
            imp.alphaIsTransparency = true;
            imp.sRGBTexture = true;
            imp.wrapMode = wrap;
            imp.filterMode = filter;
            imp.textureCompression = TextureImporterCompression.Uncompressed;
            imp.npotScale = TextureImporterNPOTScale.None;
            var settings = new TextureImporterSettings();
            imp.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            imp.SetTextureSettings(settings);
            imp.SaveAndReimport();
            return null;
        }

        static Sprite LoadSprite(string name, Vector4 border, float ppuScale = 1f)
        {
            string path = $"{ArtDir}/{name}.png";
            var imp = (TextureImporter)AssetImporter.GetAtPath(path);
            bool dirty = false;
            if (imp.spriteBorder != border) { imp.spriteBorder = border; dirty = true; }
            float ppu = 100f * ppuScale;   // 1 texel = 1 canvas unit at the scaler's 100 reference PPU (×4 for supersampled art)
            if (!Mathf.Approximately(imp.spritePixelsPerUnit, ppu)) { imp.spritePixelsPerUnit = ppu; dirty = true; }
            if (dirty) imp.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        // =================================================================
        //  Small builders
        // =================================================================
        static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = 5; // UI
            var rt = (RectTransform)go.transform;
            if (parent != null) rt.SetParent(parent, false);
            return rt;
        }

        static void Stretch(RectTransform rt, float l = 0, float b = 0, float r = 0, float t = 0)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.pivot = new Vector2(.5f, .5f);
            rt.offsetMin = new Vector2(l, b); rt.offsetMax = new Vector2(-r, -t);
        }

        static void TopLeft(RectTransform rt, float x, float y, float w, float h)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0, 1); rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = new Vector2(x, y); rt.sizeDelta = new Vector2(w, h);
        }

        static Image Img(RectTransform rt, Color c, Sprite s = null, Image.Type type = Image.Type.Simple, bool raycast = false)
        {
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = s; img.type = type; img.color = c; img.raycastTarget = raycast;
            if (type == Image.Type.Sliced) img.fillCenter = true;
            return img;
        }

        static Image Fill(Transform parent, string name, Color c, Sprite s = null, Image.Type type = Image.Type.Simple, bool raycast = false)
        {
            var rt = Rect(name, parent); Stretch(rt);
            return Img(rt, c, s, type, raycast);
        }

        static TextMeshProUGUI Txt(RectTransform rt, string text, float size, Color c, bool bold, TextAlignmentOptions align, float spacing = 0f)
        {
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            t.text = text; t.fontSize = size; t.color = c;
            UIFonts.Auto(t, bold);
            t.alignment = align; t.characterSpacing = spacing;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.overflowMode = TextOverflowModes.Overflow;
            t.raycastTarget = false; t.richText = true;
            return t;
        }

        /// <summary>box-shadow as a sliced falloff behind the element: offset (0,-dy), spread by the blur's 3σ.</summary>
        static void Shadow(Transform parent, bool big)
        {
            var rt = Rect("Shadow", parent);
            int o = big ? Sh26Out : Sh12Out; float dy = big ? 10f : 4f; float a = big ? .5f : .4f;
            Stretch(rt, -o, -o - dy, -o, -o + dy);
            Img(rt, new Color(0, 0, 0, a), big ? sShadow26 : sShadow12, Image.Type.Sliced);
            rt.SetAsFirstSibling();
        }

        /// <summary>background:var(--hatch),var(--pp).</summary>
        static void Paper(Transform parent, bool raycast = false)
        {
            Fill(parent, "Paper", Color.white, sPp, Image.Type.Simple, raycast);
            Fill(parent, "Hatch", Color.white, sHatch, Image.Type.Tiled);
        }

        /// <summary>.brk::before — inset:4px; 9x2 + 2x9 at the top-left and bottom-right corners, currentColor @ .55.</summary>
        static void Brackets(Transform parent, Color c, float rightInset = 0f)
        {
            c.a *= .55f;
            var root = Rect("Brk", parent); Stretch(root, 4, 4, 4 + rightInset, 4);
            void Bar(string n, Vector2 anchor, Vector2 size)
            {
                var r = Rect(n, root); r.anchorMin = r.anchorMax = anchor; r.pivot = anchor;
                r.anchoredPosition = Vector2.zero; r.sizeDelta = size; Img(r, c);
            }
            Bar("TL_H", new Vector2(0, 1), new Vector2(9, 2)); Bar("TL_V", new Vector2(0, 1), new Vector2(2, 9));
            Bar("BR_H", new Vector2(1, 0), new Vector2(9, 2)); Bar("BR_V", new Vector2(1, 0), new Vector2(2, 9));
        }

        static GameObject Save(GameObject go, string path)
        {
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            return prefab;
        }

        // =================================================================
        //  HUD_Objective (.tb-right)
        // =================================================================
        static void BuildObjective()
        {
            var root = Rect("HUD_Objective", null);
            TopLeft(root, 0, 0, 300, 110);
            root.gameObject.AddComponent<CanvasGroup>();
            var obj = root.gameObject.AddComponent<HudObjective>();

            Shadow(root, true);
            Paper(root, raycast: true);
            // border-right:6px solid var(--ink)
            var br = Rect("BorderRight", root);
            br.anchorMin = new Vector2(1, 0); br.anchorMax = Vector2.one; br.pivot = new Vector2(1, .5f);
            br.anchoredPosition = Vector2.zero; br.sizeDelta = new Vector2(6, 0); Img(br, Ink);

            // #objective b
            obj.titleBg = Rect("TitleBg", root); Img(obj.titleBg, Ink);
            var tt = Rect("Title", obj.titleBg); Stretch(tt, 6, 2, 6, 2);
            obj.titleText = Txt(tt, "OBJECTIVE // どちらか一方の達成で勝利", 10, Color.white, true, TextAlignmentOptions.MidlineLeft);

            // #objective{font-size:12px;line-height:1.5;color:var(--ink2)}
            var body = Rect("Body", root);
            obj.bodyText = Txt(body, $"① 人質を救出地点{Blindside.Map.ExfilZone.RangeText}のいずれかまで護送\n② または防衛チームを全員制圧", 12, Ink2, false, TextAlignmentOptions.TopLeft);
            obj.bodyText.lineSpacing = 35f;   // ≈ 18px line advance (1.5 × 12px) over the font's own line height

            obj.teamRule = Rect("TeamRule", root); Img(obj.teamRule, new Color(0, 0, 0, .15f));

            var al = Rect("AllyLabel", root);
            obj.allyLabel = Txt(al, "ALLY", 10, Blu, true, TextAlignmentOptions.MidlineLeft);
            obj.allyPips = Rect("AllyPips", root); Pips(obj.allyPips);
            var el = Rect("EnemyLabel", root);
            obj.enemyLabel = Txt(el, "ENEMY", 10, Foe, true, TextAlignmentOptions.MidlineLeft);
            obj.enemyPips = Rect("EnemyPips", root); Pips(obj.enemyPips);

            Brackets(root, Ink, rightInset: 6f);
            Save(root.gameObject, ObjectivePath);
        }

        static void Pips(RectTransform row)
        {
            for (int i = 0; i < 5; i++)
            {
                var pip = Rect("Pip_" + i, row);
                TopLeft(pip, i * 10f, 0, 7, 12);
                var shape = Rect("Shape", pip);
                shape.anchorMin = shape.anchorMax = new Vector2(.5f, .5f); shape.pivot = new Vector2(.5f, .5f);
                shape.anchoredPosition = Vector2.zero; shape.sizeDelta = new Vector2(11, 12);
                Img(shape, Color.white, sPip);
            }
        }

        // =================================================================
        //  HUD_Topbar
        // =================================================================
        static void BuildTopbar()
        {
            var root = Rect("HUD_Topbar", null);
            // #topbar{top:12px;left:14px;right:14px}
            root.anchorMin = new Vector2(0, 1); root.anchorMax = new Vector2(1, 1); root.pivot = new Vector2(.5f, 1);
            root.offsetMin = new Vector2(14, -12 - 120); root.offsetMax = new Vector2(-14, -12);
            var bar = root.gameObject.AddComponent<HudTopbar>();

            // ---- .tb-left ----
            bar.left = Rect("TbLeft", root); TopLeft(bar.left, 0, 0, 260, 68);
            bar.left.gameObject.AddComponent<CanvasGroup>();
            Shadow(bar.left, true);
            Paper(bar.left, raycast: true);
            var bk = Rect("BarK", bar.left);
            bk.anchorMin = Vector2.zero; bk.anchorMax = new Vector2(0, 1); bk.pivot = new Vector2(0, .5f);
            bk.anchoredPosition = Vector2.zero; bk.sizeDelta = new Vector2(8, 0); Img(bk, Ink);
            bar.leftLogoCol = Rect("Lg", bar.left);
            bar.logoText = Txt(Rect("Logo", bar.leftLogoCol), "BLINDSIDE", 22, Ink, true, TextAlignmentOptions.TopLeft, 14f);
            bar.logoSmall = Txt(Rect("LogoSmall", bar.leftLogoCol), "TACTICAL BREACH OPERATION", 9, Dimp, false, TextAlignmentOptions.TopLeft, 12f);
            bar.modeTag = Txt(Rect("ModeTag", bar.leftLogoCol), "攻撃チーム · 人質救出作戦", 11, Ink2, true, TextAlignmentOptions.TopLeft);
            bar.barcode = Rect("Barcode", bar.left);
            var bcCol = Ink; bcCol.a = .75f;
            Img(bar.barcode, bcCol, sBarcode, Image.Type.Tiled);

            // ---- .tb-center ----
            bar.center = Rect("TbCenter", root); TopLeft(bar.center, 300, 0, 330, 48);
            bar.center.gameObject.AddComponent<CanvasGroup>();
            Shadow(bar.center, true);
            bar.phaseBadge = Rect("PhaseBadge", bar.center); Img(bar.phaseBadge, Ink, null, Image.Type.Simple, true);
            var mk = Rect("Marker", bar.phaseBadge);
            mk.anchorMin = Vector2.zero; mk.anchorMax = new Vector2(0, 1); mk.pivot = new Vector2(0, .5f);
            mk.anchoredPosition = Vector2.zero; mk.sizeDelta = new Vector2(10, 0);
            bar.phaseMarker = Img(mk, Blu);
            bar.phaseMain = Txt(Rect("Main", bar.phaseBadge), "—", 16, Color.white, true, TextAlignmentOptions.TopLeft, 12f);
            bar.phaseSub = Txt(Rect("Sub", bar.phaseBadge), "", 10, new Color(1, 1, 1, .7f), false, TextAlignmentOptions.TopLeft);
            bar.roundBox = Rect("RoundBox", bar.center);
            Paper(bar.roundBox, raycast: true);
            bar.roundText = Txt(Rect("Round", bar.roundBox),
                "<size=9><b>ROUND</b></size><space=10><size=30><b>01</b></size><space=4><size=13><color=#6A7078>/15</color></size>",
                30, Ink, false, TextAlignmentOptions.TopLeft);
            Brackets(bar.roundBox, Ink);

            // ---- .tb-right: nested HUD_Objective ----
            var objAsset = AssetDatabase.LoadAssetAtPath<GameObject>(ObjectivePath);
            var objInst = (GameObject)PrefabUtility.InstantiatePrefab(objAsset, root);
            objInst.name = "TbRight_Objective";
            bar.objective = objInst.GetComponent<HudObjective>();

            Save(root.gameObject, TopbarPath);
        }

        // =================================================================
        //  HUD_Roster (+ its card template)
        // =================================================================
        static void BuildRoster()
        {
            var root = Rect("HUD_Roster", null);
            // #roster{left:14px;top:100px;width:216px}
            TopLeft(root, 14, -100, 216, 5 * 54 + 4 * 5);
            var roster = root.gameObject.AddComponent<HudRoster>();
            roster.cards = Rect("Cards", root); Stretch(roster.cards);

            var card = Rect("RosterCard", root);
            TopLeft(card, 0, 0, 216, 54);
            var c = card.gameObject.AddComponent<HudRosterCard>();
            var inner = Rect("Inner", card); Stretch(inner);
            var vis = Rect("Visual", inner); Stretch(vis);
            c.visual = vis;

            Shadow(vis, false);
            Paper(vis, raycast: true);

            // .rc-por{width:46px;border-left:4px solid var(--c);background:shade,var(--ink2)}
            var por = Rect("Por", vis);
            por.anchorMin = Vector2.zero; por.anchorMax = new Vector2(0, 1); por.pivot = new Vector2(0, .5f);
            por.anchoredPosition = Vector2.zero; por.sizeDelta = new Vector2(46, 0);
            var pb = Rect("Border", por);
            pb.anchorMin = Vector2.zero; pb.anchorMax = new Vector2(0, 1); pb.pivot = new Vector2(0, .5f);
            pb.anchoredPosition = Vector2.zero; pb.sizeDelta = new Vector2(4, 0);
            c.porBorder = Img(pb, Blu);
            var box = Rect("Box", por); Stretch(box, 4, 0, 0, 0); Img(box, Ink2);
            Fill(box, "Shade", Color.white, sShade);
            // .rc-por span{text-shadow:0 1px 0 #000}; font:700 17px var(--fd); place-items:center
            var ish = Rect("InitialsShadow", box); Stretch(ish, 0, -1, 0, 1);
            c.initialsShadow = Txt(ish, "CR", 17, Color.black, true, TextAlignmentOptions.Center);
            var ini = Rect("Initials", box); Stretch(ini);
            c.initials = Txt(ini, "CR", 17, Color.white, true, TextAlignmentOptions.Center);
            // .rcard.dead .rc-por::after{content:'K.I.A';inset:0;background:rgba(229,54,77,.85);font:700 11px;color:#fff}
            var kia = Rect("Kia", box); Stretch(kia);
            var kc = Foe; kc.a = .85f; Img(kia, kc);
            Txt(Rect("Text", kia), "K.I.A", 11, Color.white, true, TextAlignmentOptions.Center);
            Stretch((RectTransform)kia.Find("Text"));
            c.kia = kia.gameObject; kia.gameObject.SetActive(false);

            // .rc-body{padding:6px 12px 6px 9px} → content x 55..204 (149 wide)
            const float BX = 46 + 9, BW = 216 - 46 - 9 - 12;
            var nm = Rect("Name", vis); TopLeft(nm, BX, -6, BW, 16);
            c.nameText = Txt(nm, "CRUSHER", 13, Ink, true, TextAlignmentOptions.BottomLeft, 8f);
            var sb = Rect("Sub", vis); TopLeft(sb, BX, -6, BW, 16);
            c.subText = Txt(sb, "クラッシャー · ブリーチャー", 10, Dimp, false, TextAlignmentOptions.BottomRight);

            // .bar{height:5px;background:rgba(0,0,0,.14);margin:5px 0 4px}
            var barBg = Rect("Bar", vis); TopLeft(barBg, BX, -27, BW, 5); Img(barBg, new Color(0, 0, 0, .14f));
            var fill = Rect("Fill", barBg);
            fill.anchorMin = Vector2.zero; fill.anchorMax = Vector2.one; fill.pivot = new Vector2(0, .5f);
            fill.offsetMin = Vector2.zero; fill.offsetMax = Vector2.zero;
            c.barFill = fill; c.barFillImage = Img(fill, Blu);
            Fill(barBg, "Ticks", Color.white, sTick, Image.Type.Tiled);

            // .rc-st{font:600 10px var(--fm);color:var(--dimp)}  .st{color:var(--blu)}
            var hp = Rect("Hp", vis); TopLeft(hp, BX, -36, BW, 12);
            c.hpText = Txt(hp, "120/120", 10, Dimp, true, TextAlignmentOptions.MidlineLeft);
            var st = Rect("Status", vis); TopLeft(st, BX, -36, BW, 12);
            c.statusText = Txt(st, "", 10, Blu, true, TextAlignmentOptions.MidlineRight);

            // ::after — right edge bar
            var edge = Rect("Edge", vis);
            edge.anchorMin = new Vector2(1, 0); edge.anchorMax = Vector2.one; edge.pivot = new Vector2(1, .5f);
            edge.anchoredPosition = Vector2.zero; edge.sizeDelta = new Vector2(4, 0);
            c.edge = Img(edge, Ink);

            // .sel::before — left:-12px; top:50%; margin-top:-6px
            var arrow = Rect("SelArrow", vis);
            arrow.anchorMin = arrow.anchorMax = new Vector2(0, .5f); arrow.pivot = new Vector2(0, .5f);
            arrow.anchoredPosition = new Vector2(-12, 0); arrow.sizeDelta = new Vector2(6, 12);
            Img(arrow, Blu, sArrow);
            arrow.gameObject.AddComponent<LintIgnoreClip>().reason = "arrow hangs off the card's left edge";
            c.selArrow = arrow.gameObject; arrow.gameObject.SetActive(false);

            roster.template = c;
            card.gameObject.SetActive(false);
            Save(root.gameObject, RosterPath);
        }

        // =================================================================
        //  HUD_Log
        // =================================================================
        static void BuildLog()
        {
            var root = Rect("HUD_Log", null);
            // #log{right:14px;bottom:96px;width:310px}
            root.anchorMin = root.anchorMax = new Vector2(1, 0); root.pivot = new Vector2(1, 0);
            root.anchoredPosition = new Vector2(-14, 96); root.sizeDelta = new Vector2(310, 0);
            var view = root.gameObject.AddComponent<HudLogView>();

            var entry = Rect("LogEntry", root);
            entry.anchorMin = Vector2.zero; entry.anchorMax = new Vector2(1, 0); entry.pivot = new Vector2(.5f, 0);
            entry.anchoredPosition = Vector2.zero; entry.sizeDelta = new Vector2(0, 20);
            var inner = Rect("Inner", entry); Stretch(inner);
            Img(inner, new Color32(14, 16, 20, (byte)Mathf.RoundToInt(.84f * 255)));
            inner.gameObject.AddComponent<CanvasGroup>().blocksRaycasts = false;
            var border = Rect("Border", inner);
            border.anchorMin = Vector2.zero; border.anchorMax = new Vector2(0, 1); border.pivot = new Vector2(0, .5f);
            border.anchoredPosition = Vector2.zero; border.sizeDelta = new Vector2(3, 0);
            Img(border, UIPalette.Hex("#838A93"));
            var dot = Rect("Dot", inner);
            dot.anchorMin = dot.anchorMax = new Vector2(1, .5f); dot.pivot = new Vector2(1, .5f);
            dot.anchoredPosition = new Vector2(-4, 0); dot.sizeDelta = new Vector2(3, 3);
            Img(dot, new Color(1, 1, 1, .3f));
            var text = Rect("Text", inner); Stretch(text, 10, 3, 8, 3);
            var t = Txt(text, "戦闘記録の項目", 11, UIPalette.Hex("#D7DCE1"), false, TextAlignmentOptions.MidlineLeft);
            t.textWrappingMode = TextWrappingModes.Normal;

            view.template = entry;
            entry.gameObject.SetActive(false);
            Save(root.gameObject, LogPath);
        }

        // =================================================================
        //  Scene
        // =================================================================
        static void PlaceInScene()
        {
            var scene = EditorSceneManager.GetActiveScene();
            var old = GameObject.Find("BlindsideHUD");
            if (old != null) Object.DestroyImmediate(old);

            var root = new GameObject("BlindsideHUD", typeof(RectTransform));
            root.layer = 5;
            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = -1;   // under the banners/contact alert (z 50+), the title/fade canvas and the unit panel/menu (z 16/30)
            UITheme.Current.ApplyTo(root.AddComponent<CanvasScaler>(), UITheme.CanvasKind.Hud);
            root.AddComponent<UIThemeCanvas>().kind = UITheme.CanvasKind.Hud;
            root.AddComponent<GraphicRaycaster>();
            var hud = root.AddComponent<HudController>();

            var content = Rect("HUD", root.transform); Stretch(content);
            hud.content = content.gameObject;

            var topbar = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(TopbarPath), content);
            var roster = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(RosterPath), content);
            var log = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(LogPath), content);
            hud.topbar = topbar.GetComponent<HudTopbar>();
            hud.objective = hud.topbar.objective;
            hud.roster = roster.GetComponent<HudRoster>();
            hud.log = log.GetComponent<HudLogView>();
            // #log (z 14) under #roster (z 15) under #topbar (z 20)
            log.transform.SetSiblingIndex(0); roster.transform.SetSiblingIndex(1); topbar.transform.SetSiblingIndex(2);
            content.gameObject.SetActive(false);   // #hud.hidden until a match binds

            var boot = Object.FindAnyObjectByType<MatchBootstrap>(FindObjectsInactive.Include);
            if (boot != null) { Undo.RecordObject(boot, "Wire HUD"); boot.hud = hud; EditorUtility.SetDirty(boot); }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
    }
}
