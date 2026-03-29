Shader "ProjectSplippy/HoverPathPreview"
{
    Properties
    {
        _InkColor ("Ink Color", Color) = (0.2, 0.95, 1, 0.9)
        _Mode ("Mode (0 circle, 1 link)", Float) = 0
        _StrokeWidth ("Stroke Width", Range(0.01, 0.4)) = 0.13
        _JitterAmp ("Jitter Amount", Range(0, 0.2)) = 0.05
        _JitterFreq ("Jitter Frequency", Range(0.1, 20)) = 7.0
        _JitterSpeed ("Jitter Speed", Range(0.1, 10)) = 3.0
        _Seed ("Seed", Float) = 0
        _FillAlpha ("Fill Alpha", Range(0, 1)) = 0.08
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
        }

        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Offset -1, -1
        Cull Off

        Pass
        {
            Name "Unlit"

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
                float4 _InkColor;
                float _Mode;
                float _StrokeWidth;
                float _JitterAmp;
                float _JitterFreq;
                float _JitterSpeed;
                float _Seed;
                float _FillAlpha;
            CBUFFER_END

            float Hash11(float p)
            {
                p = frac(p * 0.1031);
                p *= p + 33.33;
                p *= p + p;
                return frac(p);
            }

            float ScribbleNoise(float2 uv, float seed)
            {
                float t = _Time.y * _JitterSpeed;
                float n = sin((uv.x + seed) * _JitterFreq + t);
                n += 0.6 * sin((uv.y + seed * 1.73) * (_JitterFreq * 1.37) - t * 1.21);
                n += 0.35 * sin((uv.x + uv.y + seed * 2.11) * (_JitterFreq * 2.2) + t * 0.63);
                return n / 1.95;
            }

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionHCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float2 uv = input.uv;
                float seed = _Seed + Hash11(floor(_Seed * 17.0) + 1.0);
                float jitter = ScribbleNoise(uv * 2.0 - 1.0, seed) * _JitterAmp;

                float alpha = 0.0;
                float feather = lerp(0.006, 0.03, saturate(_FillAlpha));

                if (_Mode < 0.5)
                {
                    float2 c = uv * 2.0 - 1.0;
                    float dist = length(c);
                    float radius = 0.72 + jitter * 0.25;
                    alpha = 1.0 - smoothstep(radius, radius + feather, dist);
                }
                else
                {
                    float xCenter = 0.5 + jitter * 0.12;
                    float2 a = float2(xCenter, 0.0);
                    float2 b = float2(xCenter, 1.0);
                    float2 pa = uv - a;
                    float2 ba = b - a;
                    float h = saturate(dot(pa, ba) / dot(ba, ba));
                    float distToCapsule = length(pa - ba * h) - (_StrokeWidth * 0.5);
                    alpha = 1.0 - smoothstep(0.0, feather, distToCapsule);
                }

                return half4(_InkColor.rgb, _InkColor.a * alpha);
            }
            ENDHLSL
        }
    }
}
