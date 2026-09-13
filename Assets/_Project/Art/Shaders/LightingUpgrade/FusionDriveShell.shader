Shader "DropletPrototype/FusionDriveShell"
{
    Properties
    {
        [HDR] _Color("Confined plasma color", Color) = (0.12, 1.25, 2.5, 1)
        _Opacity("Shell opacity", Range(0, 1)) = 0.36
        _RimPower("Energy shell edge", Range(0.3, 6)) = 1.7
        _CenterFill("Interior contribution", Range(0, 1)) = 0.1
        _FlowSpeed("Slow plasma circulation", Range(0, 2)) = 0.38
        _NoiseAmount("Surface variation", Range(0, 0.5)) = 0.18
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent" }
        Pass
        {
            Name "ConfinedEnergyShell"
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha One
            ZWrite Off
            ZTest LEqual
            Cull Back
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                float _Opacity;
                float _RimPower;
                float _CenterFill;
                float _FlowSpeed;
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
                output.directionOS = normalize(input.normalOS);
                output.phase = dot(GetObjectToWorldMatrix()._m03_m13_m23, float3(.037, .051, .019));
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                half3 normal = normalize(input.normalWS);
                half3 view = GetWorldSpaceNormalizeViewDir(input.positionWS);
                half rim = pow(1 - saturate(dot(normal, view)), _RimPower);
                float t = _Time.y * _FlowSpeed + input.phase;
                float3 p = normalize(input.directionOS);
                half field = sin(p.x * 9.0 + t * .85) * sin(p.y * 6.0 - t * .53)
                           * sin(p.z * 7.0 + t * .41);
                half alpha = saturate(lerp(_CenterFill, 1, rim) * _Opacity * (1 + field * _NoiseAmount));
                return half4(_Color.rgb, alpha);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
