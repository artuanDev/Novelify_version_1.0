using UnityEngine;

namespace Novelify
{
    public readonly struct NovelRectLayout
    {
        public readonly Vector2 AnchorMin;
        public readonly Vector2 AnchorMax;
        public readonly Vector2 Pivot;
        public readonly Vector2 AnchoredPosition;
        public readonly Vector2 SizeDelta;

        public NovelRectLayout(Vector2 anchorMin, Vector2 anchorMax,
            Vector2 pivot, Vector2 anchoredPosition, Vector2 sizeDelta)
        {
            AnchorMin = anchorMin;
            AnchorMax = anchorMax;
            Pivot = pivot;
            AnchoredPosition = anchoredPosition;
            SizeDelta = sizeDelta;
        }
        public void Apply(RectTransform rect)
        {
            if (rect == null) return;
            rect.anchorMin = AnchorMin;
            rect.anchorMax = AnchorMax;
            rect.pivot = Pivot;
            rect.anchoredPosition = AnchoredPosition;
            rect.sizeDelta = SizeDelta;

        }
    }

    public static class NovelPresentationLayout
    {
        public static NovelRectLayout Dialogue(NovelDialogueAnchor value,
            float width, float height, float bottomMargin, float horizontalMargin)
        {
            int index = (int)value;
            Vector2 anchor = new Vector2((index % 3) * 0.5f,
                1f - (index / 3) * 0.5f);
            bool stretch = width <= 0f;
            Vector2 min = stretch ? new Vector2(0f, anchor.y) : anchor;
            Vector2 max = stretch ? new Vector2(1f, anchor.y) : anchor;
            Vector2 pivot = stretch ? new Vector2(0.5f, anchor.y) : anchor;
            float x = stretch ? 0f : EdgeOffset(anchor.x, horizontalMargin);
            float y = EdgeOffset(anchor.y, bottomMargin);
            return new NovelRectLayout(min, max, pivot, new Vector2(x, y),
                new Vector2(stretch ? -horizontalMargin * 2f : width,
                    Mathf.Max(1f, height)));
        }


        private static float EdgeOffset(float anchor, float margin)
        {
            if (anchor < 0.25f) return margin;
            if (anchor > 0.75f) return -margin;
            return 0f;
        }

        public static NovelRectLayout Speaker(NovelSpeakerAnchor anchor,
            float width, float height, float offset, float overlap)
        {
            Vector2 point, pivot, position;
            switch (anchor)
            {
                case NovelSpeakerAnchor.TopCenter:
                    point = new(0.5f, 1f); pivot = new(0.5f, 0.5f);
                    position = new(offset, height * 0.5f - overlap); break;
                case NovelSpeakerAnchor.TopRight:
                    point = new(1f, 1f); pivot = new(1f, 0.5f);
                    position = new(-offset, height * 0.5f - overlap); break;
                case NovelSpeakerAnchor.LeftTop:
                    point = new(0f, 1f); pivot = new(0.5f, 1f);
                    position = new(-width * 0.5f + overlap, -offset); break;
                case NovelSpeakerAnchor.LeftCenter:
                    point = new(0f, 0.5f); pivot = new(0.5f, 0.5f);
                    position = new(-width * 0.5f + overlap, offset); break;
                case NovelSpeakerAnchor.LeftBottom:
                    point = new(0f, 0f); pivot = new(0.5f, 0f);
                    position = new(-width * 0.5f + overlap, offset); break;
                case NovelSpeakerAnchor.BottomLeft:
                    point = new(0f, 0f); pivot = new(0f, 0.5f);
                    position = new(offset, -height * 0.5f + overlap); break;
                case NovelSpeakerAnchor.BottomCenter:
                    point = new(0.5f, 0f); pivot = new(0.5f, 0.5f);
                    position = new(offset, -height * 0.5f + overlap); break;
                case NovelSpeakerAnchor.BottomRight:
                    point = new(1f, 0f); pivot = new(1f, 0.5f);
                    position = new(-offset, -height * 0.5f + overlap); break;
                case NovelSpeakerAnchor.RightBottom:
                    point = new(1f, 0f); pivot = new(0.5f, 0f);
                    position = new(width * 0.5f - overlap, offset); break;
                case NovelSpeakerAnchor.RightCenter:
                    point = new(1f, 0.5f); pivot = new(0.5f, 0.5f);
                    position = new(width * 0.5f - overlap, offset); break;
                case NovelSpeakerAnchor.RightTop:
                    point = new(1f, 1f); pivot = new(0.5f, 1f);
                    position = new(width * 0.5f - overlap, -offset); break;
                default: // TopLeft
                    point = new(0f, 1f); pivot = new(0f, 0.5f);
                    position = new(offset, height * 0.5f - overlap); break;
            }
            return new NovelRectLayout(point, point, pivot, position,
            new Vector2(Mathf.Max(1f, width), Mathf.Max(1f, height)));
        }
    }
}