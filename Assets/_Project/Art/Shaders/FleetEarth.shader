Shader "DropletPrototype/Earth"
{
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" }
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct A { float4 p:POSITION; float3 n:NORMAL; };
            struct V { float4 p:SV_POSITION; float3 n:TEXCOORD0; float3 w:TEXCOORD1; float3 obj:TEXCOORD2; };
            float hash(float3 p) { p=frac(p*.3183099+.1);p*=17;return frac(p.x*p.y*p.z*(p.x+p.y+p.z)); }
            float noise(float3 p) { float3 i=floor(p),f=frac(p); f=f*f*(3-2*f); return lerp(lerp(lerp(hash(i),hash(i+float3(1,0,0)),f.x),lerp(hash(i+float3(0,1,0)),hash(i+float3(1,1,0)),f.x),f.y),lerp(lerp(hash(i+float3(0,0,1)),hash(i+float3(1,0,1)),f.x),lerp(hash(i+float3(0,1,1)),hash(i+float3(1,1,1)),f.x),f.y),f.z); }
            float fbm(float3 p) { return noise(p)*.55+noise(p*2.03)*.28+noise(p*4.13)*.12+noise(p*8.07)*.05; }
            V vert(A a) { V o; o.w=TransformObjectToWorld(a.p.xyz);o.p=TransformWorldToHClip(o.w);o.n=TransformObjectToWorldNormal(a.n);o.obj=normalize(a.p.xyz);return o; }
            half4 frag(V i):SV_Target
            {
                float3 n=normalize(i.n),v=normalize(_WorldSpaceCameraPos-i.w);
                float land=smoothstep(.50,.535,fbm(i.obj*4.3+float3(3,1,5)));
                float ice=smoothstep(.82,.97,abs(i.obj.y));
                float clouds=smoothstep(.53,.68,fbm(i.obj*11+float3(4,7,3)));
                float3 c=lerp(float3(.016,.064,.14),float3(.10,.18,.16),land);
                c=lerp(c,float3(.63,.75,.80),saturate(ice+clouds*.8));
                float light=smoothstep(-.2,.8,dot(n,normalize(float3(-.7,.6,-.5))));
                c*=.10+light*.95;
                float rim=pow(1-saturate(dot(n,v)),5);
                c+=float3(.045,.24,.48)*rim*(.35+light*.7);
                return half4(c,1);
            }
            ENDHLSL
        }
    }
}
