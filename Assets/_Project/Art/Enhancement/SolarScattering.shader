Shader "DropletPrototype/Enhancement/LocalSolarScattering"
{
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" }
        Cull Off ZWrite Off ZTest Always
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
        float4 _CoronaSphere, _CoronaParameters, _CoronaColor;
        float4x4 _DepthInvVP;
        float3 WorldAt(float2 uv, float depth)
        {
            #if !UNITY_REVERSED_Z
            depth = lerp(UNITY_NEAR_CLIP_VALUE, 1, depth);
            #endif
            return ComputeWorldSpacePosition(uv, depth, _DepthInvVP);
        }
        float2 IntersectSphere(float3 origin, float3 ray)
        {
            float3 offset = origin - _CoronaSphere.xyz;
            float b = dot(offset, ray), c = dot(offset, offset) - _CoronaSphere.w * _CoronaSphere.w;
            float h = b*b-c;
            if(h <= 0) return float2(1,0);
            h=sqrt(h); return float2(max(0,-b-h),-b+h);
        }
        half4 Integrate(Varyings input) : SV_Target
        {
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
            float depth = SampleSceneDepth(input.texcoord);
            float3 end = WorldAt(input.texcoord, depth);
            float3 ray = normalize(end - _WorldSpaceCameraPos);
            float2 interval = IntersectSphere(_WorldSpaceCameraPos, ray);
            interval.y = min(interval.y, length(end - _WorldSpaceCameraPos));
            if(interval.y <= interval.x) return 0;
            int count=(int)_CoronaParameters.z;
            float stepLength=(interval.y-interval.x)/count;
            float sum=0, transmittance=1;
            [loop] for(int i=0;i<count;i++)
            {
                float3 p=_WorldSpaceCameraPos+ray*(interval.x+(i+.5)*stepLength);
                float radius=length(p-_CoronaSphere.xyz);
                float shell=saturate((radius-_CoronaParameters.x)/max(1,_CoronaSphere.w-_CoronaParameters.x));
                float density=exp(-shell*5)*smoothstep(1, .7, shell);
                float extinction=density*_CoronaParameters.y*stepLength/max(1,_CoronaParameters.x);
                float3 lightDir=normalize(_CoronaSphere.xyz-p);
                float cosine=dot(-ray, -lightDir), g=.35;
                float phase=(1-g*g)/max(.2,pow(max(.001,1+g*g-2*g*cosine),1.5));
                sum+=transmittance*(1-exp(-extinction))*phase;
                transmittance*=exp(-extinction);
            }
            return half4(_CoronaColor.rgb*min(sum,.15), 0);
        }
        half4 Composite(Varyings input) : SV_Target
        {
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
            float3 end=WorldAt(input.texcoord,SampleSceneDepth(input.texcoord));
            float2 interval=IntersectSphere(_WorldSpaceCameraPos,normalize(end-_WorldSpaceCameraPos));
            // Full-resolution depth rejects bilinear leakage across foreground hull edges.
            if(interval.y<=interval.x || length(end-_WorldSpaceCameraPos)<=interval.x) return 0;
            return half4(SAMPLE_TEXTURE2D_X(_BlitTexture,sampler_LinearClamp,input.texcoord).rgb,0);
        }
        ENDHLSL
        Pass
        {
            Name "RayIntegration"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Integrate
            ENDHLSL
        }
        Pass
        {
            Name "AdditiveComposite"
            Blend One One
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Composite
            ENDHLSL
        }
    }
}
