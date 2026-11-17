using System.Collections.Generic;
using UnityEngine;

namespace PartyGame
{
    /// <summary>
    /// A guest the player can invite. All stats start hidden; the player reveals them by hosting parties.
    /// </summary>
    [CreateAssetMenu(menuName = "Party Game/Guest", fileName = "Guest")]
    public class GuestData : ScriptableObject
    {
        public string id = "guest";
        public string displayName = "Guest";
        [Tooltip("Shown on the contact card before anything is revealed.")]
        [TextArea] public string blurb = "";
        public Color color = Color.white;

        [Tooltip("First party (0 = tutorial) where this guest is in your contacts. Spreadsheet 'Available' column.")]
        [Min(0)] public int availableFrom;

        [Header("Hidden stats (-2 .. +2)")]
        public AxisValues stats = new AxisValues();
        public List<GuestTrait> traits = new List<GuestTrait>();

        [Tooltip("Needs vegan / gluten-free food to be happy.")]
        public bool dietaryNeed;

        [Header("Dislikes other guests who lean hard one way on an axis")]
        public bool hasDislike;
        public PartyAxis dislikeAxis = PartyAxis.Tone;
        [Tooltip("+1 dislikes guests at the positive pole (e.g. Playful), -1 dislikes the negative pole (e.g. Serious).")]
        public int dislikeSign = -1;

        [Header("Small talk said at random during the party (Vian's RNG idea)")]
        public List<string> smallTalk = new List<string>();

        public bool HasTrait(GuestTrait t) => traits.Contains(t);

        /// <summary>Does this guest dislike that other guest?</summary>
        public bool Dislikes(GuestData other)
        {
            if (!hasDislike || other == null || other == this) return false;
            return other.stats[dislikeAxis] * dislikeSign >= 1.5f;
        }
    }
}
