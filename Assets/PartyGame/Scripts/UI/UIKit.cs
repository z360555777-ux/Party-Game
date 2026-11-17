using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PartyGame
{
    /// <summary>
    /// Small helpers to build grey-box UGUI from code, so the prototype runs with no prefabs or
    /// manual scene setup. Replace with designed prefabs during the polish phase.
    /// </summary>
    public static class UIKit
    {
        public static readonly Color Background = new Color(0.97f, 0.94f, 0.89f);
        public static readonly Color Card = Color.white;
        public static readonly Color CardSelected = new Color(1f, 0.88f, 0.62f);
        public static readonly Color Ink = new Color(0.2f, 0.17f, 0.15f);
        public static readonly Color Muted = new Color(0.5f, 0.46f, 0.42f);
        public static readonly Color Accent = new Color(0.86f, 0.42f, 0.3f);
        public static readonly Color AccentDim = new Color(0.8f, 0.75f, 0.7f);
        public static readonly Color Good = new Color(0.3f, 0.65f, 0.35f);
        public static readonly Color Bad = new Color(0.8f, 0.3f, 0.3f);

        static Font _font;
        public static Font Font
        {
            get
            {
                if (_font == null) _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                return _font;
            }
        }

        public static Canvas CreateCanvas(Transform parent)
        {
            var go = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            go.transform.SetParent(parent, false);
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            if (UnityEngine.Object.FindObjectOfType<EventSystem>() == null)
            {
                var es = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
                es.transform.SetParent(parent, false);
            }
            return canvas;
        }

        public static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        public static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        public static Image Panel(Transform parent, Color color, string name = "Panel")
        {
            var rt = Rect(name, parent);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = color;
            return img;
        }

        public static VerticalLayoutGroup VLayout(Component c, float spacing = 10, int padding = 0,
            TextAnchor align = TextAnchor.UpperLeft)
        {
            var l = c.gameObject.AddComponent<VerticalLayoutGroup>();
            l.spacing = spacing;
            l.padding = new RectOffset(padding, padding, padding, padding);
            l.childAlignment = align;
            l.childControlWidth = l.childControlHeight = true;
            l.childForceExpandWidth = true;
            l.childForceExpandHeight = false;
            return l;
        }

        public static HorizontalLayoutGroup HLayout(Component c, float spacing = 10, int padding = 0,
            TextAnchor align = TextAnchor.UpperLeft)
        {
            var l = c.gameObject.AddComponent<HorizontalLayoutGroup>();
            l.spacing = spacing;
            l.padding = new RectOffset(padding, padding, padding, padding);
            l.childAlignment = align;
            l.childControlWidth = l.childControlHeight = true;
            l.childForceExpandWidth = false;
            l.childForceExpandHeight = false;
            return l;
        }

        /// <summary>A transparent container with a layout group.</summary>
        public static RectTransform Row(Transform parent, float spacing = 10, TextAnchor align = TextAnchor.UpperLeft)
        {
            var rt = Rect("Row", parent);
            HLayout(rt, spacing, 0, align);
            return rt;
        }

        public static RectTransform Column(Transform parent, float spacing = 10, TextAnchor align = TextAnchor.UpperLeft)
        {
            var rt = Rect("Column", parent);
            VLayout(rt, spacing, 0, align);
            return rt;
        }

        /// <summary>
        /// A card with padding and a vertical layout, exactly <paramref name="width"/> wide.
        /// flexibleWidth is pinned to 0: otherwise the card's VerticalLayoutGroup reports itself as
        /// flexible and a half-empty row stretches its cards to fill the screen.
        /// </summary>
        public static Image CardPanel(Transform parent, Color color, float width, int padding = 18)
        {
            var img = Panel(parent, color, "Card");
            VLayout(img, 8, padding);
            Size(img, width, -1, 0);
            return img;
        }

        /// <summary>A row of equal-height cards that does not grow vertically to fill the page.</summary>
        public static RectTransform CardRow(Transform parent, float spacing, TextAnchor align)
        {
            var row = Row(parent, spacing, align);
            Size(row, -1, -1, -1, 0);
            return row;
        }

        static bool? _tmpReady;

        /// <summary>
        /// TextMeshPro is sharp at any resolution, but needs its Essential Resources imported
        /// (Window > TextMeshPro > Import TMP Essential Resources). Until then we fall back to legacy Text.
        /// </summary>
        public static bool UseTMP
        {
            get
            {
                if (_tmpReady == null)
                {
                    _tmpReady = Resources.Load<TMP_Settings>("TMP Settings") != null && TMP_Settings.defaultFontAsset != null;
                    if (_tmpReady == false)
                        Debug.LogWarning("[Party Game] TextMeshPro resources not found, using legacy text (can look blurry). " +
                                         "Import them via Window > TextMeshPro > Import TMP Essential Resources.");
                }
                return _tmpReady.Value;
            }
        }

        public static Graphic Label(Transform parent, string text, int size = 26, Color? color = null,
            TextAnchor align = TextAnchor.UpperLeft, FontStyle style = FontStyle.Normal)
        {
            var rt = Rect("Text", parent);
            if (UseTMP)
            {
                var tmp = rt.gameObject.AddComponent<TextMeshProUGUI>();
                tmp.text = text;
                tmp.fontSize = size;
                tmp.fontStyle = style == FontStyle.Bold ? FontStyles.Bold
                    : style == FontStyle.Italic ? FontStyles.Italic : FontStyles.Normal;
                tmp.color = color ?? Ink;
                tmp.alignment = ToTMP(align);
                tmp.enableWordWrapping = true;
                tmp.overflowMode = TextOverflowModes.Overflow;
                tmp.richText = true;
                tmp.raycastTarget = false;
                return tmp;
            }

            var t = rt.gameObject.AddComponent<Text>();
            t.font = Font;
            t.text = text;
            t.fontSize = size;
            t.fontStyle = style;
            t.color = color ?? Ink;
            t.alignment = align;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.supportRichText = true;
            t.raycastTarget = false;
            return t;
        }

        /// <summary>Changes the text of a label made by <see cref="Label"/> (TMP or legacy).</summary>
        public static void SetText(Graphic g, string text)
        {
            if (g is TMP_Text tmp) tmp.text = text;
            else if (g is Text t) t.text = text;
        }

        /// <summary>
        /// A card placed freely (not by a layout group): anchored to a screen point, fixed width,
        /// height fits its content.
        /// </summary>
        public static Image FloatingCard(Transform parent, Color color, float width, Vector2 anchor, Vector2 pivot,
            Vector2 position, int padding = 24)
        {
            var card = Panel(parent, color, "Card");
            VLayout(card, 10, padding);
            var rt = card.rectTransform;
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = pivot;
            rt.anchoredPosition = position;
            rt.sizeDelta = new Vector2(width, 0);
            card.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            return card;
        }

        static TextAlignmentOptions ToTMP(TextAnchor a) => a switch
        {
            TextAnchor.UpperLeft => TextAlignmentOptions.TopLeft,
            TextAnchor.UpperCenter => TextAlignmentOptions.Top,
            TextAnchor.UpperRight => TextAlignmentOptions.TopRight,
            TextAnchor.MiddleLeft => TextAlignmentOptions.Left,
            TextAnchor.MiddleCenter => TextAlignmentOptions.Center,
            TextAnchor.MiddleRight => TextAlignmentOptions.Right,
            TextAnchor.LowerLeft => TextAlignmentOptions.BottomLeft,
            TextAnchor.LowerCenter => TextAlignmentOptions.Bottom,
            _ => TextAlignmentOptions.BottomRight
        };

        public static Button Button(Transform parent, string label, Action onClick, float width = 260,
            float height = 64, bool interactable = true, Color? bg = null, int fontSize = 26)
        {
            var img = Panel(parent, bg ?? Accent, "Button");
            var btn = img.gameObject.AddComponent<Button>();
            btn.interactable = interactable;
            var colors = btn.colors;
            colors.disabledColor = new Color(1, 1, 1, 0.35f);
            colors.highlightedColor = new Color(0.92f, 0.92f, 0.92f);
            btn.colors = colors;
            if (onClick != null) btn.onClick.AddListener(() => onClick());
            Size(img, width, height);

            var t = Label(img.transform, label, fontSize, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
            Stretch(t.rectTransform);
            return btn;
        }

        public static LayoutElement Size(Component c, float width = -1, float height = -1,
            float flexWidth = -1, float flexHeight = -1)
        {
            // Not "??": Unity objects can be fake-null in the editor.
            var le = c.GetComponent<LayoutElement>();
            if (le == null) le = c.gameObject.AddComponent<LayoutElement>();
            if (width >= 0) { le.preferredWidth = width; le.minWidth = width; }
            if (height >= 0) { le.preferredHeight = height; le.minHeight = height; }
            if (flexWidth >= 0) le.flexibleWidth = flexWidth;
            if (flexHeight >= 0) le.flexibleHeight = flexHeight;
            return le;
        }

        /// <summary>Flexible empty space inside a layout.</summary>
        public static void Spacer(Transform parent, float height = -1)
        {
            var rt = Rect("Spacer", parent);
            if (height >= 0) Size(rt, -1, height);
            else Size(rt, -1, -1, 1, 1);
        }

        /// <summary>A row of small squares: filled for each star earned.</summary>
        public static RectTransform Stars(Transform parent, int stars, int max = 5, float size = 26)
        {
            var row = Row(parent, 6, TextAnchor.MiddleLeft);
            for (int i = 0; i < max; i++)
            {
                var s = Panel(row, i < stars ? new Color(1f, 0.75f, 0.2f) : AccentDim, "Star");
                Size(s, size, size);
            }
            return row;
        }

        /// <summary>Colour for a happiness value from 1 (bad) to 5 (great).</summary>
        public static Color MoodColor(float happiness)
        {
            float t = Mathf.InverseLerp(1.5f, 4.5f, happiness);
            return Color.Lerp(Bad, Good, t);
        }

        public static string MoodFace(float happiness) =>
            happiness >= 4.2f ? ":D" : happiness >= 3.4f ? ":)" : happiness >= 2.6f ? ":|" : happiness >= 1.8f ? ":(" : "D:";
    }
}
