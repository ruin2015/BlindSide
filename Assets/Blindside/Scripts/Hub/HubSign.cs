// A room's name at the HQ (医務室, トレーニングルーム …): drawn by HubScreen as a floating label at this point (over the door), like the
// stations' names but with nothing to interact with.
using UnityEngine;

namespace Blindside.Hub
{
    public class HubSign : MonoBehaviour
    {
        [Tooltip("The room's name (Japanese).")]
        public string displayName = "";
        [Tooltip("Under the name: the English (ROOM) or whose room it is.")]
        public string caption = "";
    }
}
