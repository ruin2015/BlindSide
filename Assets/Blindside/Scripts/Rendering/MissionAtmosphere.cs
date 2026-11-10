// An operation's weather (MapData.night / MapData.rain, set from its map catalog entry), for the intro and the whole match:
//   night  the sun becomes a dim blue moonlight, the ambient and the fog go dark blue, the sky dome is switched off for a dark sky,
//          and every helicopter (HeliRig) is lit for a night op: a searchlight under the nose sweeping slowly with its beam visible in
//          the rain, red / green position lights, a white strobe and a red beacon (HeliNightLights);
//   rain   thin streaks falling in a box in front of the camera, slanted by the wind, splashes and ripples where they land — during
//          the match only outside the buildings (the rooms have a ceiling: a particle over a wall or indoor cell is dropped), during the
//          intro everywhere (IntroCameraRig.IntroWorldShown); drops on the camera's lens (LensRain, needs the camera's opaque texture);
//          and a wet look: the map's and the intro town's lit materials darker and glossier (a wet copy of each material, made once,
//          swapped in on a slow scan so pieces built later are caught too).
// Apply(MapData) sets it (and puts everything back for a map without either). Per frame: positions and the particle filter over
// preallocated buffers — no allocations.
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using Blindside.Map;
using Blindside.Cinematic;

namespace Blindside.Rendering
{
    public class MissionAtmosphere : MonoBehaviour
    {
        static MissionAtmosphere instance;
        static bool quitting;
        void OnApplicationQuit() => quitting = true;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { instance = null; quitting = false; }

        static readonly Color MoonColor = new Color(.55f, .65f, .9f);
        const float MoonIntensity = .45f;
        static readonly Color NightAmbient = new Color(.16f, .19f, .27f), NightFog = new Color(.04f, .055f, .085f);
        const float NightFogDensity = .006f;
        const float RainSpeed = 24f, RainArea = 46f, RainDepth = 40f;
        static readonly Vector3 Wind = new Vector3(2.6f, 0f, 1.1f);
        const float WetDarken = .78f, WetSmooth = .86f, WetScanSeconds = 1.5f;
        // the match itself (after the intro) is lit enough to read the walls and the units: a brighter moon and ambient, thinner fog
        static readonly Color BattleMoonColor = new Color(.66f, .74f, .95f), BattleAmbient = new Color(.5f, .54f, .64f);
        const float BattleMoonIntensity = 1.6f, BattleFogDensity = .0015f;
        static readonly string[] WetRoots = { "Blindside_MapRoot", "IntroHouse" };

        bool night, rain;
        // what night replaced, to put back
        bool saved;
        Light sun; Color sunColor; float sunIntensity;
        Color ambientLight; UnityEngine.Rendering.AmbientMode ambientMode;
        bool fog; Color fogColor; FogMode fogMode; float fogDensity;
        Renderer skyDome;
        Camera savedCam; CameraClearFlags camFlags; Color camBg;
        UniversalAdditionalCameraData savedCamData; CameraOverrideOption savedColorOption;
        readonly List<HeliNightLights> heliLights = new List<HeliNightLights>();
        ParticleSystem rainPs, splashPs, ripplePs;
        ParticleSystem.Particle[] buf;
        Transform lens; Material lensMat;
        readonly Dictionary<Material, Material> wetOf = new Dictionary<Material, Material>();
        // the map's own pieces only get the gloss, not the darker colour: walls and floor must stay readable in the match
        readonly Dictionary<Material, Material> glossOf = new Dictionary<Material, Material>();
        readonly Dictionary<Renderer, Material[]> dryOf = new Dictionary<Renderer, Material[]>();
        readonly List<Renderer> scan = new List<Renderer>();
        float nextScan, nextUnitScan;
        bool battleLit;
        Blindside.Integration.MatchBootstrap match;
        // the map's building cells (a wall or an indoor floor: under a ceiling), rebuilt when the map changes
        MapData maskFor; bool[] roofed; int maskW, maskH;

        /// <summary>Sets the current operation's weather (none: everything as the scene had it).</summary>
        public static void Apply(MapData d)
        {
            if (!Application.isPlaying || quitting) return;
            bool n = d != null && d.night, r = d != null && d.rain;
            if (instance == null)
            {
                if (!n && !r) return;
                instance = new GameObject("MissionAtmosphere").AddComponent<MissionAtmosphere>();
            }
            instance.Set(n, r);
        }

