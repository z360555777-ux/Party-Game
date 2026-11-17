using UnityEngine;

namespace PartyGame
{
    /// <summary>
    /// A special place in the room. The host walks up to it and presses E:
    ///   Phone      invite friends
    ///   Door       start the party (guests come in through here) / they leave through it
    ///   Music      the record player: move the party on to the next round
    ///   HostStart  where the host stands at the start of each day
    /// The object's position is where the host has to stand (within <see cref="radius"/>).
    /// </summary>
    public class RoomPoint : MonoBehaviour
    {
        public enum Kind { Phone, Door, Music, HostStart }

        public Kind kind;
        [Tooltip("How close the host has to be to use it.")]
        public float radius = 1.3f;
        [Tooltip("Optional part that moves when used, e.g. the door leaf (rotates around its own Y axis).")]
        public Transform moving;

        void OnDrawGizmos()
        {
            Gizmos.color = kind == Kind.HostStart ? Color.green : new Color(1f, 0.85f, 0.2f);
            Gizmos.DrawWireSphere(transform.position + Vector3.up * 0.05f, kind == Kind.HostStart ? 0.3f : radius);
        }
    }
}
