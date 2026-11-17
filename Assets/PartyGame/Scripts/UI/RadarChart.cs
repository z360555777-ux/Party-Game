using UnityEngine;
using UnityEngine.UI;

namespace PartyGame
{
    /// <summary>
    /// Pentagon stat chart like the team's mock-up: outer frame, dotted "neutral" ring, spokes and a
    /// filled shape. Each value is 0..1 (0 = centre = -2, 0.5 = neutral ring = 0, 1 = tip = +2).
    /// Unknown stats are drawn on the neutral ring and get no dot.
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public class RadarChart : MaskableGraphic
    {
        public float[] values = new float[5];
        public bool[] known = new bool[5];
        public Color frameColor = new Color(0.2f, 0.17f, 0.15f);
        public Color fillColor = new Color(0.45f, 0.75f, 0.5f, 0.45f);
        public Color dotColor = new Color(0.25f, 0.55f, 0.3f);
        public float lineWidth = 3f;

        public void SetData(float[] v, bool[] k)
        {
            values = v;
            known = k;
            SetVerticesDirty();
        }

        int N => values?.Length ?? 0;

        Vector2 Dir(int i)
        {
            float a = Mathf.Deg2Rad * (90f - 360f / N * i); // first axis points up, then clockwise
            return new Vector2(Mathf.Cos(a), Mathf.Sin(a));
        }

        float Radius => Mathf.Min(rectTransform.rect.width, rectTransform.rect.height) * 0.5f;
        Vector2 Center => rectTransform.rect.center;

        /// <summary>Local position of the label anchor for axis i, a bit outside the frame.</summary>
        public Vector2 LabelPosition(int i, float extra) => Center + Dir(i) * (Radius + extra);

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (N < 3) return;
            float r = Radius;
            var c = Center;

            // Filled shape (triangle fan)
            int centre = vh.currentVertCount;
            vh.AddVert(c, fillColor, Vector2.zero);
            for (int i = 0; i < N; i++)
                vh.AddVert(c + Dir(i) * r * Mathf.Clamp01(values[i]), fillColor, Vector2.zero);
            for (int i = 0; i < N; i++)
                vh.AddTriangle(centre, centre + 1 + i, centre + 1 + (i + 1) % N);

            // Outer frame and spokes
            for (int i = 0; i < N; i++)
            {
                Line(vh, c + Dir(i) * r, c + Dir((i + 1) % N) * r, lineWidth, frameColor);
                Line(vh, c, c + Dir(i) * r, lineWidth * 0.6f, frameColor);
            }

            // Dotted neutral ring
            var faint = new Color(frameColor.r, frameColor.g, frameColor.b, 0.5f);
            for (int i = 0; i < N; i++)
            {
                Vector2 a = c + Dir(i) * r * 0.5f, b = c + Dir((i + 1) % N) * r * 0.5f;
                const int dashes = 8;
                for (int d = 0; d < dashes; d++)
                    Line(vh, Vector2.Lerp(a, b, d / (float)dashes), Vector2.Lerp(a, b, (d + 0.5f) / dashes), 2f, faint);
            }

            // Dots for known values
            for (int i = 0; i < N; i++)
                if (known != null && i < known.Length && known[i])
                    Square(vh, c + Dir(i) * r * Mathf.Clamp01(values[i]), 7f, dotColor);
        }

        static void Line(VertexHelper vh, Vector2 a, Vector2 b, float w, Color col)
        {
            Vector2 n = (b - a).normalized;
            n = new Vector2(-n.y, n.x) * (w * 0.5f);
            int i = vh.currentVertCount;
            vh.AddVert(a - n, col, Vector2.zero);
            vh.AddVert(a + n, col, Vector2.zero);
            vh.AddVert(b + n, col, Vector2.zero);
            vh.AddVert(b - n, col, Vector2.zero);
            vh.AddTriangle(i, i + 1, i + 2);
            vh.AddTriangle(i, i + 2, i + 3);
        }

        static void Square(VertexHelper vh, Vector2 p, float half, Color col)
        {
            int i = vh.currentVertCount;
            vh.AddVert(p + new Vector2(-half, -half), col, Vector2.zero);
            vh.AddVert(p + new Vector2(-half, half), col, Vector2.zero);
            vh.AddVert(p + new Vector2(half, half), col, Vector2.zero);
            vh.AddVert(p + new Vector2(half, -half), col, Vector2.zero);
            vh.AddTriangle(i, i + 1, i + 2);
            vh.AddTriangle(i, i + 2, i + 3);
        }
    }
}
