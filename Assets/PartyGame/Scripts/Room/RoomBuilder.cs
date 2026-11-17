using System.Collections.Generic;
using UnityEngine;

namespace PartyGame
{
    /// <summary>
    /// Builds the grey-box party room out of primitives: a living room with a floor, back, left and
    /// right walls, seen through a perspective camera from the front (where the fourth wall would be),
    /// like Animal Crossing / Consume Me interiors. The camera is framed so the picture is filled edge to
    /// edge with the room. Used by the editor menu "Party Game > Build Party Room Scene" to create
    /// PartyRoom.unity, and at runtime only as a fallback when that scene is missing.
    ///
    /// Layout (top view, +Z = back wall, camera at the front looking in):
    ///   back wall:   cafe counter (left corner) . TV . window + food table . record player . front door
    ///   left wall:   bookshelf + two armchairs, plant
    ///   right wall:  diet-food table, phone on a console
    ///   middle:      sofa facing the TV, craft table; guests stand in the front half
    /// </summary>
    public static class RoomBuilder
    {
        public const float HalfX = 5f;          // floor is 10 x 8
        public const float HalfZ = 4f;
        public const float WallHeight = 6.0f;   // tall enough that the camera never sees over them
        const float WallThick = 0.3f;
        const float FloorThick = 0.3f;

        public static readonly Vector3 CameraPosition = new Vector3(0f, 7.6f, -10.2f);
        public static readonly Vector3 CameraLookAt = new Vector3(0f, 0.4f, 0.9f);
        public const float CameraFov = 48f;

        public static readonly Color Wood = new Color(0.80f, 0.62f, 0.44f);
        public static readonly Color WoodDark = new Color(0.55f, 0.38f, 0.26f);
        static readonly Color WallBack = new Color(0.86f, 0.80f, 0.66f);
        static readonly Color WallSide = new Color(0.80f, 0.74f, 0.60f);
        static readonly Color Cream = new Color(0.96f, 0.92f, 0.84f);

        public static readonly string[] ActivityIds = { "watch", "cafe", "bookclub", "food", "dietfood", "craft" };

        /// <summary>Builds the whole room (and sets up camera and light). Returns its PartyStage.</summary>
        public static PartyStage Build(Camera cam)
        {
            var root = new GameObject("Room");
            var stage = root.AddComponent<PartyStage>();

            stage.shell = Group(root.transform, "Shell");
            BuildShell(stage.shell);

            var points = Group(root.transform, "Points");
            BuildDoor(stage.shell, points);
            BuildPhone(stage.shell, points);
            BuildRecordPlayer(stage.shell, points);
            Point(points, "HostStart", RoomPoint.Kind.HostStart, new Vector3(0f, 0, -2.4f));

            stage.activitiesRoot = Group(root.transform, "Activities");
            foreach (var id in ActivityIds)
                BuildProp(stage.activitiesRoot, id).Show(false);

            stage.spotsRoot = Group(root.transform, "GuestSpots");
            var spots = new[]
            {
                new Vector3(-2.5f, 0, -1.0f), new Vector3(-1.2f, 0, -1.9f), new Vector3(0.1f, 0, -0.8f),
                new Vector3(1.6f, 0, -1.8f), new Vector3(3.0f, 0, -0.8f),
            };
            for (int i = 0; i < spots.Length; i++)
                Group(stage.spotsRoot, "Spot" + (i + 1), spots[i]).gameObject.AddComponent<GuestSpot>();

            stage.guestsRoot = Group(root.transform, "Guests");

            stage.cam = SetupCamera(cam);
            SetupLight();
            return stage;
        }

        static Transform Group(Transform parent, string name, Vector3 pos = default)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            t.localPosition = pos;
            return t;
        }

        static RoomPoint Point(Transform parent, string name, RoomPoint.Kind kind, Vector3 pos)
        {
            var p = Group(parent, name, pos).gameObject.AddComponent<RoomPoint>();
            p.kind = kind;
            return p;
        }

        // ------------------------------------------------------------------ camera and light

