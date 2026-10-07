#if UNITY_EDITOR
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RoomBoardEditor
{
    public static partial class RoomBoardInstaller
    {
        private static Canvas CreateCanvas(Transform parent, string name, Vector2 size, float worldScale)
        {
            GameObject canvasObject = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            canvasObject.transform.SetParent(parent, false);
            canvasObject.layer = 0;
            canvasObject.transform.localRotation = Quaternion.identity;
            canvasObject.transform.localScale = Vector3.one * worldScale;
            RectTransform rect = canvasObject.GetComponent<RectTransform>();
            rect.sizeDelta = size;
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 0;
            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.dynamicPixelsPerUnit = 10f;
            return canvas;
        }

        private static GameObject CreatePanel(Transform parent, string name, float width, float preferredHeight, Color color)
        {
            GameObject panel = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(LayoutElement));
            panel.transform.SetParent(parent, false);
            RectTransform rect = panel.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(width, preferredHeight);
            Image image = panel.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            LayoutElement element = panel.GetComponent<LayoutElement>();
            element.preferredWidth = width;
            if (preferredHeight > 0f) element.preferredHeight = preferredHeight;
            return panel;
        }

        private static TextMeshProUGUI CreatePanelTitle(Transform parent, string name, string value,
            float fontSize, float width, float height, TMP_FontAsset font, Color color)
        {
            GameObject titleObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI), typeof(LayoutElement));
            titleObject.transform.SetParent(parent, false);
            LayoutElement element = titleObject.GetComponent<LayoutElement>();
            element.preferredHeight = height;
            TextMeshProUGUI text = titleObject.GetComponent<TextMeshProUGUI>();
            text.text = value;
            text.font = font;
            text.fontSize = fontSize;
            text.color = color;
            text.alignment = TextAlignmentOptions.MidlineLeft;
            text.enableWordWrapping = false;
            text.raycastTarget = false;
            RectTransform rect = titleObject.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(width, height);
            return text;
        }

        private static GameObject CreatePresenceRow(Transform parent, string name, float width, float height,
            float areaFontSize, float namesFontSize, TMP_FontAsset font)
        {
            GameObject row = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(LayoutElement));
            row.transform.SetParent(parent, false);
            RectTransform rowRect = row.GetComponent<RectTransform>();
            rowRect.sizeDelta = new Vector2(width, height);
            row.GetComponent<Image>().color = TokenSurface;
            row.GetComponent<Image>().raycastTarget = false;
            LayoutElement rowLayout = row.GetComponent<LayoutElement>();
            rowLayout.preferredHeight = height;
            rowLayout.minHeight = height;
            rowLayout.flexibleHeight = 0f;

            // Edges are placed with explicit offsets from stretched anchors; centre-pivot sizeDelta
            // placement pushed the name and count outside the row.
            const float pad = 6f;
            const float textLeft = 20f;
            const float textRight = 12f;
            float headerHeight = areaFontSize * 1.5f;
            float countWidth = areaFontSize * 4.5f;

            Image accent = CreateImage(row.transform, "Accent", Vector2.zero, Vector2.zero, TokenIdle);
            SetEdges(accent.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0f), new Vector2(8f, 0f));
            TextMeshProUGUI areaName = (TextMeshProUGUI)CreateText(row.transform, "AreaName", "部屋", areaFontSize,
                Vector2.zero, Vector2.zero, TokenText, TextAlignmentOptions.MidlineLeft, font, false, false);
            areaName.overflowMode = TextOverflowModes.Ellipsis;
            SetEdges(areaName.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(textLeft, -pad - headerHeight), new Vector2(-textRight - countWidth - 8f, -pad));
            TextMeshProUGUI count = (TextMeshProUGUI)CreateText(row.transform, "Count", "空室", areaFontSize * 0.82f,
                Vector2.zero, Vector2.zero, TokenMuted, TextAlignmentOptions.MidlineRight, font, false, false);
            SetEdges(count.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-textRight - countWidth, -pad - headerHeight), new Vector2(-textRight, -pad));
            TextMeshProUGUI names = (TextMeshProUGUI)CreateText(row.transform, "Names", "", namesFontSize,
                Vector2.zero, Vector2.zero, TokenMuted, TextAlignmentOptions.TopLeft, font, false, true);
            names.richText = true;
            names.overflowMode = TextOverflowModes.Ellipsis;
            SetEdges(names.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 1f),
                new Vector2(textLeft, pad), new Vector2(-textRight, -pad - headerHeight - 2f));
            return row;
        }

        private static Image CreateImage(Transform parent, string name, Vector2 size, Vector2 position, Color color)
        {
            GameObject imageObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            imageObject.transform.SetParent(parent, false);
            RectTransform rect = imageObject.GetComponent<RectTransform>();
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
            Image image = imageObject.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static TMP_Text CreateText(Transform parent, string name, string value, float fontSize,
            Vector2 size, Vector2 position, Color color, TextAlignmentOptions alignment,
            TMP_FontAsset font, bool autoSize, bool wordWrapping)
        {
            GameObject textObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            textObject.transform.SetParent(parent, false);
            RectTransform rect = textObject.GetComponent<RectTransform>();
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
            TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
            text.text = value;
            text.font = font;
            text.fontSize = fontSize;
            text.color = color;
            text.alignment = alignment;
            text.enableWordWrapping = wordWrapping;
            text.raycastTarget = false;
            if (autoSize)
            {
                text.enableAutoSizing = true;
                text.fontSizeMin = 12f;
                text.fontSizeMax = fontSize;
            }
            return text;
        }

        private static void SetEdges(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
        }
    }
}
#endif
