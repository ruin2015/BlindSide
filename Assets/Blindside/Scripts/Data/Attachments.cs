// Weapon attachments: the 19 parts the HQ sells (Hub.HubCatalog shows them, ProgressBonus puts the equipped ones on the player's
// operators in a real match). Each operator carries one per slot (Save.OperatorProgress.attachments, slot order below).
//
// Effects (hit / headshot chances in percentage points, like every other accuracy figure; mobility = the move range):
//   range bands by the shot's distance: 近距離 ≤ 3 m · 中距離 3 – 7 m · 遠距離 > 7 m (Combat.CombatModifiers.BandOf)
//   ホロサイト            近距離命中 +7 · ヘッドショット +3
//   2倍スコープ           命中 +3 · ヘッドショット +3
//   4倍スコープ           中距離命中 +5 · 近距離命中 −1 · ヘッドショット +5
//   8倍スコープ           中・遠距離命中 +5 · 近距離命中 −3 · ヘッドショット +5
//   16倍スコープ          遠距離命中 +10 · 近距離命中 −10 · ヘッドショット +20
//   サプレッサー          the shot leaves no 銃声 mark for the enemy · 攻撃力 −2%
//   マズルブレーキ        the volley's first round: 命中 +10 · ヘッドショット +5 (every round)
//   コンペンセイター      命中 +5 · ヘッドショット +3
//   フラッシュハイダー    攻撃力 +5% · 命中 +3
//   ロングバレル          no damage falloff with distance
//   クイックドローマガジン SPD +1 (who fires first on a mutual sighting)
//   拡張マガジン          rounds per volley +1
//   ベルトマガジン        rounds per volley +2 · 機動力 −10%
//   ドラムマガジン        rounds per volley +2 · 命中 −5
//   折りたたみストック    機動力 +10%
//   固定ストック          命中 +10
//   調整式ストック        命中 +5 · 機動力 +5%
//   バーチカルグリップ    命中 +10 · ヘッドショット +5
//   アングルグリップ      命中 +5 · 機動力 +10%
// Several parts add up (percentages add, then apply once).
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Blindside.Data
{
    public sealed class Attachment
    {
        public string key, name;
        public int slot, price;
        /// <summary>The illustration: Resources/Equipment/{art}.png.</summary>
        public string art;

        // effects
        public float acc, accNear, accMid, accFar, firstAcc, headshot;   // percentage points
        public float damagePct, mobilityPct;                              // percent
        public int extraShots;
        public float speed;                                               // SPD
        public bool noFalloff, silenced;

        string summary;

        /// <summary>The effects in words (built once).</summary>
        public string Summary => summary ?? (summary = Describe(this));

        static string Describe(Attachment a)
        {
            var sb = new StringBuilder();
            void Add(string s) { if (sb.Length > 0) sb.Append(" · "); sb.Append(s); }
            string P(float v) => (v > 0 ? "+" : "−") + Mathf.Abs(v).ToString("0.#") + "%";
            if (a.silenced) Add("銃声が敵に表示されない");
            if (a.acc != 0) Add("命中率 " + P(a.acc));
            if (a.accNear != 0) Add("近距離命中率 " + P(a.accNear));
            if (a.accMid != 0 && a.accMid == a.accFar) Add("中・遠距離命中率 " + P(a.accMid));
            else
            {
                if (a.accMid != 0) Add("中距離命中率 " + P(a.accMid));
                if (a.accFar != 0) Add("遠距離命中率 " + P(a.accFar));
            }
            if (a.firstAcc != 0) Add("1発目の命中率 " + P(a.firstAcc));
            if (a.headshot != 0) Add("ヘッドショット率 " + P(a.headshot));
            if (a.damagePct != 0) Add("攻撃力 " + P(a.damagePct));
            if (a.noFalloff) Add("距離による攻撃力の低下なし");
            if (a.speed != 0) Add("連射力 (先攻判定 SPD) +" + a.speed.ToString("0.#"));
            if (a.extraShots != 0) Add("攻撃回数 +" + a.extraShots);
            if (a.mobilityPct != 0) Add("機動力 " + P(a.mobilityPct));
            return sb.ToString();
        }
    }

    public static class Attachments
    {
        /// <summary>The slots, in save order (the first four are the original ones).</summary>
        public static readonly string[] Slots = { "マズル", "光学機器", "グリップ", "マガジン", "バレル", "ストック" };
        public const int SlotCount = 6;

        // listed by slot as the shop shows them: 光学機器 · マズル · バレル · マガジン · ストック · グリップ
        public static readonly Attachment[] All =
        {
            new Attachment { key = "holo",        slot = 1, art = "01_holographic_sight",  name = "ホロサイト",             price = 5000,  accNear = 7, headshot = 3 },
            new Attachment { key = "scope2x",     slot = 1, art = "02_scope_2x",           name = "2倍スコープ",            price = 6000,  acc = 3, headshot = 3 },
            new Attachment { key = "scope4x",     slot = 1, art = "03_scope_4x",           name = "4倍スコープ",            price = 8000,  accMid = 5, accNear = -1, headshot = 5 },
            new Attachment { key = "scope8x",     slot = 1, art = "04_scope_8x",           name = "8倍スコープ",            price = 10000, accMid = 5, accFar = 5, accNear = -3, headshot = 5 },
            new Attachment { key = "scope16x",    slot = 1, art = "05_scope_16x",          name = "16倍スコープ",           price = 12000, accFar = 10, accNear = -10, headshot = 20 },
            new Attachment { key = "suppressor",  slot = 0, art = "06_suppressor",         name = "サプレッサー",           price = 8000,  silenced = true, damagePct = -2 },
            new Attachment { key = "muzzlebrake", slot = 0, art = "07_muzzle_brake",       name = "マズルブレーキ",         price = 6000,  firstAcc = 10, headshot = 5 },
            new Attachment { key = "compensator", slot = 0, art = "08_compensator",        name = "コンペンセイター",       price = 6000,  acc = 5, headshot = 3 },
            new Attachment { key = "flashhider",  slot = 0, art = "09_flash_hider",        name = "フラッシュハイダー",     price = 5000,  damagePct = 5, acc = 3 },
            new Attachment { key = "extbarrel",   slot = 4, art = "10_extended_barrel",    name = "ロングバレル",           price = 9000,  noFalloff = true },
            new Attachment { key = "quickmag",    slot = 3, art = "11_quickdraw_magazine", name = "クイックドローマガジン", price = 6000,  speed = 1 },
            new Attachment { key = "extmag",      slot = 3, art = "12_extended_magazine",  name = "拡張マガジン",           price = 7000,  extraShots = 1 },
            new Attachment { key = "beltmag",     slot = 3, art = "13_belt_magazine",      name = "ベルトマガジン",         price = 11000, extraShots = 2, mobilityPct = -10 },
            new Attachment { key = "drummag",     slot = 3, art = "14_drum_magazine",      name = "ドラムマガジン",         price = 9000,  extraShots = 2, acc = -5 },
            new Attachment { key = "foldstock",   slot = 5, art = "15_folding_stock",      name = "折りたたみストック",     price = 5000,  mobilityPct = 10 },
            new Attachment { key = "fixedstock",  slot = 5, art = "16_fixed_stock",        name = "固定ストック",           price = 5000,  acc = 10 },
            new Attachment { key = "adjstock",    slot = 5, art = "17_adjustable_stock",   name = "調整式ストック",         price = 7000,  acc = 5, mobilityPct = 5 },
            new Attachment { key = "vgrip",       slot = 2, art = "18_vertical_grip",      name = "バーチカルグリップ",     price = 5000,  acc = 10, headshot = 5 },
            new Attachment { key = "agrip",       slot = 2, art = "19_angled_grip",        name = "アングルグリップ",       price = 5000,  acc = 5, mobilityPct = 10 },
        };

        public static Attachment Of(string key)
        {
            if (string.IsNullOrEmpty(key)) return null;
            foreach (var a in All) if (a.key == key) return a;
            return null;
        }

        static readonly Dictionary<string, Sprite> art = new Dictionary<string, Sprite>(24);

        /// <summary>The part's illustration (loaded once; null when it is missing).</summary>
        public static Sprite ArtOf(Attachment a)
        {
            if (a == null || string.IsNullOrEmpty(a.art)) return null;
            if (!art.TryGetValue(a.art, out var s)) { s = Resources.Load<Sprite>("Equipment/" + a.art); art[a.art] = s; }
            return s;
        }

        /// <summary>The sum of what `keys` (one per slot; "" = none) give; null when no part is fitted (the unit plays as before).</summary>
        public static AttachmentEffect Sum(string[] keys)
        {
            AttachmentEffect t = null;
            if (keys == null) return null;
            foreach (var k in keys)
            {
                var a = Of(k);
                if (a == null) continue;
                if (t == null) t = new AttachmentEffect();
                t.acc += a.acc; t.accNear += a.accNear; t.accMid += a.accMid; t.accFar += a.accFar;
                t.firstAcc += a.firstAcc; t.headshot += a.headshot; t.damagePct += a.damagePct; t.mobilityPct += a.mobilityPct;
                t.speed += a.speed; t.extraShots += a.extraShots;
                t.noFalloff |= a.noFalloff; t.silenced |= a.silenced;
            }
            return t;
        }
    }

    /// <summary>What a unit's fitted parts give in a match, summed (Attachments.Sum). Hit / headshot in percentage points.</summary>
    public sealed class AttachmentEffect
    {
        public float acc, accNear, accMid, accFar, firstAcc, headshot, damagePct, mobilityPct, speed;
        public int extraShots;
        public bool noFalloff, silenced;

        /// <summary>Range bands by the shot's distance (m): 近距離 ≤ NearMax, 中距離 ≤ MidMax, 遠距離 beyond.</summary>
        public const float NearMax = 3f, MidMax = 7f;

        /// <summary>The hit chance this gear adds at `distance` (the flat bonus + its band's).</summary>
        public float AccuracyAt(float distance) =>
            acc + (distance <= NearMax ? accNear : distance <= MidMax ? accMid : accFar);
    }
}
