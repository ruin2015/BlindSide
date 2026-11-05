// Which MapData the game is currently reading. Everything map-dependent goes through MapLibrary.Current; loading a map
// (SetCurrent) is what makes the next match, the exfil zones, the camera starts, ... use it. Until something sets one,
// Current is the read-only default map shipped in Resources/DefaultMap.json.
using UnityEngine;

namespace Blindside.Map
{
    public static class MapLibrary
    {
        public const string DefaultResource = "DefaultMap";

        static MapData current;

        public static MapData Current
        {
            get
            {
                if (current == null) current = LoadDefault();
                return current;
            }
        }

        /// <summary>Makes `data` the map the game reads. Refuses (and keeps the old one) when it fails validation.</summary>
        public static bool SetCurrent(MapData data)
        {
            var problems = data?.Validate();
            if (data == null || problems.Count > 0)
            {
                Debug.LogError("[MapLibrary] Map rejected: " + (data == null ? "null" : string.Join("; ", problems)));
                return false;
            }
            current = data;
            return true;
        }

        /// <summary>A fresh copy of the shipped default map (never the instance the game is mutating).</summary>
        public static MapData LoadDefault()
        {
            var text = Resources.Load<TextAsset>(DefaultResource);
            if (text == null) throw new System.IO.FileNotFoundException("Resources/" + DefaultResource + ".json is missing");
            var data = FromJson(text.text);
            var problems = data.Validate();
            if (problems.Count > 0) throw new System.InvalidOperationException("DefaultMap.json is invalid: " + string.Join("; ", problems));
            return data;
        }

        public static MapData FromJson(string json) => JsonUtility.FromJson<MapData>(json);
        public static string ToJson(MapData data) => JsonUtility.ToJson(data, true);
    }
}
