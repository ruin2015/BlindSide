// The defense gadgets a map places in the editor (MapData.gadgets), live in a match:
//   ELECTRIC — an electric field like VOLT's (DroneOps destroys a drone flying into it; ElectricFieldView draws it) that also
//              electrifies the reinforced panels within DefensePrep.ElectrifyReach;
//   MINE     — hidden from the attack; an attacker stepping within MineReach sets it off: MineDamage, its move ends, the mine is gone;
//   SENSOR   — a motion sensor: an attacker moving within SensorRadius (once per sensor and round) is reported to the defense — a
//              Spotted mark, revealed for a moment, the defense AI's latest intel. Once it has gone off, the attack knows it is there.
// Mines and sensors are drawn for the defense side (and AI-vs-AI); for an attacking player only a sensor that has gone off.
// The step check runs from MovementSystem.OnStep (every .2 m of every walk, the player's and the AI's).
using System.Collections.Generic;
using UnityEngine;
using Blindside.Abilities;
using Blindside.Data;
using Blindside.Map;
using Blindside.Rendering;
using Blindside.Turns;
using Blindside.UI;
using Blindside.Vision;

namespace Blindside.Integration
{
    public class MapGadgetRuntime : MonoBehaviour
    {
        public const int MineDamage = 60;
        public const float MineReach = .45f, SensorRadius = 1.6f;
        static readonly Color MineOrange = new Color32(0xff, 0x6a, 0x1a, 0xff);
        static readonly Color SensorGreen = new Color32(0x2f, 0xd8, 0x86, 0xff);
        static readonly Color Casing = new Color32(0x2a, 0x30, 0x38, 0xff);

        class Mine { public float x, y; public bool alive = true; public GameObject go; }

        class Sensor
        {
            public float x, y;
            public bool discovered;
            public GameObject go;
            public int round = -1;
            public readonly List<SimUnit> seen = new List<SimUnit>();
        }

        readonly List<Mine> mines = new List<Mine>();
        readonly List<Sensor> sensors = new List<Sensor>();
        Transform root;
        Material mat;
        OperatorTeam? playerTeam;
        TurnManager turns;
        MarkBoard marks;
        EnemyAI ai;
        GadgetAbilityPresenter presenter;

        public int MineCount { get { int n = 0; foreach (var m in mines) if (m.alive) n++; return n; } }
        public int SensorCount => sensors.Count;

        public void Bind(MapData data, MapGrid map, VisionSystem vision, AbilityContext ctx, TurnManager turns, MarkBoard marks, EnemyAI ai,
                         GadgetAbilityPresenter presenter, OperatorTeam? playerTeam)
        {
            Unbind();
            this.turns = turns; this.marks = marks; this.ai = ai; this.presenter = presenter; this.playerTeam = playerTeam;
            if (data == null || data.gadgets == null || data.gadgets.Length == 0) return;
            root = new GameObject("MapGadgets").transform;
            root.SetParent(transform, false);
            bool electrified = false;
            foreach (var g in data.gadgets)
            {
                if (g == null) continue;
                if (g.kind == MapData.GadgetMine) mines.Add(new Mine { x = g.cell.x, y = g.cell.y, go = BuildMine(g.cell) });
                else if (g.kind == MapData.GadgetSensor) sensors.Add(new Sensor { x = g.cell.x, y = g.cell.y, go = BuildSensor(g.cell) });
                else
                {
                    ctx.Electrics.Add(new ElectricField { x = g.cell.x, y = g.cell.y, r = DefensePrep.FieldRadius, discovered = false });
                    for (int i = 0; i < map.Panels.Count; i++)
                    {
                        var p = map.Panels[i];
                        if (!p.reinforced) continue;
                        foreach (var q in p.cells)
                            if ((q.x - g.cell.x) * (q.x - g.cell.x) + (q.y - g.cell.y) * (q.y - g.cell.y) <= DefensePrep.ElectrifyReach * DefensePrep.ElectrifyReach)
                            { map.SetPanelElectrified(p.id, true); electrified = true; break; }
                    }
                }
            }
            if (electrified)
            {
                if (vision != null) vision.MapVersion++;
                map.BumpMap();
            }
            SyncVisibility();
        }

        public void Unbind()
        {
            mines.Clear(); sensors.Clear();
            if (root != null) Destroy(root.gameObject);
            root = null;
            turns = null; marks = null; ai = null; presenter = null;
        }