        void Set(bool n, bool r)
        {
            if (n && !saved) Save();
            night = n; rain = r;
            if (n) Night(); else Restore();
            if (r) EnsureRain();
            else
            {
                foreach (var ps in new[] { rainPs, splashPs, ripplePs }) if (ps != null) ps.gameObject.SetActive(false);
                if (lens != null) lens.gameObject.SetActive(false);
                DryAll();
            }
            nextScan = 0f;
        }

        void Save()
        {
            saved = true;
            sun = RenderSettings.sun;
            if (sun == null) foreach (var l in FindObjectsByType<Light>(FindObjectsInactive.Exclude)) if (l.type == LightType.Directional) { sun = l; break; }
            if (sun != null) { sunColor = sun.color; sunIntensity = sun.intensity; }
            ambientMode = RenderSettings.ambientMode; ambientLight = RenderSettings.ambientLight;
            fog = RenderSettings.fog; fogColor = RenderSettings.fogColor; fogMode = RenderSettings.fogMode; fogDensity = RenderSettings.fogDensity;
            var sd = GameObject.Find("SkyDome");
            skyDome = sd != null ? sd.GetComponent<Renderer>() : null;
        }

        void Night()
        {
            battleLit = !IntroCameraRig.IntroWorldShown;
            NightLevel(battleLit);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.fog = true; RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogColor = NightFog;
            if (skyDome != null) skyDome.enabled = false;
            DressCamera();
            foreach (var heli in FindObjectsByType<HeliRig>(FindObjectsInactive.Include))
            {
                var l = heli.GetComponent<HeliNightLights>();
                if (l == null) { l = heli.gameObject.AddComponent<HeliNightLights>(); heliLights.Add(l); }
                l.enabled = true;
            }
        }

        /// <summary>The intro's dark night, or the match's brighter one.</summary>
        void NightLevel(bool battle)
        {
            if (sun != null) { sun.color = battle ? BattleMoonColor : MoonColor; sun.intensity = battle ? BattleMoonIntensity : MoonIntensity; }
            RenderSettings.ambientLight = battle ? BattleAmbient : NightAmbient;
            RenderSettings.fogDensity = battle ? BattleFogDensity : NightFogDensity;
        }

        /// <summary>Every unit of the match gets its night light (UnitNightLight): the player's side a weapon light and a glow, the
        /// other side a faint red glow (an AI-only match: the attack is "ours").</summary>
        void LightUnits()
        {
            if (match == null) match = FindAnyObjectByType<Blindside.Integration.MatchBootstrap>();
            if (match == null || match.ActiveUnits == null) return;
            var side = match.playerTeam ?? Blindside.Data.OperatorTeam.Attack;
            foreach (var u in match.ActiveUnits)
            {
                if (!(u.View is UnitView uv) || uv == null || uv.GetComponent<UnitNightLight>() != null) continue;
                var unit = u;
                uv.gameObject.AddComponent<UnitNightLight>().Setup(uv, unit.Team == side, () => unit.Alive);
            }
        }

        void DressCamera()
        {
            var cam = Camera.main;
            if (cam == null || cam == savedCam) return;
            UndressCamera();
            savedCam = cam; camFlags = cam.clearFlags; camBg = cam.backgroundColor;
            if (night) { cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = NightFog; }
            // the lens drops read the scene behind them
            savedCamData = cam.GetComponent<UniversalAdditionalCameraData>();
            if (savedCamData != null) { savedColorOption = savedCamData.requiresColorOption; if (rain) savedCamData.requiresColorOption = CameraOverrideOption.On; }
        }

        void UndressCamera()
        {
            if (savedCam != null) { savedCam.clearFlags = camFlags; savedCam.backgroundColor = camBg; }
            if (savedCamData != null) savedCamData.requiresColorOption = savedColorOption;
            savedCam = null; savedCamData = null;
        }

