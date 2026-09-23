using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Novelify
{
    public enum NovelChoiceArrangement
    {
        Vertical,
        Horizontal,
        Circular
    }

    /// <summary>
    /// Lays generated choices out in columns, rows, or concentric rings.
    /// ChoicesPerGroup controls items per column, row, or ring respectively.
    /// </summary>
    [AddComponentMenu("")]
    public sealed class NovelChoiceLayoutGroup : UIBehaviour, ILayoutGroup
    {
        [SerializeField] private NovelChoiceArrangement arrangement;
        [SerializeField] private int choicesPerGroup;
        [SerializeField] private float choiceSpacing = 14f;
        [SerializeField] private float groupSpacing = 24f;
        [SerializeField] private float choiceWidth = 640f;
        [SerializeField] private float choiceHeight = 64f;
        [SerializeField] private float circleRadius = 210f;
        [SerializeField] private float circleStartAngle = 90f;
        [SerializeField] private float circleArc = 360f;
        private const float LayoutPadding = 12f;

        public NovelChoiceArrangement Arrangement => arrangement;
        public int ChoicesPerGroup => choicesPerGroup;
        private readonly DrivenRectTransformTracker _tracker = new();
        private RectTransform RectTransform => transform as RectTransform;

        public void Configure(
            NovelChoiceArrangement value,
            int perGroup,
            float spacing,
            float betweenGroups,
            Vector2 choiceSize,
            float radius,
            float startAngle,
            float arc)
        {
            arrangement = value;
            choicesPerGroup = Mathf.Max(0, perGroup);
            choiceSpacing = Mathf.Max(0f, spacing);
            groupSpacing = Mathf.Max(0f, betweenGroups);
            choiceWidth = Mathf.Max(1f, choiceSize.x);
            choiceHeight = Mathf.Max(1f, choiceSize.y);
            circleRadius = Mathf.Max(0f, radius);
            circleStartAngle = startAngle;
            circleArc = Mathf.Clamp(arc, -360f, 360f);
            SetDirty();
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            SetDirty();
        }

        protected override void OnDisable()
        {
            _tracker.Clear();
            if (RectTransform != null)
                LayoutRebuilder.MarkLayoutForRebuild(RectTransform);
            base.OnDisable();
        }

        protected override void OnRectTransformDimensionsChange()
        {
            base.OnRectTransformDimensionsChange();
            SetDirty();
        }

        public void SetLayoutHorizontal() => ApplyLayout();

        public void SetLayoutVertical() => ApplyLayout();

        private void ApplyLayout()
        {
            var children = new System.Collections.Generic.List<RectTransform>();
            for (int index = 0; index < transform.childCount; index++)
            {
                Transform candidate = transform.GetChild(index);
                if (candidate.gameObject.activeInHierarchy &&
                    candidate is RectTransform rect &&
                    candidate.GetComponent<Button>() != null)
                    children.Add(rect);
            }
            int count = children.Count;
            _tracker.Clear();
            if (count == 0)
                return;

            int perGroup = choicesPerGroup <= 0
                ? count
                : Mathf.Max(1, choicesPerGroup);
            int groupCount = Mathf.CeilToInt(count / (float)perGroup);
            Vector2 choiceSize = new Vector2(choiceWidth, choiceHeight);
            Vector2 panelSize = RectTransform != null
                ? RectTransform.rect.size
                : Vector2.zero;
            float fitScale = CalculateFitScale(
                arrangement,
                count,
                perGroup,
                choiceSize,
                choiceSpacing,
                groupSpacing,
                circleRadius,
                circleStartAngle,
                circleArc,
                panelSize,
                LayoutPadding);

            for (int index = 0; index < count; index++)
            {
                RectTransform child = children[index];
                _tracker.Add(this, child,
                    DrivenTransformProperties.Anchors |
                    DrivenTransformProperties.AnchoredPosition |
                    DrivenTransformProperties.Pivot |
                    DrivenTransformProperties.SizeDelta |
                    DrivenTransformProperties.Scale);
                child.anchorMin = new Vector2(0.5f, 0.5f);
                child.anchorMax = new Vector2(0.5f, 0.5f);
                child.pivot = new Vector2(0.5f, 0.5f);
                child.sizeDelta = new Vector2(choiceWidth, choiceHeight);
                child.localScale = new Vector3(
                    fitScale, fitScale, 1f);
                child.anchoredPosition = CalculateChoicePosition(
                    arrangement,
                    index,
                    count,
                    perGroup,
                    groupCount,
                    choiceSize,
                    choiceSpacing,
                    groupSpacing,
                    circleRadius,
                    circleStartAngle,
                    circleArc) * fitScale;
            }
        }

        private void SetDirty()
        {
            if (!IsActive() || RectTransform == null ||
                CanvasUpdateRegistry.IsRebuildingLayout())
                return;
            LayoutRebuilder.MarkLayoutForRebuild(RectTransform);
        }

        public static Vector2 CalculateChoicePosition(
            NovelChoiceArrangement value,
            int index,
            int count,
            int perGroup,
            int groupCount,
            Vector2 choiceSize,
            float spacing,
            float betweenGroups,
            float radius,
            float startAngle,
            float arc)
        {
            return value switch
            {
                NovelChoiceArrangement.Horizontal => HorizontalPosition(
                    index, count, perGroup, groupCount, choiceSize,
                    spacing, betweenGroups),
                NovelChoiceArrangement.Circular => CircularPosition(
                    index, count, perGroup, choiceSize,
                    betweenGroups, radius, startAngle, arc),
                _ => VerticalPosition(
                    index, count, perGroup, groupCount, choiceSize,
                    spacing, betweenGroups)
            };
        }

        public static float CalculateFitScale(
            NovelChoiceArrangement value,
            int count,
            int perGroup,
            Vector2 choiceSize,
            float spacing,
            float betweenGroups,
            float radius,
            float startAngle,
            float arc,
            Vector2 panelSize,
            float padding = LayoutPadding)
        {
            if (count <= 0 || panelSize.x <= 0f || panelSize.y <= 0f)
                return 1f;
            Vector2 bounds = CalculateContentSize(
                value, count, perGroup, choiceSize, spacing,
                betweenGroups, radius, startAngle, arc);
            float availableWidth = Mathf.Max(
                1f, panelSize.x - Mathf.Max(0f, padding) * 2f);
            float availableHeight = Mathf.Max(
                1f, panelSize.y - Mathf.Max(0f, padding) * 2f);
            return Mathf.Clamp01(Mathf.Min(
                availableWidth / Mathf.Max(1f, bounds.x),
                availableHeight / Mathf.Max(1f, bounds.y)));
        }

        /// <summary>
        /// Returns the authored bounds of all choice button bodies before the
        /// panel's safety fitting is applied.
        /// </summary>
        public static Vector2 CalculateContentSize(
            NovelChoiceArrangement value,
            int count,
            int perGroup,
            Vector2 choiceSize,
            float spacing,
            float betweenGroups,
            float radius,
            float startAngle,
            float arc)
        {
            if (count <= 0)
                return Vector2.zero;
            perGroup = perGroup <= 0 ? count : Mathf.Max(1, perGroup);
            int groupCount = Mathf.CeilToInt(count / (float)perGroup);
            Vector2 minimum = new Vector2(
                float.PositiveInfinity, float.PositiveInfinity);
            Vector2 maximum = new Vector2(
                float.NegativeInfinity, float.NegativeInfinity);
            Vector2 half = new Vector2(
                Mathf.Max(1f, choiceSize.x),
                Mathf.Max(1f, choiceSize.y)) * 0.5f;
            for (int index = 0; index < count; index++)
            {
                Vector2 position = CalculateChoicePosition(
                    value, index, count, perGroup, groupCount,
                    choiceSize, spacing, betweenGroups, radius,
                    startAngle, arc);
                minimum = Vector2.Min(minimum, position - half);
                maximum = Vector2.Max(maximum, position + half);
            }
            return maximum - minimum;
        }

        private static Vector2 VerticalPosition(
            int index,
            int count,
            int perGroup,
            int groupCount,
            Vector2 choiceSize,
            float spacing,
            float betweenGroups)
        {
            int group = index / perGroup;
            int within = index % perGroup;
            int inThisGroup = Mathf.Min(perGroup, count - group * perGroup);
            float groupStride = choiceSize.x + betweenGroups;
            float itemStride = choiceSize.y + spacing;
            return new Vector2(
                (group - (groupCount - 1) * 0.5f) * groupStride,
                ((inThisGroup - 1) * 0.5f - within) * itemStride);
        }

        private static Vector2 HorizontalPosition(
            int index,
            int count,
            int perGroup,
            int groupCount,
            Vector2 choiceSize,
            float spacing,
            float betweenGroups)
        {
            int group = index / perGroup;
            int within = index % perGroup;
            int inThisGroup = Mathf.Min(perGroup, count - group * perGroup);
            float itemStride = choiceSize.x + spacing;
            float groupStride = choiceSize.y + betweenGroups;
            return new Vector2(
                (within - (inThisGroup - 1) * 0.5f) * itemStride,
                ((groupCount - 1) * 0.5f - group) * groupStride);
        }

        private static Vector2 CircularPosition(
            int index,
            int count,
            int perGroup,
            Vector2 choiceSize,
            float betweenGroups,
            float radius,
            float startAngle,
            float arc)
        {
            int ring = index / perGroup;
            int within = index % perGroup;
            int inThisRing = Mathf.Min(perGroup, count - ring * perGroup);
            float angleStep = inThisRing <= 1
                ? 0f
                : Mathf.Approximately(Mathf.Abs(arc), 360f)
                    ? arc / inThisRing
                    : arc / (inThisRing - 1);
            float angle = (startAngle + angleStep * within) *
                Mathf.Deg2Rad;
            float ringRadius = radius + ring *
                (choiceSize.y + betweenGroups);
            return new Vector2(
                Mathf.Cos(angle) * ringRadius,
                Mathf.Sin(angle) * ringRadius);
        }
    }
}
