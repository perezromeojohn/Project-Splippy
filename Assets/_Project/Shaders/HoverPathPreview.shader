Shader "ProjectSplippy/HoverPathPreview"
{
    Properties
    {
        _BaseColor ("Base Color", Color) = (0.2, 0.95, 1, 0.72)
        _PulseSpeed ("Pulse Speed", Float) = 6.0
        _PulseStrength ("Pulse Strength", Float) = 0.25
        _EdgeSoftness ("Edge Softness", Float) = 2.2
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
                float4 _BaseColor;
                float _PulseSpeed;
                float _PulseStrength;
                float _EdgeSoftness;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionHCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float2 centered = input.uv * 2.0 - 1.0;
                float dist = length(centered);
                float radialMask = saturate(1.0 - pow(dist, _EdgeSoftness));

                float pulse = 1.0 + sin(_Time.y * _PulseSpeed) * _PulseStrength;
                float alpha = saturate(_BaseColor.a * radialMask * pulse);
                float3 color = _BaseColor.rgb * pulse;

                return half4(color, alpha);
            }
            ENDHLSL
        }
    }
}
