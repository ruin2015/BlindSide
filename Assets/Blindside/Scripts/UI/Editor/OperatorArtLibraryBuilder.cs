// Editor-only: fills Resources/OperatorArtLibrary.asset from Assets/Blindside/Art/Operators (KEY.png = body, KEY_face.png = face).
//   Blindside ▸ UI ▸ Build Operator Art Library
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Blindside.UI.Editor
{
    public static class OperatorArtLibraryBuilder
    {
        const string ArtFolder = "Assets/Blindside/Art/Operators";
        const string AssetPath = "Assets/Blindside/Resources/OperatorArtLibrary.asset";

        [MenuItem("Blindside/UI/Build Operator Art Library")]
        public static void Build()
        {
            var bodies = new SortedDictionary<string, Sprite>(); var faces = new Dictionary<string, Sprite>();
            foreach (var guid in AssetDatabase.FindAssets("t:Sprite", new[] { ArtFolder }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                string name = Path.GetFileNameWithoutExtension(path);
                if (name.EndsWith("_face")) faces[name.Substring(0, name.Length - 5)] = sprite; else bodies[name] = sprite;
            }
            var lib = AssetDatabase.LoadAssetAtPath<OperatorArtLibrary>(AssetPath);
            if (lib == null) { lib = ScriptableObject.CreateInstance<OperatorArtLibrary>(); AssetDatabase.CreateAsset(lib, AssetPath); }

            var keys = new SortedSet<string>(bodies.Keys); keys.UnionWith(faces.Keys);
            var list = new List<OperatorArtLibrary.Entry>();
            foreach (var k in keys)
            {
                bodies.TryGetValue(k, out var b); faces.TryGetValue(k, out var f);
                list.Add(new OperatorArtLibrary.Entry { key = k, body = b, face = f });
            }
            // operators drawn with someone else's art (OperatorData.artKey, e.g. every defender → TERRORIST): their key maps to it
            int aliased = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:Blindside.Data.OperatorData"))
            {
                var op = AssetDatabase.LoadAssetAtPath<Blindside.Data.OperatorData>(AssetDatabase.GUIDToAssetPath(guid));
                if (op == null || string.IsNullOrEmpty(op.artKey) || op.artKey == op.key) continue;
                bodies.TryGetValue(op.artKey, out var ab); faces.TryGetValue(op.artKey, out var af);
                if (ab == null && af == null) { Debug.LogWarning($"[OperatorArtLibrary] {op.key}: no art named {op.artKey}"); continue; }
                list.RemoveAll(e => e.key == op.key);
                list.Add(new OperatorArtLibrary.Entry { key = op.key, body = ab, face = af });
                aliased++;
            }
            if (aliased > 0) Debug.Log($"[OperatorArtLibrary] {aliased} operator(s) drawn with another's art");
            lib.entries = list.ToArray();
            EditorUtility.SetDirty(lib);
            AssetDatabase.SaveAssets();
            Debug.Log($"[OperatorArtLibrary] {list.Count} operators ({bodies.Count} bodies, {faces.Count} faces) → {AssetPath}");
        }
    }
}
