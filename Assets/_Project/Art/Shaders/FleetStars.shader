Shader "DropletPrototype/Stars"
{
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Background+1" }
        Pass
        {
            Cull Off ZWrite Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct A { float4 p:POSITION; half4 c:COLOR; };
            struct V { float4 p:SV_POSITION; half4 c:COLOR; };
            V vert(A a) { V o; o.p=TransformObjectToHClip(a.p.xyz); o.c=a.c; return o; }
            half4 frag(V i):SV_Target { return i.c; }
            ENDHLSL
        }
    }
}
