// Inspection-only ideal mirror reflecting a continuous procedural stripe world.
// No textures, surface stripes, double-sided rendering, normal changes, or post effects.
Shader "DropletPrototype/Inspection/RebuiltAnalyticMirror"
{
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Cull Back
            ZWrite On
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Input { float4 positionOS:POSITION; float3 normalOS:NORMAL; };
            struct Output { float4 positionCS:SV_POSITION; float3 positionWS:TEXCOORD0; float3 normalWS:TEXCOORD1; };
            Output Vert(Input v)
            { Output o; o.positionWS=TransformObjectToWorld(v.positionOS.xyz); o.positionCS=TransformWorldToHClip(o.positionWS); o.normalWS=TransformObjectToWorldNormal(v.normalOS); return o; }
            half4 Frag(Output i):SV_Target
            {
                float3 view=GetWorldSpaceNormalizeViewDir(i.positionWS);
                float3 direction=reflect(-view,normalize(i.normalWS));
                float wave=sin(direction.z*13.0);
                float edge=max(.06,fwidth(wave));
                float stripe=smoothstep(-edge,edge,wave);
                return half4(lerp(half3(.035,.035,.035),half3(.65,.65,.65),stripe),1);
            }
            ENDHLSL
        }
    }
}
