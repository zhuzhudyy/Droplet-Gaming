Shader "DropletPrototype/Lighting/SolarDisc"
{
    Properties{[HDR]_BaseColor("Radiance",Color)=(5,2.8,.7,1)}
    SubShader
    {
        Tags{"RenderPipeline"="UniversalPipeline" "RenderType"="Opaque"}
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
            float4 _BaseColor;
            CBUFFER_END
            struct A{float4 p:POSITION;float3 n:NORMAL;};
            struct V{float4 p:SV_POSITION;float3 n:TEXCOORD0;float3 w:TEXCOORD1;float3 obj:TEXCOORD2;};
            V vert(A a){V o;o.w=TransformObjectToWorld(a.p.xyz);o.p=TransformWorldToHClip(o.w);o.n=TransformObjectToWorldNormal(a.n);o.obj=a.p.xyz;return o;}
            half4 frag(V i):SV_Target
            {float limb=saturate(dot(normalize(i.n),normalize(_WorldSpaceCameraPos-i.w)));float grain=sin(i.obj.x*36+sin(i.obj.z*17))*sin(i.obj.y*31);return half4(_BaseColor.rgb*(.5+.5*pow(limb,.38))*(.93+grain*.07),1);}
            ENDHLSL
        }
    }
}
