Shader "Interstellar/Menu/Overlay Surface"
{
    Properties
    {
        [MainTexture] _MainTex("Texture", 2D) = "white" {}
        [MainColor] _Color("Tint", Color) = (1, 1, 1, 1)
        [Enum(Color,0,Alpha,1,Luminance,2)] _BrandMask("Brand Mask", Float) = 0
        _MaskBackground("Mask Background", Float) = 0.08627451
        _MaskWhite("Mask White", Float) = 0.98431373
        [Toggle] _Monochrome("Monochrome", Float) = 0
        _ToneExponent("Tone Exponent", Float) = 1
        _ToneWhitePoint("Tone White Point", Float) = 0.9
        _BevelStrength("Bevel Highlight", Float) = 0
        _LetteringShift("Lettering Shift", Vector) = (0, 0, 0, 0)
        _LetteringRect("Lettering Rect", Vector) = (0, 0, 0, 0)
        [HideInInspector] _SrcBlend("Source Blend", Float) = 5
        [HideInInspector] _DstBlend("Destination Blend", Float) = 10
        [HideInInspector] _ZWrite("Depth Write", Float) = 0
        [Enum(UnityEngine.Rendering.CompareFunction)] _ZTest("Depth Test", Float) = 4
        [HideInInspector] _Surface("Surface", Float) = 1
        [HideInInspector] _Blend("Blend", Float) = 0
        [Enum(UnityEngine.Rendering.CullMode)] _Cull("Cull", Float) = 2
        _StencilComp("Stencil Comparison", Float) = 8
        _Stencil("Stencil ID", Float) = 0
        _StencilOp("Stencil Operation", Float) = 0
        _StencilWriteMask("Stencil Write Mask", Float) = 255
        _StencilReadMask("Stencil Read Mask", Float) = 255
        _ColorMask("Color Mask", Float) = 15
    }

    SubShader
    {
        Tags { "Queue" = "Transparent-10" "RenderType" = "Transparent" "IgnoreProjector" = "True" }

        Pass
        {
            Tags { "LightMode" = "SRPDefaultUnlit" }
            Blend [_SrcBlend] [_DstBlend]
            ZTest [_ZTest]
            ZWrite [_ZWrite]
            Cull [_Cull]
            ColorMask [_ColorMask]

            Stencil
            {
                Ref [_Stencil]
                Comp [_StencilComp]
                Pass [_StencilOp]
                ReadMask [_StencilReadMask]
                WriteMask [_StencilWriteMask]
            }

            CGPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile _ STEREO_INSTANCING_ON STEREO_MULTIVIEW_ON
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _Color;
            float _BrandMask;
            float _MaskBackground;
            float _MaskWhite;
            float _Monochrome;
            float _ToneExponent;
            float _ToneWhitePoint;
            float _BevelStrength;
            float4 _LetteringShift;
            float4 _LetteringRect;

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR;
                float3 normal : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            v2f vert(appdata input)
            {
                v2f output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.vertex = UnityObjectToClipPos(input.vertex);
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                output.color = input.color * _Color;
                output.normal = input.normal;
                return output;
            }

            fixed4 frag(v2f input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.uv;
                if (all(uv >= _LetteringRect.xy) && all(uv <= _LetteringRect.zw))
                    uv -= _LetteringShift.xy;
                fixed4 sampled = tex2D(_MainTex, uv);
                if (_BrandMask > 0.5)
                {
                    float coverage = sampled.a;
                    if (_BrandMask > 1.5)
                    {


                        float3 sourceColor = sampled.rgb;
                        #ifndef UNITY_COLORSPACE_GAMMA
                            sourceColor = LinearToGammaSpace(sourceColor);
                        #endif
                        float luminance = dot(sourceColor, float3(0.2126, 0.7152, 0.0722));
                        coverage *= saturate((luminance - _MaskBackground)
                            / max(0.0001, _MaskWhite - _MaskBackground));
                    }


                    float alpha = input.color.a * coverage;
                    clip(alpha - 0.001);
                    return fixed4(input.color.rgb, alpha);
                }
                if (_Monochrome > 0.5)
                {
                    float luminance = dot(sampled.rgb, float3(0.2126, 0.7152, 0.0722));
                    float whitePoint = max(0.0001, _ToneWhitePoint);
                    float tone = saturate(pow(max(0.0, luminance / whitePoint),
                        max(0.0001, _ToneExponent)) * whitePoint);
                    sampled.rgb = tone.xxx;
                }
                float4 surface = sampled * input.color;
                if (_BevelStrength > 0.0)
                {
                    float3 normal = normalize(input.normal);
                    float bevel = pow(saturate(1.0 - abs(normal.x)), 0.8);
                    float light = saturate(dot(normal, normalize(float3(0.12, -0.62, 0.77))));
                    surface.rgb += (_BevelStrength * bevel * light).xxx;
                }
                clip(surface.a - 0.001);
                return surface;
            }
            ENDCG
        }
    }
}