        void Restore()
        {
            if (!saved) return;
            if (sun != null) { sun.color = sunColor; sun.intensity = sunIntensity; }
            RenderSettings.ambientMode = ambientMode; RenderSettings.ambientLight = ambientLight;
            RenderSettings.fog = fog; RenderSettings.fogColor = fogColor; RenderSettings.fogMode = fogMode; RenderSettings.fogDensity = fogDensity;
            if (skyDome != null) skyDome.enabled = true;
            UndressCamera();
            foreach (var l in heliLights) if (l != null) Destroy(l);
            heliLights.Clear();
            saved = false;
        }

        // ---- rain ----------------------------------------------------------------------------------------------------------------
        void EnsureRain()
        {
            if (rainPs == null) rainPs = BuildRain();
            if (splashPs == null) splashPs = BuildSplashes();
            if (ripplePs == null) ripplePs = BuildRipples();
            if (lens == null) BuildLens();
            foreach (var ps in new[] { rainPs, splashPs, ripplePs }) { ps.gameObject.SetActive(true); if (!ps.isPlaying) ps.Play(); }
            if (lens != null) lens.gameObject.SetActive(true);
            if (buf == null) buf = new ParticleSystem.Particle[8000];
            savedCam = null;   // (DressCamera again: the opaque texture for the lens)
        }

