using UnityEngine;

namespace PartyGame
{
    /// <summary>
    /// The request card that starts each party: tells the player what kind of party it is and the budget.
    /// </summary>
    [CreateAssetMenu(menuName = "Party Game/Occasion", fileName = "Occasion")]
    public class OccasionData : ScriptableObject
    {
        public string id = "occasion";
        public string title = "Party";
        [TextArea] public string brief = "";

        [Tooltip("The vibe this occasion has before any activities are added.")]
        public AxisValues baseVibe = new AxisValues();

        [Min(1)] public int guestCount = 3;
        [Min(0)] public int budget = 5;
        [Tooltip("Chats the player gets in EACH party round (refilled every round). Max one chat per guest per round.")]
        [Min(0)] public int chatsPerRound = 2;
        [Min(1)] public int rounds = 3;
    }
}
