Shader "BreathCasino/CellShade"
{
    Properties
    {
        _BaseColor ("Base Color", Color) = (1, 1, 1, 1)
        _BaseMap ("Base Texture", 2D) = "white" {}

        [Header(Toon Shading)]
        _ShadowColor ("Shadow Color", Color) = (0.3, 0.3, 0.4, 1)
        _ShadowThreshold ("Shadow Threshold", Range(0, 1)) = 0.5
        _ShadowSmoothness ("Shadow Smoothness", Range(0.001, 1)) = 0.05

        [Header(Specular)]
        _SpecularColor ("Specular Color", Color) = (1, 1, 1, 1)
        _SpecularThreshold ("Specular Threshold", Range(0, 1)) = 0.9
        _SpecularSmoothness ("Specular Smoothness", Range(0.001, 1)) = 0.05
        _Glossiness ("Glossiness", Range(1, 500)) = 32

        [Header(Rim Light)]
        _RimColor ("Rim Color", Color) = (1, 1, 1, 1)
        _RimAmount ("Rim Amount", Range(0, 1)) = 0.7
        _RimThreshold ("Rim Threshold", Range(0, 1)) = 0.1

        [Header(Outline)]
        _OutlineColor ("Outline Color", Color) = (0, 0, 0, 1)
        _OutlineWidth ("Outline Width", Range(0, 0.1)) = 0.005
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Geometry"
        }

        // Outline Pass
        Pass
        {
            Name "Outline"
            Tags { "LightMode" = "SRPDefaultUnlit" }

            Cull Front

            HLSLPROGRAM
            #pragma vertex OutlineVert
            #pragma fragment OutlineFrag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _OutlineColor;
                float _OutlineWidth;
            CBUFFER_END

            Varyings OutlineVert(Attributes input)
            {
                Varyings output;

                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                positionWS += normalWS * _OutlineWidth;

                output.positionCS = TransformWorldToHClip(positionWS);
                return output;
            }

            half4 OutlineFrag(Varyings input) : SV_Target
            {
                return _OutlineColor;
            }
            ENDHLSL
        }

        // Main Toon Shading Pass
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex ToonVert
            #pragma fragment ToonFrag

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _SHADOWS_SOFT
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 normalWS : TEXCOORD2;
                float3 viewDirWS : TEXCOORD3;
            };

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _BaseColor;
                float4 _ShadowColor;
                float _ShadowThreshold;
                float _ShadowSmoothness;
                float4 _SpecularColor;
                float _SpecularThreshold;
                float _SpecularSmoothness;
                float _Glossiness;
                float4 _RimColor;
                float _RimAmount;
                float _RimThreshold;
            CBUFFER_END

            Varyings ToonVert(Attributes input)
            {
                Varyings output;

                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInputs = GetVertexNormalInputs(input.normalOS);

                output.positionCS = positionInputs.positionCS;
                output.positionWS = positionInputs.positionWS;
                output.normalWS = normalInputs.normalWS;
                output.viewDirWS = GetWorldSpaceViewDir(positionInputs.positionWS);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);

                return output;
            }

            half4 ToonFrag(Varyings input) : SV_Target
            {
                // Sample texture
                half4 baseMap = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
                half4 baseColor = baseMap * _BaseColor;

                // Normalize vectors
                float3 normalWS = normalize(input.normalWS);
                float3 viewDirWS = normalize(input.viewDirWS);

                // Get main light
                Light mainLight = GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                float3 lightDir = normalize(mainLight.direction);

                // Diffuse lighting (N dot L)
                float NdotL = dot(normalWS, lightDir);

                // Toon shading - stepped lighting
                float lightIntensity = smoothstep(_ShadowThreshold - _ShadowSmoothness,
                                                   _ShadowThreshold + _ShadowSmoothness,
                                                   NdotL);

                // Apply shadow attenuation
                lightIntensity *= mainLight.shadowAttenuation;

                // Mix base color with shadow color
                half3 diffuse = lerp(_ShadowColor.rgb, baseColor.rgb, lightIntensity);
                diffuse *= mainLight.color;

                // Specular (Blinn-Phong)
                float3 halfVector = normalize(lightDir + viewDirWS);
                float NdotH = dot(normalWS, halfVector);
                float specularIntensity = pow(max(NdotH, 0.0), _Glossiness);

                // Toon specular - stepped
                float specularToon = smoothstep(_SpecularThreshold - _SpecularSmoothness,
                                                _SpecularThreshold + _SpecularSmoothness,
                                                specularIntensity);
                specularToon *= lightIntensity; // Only show specular in lit areas

                half3 specular = specularToon * _SpecularColor.rgb * mainLight.color;

                // Additional lights
                #ifdef _ADDITIONAL_LIGHTS
                uint pixelLightCount = GetAdditionalLightsCount();
                for (uint lightIndex = 0u; lightIndex < pixelLightCount; ++lightIndex)
                {
                    Light light = GetAdditionalLight(lightIndex, input.positionWS);

                    // Toon shading for additional light
                    float3 addLightDir = normalize(light.direction);
                    float addNdotL = dot(normalWS, addLightDir);

                    float addLightIntensity = smoothstep(_ShadowThreshold - _ShadowSmoothness,
                                                         _ShadowThreshold + _ShadowSmoothness,
                                                         addNdotL);
                    addLightIntensity *= light.distanceAttenuation * light.shadowAttenuation;

                    // Add diffuse from this light
                    diffuse += baseColor.rgb * addLightIntensity * light.color;

                    // Add specular from this light
                    float3 addHalfVector = normalize(addLightDir + viewDirWS);
                    float addNdotH = dot(normalWS, addHalfVector);
                    float addSpecularIntensity = pow(max(addNdotH, 0.0), _Glossiness);
                    float addSpecularToon = smoothstep(_SpecularThreshold - _SpecularSmoothness,
                                                       _SpecularThreshold + _SpecularSmoothness,
                                                       addSpecularIntensity);
                    addSpecularToon *= addLightIntensity;
                    specular += addSpecularToon * _SpecularColor.rgb * light.color;
                }
                #endif

                // Rim lighting
                float rimDot = 1.0 - dot(viewDirWS, normalWS);
                float rimIntensity = rimDot * pow(NdotL, _RimThreshold);
                rimIntensity = smoothstep(_RimAmount - 0.01, _RimAmount + 0.01, rimIntensity);

                half3 rim = rimIntensity * _RimColor.rgb;

                // Combine all lighting
                half3 finalColor = diffuse + specular + rim;

                // Add ambient
                finalColor += baseColor.rgb * 0.1;

                return half4(finalColor, baseColor.a);
            }
            ENDHLSL
        }

        // Shadow caster pass
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            float3 _LightDirection;

            Varyings ShadowPassVertex(Attributes input)
            {
                Varyings output;

                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInputs = GetVertexNormalInputs(input.normalOS);

                output.positionCS = TransformWorldToHClip(ApplyShadowBias(positionInputs.positionWS, normalInputs.normalWS, _LightDirection));

                return output;
            }

            half4 ShadowPassFragment(Varyings input) : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
