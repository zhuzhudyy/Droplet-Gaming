Shader "DropletPrototype/NarrativeCombat/LaserChrome"
{
    Properties
    {
        [MainColor] _BaseColor("Cold silver reflectance", Color) = (.82,.86,.9,1)
        _Metallic("Metallic", Range(0,1)) = 1
        _Smoothness("Mirror smoothness", Range(.8,1)) = .985
        _ReflectionStrength("Probe reflection exposure", Range(1,3)) = 1.45
        _SunReflectionStrength("Finite solar disc reflection", Range(0,8)) = 2.8
        _SunAngularRadius("Solar reflection angular radius (radians)", Range(.002,.04)) = .006
        _ExplosionReflectionStrength("Reactor burst reflection", Range(0,8)) = 2.6
        _ExplosionGrazingStrength("Third-person source wrap", Range(0,3)) = 1.1
        _ReactorReflectionStrength("Operating reactor reflection", Range(0,4)) = 1.1
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "UniversalMaterialType"="Lit" "RenderType"="Opaque" "Queue"="Geometry" }
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        CBUFFER_START(UnityPerMaterial)
            float4 _BaseColor;
            float _Metallic, _Smoothness, _ReflectionStrength, _SunReflectionStrength;
            float _SunAngularRadius, _ExplosionReflectionStrength, _ReactorReflectionStrength, _ExplosionGrazingStrength;
        CBUFFER_END
        // Per-renderer MPB data: only the unique player gets this bounded response.
        float4 _BurstPositions[4];
        float4 _BurstColors[4];
        float4 _ReactorPositions[3];
        float4 _ReactorColors[3];
        float _BurstCount, _ReactorCount;
        float4 _LaserContactPositions[4];
        float4 _LaserContactColors[4];
        float4 _LaserContactIncoming[4];
        float _LaserContactCount;

        struct Attributes
        {
            float4 positionOS : POSITION;
            float3 normalOS : NORMAL;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };
        struct Varyings
        {
            float4 positionCS : SV_POSITION;
            float3 positionWS : TEXCOORD0;
            float3 normalWS : TEXCOORD1;
            UNITY_VERTEX_INPUT_INSTANCE_ID
            UNITY_VERTEX_OUTPUT_STEREO
        };
        Varyings ChromeVertex(Attributes input)
        {
            Varyings output = (Varyings)0;
            UNITY_SETUP_INSTANCE_ID(input);
            UNITY_TRANSFER_INSTANCE_ID(input, output);
            UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
            output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
            output.positionCS = TransformWorldToHClip(output.positionWS);
            output.normalWS = TransformObjectToWorldNormal(input.normalOS);
            return output;
        }

        // An analytic reflection of an extended luminous source. The normal and
        // camera define a real world reflected ray, so highlights slide with both
        // object and camera motion. This is not an albedo tint or fixed UV decal.
        // Radius is the pool's influence radius; its visible fire volume is ~28%.
        float3 ReflectedSource(float3 ray, float3 positionWS, float4 source, float4 light)
        {
            float3 delta = source.xyz - positionWS;
            float distanceSquared = max(dot(delta, delta), .01);
            float inverseDistance = rsqrt(distanceSquared);
            float angularRadius = clamp(light.w * .28 * inverseDistance, .008, .72);
            float alignment = clamp(dot(ray, delta * inverseDistance), -1, 1);
            float pixelWidth = max(fwidth(alignment), .00002);
            float width = max(1 - cos(angularRadius), pixelWidth * 1.2);
            float normalizedAngle = max(0, 1 - alignment) / width;
            float core = exp2(-normalizedAngle * 4.2);
            float envelope = exp2(-normalizedAngle * .8);
            float radiusSquared = max(light.w * light.w, .01);
            float attenuation = rcp(1 + distanceSquared / (radiusSquared * 2.25));
            // A compact white-hot kernel inside the colored envelope gives a
            // reflected fire volume rather than a uniform colored point glint.
            float3 whiteHot = lerp(light.rgb, float3(1.0, .96, .88), .66);
            return (light.rgb * envelope * .68 + whiteHot * core * 1.35)
                * min(source.w, 3.0) * attenuation;
        }

        float3 ReflectedBurstGrazing(float3 normal, float3 view, float3 positionWS, float4 source, float4 light)
        {
            float3 delta = source.xyz - positionWS;
            float distanceSquared = max(dot(delta, delta), .01);
            float3 toSource = delta * rsqrt(distanceSquared);
            float noV = saturate(dot(normal, view));
            float bandCoordinate = (noV - .30) / .24;
            float band = exp2(-bandCoordinate * bandCoordinate * 1.8) * smoothstep(.015, .10, noV);
            // Source location controls the brighter side of the curved band. For
            // a blast exactly behind the camera, the projected source is singular;
            // use a stable world-up projection for the unresolved luminous haze.
            float3 projectedSource = toSource - view * dot(toSource, view);
            float3 projectedUp = float3(0, 1, 0) - view * view.y;
            projectedSource += projectedUp * .12;
            float3 sourceSide = SafeNormalize(projectedSource);
            float sideWeight = .3 + .7 * smoothstep(-.45, .6, dot(normal, sourceSide));
            float sourceFacing = smoothstep(-.8, .6, dot(normal, toSource));
            float attenuation = rcp(1 + distanceSquared / max(light.w * light.w * 2.25, .01));
            float core = band * band * band * band;
            float3 hotKernel = lerp(light.rgb, float3(1, .94, .82), .7);
            // ACES desaturates the white-hot radiance. Keep a clearly orange
            // outer fire reflection without increasing luminance; the white core
            // and the blue-white pre-burst response retain their original colors.
            float firePhase = smoothstep(.08, .45, light.r - light.b);
            float3 shoulder = lerp(light.rgb, float3(light.r, light.g * .55, light.b * .32), firePhase);
            return (shoulder * band + hotKernel * core * .32) * sideWeight * sourceFacing
                * attenuation * min(source.w, 3.0);
        }
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForwardOnly" }
            ZWrite On
            Cull Back
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex ChromeVertex
            #pragma fragment ChromeFragment
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_ATLAS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_instancing
            half4 ChromeFragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float3 normal = normalize(input.normalWS);
                float3 view = SafeNormalize(GetWorldSpaceViewDir(input.positionWS));
                float3 reflectedRay = reflect(-view, normal);
                InputData lighting = (InputData)0;
                lighting.positionWS = input.positionWS;
                lighting.normalWS = normal;
                lighting.viewDirectionWS = view;
                lighting.shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                lighting.bakedGI = SampleSH(normal);
                lighting.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
                lighting.shadowMask = half4(1, 1, 1, 1);

                float fresnel = Pow4(1 - saturate(dot(normal, view)));
                float3 reflectance = lerp(_BaseColor.rgb, float3(1, 1, 1), fresnel);
                float3 reflectedRadiance = 0;
                // The extremely long tapered body presents nearly grazing normals
                // to the chase camera: a geometrically exact rear blast reflection
                // occupies only a few tip pixels. Model its unresolved luminous
                // haze with a bounded grazing response. This explicit art
                // approximation affects a curved visible band, not the whole
                // albedo; source world direction selects its bright side and the
                // explosion pool remains its only lifetime/intensity authority.
                [unroll] for (int burst = 0; burst < 4; burst++)
                    if (burst < _BurstCount)
                    {
                        reflectedRadiance += ReflectedSource(reflectedRay, input.positionWS,
                            _BurstPositions[burst], _BurstColors[burst]) * _ExplosionReflectionStrength;
                        reflectedRadiance += ReflectedBurstGrazing(normal, view, input.positionWS,
                            _BurstPositions[burst], _BurstColors[burst])
                            * (_ExplosionReflectionStrength * _ExplosionGrazingStrength);
                    }
                [unroll] for (int reactor = 0; reactor < 3; reactor++)
                    if (reactor < _ReactorCount)
                        reflectedRadiance += ReflectedSource(reflectedRay, input.positionWS,
                            _ReactorPositions[reactor], _ReactorColors[reactor]) * _ReactorReflectionStrength;

                // A finite, contact-local optical source on the preserved smooth
                // surface. Its location follows only the short material afterglow;
                // the independent pulse keeps its original complete world path.
                [unroll] for (int contact = 0; contact < 4; contact++)
                    if (contact < _LaserContactCount)
                    {
                        float4 spot = _LaserContactPositions[contact];
                        float3 delta = input.positionWS - spot.xyz;
                        float distanceSquared = dot(delta, delta) / max(spot.w * spot.w, .000001);
                        float footprint = exp2(-distanceSquared * 5.0);
                        float alignment = saturate(dot(reflectedRay, _LaserContactIncoming[contact].xyz));
                        float mirror = pow(alignment, 96);
                        // Compact unresolved glint makes the contact legible from
                        // the ordinary chase view without changing the full albedo.
                        float glint = exp2(-distanceSquared * 32) * .3;
                        float4 pulse = _LaserContactColors[contact];
                        reflectedRadiance += (pulse.rgb * footprint * (mirror * 2.4 + .12)
                            + float3(2.2, 2.3, 2.5) * glint) * pulse.w;
                    }

                Light sun = GetMainLight(lighting.shadowCoord);
                float sunAlignment = dot(reflectedRay, sun.direction);
                float discWidth = max(1 - cos(_SunAngularRadius), max(fwidth(sunAlignment), .000015));
                float sunAngle = max(0, 1 - sunAlignment) / discWidth;
                reflectedRadiance += sun.color * sun.shadowAttenuation * _SunReflectionStrength
                    * (exp2(-sunAngle * 2.5) + exp2(-sunAngle * .14) * .045);

                // Keep URP's probe projection/blending/Forward+ atlas and standard
                // microfacet direct lighting. A modest exposure lift helps the
                // existing dark space cubemap without painting the body silver.
                if (_ReflectionStrength > 1.001)
                    reflectedRadiance += GlossyEnvironmentReflection(reflectedRay, input.positionWS,
                        1 - _Smoothness, 1, lighting.normalizedScreenSpaceUV) * (_ReflectionStrength - 1);
                SurfaceData surface = (SurfaceData)0;
                surface.albedo = _BaseColor.rgb;
                surface.metallic = _Metallic;
                surface.specular = half3(.04, .04, .04);
                surface.smoothness = _Smoothness;
                surface.normalTS = half3(0, 0, 1);
                surface.occlusion = 1;
                surface.alpha = 1;
                // Custom radiance is view-dependent reflected light. Supplying it
                // through emission avoids a second BRDF convolution; it is NOT a
                // self-illuminating, uniform material emissive color.
                surface.emission = min(reflectedRadiance, float3(14, 14, 14)) * reflectance;
                return UniversalFragmentPBR(lighting, surface);
            }
            ENDHLSL
        }
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            ZWrite On
            ColorMask R
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex ChromeVertex
            #pragma fragment DepthFragment
            #pragma multi_compile_instancing
            half4 DepthFragment(Varyings input) : SV_Target { return input.positionCS.z; }
            ENDHLSL
        }
        Pass
        {
            Name "DepthNormalsOnly"
            Tags { "LightMode"="DepthNormalsOnly" }
            ZWrite On
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex ChromeVertex
            #pragma fragment NormalFragment
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            #pragma multi_compile_instancing
            half4 NormalFragment(Varyings input) : SV_Target
            {
                float3 normal = normalize(input.normalWS);
                #if defined(_GBUFFER_NORMALS_OCT)
                    float2 encoded = PackNormalOctQuadEncode(normal) * .5 + .5;
                    return half4(PackFloat2To888(saturate(encoded)), 0);
                #else
                    return half4(normal, 0);
                #endif
            }
            ENDHLSL
        }
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            ZWrite On
            ZTest LEqual
            ColorMask 0
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex ShadowVertex
            #pragma fragment ShadowFragment
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #pragma multi_compile_instancing
            float3 _LightDirection, _LightPosition;
            float4 ShadowVertex(Attributes input) : SV_POSITION
            {
                UNITY_SETUP_INSTANCE_ID(input);
                float3 world = TransformObjectToWorld(input.positionOS.xyz);
                float3 normal = TransformObjectToWorldNormal(input.normalOS);
                #if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
                    float3 direction = normalize(_LightPosition - world);
                #else
                    float3 direction = _LightDirection;
                #endif
                float4 clipPosition = TransformWorldToHClip(ApplyShadowBias(world, normal, direction));
                #if UNITY_REVERSED_Z
                    clipPosition.z = min(clipPosition.z, UNITY_NEAR_CLIP_VALUE * clipPosition.w);
                #else
                    clipPosition.z = max(clipPosition.z, UNITY_NEAR_CLIP_VALUE * clipPosition.w);
                #endif
                return clipPosition;
            }
            half4 ShadowFragment() : SV_Target { return 0; }
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
