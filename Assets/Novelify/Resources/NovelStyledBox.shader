Shader "Novelify/UI/Styled Box"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _FillTex ("Fill Texture", 2D) = "white" {}
        _OutlineTex ("Outline Texture", 2D) = "white" {}
        _FillColor ("Fill Tint", Color) = (1,1,1,1)
        _OutlineColor ("Outline Tint", Color) = (1,1,1,1)
        _FillTiling ("Fill Tiling", Vector) = (1,1,0,0)
        _FillOffset ("Fill Offset", Vector) = (0,0,0,0)
        _OutlineTiling ("Outline Tiling", Vector) = (1,1,0,0)
        _OutlineOffset ("Outline Offset", Vector) = (0,0,0,0)
        _RectSize ("Rect Size", Vector) = (100,100,0,0)
        _CornerRadius ("Corner Radius", Float) = 0
        _OutlineThickness ("Outline Thickness", Float) = 0
        _OutlineEnabled ("Outline Enabled", Float) = 0

        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="False"
        }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            Name "StyledBox"
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

            struct appdata_t
            {
                float4 vertex : POSITION;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
            };

            sampler2D _FillTex;
            sampler2D _OutlineTex;
            fixed4 _FillColor;
            fixed4 _OutlineColor;
            float4 _FillTiling;
            float4 _FillOffset;
            float4 _OutlineTiling;
            float4 _OutlineOffset;
            float4 _RectSize;
            float _CornerRadius;
            float _OutlineThickness;
            float _OutlineEnabled;
            float4 _ClipRect;

            v2f vert(appdata_t input)
            {
                v2f output;
                output.worldPosition = input.vertex;
                output.vertex = UnityObjectToClipPos(input.vertex);
                output.uv = input.texcoord;
                return output;
            }

            fixed4 frag(v2f input) : SV_Target
            {
                float2 size = max(_RectSize.xy, float2(1.0, 1.0));
                float radius = min(max(_CornerRadius, 0.0), min(size.x, size.y) * 0.5);
                float2 samplePoint = (input.uv - 0.5) * size;
                float2 distanceToCore = abs(samplePoint) -
                    (size * 0.5 - radius);
                float distanceToEdge = length(max(distanceToCore, 0.0)) +
                    min(max(distanceToCore.x, distanceToCore.y), 0.0) - radius;
                float antialias = max(fwidth(distanceToEdge), 0.75);
                float outerCoverage = saturate(0.5 - distanceToEdge / antialias);
                float thickness = _OutlineEnabled > 0.5
                    ? max(_OutlineThickness, 0.0)
                    : 0.0;
                float fillCoverage = saturate(0.5 -
                    (distanceToEdge + thickness) / antialias);
                float outlineCoverage = max(0.0, outerCoverage - fillCoverage);

                float2 fillUv = frac(input.uv * _FillTiling.xy + _FillOffset.xy);
                float2 outlineUv = frac(input.uv * _OutlineTiling.xy + _OutlineOffset.xy);
                fixed4 fill = tex2D(_FillTex, fillUv) * _FillColor;
                fixed4 outline = tex2D(_OutlineTex, outlineUv) * _OutlineColor;
                float fillAlpha = fill.a * fillCoverage;
                float outlineAlpha = outline.a * outlineCoverage;
                float combinedAlpha = saturate(fillAlpha + outlineAlpha);
                fixed3 premultiplied =
                    fill.rgb * fillAlpha + outline.rgb * outlineAlpha;
                fixed4 color = fixed4(
                    premultiplied / max(combinedAlpha, 0.0001),
                    combinedAlpha);

                #ifdef UNITY_UI_CLIP_RECT
                color.a *= UnityGet2DClipping(input.worldPosition.xy, _ClipRect);
                #endif
                #ifdef UNITY_UI_ALPHACLIP
                clip(color.a - 0.001);
                #endif
                return color;
            }
            ENDCG
        }
    }
}
