Shader "DropletPrototype/Lighting/SolarCorona"
{
    Properties{[HDR]_BaseColor("Corona",Color)=(.6,.24,.035,1)}
    SubShader
    {
        Tags{"RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent"}
        Pass
        {
            Blend One One
            ZWrite Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
            float4 _BaseColor;
            CBUFFER_END
            struct A{float4 p:POSITION;float3 n:NORMAL;};struct V{float4 p:SV_POSITION;float3 n:TEXCOORD0;float3 w:TEXCOORD1;};
            V vert(A a){V o;o.w=TransformObjectToWorld(a.p.xyz);o.p=TransformWorldToHClip(o.w);o.n=TransformObjectToWorldNormal(a.n);return o;}
            half4 frag(V i):SV_Target{float mu=saturate(dot(normalize(i.n),normalize(_WorldSpaceCameraPos-i.w)));float radial=sqrt(saturate(1-mu*mu));float glow=exp(-radial*8)*smoothstep(1,.72,radial);return half4(_BaseColor.rgb*glow,0);}
            ENDHLSL
        }
    }
}
