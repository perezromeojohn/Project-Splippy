Shader "ProjectSplippy/GridTileScribbleOutline"
{
    Properties
    {
        [MainTexture] _BaseMap("Base Map", 2D) = "white" {}
        [MainColor] _Color("Base Color", Color) = (1,1,1,1)

        _InkColor("Outline Color", Color) = (0.05, 0.07, 0.08, 1)
        _OutlineWidth("Outline Width", Range(0, 0.2)) = 0.04
        _JitterAmp("Jitter Amount", Range(0, 0.03)) = 0.006
        _JitterFreq("Jitter Frequency", Range(0.5, 24)) = 9.0
        _JitterSpeed("Jitter Speed", Range(0, 12)) = 4.0
        _Seed("Seed", Float) = 1.0
        _RadialExtrude("Radial Extrude", Range(0, 1)) = 1.0
        _SmokeWobble("Smoke Wobble", Range(0, 0.08)) = 0.02
        _NeighborFade("Neighbor Fade", Range(0, 1)) = 1.0
        _Softness("Softness", Range(0, 1)) = 0.75
        _SoftnessPower("Softness Power", Range(0.5, 6)) = 2.4
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "Outline"
            Tags { "LightMode" = "SRPDefaultUnlit" }

            Cull Front
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
                float3 normalWS : TEXCOORD0;
                float3 viewDirWS : TEXCOORD1;
                float3 normalOS : TEXCOORD2;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _InkColor;
                float _OutlineWidth;
                float _JitterAmp;
                float _JitterFreq;
                float _JitterSpeed;
                float _Seed;
                float _RadialExtrude;
                float _SmokeWobble;
                float _NeighborFade;
                float _Softness;
                float _SoftnessPower;
            CBUFFER_END

            float4 _NeighborMask;

            float ScribbleNoise(float3 p)
            {
                float t = _Time.y * _JitterSpeed;
                float n = sin((p.x + _Seed) * _JitterFreq + t);
                n += 0.6 * sin((p.y + _Seed * 1.73) * (_JitterFreq * 1.31) - t * 1.19);
                n += 0.35 * sin((p.z + _Seed * 2.11) * (_JitterFreq * 2.13) + t * 0.67);
                return n / 1.95;
            }

            float LowFreqWobble(float3 p)
            {
                float t = _Time.y * (_JitterSpeed * 0.55);
                float n = sin((p.x + _Seed * 3.17) * (_JitterFreq * 0.41) + t);
                n += 0.75 * sin((p.y + _Seed * 1.29) * (_JitterFreq * 0.33) - t * 1.13);
                n += 0.5 * sin((p.z + _Seed * 2.47) * (_JitterFreq * 0.29) + t * 0.81);
                return n / 2.25;
            }

            Varyings vert(Attributes input)
            {
                Varyings output;

                float3 posWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 radialOS = normalize(input.positionOS.xyz);
                float3 fallbackRadial = normalize(input.normalOS);
                float radialValid = step(1e-6, dot(radialOS, radialOS));
                radialOS = normalize(lerp(fallbackRadial, radialOS, radialValid));
                float3 blendedNormalOS = normalize(lerp(input.normalOS, radialOS, saturate(_RadialExtrude)));
                float3 nrmWS = normalize(TransformObjectToWorldNormal(blendedNormalOS));
                float jitter = ScribbleNoise(posWS) * _JitterAmp;
                float wobble = LowFreqWobble(posWS) * _SmokeWobble;
                float width = max(0.0, _OutlineWidth + jitter);
                posWS += nrmWS * (width + wobble);

                output.positionHCS = TransformWorldToHClip(posWS);
                output.normalWS = nrmWS;
                output.viewDirWS = GetWorldSpaceNormalizeViewDir(posWS);
                output.normalOS = blendedNormalOS;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float facing = saturate(dot(-normalize(input.normalWS), normalize(input.viewDirWS)));
                float edge = saturate(1.0 - facing);
                float softAlpha = saturate(pow(edge, max(0.001, _SoftnessPower)));
                float alpha = lerp(1.0, softAlpha, saturate(_Softness));

                float2 side = normalize(input.normalOS.xz + 1e-6);
                float westW = saturate(-side.x);
                float eastW = saturate(side.x);
                float southW = saturate(-side.y);
                float northW = saturate(side.y);
                float sideStrength = saturate(length(input.normalOS.xz) * 2.0);

                float neighborMask =
                    westW * _NeighborMask.x +
                    eastW * _NeighborMask.y +
                    southW * _NeighborMask.z +
                    northW * _NeighborMask.w;

                float neighborSuppression = saturate(neighborMask * sideStrength * saturate(_NeighborFade));
                alpha *= (1.0 - neighborSuppression);

                return half4(_InkColor.rgb, _InkColor.a * alpha);
            }
            ENDHLSL
        }

        Pass
        {
            Name "Base"
            Tags { "LightMode" = "UniversalForward" }

            Cull Back
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

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
                float4 _Color;
            CBUFFER_END

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionHCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                return SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv) * _Color;
            }
            ENDHLSL
        }
    }
}
