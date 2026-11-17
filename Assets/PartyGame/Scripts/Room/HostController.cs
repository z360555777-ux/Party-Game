using System;
using UnityEngine;

namespace PartyGame
{
    /// <summary>
    /// The player: walks around the room with WASD / arrow keys (relative to the camera) and presses E
    /// to use the nearest <see cref="Interactable"/>. Bumps into walls, furniture and guests.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class HostController : MonoBehaviour
    {
        public float speed = 3.6f;
        public Camera cam;
        [Tooltip("Set by the game while a menu, the phone or a conversation is open.")]
        public bool frozen;

        /// <summary>Called when the nearest usable thing changes (null = nothing in reach).</summary>
        public event Action<Interactable> TargetChanged;
        public Interactable Target { get; private set; }
        /// <summary>Point above the host's head (the "E" prompt floats here).</summary>
        public Transform head;

        CharacterController _cc;
        Transform _figure;
        float _walkTime;

        void Awake()
        {
            _cc = GetComponent<CharacterController>();
            _figure = transform.Find("Figure");
        }

        void Update()
        {
            if (cam == null) cam = Camera.main;
            Vector3 move = Vector3.zero;
            if (!frozen && cam != null)
            {
                float h = Input.GetAxisRaw("Horizontal");
                float v = Input.GetAxisRaw("Vertical");
                // Camera-relative: "up" on the keyboard walks away from the camera, along the floor.
                Vector3 fwd = Vector3.ProjectOnPlane(cam.transform.forward, Vector3.up).normalized;
                Vector3 right = Vector3.ProjectOnPlane(cam.transform.right, Vector3.up).normalized;
                move = Vector3.ClampMagnitude(fwd * v + right * h, 1f) * speed;
            }
            if (_cc.enabled) _cc.Move(move * Time.deltaTime);
            var p = transform.position;
            if (Mathf.Abs(p.y) > 0.001f) transform.position = new Vector3(p.x, 0f, p.z); // stay on the floor

            // A little hop while walking.
            if (_figure != null)
            {
                if (move.sqrMagnitude > 0.01f) _walkTime += Time.deltaTime;
                else _walkTime = 0f;
                _figure.localPosition = new Vector3(0, Mathf.Abs(Mathf.Sin(_walkTime * 12f)) * 0.08f, 0);
            }

            var target = frozen ? null : Interactable.Nearest(transform.position);
            if (target != Target)
            {
                Target = target;
                TargetChanged?.Invoke(target);
            }

            if (!frozen && Target != null && Target.CanUse &&
                (Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.Space)))
                Target.use?.Invoke();
        }

        /// <summary>Moves the host somewhere without walking (start of day, debug).</summary>
        public void Teleport(Vector3 pos)
        {
            _cc.enabled = false;
            transform.position = new Vector3(pos.x, 0f, pos.z);
            _cc.enabled = true;
        }

        public static HostController Create(PartyStage stage, Vector3 pos)
        {
            var root = PartyStage.MakeCharacter("Host", GuestSprites.ForHost(), stage.transform, pos, stage.cam,
                stage.headHeight, out var head);
            UnityEngine.Object.DestroyImmediate(root.GetComponent<CapsuleCollider>()); // the CharacterController is its body
            var cc = root.gameObject.AddComponent<CharacterController>();
            cc.center = new Vector3(0, 0.95f, 0);
            cc.height = 1.7f;
            cc.radius = 0.25f;
            cc.skinWidth = 0.03f;
            cc.stepOffset = 0.05f;
            var host = root.gameObject.AddComponent<HostController>();
            host.cam = stage.cam;
            host.head = head;
            return host;
        }
    }
}
