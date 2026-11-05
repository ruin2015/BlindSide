// Port of the FX block in blindside_source/03_renderer_threejs.js: fxTracer (~line 243),
// fxRing (~line 248) and boom (~line 249). The source keeps every effect in a shared `R.fx` list
// ticked once per frame by syncFx(); here each spawned effect owns a small self-ticking
// FxParticle component instead, so no central manager/registration is required.
using UnityEngine;

namespace Blindside.Rendering
{
    public enum FxKind { Fade, Ring, Part, Flash }

    /// <summary>One-shot visual matching a single `R.fx` entry's `k` in syncFx(t).</summary>
    [RequireComponent(typeof(MeshRenderer))]
    public class FxParticle : MonoBehaviour
    {
        public FxKind kind;
        public float durationSeconds;
        public float ringBaseRadius = 1f;
        public Vector3 velocity;   // Part: units/sec (JS units/tick * 60)
        public float gravity;     // Part: JS `vy-=.008` per tick -> per-second below

        float t0;
        Material mat;
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int ColorId = Shader.PropertyToID("_Color");

        void Start()
        {
            t0 = Time.time;
            mat = GetComponent<MeshRenderer>().material; // instance, safe to fade independently
        }

        void Update()
        {
            float p = (Time.time - t0) / durationSeconds;
            if (p >= 1f) { Destroy(gameObject); return; }

            switch (kind)
            {
                case FxKind.Fade:
                    SetAlpha(1f - p);
                    break;
                case FxKind.Ring:
                    { float s = (0.3f + p) * ringBaseRadius; transform.localScale = new Vector3(s, s, s); SetAlpha(1f - p); }
                    break;
                case FxKind.Part:
                    transform.position += velocity * Time.deltaTime;
                    velocity.y -= gravity * Time.deltaTime;
                    if (transform.position.y < 0.03f)
                    {
                        var pos = transform.position; pos.y = 0.03f; transform.position = pos;
                    }
                    SetAlpha(1f - p);
                    break;
                case FxKind.Flash:
                    { float s = 0.2f + p * 0.8f; transform.localScale = new Vector3(s, s, s); SetAlpha(1f - p); }
                    break;
            }
        }

        void SetAlpha(float a)
        {
            var c = mat.HasProperty(BaseColorId) ? mat.GetColor(BaseColorId) : mat.GetColor(ColorId);
            c.a = a;
            if (mat.HasProperty(BaseColorId)) mat.SetColor(BaseColorId, c);
            if (mat.HasProperty(ColorId)) mat.SetColor(ColorId, c);
        }
    }

    /// <summary>Spawns the combat FX above without needing any persistent scene manager.</summary>
    public static class CombatFx
    {
        static Material fxMatTemplate;
        static Material sharedMat
        {
            get
            {
                if (fxMatTemplate == null)
                {
                    var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
                    fxMatTemplate = new Material(shader);
                    fxMatTemplate.SetFloat("_Surface", 1f); // transparent
                    fxMatTemplate.SetFloat("_Blend", 0f);
                    fxMatTemplate.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                    fxMatTemplate.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                    fxMatTemplate.SetFloat("_ZWrite", 0f);
                    fxMatTemplate.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
                }
                return fxMatTemplate;
            }
        }

        static GameObject MakePrimitive(PrimitiveType type, Color color, Vector3 pos, Vector3 scale)
        {
            var go = GameObject.CreatePrimitive(type);
            Object.Destroy(go.GetComponent<Collider>());
            go.transform.position = pos;
            go.transform.localScale = scale;
            var mr = go.GetComponent<MeshRenderer>();
            var mat = new Material(sharedMat);
            mat.SetColor("_BaseColor", color);
            mat.SetColor("_Color", color);
            mr.material = mat;
            return go;
        }

        static FxParticle Attach(GameObject go, FxKind kind, float durationSeconds)
        {
            var fx = go.AddComponent<FxParticle>();
            fx.kind = kind;
            fx.durationSeconds = durationSeconds;
            return fx;
        }

        /// <summary>
        /// Port of fxTracer(a,b,hit): a thin beam from shooter to target plus a brief muzzle flash at the shooter's gun.
        /// A miss flies clearly wide — past one side of the body (.45–.9 m off the line) and on beyond it — from the same two
        /// random draws the source's jitter made (`(Math.random()-.5)*.9` twice), so the random stream is unchanged.
        /// </summary>
        public static void Tracer(Vector3 shooterPos, float shooterFacing, Vector3 targetPos, bool hit, Vector3? muzzle = null)
        {
            if (hit) { TracerTo(shooterPos, shooterFacing, targetPos, muzzle); return; }
            float a = Random.value, b = Random.value;
            var line = targetPos - shooterPos; line.y = 0f;
            var fwd = line.sqrMagnitude > 1e-4f ? line.normalized : Vector3.forward;
            var side = new Vector3(fwd.z, 0f, -fwd.x);
            float off = (a < .5f ? -1f : 1f) * (.45f + Mathf.Abs(a - .5f) * .9f);
            var end = targetPos + side * off + fwd * (1.5f + b * 2f);
            end.y = targetPos.y + (b - .5f) * .4f;
            TracerTo(shooterPos, shooterFacing, end, muzzle);
        }

