// The status screen's 3D figure (HubStatusScreen): one operator standing alone, a little turned, its left arm raised out to the side
// (the right one hanging) — the model's own humanoid bones set by hand from its rest pose, the animator off, so the pose holds in
// play and in previews alike. Built far below the HQ (y −360) with its own camera into a portrait RenderTexture; the camera renders
// only while the screen is open (SetLive) or once on demand (RenderNow). Nothing is allocated per frame.
using UnityEngine;
using Blindside.Data;
using Blindside.Rendering;

namespace Blindside.Hub
{
    public class HubStatusStage : MonoBehaviour
    {
        public static readonly Vector3 Origin = new Vector3(0f, -360f, 0f);
        public const int TexW = 840, TexH = 1320;
        // the camera: in front, chest height, framing the whole figure with a little room over the raised hand
        const float CamBack = 4.3f, CamHeight = 1.0f, LookHeight = .95f, Fov = 30f, Turn = 18f;
        // the pose: the raised arm a little above the shoulder line, the hanging one a little away from the hip
        const float RaiseUp = .32f, HangOut = .16f;

        Camera cam;
        RenderTexture rt;
        GameObject figure;
        string key;

        public RenderTexture Texture => rt;

        public static HubStatusStage Build()
        {
            var go = new GameObject("HubStatusStage");
            go.transform.position = Origin;
            var s = go.AddComponent<HubStatusStage>();
            s.BuildRoom();
            return s;
        }

        void BuildRoom()
        {
            var key = AddLight("Key", LightType.Spot, new Vector3(-1.4f, 3.6f, -3f), new Color(1f, .96f, .9f), 12f, 10f);
            key.spotAngle = 60f; key.innerSpotAngle = 30f; key.shadows = LightShadows.Soft;
            key.transform.LookAt(transform.TransformPoint(new Vector3(0f, 1f, 0f)));
            var rim = AddLight("Rim", LightType.Spot, new Vector3(1.6f, 2.6f, 2.4f), new Color(.45f, .7f, 1f), 10f, 8f);
            rim.spotAngle = 70f;
            rim.transform.LookAt(transform.TransformPoint(new Vector3(0f, 1.2f, 0f)));
            AddLight("Fill", LightType.Point, new Vector3(1.8f, 1.2f, -2.4f), new Color(1f, .75f, .6f), 1.6f, 7f);

            var cgo = new GameObject("StatusCamera");
            cgo.transform.SetParent(transform, false);
            cam = cgo.AddComponent<Camera>();
            cam.fieldOfView = Fov; cam.nearClipPlane = .05f; cam.farClipPlane = 20f;
            cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(.055f, .062f, .075f);
            cam.transform.localPosition = new Vector3(0f, CamHeight, -CamBack);
            cam.transform.localRotation = Quaternion.LookRotation(new Vector3(0f, LookHeight - CamHeight, CamBack), Vector3.up);
            rt = new RenderTexture(TexW, TexH, 24) { name = "StatusRT", antiAliasing = 4 };
            cam.targetTexture = rt;
            cam.enabled = false;
        }

        Light AddLight(string name, LightType type, Vector3 pos, Color c, float intensity, float range)
        {
            var l = new GameObject(name).AddComponent<Light>();
            l.transform.SetParent(transform, false);
            l.transform.localPosition = pos;
            l.type = type; l.color = c; l.intensity = intensity; l.range = range;
            l.shadows = LightShadows.None;
            return l;
        }

        /// <summary>`op` stands posed (the same operator again: nothing changes; null: nobody).</summary>
        public void Show(OperatorData op)
        {
            string k = op != null ? op.key : null;
            if (k == key && (figure != null || k == null)) return;
            if (figure != null) Kill(figure);
            figure = null; key = k;
            if (op == null || op.modelPrefab == null) return;
            var go = Instantiate(op.modelPrefab, transform, false);
            go.name = "StatusFigure_" + op.key;
            foreach (var c in go.GetComponentsInChildren<Collider>(true)) Kill(c);
            var t = go.transform;
            t.localPosition = Vector3.zero;
            t.localRotation = Quaternion.Euler(0f, 180f + Turn, 0f);   // (facing the camera, a little turned)
            var prof = op.animationProfile;
            var rs = go.GetComponentsInChildren<Renderer>();
            if (rs.Length > 0)
            {
                var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
                float want = prof != null ? prof.modelHeight : 1.75f;
                if (b.size.y > 1e-3f) t.localScale *= want / b.size.y;
            }
            var anim = go.GetComponentInChildren<Animator>();
            if (anim != null && anim.isHuman)
            {
                Pose(anim, t);
                anim.enabled = false;   // (the pose holds: nothing drives the bones)
            }
            rs = go.GetComponentsInChildren<Renderer>();
            if (rs.Length > 0)
            {
                var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
                t.position += Vector3.up * (transform.position.y - b.min.y);
                var mid = b.center; mid.y = t.position.y;
                t.position += new Vector3(transform.position.x - mid.x, 0f, transform.position.z - mid.z);   // (centred in the frame)
            }
            figure = go;
        }

        // the left arm raised out to the side (a little above the shoulder), the right one hanging a little away from the body
        static void Pose(Animator a, Transform body)
        {
            Vector3 right = body.right, up = Vector3.up;
            var raised = (-right + up * RaiseUp).normalized;
            var hang = (-up + right * HangOut).normalized;
            Aim(a, HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, raised);
            Aim(a, HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand, raised);
            Aim(a, HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, hang);
            Aim(a, HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand, (hang + body.forward * .12f).normalized);
        }

        static void Aim(Animator a, HumanBodyBones bone, HumanBodyBones child, Vector3 dir)
        {
            var b = a.GetBoneTransform(bone); var c = a.GetBoneTransform(child);
            if (b == null || c == null) return;
            var cur = c.position - b.position;
            if (cur.sqrMagnitude < 1e-6f) return;
            b.rotation = Quaternion.FromToRotation(cur, dir) * b.rotation;
        }

        public void SetLive(bool on) { if (cam != null) cam.enabled = on; }

        public void RenderNow() { if (cam != null) cam.Render(); }

        static void Kill(Object o) { if (o == null) return; if (Application.isPlaying) Destroy(o); else DestroyImmediate(o); }

        void OnDestroy()
        {
            if (rt != null) { rt.Release(); Kill(rt); }
        }
    }
}
