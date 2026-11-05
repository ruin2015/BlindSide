// The operators' art, by key: the upper-body card illustration (KEY.png) and the face (KEY_face.png). One asset in a Resources
// folder (Resources/OperatorArtLibrary.asset) so a build carries the sprites; filled by the menu Blindside > UI > Build Operator
// Art Library from Assets/Blindside/Art/Operators. Read through OperatorArt, never directly.
using System;
using UnityEngine;

namespace Blindside.UI
{
    public class OperatorArtLibrary : ScriptableObject
    {
        [Serializable]
        public struct Entry
        {
            public string key;      // CRUSHER, FLARE, …
            public Sprite body;     // KEY.png — upper body, for the illustration card
            public Sprite face;     // KEY_face.png — for the roster, the unit panel and the title's squad list
        }

        public Entry[] entries = new Entry[0];
    }
}