        /// <summary>The same beam + muzzle flash to an exact end point (no jitter, no random numbers). With the gun's
        /// `muzzle` (a model holding one) the round leaves the muzzle and the flash (MuzzleFlash particles) sits on it;
        /// without, the old spot .55 out along the facing, .55 up.</summary>
        public static void TracerTo(Vector3 shooterPos, float shooterFacing, Vector3 end, Vector3? muzzle = null)
        {
            Vector3 from = muzzle ?? shooterPos;
            Vector3 mid = (from + end) * 0.5f; mid.y = Mathf.Max(0.55f, mid.y);
            float dx = end.x - from.x, dz = end.z - from.z;
            float len = Mathf.Sqrt(dx * dx + dz * dz);

            var beam = MakePrimitive(PrimitiveType.Cube, new Color32(0xff, 0xf0, 0xb4, 0xff), mid, new Vector3(len, 0.035f, 0.035f));
            beam.transform.rotation = Quaternion.Euler(0f, -Mathf.Atan2(dz, dx) * Mathf.Rad2Deg, 0f);
            Attach(beam, FxKind.Fade, 0.2f);

            Vector3 at = muzzle ?? shooterPos + GridSpace.FacingToWorldDirection(shooterFacing) * 0.55f;
            if (!muzzle.HasValue) at.y = 0.55f;
            MuzzleFlash.Play(at, new Vector3(dx, 0f, dz));
        }

        /// <summary>Port of fxRing(x,y,col,d,r): an expanding, fading ring on the floor.</summary>
        public static void Ring(Vector3 pos, Color color, float durationSeconds = 0.6f, float radius = 1f)
        {
            pos.y = 0.08f;
            var go = new GameObject("FxRing");
            go.transform.position = pos;
            var mf = go.AddComponent<MeshFilter>();
            mf.mesh = BuildRingMesh(0.975f, 1f, 64);
            var mr = go.AddComponent<MeshRenderer>();
            var mat = new Material(sharedMat);
            mat.SetColor("_BaseColor", color); mat.SetColor("_Color", color);
            mr.material = mat;
            var fx = Attach(go, FxKind.Ring, durationSeconds);
            fx.ringBaseRadius = radius;
        }

        /// <summary>A small sphere that pops and fades in place — the tracer's muzzle-flash look
        /// (0xffd27a, .14, 90ms fade), reused as a bullet-impact mark on a unit's hit point.</summary>
        public static void Spark(Vector3 pos, Color color, float size = 0.14f, float durationSeconds = 0.09f)
        {
            var go = MakePrimitive(PrimitiveType.Sphere, color, pos, Vector3.one * size);
            Attach(go, FxKind.Fade, durationSeconds);
        }

        /// <summary>
        /// Port of boom(x,y,col,n): a debris burst (n cubes), a bright flash sphere, and a ring —
        /// used for breach/explosion feedback.
        /// </summary>
        public static void Boom(Vector3 pos, Color? color = null, int debrisCount = 22)
        {
            var col = color ?? new Color32(0xff, 0xb1, 0x3b, 0xff);
            var grey = new Color32(0x8a, 0x93, 0x9e, 0xff);
            for (int i = 0; i < debrisCount; i++)
            {
                float s = 0.05f + Random.value * 0.07f;
                var basePos = pos; basePos.y = 0.5f;
                var cube = MakePrimitive(PrimitiveType.Cube, Random.value < 0.5f ? col : grey, basePos, Vector3.one * s);
                float a = Random.value * Mathf.PI * 2f, v = 0.03f + Random.value * 0.07f;
                // JS advances position by (vx,vy,vz) units per ~16.6ms tick; scaled to units/sec here.
                var fx = Attach(cube, FxKind.Part, 0.8f);
                fx.velocity = new Vector3(Mathf.Cos(a) * v, 0.06f + Random.value * 0.08f, Mathf.Sin(a) * v) * 60f;
                fx.gravity = 0.008f * 60f * 60f;
            }
            var flashPos = pos; flashPos.y = 0.5f;
            var flash = MakePrimitive(PrimitiveType.Sphere, new Color32(0xff, 0xc0, 0x60, 0xff), flashPos, Vector3.one * 0.001f);
            Attach(flash, FxKind.Flash, 0.35f);
            Ring(pos, col, 0.5f, 1.4f);
        }

        static Mesh BuildRingMesh(float inner, float outer, int segments)
        {
            var mesh = new Mesh();
            var verts = new Vector3[(segments + 1) * 2];
            var tris = new int[segments * 6];
            for (int i = 0; i <= segments; i++)
            {
                float a = (float)i / segments * Mathf.PI * 2f;
                float cs = Mathf.Cos(a), sn = Mathf.Sin(a);
                verts[i * 2] = new Vector3(cs * inner, 0f, sn * inner);
                verts[i * 2 + 1] = new Vector3(cs * outer, 0f, sn * outer);
            }
            for (int i = 0; i < segments; i++)
            {
                int a = i * 2, b = i * 2 + 1, c = i * 2 + 2, d = i * 2 + 3;
                int t = i * 6;
                tris[t] = a; tris[t + 1] = c; tris[t + 2] = b;
                tris[t + 3] = b; tris[t + 4] = c; tris[t + 5] = d;
            }
            mesh.vertices = verts; mesh.triangles = tris; mesh.RecalculateNormals();
            return mesh;
        }
    }
}
