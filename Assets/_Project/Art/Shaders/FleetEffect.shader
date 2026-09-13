Shader "DropletPrototype/Effect"
{
    Properties { _BaseColor("Tint",Color)=(1,1,1,1) _UseUVFade("UV fade for trail",Float)=0 }
    SubShader
    {
        Tags {"RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent"}
        Pass
        {
            Blend SrcAlpha One Cull Off ZWrite Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial) float4 _BaseColor; float _UseUVFade; CBUFFER_END
            struct A{float4 p:POSITION;float2 uv:TEXCOORD0;half4 c:COLOR;};
            struct V{float4 p:SV_POSITION;float2 uv:TEXCOORD0;half4 c:COLOR;};
            V vert(A a){V o;o.p=TransformObjectToHClip(a.p.xyz);o.uv=a.uv;o.c=a.c;return o;}
            half4 frag(V i):SV_Target{float a=_UseUVFade>1.5?saturate(1-length(i.uv*2-1)):saturate(1-abs(i.uv.y*2-1));a=lerp(1,a*a,saturate(_UseUVFade));return half4(_BaseColor.rgb*i.c.rgb,_BaseColor.a*i.c.a*a);}
            ENDHLSL
        }
    }
}
