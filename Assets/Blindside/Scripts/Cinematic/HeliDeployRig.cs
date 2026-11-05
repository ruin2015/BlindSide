// Script-driven pose of the helicopter during the deploy phase — the G.heli state (`{x,y,z,hd,rope,tilt,vis}`)
// of the source plus syncHeli(), for the part after the intro. The intro's IntroCameraRig stops writing the
// HeliRig transform when the Timeline ends (and switches the object off), so from then on this component owns
// it: Begin() puts it back where the intro left it, and Fly / RopeTo / Leave are the tweens that rappel() and
// heliLeave() (05_ui_input_intro.js ~270-279) run on it.
//
// Coordinates are grid coordinates (y as in the map); height z is metres above the floor. Only the drawing
// goes through GridSpace (world z = -y). It lives on an always-active object (MatchBootstrap's) because the
// HeliRig object itself is deactivated whenever the helicopter isn't shown.
using System.Collections;
using UnityEngine;
using Blindside.Rendering;

namespace Blindside.Cinematic
{
    public class HeliDeployRig : MonoBehaviour
    {
        /// <summary>Metres the helicopter hovers above the source's height 6 (intro's last point and the deploy hover):
        /// clear of the intro house's roof. The rope and the rappel are lengthened by the same amount.</summary>
        public const float ExtraHeight = 5f;
        /// <summary>Rope length of a full lowering: the source's `rope:4.75` at hull scale 1.5 reaches from height 6 to .05 above
        /// the floor; this is the length that does the same from 6 + ExtraHeight at HullScale (HeliRig: end at local
        /// y = −0.8 − length/1.5, times the root's scale).</summary>
        public const float RopeFull = 1.5f * ((5.95f + ExtraHeight) / HullScale - 0.8f);
        /// <summary>Where the rope's end sits above the floor when it is fully out (source: 6 − 1.2 − 4.75 = .05).</summary>
        const float RopeTipAboveFloor = 0.05f;

        HeliRig rig;
        /// <summary>Whether the helicopter hovers over the map while the drop points are picked (off: it isn't shown at all).</summary>
        public static bool ShowDuringDeploy = false;

        // G.heli
        public float X, Y, Z = 6f, Hd, Tilt, Rope;
        public bool Visible;
        /// <summary>Hover height at which a fully lowered rope just touches the floor.</summary>
        public float RestZ { get; private set; } = 6f;

        public bool IsReady => rig != null;

        /// <summary>makeHeli()'s `g.scale.set(1.5,1.5,1.5)`. The Unity prefab is authored at 1× (the intro flies it that
        /// way), so the deploy phase applies the source's scale: with it, the source's numbers — hull at height 6, rope
        /// 4.75, figure appearing 4.4 up — put the rope's end on the floor exactly as they do there.</summary>
        /// The prefab's body is now drawn at real scale for the intro (BobVisual/Body ×1.75: a ~19 m Black Hawk next to the
        /// 20 m house), so the deploy phase scales the root by 1.5 / 1.75 — the helicopter over the map keeps its size.
        public const float IntroBodyScale = 1.75f;
        public const float HullScale = 1.5f / IntroBodyScale;
        Vector3 baseScale = Vector3.one;

        public void Setup(HeliRig r)
        {
            if (r != null && r != rig) baseScale = r.transform.localScale;   // read once: a later match must not compound the 1.5×
            rig = r;
        }

        /// <summary>Puts the helicopter back at the pose the intro ended on: `Object.assign(h,heliPath(1))` —
        /// over (10.5, 1.5), nose along the last leg of the approach — hovering at the rope-reaches-floor height.</summary>
        public void Begin()
        {
            if (rig == null) return;
            // the helicopter got in the way of picking drop points: the deployment runs without it (the intro already
            // showed the insertion; Rappel drops the figures in on their own)
            if (!ShowDuringDeploy) { Hide(); return; }
            // heliPath's last leg runs from HELI_P[2] = (HX+dx·6, HZ+dz·6) to HELI_P[3] = (10.5, 1.5).
            // (a map without the house intro flies the simple intro: the helicopter comes to rest over the map's middle — SimpleIntro)
            var data = Blindside.Map.MapLibrary.Current;
            float HX = data.IntroCenter.x, HZ = data.IntroCenter.y, hoverX = data.HeliHover.x, hoverY = data.HeliHover.y;
            float p = 4200f / 11500f, e = p * p * (3f - 2f * p), a = 1f + e * 0.7f;
            float ax = HX + Mathf.Cos(a) * 6f, ay = HZ + Mathf.Sin(a) * 6f;
            rig.transform.localScale = baseScale * HullScale;
            CalibrateRestHeight();
            X = hoverX; Y = hoverY; Z = RestZ;
            // the house intro ends nose-in, facing the house (IntroCameraRig turns it on the last leg); the simple intro
            // keeps the last leg's heading
            Hd = data.useHouseIntro ? IntroCameraRig.HeadingToHouse(hoverX, hoverY) : Mathf.Atan2(hoverY - ay, hoverX - ax);
            Tilt = 0f; Rope = 0f; Visible = true;
            Apply();
        }

