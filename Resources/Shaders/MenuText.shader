Shader "Interstellar/Menu/Text"
{
    Properties
    {
        _FaceColor("Face Color", Color) = (1, 1, 1, 1)
        _FaceDilate("Face Dilate", Range(-1, 1)) = 0
        _TextBackgroundColor("Text Background Color", Color) = (0, 0, 0, 0)
        _TextReferenceColors("Reference Foreground and Background", Vector) = (0.89411765, 0.001517635, 0, 0)
        _OutlineColor("Outline Color", Color) = (0, 0, 0, 1)
        _OutlineWidth("Outline Thickness", Range(0, 1)) = 0
        _OutlineSoftness("Outline Softness", Range(0, 1)) = 0
        _WeightNormal("Weight Normal", Float) = 0
        _WeightBold("Weight Bold", Float) = 0.5
        _ShaderFlags("Flags", Float) = 0
        _ScaleRatioA("Scale Ratio A", Float) = 1
        _ScaleRatioB("Scale Ratio B", Float) = 1
        _ScaleRatioC("Scale Ratio C", Float) = 1
        _MainTex("Font Atlas", 2D) = "white" {}
        _TextureWidth("Texture Width", Float) = 512
        _TextureHeight("Texture Height", Float) = 512
        _GradientScale("Gradient Scale", Float) = 5
        _ScaleX("Scale X", Float) = 1
        _ScaleY("Scale Y", Float) = 1
        _PerspectiveFilter("Perspective Correction", Range(0, 1)) = 0.875
        _Sharpness("Sharpness", Range(-1, 1)) = 0
        _VertexOffsetX("Vertex Offset X", Float) = 0
        _VertexOffsetY("Vertex Offset Y", Float) = 0
        _ClipRect("Clip Rect", Vector) = (-32767, -32767, 32767, 32767)
        _MaskSoftnessX("Mask Softness X", Float) = 0
        _MaskSoftnessY("Mask Softness Y", Float) = 0
        _StencilComp("Stencil Comparison", Float) = 8
        _Stencil("Stencil ID", Float) = 0
        _StencilOp("Stencil Operation", Float) = 0
        _StencilWriteMask("Stencil Write Mask", Float) = 255
        _StencilReadMask("Stencil Read Mask", Float) = 255
        _CullMode("Cull Mode", Float) = 0
        _ColorMask("Color Mask", Float) = 15
        [Enum(UnityEngine.Rendering.CompareFunction)] _ZTest("Depth Test", Float) = 4
    }

    SubShader
    {
        Tags { "Queue" = "Transparent-6" "IgnoreProjector" = "True" "RenderType" = "Transparent" }
        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }
        Cull [_CullMode]
        ZWrite Off
        Lighting Off
        Fog { Mode Off }
        ZTest [_ZTest]
        Blend One OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            Tags { "LightMode" = "SRPDefaultUnlit" }
            CGPROGRAM
            #pragma target 3.0
            #pragma vertex VertShader
            #pragma fragment PixShader
            #pragma multi_compile_instancing
            #pragma multi_compile _ STEREO_INSTANCING_ON STEREO_MULTIVIEW_ON
            #pragma multi_compile _ UNITY_UI_CLIP_RECT
            #pragma multi_compile _ UNITY_UI_ALPHACLIP
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            sampler2D _MainTex;
            float4 _FaceColor;
            float4 _TextBackgroundColor;
            float4 _TextReferenceColors;
            float _FaceDilate;
            float _OutlineSoftness;
            float _WeightNormal;
            float _WeightBold;
            float _ScaleRatioA;
            float _GradientScale;
            float _ScaleX;
            float _ScaleY;
            float _PerspectiveFilter;
            float _Sharpness;
            float _VertexOffsetX;
            float _VertexOffsetY;
            float4 _ClipRect;
            float _MaskSoftnessX;
            float _MaskSoftnessY;
            float _UIMaskSoftnessX;
            float _UIMaskSoftnessY;
            int _UIVertexColorAlwaysGammaSpace;

            struct vertex_t
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float4 color : COLOR;
                float4 texcoord0 : TEXCOORD0;
                float2 texcoord1 : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct pixel_t
            {
                float4 vertex : SV_POSITION;
                float4 faceColor : COLOR;
                float2 texcoord0 : TEXCOORD0;
                float2 param : TEXCOORD1;
                float4 mask : TEXCOORD2;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            float3 ToLinear(float3 value)
            {
                return lerp(value / 12.92, pow((value + 0.055) / 1.055, 2.4), step(0.04045, value));
            }

            float3 ToGamma(float3 value)
            {
                value = max(value, 0.0);
                return lerp(value * 12.92, 1.055 * pow(value, 1.0 / 2.4) - 0.055, step(0.0031308, value));
            }

            pixel_t VertShader(vertex_t input)
            {
                pixel_t output;
                UNITY_INITIALIZE_OUTPUT(pixel_t, output);
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                float signedScale = abs(input.texcoord0.w) > 0.000001 ? input.texcoord0.w : input.texcoord1.y;
                float bold = step(signedScale, 0.0);
                float4 vert = input.vertex;
                vert.xy += float2(_VertexOffsetX, _VertexOffsetY);
                float4 vPosition = UnityObjectToClipPos(vert);
                float2 pixelSize = vPosition.w;
                pixelSize /= float2(_ScaleX, _ScaleY) * abs(mul((float2x2)UNITY_MATRIX_P, _ScreenParams.xy));
                float scale = rsqrt(dot(pixelSize, pixelSize));
                scale *= abs(signedScale) * _GradientScale * (_Sharpness + 1.0);
                if (UNITY_MATRIX_P[3][3] == 0)
                    scale = lerp(abs(scale) * (1.0 - _PerspectiveFilter), scale,
                        abs(dot(UnityObjectToWorldNormal(input.normal), normalize(WorldSpaceViewDir(vert)))));
                float weight = lerp(_WeightNormal, _WeightBold, bold) / 4.0;
                weight = (weight + _FaceDilate) * _ScaleRatioA * 0.5;
                scale /= 1.0 + _OutlineSoftness * _ScaleRatioA * scale;
                float bias = (0.5 - weight) * scale - 0.5;

                if (_UIVertexColorAlwaysGammaSpace && !IsGammaSpace())
                    input.color.rgb = UIGammaToLinear(input.color.rgb);
                float4 clampedRect = clamp(_ClipRect, -2e10, 2e10);
                float2 maskSoftness = max(float2(_UIMaskSoftnessX, _UIMaskSoftnessY),
                    float2(_MaskSoftnessX, _MaskSoftnessY));
                output.vertex = vPosition;
                output.faceColor = input.color * _FaceColor;
                output.texcoord0 = input.texcoord0.xy;
                output.param = float2(scale, bias);
                output.mask = float4(vert.xy * 2.0 - clampedRect.xy - clampedRect.zw,
                    0.25 / (0.25 * maskSoftness + pixelSize));
                return output;
            }

            float4 PixShader(pixel_t input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float coverage = saturate(tex2D(_MainTex, input.texcoord0).a * input.param.x - input.param.y);
                float4 color = float4(input.faceColor.rgb * coverage, coverage);
                #ifndef UNITY_COLORSPACE_GAMMA
                if (_TextBackgroundColor.a > 0.0)
                {
                    float3 background = _TextBackgroundColor.rgb;
                    float3 reference = float3(_TextReferenceColors.x, _TextReferenceColors.y, 0.0);
                    float3 referenceGamma = ToGamma(reference);
                    float blendedReference = ToGamma(lerp(reference.y, reference.x, coverage).xxx).x;
                    float visibleCoverage = saturate((blendedReference - referenceGamma.y)
                        / max(referenceGamma.x - referenceGamma.y, 0.000001));
                    float3 target = ToLinear(lerp(ToGamma(background), ToGamma(input.faceColor.rgb), visibleCoverage));
                    float3 requiredAlpha = 1.0 - target / max(background, 0.000001);
                    float alpha = saturate(max(coverage, max(requiredAlpha.r, max(requiredAlpha.g, requiredAlpha.b))));
                    color = float4(max(target - background * (1.0 - alpha), 0.0), alpha);
                }
                #endif
                color *= input.faceColor.a;
                #ifdef UNITY_UI_CLIP_RECT
                float2 mask = saturate((_ClipRect.zw - _ClipRect.xy - abs(input.mask.xy)) * input.mask.zw);
                color *= mask.x * mask.y;
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
