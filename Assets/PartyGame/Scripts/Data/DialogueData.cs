using System;
using System.Collections.Generic;
using UnityEngine;

namespace PartyGame
{
    /// <summary>
    /// Dialogue pool keyed by reason (the doc's Points / Reason / Dialogue table).
    /// Keys look like "Tone.Serious.Bad" (a Serious guest at a Playful party) or "Pair.Story".
    /// Lines may use {name} for the speaker and {other} for another guest involved.
    /// </summary>
    [CreateAssetMenu(menuName = "Party Game/Dialogue", fileName = "Dialogue")]
    public class DialogueData : ScriptableObject
    {
        [Serializable]
        public class Entry
        {
            public string key;
            [TextArea] public List<string> lines = new List<string>();
        }

        public List<Entry> entries = new List<Entry>();

        Dictionary<string, Entry> _lookup;

        public void Add(string key, params string[] lines)
        {
            var e = entries.Find(x => x.key == key);
            if (e == null) { e = new Entry { key = key }; entries.Add(e); }
            e.lines.AddRange(lines);
            _lookup = null;
        }

        public bool TryGet(string key, out List<string> lines)
        {
            if (_lookup == null)
            {
                _lookup = new Dictionary<string, Entry>();
                foreach (var e in entries)
                    if (!string.IsNullOrEmpty(e.key)) _lookup[e.key] = e;
            }
            if (_lookup.TryGetValue(key, out var entry) && entry.lines.Count > 0)
            {
                lines = entry.lines;
                return true;
            }
            lines = null;
            return false;
        }

        void OnValidate() => _lookup = null;
    }
}
