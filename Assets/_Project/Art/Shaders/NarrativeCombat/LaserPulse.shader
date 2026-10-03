Shader "DropletPrototype/NarrativeCombat/LaserPulse"
{
    Properties
    {
        [HDR] [MainColor] _BaseColor("Radiance", Color) = (1,1,1,1)
        _ContactShape("Contact or spark (0 = beam)", Float) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent" }
        Pass
        {
            Name "LaserPulse"
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha One
            ZWrite Off
            ZTest LEqual
            Cull Off
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float _ContactShape;
            CBUFFER_END
            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
                UNITY_VERTEX_OUTPUT_STEREO
            };
            Varyings Vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                output.color = input.color;
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 p = input.uv * 2 - 1;
                float distanceSquared = lerp(p.y * p.y, dot(p, p), _ContactShape);
                float envelope = exp2(-distanceSquared * 5) * (1 - smoothstep(.72, 1, distanceSquared));
                float core = exp2(-distanceSquared * 80);
                half4 color = _BaseColor * input.color;
                half3 radiance = color.rgb * .8 + lerp(color.rgb, half3(3, 3, 3), .64) * core;
                return half4(radiance, color.a * envelope);
            }
            ENDHLSL
        }
    }
}
