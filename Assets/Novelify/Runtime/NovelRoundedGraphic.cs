using NUnit.Framework;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using static System.Net.Mime.MediaTypeNames;

namespace Novelify
{
    [RequireComponent(typeof(CanvasRenderer))]
    public class NovelRoundedGraphic : MaskableGraphic
    {
        [SerializeField] private float cornerRadius = 24f;
        [SerializeField] private bool outlineEnabled;
        [SerializeField] private Color outlineColor = Color.white;
        [SerializeField] private float outlineThickness = 2f;
        [SerializeField, UnityEngine.Range(2, 16)] private int segmentsPerCorner = 8;

        public void Apply(NovelBoxStyle style)
        {
            NovelBoxStyle value = style.Validated();
            color = value.EffectiveFillColor;
            cornerRadius = value.CornerRadius;
            outlineEnabled = value.OutlineEnabled;
            outlineColor = value.OutlineColor;
            outlineThickness = value.OutlineThickness;
            raycastTarget = false;
            SetVerticesDirty();
        }

        protected override void OnRectTransformDimensionsChange()
        {
            base.OnRectTransformDimensionsChange();
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper helper)
        {
            helper.Clear();
            Rect outerRect = GetPixelAdjustedRect();
            if (outerRect.width <= 0f || outerRect.height <= 0f)
                return;

            float radius = Mathf.Min(
                Mathf.Max(0f, cornerRadius),
                Mathf.Min(outerRect.width, outerRect.height) * 0.5f);

            int segments = Mathf.Clamp(segmentsPerCorner, 2, 16);

            List<Vector2> outer = BuildContour(outerRect, radius, segments);

            float thickness = outlineEnabled
                ? Mathf.Min(
                    Mathf.Max(0f, outlineThickness),
                    Mathf.Min(outerRect.width, outerRect.height) * 0.5f)
                : 0f;

            if(thickness < 0.001f)
            {
                AddFan(helper, outer, color);
                return;
            }

            Rect innerRect = new Rect(
                outerRect.xMin + thickness,
                outerRect.yMin + thickness,
                Mathf.Max(0f, outerRect.width - thickness * 2f),
                Mathf.Max(0f, outerRect.height - thickness * 2f));

            float innerRadius = Mathf.Max(0f, radius - thickness);
            List<Vector2> inner = BuildContour(innerRect, innerRadius, segments);

            AddRing(helper, outer, inner, outlineColor);
            AddFan(helper, inner, color);
        }

        private static List<Vector2> BuildContour(
             Rect rect,
             float radius,
             int segments)
        {
            var points = new List<Vector2>((segments + 1) * 4);
            AddArc(points, new Vector2(rect.xMin + radius, rect.yMin + radius),
                radius, 180f, 270f, segments);
            AddArc(points, new Vector2(rect.xMax - radius, rect.yMin + radius),
                radius, 270f, 360f, segments);
            AddArc(points, new Vector2(rect.xMax - radius, rect.yMax - radius),
                radius, 0f, 90f, segments);
            AddArc(points, new Vector2(rect.xMin + radius, rect.yMax - radius),
                radius, 90f, 180f, segments);
            return points;
        }

        private static void AddArc(
            List<Vector2> points,
            Vector2 center,
            float radius,
            float startDegrees,
            float endDegrees,
            int segments)
        {
            for (int index = 0; index <= segments; index++)
            {
                float progress = index / (float)segments;
                float radians = Mathf.Lerp(startDegrees, endDegrees, progress)
                    * Mathf.Deg2Rad;
                points.Add(center + new Vector2(
                    Mathf.Cos(radians), Mathf.Sin(radians)) * radius);
            }
        }

        private static void AddFan(
            VertexHelper helper,
            IReadOnlyList<Vector2> contour,
            Color32 vertexColor)
        {
            if (contour.Count < 3)
                return;

            Vector2 center = Vector2.zero;
            for (int index = 0; index < contour.Count; index++)
                center += contour[index];
            center /= contour.Count;

            int centerIndex = helper.currentVertCount;
            helper.AddVert(center, vertexColor, Vector2.zero);
            for (int index = 0; index < contour.Count; index++)
                helper.AddVert(contour[index], vertexColor, Vector2.zero);

            for (int index = 0; index < contour.Count; index++)
            {
                int next = (index + 1) % contour.Count;
                helper.AddTriangle(
                    centerIndex,
                    centerIndex + 1 + index,
                    centerIndex + 1 + next);
            }
        }

        private static void AddRing(
            VertexHelper helper,
            IReadOnlyList<Vector2> outer,
            IReadOnlyList<Vector2> inner,
            Color32 vertexColor)
        {
            int count = Mathf.Min(outer.Count, inner.Count);
            int first = helper.currentVertCount;
            for (int index = 0; index<count; index++)
            {
                helper.AddVert(outer[index], vertexColor, Vector2.zero);
                helper.AddVert(inner[index], vertexColor, Vector2.zero);
            }

            for (int index = 0; index<count; index++)
            {
                int next = (index + 1) % count;
                int outerA = first + index * 2;
                int innerA = outerA + 1;
                int outerB = first + next * 2;
                int innerB = outerB + 1;
                helper.AddTriangle(outerA, outerB, innerB);
                helper.AddTriangle(outerA, innerB, innerA);
            }
        }
    }

    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class NovelTriangleGraphic : MaskableGraphic
    {
        protected override void OnPopulateMesh(VertexHelper helper)
        {
            helper.Clear();
            Rect rect = GetPixelAdjustedRect();
            Color32 vertexColor = color;
            helper.AddVert(new Vector2(rect.xMin, rect.yMax),
                vertexColor, Vector2.zero);
            helper.AddVert(new Vector2(rect.xMax, rect.yMax),
                vertexColor, Vector2.zero);
            helper.AddVert(new Vector2(rect.center.x, rect.yMin),
                vertexColor, Vector2.zero);
            helper.AddTriangle(0, 1, 2);
        }
    }
}
