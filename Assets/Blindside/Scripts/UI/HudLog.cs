// Port of log(msg,cls) (blindside_source/05_ui_input_intro.js:28):
//   function log(msg,cls){ const el=$('#log'); ... d.className=cls||''; ... while(el.children.length>7) el.firstChild.remove(); }
// A plain static sink so game-logic classes (TurnManager, CombatSystem, MovementSystem via
// PlayerSelectionController, EnemyAI) can write log lines without holding a UI reference — the
// scene's HudLogView listens to OnAdd and draws them. No UnityEngine.UI dependency here.
using System.Collections.Generic;
using UnityEngine;
using Blindside.Data;

namespace Blindside.UI
{
    /// <summary>The source's log classes: '' (dim border), 'a' (--blu), 'f' (--foe), 'w' (--acc).</summary>
    public enum HudLogType { Normal, Ally, Foe, Warn }

    public static class HudLog
    {
        /// <summary>`while(el.children.length>7) el.firstChild.remove()`.</summary>
        public const int MaxLines = 7;

        /// <summary>G.side — the human player's team. Null (AI-vs-AI) reads as the attack side, the same
        /// default PlayerSelectionController uses.</summary>
        public static OperatorTeam? PlayerTeam;
        public static OperatorTeam Side => PlayerTeam ?? OperatorTeam.Attack;

        public static event System.Action<string, HudLogType> OnAdd;
        public static event System.Action OnCleared;

        static readonly List<(string text, HudLogType type)> recent = new List<(string, HudLogType)>();
        /// <summary>The last ≤7 lines, oldest first (what #log currently holds).</summary>
        public static IReadOnlyList<(string text, HudLogType type)> Recent => recent;

        public static void Add(string text, HudLogType type = HudLogType.Normal)
        {
            Debug.Log($"[Log] {text}");
            recent.Add((text, type));
            while (recent.Count > MaxLines) recent.RemoveAt(0);
            OnAdd?.Invoke(text, type);
        }

        /// <summary>`a.team===G.side?'a':'f'` — a line about something this team did.</summary>
        public static HudLogType TeamType(OperatorTeam team) => team == Side ? HudLogType.Ally : HudLogType.Foe;

        /// <summary>Maps the source's cls strings ('', 'a', 'f', 'w').</summary>
        public static HudLogType FromCls(string cls) => cls switch
        {
            "a" => HudLogType.Ally,
            "f" => HudLogType.Foe,
            "w" => HudLogType.Warn,
            _ => HudLogType.Normal,
        };

        /// <summary>A line taken off the log early (a message that only makes sense for one phase).</summary>
        public static event System.Action<string> OnRemoved;

        /// <summary>Removes the newest line with exactly this text, if it is still in the log.</summary>
        public static void Remove(string text)
        {
            for (int i = recent.Count - 1; i >= 0; i--)
            {
                if (recent[i].text != text) continue;
                recent.RemoveAt(i);
                OnRemoved?.Invoke(text);
                return;
            }
        }

        public static void Clear()
        {
            recent.Clear();
            OnCleared?.Invoke();
        }

        // Survives "Enter Play Mode" without a domain reload: drop last session's lines and listeners.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            recent.Clear(); OnAdd = null; OnCleared = null; OnRemoved = null; PlayerTeam = null;
        }
    }
}
