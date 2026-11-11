// Ported from the drone-related fragments of blindside_source/04_gameplay_combat_ai.js:
// deployDrone() (~line 323), the per-round moveLeft reset (~line 352), and the shock ability's
// drone kill (~line 241: `for(const d of G.drones) if(d.alive&&Math.hypot(d.x-x,d.y-y)<=e.r){...}`).
// Recon movement/AI for drones lives with the rest of the attack-team AI, not here; this class only
// owns drone state so AbilitySystem (shock) doesn't need to reach into a combat/AI-owned list directly.
using System.Collections.Generic;
using UnityEngine;

namespace Blindside.Drones
{
    public class Drone
    {
        public string id;
        public float x, y;               // logical position (moveDrone jumps it one step at a time)
        public float rx, ry;             // drawn position — tweened toward x/y (the source's d.rx/d.ry)
        public float returnX, returnY;   // recon home point (the deploy position)
        public bool alive = true;
        public float moveLeft;
        /// <summary>What it gets back each round: 8, or 10 for a drone bought at the shop.</summary>
        public float moveMax = 8f;
        /// <summary>It marks the enemy gadgets it finds (the shop's drone, and the AI's; not the player's basic one).</summary>
        public bool revealsGadgets = true;
        public Blindside.Data.OperatorTeam team = Blindside.Data.OperatorTeam.Attack;   // d.team is always 'atk'
        public Blindside.Abilities.IAbilityUnit owner;                                   // d.owner — the operator that deployed it
    }

    public class DroneController
    {
        readonly List<Drone> drones = new List<Drone>();
        public IReadOnlyList<Drone> Drones => drones;

        /// <summary>Port of `boom(d.x,d.y,0x4fb3ff,10)` on kill; caller plays fx/sfx.</summary>
        public event System.Action<Drone> OnDroneDestroyed;

        /// <summary>Port of deployDrone(u): spawns a drone at the owner's position with 8m of move budget.</summary>
        public Drone Deploy(string ownerId, float x, float y)
        {
            var d = new Drone { id = "d" + ownerId, x = x, y = y, rx = x, ry = y, returnX = x, returnY = y, moveLeft = 8f };
            drones.Add(d);
            return d;
        }

        /// <summary>deployDrone(u): `{id:'d'+u.id,team:'atk',owner:u,x:u.x,y:u.y,rx:u.x,ry:u.y,alive:true,moveLeft:8}`. The
        /// caller sets u.drone / u.droneSpent (they live on the unit).</summary>
        public Drone Deploy(Blindside.Abilities.IAbilityUnit owner)
        {
            var d = Deploy(owner.Id, owner.X, owner.Y);
            d.owner = owner;
            return d;
        }

        /// <summary>Port of the round-start reset: `for(const d of G.drones) if(d.team===team) d.moveLeft=8;`.</summary>
        public void ResetMoveBudget()
        {
            foreach (var d in drones)
                if (d.alive) d.moveLeft = d.moveMax;
        }

        /// <summary>
        /// Port of the shock ability's drone kill: any alive drone within radius r of (x,y) is destroyed.
        /// Returns the drones destroyed so the caller can play a boom fx per one, as the source does.
        /// </summary>
        public List<Drone> DestroyInRadius(float x, float y, float r)
        {
            var killed = new List<Drone>();
            foreach (var d in drones)
            {
                if (!d.alive) continue;
                if (Mathf.Sqrt((d.x - x) * (d.x - x) + (d.y - y) * (d.y - y)) > r) continue;
                d.alive = false;
                killed.Add(d);
                OnDroneDestroyed?.Invoke(d);
            }
            return killed;
        }
    }
}