        /// <summary>A soft vertical streak (bright thin core, fading at both ends) for the rain, or a round soft dot.</summary>
        static Texture2D StreakTexture(bool round)
        {
            int w = round ? 32 : 8, h = round ? 32 : 64;
            var t = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = round ? "RainDot" : "RainStreak" };
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float u = (x + .5f) / w * 2f - 1f, v = (y + .5f) / h * 2f - 1f;
                    float a = round ? Mathf.Clamp01(1f - Mathf.Sqrt(u * u + v * v)) : Mathf.Clamp01(1f - Mathf.Abs(u) * 1.15f) * Mathf.Clamp01(1f - Mathf.Abs(v)) ;
                    a = round ? a * a : Mathf.Pow(a, 1.6f);
                    px[y * w + x] = new Color32(255, 255, 255, (byte)(a * 255));
                }
            t.SetPixels32(px); t.Apply();
            return t;
        }

        static Material ParticleMaterial(Texture tex, Color tint, bool additive)
        {
            var src = Resources.Load<Material>(additive ? "FX/FX_MuzzleGlow" : "FX/FX_Smoke");
            var m = src != null ? new Material(src) : new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
            if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", tex);
            if (m.HasProperty("_MainTex")) m.SetTexture("_MainTex", tex);
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", tint);
            if (m.HasProperty("_Color")) m.SetColor("_Color", tint);
            return m;
        }

        static ParticleSystem NewSystem(Transform parent, string name, int max)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = true; main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = max;
            main.startSpeed = 0f;
            return ps;
        }

        ParticleSystem BuildRain()
        {
            var ps = NewSystem(transform, "Rain", 7000);
            var main = ps.main;
            main.startLifetime = 1.6f;
            main.startSize = new ParticleSystem.MinMaxCurve(.018f, .034f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(.8f, .86f, 1f, .16f), new Color(.85f, .9f, 1f, .42f));
            var em = ps.emission; em.rateOverTime = 4200f;
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Box; sh.scale = new Vector3(RainArea, .5f, RainArea);
            var vel = ps.velocityOverLifetime;
            vel.enabled = true; vel.space = ParticleSystemSimulationSpace.World;
            vel.x = new ParticleSystem.MinMaxCurve(Wind.x * .8f, Wind.x * 1.2f);
            vel.y = new ParticleSystem.MinMaxCurve(-RainSpeed * 1.1f, -RainSpeed * .9f);
            vel.z = new ParticleSystem.MinMaxCurve(Wind.z * .8f, Wind.z * 1.2f);
            var r = ps.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Stretch;
            r.velocityScale = .055f; r.lengthScale = 1f;
            r.sharedMaterial = ParticleMaterial(StreakTexture(false), Color.white, false);
            return ps;
        }

        /// <summary>Tiny droplets thrown up where the rain lands (on the ground in front of the camera).</summary>
        ParticleSystem BuildSplashes()
        {
            var ps = NewSystem(transform, "RainSplashes", 2500);
            ps.transform.rotation = Quaternion.Euler(-90f, 0f, 0f);   // the box emits along its +Z: up
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(.18f, .32f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1.2f, 2.6f);
            main.startSize = new ParticleSystem.MinMaxCurve(.015f, .03f);
            main.gravityModifier = 1.4f;
            main.startColor = new Color(.8f, .88f, 1f, .5f);
            var em = ps.emission; em.rateOverTime = 4000f;
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Box; sh.scale = new Vector3(RainArea, RainArea, .01f);
            sh.randomDirectionAmount = .35f;
            var r = ps.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Stretch;
            r.velocityScale = .04f; r.lengthScale = 1f;
            r.sharedMaterial = ParticleMaterial(StreakTexture(true), Color.white, false);
            return ps;
        }

        /// <summary>Rings spreading on the wet ground.</summary>
        ParticleSystem BuildRipples()
        {
            var ps = NewSystem(transform, "RainRipples", 1500);
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(.3f, .5f);
            main.startSize = new ParticleSystem.MinMaxCurve(.25f, .5f);
            main.startColor = new Color(.75f, .85f, 1f, .2f);
            var em = ps.emission; em.rateOverTime = 2200f;
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Box; sh.scale = new Vector3(RainArea, .01f, RainArea);
            var size = ps.sizeOverLifetime; size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, .25f, 1f, 1.3f));
            var col = ps.colorOverLifetime; col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                      new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
            col.color = g;
            var r = ps.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.HorizontalBillboard;
            r.sharedMaterial = ParticleMaterial(RingTexture(), Color.white, false);
            return ps;
        }

        static Texture2D RingTexture()
        {
            const int n = 64;
            var t = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "RainRing" };
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float u = (x + .5f) / n * 2f - 1f, v = (y + .5f) / n * 2f - 1f;
                    float d = Mathf.Sqrt(u * u + v * v);
                    float a = Mathf.Clamp01(1f - Mathf.Abs(d - .8f) / .12f);
                    px[y * n + x] = new Color32(255, 255, 255, (byte)(a * a * 255));
                }
            t.SetPixels32(px); t.Apply();
            return t;
        }

        void BuildLens()
        {
            var sh = Shader.Find("Blindside/LensRain");
            if (sh == null) return;
            var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Destroy(q.GetComponent<Collider>());
            q.name = "LensRain";
            lensMat = new Material(sh);
            var r = q.GetComponent<MeshRenderer>();
            r.sharedMaterial = lensMat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false;
            lens = q.transform;
        }

        /// <summary>The lens quad fills the camera's view just past its near plane.</summary>
        void PlaceLens(Camera cam)
        {
            if (lens == null) return;
            if (lens.parent != cam.transform) lens.SetParent(cam.transform, false);
            float d = cam.nearClipPlane + .02f;
            float h = 2f * d * Mathf.Tan(cam.fieldOfView * .5f * Mathf.Deg2Rad);
            lens.localPosition = new Vector3(0f, 0f, d);
            lens.localRotation = Quaternion.identity;
            lens.localScale = new Vector3(h * cam.aspect * 1.02f, h * 1.02f, 1f);
        }

        void LateUpdate()
        {
            if (night || rain) DressCamera();
            if (night)
            {
                bool battle = !IntroCameraRig.IntroWorldShown;
                if (battle != battleLit) { battleLit = battle; NightLevel(battle); }
                if (battle && Time.unscaledTime >= nextUnitScan) { nextUnitScan = Time.unscaledTime + 1f; LightUnits(); }
            }
            if (!rain || rainPs == null) return;
            var cam = Camera.main;
            if (cam == null) return;
            PlaceLens(cam);
            // the rain falls in a box just in front of the camera (from a little above it, RainDepth deep) — seen from inside, never
            // as a sheet from far off; splashes and ripples on the ground point the camera looks at
            var p = cam.transform.position; var f = cam.transform.forward;
            Vector3 ground = f.y < -.05f ? p + f * (-p.y / f.y) : new Vector3(p.x, 0f, p.z) + new Vector3(f.x, 0f, f.z) * 25f;
            var ahead = p + f * (RainArea * .3f);
            float top = p.y + 6f;
            rainPs.transform.position = new Vector3(ahead.x - Wind.x, top, ahead.z - Wind.z);
            var main = rainPs.main;
            main.startLifetime = Mathf.Min(top, RainDepth) / RainSpeed;
            bool nearGround = p.y < 70f;
            splashPs.transform.position = new Vector3(ground.x, .03f, ground.z);
            ripplePs.transform.position = new Vector3(ground.x, .05f, ground.z);
            if (splashPs.gameObject.activeSelf != nearGround) { splashPs.gameObject.SetActive(nearGround); ripplePs.gameObject.SetActive(nearGround); }
            // the match: nothing falls into a room (they have a ceiling); the intro's world is all outside
            if (!IntroCameraRig.IntroWorldShown && UpdateMask())
            {
                DropRoofed(rainPs, p);
                if (nearGround) { DropRoofed(splashPs, p); DropRoofed(ripplePs, p); }
            }
            if (Time.unscaledTime >= nextScan) { nextScan = Time.unscaledTime + WetScanSeconds; WetScan(); }
        }

        bool UpdateMask()
        {
            var d = MapLibrary.Current;
            if (d == null || d.terrain == null) return false;
            if (!ReferenceEquals(d, maskFor))
            {
                maskFor = d; maskW = d.width; maskH = d.height;
                roofed = new bool[maskW * maskH];
                for (int i = 0; i < roofed.Length; i++) roofed[i] = d.terrain[i] == MapTerrain.In || MapData.IsWall(d.terrain[i]);
            }
            return true;
        }

        /// <summary>Kills the particles seen over a building cell (grid x = world x, grid y = −world z, cells centred on integers): a
        /// drop in the air counts where the camera's line through it meets the ground — so no rain is drawn over a room, at any height.</summary>
        void DropRoofed(ParticleSystem ps, Vector3 eye)
        {
            int n = ps.GetParticles(buf);
            bool changed = false;
            for (int i = 0; i < n; i++)
            {
                var q = buf[i].position;
                if (q.y > .1f && q.y < eye.y - .05f)
                {
                    float k = eye.y / (eye.y - q.y);
                    q = eye + (q - eye) * k;
                }
                int x = Mathf.RoundToInt(q.x), y = Mathf.RoundToInt(-q.z);
                if (x < 0 || y < 0 || x >= maskW || y >= maskH || !roofed[y * maskW + x]) continue;
                buf[i].remainingLifetime = 0f;
                changed = true;
            }
            if (changed) ps.SetParticles(buf, n);
        }

        // ---- wet surfaces ---------------------------------------------------------------------------------------------------
        void WetScan()
        {
            foreach (var name in WetRoots)
            {
                var go = GameObject.Find(name);
                if (go == null) continue;
                go.GetComponentsInChildren(true, scan);
                bool map = name == "Blindside_MapRoot";
                foreach (var r in scan)
                {
                    if (r is ParticleSystemRenderer || r is LineRenderer || dryOf.ContainsKey(r)) continue;
                    var mats = r.sharedMaterials;
                    bool any = false;
                    var wet = new Material[mats.Length];
                    for (int i = 0; i < mats.Length; i++) { wet[i] = WetOf(mats[i], map); any |= wet[i] != mats[i]; }
                    if (!any) continue;
                    dryOf[r] = mats;
                    r.sharedMaterials = wet;
                }
            }
        }

        Material WetOf(Material m, bool glossOnly)
        {
            if (m == null || !m.HasProperty("_Smoothness")) return m;
            var cache = glossOnly ? glossOf : wetOf;
            if (cache.TryGetValue(m, out var w)) return w;
            w = new Material(m) { name = m.name + " (wet)" };
            if (!glossOnly && w.HasProperty("_BaseColor")) w.SetColor("_BaseColor", w.GetColor("_BaseColor") * new Color(WetDarken, WetDarken, WetDarken, 1f));
            w.SetFloat("_Smoothness", Mathf.Max(w.GetFloat("_Smoothness"), glossOnly ? .7f : WetSmooth));
            cache[m] = w;
            return w;
        }

        void DryAll()
        {
            foreach (var kv in dryOf) if (kv.Key != null) kv.Key.sharedMaterials = kv.Value;
            dryOf.Clear();
        }

        void OnDestroy()
        {
            DryAll();
            Restore();
            UndressCamera();
            if (lens != null) Destroy(lens.gameObject);
            foreach (var w in wetOf.Values) if (w != null) Destroy(w);
            foreach (var w in glossOf.Values) if (w != null) Destroy(w);
            wetOf.Clear(); glossOf.Clear();
            if (instance == this) instance = null;
        }
    }
}
