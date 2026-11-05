// The tutorial's training dummy for the shot through a wall: a white figure on a stand with orange target rings on its chest,
// made of primitives. Hit() knocks it back on its spring (a damped wobble on the unscaled clock) with a burst of sparks.
using UnityEngine;
using Blindside.Rendering;
using Blindside.UI;

namespace Blindside.Tutorial
{
    public class TutorialDummy : MonoBehaviour, ITimedEffect
    {
        public const float Height = 1.8f, WobbleTime = 1.6f, Knock = 38f;
        Transform pivot;
        float hitT = -1f, clock, hitDir;
        readonly System.Collections.Generic.List<Material> mats = new System.Collections.Generic.List<Material>();

        public float Duration => WobbleTime;

        public static TutorialDummy Create(Vector2 cell, float faceToward, Transform parent)
        {
            var go = new GameObject("TutorialDummy");
            go.transform.SetParent(parent, false);
            go.transform.position = GridSpace.GridToWorld(cell.x, cell.y, 0f);
            var d = go.AddComponent<TutorialDummy>();
            d.Build(faceToward);
            return d;
        }

        void Build(float faceToward)
        {
            var white = Mat(new Color(.95f, .96f, .97f), Color.black);
            var dark = Mat(new Color(.18f, .2f, .23f), Color.black);
            var orange = Mat(new Color(1f, .45f, .1f), new Color(1f, .4f, .05f) * 1.4f);
            Part(PrimitiveType.Cylinder, "Base", transform, new Vector3(0f, .03f, 0f), new Vector3(.7f, .03f, .7f), dark);
            pivot = new GameObject("Spring").transform;
            pivot.SetParent(transform, false);
            pivot.localPosition = new Vector3(0f, .06f, 0f);
            // face the shooter (grid angle → world yaw)
            pivot.localRotation = Quaternion.LookRotation(GridSpace.GridToWorld(Mathf.Cos(faceToward), Mathf.Sin(faceToward)), Vector3.up);
            Part(PrimitiveType.Cylinder, "Post", pivot, new Vector3(0f, .45f, 0f), new Vector3(.08f, .45f, .08f), dark);
            Part(PrimitiveType.Capsule, "Body", pivot, new Vector3(0f, 1.15f, 0f), new Vector3(.55f, .5f, .32f), white);
            Part(PrimitiveType.Sphere, "Head", pivot, new Vector3(0f, 1.6f, 0f), new Vector3(.3f, .32f, .3f), white);
            // target rings on the chest (facing the shooter: local +z)
            Part(PrimitiveType.Cylinder, "RingOuter", pivot, new Vector3(0f, 1.2f, .17f), new Vector3(.36f, .005f, .36f), orange, new Vector3(90f, 0f, 0f));
            Part(PrimitiveType.Cylinder, "RingInner", pivot, new Vector3(0f, 1.2f, .175f), new Vector3(.24f, .005f, .24f), white, new Vector3(90f, 0f, 0f));
            Part(PrimitiveType.Cylinder, "Bull", pivot, new Vector3(0f, 1.2f, .18f), new Vector3(.12f, .005f, .12f), orange, new Vector3(90f, 0f, 0f));
        }

        public void Hit(float fromGridAngle)
        {
            hitT = clock; hitDir = 1f;
            CombatFx.Boom(transform.position + Vector3.up * 1.2f, new Color32(0xff, 0x8a, 0x2a, 0xff), 12);
        }

        void Update()
        {
            clock += Time.unscaledDeltaTime;
            Evaluate(hitT >= 0f ? clock - hitT : WobbleTime);
        }

        /// <summary>`t` seconds after a hit: knocked back Knock°, then a damped spring back upright.</summary>
        public void Evaluate(float t)
        {
            if (pivot == null) return;
            float a = t >= WobbleTime ? 0f : Knock * hitDir * Mathf.Exp(-t * 3.2f) * Mathf.Cos(t * 11f);
            var e = pivot.localEulerAngles; e.x = -a; pivot.localEulerAngles = e;
        }

        Material Mat(Color c, Color emission)
        {
            var m = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
            m.SetColor("_BaseColor", c);
            if (emission.maxColorComponent > 0f) { m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", emission); }
            mats.Add(m);
            return m;
        }

        static void Part(PrimitiveType type, string name, Transform parent, Vector3 pos, Vector3 scale, Material mat, Vector3? euler = null)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            if (euler.HasValue) go.transform.localEulerAngles = euler.Value;
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
        }

        void OnDestroy() { foreach (var m in mats) if (m != null) Destroy(m); }
    }
}
