Shader "DropletPrototype/VisualUpgrade/SolarStar"
{
    Properties{[HDR]_BaseColor("Photosphere radiance",Color)=(7,4.8,2.1,1)}
    SubShader
    {
        Tags{"RenderPipeline"="UniversalPipeline" "RenderType"="Opaque"}
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial) float4 _BaseColor; CBUFFER_END
            struct A{float4 p:POSITION;float3 n:NORMAL;};
            struct V{float4 p:SV_POSITION;float3 n:TEXCOORD0;float3 w:TEXCOORD1;float3 d:TEXCOORD2;};
            V vert(A a){V o;o.w=TransformObjectToWorld(a.p.xyz);o.p=TransformWorldToHClip(o.w);o.n=TransformObjectToWorldNormal(a.n);o.d=a.n;return o;}
            float hash(float3 p){p=frac(p*.1031);p+=dot(p,p.yzx+33.33);return frac((p.x+p.y)*p.z);}
            float noise(float3 p)
            {
                float3 b=floor(p),f=frac(p);f=f*f*(3-2*f);
                return lerp(lerp(lerp(hash(b),hash(b+float3(1,0,0)),f.x),lerp(hash(b+float3(0,1,0)),hash(b+float3(1,1,0)),f.x),f.y),lerp(lerp(hash(b+float3(0,0,1)),hash(b+float3(1,0,1)),f.x),lerp(hash(b+float3(0,1,1)),hash(b+float3(1,1,1)),f.x),f.y),f.z);
            }
            half4 frag(V i):SV_Target
            {
                float mu=saturate(dot(normalize(i.n),GetWorldSpaceNormalizeViewDir(i.w)));
                float3 d=normalize(i.d);float t=_Time.y*.018;
                float cells=noise(d*84+float3(t,t*.3,0));
                float activeRegion=noise(d*9+float3(13,7,21));
                float resolved=1-smoothstep(.4,1.6,length(fwidth(d*84)));
                cells=lerp(.5,cells,resolved);
                float granular=(.24+.76*cells)*(1-.45*smoothstep(.60,.84,activeRegion));
                float limb=.11+.89*pow(mu,.62);
                float3 color=lerp(float3(1,.40,.09),float3(1,1,1),pow(mu,.5));
                return half4(_BaseColor.rgb*color*limb*granular,1);
            }
            ENDHLSL
        }
    }
}
