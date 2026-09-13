Shader "DropletPrototype/Fleet2000Sun/SolarPhotosphere"
{
    Properties
    {
        [HDR] _CoreColor("White-hot photosphere radiance", Color) = (2.4,1.9,1.2,1)
        [HDR] _RimColor("Warm-gold limb radiance", Color) = (1.65,.68,.14,1)
        _Granulation("Surface granulation", Range(0,.85)) = .65
        _CellScale("Granulation scale", Range(12,180)) = 94
        _EvolutionSpeed("Slow surface evolution", Range(0,.1)) = .014
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        CBUFFER_START(UnityPerMaterial)
        float4 _CoreColor, _RimColor;
        float _Granulation, _CellScale, _EvolutionSpeed;
        CBUFFER_END
        struct Attributes { float4 positionOS:POSITION; float3 normalOS:NORMAL; };
        struct Varyings
        {
            float4 positionCS:SV_POSITION;
            float3 positionWS:TEXCOORD0;
            float3 normalWS:TEXCOORD1;
            float3 radialOS:TEXCOORD2;
        };
        Varyings SolarVertex(Attributes input)
        {
            Varyings output;
            output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
            output.positionCS = TransformWorldToHClip(output.positionWS);
            output.normalWS = TransformObjectToWorldNormal(input.normalOS);
            output.radialOS = input.normalOS;
            return output;
        }
        float SolarHash(float3 p)
        {
            p = frac(p * .1031);
            p += dot(p, p.yzx + 33.33);
            return frac((p.x + p.y) * p.z);
        }
        float SolarNoise(float3 p)
        {
            float3 cell = floor(p), f = frac(p);
            f = f * f * (3 - 2 * f);
            float a = lerp(SolarHash(cell), SolarHash(cell + float3(1,0,0)), f.x);
            float b = lerp(SolarHash(cell + float3(0,1,0)), SolarHash(cell + float3(1,1,0)), f.x);
            float c = lerp(SolarHash(cell + float3(0,0,1)), SolarHash(cell + float3(1,0,1)), f.x);
            float d = lerp(SolarHash(cell + float3(0,1,1)), SolarHash(cell + float3(1,1,1)), f.x);
            float resolved = 1 - smoothstep(.4, 1.4, length(fwidth(p)));
            return lerp(.5, lerp(lerp(a,b,f.y), lerp(c,d,f.y), f.z), resolved);
        }
        ENDHLSL
        Pass
        {
            Name "Photosphere"
            Tags { "LightMode"="UniversalForwardOnly" }
            Cull Back
            ZWrite On
            ZTest LEqual
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex SolarVertex
            #pragma fragment SolarFragment
            half4 SolarFragment(Varyings input):SV_Target
            {
                float mu = saturate(dot(normalize(input.normalWS), GetWorldSpaceNormalizeViewDir(input.positionWS)));
                float3 d = normalize(input.radialOS);
                float t = _Time.y * _EvolutionSpeed;
                // Two bounded value-noise samples; no textures, raymarch or
                // surface displacement. Unresolved detail fades before aliasing.
                float cells = SolarNoise(d * _CellScale + float3(t,t*.31,0));
                float region = SolarNoise(d * 9.7 + float3(13,7,21));
                // Keep the bulk of the photosphere below the flat shoulder of
                // ACES. Dark inter-granule lanes remain visible while sparse
                // white-hot kernels still provide genuine HDR bloom energy.
                float granules = lerp(1, .24 + 1.11 * smoothstep(.14,.86,cells), saturate(_Granulation / .65));
                float activeRegions = 1 - smoothstep(.55,.80,region) * .38;
                float coreWeight = smoothstep(.01,.53,pow(mu,.72));
                float3 radiance = lerp(_RimColor.rgb, _CoreColor.rgb, coreWeight);
                radiance *= granules * activeRegions * (.72 + .28 * mu);
                float hotKernels = smoothstep(.72,.91,cells) * coreWeight;
                radiance += float3(3.1,2.9,2.4) * hotKernels;
                return half4(radiance, 1);
            }
            ENDHLSL
        }
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            Cull Back
            ZWrite On
            ColorMask R
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex SolarVertex
            #pragma fragment SolarDepth
            half4 SolarDepth(Varyings input):SV_Target { return input.positionCS.z; }
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
