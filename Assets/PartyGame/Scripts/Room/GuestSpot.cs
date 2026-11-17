using UnityEngine;

namespace PartyGame
{
    /// <summary>
    /// A place where a guest stands during the party. Keep them in left-to-right order (as seen by
    /// the camera) under Room/GuestSpots; with fewer guests than spots, guests spread out evenly.
    /// </summary>
    public class GuestSpot : MonoBehaviour
    {
        void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.3f, 0.6f, 1f, 0.9f);
            Gizmos.DrawWireSphere(transform.position + Vector3.up * 0.05f, 0.45f);
            Gizmos.DrawLine(transform.position, transform.position + Vector3.up * GuestSprites.Height);
        }
    }
}
