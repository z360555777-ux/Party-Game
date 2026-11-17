using System.Collections.Generic;
using UnityEngine;

namespace PartyGame
{
    /// <summary>
    /// Everything the game needs, in one asset. Put it at Resources/PartyGameContent to override the
    /// built-in defaults (menu: Party Game > Create Content Assets).
    /// </summary>
    [CreateAssetMenu(menuName = "Party Game/Game Content", fileName = "PartyGameContent")]
    public class GameContent : ScriptableObject
    {
        public const string ResourcePath = "PartyGameContent";

        [Tooltip("Played in order: tutorial, apply, final challenge.")]
        public List<OccasionData> occasions = new List<OccasionData>();
        public List<GuestData> guests = new List<GuestData>();
        public List<ActivityData> activities = new List<ActivityData>();
        public DialogueData dialogue;
        public ScoringSettings scoring = new ScoringSettings();

        /// <summary>Loads the content asset from Resources, or builds the defaults in memory.</summary>
        public static GameContent Load()
        {
            var asset = Resources.Load<GameContent>(ResourcePath);
            if (asset != null && asset.occasions.Count > 0 && asset.guests.Count > 0) return asset;
            return DefaultContent.Build();
        }
    }

    /// <summary>All tuning numbers in one place so designers can tweak them in the Inspector.</summary>
    [System.Serializable]
    public class ScoringSettings
    {
        [Tooltip("Happiness every guest starts at (3 = 3 stars).")]
        public float baseHappiness = 3f;

        [Tooltip("A guest only cares about an axis if |stat| is at least this.")]
        public float caresThreshold = 1f;

        [Tooltip("Happiness per point of (guest stat x party vibe) on the same axis.")]
        public float axisMatchScale = 0.35f;

        [Tooltip("Party size: Crowd vibe = (guests - this) x crowdPerGuest.")]
        public int neutralGuestCount = 4;
        public float crowdPerGuest = 1f;

        [Tooltip("Share of the party-wide Vibes added to every guest.")]
        public float vibesShare = 0.5f;

        [Tooltip("Storyteller + Inquisitive at the same party.")]
        public float storyPairVibes = 2f;

        [Tooltip("This many Gossips or more start a fight.")]
        public int gossipFightCount = 3;
        public float gossipFightVibes = -2f;

        [Tooltip("Each Gossip pushes every other guest this far toward Wallflower.")]
        public float gossipWallflowerShift = 0.5f;

        public float dislikePenalty = -1f;
        public float noFoodPenalty = -0.5f;
        public float dietaryUnmetPenalty = -1.5f;

        [Tooltip("Helper at the party lowers the TOTAL activity cost by this much, once per party (design doc: reduce prep by 1).")]
        public int helperDiscount = 1;
    }
}
