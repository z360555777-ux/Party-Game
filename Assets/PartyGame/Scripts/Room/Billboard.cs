using UnityEngine;

namespace PartyGame
{
    /// <summary>Keeps a flat sprite turned to the camera, like a paper stand-up.</summary>
    public class Billboard : MonoBehaviour
    {
        public Camera cam;

        void LateUpdate()
        {
            if (cam == null) cam = Camera.main;
            if (cam != null) transform.rotation = cam.transform.rotation;
        }
    }
}