        /// <summary>Perspective camera at the open front of the room, looking in and down.</summary>
        public static Camera SetupCamera(Camera cam)
        {
            if (cam == null) cam = Camera.main;
            if (cam == null)
            {
                var go = new GameObject("Main Camera") { tag = "MainCamera" };
                cam = go.AddComponent<Camera>();
                go.AddComponent<AudioListener>();
            }
            cam.orthographic = false;
            cam.fieldOfView = CameraFov;
            cam.nearClipPlane = 0.2f;
            cam.farClipPlane = 60f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.25f, 0.22f, 0.2f);
            cam.transform.position = CameraPosition;
            cam.transform.rotation = Quaternion.LookRotation(CameraLookAt - CameraPosition, Vector3.up);
            return cam;
        }

        public static Light SetupLight()
        {
            Light sun = null;
            foreach (var l in Object.FindObjectsOfType<Light>())
                if (l.type == LightType.Directional) { sun = l; break; }
            if (sun == null)
            {
                sun = new GameObject("Sun").AddComponent<Light>();
                sun.type = LightType.Directional;
            }
            // Warm light coming in from the front-left, like through a window behind the camera.
            sun.transform.rotation = Quaternion.Euler(50f, 30f, 0f);
            sun.color = new Color(1f, 0.93f, 0.82f);
            sun.intensity = 0.85f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.5f;

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.62f, 0.56f, 0.52f);
            RenderSettings.skybox = null;
            return sun;
        }

        // ------------------------------------------------------------------ room shell

        static void BuildShell(Transform shell)
        {
            var floor = Group(shell, "Floor");
            Box(floor, "Slab", new Vector3(0, -FloorThick / 2, -1f), new Vector3(HalfX * 2 + 1f, FloorThick, HalfZ * 2 + 2f), Wood);
            for (int i = -4; i <= 4; i++)
                Box(floor, "Plank", new Vector3(i * 1.1f, 0.002f, -1f), new Vector3(0.04f, 0.01f, HalfZ * 2 + 2f), WoodDark * 1.1f, solid: false);

            var walls = Group(shell, "Walls");
            float h = WallHeight;
            Box(walls, "BackWall", new Vector3(0, h / 2, HalfZ + WallThick / 2), new Vector3(HalfX * 2 + WallThick * 2, h, WallThick), WallBack);
            Box(walls, "LeftWall", new Vector3(-HalfX - WallThick / 2, h / 2, -1f), new Vector3(WallThick, h, HalfZ * 2 + 2f), WallSide);
            Box(walls, "RightWall", new Vector3(HalfX + WallThick / 2, h / 2, -1f), new Vector3(WallThick, h, HalfZ * 2 + 2f), WallSide);
            // Wainscot band and baseboards make the walls read as a real room.
            Box(walls, "BackWainscot", new Vector3(0, 0.55f, HalfZ - 0.02f), new Vector3(HalfX * 2, 1.1f, 0.04f), new Color(0.62f, 0.72f, 0.62f), solid: false);
            Box(walls, "LeftWainscot", new Vector3(-HalfX + 0.02f, 0.55f, -1f), new Vector3(0.04f, 1.1f, HalfZ * 2 + 2f), new Color(0.58f, 0.68f, 0.58f), solid: false);
            Box(walls, "RightWainscot", new Vector3(HalfX - 0.02f, 0.55f, -1f), new Vector3(0.04f, 1.1f, HalfZ * 2 + 2f), new Color(0.58f, 0.68f, 0.58f), solid: false);
            Box(walls, "BackRail", new Vector3(0, 1.12f, HalfZ - 0.04f), new Vector3(HalfX * 2, 0.06f, 0.06f), Cream, solid: false);
            Box(walls, "LeftRail", new Vector3(-HalfX + 0.04f, 1.12f, -1f), new Vector3(0.06f, 0.06f, HalfZ * 2 + 2f), Cream, solid: false);
            Box(walls, "RightRail", new Vector3(HalfX - 0.04f, 1.12f, -1f), new Vector3(0.06f, 0.06f, HalfZ * 2 + 2f), Cream, solid: false);

            // Invisible edge at the front, so the host can't walk out towards the camera.
            Edge(walls, "EdgeFront", new Vector3(0, 1f, -HalfZ - 0.2f), new Vector3(HalfX * 2, 2f, 0.4f));

            var deco = Group(shell, "Decoration");
            var window = Group(deco, "Window");
            Box(window, "Frame", new Vector3(1.0f, 2.3f, HalfZ - 0.03f), new Vector3(2.0f, 1.5f, 0.08f), Cream, solid: false);
            Box(window, "Glass", new Vector3(1.0f, 2.3f, HalfZ - 0.08f), new Vector3(1.75f, 1.25f, 0.04f), new Color(0.66f, 0.84f, 0.95f), solid: false);
            Box(window, "Bar", new Vector3(1.0f, 2.3f, HalfZ - 0.11f), new Vector3(0.06f, 1.25f, 0.04f), Cream, solid: false);
            Box(window, "Sill", new Vector3(1.0f, 1.52f, HalfZ - 0.12f), new Vector3(2.1f, 0.06f, 0.2f), Cream, solid: false);
            var picture = Group(deco, "Picture");
            Box(picture, "Frame", new Vector3(-1.2f, 2.9f, HalfZ - 0.04f), new Vector3(1.3f, 0.9f, 0.06f), WoodDark, solid: false);
            Box(picture, "Canvas", new Vector3(-1.2f, 2.9f, HalfZ - 0.08f), new Vector3(1.1f, 0.72f, 0.04f), new Color(0.95f, 0.75f, 0.55f), solid: false);
            var rug = Group(deco, "Rug");
            Box(rug, "Border", new Vector3(0.1f, 0.015f, -1.3f), new Vector3(6.6f, 0.03f, 2.6f), new Color(0.86f, 0.55f, 0.45f), solid: false);
            Box(rug, "Inner", new Vector3(0.1f, 0.02f, -1.3f), new Vector3(6.0f, 0.03f, 2.0f), new Color(0.95f, 0.82f, 0.62f), solid: false);
            var plant = Group(deco, "Plant");
            Cyl(plant, "Pot", new Vector3(-4.5f, 0.3f, 2.5f), new Vector3(0.6f, 0.3f, 0.6f), new Color(0.78f, 0.45f, 0.32f));
            Sphere(plant, "Leaves", new Vector3(-4.5f, 1.0f, 2.5f), new Vector3(0.9f, 1.0f, 0.9f), new Color(0.38f, 0.6f, 0.36f), solid: false);
        }

        static void Edge(Transform parent, string name, Vector3 pos, Vector3 size)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.AddComponent<BoxCollider>().size = size;
        }

        /// <summary>Front door at the right end of the back wall; guests come in and leave through it.</summary>
        static void BuildDoor(Transform shell, Transform points)
        {
            var door = Group(shell, "Door");
            const float x = 3.7f, w = 1.0f, h = 2.4f;
            float z = HalfZ - 0.05f;
            Box(door, "FrameTop", new Vector3(x, h + 0.06f, z), new Vector3(w + 0.24f, 0.12f, 0.12f), Cream, solid: false);
            Box(door, "FrameL", new Vector3(x - w / 2 - 0.06f, h / 2, z), new Vector3(0.12f, h, 0.12f), Cream, solid: false);
            Box(door, "FrameR", new Vector3(x + w / 2 + 0.06f, h / 2, z), new Vector3(0.12f, h, 0.12f), Cream, solid: false);
            // The leaf hangs on a hinge at its left edge, so it can swing open.
            var hinge = Group(door, "Hinge", new Vector3(x - w / 2, 0, z - 0.01f));
            Box(hinge, "Leaf", new Vector3(w / 2, h / 2, 0), new Vector3(w, h, 0.06f), new Color(0.7f, 0.45f, 0.35f), solid: false);
            Sphere(hinge, "Knob", new Vector3(w - 0.12f, 1.1f, -0.06f), new Vector3(0.09f, 0.09f, 0.09f), new Color(0.95f, 0.8f, 0.4f), solid: false);
            Box(door, "Mat", new Vector3(x, 0.012f, HalfZ - 0.5f), new Vector3(1.1f, 0.02f, 0.6f), new Color(0.55f, 0.45f, 0.4f), solid: false);

            var p = Point(points, "Door", RoomPoint.Kind.Door, new Vector3(x, 0, HalfZ - 0.8f));
            p.moving = hinge;
        }

        /// <summary>Phone on a console against the right wall: used to invite friends.</summary>
        static void BuildPhone(Transform shell, Transform points)
        {
            var t = Group(shell, "PhoneConsole", new Vector3(HalfX - 0.3f, 0, 2.2f));
            Box(t, "Console", new Vector3(0, 0.45f, 0), new Vector3(0.55f, 0.9f, 0.9f), WoodDark);
            Box(t, "Phone", new Vector3(-0.02f, 0.96f, 0), new Vector3(0.3f, 0.12f, 0.4f), new Color(0.85f, 0.35f, 0.35f), solid: false);
            Box(t, "Handset", new Vector3(-0.02f, 1.05f, 0), new Vector3(0.1f, 0.06f, 0.44f), new Color(0.75f, 0.3f, 0.3f), solid: false);
            Point(points, "Phone", RoomPoint.Kind.Phone, new Vector3(HalfX - 1.15f, 0, 2.2f));
        }

        /// <summary>Record player on a cabinet by the back wall, left of the door: changing the record moves the party on.</summary>
        static void BuildRecordPlayer(Transform shell, Transform points)
        {
            var t = Group(shell, "RecordPlayer", new Vector3(2.5f, 0, HalfZ - 0.45f));
            Box(t, "Cabinet", new Vector3(0, 0.4f, 0), new Vector3(0.9f, 0.8f, 0.7f), WoodDark);
            Box(t, "Player", new Vector3(0, 0.86f, 0), new Vector3(0.6f, 0.12f, 0.6f), new Color(0.9f, 0.86f, 0.78f), solid: false);
            Cyl(t, "Record", new Vector3(0, 0.93f, 0), new Vector3(0.45f, 0.01f, 0.45f), new Color(0.12f, 0.12f, 0.14f), solid: false);
            Cyl(t, "Label", new Vector3(0, 0.94f, 0), new Vector3(0.14f, 0.01f, 0.14f), new Color(0.9f, 0.4f, 0.3f), solid: false);
            Point(points, "RecordPlayer", RoomPoint.Kind.Music, new Vector3(2.5f, 0, HalfZ - 1.25f));
        }

        // ------------------------------------------------------------------ activity props

        /// <summary>One activity: an empty spot to stand on, and the furniture that appears once it's set up.</summary>
        public static ActivityProp BuildProp(Transform parent, string id)
        {
            var root = new GameObject("Activity_" + id).transform;
            root.SetParent(parent, false);
            var prop = root.gameObject.AddComponent<ActivityProp>();
            prop.activityId = id;
            var f = Group(root, "Furniture");
            prop.furniture = f.gameObject;
            Vector3 spot;

            switch (id)
            {
                case "watch": // TV against the back wall, sofa facing it
                    Box(f, "TVStand", new Vector3(-1.2f, 0.3f, 3.6f), new Vector3(1.8f, 0.6f, 0.6f), WoodDark);
                    Box(f, "TV", new Vector3(-1.2f, 1.3f, 3.75f), new Vector3(2.0f, 1.15f, 0.12f), new Color(0.15f, 0.14f, 0.16f));
                    Box(f, "Screen", new Vector3(-1.2f, 1.3f, 3.68f), new Vector3(1.8f, 0.98f, 0.02f), new Color(0.45f, 0.65f, 0.85f), solid: false);
                    var sofa = Group(f, "Sofa");
                    Box(sofa, "Seat", new Vector3(-1.2f, 0.25f, 2.35f), new Vector3(2.2f, 0.5f, 0.8f), new Color(0.85f, 0.55f, 0.42f));
                    Box(sofa, "Back", new Vector3(-1.2f, 0.7f, 1.95f), new Vector3(2.2f, 0.8f, 0.25f), new Color(0.8f, 0.5f, 0.38f));
                    Box(sofa, "ArmL", new Vector3(-2.2f, 0.45f, 2.35f), new Vector3(0.25f, 0.5f, 0.8f), new Color(0.8f, 0.5f, 0.38f));
                    Box(sofa, "ArmR", new Vector3(-0.2f, 0.45f, 2.35f), new Vector3(0.25f, 0.5f, 0.8f), new Color(0.8f, 0.5f, 0.38f));
                    spot = new Vector3(-1.2f, 0, 1.3f);
                    break;

                case "cafe": // counter in the back-left corner with cups, a coffee machine and two stools
                    Box(f, "Counter", new Vector3(-3.8f, 0.5f, 3.55f), new Vector3(2.0f, 1.0f, 0.9f), new Color(0.9f, 0.86f, 0.78f));
                    Box(f, "Top", new Vector3(-3.8f, 1.03f, 3.55f), new Vector3(2.1f, 0.06f, 1.0f), WoodDark, solid: false);
                    Box(f, "CoffeeMachine", new Vector3(-4.4f, 1.3f, 3.7f), new Vector3(0.45f, 0.5f, 0.4f), new Color(0.3f, 0.28f, 0.3f), solid: false);
                    for (int i = 0; i < 3; i++)
                        Cyl(f, "Cup", new Vector3(-3.7f + i * 0.3f, 1.12f, 3.4f), new Vector3(0.16f, 0.07f, 0.16f), Cream, solid: false);
                    Cyl(f, "Stool", new Vector3(-4.3f, 0.35f, 2.75f), new Vector3(0.42f, 0.35f, 0.42f), new Color(0.55f, 0.75f, 0.7f));
                    Cyl(f, "Stool", new Vector3(-3.3f, 0.35f, 2.75f), new Vector3(0.42f, 0.35f, 0.42f), new Color(0.55f, 0.75f, 0.7f));
                    spot = new Vector3(-3.8f, 0, 2.0f);
                    break;

                case "bookclub": // bookshelf on the left wall, two armchairs and a side table
                    var shelf = Group(f, "Bookshelf");
                    Box(shelf, "Case", new Vector3(-4.7f, 1.2f, 0.4f), new Vector3(0.6f, 2.4f, 1.8f), WoodDark);
                    var bookColors = new[] { new Color(0.8f, 0.3f, 0.3f), new Color(0.3f, 0.5f, 0.8f), new Color(0.9f, 0.75f, 0.3f), new Color(0.4f, 0.7f, 0.45f) };
                    for (int row = 0; row < 3; row++)
                        for (int b = 0; b < 6; b++)
                            Box(shelf, "Book", new Vector3(-4.48f, 0.45f + row * 0.7f, -0.3f + b * 0.26f),
                                new Vector3(0.25f, 0.45f, 0.18f), bookColors[(row + b) % bookColors.Length], solid: false);
                    for (int c = 0; c < 2; c++)
                    {
                        var chair = Group(f, "Armchair");
                        float z = c == 0 ? -0.2f : 1.0f;
                        Box(chair, "Seat", new Vector3(-3.5f, 0.3f, z), new Vector3(0.9f, 0.6f, 0.9f), new Color(0.45f, 0.55f, 0.75f));
                        Box(chair, "Back", new Vector3(-3.15f, 0.75f, z), new Vector3(0.2f, 0.9f, 0.9f), new Color(0.4f, 0.5f, 0.7f));
                    }
                    Cyl(f, "SideTable", new Vector3(-3.6f, 0.3f, 0.4f), new Vector3(0.45f, 0.3f, 0.45f), Wood);
                    Box(f, "Book", new Vector3(-3.6f, 0.63f, 0.4f), new Vector3(0.3f, 0.06f, 0.22f), new Color(0.8f, 0.3f, 0.3f), solid: false);
                    spot = new Vector3(-2.4f, 0, 0.4f);
                    break;

                case "food": // table under the window with plates and a cake
                    Table(f, new Vector3(1.0f, 0, 3.45f), new Vector2(1.6f, 0.9f), Cream);
                    Cyl(f, "Cake", new Vector3(1.0f, 0.95f, 3.45f), new Vector3(0.45f, 0.12f, 0.45f), new Color(0.95f, 0.7f, 0.75f), solid: false);
                    Cyl(f, "Plate", new Vector3(0.45f, 0.86f, 3.35f), new Vector3(0.3f, 0.02f, 0.3f), Color.white, solid: false);
                    Cyl(f, "Plate", new Vector3(1.55f, 0.86f, 3.35f), new Vector3(0.3f, 0.02f, 0.3f), Color.white, solid: false);
                    Sphere(f, "Bun", new Vector3(1.55f, 0.92f, 3.35f), new Vector3(0.16f, 0.1f, 0.16f), new Color(0.9f, 0.6f, 0.3f), solid: false);
                    spot = new Vector3(1.0f, 0, 2.5f);
                    break;

                case "dietfood": // a second table by the right wall with a green cloth and salad bowls
                    Table(f, new Vector3(4.35f, 0, 0.3f), new Vector2(0.9f, 1.6f), new Color(0.6f, 0.82f, 0.58f));
                    Cyl(f, "Bowl", new Vector3(4.35f, 0.92f, -0.1f), new Vector3(0.4f, 0.1f, 0.4f), Color.white, solid: false);
                    Sphere(f, "Salad", new Vector3(4.35f, 0.98f, -0.1f), new Vector3(0.32f, 0.12f, 0.32f), new Color(0.45f, 0.75f, 0.35f), solid: false);
                    Cyl(f, "Bowl", new Vector3(4.35f, 0.92f, 0.7f), new Vector3(0.4f, 0.1f, 0.4f), Color.white, solid: false);
                    Sphere(f, "Salad", new Vector3(4.35f, 0.98f, 0.7f), new Vector3(0.32f, 0.12f, 0.32f), new Color(0.95f, 0.55f, 0.3f), solid: false);
                    spot = new Vector3(3.5f, 0, -0.5f);
                    break;

                case "craft": // table in the middle with paper, a jar of pens and a box
                    Table(f, new Vector3(1.6f, 0, 1.3f), new Vector2(1.6f, 1.0f), Wood);
                    Box(f, "Paper", new Vector3(1.2f, 0.87f, 1.4f), new Vector3(0.5f, 0.02f, 0.35f), new Color(0.95f, 0.85f, 0.4f), solid: false);
                    Box(f, "Paper", new Vector3(1.4f, 0.88f, 1.15f), new Vector3(0.45f, 0.02f, 0.3f), new Color(0.6f, 0.75f, 0.95f), solid: false);
                    Cyl(f, "PenJar", new Vector3(2.05f, 0.98f, 1.45f), new Vector3(0.18f, 0.12f, 0.18f), new Color(0.85f, 0.4f, 0.4f), solid: false);
                    Box(f, "CraftBox", new Vector3(2.05f, 0.95f, 1.05f), new Vector3(0.35f, 0.18f, 0.25f), new Color(0.7f, 0.55f, 0.85f), solid: false);
                    spot = new Vector3(1.6f, 0, 0.3f);
                    break;

                default:
                    Box(f, "Crate", new Vector3(3.6f, 0.3f, -2.6f), new Vector3(0.6f, 0.6f, 0.6f), new Color(0.85f, 0.7f, 0.5f));
                    spot = new Vector3(3.0f, 0, -2.6f);
                    break;
            }

            prop.spot = Group(root, "Spot", spot);
            return prop;
        }

        static void Table(Transform root, Vector3 at, Vector2 size, Color cloth)
        {
            var table = Group(root, "Table", at);
            Box(table, "Top", new Vector3(0, 0.8f, 0), new Vector3(size.x, 0.1f, size.y), cloth, solid: false);
            float lx = size.x / 2 - 0.1f, lz = size.y / 2 - 0.1f;
            foreach (var x in new[] { -lx, lx })
            foreach (var z in new[] { -lz, lz })
                Box(table, "Leg", new Vector3(x, 0.38f, z), new Vector3(0.08f, 0.76f, 0.08f), WoodDark, solid: false);
            // One collider for the whole table, from the floor up, so the host walks around it.
            var block = table.gameObject.AddComponent<BoxCollider>();
            block.center = new Vector3(0, 0.45f, 0);
            block.size = new Vector3(size.x, 0.9f, size.y);
        }

        // ------------------------------------------------------------------ primitives and materials

        static GameObject Shape(PrimitiveType type, Transform parent, string name, Vector3 pos, Vector3 scale, Color color, bool solid)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            if (!solid)
            {
                var col = go.GetComponent<Collider>();
                if (Application.isPlaying) Object.Destroy(col); else Object.DestroyImmediate(col);
            }
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = Mat(color);
            return go;
        }

        static GameObject Box(Transform p, string n, Vector3 pos, Vector3 scale, Color c, bool solid = true) => Shape(PrimitiveType.Cube, p, n, pos, scale, c, solid);
        // Unity's cylinder is 2 units tall, so scale.y is half the height.
        static GameObject Cyl(Transform p, string n, Vector3 pos, Vector3 scale, Color c, bool solid = true) => Shape(PrimitiveType.Cylinder, p, n, pos, scale, c, solid);
        static GameObject Sphere(Transform p, string n, Vector3 pos, Vector3 scale, Color c, bool solid = true) => Shape(PrimitiveType.Sphere, p, n, pos, scale, c, solid);

        // One shared matte material per colour. "Legacy Shaders/Diffuse" is in the project's Always
        // Included Shaders, so it also works in builds. The scene builder saves these as assets.
        static Shader _diffuse;
        static readonly Dictionary<Color, Material> Mats = new Dictionary<Color, Material>();

        public static Material Mat(Color c)
        {
            if (Mats.TryGetValue(c, out var m) && m != null) return m;
            if (_diffuse == null) _diffuse = Shader.Find("Legacy Shaders/Diffuse");
            m = new Material(_diffuse != null ? _diffuse : Shader.Find("Standard")) { color = c };
            m.name = "Room_" + ColorUtility.ToHtmlStringRGB(c);
            Mats[c] = m;
            return m;
        }

        /// <summary>A see-through glowing disc for empty activity spots (runtime only).</summary>
        public static Material GlowMat(Color c)
        {
            var shader = Shader.Find("Sprites/Default"); // always included, supports transparency
            return new Material(shader) { color = c };
        }
    }
}
