using System.Collections.Generic;
using UnityEngine;

namespace PartyGame
{
    /// <summary>
    /// Placeholder 2D character art, drawn in code: a body in the guest's colour, a head with hair and
    /// eyes, and a dark outline. Swap GuestData for real sprites later (art direction: 2D characters
    /// in a 3D room).
    /// </summary>
    public static class GuestSprites
    {
        public const float Height = 1.8f; // world units, feet to top of head
        const int W = 128, H = 256;

        static readonly Dictionary<GuestData, Sprite> Cache = new Dictionary<GuestData, Sprite>();

        static readonly Color[] Skin =
        {
            new Color(0.98f, 0.86f, 0.74f), new Color(0.9f, 0.72f, 0.56f),
            new Color(0.72f, 0.52f, 0.38f), new Color(0.5f, 0.34f, 0.25f),
        };
        static readonly Color[] Hair =
        {
            new Color(0.2f, 0.14f, 0.1f), new Color(0.45f, 0.28f, 0.15f),
            new Color(0.85f, 0.4f, 0.25f), new Color(0.12f, 0.12f, 0.14f), new Color(0.9f, 0.75f, 0.45f),
        };
        static readonly Color Outline = new Color(0.2f, 0.15f, 0.13f);

        public static Sprite For(GuestData g)
        {
            if (Cache.TryGetValue(g, out var cached) && cached != null) return cached;
            var sprite = Make(g.id ?? g.displayName ?? "guest", g.color, false);
            Cache[g] = sprite;
            return sprite;
        }

        static Sprite _host;

        /// <summary>The player's host: same style as the guests, with an apron so they stand out.</summary>
        public static Sprite ForHost()
        {
            if (_host == null) _host = Make("host", new Color(0.36f, 0.42f, 0.62f), true);
            return _host;
        }

        static Sprite Make(string key, Color body, bool apron)
        {
            int seed = Mathf.Abs(key.GetHashCode());
            Color skin = Skin[seed % Skin.Length];
            Color hair = Hair[(seed / 7) % Hair.Length];

            var px = new Color[W * H];
            const int o = 4; // outline width

            // Outline first (shapes grown by o), then fill.
            Body(px, o, Outline);
            Head(px, o, Outline);
            Body(px, 0, body);
            if (apron)
                for (int y = 6; y <= 112; y++)
                for (int x = 40; x <= 88; x++)
                    Set(px, x, y, new Color(0.97f, 0.95f, 0.9f));
            Head(px, 0, skin);
            HairCap(px, hair);
            Dot(px, 49, 186, 6, Outline);
            Dot(px, 79, 186, 6, Outline);
            Dot(px, 40, 170, 7, new Color(0.95f, 0.6f, 0.6f, 0.6f)); // cheeks
            Dot(px, 88, 170, 7, new Color(0.95f, 0.6f, 0.6f, 0.6f));

            var tex = new Texture2D(W, H, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                name = "Character_" + key,
            };
            tex.SetPixels(px);
            tex.Apply();

            var sprite = Sprite.Create(tex, new Rect(0, 0, W, H), new Vector2(0.5f, 0f), H / Height);
            sprite.name = tex.name;
            return sprite;
        }

        /// <summary>Rounded body: shoulders at the top, flat at the feet.</summary>
        static void Body(Color[] px, int grow, Color c)
        {
            int x0 = 22 - grow, x1 = 106 + grow, y0 = 0, y1 = 140 + grow, r = 34 + grow;
            for (int y = y0; y <= y1 && y < H; y++)
            for (int x = Mathf.Max(0, x0); x <= x1 && x < W; x++)
            {
                // Round the two top corners.
                int cx = x < x0 + r ? x0 + r : x > x1 - r ? x1 - r : x;
                int cy = y > y1 - r ? y1 - r : y;
                if ((x - cx) * (x - cx) + (y - cy) * (y - cy) <= r * r) Set(px, x, y, c);
            }
        }

        static void Head(Color[] px, int grow, Color c) => Dot(px, 64, 184, 46 + grow, c);

        static void HairCap(Color[] px, Color c)
        {
            // Top part of the head circle, with a little fringe.
            for (int y = 196; y <= 232; y++)
            for (int x = 16; x <= 112; x++)
            {
                int dx = x - 64, dy = y - 184;
                bool inHead = dx * dx + dy * dy <= 46 * 46;
                bool fringe = y >= 200 + (int)(4 * Mathf.Sin(x * 0.35f));
                if (inHead && fringe) Set(px, x, y, c);
            }
        }

        static void Dot(Color[] px, int cx, int cy, int r, Color c)
        {
            for (int y = cy - r; y <= cy + r; y++)
            for (int x = cx - r; x <= cx + r; x++)
                if ((x - cx) * (x - cx) + (y - cy) * (y - cy) <= r * r) Set(px, x, y, c);
        }

        static void Set(Color[] px, int x, int y, Color c)
        {
            if (x < 0 || y < 0 || x >= W || y >= H) return;
            int i = y * W + x;
            px[i] = c.a >= 1f ? c : Color.Lerp(px[i], new Color(c.r, c.g, c.b, 1f), c.a);
        }
    }
}
