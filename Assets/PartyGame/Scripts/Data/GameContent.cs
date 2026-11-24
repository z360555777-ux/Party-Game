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
        [Tooltip("Things the host can offer one guest, once per party.")]
        public List<InterventionItem> interventions = new List<InterventionItem>();
        public ScoringSettings scoring = new ScoringSettings();

        /// <summary>Loads the content asset from Resources, or builds the defaults in memory.</summary>
        public static GameContent Load()
        {
            var asset = Resources.Load<GameContent>(ResourcePath);
            if (asset != null && asset.occasions.Count > 0 && asset.guests.Count > 0) return asset;
            return DefaultContent.Build();
        }
    }

    /// <summary>
    /// Design doc "Intervention": something the host offers one guest during a party.
    /// <see cref="push"/> says which way it nudges that guest's party (e.g. a book: Small party, Wallflower).
    /// A guest who leans that way likes it, one who leans the other way doesn't, and either way the
    /// player learns where they stand on those scales.
    /// </summary>
    [System.Serializable]
    public class InterventionItem
    {
        public string id;
        public string displayName;
        [TextArea] public string description;
        public AxisValues push = new AxisValues();
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

        [Header("Willingness (design doc: inviting a friend to incompatible parties lowers it)")]
        [Tooltip("Most Willingness a friend can have.")]
        public int willingnessMax = 10;
        [Tooltip("Willingness every friend starts a new game with.")]
        public int willingnessStart = 5;
        [Tooltip("A guest who leaves with this many stars or fewer loses 1 Willingness.")]
        public int willingnessLossStars = 2;
        [Tooltip("A guest who leaves with this many stars or more gains 1 Willingness back.")]
        public int willingnessGainStars = 4;

        [Header("Interventions (design doc: one per party, affects one guest)")]
        [Tooltip("Happiness per point of the guest's stat on the item's axis.")]
        public float interventionScale = 0.5f;
        [Tooltip("Largest happiness change one intervention can cause.")]
        public float interventionMax = 1f;
    }
}
