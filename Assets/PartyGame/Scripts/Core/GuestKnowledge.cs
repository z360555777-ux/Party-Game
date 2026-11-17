using System.Collections.Generic;
using System.Linq;

namespace PartyGame
{
    /// <summary>
    /// What the player has learned about each guest. Stats start hidden and are revealed by
    /// chatting during parties and by what guests say afterwards. Persists across the 3 parties.
    /// </summary>
    public class GuestKnowledge
    {
        readonly Dictionary<GuestData, HashSet<PartyAxis>> _axes = new Dictionary<GuestData, HashSet<PartyAxis>>();
        readonly Dictionary<GuestData, HashSet<GuestTrait>> _traits = new Dictionary<GuestData, HashSet<GuestTrait>>();
        readonly HashSet<GuestData> _dislikeKnown = new HashSet<GuestData>();
        readonly HashSet<GuestData> _dietKnown = new HashSet<GuestData>();

        public bool Knows(GuestData g, PartyAxis axis) => _axes.TryGetValue(g, out var s) && s.Contains(axis);
        public bool Knows(GuestData g, GuestTrait t) => _traits.TryGetValue(g, out var s) && s.Contains(t);
        public bool KnowsDislike(GuestData g) => _dislikeKnown.Contains(g);
        public bool KnowsDiet(GuestData g) => _dietKnown.Contains(g);

        /// <summary>Returns true if this was new information.</summary>
        public bool Reveal(GuestData g, PartyAxis axis) => Set(_axes, g).Add(axis);
        public bool Reveal(GuestData g, GuestTrait t) => g.HasTrait(t) && Set(_traits, g).Add(t);
        public bool RevealDislike(GuestData g) => g.hasDislike && _dislikeKnown.Add(g);
        public bool RevealDiet(GuestData g) => g.dietaryNeed && _dietKnown.Add(g);

        public IEnumerable<PartyAxis> UnknownAxes(GuestData g) => Axes.All.Where(a => !Knows(g, a));

        // --- Save / load (see SaveSystem)

        public void WriteTo(GuestSave s, GuestData g)
        {
            s.axes = _axes.TryGetValue(g, out var a) ? a.Select(x => (int)x).ToList() : new List<int>();
            s.traits = _traits.TryGetValue(g, out var t) ? t.Select(x => (int)x).ToList() : new List<int>();
            s.dislike = _dislikeKnown.Contains(g);
            s.diet = _dietKnown.Contains(g);
        }

        public void ReadFrom(GuestSave s, GuestData g)
        {
            foreach (var a in s.axes) Set(_axes, g).Add((PartyAxis)a);
            foreach (var t in s.traits) Set(_traits, g).Add((GuestTrait)t);
            if (s.dislike) _dislikeKnown.Add(g);
            if (s.diet) _dietKnown.Add(g);
        }

        static HashSet<T> Set<T>(Dictionary<GuestData, HashSet<T>> dict, GuestData g)
        {
            if (!dict.TryGetValue(g, out var s)) dict[g] = s = new HashSet<T>();
            return s;
        }
    }

    /// <summary>A single piece of newly learned info, for the results screen.</summary>
    public struct Discovery
    {
        public GuestData guest;
        public string text;
    }
}
