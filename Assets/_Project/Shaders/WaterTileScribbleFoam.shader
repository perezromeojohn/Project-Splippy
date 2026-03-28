Shader "ProjectSplippy/WaterTileScribbleFoam"
{
    Properties
    {
        _WaterColor("Water Color", Color) = (0.2,0.5,0.8,1)
        _FoamColor("Foam Color", Color) = (1,1,1,1)
        _FoamWidth("Foam Width", Range(0,0.2)) = 0.06
        _JitterAmp("Jitter Amount", Range(0,1)) = 0.008
        _JitterFreq("Jitter Frequency", Range(0.5,24)) = 8.0
        _JitterSpeed("Jitter Speed", Range(0,12)) = 3.0
        _Seed("Seed", Float) = 1.0
        _TopHalfExtents("Top Half Extents", Vector) = (0.5,0.5,0,0)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Name "WaterFoam"
            Tags { "LightMode"="SRPDefaultUnlit" }
            Cull Back
            ZWrite On
            ZTest LEqual
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };
            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 posWS : TEXCOORD0;
                float3 posOS : TEXCOORD1;
                float3 normalOS : TEXCOORD2;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _WaterColor;
                float4 _FoamColor;
                float _FoamWidth;
                float _JitterAmp;
                float _JitterFreq;
                float _JitterSpeed;
                float _Seed;
                float4 _TopHalfExtents;
            CBUFFER_END

            float ScribbleNoise(float3 p)
            {
                float t = _Time.y * _JitterSpeed;
                float n = sin((p.x + _Seed) * _JitterFreq + t);
                n += 0.6 * sin((p.y + _Seed * 1.73) * (_JitterFreq * 1.31) - t * 1.19);
                n += 0.35 * sin((p.z + _Seed * 2.11) * (_JitterFreq * 2.13) + t * 0.67);
                return n / 1.95;
            }

            Varyings vert(Attributes input)
            {
                Varyings o;
                o.positionHCS = TransformObjectToHClip(input.positionOS.xyz);
                o.posWS = TransformObjectToWorld(input.positionOS.xyz);
                o.posOS = input.positionOS.xyz;
                o.normalOS = input.normalOS;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float topMask = smoothstep(0.6, 0.9, normalize(i.normalOS).y);

                float2 ext = max(_TopHalfExtents.xy, float2(0.0001, 0.0001));
                float2 d = ext - abs(i.posOS.xz);
                float edgeDist = min(d.x, d.y);
                float edgeDistN = saturate(edgeDist / min(ext.x, ext.y));

                float jitterA = ScribbleNoise(float3(i.posWS.x, i.posWS.z, i.posOS.y)) * _JitterAmp;
                float jitterB = ScribbleNoise(float3(i.posWS.x * 0.67 + 5.3, i.posWS.z * 0.67 + 9.1, i.posOS.y + 1.7)) * (_JitterAmp * 1.9);
                float width = max(0.0001, _FoamWidth + jitterA + jitterB);
                float softness = max(0.004, width * 1.25);
                float warpedDist = edgeDistN + jitterA * 0.35 + jitterB * 0.65;
                float foam = smoothstep(width + softness, 0.0, warpedDist) * topMask;

                float4 col = lerp(_WaterColor, _FoamColor, foam * 0.72);
                col.a = 1;
                return col;
            }
            ENDHLSL
        }
    }
}