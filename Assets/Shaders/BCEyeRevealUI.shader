Shader "BreathCasino/UI/EyeReveal"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Color", Color) = (0,0,0,1)
        _Reveal ("Reveal", Range(0, 2)) = 0
        _Softness ("Softness", Range(0.001, 1)) = 0.12
        _Aspect ("Aspect", Float) = 1
        _VerticalScale ("Vertical Scale", Float) = 1.35
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

        Cull Off
        Lighting Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata_t
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            fixed4 _Color;
            sampler2D _MainTex;
            float _Reveal;
            float _Softness;
            float _Aspect;
            float _VerticalScale;

            v2f vert(appdata_t v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 baseSample = tex2D(_MainTex, i.uv);

                if (_Reveal <= 0.0001)
                {
                    return fixed4(_Color.rgb, _Color.a * baseSample.a);
                }

                float2 centered = i.uv * 2.0 - 1.0;
                centered.x *= _Aspect;
                centered.y *= _VerticalScale;

                float radius = length(centered);
                float alpha = smoothstep(_Reveal - _Softness, _Reveal + _Softness, radius);
                return fixed4(_Color.rgb, _Color.a * alpha * baseSample.a);
            }
            ENDCG
        }
    }
}
