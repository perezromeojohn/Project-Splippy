Shader "ProjectSplippy/FarmlandBillboardLitSway"
{
    Properties
    {
        [PerRendererData] _MainTex("Sprite Texture", 2D) = "white" {}
        _Color("Tint", Color) = (1,1,1,1)
        _Cutoff("Alpha Cutoff", Range(0,1)) = 0.2

        _WindAmplitude("Wind Amplitude", Range(0,0.2)) = 0.035
        _WindFrequency("Wind Frequency", Range(0,8)) = 2.2
        _WindSpeed("Wind Speed", Range(0,8)) = 1.6

        _InteractorStrength("Interactor Strength", Range(0,0.3)) = 0.08
        _InteractorFalloff("Interactor Falloff", Range(0.1,4)) = 1.4
        _ColorWobbleAmount("Color Wobble Amount", Range(0,0.2)) = 0.03
        _ColorWobbleSpeed("Color Wobble Speed", Range(0,8)) = 1.3
        _ColorWobbleScale("Color Wobble Scale", Range(0,8)) = 2.0
    }

    SubShader
    {
        Tags
        {
            "Queue"="AlphaTest"
            "RenderType"="TransparentCutout"
            "RenderPipeline"="UniversalPipeline"
            "IgnoreProjector"="True"
            "CanUseSpriteAtlas"="True"
        }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }

            Cull Off
            ZWrite On
            Blend SrcAlpha OneMinusSrcAlpha
            AlphaToMask On

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float _Cutoff;
                float _WindAmplitude;
                float _WindFrequency;
                float _WindSpeed;
                float _InteractorStrength;
                float _InteractorFalloff;
                float _ColorWobbleAmount;
                float _ColorWobbleSpeed;
                float _ColorWobbleScale;
            CBUFFER_END

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            float4 _SplippyInteractor;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 normalWS : TEXCOORD2;
                float fogFactor : TEXCOORD3;
                float4 shadowCoord : TEXCOORD4;
            };

            float3 ApplySway(float3 positionOS, float2 uv)
            {
                float tipMask = saturate(uv.y);
                float wind = sin(_Time.y * _WindSpeed + positionOS.x * _WindFrequency + positionOS.z * (_WindFrequency * 0.7));

                float3 interactorPos = _SplippyInteractor.xyz;
                float interactorRadius = max(0.01, _SplippyInteractor.w);
                float dist = distance(mul(unity_ObjectToWorld, float4(positionOS, 1.0)).xyz, interactorPos);
                float interact01 = saturate(1.0 - dist / interactorRadius);
                interact01 = pow(interact01, _InteractorFalloff);

                float swayX = wind * _WindAmplitude * tipMask;
                swayX += _InteractorStrength * interact01 * tipMask;

                positionOS.x += swayX;
                return positionOS;
            }

            Varyings vert(Attributes input)
            {
                Varyings output;

                float3 positionOS = ApplySway(input.positionOS.xyz, input.uv);
                float3 positionWS = TransformObjectToWorld(positionOS);

                output.positionWS = positionWS;
                output.positionCS = TransformWorldToHClip(positionWS);
                output.uv = input.uv;
                output.normalWS = normalize(TransformObjectToWorldNormal(float3(0.0, 0.0, -1.0)));
                output.fogFactor = ComputeFogFactor(output.positionCS.z);
                output.shadowCoord = TransformWorldToShadowCoord(positionWS);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                half4 texColor = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv) * _Color;
                clip(texColor.a - _Cutoff);

                Light mainLight = GetMainLight(input.shadowCoord);
                half ndotl = saturate(dot(input.normalWS, mainLight.direction) * 0.5h + 0.5h);
                half3 lit = texColor.rgb * (0.25h + ndotl * mainLight.shadowAttenuation);

                #if defined(_ADDITIONAL_LIGHTS)
                uint lightCount = GetAdditionalLightsCount();
                for (uint i = 0; i < lightCount; i++)
                {
                    Light light = GetAdditionalLight(i, input.positionWS);
                    half addNdotL = saturate(dot(input.normalWS, light.direction) * 0.5h + 0.5h);
                    lit += texColor.rgb * light.color * addNdotL * 0.2h;
                }
                #endif

                float wobbleTime = _Time.y * _ColorWobbleSpeed;
                float wobblePhase = (input.positionWS.x + input.positionWS.z) * _ColorWobbleScale;
                float wobble = sin(wobbleTime + wobblePhase) * _ColorWobbleAmount;
                float3 wobbleTint = float3(1.0 + wobble, 1.0 + wobble * 0.35, 1.0 - wobble * 0.45);
                lit *= wobbleTint;

                lit = MixFog(lit, input.fogFactor);
                return half4(lit, texColor.a);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }

            ZWrite On
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float _Cutoff;
                float _WindAmplitude;
                float _WindFrequency;
                float _WindSpeed;
                float _InteractorStrength;
                float _InteractorFalloff;
                float _ColorWobbleAmount;
                float _ColorWobbleSpeed;
                float _ColorWobbleScale;
            CBUFFER_END

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            float4 _SplippyInteractor;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            float3 ApplySway(float3 positionOS, float2 uv)
            {
                float tipMask = saturate(uv.y);
                float wind = sin(_Time.y * _WindSpeed + positionOS.x * _WindFrequency + positionOS.z * (_WindFrequency * 0.7));

                float3 interactorPos = _SplippyInteractor.xyz;
                float interactorRadius = max(0.01, _SplippyInteractor.w);
                float dist = distance(mul(unity_ObjectToWorld, float4(positionOS, 1.0)).xyz, interactorPos);
                float interact01 = saturate(1.0 - dist / interactorRadius);
                interact01 = pow(interact01, _InteractorFalloff);

                float swayX = wind * _WindAmplitude * tipMask;
                swayX += _InteractorStrength * interact01 * tipMask;

                positionOS.x += swayX;
                return positionOS;
            }

            Varyings vert(Attributes input)
            {
                Varyings output;
                float3 positionOS = ApplySway(input.positionOS.xyz, input.uv);
                float3 positionWS = TransformObjectToWorld(positionOS);
                output.positionCS = TransformWorldToHClip(positionWS);
                output.uv = input.uv;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                half4 texColor = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv) * _Color;
                clip(texColor.a - _Cutoff);
                return 0;
            }
            ENDHLSL
        }
    }
}
