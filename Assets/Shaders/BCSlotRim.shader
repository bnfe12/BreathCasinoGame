Shader "BreathCasino/BCSlotRim"
{
    Properties
    {
        _BaseColor("Center Color", Color) = (0, 0, 0, 0)
        _FrameColor("Frame Color", Color) = (0.82, 0.97, 1.0, 0.95)
        _GlowColor("Glow Color", Color) = (0.23, 0.84, 1.0, 0.72)
        _BorderThickness("Border Thickness", Range(0.01, 0.35)) = 0.08
        _Feather("Feather", Range(0.001, 0.2)) = 0.035
        _GlowWidth("Glow Width", Range(0.01, 0.4)) = 0.16
        _GlowIntensity("Glow Intensity", Range(0, 3)) = 1.15
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
        }

        Pass
        {
            Name "UniversalForward"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float4 _FrameColor;
                float4 _GlowColor;
                float _BorderThickness;
                float _Feather;
                float _GlowWidth;
                float _GlowIntensity;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionHCS = positionInputs.positionCS;
                output.uv = input.uv;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float2 centeredUv = abs(input.uv - 0.5f) * 2.0f;
                float edgeDistance = 1.0f - max(centeredUv.x, centeredUv.y);

                float frameMask = 1.0f - smoothstep(_BorderThickness, _BorderThickness + _Feather, edgeDistance);
                float glowMask = 1.0f - smoothstep(_BorderThickness + _GlowWidth, _BorderThickness + _GlowWidth + _Feather, edgeDistance);
                glowMask = saturate(glowMask - frameMask * 0.35f);

                half3 color = _BaseColor.rgb;
                color += _GlowColor.rgb * glowMask * _GlowIntensity;
                color += _FrameColor.rgb * frameMask;

                half alpha = saturate(_BaseColor.a + (glowMask * _GlowColor.a * _GlowIntensity) + (frameMask * _FrameColor.a));
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }
}
