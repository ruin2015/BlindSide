// Generates the WeaponData / OperatorData assets from the literal WEAP / OPS
// tables in blindside_source/01_core_data_map_vision.js.
// Menu: Blindside > Data > Generate Weapons & Operators
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Blindside.Data.EditorTools
{
    public static class BlindsideDataBuilder
    {
        const string WeaponDir = "Assets/Blindside/Data/Weapons";
        const string OperatorDir = "Assets/Blindside/Data/Operators";

        [MenuItem("Blindside/Data/Generate Weapons & Operators")]
        public static void Generate()
        {
            EnsureFolder("Assets/Blindside", "Data");
            EnsureFolder("Assets/Blindside/Data", "Weapons");
            EnsureFolder("Assets/Blindside/Data", "Operators");

            var weapons = new Dictionary<string, WeaponData>
            {
                ["AR"] = CreateWeapon("AR", "K-417 突撃銃", 18, 3, 76, 9, false),
                ["SMG"] = CreateWeapon("SMG", "V-9 短機関銃", 12, 5, 70, 6, false),
                ["SG"] = CreateWeapon("SG", "M-870 散弾銃", 10, 6, 62, 4, true),
                ["PST"] = CreateWeapon("PST", "P-12 拳銃", 22, 2, 74, 6, false),
            };

            CreateOperator("CRUSHER", "クラッシャー", "ブリーチャー", OperatorTeam.Attack, 120, 2, weapons["AR"],
                "hammer", "ブリーチングハンマー", 5, "#eaaf20", "隣接する通常の壁をハンマーで破壊し、通れる新しい通路を作る。");
            CreateOperator("FLARE", "フレア", "遠距離ブリーチャー", OperatorTeam.Attack, 100, 3, weapons["AR"],
                "launcher", "ブリーチングランチャー", 4, "#f8594f", "遠距離から爆発物を発射して壁・強化壁に穴を開ける。視界のみ開き、通行は不可。");
            CreateOperator("IGNIS", "イグニス", "ハードブリーチャー", OperatorTeam.Attack, 120, 2, weapons["SMG"],
                "thermite", "発熱爆薬", 2, "#00cef9", "隣接する壁・強化壁に爆薬を設置し、通行可能な通路を開く。通電中の壁には設置不可。");
            CreateOperator("SERUM", "セラム", "メディック", OperatorTeam.Attack, 100, 3, weapons["SMG"],
                "stim", "戦闘スティム", 3, "#55ca8a", "近くの味方に薬剤を投与し、体力を50回復、今ターンの移動力を1.5倍にする。");
            CreateOperator("BASTION", "バスティオン", "シールド", OperatorTeam.Attack, 140, 1, weapons["PST"],
                "shield", "展開式シールド", 99, "#9e6abf", "シールドを展開し、次の自分のターンまで正面からの攻撃を無効化する。展開中も移動可能。");

            CreateOperator("VOLT", "ボルト", "電子戦", OperatorTeam.Defense, 120, 2, weapons["SG"],
                "shock", "ショックワイヤー", 5, "#ef8202", "円形の電界を設置する。範囲内の敵ガジェットを破壊し、強化壁に電流を流して爆薬の設置を妨げる。");
            CreateOperator("MIASMA", "ミアズマ", "毒ガス", OperatorTeam.Defense, 120, 2, weapons["SMG"],
                "gas", "毒ガス弾", 3, "#aee302", "ガス弾を投げて視界を遮り、範囲内の敵にダメージを与える。2ターン持続。");
            CreateOperator("WRAITH", "レイス", "ローマー", OperatorTeam.Defense, 100, 3, weapons["SMG"],
                "ghost", "電波妨害", 3, "#4d7fff", "次の自分のターンまで、敵のドローン・監視装置に捕捉されない。使用後も移動可能。");
            CreateOperator("OBSIDIAN", "オブシディアン", "情報", OperatorTeam.Defense, 140, 1, weapons["AR"],
                "mirror", "ブラックミラー", 2, "#ff0e1b", "隣接する壁に片面防弾ミラーを設置する。防衛チームだけが向こう側を見られる。");
            CreateOperator("ARGUS", "アルゴス", "監視", OperatorTeam.Defense, 120, 2, weapons["AR"],
                "cam", "ブラックアイ", 3, "#cc5f4d", "周囲に監視カメラを追加設置する。ガジェットからカメラ映像を確認できる。");

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[Blindside] Data generated: 4 WeaponData in {WeaponDir}, 10 OperatorData in {OperatorDir}.");
        }

        static void EnsureFolder(string parent, string name)
        {
            string path = parent + "/" + name;
            if (!AssetDatabase.IsValidFolder(path)) AssetDatabase.CreateFolder(parent, name);
        }

        static WeaponData CreateWeapon(string key, string name, int dmg, int shots, int acc, int range, bool breach)
        {
            string path = $"{WeaponDir}/Weapon_{key}.asset";
            var w = AssetDatabase.LoadAssetAtPath<WeaponData>(path);
            if (w == null) { w = ScriptableObject.CreateInstance<WeaponData>(); AssetDatabase.CreateAsset(w, path); }
            w.key = key; w.weaponName = name; w.damage = dmg; w.shots = shots; w.accuracy = acc; w.range = range; w.breach = breach;
            EditorUtility.SetDirty(w);
            return w;
        }

        static void CreateOperator(string key, string kr, string role, OperatorTeam team, int hp, int mv, WeaponData weapon,
            string abilityKey, string abilityName, int charges, string hex, string desc)
        {
            string path = $"{OperatorDir}/Operator_{key}.asset";
            var o = AssetDatabase.LoadAssetAtPath<OperatorData>(path);
            if (o == null) { o = ScriptableObject.CreateInstance<OperatorData>(); AssetDatabase.CreateAsset(o, path); }
            ColorUtility.TryParseHtmlString(hex, out var color);
            o.key = key; o.kr = kr; o.role = role; o.team = team; o.hp = hp; o.mv = mv; o.weapon = weapon;
            o.abilityKey = abilityKey; o.abilityName = abilityName; o.charges = charges; o.color = color; o.description = desc;
            EditorUtility.SetDirty(o);
        }
    }
}
