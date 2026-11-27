// Save safety (QA review 2026-11-27, P0-1): decides what to load and when writing is allowed, so a
// save that fails to load is never replaced by an empty game. Unity-free so Tests~ can check it; the
// client gives it PlayerPrefs through ISaveStorage.
//   - A save that can't be read is copied to "<key>.corrupt-<time>" and left in place.
//   - Every write first copies the previous good save to "<key>.bak" (a rolling backup).
//   - On a failed load the backup is tried. If that fails too, the store is in the Failed state and
//     refuses every write (autosave on app switch included) until the player chooses to start over
//     (AcceptFresh) or imports a save code.
using System;

namespace Sigilbound.Core
{
    public interface ISaveStorage
    {
        string Get(string key);          // "" or null when missing
        void Set(string key, string value);
        void Flush();
    }

    public enum SaveLoadState { Fresh, Loaded, FromBackup, Failed }

    public sealed class SaveStore
    {
        public const string MainKey = "sigilbound.save.v1";
        public const string BackupKey = MainKey + ".bak";
        public const string CorruptPrefix = MainKey + ".corrupt-";
        public const string LastCorruptKey = MainKey + ".corrupt-last"; // name of the newest corrupt copy

        readonly ISaveStorage _st;
        bool _mainTrusted; // the main key holds a save this build read or wrote, so it may roll into the backup

        public SaveLoadState State { get; private set; } = SaveLoadState.Fresh;
        public string Error { get; private set; } = "";
        public string CorruptKey { get; private set; } = ""; // where an unreadable save was kept
        public bool CanWrite => State != SaveLoadState.Failed;

        public SaveStore(ISaveStorage storage) { _st = storage; }

        // Never throws: any parse error is a failed load.
        public static bool TryParse(string json, out SaveData save, out string error)
        {
            save = null; error = "";
            if (string.IsNullOrEmpty(json)) { error = "empty"; return false; }
            try { save = SaveCodec.FromJson(json); }
            catch (Exception e) { error = e.GetType().Name + ": " + e.Message; save = null; return false; }
            if (save == null) { error = "not a save (no heroes list or gold)"; return false; }
            return true;
        }

        public SaveData Load(DateTime now)
        {
            Error = ""; CorruptKey = "";
            var main = _st.Get(MainKey);
            if (string.IsNullOrEmpty(main))
            {
                // No save. A backup on its own means the main key was lost: use it.
                if (TryParse(_st.Get(BackupKey), out var lone, out _)) { State = SaveLoadState.FromBackup; _mainTrusted = true; return lone; }
                State = SaveLoadState.Fresh; _mainTrusted = true;
                return new SaveData();
            }
            if (TryParse(main, out var s, out var err)) { State = SaveLoadState.Loaded; _mainTrusted = true; return s; }

            Error = err;
            KeepCorrupt(main, now);
            _mainTrusted = false; // the bad save must never become the backup
            if (TryParse(_st.Get(BackupKey), out var bak, out _)) { State = SaveLoadState.FromBackup; return bak; }
            State = SaveLoadState.Failed;
            return new SaveData();
        }

        // Writes the save. Returns false (and writes nothing) while a failed load is unresolved.
        public bool Write(SaveData s)
        {
            if (!CanWrite) return false;
            var json = SaveCodec.ToJson(s);
            if (_mainTrusted)
            {
                var cur = _st.Get(MainKey);
                if (!string.IsNullOrEmpty(cur) && cur != json) _st.Set(BackupKey, cur);
            }
            _st.Set(MainKey, json);
            _st.Flush();
            _mainTrusted = true;
            s.sourceJson = json;
            return true;
        }

        // The player chose to start over (or loaded a save code) after a failed load. The unreadable
        // save stays in its corrupt copy.
        public void AcceptFresh()
        {
            if (State == SaveLoadState.Failed) State = SaveLoadState.Fresh;
        }

        // The raw text of the unreadable save, for "copy damaged save" (support can sometimes fix it).
        public string CorruptText => string.IsNullOrEmpty(CorruptKey) ? "" : _st.Get(CorruptKey) ?? "";

        void KeepCorrupt(string raw, DateTime now)
        {
            // Same bad save as last launch: keep the one copy instead of a new one per launch.
            var last = _st.Get(LastCorruptKey);
            if (!string.IsNullOrEmpty(last) && _st.Get(last) == raw) { CorruptKey = last; return; }
            var key = CorruptPrefix + now.ToString("yyyyMMdd-HHmmss", System.Globalization.CultureInfo.InvariantCulture);
            if (key == last) key += "b";
            _st.Set(key, raw);
            _st.Set(LastCorruptKey, key);
            _st.Flush();
            CorruptKey = key;
        }
    }
}
