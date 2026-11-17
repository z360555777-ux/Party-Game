using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace PartyGame
{
    /// <summary>What the player chose for one party.</summary>
    public class PartyPlan
    {
        public OccasionData occasion;
        public readonly List<GuestData> guests = new List<GuestData>();
        public readonly List<ActivityData> activities = new List<ActivityData>();
    }

    /// <summary>One plus or minus to a guest's happiness, with the dialogue key that explains it.</summary>
    public class Reason
    {
        public string key;          // e.g. "Tone.Serious.Bad"
        public float value;         // happiness change
        public GuestData other;     // guest involved, for {other}
        public PartyAxis? axis;     // axis involved, so results can reveal it
        public GuestTrait? trait;   // trait involved, so results can reveal it

        public bool IsGood => value > 0;
        public override string ToString() => $"{key} {value:+0.00;-0.00}";
    }

    public class GuestOutcome
    {
        public GuestData guest;
        public float happiness;
        public readonly List<Reason> reasons = new List<Reason>();

        /// <summary>
        /// Which reasons are revealed in each party round. Small effects come first, the biggest last,
        /// so every change to the mood bar during the party has a visible cause.
        /// happiness == base + sum of all reasons, so the bar ends exactly on the final score.
        /// </summary>
        public List<Reason>[] Schedule(int rounds)
        {
            var schedule = new List<Reason>[rounds];
            for (int i = 0; i < rounds; i++) schedule[i] = new List<Reason>();
            var ordered = reasons.OrderBy(r => Mathf.Abs(r.value)).ToList();
            for (int i = 0; i < ordered.Count; i++)
            {
                // Spread evenly; with fewer reasons than rounds, the early rounds stay empty (small talk).
                int round = ordered.Count >= rounds
                    ? i * rounds / ordered.Count
                    : rounds - ordered.Count + i;
                schedule[round].Add(ordered[i]);
            }
            return schedule;
        }

        public int Stars => Mathf.Clamp(Mathf.RoundToInt(happiness), 1, 5);
        public Reason Best => reasons.Where(r => r.value > 0).OrderByDescending(r => r.value).FirstOrDefault();
        public Reason Worst => reasons.Where(r => r.value < 0).OrderBy(r => r.value).FirstOrDefault();
    }

    public class PartyOutcome
    {
        public AxisValues vibe;               // final party vibe on each axis
        public float vibes;                   // party-wide Vibes from chemistry (doc's "+2 Vibes")
        public bool storyPair;
        public bool gossipFight;
        public readonly List<GuestOutcome> guests = new List<GuestOutcome>();

        public float AverageHappiness => guests.Count == 0 ? 0 : guests.Average(g => Mathf.Clamp(g.happiness, 1f, 5f));
        public int PartyStars => Mathf.Clamp(Mathf.RoundToInt(AverageHappiness), 1, 5);
        public GuestOutcome For(GuestData g) => guests.Find(x => x.guest == g);
    }

    /// <summary>
    /// Pure scoring logic: happiness = fit with the party + chemistry with other guests + trait bonuses.
    /// No UI, no MonoBehaviour, so it can be tested and tuned on its own.
    /// </summary>
    public static class PartyScorer
    {
        /// <summary>
        /// Total cost of the chosen activities. Design doc: "Helper - Reduce prep by 1", so a Helper
        /// takes helperDiscount off the TOTAL, once per party (not off every activity).
        /// </summary>
        public static int ActivityCost(PartyPlan plan, ScoringSettings s) =>
            Mathf.Max(0, RawActivityCost(plan) - HelperSaving(plan, s));

        public static int RawActivityCost(PartyPlan plan) => plan.activities.Sum(a => a.cost);

        /// <summary>How much the Helper actually saves right now (0 with no Helper or nothing chosen).</summary>
        public static int HelperSaving(PartyPlan plan, ScoringSettings s) =>
            HasHelper(plan) ? Mathf.Min(s.helperDiscount, RawActivityCost(plan)) : 0;

        public static bool HasHelper(PartyPlan plan) => plan.guests.Any(g => g.HasTrait(GuestTrait.Helper));

        /// <summary>Party vibe = occasion base + activities, with Crowd set by how many people came.</summary>
        public static AxisValues ComputeVibe(PartyPlan plan, ScoringSettings s)
        {
            var vibe = plan.occasion.baseVibe.Clone();
            foreach (var a in plan.activities)
                foreach (var axis in Axes.All)
                    vibe[axis] += a.vibeShift[axis];

            vibe[PartyAxis.Crowd] += (plan.guests.Count - s.neutralGuestCount) * s.crowdPerGuest;

            foreach (var axis in Axes.All)
                vibe[axis] = Axes.Clamp(vibe[axis]);
            return vibe;
        }

        public static PartyOutcome Score(PartyPlan plan, ScoringSettings s)
        {
            var outcome = new PartyOutcome { vibe = ComputeVibe(plan, s) };
            var guests = plan.guests;

            var storytellers = guests.Where(g => g.HasTrait(GuestTrait.Storyteller)).ToList();
            var askers = guests.Where(g => g.HasTrait(GuestTrait.Inquisitive)).ToList();
            int gossipCount = guests.Count(g => g.HasTrait(GuestTrait.Gossip));

            outcome.storyPair = storytellers.Count > 0 && askers.Count > 0;
            outcome.gossipFight = gossipCount >= s.gossipFightCount;
            if (outcome.storyPair) outcome.vibes += s.storyPairVibes;
            if (outcome.gossipFight) outcome.vibes += s.gossipFightVibes;

            bool anyFood = plan.activities.Any(a => a.isFood);
            bool dietFood = plan.activities.Any(a => a.dietaryFriendly);
            float everyoneBonus = plan.activities.Sum(a => a.everyoneBonus);

            foreach (var g in guests)
            {
                var go = new GuestOutcome { guest = g, happiness = s.baseHappiness };

                // --- 1. Fit with the party, axis by axis
                var stats = g.stats.Clone();
                int otherGossips = gossipCount - (g.HasTrait(GuestTrait.Gossip) ? 1 : 0);
                if (!g.HasTrait(GuestTrait.Gossip))
                    // Wallflower is the negative pole of Social.
                    stats[PartyAxis.Social] = Axes.Clamp(stats[PartyAxis.Social] - otherGossips * s.gossipWallflowerShift);

                foreach (var axis in Axes.All)
                {
                    float gv = stats[axis];
                    if (Mathf.Abs(gv) < s.caresThreshold) continue;
                    float v = gv * outcome.vibe[axis] * s.axisMatchScale;
                    if (Mathf.Abs(v) < 0.05f) continue;
                    Add(go, $"{axis}.{Axes.PoleKey(axis, gv)}.{(v > 0 ? "Good" : "Bad")}", v, axis: axis);
                }

                // --- 2. Chemistry with other guests
                if (outcome.storyPair)
                {
                    float share = s.storyPairVibes * s.vibesShare;
                    if (g.HasTrait(GuestTrait.Inquisitive))
                        Add(go, "Pair.Story.Good", share, other: storytellers.FirstOrDefault(x => x != g), trait: GuestTrait.Inquisitive);
                    else if (g.HasTrait(GuestTrait.Storyteller))
                        Add(go, "Pair.Ask.Good", share, other: askers.FirstOrDefault(x => x != g), trait: GuestTrait.Storyteller);
                    else
                        Add(go, "Vibes.Good", share); // good energy in the room
                }
                if (outcome.gossipFight)
                    Add(go, "Gossip.Fight.Bad", s.gossipFightVibes * s.vibesShare, trait: GuestTrait.Gossip);

                foreach (var other in guests)
                    if (g.Dislikes(other))
                        Add(go, "Dislike.Bad", s.dislikePenalty, other: other);

                // --- 3. Food and traits
                if (!anyFood) Add(go, "Food.None.Bad", s.noFoodPenalty);
                else if (everyoneBonus > 0) Add(go, "Food.Some.Good", everyoneBonus);

                if (g.dietaryNeed)
                {
                    if (dietFood) Add(go, "Diet.Met.Good", 0.5f);
                    else Add(go, "Diet.Unmet.Bad", s.dietaryUnmetPenalty);
                }

                if (g.HasTrait(GuestTrait.Helper))
                    Add(go, "Helper.Good", 0.5f, trait: GuestTrait.Helper);

                outcome.guests.Add(go);
            }
            return outcome;
        }

        static void Add(GuestOutcome go, string key, float value, GuestData other = null,
            PartyAxis? axis = null, GuestTrait? trait = null)
        {
            go.happiness += value;
            go.reasons.Add(new Reason { key = key, value = value, other = other, axis = axis, trait = trait });
        }
    }
}