        /// <summary>MovementSystem.OnStep: an attacker has just moved a step.</summary>
        public void Step(ITurnUnit tu)
        {
            var u = tu as SimUnit;
            if (u == null || !u.Alive || u.Team != OperatorTeam.Attack) return;
            for (int i = 0; i < mines.Count; i++)
            {
                var m = mines[i];
                if (!m.alive || Dist(m.x, m.y, u.X, u.Y) > MineReach) continue;
                m.alive = false;
                if (m.go != null) Destroy(m.go);
                CombatFx.Boom(GridSpace.GridToWorld(m.x, m.y), MineOrange, 16);
                SfxNoise.Boom();
                u.MoveLeft = 0f;   // (the walk ends here)
                marks?.Add(OperatorTeam.Defense, u.X, u.Y, MarkKind.Spotted);
                if (presenter != null) presenter.HazardDamage(u, MineDamage, "-" + MineDamage + " MINE", $"{u.Kr}: 地雷を踏んだ — {MineDamage}ダメージ");
                if (!u.Alive) return;
            }
            int round = turns != null ? turns.Round : 0;
            for (int i = 0; i < sensors.Count; i++)
            {
                var s = sensors[i];
                if (Dist(s.x, s.y, u.X, u.Y) > SensorRadius) continue;
                if (s.round != round) { s.round = round; s.seen.Clear(); }
                if (s.seen.Contains(u)) continue;
                s.seen.Add(u);
                Detect(s, u);
            }
        }

        void Detect(Sensor s, SimUnit u)
        {
            bool wasHidden = !s.discovered;
            s.discovered = true;
            u.RevealT = Time.unscaledTime + 3f;
            marks?.Add(OperatorTeam.Defense, u.X, u.Y, MarkKind.Spotted);
            ai?.NoteIntel(u.X, u.Y);
            CombatFx.Ring(GridSpace.GridToWorld(s.x, s.y), SensorGreen, .6f, SensorRadius);
            WorldPopup.Show(u.X, u.Y, "DETECTED", SensorGreen);
            SfxNoise.Ui();
            if (playerTeam == OperatorTeam.Defense) HudLog.Add($"動体センサー: {u.Kr}を検知", HudLogType.Ally);
            else HudLog.Add($"{u.Kr}: 動体センサーに検知された — 位置が敵に知られた", HudLogType.Warn);
            if (wasHidden) SyncVisibility();
        }

        /// <summary>The attacking player sees a sensor only once it has gone off, and never a mine; everyone else sees them all.</summary>
        void SyncVisibility()
        {
            bool all = playerTeam != OperatorTeam.Attack;
            foreach (var m in mines) if (m.go != null) m.go.SetActive(all && m.alive);
            foreach (var s in sensors) if (s.go != null) s.go.SetActive(all || s.discovered);
        }

        GameObject BuildMine(Vector2Int c)
        {
            var go = new GameObject("Mine");
            go.transform.SetParent(root, false);
            go.transform.position = GridSpace.GridToWorld(c.x, c.y);
            Part(go.transform, PrimitiveType.Cylinder, new Vector3(0, .03f, 0), new Vector3(.34f, .03f, .34f), Casing);
            Part(go.transform, PrimitiveType.Cylinder, new Vector3(0, .07f, 0), new Vector3(.12f, .02f, .12f), MineOrange);
            return go;
        }

        GameObject BuildSensor(Vector2Int c)
        {
            var go = new GameObject("MotionSensor");
            go.transform.SetParent(root, false);
            go.transform.position = GridSpace.GridToWorld(c.x, c.y);
            Part(go.transform, PrimitiveType.Cylinder, new Vector3(0, .18f, 0), new Vector3(.06f, .18f, .06f), Casing);
            Part(go.transform, PrimitiveType.Cube, new Vector3(0, .4f, 0), new Vector3(.18f, .1f, .12f), Casing);
            Part(go.transform, PrimitiveType.Sphere, new Vector3(0, .4f, .07f), new Vector3(.07f, .07f, .03f), SensorGreen);
            return go;
        }

        void Part(Transform parent, PrimitiveType type, Vector3 pos, Vector3 scale, Color color)
        {
            var p = GameObject.CreatePrimitive(type);
            Destroy(p.GetComponent<Collider>());
            p.transform.SetParent(parent, false);
            p.transform.localPosition = pos;
            p.transform.localScale = scale;
            if (mat == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
                mat = new Material(shader);
            }
            var r = p.GetComponent<Renderer>();
            var m = new Material(mat);
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
            if (m.HasProperty("_Color")) m.SetColor("_Color", color);
            r.sharedMaterial = m;
        }

        static float Dist(float ax, float ay, float bx, float by) => Mathf.Sqrt((ax - bx) * (ax - bx) + (ay - by) * (ay - by));

        void OnDestroy() => Unbind();
    }
}
