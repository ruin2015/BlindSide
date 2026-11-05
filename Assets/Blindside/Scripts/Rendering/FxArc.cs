// Port of fxArc(a,bx,by,col,d) in blindside_source/03_renderer_threejs.js (~254) and its 'arc' branch in
// the fx loop (~263): a small sphere (GEO.sph r=1 scaled .1) lobbed from the thrower to (bx,by),
//   position = (x0+(x1-x0)*p, .6+sin(p*PI)*2.2, y0+(y1-y0)*p), removed at p>=1.
// Its own file: a MonoBehaviour that's AddComponent'd needs to be the file's main class (see CameraPunch.cs).
using UnityEngine;

namespace Blindside.Rendering
{
    public class FxArc : MonoBehaviour
    {
        float x0, y0, x1, y1, dur, t;

        /// <summary>fxArc: throw from grid (fromX,fromY) to (toX,toY) over `seconds`.</summary>
        public static void Spawn(float fromX, float fromY, float toX, float toY, Color color, float seconds)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "FxArc";
            Destroy(go.GetComponent<Collider>());
            go.transform.localScale = Vector3.one * .2f;   // r=1 × .1 → diameter .2
            var mr = go.GetComponent<MeshRenderer>();
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
            var mat = new Material(shader);                  // mkBasic(col): opaque, unlit
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);
            mr.material = mat;
            var fx = go.AddComponent<FxArc>();
            fx.x0 = fromX; fx.y0 = fromY; fx.x1 = toX; fx.y1 = toY; fx.dur = Mathf.Max(.001f, seconds);
            fx.Place(0f);
        }

        // Scaled time, like the WaitForSeconds the ability waits on while the shell is in the air.
        void Update()
        {
            t += Time.deltaTime;
            float p = t / dur;
            if (p >= 1f) { Destroy(gameObject); return; }
            Place(p);
        }

        void Place(float p) =>
            transform.position = GridSpace.GridToWorld(x0 + (x1 - x0) * p, y0 + (y1 - y0) * p, .6f + Mathf.Sin(p * Mathf.PI) * 2.2f);

        void OnDestroy()
        {
            var mr = GetComponent<MeshRenderer>();
            if (mr != null && mr.material != null) Destroy(mr.material);
        }
    }
}
