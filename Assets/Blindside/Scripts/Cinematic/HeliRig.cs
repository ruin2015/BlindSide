// Visual + idle-animation port of makeHeli()/syncHeli(t) in blindside_source/03_renderer_threejs.js
// (~lines 362-390). Geometry (hull/skids/rotor/tail rotor/rope) is authored on the prefab; this
// component only replays what syncHeli() computed every frame: continuous rotor/tail-rotor spin,
// a small idle bob (`Math.sin(t/400)*.08`), and the rope's length-driven scale/position
// (`L=max(.001,rope)/1.5; rope.scale.set(1,L,1); rope.position.y=-.8-L/2`). `Tilt` and `RopeLength`
// are driven externally (e.g. by a Timeline AnimationTrack) instead of by the JS tween helpers.
using UnityEngine;

namespace Blindside.Cinematic
{
    public class HeliRig : MonoBehaviour
    {
        [Header("Refs")]
        public Transform body;        // pitches with Tilt (source: `body.rotation.z`), banks with Roll
        public Transform rotor;       // main rotor, spins around Y
        public Transform tailRotor;   // tail rotor, spins around Z
        public Transform ropeVisual;  // cylinder scaled/positioned by RopeLength
        public Transform bobVisual;   // child that gets the idle sine bob, keeps the root's Timeline-driven position clean

        [Header("Live state (settable from code or a Timeline track)")]
        [Range(-0.5f, 0.5f)] public float tilt;         // radians, source: h.tilt (negative: nose down)
        [Range(-0.8f, 0.8f)] public float roll;         // radians, bank (positive: right side up — a left turn)
        [Range(0f, 30f)] public float ropeLength;        // source: h.rope (world length = ropeLength / 1.5 × the root's scale)

        const float RotorSpeedDeg = 2050f;   // ~ source's `t/28` (t in ms) converted to deg/s
        const float TailSpeedDeg = 2865f;    // ~ source's `t/20`
        const float BobAmplitude = 0.08f, BobPeriod = 0.4f;

        void Update()
        {
            if (rotor) rotor.Rotate(0f, RotorSpeedDeg * Time.deltaTime, 0f, Space.Self);
            if (tailRotor) tailRotor.Rotate(0f, 0f, TailSpeedDeg * Time.deltaTime, Space.Self);
            // the source's hull pointed along +X, so its z-rotation was the pitch; this model's nose is +Z: pitch about X
            if (body) body.localRotation = Quaternion.Euler(-tilt * Mathf.Rad2Deg, 0f, roll * Mathf.Rad2Deg);
            if (bobVisual) bobVisual.localPosition = new Vector3(0f, Mathf.Sin(Time.time / BobPeriod) * BobAmplitude, 0f);

            if (ropeVisual)
            {
                // World-space rope length, matching source's `L`. Unity's default cylinder mesh is
                // 2 units tall (vs. the source's 1-unit-tall geometry), so scale.y = L/2 here.
                float worldLength = Mathf.Max(0.001f, ropeLength) / 1.5f;
                bool visible = ropeLength > 0.02f;
                ropeVisual.gameObject.SetActive(visible);
                if (visible)
                {
                    var s = ropeVisual.localScale; s.y = worldLength / 2f; ropeVisual.localScale = s;
                    ropeVisual.localPosition = new Vector3(0f, -0.8f - worldLength / 2f, 0f);
                }
            }
        }
    }
}
