using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace PartyGame
{
    /// <summary>
    /// The party room in the PartyRoom scene. Everything in it is a normal scene object that the team
    /// can move, replace or restyle in the editor:
    ///   Room/Shell        floor, walls, door, phone table, record player, decoration
    ///   Room/Points       <see cref="RoomPoint"/>s: Phone, Door, Music (record player), HostStart
    ///   Room/Activities   one <see cref="ActivityProp"/> per activity: an empty spot + its furniture
    ///   Room/GuestSpots   <see cref="GuestSpot"/>s, left to right on screen; guests stand on these
    ///   Room/Guests       guests are spawned here at runtime (they depend on who you invite)
    /// The camera is used exactly as placed in the scene; the game only sets which part of the screen
    /// it draws into (the UI panel takes the left side).
    /// </summary>
    public class PartyStage : MonoBehaviour
    {
        public Camera cam;
        [Tooltip("Everything under here is used to work out how big the room is on screen.")]
        public Transform shell;
        public Transform activitiesRoot;
        public Transform spotsRoot;
        public Transform guestsRoot;
        [Tooltip("How far above a character's feet their head tag sits.")]
        public float headHeight = GuestSprites.Height + 0.15f;
        [Tooltip("Walking speed of guests coming in and leaving.")]
        public float guestWalkSpeed = 2.6f;

        readonly Dictionary<GuestData, Transform> _guests = new Dictionary<GuestData, Transform>();
        readonly Dictionary<GuestData, Transform> _heads = new Dictionary<GuestData, Transform>();

        public Camera Cam => cam;
        public IEnumerable<GuestData> Guests => _guests.Keys;

        void Awake()
        {
            if (cam == null) cam = Camera.main;
        }

        // ------------------------------------------------------------------ points and props

        public RoomPoint Point(RoomPoint.Kind kind)
        {
            foreach (var p in GetComponentsInChildren<RoomPoint>(true))
                if (p.kind == kind) return p;
            return null;
        }

        public ActivityProp[] Props =>
            activitiesRoot != null ? activitiesRoot.GetComponentsInChildren<ActivityProp>(true) : new ActivityProp[0];

        /// <summary>Shows the furniture of exactly these activities and hides the rest.</summary>
        public void SetActivities(IEnumerable<ActivityData> activities)
        {
            var wanted = new HashSet<string>();
            if (activities != null)
                foreach (var a in activities) wanted.Add(a.id);
            foreach (var prop in Props) prop.Show(wanted.Contains(prop.activityId));
        }

        /// <summary>Opens or closes the front door (swings the leaf on its hinge).</summary>
        public void SetDoorOpen(bool open)
        {
            var door = Point(RoomPoint.Kind.Door);
            if (door != null && door.moving != null)
                door.moving.localRotation = Quaternion.Euler(0, open ? -100f : 0f, 0);
        }

        // ------------------------------------------------------------------ guests

        /// <summary>Head anchor of a guest in the room, for UI that follows them. Null if not here.</summary>
        public Transform HeadOf(GuestData g) => g != null && _heads.TryGetValue(g, out var t) ? t : null;

        /// <summary>The guest's object in the room (has a collider), or null.</summary>
        public Transform RootOf(GuestData g) => g != null && _guests.TryGetValue(g, out var t) ? t : null;

        public void ClearGuests()
        {
            if (guestsRoot == null) guestsRoot = transform;
            for (int i = guestsRoot.childCount - 1; i >= 0; i--)
            {
                var child = guestsRoot.GetChild(i).gameObject;
                child.SetActive(false);
                Destroy(child);
            }
            _guests.Clear();
            _heads.Clear();
        }

        /// <summary>Puts every guest straight onto their spot.</summary>
        public void SetGuests(IList<GuestData> guests)
        {
            ClearGuests();
            if (guests == null) return;
            for (int i = 0; i < guests.Count; i++)
                Spawn(guests[i], SpotFor(i, guests.Count));
        }

        /// <summary>Guests come in through the front door one after another and walk to their spots.</summary>
        public IEnumerator Arrive(IList<GuestData> guests)
        {
            ClearGuests();
            var door = DoorInside();
            var walks = new List<Coroutine>();
            for (int i = 0; i < guests.Count; i++)
            {
                var t = Spawn(guests[i], door);
                walks.Add(StartCoroutine(Walk(t, SpotFor(i, guests.Count))));
                yield return new WaitForSeconds(0.45f);
            }
            foreach (var w in walks) yield return w;
        }

        /// <summary>Everyone walks out of the front door and disappears.</summary>
        public IEnumerator Leave()
        {
            var door = DoorInside();
            var walks = new List<Coroutine>();
            foreach (var t in _guests.Values)
            {
                walks.Add(StartCoroutine(Walk(t, door)));
                yield return new WaitForSeconds(0.25f);
            }
            foreach (var w in walks) yield return w;
            ClearGuests();
        }

        IEnumerator Walk(Transform t, Vector3 to)
        {
            if (t == null) yield break;
            Vector3 from = t.position;
            float time = Vector3.Distance(from, to) / Mathf.Max(0.1f, guestWalkSpeed);
            var figure = t.Find("Figure");
            for (float k = 0; k < time && t != null; k += Time.deltaTime)
            {
                t.position = Vector3.Lerp(from, to, k / time);
                if (figure != null) figure.localPosition = new Vector3(0, Mathf.Abs(Mathf.Sin(k * 12f)) * 0.08f, 0); // little hop
                yield return null;
            }
            if (t == null) yield break;
            t.position = to;
            if (figure != null) figure.localPosition = Vector3.zero;
        }

        Vector3 DoorInside()
        {
            var door = Point(RoomPoint.Kind.Door);
            return door != null ? door.transform.position : transform.position;
        }

        Vector3 SpotFor(int i, int n)
        {
            var spots = new List<Transform>();
            if (spotsRoot != null)
                foreach (var s in spotsRoot.GetComponentsInChildren<GuestSpot>()) spots.Add(s.transform);
            if (spots.Count == 0) return transform.position + Vector3.right * i;
            if (n >= spots.Count) return spots[i % spots.Count].position;
            // e.g. 3 guests on 5 spots -> spots 0, 2, 4
            int index = n == 1 ? spots.Count / 2 : Mathf.RoundToInt(i * (spots.Count - 1) / (float)(n - 1));
            return spots[index].position;
        }

        Transform Spawn(GuestData g, Vector3 pos)
        {
            var root = MakeCharacter("Guest_" + g.id, GuestSprites.For(g), guestsRoot, pos, cam, headHeight, out var head);
            _guests[g] = root;
            _heads[g] = head;
            return root;
        }

        /// <summary>A flat 2D stand-up with a floor shadow and a collider (used for guests and the host).</summary>
        public static Transform MakeCharacter(string name, Sprite sprite, Transform parent, Vector3 pos, Camera cam,
            float headHeight, out Transform head)
        {
            var root = new GameObject(name).transform;
            root.SetParent(parent, false);
            root.position = pos;

            var shadow = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Destroy(shadow.GetComponent<Collider>());
            shadow.name = "Shadow";
            shadow.transform.SetParent(root, false);
            shadow.transform.localPosition = new Vector3(0, 0.01f, 0);
            shadow.transform.localScale = new Vector3(0.8f, 0.005f, 0.8f);
            shadow.GetComponent<Renderer>().sharedMaterial = RoomBuilder.Mat(RoomBuilder.WoodDark * 0.85f);

            var figure = new GameObject("Figure");
            figure.transform.SetParent(root, false);
            figure.AddComponent<SpriteRenderer>().sprite = sprite;
            figure.AddComponent<Billboard>().cam = cam;

            var body = root.gameObject.AddComponent<CapsuleCollider>();
            body.center = new Vector3(0, 0.9f, 0);
            body.height = 1.8f;
            body.radius = 0.3f;

            head = new GameObject("Head").transform;
            head.SetParent(root, false);
            head.localPosition = new Vector3(0, headHeight, 0);
            return root;
        }

        // ------------------------------------------------------------------ camera

        /// <summary>
        /// Which part of the screen (0..1) the room is drawn in. The camera itself (position, angle,
        /// field of view) is whatever is set on it in the scene, so it can be adjusted there.
        /// </summary>
        public void SetViewport(Rect screen01)
        {
            if (cam != null) cam.rect = screen01;
        }
    }
}