        /// <summary>Measures the rig: with the rope out by RopeFull, how far below the hull origin does its end hang.</summary>
        void CalibrateRestHeight()
        {
            RestZ = 6f + ExtraHeight;
            if (rig == null || rig.ropeVisual == null || rig.ropeVisual.parent == null) return;
            // HeliRig: rope centre local y = -0.8 - L/2 and mesh length = L·(cylinder 2 units × scale L/2), L = length/1.5,
            // so the end is at local y = -0.8 - L (the bob only moves the body, not the rope) — times the root's scale.
            float L = RopeFull / 1.5f;
            var parent = rig.ropeVisual.parent;
            float tipY = parent.TransformPoint(new Vector3(rig.ropeVisual.localPosition.x, -0.8f - L, rig.ropeVisual.localPosition.z)).y;
            float originY = rig.transform.position.y;
            float hang = originY - tipY;   // metres from the hull origin down to the rope end
            if (hang > 0.5f && hang < 40f) RestZ = hang + RopeTipAboveFloor;
        }

        void LateUpdate()
        {
            if (rig != null && Visible) Apply();
        }

        /// <summary>syncHeli(): `g.position.set(h.x, h.z, h.y)`, yaw −hd, body tilt, rope length.</summary>
        void Apply()
        {
            if (rig == null) return;
            if (!rig.gameObject.activeSelf) rig.gameObject.SetActive(true);
            rig.transform.SetPositionAndRotation(GridSpace.GridToWorld(X, Y, Z),
                Quaternion.LookRotation(GridSpace.FacingToWorldDirection(Hd), Vector3.up));
            rig.tilt = Tilt;
            rig.roll = 0f;   // the intro banks it; the deployment's short hops fly level
            rig.ropeLength = Rope;
        }

        void Hide()
        {
            Visible = false; Tilt = 0f; Rope = 0f;
            if (rig != null) { rig.ropeLength = 0f; rig.transform.localScale = baseScale; rig.gameObject.SetActive(false); }
        }

        static float EaseIO(float p) => p < .5f ? 2f * p * p : 1f - Mathf.Pow(-2f * p + 2f, 2f) / 2f;

        /// <summary>tween(h,{x,y,z},ms): easeIO from wherever it is now.</summary>
        public IEnumerator Fly(float x, float y, float z, float seconds)
        {
            float x0 = X, y0 = Y, z0 = Z, t = 0f;
            while (t < seconds)
            {
                t += Time.unscaledDeltaTime;
                float e = EaseIO(Mathf.Clamp01(t / seconds));
                X = Mathf.Lerp(x0, x, e); Y = Mathf.Lerp(y0, y, e); Z = Mathf.Lerp(z0, z, e);
                yield return null;
            }
            X = x; Y = y; Z = z;
        }

        /// <summary>tween(h,{rope},ms,true): linear.</summary>
        public IEnumerator RopeTo(float length, float seconds)
        {
            float r0 = Rope, t = 0f;
            while (t < seconds)
            {
                t += Time.unscaledDeltaTime;
                Rope = Mathf.Lerp(r0, length, Mathf.Clamp01(t / seconds));
                yield return null;
            }
            Rope = length;
        }

        /// <summary>heliLeave(): fly off backwards (out the top of the map, left if it is over x&lt;13, else right)
        /// for 1.4 s, then disappear. Heading uses the source's own numbers (−30 / 60−x), the flight its −18 / 42.</summary>
        public IEnumerator Leave()
        {
            if (!Visible) yield break;
            // Off the top of the map, out the side it is nearer to: left of the map's middle → off the left edge, else off the right.
            float W = Blindside.Map.MapLibrary.Current.width;
            bool left = X < W * .5f;
            Hd = Mathf.Atan2(-12f - Y, left ? -30f : (W + 34f) - X);
            Tilt = -.15f;
            float tx = left ? -18f : W + 16f;
            yield return Fly(tx, -12f, 9f, 1.4f);
            Hide();
        }
    }
}
