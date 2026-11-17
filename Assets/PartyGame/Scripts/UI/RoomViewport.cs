using UnityEngine;

namespace PartyGame
{
    /// <summary>Keeps a UI element pinned above a point in the 3D room (e.g. a guest's head).</summary>
    [DefaultExecutionOrder(100)]
    public class WorldFollower : MonoBehaviour
    {
        public Transform target;
        public Camera cam;
        [Tooltip("Extra offset in canvas units (1920x1080 reference), e.g. to stagger neighbouring tags.")]
        public Vector2 offset;
        Canvas _canvas;

        void LateUpdate()
        {
            if (target == null || cam == null) return;
            if (_canvas == null) _canvas = GetComponentInParent<Canvas>();
            float scale = _canvas != null ? _canvas.scaleFactor : 1f;
            Vector3 p = cam.WorldToScreenPoint(target.position);
            transform.position = new Vector3(p.x + offset.x * scale, p.y + offset.y * scale, 0f);
        }
    }
}
