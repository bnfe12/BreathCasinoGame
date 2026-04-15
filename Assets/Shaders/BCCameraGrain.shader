// Assets/Shaders/BlockoutCameraGrain.shader
// Full-screen post-process: film grain + vignette + color grade (dark noir).
// Применяется ТОЛЬКО через ScriptableRendererFeature — не назначать на объекты.
// Работает в URP 14+ (Unity 6 совместим).

Shader "BreathCasino/CameraGrain"
{
    Properties
    {
        // Заполняются из C# через Material.SetXxx — не редактировать вручную
        [HideInInspector] _MainTex ("Source", 2D) = "white" {}
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }
        Cull Off   // рисуем full-screen quad
        ZWrite Off
        ZTest Always

        Pass
        {
            Name "BlockoutCameraGrain"

            HLSLPROGRAM
            #pragma vertex   Vert
            #pragma fragment Frag

            // URP blit utilities (определяет Varyings, Vert, _BlitTexture)
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            // ── Параметры (устанавливаются из C#) ───────────────────
            CBUFFER_START(UnityPerMaterial)
                // Grain
                float _GrainStrength;    // 0..0.5   рекомендуется 0.12..0.22
                float _GrainSize;        // 0.5..4   размер зерна в пикселях
                float _GrainSpeed;       // 0.5..8   скорость смены зерна

                // Vignette
                float _VignetteStrength; // 0..1     интенсивность затемнения краёв
                float _VignetteRadius;   // 0..1     радиус начала виньетки (0.4–0.5)
                float _VignetteSoftness; // 0..1     мягкость перехода виньетки

                // Color grade
                float  _Exposure;        // -2..0    засветка (< 0 = темнее)
                float  _Contrast;        // 0..2     контраст (> 1 = больше)
                float  _Saturation;      // 0..1     насыщенность (< 1 = монохром)
                float4 _ShadowTint;      // RGB тонирование теней (нуар = синий/зелёный)

                // Runtime FX
                float  _BlurStrength;    // 0..1     интенсивность blur
                float  _ChannelSplit;    // 0..0.03  хроматический разъезд
                float4 _PulseTint;       // RGB tint, A = strength

                // Object fringe
                float  _OutlineStrength;       // 0..1      сила контурного затемнения
                float  _OutlineThickness;      // 0.5..4    толщина depth-outline
                float  _OutlineDepthThreshold; // 0.0001..0.02 чувствительность к depth-разнице
                float  _EdgeFringeStrength;    // 0..0.02   постоянный chromatic fringe по краям
                float4 _OutlineColor;          // RGB цвет контура
                float  _GlareStrength;         // 0..2      белые блики и световые пятна
                float  _VeilStrength;          // 0..1      молочный haze поверх кадра
                float  _GlareThreshold;        // 0.2..0.95 порог яркости для бликов
                float  _OutlineFogFadeStart;   // 0..1      начало затухания контура в тумане/дальности
                float  _OutlineFogFadeEnd;     // 0..1      полное затухание контура в тумане/дальности
                float  _OutlineDistanceThinness; // 0..1    насколько линия сужается вдали
                float  _FlatShadeBlend;        // 0..1      ступенчатое освещение / posterize
                float  _FlatShadeSteps;        // 2..8      число световых ступеней
                float  _ShadowCrush;           // 0..1      усиление плотных теней
            CBUFFER_END

            // ── Утилиты ─────────────────────────────────────────────

            // Псевдослучайный шум на основе sin/frac — дёшево, выглядит как плёночное зерно
            float Hash(float2 p)
            {
                return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453);
            }

            // Зерно в пикселях с анимацией по времени
            float FilmGrain(float2 uv)
            {
                // Масштабируем UV в пространство пикселей, делим на GrainSize
                float2 pixel = floor(uv * _ScreenParams.xy / max(_GrainSize, 0.1));
                // Seed меняется каждые (1/GrainSpeed) секунд — зерно мерцает
                float  seed  = floor(_Time.y * _GrainSpeed) * 0.137;
                float  n     = Hash(pixel + seed);
                return (n - 0.5) * 2.0; // диапазон -1..1
            }

            // Виньетка: затемнение от краёв к центру
            float Vignette(float2 uv)
            {
                float2 center = uv - 0.5;
                // Корректируем по соотношению сторон чтобы виньетка была круглой
                center.x *= _ScreenParams.x / max(_ScreenParams.y, 1.0);
                float dist = length(center);
                // smoothstep: 1 в центре (radius–softness), 0 на краю (radius)
                float edge = _VignetteRadius - _VignetteSoftness;
                float v    = smoothstep(_VignetteRadius, max(edge, 0.01), dist);
                // lerp: в центре сохраняем яркость, на краях тёмнее
                return lerp(1.0 - _VignetteStrength, 1.0, v);
            }

            // Цветокоррекция
            float3 ColorGrade(float3 col)
            {
                // 1. Экспозиция (exp2 = перцептивно линейно)
                col *= exp2(_Exposure);

                // 2. Контраст вокруг среднего серого (0.5)
                col = (col - 0.5) * max(_Contrast, 0.0) + 0.5;

                // 3. Насыщенность: lerp между luma и оригиналом
                float lum = dot(col, float3(0.2126, 0.7152, 0.0722));
                col = lerp(float3(lum, lum, lum), col, saturate(_Saturation));

                // 4. Тонирование теней: добавляем цвет только в тёмных областях
                // shadow маска = 0 в ярких местах, 1 в тёмных
                float shadow = saturate(1.0 - lum * 2.5);
                col += _ShadowTint.rgb * shadow * shadow; // квадрат для плавности

                return saturate(col);
            }

            float3 SampleScene(float2 uv)
            {
                return SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, uv).rgb;
            }

            float SampleLinearDepth01(float2 uv)
            {
                return Linear01Depth(SampleSceneDepth(uv), _ZBufferParams);
            }

            float DepthEdgeMask(float2 uv)
            {
                float center = SampleLinearDepth01(uv);
                float distanceThin = 1.0 - saturate(smoothstep(_OutlineFogFadeStart, _OutlineFogFadeEnd, center) * _OutlineDistanceThinness);
                float thickness = max(0.35, _OutlineThickness * distanceThin);
                float2 texel = rcp(max(_ScreenParams.xy, float2(1.0, 1.0))) * thickness;
                float dx0 = abs(center - SampleLinearDepth01(uv + float2(texel.x, 0.0)));
                float dx1 = abs(center - SampleLinearDepth01(uv - float2(texel.x, 0.0)));
                float dy0 = abs(center - SampleLinearDepth01(uv + float2(0.0, texel.y)));
                float dy1 = abs(center - SampleLinearDepth01(uv - float2(0.0, texel.y)));
                float edge = max(max(dx0, dx1), max(dy0, dy1));
                float edgeMask = saturate(smoothstep(_OutlineDepthThreshold, _OutlineDepthThreshold * 4.0, edge));
                float fogFade = 1.0 - saturate(smoothstep(_OutlineFogFadeStart, _OutlineFogFadeEnd, center));
                return edgeMask * fogFade;
            }

            float3 SampleChannelSplit(float2 uv, float split)
            {
                split = saturate(split);
                float2 dir = uv - 0.5;
                float len = max(length(dir), 0.0001);
                dir /= len;
                dir *= split;

                float r = SampleScene(uv + dir).r;
                float g = SampleScene(uv).g;
                float b = SampleScene(uv - dir).b;
                return float3(r, g, b);
            }

            float3 SampleBlur(float2 uv, float blurStrength, float split)
            {
                float blur = saturate(blurStrength);
                if (blur <= 0.0001)
                {
                    return SampleChannelSplit(uv, split);
                }

                float2 texel = rcp(max(_ScreenParams.xy, float2(1.0, 1.0)));
                texel *= lerp(1.0, 8.0, blur);

                float3 center = SampleChannelSplit(uv, split) * 0.36;
                float3 xPlus = SampleChannelSplit(uv + float2(texel.x, 0.0), split) * 0.16;
                float3 xMinus = SampleChannelSplit(uv - float2(texel.x, 0.0), split) * 0.16;
                float3 yPlus = SampleChannelSplit(uv + float2(0.0, texel.y), split) * 0.16;
                float3 yMinus = SampleChannelSplit(uv - float2(0.0, texel.y), split) * 0.16;

                return center + xPlus + xMinus + yPlus + yMinus;
            }

            float SoftDisc(float2 uv, float2 center, float radius, float softness)
            {
                float2 offset = uv - center;
                offset.x *= _ScreenParams.x / max(_ScreenParams.y, 1.0);
                float dist = length(offset);
                return 1.0 - smoothstep(radius, radius + max(softness, 0.0001), dist);
            }

            float3 ApplyImpactGlare(float2 uv, float3 col)
            {
                float lum = dot(col, float3(0.2126, 0.7152, 0.0722));
                float highlightMask = saturate((lum - _GlareThreshold) / max(1e-4, 1.0 - _GlareThreshold));

                float2 centered = uv - 0.5;
                centered.x *= _ScreenParams.x / max(_ScreenParams.y, 1.0);
                float radial = saturate(1.0 - length(centered) * 1.55);

                float t = _Time.y;
                float blotchA = SoftDisc(uv, float2(0.31 + sin(t * 0.73) * 0.03, 0.30 + cos(t * 0.51) * 0.02), 0.11, 0.12);
                float blotchB = SoftDisc(uv, float2(0.68 + cos(t * 0.64) * 0.02, 0.38 + sin(t * 0.92) * 0.03), 0.14, 0.14);
                float blotchC = SoftDisc(uv, float2(0.53 + sin(t * 0.46) * 0.02, 0.58 + cos(t * 0.88) * 0.025), 0.10, 0.10);
                float streak = pow(saturate(1.0 - abs(centered.y + sin(t * 0.81) * 0.04) * 4.8), 3.0) * 0.28;

                float glareMask = saturate(radial * 0.35 + blotchA * 0.72 + blotchB * 0.64 + blotchC * 0.55 + streak);
                float whiteGlow = highlightMask * _GlareStrength * glareMask;
                float veil = _VeilStrength * (0.35 + radial * 0.65);

                return col + float3(1.0, 1.0, 1.0) * (whiteGlow + veil);
            }

            float3 ApplyFlatShade(float3 col)
            {
                float steps = max(_FlatShadeSteps, 2.0);
                float3 crushed = saturate(col - _ShadowCrush * 0.22);
                float3 posterized = floor(crushed * steps) / max(steps - 1.0, 1.0);
                return lerp(col, saturate(posterized), saturate(_FlatShadeBlend));
            }

            // ── Fragment ─────────────────────────────────────────────
            half4 Frag(Varyings input) : SV_Target
            {
                float2 uv = input.texcoord;
                float edgeMask = DepthEdgeMask(uv);
                float split = saturate(_ChannelSplit + edgeMask * _EdgeFringeStrength);

                half4 col = half4(SampleBlur(uv, _BlurStrength, split), 1.0);

                // 2. Film grain (добавляем к RGB)
                float grain = FilmGrain(uv) * _GrainStrength;
                col.rgb += grain;

                // 3. Цветокоррекция (exposure, contrast, saturation, shadow tint)
                col.rgb = ColorGrade(col.rgb);

                // 4. White glare / haze on heavy impacts.
                col.rgb = ApplyImpactGlare(uv, col.rgb);

                // 4.5. Грубое ступенчатое освещение без плавного градиента.
                col.rgb = ApplyFlatShade(col.rgb);

                // 5. Виньетка (умножаем на маску)
                col.rgb *= Vignette(uv);

                // 6. Глобальный контур по depth — эффект ложится на все предметы сразу.
                col.rgb = lerp(col.rgb, _OutlineColor.rgb, edgeMask * _OutlineStrength);

                // 7. Короткие цветовые импульсы для выстрела/урона/фаз
                col.rgb = saturate(col.rgb + _PulseTint.rgb * _PulseTint.a);

                return col;
            }
            ENDHLSL
        }
    }

    // Fallback: если URP не подхватил — не показывать ничего (не роняем рендер)
    FallBack Off
}
