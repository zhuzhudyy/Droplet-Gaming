Shader "DropletPrototype/VisualUpgrade/ReactorCore"
{
    Properties
    {
        [HDR] _CoreColor("Hot inner plasma", Color) = (5.5, 5.0, 3.9, 1)
        [HDR] _EdgeColor("Cool confined edge", Color) = (0.15, 1.2, 2.8, 1)
        _Instability("Reactor instability", Range(0,1)) = 0
        _FlowSpeed("Slow plasma circulation", Range(0, 2)) = 0.38
        _PulseAmount("Small power variation", Range(0, 0.12)) = 0.035
        _NoiseAmount("Surface variation", Range(0, 0.5)) = 0.16
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry+10" }
        Pass
        {
            Name "FusionCore"
            Tags { "LightMode"="UniversalForward" }
            ZWrite On
            Cull Back
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _CoreColor;
                half4 _EdgeColor;
                float _Instability;
                float _FlowSpeed;
                float _PulseAmount;
                float _NoiseAmount;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                half3 directionOS : TEXCOORD2;
                float phase : TEXCOORD3;
                UNITY_VERTEX_OUTPUT_STEREO
            };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                // Direction comes from the imported sphere normal, so the pattern
                // does not depend on that shared source mesh's authored radius.
                output.directionOS = normalize(input.normalOS);
                output.phase = dot(GetObjectToWorldMatrix()._m03_m13_m23, float3(.037, .051, .019));
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                half3 normal = normalize(input.normalWS);
                half3 view = GetWorldSpaceNormalizeViewDir(input.positionWS);
                half facing = saturate(dot(normal, view));
                float t = _Time.y * _FlowSpeed + input.phase;
                float3 p = normalize(input.directionOS);
                // Smooth low-frequency field: no stochastic frame noise, texture
                // allocations, sharp flicker, or long exhaust plume.
                half field = sin(p.x * 8.0 + t) * sin(p.y * 7.0 - t * .63)
                           * sin(p.z * 6.0 + t * .41);
                // Keep most of the shell below the tone mapper's white shoulder.
                // The old broad smoothstep drove nearly the entire visible sphere
                // to HDR white, erasing depth and its restrained plasma variation.
                half circulation = .5 + .5 * sin(p.x * 7 + sin(p.y * 5 - t * .4)
                                               + p.z * 4 + t * .6);
                half channels = smoothstep(.25, .82, circulation + field * .18);
                half hot = pow(facing, 4) * lerp(.22, .78, channels);
                half concentratedCore = pow(facing, 24) * .24;
                half3 emission = _EdgeColor.rgb * (.14 + .12 * facing)
                               + _CoreColor.rgb * (hot + concentratedCore);
                emission *= (1 + field * _NoiseAmount) * (1 + sin(t * 1.3) * _PulseAmount);
                emission = lerp(emission, emission * 3.5 + half3(3, 4, 5), saturate(_Instability));
                return half4(max(emission, 0), 1);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
