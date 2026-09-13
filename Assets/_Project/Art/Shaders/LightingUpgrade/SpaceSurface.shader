Shader "DropletPrototype/Lighting/SpaceSurface"
{
    Properties
    {
        _BaseColor("Surface tint",Color)=(.4,.45,.5,1)
        _BaseMap("Surface map",2D)="white"{}
        _Metallic("Metallic",Range(0,1))=.9
        _Smoothness("Smoothness",Range(0,1))=.7
        _Variation("Surface variation",Range(0,1))=.12
        _SurfaceKind("0 Armor / 1 Rock / 2 Earth",Float)=0
        [HDR]_EmissionColor("Emission",Color)=(0,0,0,0)
    }
    SubShader
    {
        Tags{"RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry"}
        Pass
        {
            Name "ForwardLit"
            Tags{"LightMode"="UniversalForward"}
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.5
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_ATLAS
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            TEXTURE2D(_BaseMap);SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial)
            float4 _BaseColor,_BaseMap_ST,_EmissionColor;
            float _Metallic,_Smoothness,_Variation,_SurfaceKind;
            CBUFFER_END
            float SurfaceHash(float3 p)
            {
                p=frac(p*.1031);
                p+=dot(p,p.yzx+33.33);
                return frac((p.x+p.y)*p.z);
            }
            // Smooth value and its analytic object-space gradient from the same
            // eight corners. No textures, extra gradient samples or periodic sine.
            float4 SurfaceNoise(float3 p)
            {
                float3 cell=floor(p),f=frac(p);
                float3 u=f*f*(3-2*f),du=6*f*(1-f);
                float a=SurfaceHash(cell),b=SurfaceHash(cell+float3(1,0,0));
                float c=SurfaceHash(cell+float3(0,1,0)),d=SurfaceHash(cell+float3(1,1,0));
                float e=SurfaceHash(cell+float3(0,0,1)),f1=SurfaceHash(cell+float3(1,0,1));
                float g=SurfaceHash(cell+float3(0,1,1)),h=SurfaceHash(cell+float3(1,1,1));
                float x00=lerp(a,b,u.x),x10=lerp(c,d,u.x);
                float x01=lerp(e,f1,u.x),x11=lerp(g,h,u.x);
                float y0=lerp(x00,x10,u.y),y1=lerp(x01,x11,u.y);
                float3 gradient=float3(
                    lerp(lerp(b-a,d-c,u.y),lerp(f1-e,h-g,u.y),u.z)*du.x,
                    lerp(x10-x00,x11-x01,u.z)*du.y,
                    (y1-y0)*du.z);
                // Fade unresolved cells to their mean before they become crawling
                // high-frequency grain at distant LODs or grazing camera angles.
                float3 footprint=fwidth(p);
                float bandwidth=1-smoothstep(.35,1.2,max(footprint.x,max(footprint.y,footprint.z)));
                return float4(lerp(.5,lerp(y0,y1,u.z),bandwidth),gradient*bandwidth);
            }
            struct A{float4 positionOS:POSITION;float3 normalOS:NORMAL;float2 uv:TEXCOORD0;UNITY_VERTEX_INPUT_INSTANCE_ID};
            struct V{float4 positionCS:SV_POSITION;float3 positionWS:TEXCOORD0;float3 normalWS:TEXCOORD1;float3 positionOS:TEXCOORD2;float2 uv:TEXCOORD3;UNITY_VERTEX_INPUT_INSTANCE_ID UNITY_VERTEX_OUTPUT_STEREO};
            V vert(A a){V o=(V)0;UNITY_SETUP_INSTANCE_ID(a);UNITY_TRANSFER_INSTANCE_ID(a,o);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);o.positionWS=TransformObjectToWorld(a.positionOS.xyz);o.positionCS=TransformWorldToHClip(o.positionWS);o.normalWS=TransformObjectToWorldNormal(a.normalOS);o.positionOS=a.positionOS.xyz;o.uv=TRANSFORM_TEX(a.uv,_BaseMap);return o;}
            half4 frag(V i):SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                float3 n=normalize(i.normalWS);float3 view=SafeNormalize(_WorldSpaceCameraPos-i.positionWS);
                half3 albedo=_BaseColor.rgb;
                half smoothness=_Smoothness;
                half metallic=_Metallic;half3 emission=_EmissionColor.rgb;
                if(_SurfaceKind>1.5)
                {
                    half3 map=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,i.uv).rgb;
                    float cloud=smoothstep(.38,.8,min(map.r,min(map.g,map.b)));
                    float ocean=(1-smoothstep(.015,.07,map.g-map.b*.55))*(1-cloud);
                    albedo=map*_BaseColor.rgb;smoothness=lerp(.22,.86,ocean);metallic=0;
                    Light sun=GetMainLight();float rim=pow(1-saturate(dot(n,view)),5);
                    emission+=half3(.012,.055,.11)*rim*saturate(dot(n,sun.direction)+.18);
                }
                else if(_SurfaceKind>.5)
                {
                    float4 weathering=SurfaceNoise(i.positionOS*4.7);
                    float4 granules=SurfaceNoise(i.positionOS*37.1+float3(17.4,9.2,31.1));
                    albedo=_BaseColor.rgb*(.82+_Variation*((weathering.x-.5)*.7+(granules.x-.5)*.25));
                    smoothness=clamp(_Smoothness+(granules.x-.5)*.025,.06,.16);metallic=0;
                    // Both the height gradient and tangent projection are in object
                    // space; transform the finished normal once. This keeps rotated
                    // and non-uniformly scaled rock instances' weathering attached.
                    float3 normalOS=TransformWorldToObjectNormal(n);
                    float3 gradientOS=weathering.yzw*(4.7*.012)+granules.yzw*(37.1*.0025);
                    gradientOS-=normalOS*dot(gradientOS,normalOS);
                    n=TransformObjectToWorldNormal(normalize(normalOS-gradientOS));
                }
                else
                {
                    float patches=SurfaceNoise(i.positionOS*.67).x-.5;
                    float grain=SurfaceNoise(i.positionOS*9.73+float3(5.7,13.2,2.8)).x-.5;
                    albedo*=1+_Variation*(patches*.45+grain*.18);
                    smoothness=saturate(_Smoothness+_Variation*(patches*.12+grain*.10));
                }
                InputData input=(InputData)0;input.positionWS=i.positionWS;input.normalWS=n;input.viewDirectionWS=view;
                input.shadowCoord=TransformWorldToShadowCoord(i.positionWS);input.bakedGI=SampleSH(n);input.normalizedScreenSpaceUV=GetNormalizedScreenSpaceUV(i.positionCS);input.shadowMask=half4(1,1,1,1);
                SurfaceData surface=(SurfaceData)0;surface.albedo=albedo;surface.metallic=metallic;surface.specular=half3(.04,.04,.04);surface.smoothness=smoothness;surface.normalTS=half3(0,0,1);surface.emission=emission;surface.occlusion=1;surface.alpha=1;
                return UniversalFragmentPBR(input,surface);
            }
            ENDHLSL
        }
        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
        UsePass "Universal Render Pipeline/Lit/DepthNormals"
    }
}
