Shader "Custom/FogUI"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _FogSpeed ("Fog Speed", Vector) = (0.03, 0.01, 0, 0)
        _Fog_Size ("Fog Size", Float) = 3.0
        _Color ("Fog Color", Color) = (0.6, 0.8, 1.0, 1.0)
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "IgnoreProjector" = "True"
            "PreviewType" = "Plane"
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

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float4 _FogSpeed;
            float _Fog_Size;
            float4 _Color;

            // Simple noise function
            float2 hash2(float2 p)
            {
                p = float2(dot(p, float2(127.1, 311.7)),
                           dot(p, float2(269.5, 183.3)));
                return frac(sin(p) * 43758.5453);
            }

            float noise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);

                float a = dot(hash2(i + float2(0,0)), f - float2(0,0));
                float b = dot(hash2(i + float2(1,0)), f - float2(1,0));
                float c = dot(hash2(i + float2(0,1)), f - float2(0,1));
                float d = dot(hash2(i + float2(1,1)), f - float2(1,1));

                return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y) * 0.5 + 0.5;
            }

            float fbm(float2 p)
            {
                float value = 0.0;
                float amplitude = 0.5;
                float frequency = 1.0;

                for (int i = 0; i < 4; i++)
                {
                    value += amplitude * noise(p * frequency);
                    amplitude *= 0.5;
                    frequency *= 2.0;
                }
                return value;
            }

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.color = v.color;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 uv = i.uv;

                // Scroll fog
                float2 scrolledUV1 = uv * _Fog_Size + _Time.y * _FogSpeed.xy;
                float2 scrolledUV2 = uv * _Fog_Size * 1.5 + _Time.y * _FogSpeed.xy * -0.7;

                // Layer 2 noise
                float fog1 = fbm(scrolledUV1);
                float fog2 = fbm(scrolledUV2);
                float fogValue = (fog1 + fog2) * 0.5;

                // Fade edges (vignette nhẹ ở rìa)
                float2 edge = abs(uv - 0.5) * 2.0;
                float vignette = 1.0 - smoothstep(0.6, 1.0, max(edge.x, edge.y));
                fogValue *= vignette;

                // Output
                float4 col = _Color;
                col.a *= fogValue * i.color.a;

                return col;
            }
            ENDCG
        }
    }
}
