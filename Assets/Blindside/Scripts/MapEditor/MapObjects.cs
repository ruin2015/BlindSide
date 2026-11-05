// The objects a map carries on top of its terrain, as the editor holds them while editing: the hostage (0 or 1), the five defender
// starts (fixed slots, slot i is operator MapData.DefenderKeys[i]), up to 8 surveillance cameras and up to 8 exfil rectangles.
// Unlike MapData this may be incomplete (that is what the validation panel reports), so it lives in the model, not in MapData;
// a later save step turns a complete one into MapData fields.
using System.Collections.Generic;
using UnityEngine;

namespace Blindside.MapEditor
{
    public enum ObjKind { None, Hostage, Defender, Camera, Exfil }

    /// <summary>One placed object: its kind and the index in its list (Defender: the slot 0..4; Hostage: always 0).</summary>
    public struct ObjRef
    {
        public ObjKind kind;
        public int index;
        public ObjRef(ObjKind kind, int index) { this.kind = kind; this.index = index; }
        public bool IsNone => kind == ObjKind.None;
        public static bool operator ==(ObjRef a, ObjRef b) => a.kind == b.kind && a.index == b.index;
        public static bool operator !=(ObjRef a, ObjRef b) => !(a == b);
        public override bool Equals(object o) => o is ObjRef r && r == this;
        public override int GetHashCode() => ((int)kind << 8) ^ index;
    }

    /// <summary>An exfil zone: an inclusive rectangle of cells. Its letter comes from its position in the list (A, B, C…).</summary>
    public struct ExfilRect
    {
        public int x0, y0, x1, y1;
        public ExfilRect(Vector2Int a, Vector2Int b)
        {
            x0 = Mathf.Min(a.x, b.x); x1 = Mathf.Max(a.x, b.x); y0 = Mathf.Min(a.y, b.y); y1 = Mathf.Max(a.y, b.y);
        }
        public int Width => x1 - x0 + 1;
        public int Height => y1 - y0 + 1;
        public Vector2 Center => new Vector2((x0 + x1) / 2f, (y0 + y1) / 2f);
        public bool Contains(int x, int y) => x >= x0 && x <= x1 && y >= y0 && y <= y1;
        public bool Overlaps(ExfilRect o) => x0 <= o.x1 && o.x0 <= x1 && y0 <= o.y1 && o.y0 <= y1;
    }

    /// <summary>A copy-able set of all the objects — one undo step stores two of these.</summary>
    public sealed class ObjectState
    {
        public const int DefenderSlots = 5;

        public Vector2Int? hostage;
        public readonly bool[] defPlaced = new bool[DefenderSlots];
        public readonly Vector2Int[] defCell = new Vector2Int[DefenderSlots];
        public readonly float[] defFacing = new float[DefenderSlots];
        public readonly List<Vector2Int> cameras = new List<Vector2Int>();
        public readonly List<ExfilRect> exfils = new List<ExfilRect>();

        public int DefenderCount { get { int n = 0; foreach (var p in defPlaced) if (p) n++; return n; } }

        public ObjectState Clone()
        {
            var o = new ObjectState { hostage = hostage };
            System.Array.Copy(defPlaced, o.defPlaced, DefenderSlots);
            System.Array.Copy(defCell, o.defCell, DefenderSlots);
            System.Array.Copy(defFacing, o.defFacing, DefenderSlots);
            o.cameras.AddRange(cameras);
            o.exfils.AddRange(exfils);
            return o;
        }

        public bool SameAs(ObjectState o)
        {
            if (hostage != o.hostage || cameras.Count != o.cameras.Count || exfils.Count != o.exfils.Count) return false;
            for (int i = 0; i < DefenderSlots; i++)
            {
                if (defPlaced[i] != o.defPlaced[i]) return false;
                if (defPlaced[i] && (defCell[i] != o.defCell[i] || defFacing[i] != o.defFacing[i])) return false;
            }
            for (int i = 0; i < cameras.Count; i++) if (cameras[i] != o.cameras[i]) return false;
            for (int i = 0; i < exfils.Count; i++)
            {
                var a = exfils[i]; var b = o.exfils[i];
                if (a.x0 != b.x0 || a.y0 != b.y0 || a.x1 != b.x1 || a.y1 != b.y1) return false;
            }
            return true;
        }
    }
}
