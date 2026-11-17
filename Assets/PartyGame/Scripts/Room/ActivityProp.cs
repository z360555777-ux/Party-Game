using UnityEngine;

namespace PartyGame
{
    /// <summary>
    /// One activity's place in the room. While planning, an empty glowing spot shows at
    /// <see cref="spot"/>; the host walks there and presses E to set the activity up, which switches
    /// <see cref="furniture"/> on. The id matches ActivityData.id: craft, food, dietfood, cafe, bookclub, watch.
    /// Replace the furniture's children with real models freely; only this component and the id matter.
    /// </summary>
    public class ActivityProp : MonoBehaviour
    {
        public string activityId;
        [Tooltip("Shown when the activity is chosen. If empty, this whole object is switched instead.")]
        public GameObject furniture;
        [Tooltip("Where the host stands to set it up. If empty, this object's position.")]
        public Transform spot;

        public Vector3 SpotPosition => spot != null ? spot.position : transform.position;

        public void Show(bool on)
        {
            if (furniture != null) furniture.SetActive(on);
            else gameObject.SetActive(on);
        }

        void OnDrawGizmos()
        {
            Gizmos.color = new Color(1f, 0.6f, 0.2f, 0.8f);
            Gizmos.DrawWireCube(SpotPosition + Vector3.up * 0.05f, new Vector3(0.6f, 0.1f, 0.6f));
        }
    }
}
