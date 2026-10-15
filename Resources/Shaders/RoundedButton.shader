Shader "Interstellar/Menu/Rounded Button"
{
    Properties
    {
        [MainColor] _Color("Fill", Color) = (0, 0, 0, 1)
        [MainTexture] _MainTex("Fill Texture", 2D) = "white" {}
        [HideInInspector] _SrcBlend("Source Blend", Float) = 5
        [HideInInspector] _DstBlend("Destination Blend", Float) = 10
        [HideInInspector] _ZWrite("Depth Write", Float) = 0
        [Enum(UnityEngine.Rendering.CompareFunction)] _ZTest("Depth Test", Float) = 4
        [HideInInspector] _Surface("Surface", Float) = 1
        [HideInInspector] _Blend("Blend", Float) = 0
        _BorderColor("Border", Color) = (0.08, 0.08, 0.08, 1)
        _RectSize("Rectangle Width and Height", Vector) = (1, 0.1, 0, 0)
        _Radius("Corner Radius", Float) = 0.02
        _BorderWidth("Border Width", Float) = 0.001
        _Padding("Quad Padding", Float) = 0.005
        _GlowWidth("Edge Glow Width", Float) = 0
        _GlowOpacity("Edge Glow Opacity", Float) = 0
        _RimStrength("Inner Rim Highlight", Float) = 0
    }

    SubShader
    {
        Tags { "Queue" = "Transparent-8" "RenderType" = "Transparent" "IgnoreProjector" = "True" }

        Pass
        {
            Tags { "LightMode" = "SRPDefaultUnlit" }
            Blend [_SrcBlend] [_DstBlend]
            ZTest [_ZTest]
            ZWrite [_ZWrite]
            Cull Back

            CGPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile _ STEREO_INSTANCING_ON STEREO_MULTIVIEW_ON
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float4 _Color;
            float4 _BorderColor;
            float4 _RectSize;
            float _Radius;
            float _BorderWidth;
            float _Padding;
            float _GlowWidth;
            float _GlowOpacity;
            float _RimStrength;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            v2f vert(appdata input)
            {
                v2f output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.vertex = UnityObjectToClipPos(input.vertex);
                output.uv = input.uv;
                output.color = input.color;
                return output;
            }

            float4 frag(v2f input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float2 size = max(_RectSize.xy, float2(0.000001, 0.000001));
                float radius = clamp(_Radius, 0.0, min(size.x, size.y) * 0.5);
                float2 p = (input.uv - 0.5) * (size + max(_Padding, 0.0) * 2.0);
                float2 q = abs(p) - (size * 0.5 - radius);
                float distance = length(max(q, 0.0)) + min(max(q.x, q.y), 0.0) - radius;


                float edgeWidth = max(fwidth(distance), 0.000001);
                float outerCoverage = saturate(0.5 - distance / edgeWidth);
                float fillCoverage = saturate(0.5 - (distance + max(_BorderWidth, 0.0)) / edgeWidth);
                float borderCoverage = max(outerCoverage - fillCoverage, 0.0);


                float2 fillUV = p / size + 0.5;
                float4 fill = tex2D(_MainTex, fillUV * _MainTex_ST.xy + _MainTex_ST.zw) * _Color * input.color;
                float glowWidth = min(max(_GlowWidth, 0.0), max(_Padding - edgeWidth, 0.0));
                float innerRim = 1.0 - smoothstep(0.0, max(glowWidth, 0.000001),
                    max(-distance - _BorderWidth, 0.0));
                float rimLight = lerp(0.35, 1.0, saturate(fillUV.x * 0.4 + fillUV.y * 0.6));
                fill.rgb = saturate(fill.rgb + (_RimStrength * innerRim * rimLight).xxx);


                float fillAlpha = fill.a * fillCoverage;
                float borderAlpha = _BorderColor.a * _Color.a * input.color.a * borderCoverage;
                float halo = 1.0 - smoothstep(0.0, max(glowWidth, 0.000001), max(distance, 0.0));
                float glowAlpha = glowWidth > 0.0 ? halo * halo * _GlowOpacity
                    * _Color.a * input.color.a * (1.0 - outerCoverage) : 0.0;
                float alpha = fillAlpha + borderAlpha + glowAlpha;
                clip(alpha - 0.001);
                float3 color = (fill.rgb * fillAlpha + _BorderColor.rgb * input.color.rgb * borderAlpha
                    + input.color.rgb * glowAlpha) / max(alpha, 0.000001);
                return float4(color, alpha);
            }
            ENDCG
        }
    }
}
