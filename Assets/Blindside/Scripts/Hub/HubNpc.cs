// Someone at the HQ the player can walk up to: a station (隊員強化 / ショップ / 会話 / 訓練所 / 作戦室 / 装備設定) with the name shown
// over their head (HubScreen's floating labels) and the radius at which the prompt appears. A 会話 NPC is a squad operator
// (operatorKey, one of HubCatalog.TalkKeys). An operator who runs a station (CRUSHER 作戦室, BASTION 訓練所, IGNIS 隊員・装備管理,
// SERUM 医務室) has both: F opens the station, G talks.
using UnityEngine;

namespace Blindside.Hub
{
    public class HubNpc : MonoBehaviour
    {
        public HubStation station;
        [Tooltip("The name over the head (Japanese).")]
        public string displayName = "";
        [Tooltip("What the station is, under the name (Japanese).")]
        public string caption = "";
        [Tooltip("会話: the operator (CRUSHER / SERUM / IGNIS / BASTION).")]
        public string operatorKey = "";
        public float radius = 2.2f;
        [Tooltip("Height of the floating label over the floor (m).")]
        public float labelHeight = 2.15f;

        public Vector3 LabelPoint => transform.position + Vector3.up * labelHeight;

        /// <summary>A squad operator: G talks (besides F for the station).</summary>
        public bool CanTalk => !string.IsNullOrEmpty(operatorKey);

        /// <summary>Turns (yaw only) to face `p`.</summary>
        public void Face(Vector3 p)
        {
            var d = p - transform.position; d.y = 0f;
            if (d.sqrMagnitude > 1e-4f) transform.rotation = Quaternion.LookRotation(d.normalized, Vector3.up);
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(.12f, .59f, .91f, .6f);
            Gizmos.DrawWireSphere(transform.position, radius);
        }
    }
}
