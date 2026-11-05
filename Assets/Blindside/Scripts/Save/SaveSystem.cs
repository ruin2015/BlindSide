// One save slot (persistentDataPath/Save/save_0.json) holding SaveData, and the session that uses it:
//   NewGame()   a fresh SaveData becomes the session's and is written at once (the old file goes to save_0.json.bak)
//   Load()      the file (or, when it can't be read, its .bak) becomes the session's
//   Write()     the session's data to disk: written to save_0.json.tmp first, then swapped in (the previous file kept as .bak), so a
//               crash mid-write never leaves a broken save
//   Peek()      reads the file without starting a session (the title's 読み込み button shows its date and play time)
// Play time is counted from the session's start and added at every write. Saved(auto) is raised after every successful write (the
// autosave indicator listens). Nothing here runs per frame.
// AutoSave: the game's save points — the tutorial's end, every sortie, every match result, and quitting the app. Each changes the
// session's data and writes it; with no session (a scene started on its own in the editor, a test play) they do nothing.
using System;
using System.IO;
using UnityEngine;
using Blindside.Data;

namespace Blindside.Save
{
    public static class SaveSystem
    {
        public const string FileName = "save_0.json";

        /// <summary>Tests / tools: another folder instead of persistentDataPath/Save.</summary>
        public static string DirectoryOverride;
        public static string Folder => DirectoryOverride ?? Path.Combine(Application.persistentDataPath, "Save");
        public static string FilePath => Path.Combine(Folder, FileName);
        static string BackupPath => FilePath + ".bak";
        static string TempPath => FilePath + ".tmp";

        /// <summary>The session's save (null before ニューゲーム / 読み込み).</summary>
        public static SaveData Current { get; private set; }
        public static bool HasSession => Current != null;

        /// <summary>After every successful write: true for an autosave.</summary>
        public static event Action<bool> Saved;

        static float sessionMark;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Current = null;
            Saved = null;
            Application.quitting -= OnQuit;
            Application.quitting += OnQuit;
        }

        static void OnQuit() => AutoSave.Quitting();

        /// <summary>A save file is there (or at least its backup).</summary>
        public static bool Exists => File.Exists(FilePath) || File.Exists(BackupPath);

        /// <summary>The save on disk without starting a session (null when there is none or it can't be read).</summary>
        public static SaveData Peek()
        {
            return Read(FilePath, out _) ?? Read(BackupPath, out _);
        }

        /// <summary>A fresh save becomes the session's and is written (the old one is kept as the backup).</summary>
        public static SaveData NewGame()
        {
            Current = SaveData.Fresh();
            sessionMark = Time.realtimeSinceStartup;
            Write(false);
            return Current;
        }

        /// <summary>The save on disk becomes the session's (its backup when the file itself is damaged). False with `why` when neither reads.</summary>
        public static bool Load(out string why)
        {
            var d = Read(FilePath, out why);
            if (d == null)
            {
                d = Read(BackupPath, out string whyBak);
                if (d == null) { if (string.IsNullOrEmpty(why)) why = whyBak; return false; }
                Debug.LogWarning("[Save] " + FileName + " could not be read (" + why + "); the backup was loaded instead.");
            }
            why = null;
            Current = d;
            sessionMark = Time.realtimeSinceStartup;
            return true;
        }

        /// <summary>The session ends (nothing is written).</summary>
        public static void EndSession() => Current = null;

        /// <summary>Writes the session's save; false (and a warning) when it could not be written or there is no session.</summary>
        public static bool Write(bool auto)
        {
            if (Current == null) return false;
            float now = Time.realtimeSinceStartup;
            Current.playSeconds += Mathf.Max(0f, now - sessionMark);
            sessionMark = now;
            Current.savedUtc = DateTime.UtcNow.ToString("o");
            Current.version = SaveData.CurrentVersion;
            try
            {
                Directory.CreateDirectory(Folder);
                File.WriteAllText(TempPath, JsonUtility.ToJson(Current, true));
                if (File.Exists(FilePath))
                {
                    try { File.Replace(TempPath, FilePath, BackupPath); }
                    catch (Exception)
                    {
                        // a file system without Replace: keep the old file as the backup by hand
                        File.Copy(FilePath, BackupPath, true);
                        File.Delete(FilePath);
                        File.Move(TempPath, FilePath);
                    }
                }
                else File.Move(TempPath, FilePath);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Save] could not write " + FilePath + ": " + e.Message);
                return false;
            }
            Saved?.Invoke(auto);
            return true;
        }

        static SaveData Read(string path, out string why)
        {
            why = null;
            try
            {
                if (!File.Exists(path)) { why = "no file"; return null; }
                var text = File.ReadAllText(path);
                if (string.IsNullOrWhiteSpace(text)) { why = "empty file"; return null; }
                var d = JsonUtility.FromJson<SaveData>(text);
                if (d == null) { why = "not a save"; return null; }
                if (d.version > SaveData.CurrentVersion) { why = "made by a newer version (" + d.version + ")"; return null; }
                d.Normalize();
                return d;
            }
            catch (Exception e)
            {
                why = e.Message;
                return null;
            }
        }
    }

    /// <summary>The game's save points. Each one changes the session's save and writes it (an autosave); without a session nothing happens.</summary>
    public static class AutoSave
    {
        /// <summary>A sortie from the select screen (or the tutorial's hand-over): the side and the map are remembered.</summary>
        public static void Sortie(OperatorTeam team, string map)
        {
            var d = SaveSystem.Current;
            if (d == null) return;
            d.LastTeam = team;
            d.lastMap = map ?? "";
            SaveSystem.Write(true);
        }

        /// <summary>The tutorial's end (its hand-over to the main operation on the default house, attack side).</summary>
        public static void TutorialFinished()
        {
            var d = SaveSystem.Current;
            if (d == null) return;
            d.tutorialDone = true;
            d.LastTeam = OperatorTeam.Attack;
            d.lastMap = "";
            SaveSystem.Write(true);
        }

        /// <summary>A match's result (not the tutorial's, not a test play): counted on the last sortie's map.</summary>
        public static void MatchEnded(bool win)
        {
            var d = SaveSystem.Current;
            if (d == null) return;
            d.matches++;
            if (win) d.wins++; else d.losses++;
            var r = d.RecordOf(d.lastMap);
            r.plays++;
            if (win) r.wins++;
            SaveSystem.Write(true);
        }

        /// <summary>The app is closing: the play time so far.</summary>
        public static void Quitting()
        {
            if (SaveSystem.Current != null) SaveSystem.Write(true);
        }
    }
}
