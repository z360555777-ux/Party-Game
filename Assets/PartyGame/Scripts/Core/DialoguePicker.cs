using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace PartyGame
{
    /// <summary>Turns scoring reasons into lines of dialogue and short +/- captions.</summary>
    public class DialoguePicker
    {
        readonly DialogueData _data;
        readonly System.Random _rng;

        public DialoguePicker(DialogueData data, int seed)
        {
            _data = data;
            _rng = new System.Random(seed);
        }

        public string ForReason(Reason r, GuestData speaker)
        {
            if (r == null) return Pick("Generic.Good", speaker, null);
            string fallback = r.IsGood ? "Generic.Good" : "Generic.Bad";
            return _data.TryGet(r.key, out _) ? Pick(r.key, speaker, r.other) : Pick(fallback, speaker, r.other);
        }

        /// <summary>
        /// What a guest says in a party round: a reaction to the strongest thing revealed this round,
        /// or small talk (Vian's "random topics" idea) when nothing new happened to them.
        /// </summary>
        public string ForRound(GuestData g, List<Reason> revealedThisRound)
        {
            if (revealedThisRound == null || revealedThisRound.Count == 0) return SmallTalk(g);
            var strongest = revealedThisRound.OrderByDescending(r => Mathf.Abs(r.value)).First();
            return ForReason(strongest, g);
        }

        /// <summary>Short caption under the bubble, e.g. "Loves the playful mood". Key "Label.&lt;reason key&gt;".</summary>
        public string Caption(Reason r)
        {
            string key = "Label." + r.key;
            if (!_data.TryGet(key, out var lines)) return r.key;
            return lines[0].Replace("{other}", r.other != null ? r.other.displayName : "someone");
        }

        string SmallTalk(GuestData g) =>
            g.smallTalk.Count > 0 ? g.smallTalk[_rng.Next(g.smallTalk.Count)] : Pick("SmallTalk", g, null);

        public string ForReveal(GuestData g, PartyAxis axis, float caresThreshold)
        {
            float v = g.stats[axis];
            string key = Mathf.Abs(v) < caresThreshold ? "Reveal.Nothing" : $"Reveal.{axis}.{Axes.PoleKey(axis, v)}";
            return Pick(key, g, null);
        }

        public string Pick(string key, GuestData speaker, GuestData other)
        {
            if (!_data.TryGet(key, out List<string> lines)) return "...";
            string line = lines[_rng.Next(lines.Count)];
            return line.Replace("{name}", speaker != null ? speaker.displayName : "")
                       .Replace("{other}", other != null ? other.displayName : "someone");
        }
    }
}
