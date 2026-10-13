Shader "Hortensia/Retro Lit"
{
    Properties
    {
        [MainTexture] _BaseMap("Base Map", 2D) = "white" {}
        [MainColor] _BaseColor("Base Color", Color) = (1, 1, 1, 1)

        [Header(Retro Geometry)]
        _VertexSnapStrength("Vertex Snap Strength", Range(0, 1)) = 1
        _VertexSnapGrid("Vertex Snap Grid (XY Pixels)", Vector) = (256, 256, 0, 0)
        _AffineStrength("Affine UV Strength", Range(0, 100)) = 1

        [Header(Retro Color)]
        _AmbientStrength("Ambient Strength", Range(0, 2)) = 1
        _LightSteps("Light Quantization Steps", Range(2, 64)) = 16
        _MipBias("Texture Mip Bias (Anti-Shimmer)", Range(0, 5)) = 1.5

        [Header(Fog)]
        [Toggle] _UseAlternateFog("Use Alternate Fog", Float) = 0

        [Enum(UnityEngine.Rendering.CullMode)] _Cull("Cull", Float) = 2
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
            "RenderPipeline" = "UniversalPipeline"
            "UniversalMaterialType" = "SimpleLit"
            "IgnoreProjector" = "True"
        }
        LOD 200

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        TEXTURE2D(_BaseMap);
        SAMPLER(sampler_BaseMap);

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4 _BaseColor;
            float4 _VertexSnapGrid;
            half _VertexSnapStrength;
            half _AffineStrength;
            half _AmbientStrength;
            half _LightSteps;
            half _MipBias;
            half _UseAlternateFog;
            half _Cull;
                CBUFFER_END

        // Per-renderer via MaterialPropertyBlock: DEVE stare fuori da UnityPerMaterial.
        float _PS1DistanceFade;

        // Bayer 4x4 per dissolve ditherato: disponibile in tutti i pass del subshader.
        static const float PS1Bayer4x4[16] = {
             0.5,  8.5,  2.5, 10.5,
            12.5,  4.5, 14.5,  6.5,
             3.5, 11.5,  1.5,  9.5,
            15.5,  7.5, 13.5,  5.5 };

        // Alternate fog profile, for materials with "Use Alternate Fog"
        // checked (set globally by PS1Fog.cs). Main fog is untouched -
        // Unity's own RenderSettings-driven MixFog(), same as always.
        float _PS1AltFogStart;
        float _PS1AltFogEnd;
        half4 _PS1AltFogColor;

        float4 SnapClipPosition(float4 positionCS)
        {
            float2 grid = max(_VertexSnapGrid.xy, float2(1.0, 1.0));
            float safeW = abs(positionCS.w) < 0.00001
                ? (positionCS.w < 0.0 ? -0.00001 : 0.00001)
                : positionCS.w;
            float2 ndc = positionCS.xy / safeW;
            float2 pixel = (ndc * 0.5 + 0.5) * grid;
            float2 snappedPixel = floor(pixel + 0.5);
            float2 snappedNdc = (snappedPixel / grid) * 2.0 - 1.0;

            positionCS.xy = lerp(
                positionCS.xy,
                snappedNdc * positionCS.w,
                saturate(_VertexSnapStrength));
            return positionCS;
        }
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForwardOnly" }

            Cull [_Cull]
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex RetroLitVertex
            #pragma fragment RetroLitFragment

            #pragma multi_compile_instancing
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fragment _ _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _LIGHT_COOKIES

            // Force the vertex-lit path is NOT used for additional lights:
            // under Forward+ (_CLUSTER_LIGHT_LOOP), additional lights are
            // organised into screen-space clusters, which only exist after
            // rasterization - URP's VertexLighting() helper does not support
            // that combination and fails to compile. Main light + ambient are
            // vertex-lit below (the dominant PS1-look contributor); additional
            // (point/spot) lights stay per-pixel, evaluated in the fragment
            // stage further down, since that is the only path Forward+ supports
            // for them.

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                float2 perspectiveUV : TEXCOORD2;
                float2 affineUVTimesW : TEXCOORD3;
                float2 affineWAndFog : TEXCOORD4;
                half3 lighting : TEXCOORD5;
                float4 shadowCoord : TEXCOORD6;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            half3 EvaluateDiffuseLight(Light light, half3 normalWS)
            {
                half diffuse = saturate(dot(normalWS, light.direction));
                return light.color
                    * diffuse
                    * light.distanceAttenuation
                    * light.shadowAttenuation;
            }

            // Ambient is vertex-computed and interpolated, preserving the
            // faceted PS1 look while allowing the main light to be shadowed
            // independently per pixel below. Do not multiply the ambient term
            // by the main-light shadow: that made enclosed playable routes
            // collapse to black even when ambient and point fills were present.
            half3 EvaluateVertexLighting(float3 positionWS, half3 normalWS)
            {
                return max(SampleSH(normalWS) * _AmbientStrength, 0.0h);
            }

            Varyings RetroLitVertex(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                VertexPositionInputs positions = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normals = GetVertexNormalInputs(input.normalOS);
                half3 normalWS = NormalizeNormalPerVertex(normals.normalWS);
                float2 transformedUV = TRANSFORM_TEX(input.uv, _BaseMap);
                float4 shadowCoord = GetShadowCoord(positions);

                output.positionCS = SnapClipPosition(positions.positionCS);
                output.positionWS = positions.positionWS;
                output.normalWS = normalWS;
                output.perspectiveUV = transformedUV;

                // Perspective-correct interpolation divides varyings by W.
                // Multiplying both UV and a companion value by clip-space W,
                // then dividing them in the fragment stage, cancels that
                // correction and reconstructs affine screen-space UVs.
                output.affineUVTimesW = transformedUV * output.positionCS.w;
                output.affineWAndFog.x = output.positionCS.w;
                output.affineWAndFog.y = ComputeFogFactor(positions.positionCS.z);
                output.lighting = EvaluateVertexLighting(positions.positionWS, normalWS);
                output.shadowCoord = shadowCoord;
                return output;
            }

                        half4 RetroLitFragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                // Bayer dissolve: dissolve ditherato negli ultimi metri prima del
                // cutoff. fade=0 -> non scarta niente; fade=1 -> scarta tutto.
                // Applicato qui (solo pass ForwardLit) per non bucherellare le ombre
                // o il depth pre-pass, che creerebbero inconsistenze di silhouette.
                {
                    float dith = PS1Bayer4x4[(uint(input.positionCS.x) & 3u) * 4u + (uint(input.positionCS.y) & 3u)] / 16.0;
                    clip(dith - _PS1DistanceFade);
                }

                float safeAffineW = max(abs(input.affineWAndFog.x), 0.00001);
                float2 affineUV = input.affineUVTimesW / safeAffineW;
                float2 uv = lerp(
                    input.perspectiveUV,
                    affineUV,
                    saturate(_AffineStrength));
                half4 albedo = SAMPLE_TEXTURE2D_BIAS(_BaseMap, sampler_BaseMap, uv, _MipBias) * _BaseColor;

                half3 normalWS = NormalizeNormalPerPixel(input.normalWS);
                half3 lighting = input.lighting;

                // Evaluate the directional light with its own shadow attenuation.
                // Ambient light remains available in shadowed interiors.
                Light mainLight = GetMainLight(input.shadowCoord);
                lighting += EvaluateDiffuseLight(mainLight, normalWS);

                #if USE_CLUSTER_LIGHT_LOOP
                    // Forward+ organises additional lights into screen-space
                    // clusters that only exist after rasterization, so they
                    // must be evaluated here, per pixel - not in the vertex
                    // stage. Directional additional lights first, then the
                    // punctual-light cluster.
                    [loop] for (uint lightIndex = 0u;
                        lightIndex < min(URP_FP_DIRECTIONAL_LIGHTS_COUNT, MAX_VISIBLE_LIGHTS);
                        ++lightIndex)
                    {
                        Light additionalLight = GetAdditionalLight(
                            lightIndex,
                            input.positionWS,
                            half4(1.0h, 1.0h, 1.0h, 1.0h));
                        lighting += EvaluateDiffuseLight(additionalLight, normalWS);
                    }

                    ClusterIterator clusterIterator = ClusterInit(
                        GetNormalizedScreenSpaceUV(input.positionCS),
                        input.positionWS,
                        0);
                    uint clusteredLightIndex;
                    [loop] while (ClusterNext(clusterIterator, clusteredLightIndex))
                    {
                        clusteredLightIndex += URP_FP_DIRECTIONAL_LIGHTS_COUNT;
                        Light additionalLight = GetAdditionalLight(
                            clusteredLightIndex,
                            input.positionWS,
                            half4(1.0h, 1.0h, 1.0h, 1.0h));
                        lighting += EvaluateDiffuseLight(additionalLight, normalWS);
                    }
                #elif defined(_ADDITIONAL_LIGHTS)
                    uint additionalLightCount = (uint)GetAdditionalLightsCount();
                    for (uint lightIndex = 0u; lightIndex < additionalLightCount; ++lightIndex)
                    {
                        Light additionalLight = GetAdditionalLight(
                            lightIndex,
                            input.positionWS,
                            half4(1.0h, 1.0h, 1.0h, 1.0h));
                        lighting += EvaluateDiffuseLight(additionalLight, normalWS);
                    }
                #endif

                // Quantize the fully combined lighting (vertex-computed main
                // light/ambient plus any per-pixel additional lights) into
                // flat steps. Doing this after combining, not before, is what
                // makes point/spot lights band the same way as the main light
                // instead of staying smooth.
                half steps = max(_LightSteps, 2.0h);
                lighting = floor(lighting * steps + 0.5h) / steps;

                // Imported greybox rooms frequently provide no usable ambient
                // probe. A small warm floor retains dark corners but keeps
                // doors, floors, and collision boundaries navigable instead of
                // rendering them as featureless black.
                lighting = max(lighting, half3(0.72h, 0.62h, 0.52h));
                lighting = saturate(lighting);

                half3 color = albedo.rgb * lighting;

                // Main fog: unchanged, exactly as before - Unity's built-in
                // RenderSettings-driven MixFog(), untouched.
                //
                // Materials with "Use Alternate Fog" checked skip that and
                // blend toward a second, independent profile instead (its
                // own start/end/colour), computed manually here since
                // MixFog() only supports one global profile at a time.
                float playerDistance = distance(input.positionWS, GetCameraPositionWS());
                if (_UseAlternateFog < 0.5h)
                {
                    color = MixFog(color, input.affineWAndFog.y);
                }
                else
                {
                    float fogRange = max(_PS1AltFogEnd - _PS1AltFogStart, 0.001);
                    float fogFactor = saturate((playerDistance - _PS1AltFogStart) / fogRange);
                    color = lerp(color, _PS1AltFogColor.rgb, fogFactor);
                }

                // The protagonist carries a modest practical fill. Applying it
                // after fog keeps nearby floors, doors, and collision edges
                // readable even in imported rooms whose fog profile is much
                // darker than their authored point lights.
                half explorationFill = saturate(1.0h - max(playerDistance - 1.0, 0.0) / 11.0);
                color = max(color, albedo.rgb * half3(0.75h, 0.66h, 0.55h) * explorationFill);

                return half4(color, albedo.a);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            Cull [_Cull]
            ZWrite On
            ZTest LEqual
            ColorMask 0

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex RetroShadowVertex
            #pragma fragment RetroShadowFragment
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            struct ShadowAttributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct ShadowVaryings
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            ShadowVaryings RetroShadowVertex(ShadowAttributes input)
            {
                ShadowVaryings output = (ShadowVaryings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);

                #if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
                    float3 lightDirectionWS = normalize(_LightPosition - positionWS);
                #else
                    float3 lightDirectionWS = _LightDirection;
                #endif

                output.positionCS = TransformWorldToHClip(
                    ApplyShadowBias(positionWS, normalWS, lightDirectionWS));
                output.positionCS = ApplyShadowClamping(output.positionCS);
                return output;
            }

            half4 RetroShadowFragment(ShadowVaryings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                return 0.0h;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            Cull [_Cull]
            ZWrite On
            ZTest LEqual
            ColorMask R

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex RetroDepthVertex
            #pragma fragment RetroDepthFragment
            #pragma multi_compile_instancing

            struct DepthAttributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct DepthVaryings
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            DepthVaryings RetroDepthVertex(DepthAttributes input)
            {
                DepthVaryings output = (DepthVaryings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                output.positionCS = SnapClipPosition(
                    TransformObjectToHClip(input.positionOS.xyz));
                return output;
            }

            half4 RetroDepthFragment(DepthVaryings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                return 0.0h;
            }
            ENDHLSL
        }
    }

    FallBack Off
}
