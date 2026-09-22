using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

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

        private NovelBoxStyle _style = NovelBoxStyle.DialogueDefault;
        private Material _styleMaterial;

        public void Apply(NovelBoxStyle style)
        {
            NovelBoxStyle value = style.Validated();
            _style = value;
            color = value.EffectiveFillColor;
            cornerRadius = value.CornerRadius;
            outlineEnabled = value.OutlineEnabled;
            outlineColor = value.EffectiveOutlineColor;
            outlineThickness = value.OutlineThickness;
            raycastTarget = false;
            EnsureStyleMaterial();
            UpdateMaterialProperties(GetPixelAdjustedRect());
            SetVerticesDirty();
            SetMaterialDirty();
        }

        protected override void OnRectTransformDimensionsChange()
        {
            base.OnRectTransformDimensionsChange();
            UpdateMaterialProperties(GetPixelAdjustedRect());
            SetVerticesDirty();
            SetMaterialDirty();
        }

        protected override void OnPopulateMesh(VertexHelper helper)
        {
            helper.Clear();
            Rect outerRect = GetPixelAdjustedRect();
            if (outerRect.width <= 0f || outerRect.height <= 0f)
                return;

            if (_styleMaterial != null)
            {
                UpdateMaterialProperties(outerRect);
                AddQuad(helper, outerRect, Color.white);
                return;
            }

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

        private void EnsureStyleMaterial()
        {
            if (_styleMaterial != null)
                return;
            Shader shader = Resources.Load<Shader>("NovelStyledBox");
            if (shader == null)
                shader = Shader.Find("Novelify/UI/Styled Box");
            if (shader == null)
                return;
            _styleMaterial = new Material(shader)
            {
                name = "Novelify Styled Box (Instance)",
                hideFlags = HideFlags.HideAndDontSave
            };
            material = _styleMaterial;
        }

        private void UpdateMaterialProperties(Rect rect)
        {
            if (_styleMaterial == null)
                return;
            NovelBoxStyle style = _style.Validated();
            _styleMaterial.SetTexture(
                "_FillTex", style.FillTexture != null
                    ? style.FillTexture
                    : Texture2D.whiteTexture);
            _styleMaterial.SetColor("_FillColor", style.EffectiveFillColor);
            _styleMaterial.SetVector("_FillTiling", style.FillTiling);
            _styleMaterial.SetVector("_FillOffset", style.FillOffset);
            _styleMaterial.SetTexture(
                "_OutlineTex", style.OutlineTexture != null
                    ? style.OutlineTexture
                    : Texture2D.whiteTexture);
            _styleMaterial.SetColor(
                "_OutlineColor", style.EffectiveOutlineColor);
            _styleMaterial.SetVector("_OutlineTiling", style.OutlineTiling);
            _styleMaterial.SetVector("_OutlineOffset", style.OutlineOffset);
            _styleMaterial.SetVector(
                "_RectSize", new Vector4(rect.width, rect.height, 0f, 0f));
            _styleMaterial.SetFloat("_CornerRadius", style.CornerRadius);
            _styleMaterial.SetFloat(
                "_OutlineThickness", style.OutlineThickness);
            _styleMaterial.SetFloat(
                "_OutlineEnabled", style.OutlineEnabled ? 1f : 0f);
        }

        private static void AddQuad(
            VertexHelper helper,
            Rect rect,
            Color32 vertexColor)
        {
            helper.AddVert(
                new Vector2(rect.xMin, rect.yMin), vertexColor, Vector2.zero);
            helper.AddVert(
                new Vector2(rect.xMin, rect.yMax), vertexColor, Vector2.up);
            helper.AddVert(
                new Vector2(rect.xMax, rect.yMax), vertexColor, Vector2.one);
            helper.AddVert(
                new Vector2(rect.xMax, rect.yMin), vertexColor, Vector2.right);
            helper.AddTriangle(0, 1, 2);
            helper.AddTriangle(0, 2, 3);
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
        private bool _usesCustomPoints;
        private Vector2 _baseA;
        private Vector2 _baseB;
        private Vector2 _tip;
        private Texture2D _texture;
        private Vector2 _tiling = Vector2.one;
        private Vector2 _offset;

        public override Texture mainTexture =>
            _texture != null ? _texture : Texture2D.whiteTexture;

        public void ApplyAppearance(
            Texture2D texture,
            Color tint,
            Vector2 tiling,
            Vector2 offset)
        {
            _texture = texture;
            color = tint;
            _tiling = tiling;
            _offset = offset;
            SetMaterialDirty();
            SetVerticesDirty();
        }

        public void SetPoints(Vector2 baseA, Vector2 baseB, Vector2 tip)
        {
            _usesCustomPoints = true;
            _baseA = baseA;
            _baseB = baseB;
            _tip = tip;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper helper)
        {
            helper.Clear();
            Color32 vertexColor = color;
            Rect rect = GetPixelAdjustedRect();
            if (_usesCustomPoints)
            {
                helper.AddVert(_baseA, vertexColor, TextureUv(_baseA, rect));
                helper.AddVert(_baseB, vertexColor, TextureUv(_baseB, rect));
                helper.AddVert(_tip, vertexColor, TextureUv(_tip, rect));
                helper.AddTriangle(0, 1, 2);
                return;
            }

            Vector2 left = new Vector2(rect.xMin, rect.yMax);
            Vector2 right = new Vector2(rect.xMax, rect.yMax);
            Vector2 tip = new Vector2(rect.center.x, rect.yMin);
            helper.AddVert(left, vertexColor, TextureUv(left, rect));
            helper.AddVert(right, vertexColor, TextureUv(right, rect));
            helper.AddVert(tip, vertexColor, TextureUv(tip, rect));
            helper.AddTriangle(0, 1, 2);
        }

        private Vector2 TextureUv(Vector2 point, Rect rect)
        {
            Vector2 normalized = new Vector2(
                rect.width > 0f ? (point.x - rect.xMin) / rect.width : 0f,
                rect.height > 0f ? (point.y - rect.yMin) / rect.height : 0f);
            return Vector2.Scale(normalized, _tiling) + _offset;
        }
    }
}
