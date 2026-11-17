using UnityEngine;

namespace PartyGame
{
    /// <summary>
    /// Something the player buys for the party. Resource and prep from the doc are merged into one cost.
    /// </summary>
    [CreateAssetMenu(menuName = "Party Game/Activity", fileName = "Activity")]
    public class ActivityData : ScriptableObject
    {
        public string id = "activity";
        public string displayName = "Activity";
        [TextArea] public string description = "";
        [Min(0)] public int cost = 1;

        [Tooltip("How much this activity pushes the party's vibe on each axis.")]
        public AxisValues vibeShift = new AxisValues();

        [Tooltip("Flat happiness bonus for every guest (e.g. food).")]
        public float everyoneBonus;

        [Tooltip("Counts as food for guests with a dietary need.")]
        public bool dietaryFriendly;

        [Tooltip("Counts as food at all (guests mind if there is none).")]
        public bool isFood;
    }
}
