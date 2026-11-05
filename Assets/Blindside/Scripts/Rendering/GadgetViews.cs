// Drone/camera visuals ported from the `drones`/`cams` blocks of syncGadgets() in
// blindside_source/03_renderer_threejs.js (lines ~187-193). Geometry is authored on the prefab;
// these components only replay the per-frame animation the source computed inline each tick.
using UnityEngine;

namespace Blindside.Rendering
{
    /// <summary>
    /// Port of the drone branch of syncGadgets(t): a hovering quad-ring drone that bobs
    /// (`o.position.set(d.rx,.34+Math.sin(t/200)*.04,d.ry)`) and shows a friendly detection
    /// ring underneath when it belongs to the viewing side.
    /// </summary>
    public class DroneView : MonoBehaviour
    {
        [SerializeField] Renderer[] teamTintRenderers; // the 4 corner rings + eye
        [SerializeField] Transform friendlyRing;       // `rr`: only added for the player's own drones

        static readonly Color AllyEye = new Color32(0x7c, 0xf0, 0xc8, 0xff);
        static readonly Color EnemyEye = new Color32(0xff, 0x3b, 0x54, 0xff);

        const float BobHeight = 0.34f, BobAmp = 0.04f, BobPeriodMs = 200f;

        public void SetTeamColor(bool isAlly)
        {
            var c = isAlly ? AllyEye : EnemyEye;
            if (teamTintRenderers != null)
                foreach (var r in teamTintRenderers)
                    if (r) SetColor(r, c);
            if (friendlyRing) friendlyRing.gameObject.SetActive(isAlly);
        }

        /// <param name="timeMs">Same clock unit as the source's `t` (milliseconds).</param>
        public void SetPose(float x, float y, float timeMs, bool visible)
        {
            gameObject.SetActive(visible);
            if (!visible) return;
            float bobY = BobHeight + Mathf.Sin(timeMs / BobPeriodMs) * BobAmp;
            transform.position = GridSpace.GridToWorld(x, y, bobY);
            if (friendlyRing) friendlyRing.localPosition = new Vector3(0f, -bobY + 0.05f, 0f);
        }

        static void SetColor(Renderer r, Color c)
        {
            var mpb = new MaterialPropertyBlock();
            r.GetPropertyBlock(mpb);
            mpb.SetColor("_BaseColor", c);
            mpb.SetColor("_Color", c);
            r.SetPropertyBlock(mpb);
        }
    }

    /// <summary>
    /// Port of the cams branch of syncGadgets(t): a fixed surveillance camera that idles-sways
    /// (`o.rotation.y=Math.sin(t/1500+c.x)*.8`) and blinks a red LED (`led.visible=Math.sin(t/200)>0`).
    /// Only visible to the defense side, or once discovered/seen by attack (`c.discovered||vis(c.x,c.y)`).
    /// </summary>
    public class CameraView : MonoBehaviour
    {
        [SerializeField] Transform head;
        [SerializeField] Renderer led;

        const float SwayPeriodMs = 1500f, SwayAmpRad = 0.8f;
        const float BlinkPeriodMs = 200f;

        float swayPhase;

        public void SetPose(float x, float y, float timeMs, bool visible)
        {
            gameObject.SetActive(visible);
            if (!visible) return;
            transform.position = GridSpace.GridToWorld(x, y);
            float yawRad = Mathf.Sin(timeMs / SwayPeriodMs + x) * SwayAmpRad;
            (head != null ? head : transform).localRotation = Quaternion.Euler(0f, -yawRad * Mathf.Rad2Deg, 0f);
            if (led) led.enabled = Mathf.Sin(timeMs / BlinkPeriodMs) > 0f;
        }
    }
}
