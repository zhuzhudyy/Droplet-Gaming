Shader "DropletPrototype/VisualUpgrade/ReactorFire"
{
    Properties
    {
        [HDR]_BaseColor("Radiance / lifetime opacity",Color)=(5,1.4,.18,1)
        _Kind("0 turbulent fire / 1 penetration flash",Float)=0
        _Age("Evolution",Float)=0
        _Seed("Stable lobe variation",Float)=0
    }
    SubShader
    {
        Tags{"RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent"}
        Pass
        {
            Blend SrcAlpha One
            Cull Off ZWrite Off ZTest LEqual
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
            float4 _BaseColor; float _Kind,_Age,_Seed;
            CBUFFER_END
            struct A{float4 p:POSITION;float3 n:NORMAL;float2 uv:TEXCOORD0;float4 color:COLOR;UNITY_VERTEX_INPUT_INSTANCE_ID};
            struct V{float4 p:SV_POSITION;float3 w:TEXCOORD0;float3 n:TEXCOORD1;float3 obj:TEXCOORD2;float2 uv:TEXCOORD3;float4 color:COLOR;UNITY_VERTEX_OUTPUT_STEREO};
            V vert(A a){V o;UNITY_SETUP_INSTANCE_ID(a);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);o.w=TransformObjectToWorld(a.p.xyz);o.p=TransformWorldToHClip(o.w);o.n=TransformObjectToWorldNormal(a.n);o.obj=a.p.xyz;o.uv=a.uv;o.color=a.color;return o;}
            float hash(float3 p){p=frac(p*.1031);p+=dot(p,p.yzx+33.33);return frac((p.x+p.y)*p.z);}
            float noise(float3 p){float3 b=floor(p),f=frac(p);f=f*f*(3-2*f);return lerp(lerp(lerp(hash(b),hash(b+float3(1,0,0)),f.x),lerp(hash(b+float3(0,1,0)),hash(b+float3(1,1,0)),f.x),f.y),lerp(lerp(hash(b+float3(0,0,1)),hash(b+float3(1,0,1)),f.x),lerp(hash(b+float3(0,1,1)),hash(b+float3(1,1,1)),f.x),f.y),f.z);}
            half4 frag(V i):SV_Target
            {
                float2 disc=i.uv*2-1;float radius=length(disc);
                float facing=sqrt(saturate(1-dot(disc,disc)));
                float3 p=float3(disc,facing)*5.5+float3(_Seed,_Age*1.3,-_Age*.65);
                float cells=noise(p)+.4*noise(p*2.07+13.1);
                float flame=smoothstep(.3,1.08,cells);
                float edge=1-smoothstep(.65+cells*.16,.82+cells*.14,radius);
                float hot=pow(facing,3)*smoothstep(.62,1.12,cells);
                float3 radiance=_BaseColor.rgb*(.28+flame*1.25);
                radiance+=float3(3.2,2.4,1.4)*hot*_BaseColor.a;
                float alpha=_BaseColor.a*edge*(.14+.68*flame);
                if(_Kind>.5){radiance=_BaseColor.rgb;alpha=_BaseColor.a*pow(saturate(1-radius),1.5);}
                return half4(radiance*i.color.rgb,saturate(alpha*i.color.a));
            }
            ENDHLSL
        }
    }
}
