Shader "BreathCasino/CardTriplanarNumber"
{
    Properties
    {
        _BaseColor("Base Color", Color) = (0.55, 0.55, 0.55, 1)
        _NumberTex("Number Texture", 2D) = "white" {}
        _NumberColor("Number Color", Color) = (0.98, 0.98, 0.98, 1)
        _Tile("Triplanar Tile", Float) = 1.0
        _Blend("Triplanar Blend", Float) = 4.0
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
        }

        Pass
        {
            Name "UniversalForward"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 positionOS : TEXCOORD0;
                float3 normalOS : TEXCOORD1;
            };

            TEXTURE2D(_NumberTex);
            SAMPLER(sampler_NumberTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float4 _NumberColor;
                float _Tile;
                float _Blend;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs posInputs = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionHCS = posInputs.positionCS;
                output.positionOS = input.positionOS.xyz;
                output.normalOS = input.normalOS;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float3 pos = input.positionOS * _Tile;
                float3 n = normalize(abs(input.normalOS));
                n = pow(n, _Blend);
                n /= (n.x + n.y + n.z);

                float4 sampleX = SAMPLE_TEXTURE2D(_NumberTex, sampler_NumberTex, pos.zy);
                float4 sampleY = SAMPLE_TEXTURE2D(_NumberTex, sampler_NumberTex, pos.xz);
                float4 sampleZ = SAMPLE_TEXTURE2D(_NumberTex, sampler_NumberTex, pos.xy);

                float4 numberSample = sampleX * n.x + sampleY * n.y + sampleZ * n.z;
                half mask = numberSample.a * _NumberColor.a;
                half3 finalColor = lerp(_BaseColor.rgb, _NumberColor.rgb * numberSample.rgb, mask);
                return half4(finalColor, 1.0h);
            }
            ENDHLSL
        }
    }
}
