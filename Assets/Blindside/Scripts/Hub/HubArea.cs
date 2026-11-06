// A named place at the HQ (ロビー, 作戦室, 医務室 …): a box on the floor plan (XZ, centred on this object). HubDirector shows the
// smallest area holding the player next to 「本部」 in the top left; the minimap prints the names too.
using UnityEngine;

namespace Blindside.Hub
{
    public class HubArea : MonoBehaviour
    {
        [Tooltip("The place's name (Japanese).")]
        public string displayName = "";
        [Tooltip("Width (x) and depth (z) of the area, m.")]
        public Vector2 size = new Vector2(8f, 8f);

        public bool Contains(Vector3 p)
        {
            var c = transform.position;
            return Mathf.Abs(p.x - c.x) <= size.x * .5f && Mathf.Abs(p.z - c.z) <= size.y * .5f;
        }

        public float Extent => size.x * size.y;

        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, .78f, .12f, .5f);
            Gizmos.DrawWireCube(transform.position + Vector3.up * .05f, new Vector3(size.x, .1f, size.y));
        }
    }
}
