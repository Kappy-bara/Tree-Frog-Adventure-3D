// Tree Frog Adventure 3D - biome rock / prop surface.
//
// Built for the shared UNS_Color_Palette atlas that every rock, cliff, log and
// prop in the Emerald Forest uses. Instead of tinting (which just washes the
// atlas out), it takes the atlas LUMINANCE and remaps it through a three-stop
// colour ramp. One material swap turns forest stone into alpine granite or
// volcanic basalt, with no new textures.
Shader "TreeFrogAdventure/Biome/Rock"
{
    Properties
    {
        [MainTexture] _BaseMap      ("Base Map", 2D) = "white" {}
        [MainColor]   _BaseColor    ("Base Tint", Color) = (1, 1, 1, 1)

        [Header(Palette Remap)][Space(4)]
        _RemapStrength              ("Remap Strength", Range(0, 1)) = 1
        _ShadowColor                ("Ramp - Shadow", Color) = (0.10, 0.12, 0.13, 1)
        _MidColor                   ("Ramp - Mid", Color) = (0.38, 0.40, 0.42, 1)
        _HighColor                  ("Ramp - Highlight", Color) = (0.72, 0.75, 0.78, 1)
        _RampMidPoint               ("Ramp Midpoint", Range(0.05, 0.95)) = 0.5
        _RampContrast               ("Ramp Contrast", Range(0.25, 4)) = 1
        _ColourVariation            ("Per-Rock Colour Variation", Range(0, 1)) = 0.15
        _VariationScale             ("Variation Scale", Float) = 0.08

        [Header(Recolour   used when remap strength is 0)][Space(4)]
        _HueShift                   ("Hue Shift", Range(-0.5, 0.5)) = 0
        _Saturation                 ("Saturation", Range(0, 3)) = 1
        _Brightness                 ("Brightness", Range(0, 3)) = 1

        [Header(Surface)][Space(4)]
        _Smoothness                 ("Smoothness", Range(0, 1)) = 0.1
        _Metallic                   ("Metallic", Range(0, 1)) = 0
        _AlphaClipEnabled           ("Alpha Clip Enabled", Range(0, 1)) = 0
        _Cutoff                     ("Alpha Cutoff", Range(0, 1)) = 0.5
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 2

        [Header(Overlay   snow moss ash)][Space(4)]
        _OverlayColor               ("Overlay Colour (A = strength)", Color) = (1, 1, 1, 0)
        _OverlayCoverage            ("Overlay Coverage", Range(0, 1)) = 0.5
        _OverlaySoftness            ("Overlay Softness", Range(0.01, 1)) = 0.25
        _OverlayNoiseScale          ("Overlay Noise Scale", Float) = 0.5
        _OverlayNoiseAmount         ("Overlay Noise Amount", Range(0, 1)) = 0.6
        _OverlaySmoothness          ("Overlay Smoothness", Range(0, 1)) = 0.15

        [Header(Waterline Band   swamp slime)][Space(4)]
        _LowerColor                 ("Lower Band Colour (A = strength)", Color) = (0.2, 0.3, 0.15, 0)
        _LowerBandTop               ("Lower Band Top (world Y)", Float) = 2
        _LowerBandFalloff           ("Lower Band Falloff", Float) = 1.5
        _LowerSmoothness            ("Lower Band Smoothness", Range(0, 1)) = 0.7

        [Header(Emissive Cracks   lava veins)][Space(4)]
        [HDR] _CrackColor           ("Crack Emission", Color) = (0, 0, 0, 1)
        _CrackScale                 ("Crack Scale", Float) = 0.35
        _CrackWidth                 ("Crack Width", Range(0.001, 0.6)) = 0.12
        _CrackCavityInfluence       ("Confine To Crevices", Range(0, 1)) = 0.7
        _CrackPulseSpeed            ("Crack Pulse Speed", Float) = 0.8
        _CrackPulseAmount           ("Crack Pulse Amount", Range(0, 1)) = 0.35

        [Header(Biome Haze)][Space(4)]
        _HazeColor                  ("Haze Colour", Color) = (0.6, 0.7, 0.75, 1)
        _HazeStart                  ("Haze Start", Float) = 25
        _HazeEnd                    ("Haze End", Float) = 160
        _HazeStrength               ("Haze Strength", Range(0, 1)) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Geometry"
            "IgnoreProjector" = "True"
            "ShaderModel" = "4.5"
        }
        LOD 300

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "TFA_BiomeCommon.hlsl"

        #if defined(LOD_FADE_CROSSFADE)
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/LODCrossFade.hlsl"
        #endif

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4  _BaseColor;

            half   _RemapStrength;
            half4  _ShadowColor;
            half4  _MidColor;
            half4  _HighColor;
            half   _RampMidPoint;
            half   _RampContrast;
            half   _ColourVariation;
            float  _VariationScale;

            half   _HueShift;
            half   _Saturation;
            half   _Brightness;

            half   _Smoothness;
            half   _Metallic;
            half   _AlphaClipEnabled;
            half   _Cutoff;
            half   _Cull;

            half4  _OverlayColor;
            half   _OverlayCoverage;
            half   _OverlaySoftness;
            float  _OverlayNoiseScale;
            half   _OverlayNoiseAmount;
            half   _OverlaySmoothness;

            half4  _LowerColor;
            float  _LowerBandTop;
            float  _LowerBandFalloff;
            half   _LowerSmoothness;

            half4  _CrackColor;
            float  _CrackScale;
            half   _CrackWidth;
            half   _CrackCavityInfluence;
            half   _CrackPulseSpeed;
            half   _CrackPulseAmount;

            half4  _HazeColor;
            float  _HazeStart;
            float  _HazeEnd;
            half   _HazeStrength;
        CBUFFER_END

        TEXTURE2D(_BaseMap);
        SAMPLER(sampler_BaseMap);

        struct Attributes
        {
            float4 positionOS       : POSITION;
            float3 normalOS         : NORMAL;
            float4 tangentOS        : TANGENT;
            float2 texcoord         : TEXCOORD0;
            float2 staticLightmapUV : TEXCOORD1;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };

        void TFA_AlphaClip(half alpha)
        {
            clip(alpha - _Cutoff * _AlphaClipEnabled - 1.0e-4);
        }

        // Ridged noise thresholded into thin veins, then pushed into the darker
        // (concave) parts of the atlas so glow reads as coming from crevices
        // rather than being painted over the whole rock.
        half TFA_CrackMask(float3 positionWS, half albedoLuma)
        {
            float n  = TFA_FBM3(positionWS * _CrackScale);
            float n2 = TFA_FBM3(positionWS * _CrackScale * 2.13 + 19.7);
            float ridge = 1.0 - abs((n * 0.65 + n2 * 0.35) * 2.0 - 1.0);

            half crack = smoothstep(1.0 - _CrackWidth, 1.0, ridge);

            half cavity = saturate(1.25 - albedoLuma * 1.6);
            crack *= lerp(1.0, cavity, _CrackCavityInfluence);

            return saturate(crack);
        }

        half3 TFA_RockShade(float2 uv, float3 positionWS, half3 normalWS,
                            out half outSmoothness, out half outAlpha, out half3 outEmission)
        {
            half4 tex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv);
            outAlpha = tex.a;

            half luma = TFA_Luminance(tex.rgb);

            // Contrast around the ramp midpoint before remapping, otherwise a
            // flat palette atlas collapses into a single ramp colour.
            half t = saturate((luma - 0.5) * _RampContrast + 0.5);

            // Large-scale noise breaks up the "every rock is identical" look
            // that a shared atlas otherwise produces.
            //
            // Each noise block in this function is gated on its own feature
            // being enabled. TFA_FBM3 costs 24 hash evaluations per pixel, and
            // this shader could otherwise run three of them unconditionally on
            // every rock, cliff, log and prop in the scene. The conditions are
            // uniform (per-material) constants, so there is no branch
            // divergence - the GPU skips the whole block.
            if (_ColourVariation > 0.0)
            {
                half variation = (TFA_FBM3(positionWS * _VariationScale) - 0.5) * _ColourVariation;
                t = saturate(t + variation);
            }

            half3 remapped = TFA_Ramp3(t, _ShadowColor.rgb, _MidColor.rgb, _HighColor.rgb, _RampMidPoint);
            half3 shifted  = TFA_HueShift(tex.rgb, _HueShift, _Saturation, _Brightness);

            half3 albedo = lerp(shifted, remapped, _RemapStrength) * _BaseColor.rgb;
            outSmoothness = _Smoothness;

            // Waterline band - swamp slime and algae creeping up from the water.
            if (_LowerColor.a > 0.0)
            {
                half lower = saturate((_LowerBandTop - positionWS.y) / max(_LowerBandFalloff, 1.0e-4));
                lower *= _LowerColor.a;
                lower *= lerp(1.0, saturate(TFA_FBM3(positionWS * 0.9) * 1.7), 0.6);
                lower = saturate(lower);
                albedo = lerp(albedo, _LowerColor.rgb, lower);
                outSmoothness = lerp(outSmoothness, _LowerSmoothness, lower);
            }

            // Upward-facing overlay - snow caps, ash fall, moss on flat tops.
            // Declared outside the branch because the crack glow below fades
            // where the overlay has covered the rock.
            half overlay = 0.0;
            if (_OverlayColor.a > 0.0)
            {
                overlay = TFA_UpMask(normalWS, _OverlayCoverage, _OverlaySoftness);

                if (_OverlayNoiseAmount > 0.0)
                {
                    overlay *= lerp(1.0, saturate(TFA_FBM3(positionWS * _OverlayNoiseScale) * 1.6),
                                    _OverlayNoiseAmount);
                }

                overlay = saturate(overlay) * _OverlayColor.a;
                albedo = lerp(albedo, _OverlayColor.rgb, overlay);
                outSmoothness = lerp(outSmoothness, _OverlaySmoothness, overlay);
            }

            // Emissive crack network. Snow and ash sit on top of the cracks, so
            // fade the glow where the overlay has covered the rock.
            outEmission = half3(0, 0, 0);
            if (max(_CrackColor.r, max(_CrackColor.g, _CrackColor.b)) > 0.0)
            {
                half crack = TFA_CrackMask(positionWS, luma) * (1.0 - overlay);
                half phase = TFA_Hash21(floor(positionWS.xz * 0.35)) * 6.2831853;
                half pulse = lerp(1.0, TFA_Pulse(_CrackPulseSpeed, phase), _CrackPulseAmount);
                outEmission = _CrackColor.rgb * crack * pulse;

                // Hot rock is not black in the cracks - warm the albedo too.
                albedo = lerp(albedo, saturate(_CrackColor.rgb) * 0.5, crack * 0.4);
            }

            return albedo;
        }
        ENDHLSL

        // ------------------------------------------------------------ Forward
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            Cull [_Cull]
            ZWrite On
            Blend One Zero

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex ForwardVertex
            #pragma fragment ForwardFragment

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ EVALUATE_SH_MIXED EVALUATE_SH_VERTEX
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fragment _ _LIGHT_COOKIES
            #pragma multi_compile _ _LIGHT_LAYERS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile _ LIGHTMAP_SHADOW_MIXING
            #pragma multi_compile _ SHADOWS_SHADOWMASK
            #pragma multi_compile _ DIRLIGHTMAP_COMBINED
            #pragma multi_compile _ LIGHTMAP_ON
            #pragma multi_compile _ DYNAMICLIGHTMAP_ON
            #pragma multi_compile_instancing
            #pragma multi_compile _ LOD_FADE_CROSSFADE

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                half3  normalWS   : TEXCOORD2;
                half   fogFactor  : TEXCOORD3;
                DECLARE_LIGHTMAP_OR_SH(staticLightmapUV, vertexSH, 4);
            #ifdef USE_APV_PROBE_OCCLUSION
                float4 probeOcclusion : TEXCOORD5;
            #endif
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings ForwardVertex(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                VertexPositionInputs vp = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs vn = GetVertexNormalInputs(input.normalOS, input.tangentOS);

                output.positionCS = vp.positionCS;
                output.positionWS = vp.positionWS;
                output.normalWS   = vn.normalWS;
                output.uv         = TRANSFORM_TEX(input.texcoord, _BaseMap);
                output.fogFactor  = ComputeFogFactor(vp.positionCS.z);

                OUTPUT_LIGHTMAP_UV(input.staticLightmapUV, unity_LightmapST, output.staticLightmapUV);
                OUTPUT_SH4(vp.positionWS, output.normalWS.xyz, GetWorldSpaceNormalizeViewDir(vp.positionWS),
                           output.vertexSH, output.probeOcclusion);

                return output;
            }

            half4 ForwardFragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

            #if defined(LOD_FADE_CROSSFADE)
                // Grass/tree prefabs use animated LOD crossfade. Without this the
                // two LOD levels both render solid during the transition, which
                // both pops visually and doubles the cost of every fading object.
                LODFadeCrossFade(input.positionCS);
            #endif

                half3 normalWS = NormalizeNormalPerPixel(input.normalWS);

                half smoothness, alpha;
                half3 emission;
                half3 albedo = TFA_RockShade(input.uv, input.positionWS, normalWS,
                                             smoothness, alpha, emission);
                TFA_AlphaClip(alpha);

                InputData inputData = (InputData)0;
                inputData.positionWS = input.positionWS;
                inputData.normalWS = normalWS;
                inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
            #if defined(MAIN_LIGHT_CALCULATE_SHADOWS)
                inputData.shadowCoord = TransformWorldToShadowCoord(input.positionWS);
            #else
                inputData.shadowCoord = float4(0, 0, 0, 0);
            #endif
                inputData.fogCoord = InitializeInputDataFog(float4(input.positionWS, 1.0), input.fogFactor);
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
                inputData.bakedGI = SAMPLE_GI(input.staticLightmapUV, input.vertexSH, normalWS);
                inputData.shadowMask = SAMPLE_SHADOWMASK(input.staticLightmapUV);

                SurfaceData surfaceData = (SurfaceData)0;
                surfaceData.albedo = albedo;
                surfaceData.alpha = 1.0;
                surfaceData.metallic = _Metallic;
                surfaceData.smoothness = smoothness;
                surfaceData.occlusion = 1.0;
                surfaceData.normalTS = half3(0, 0, 1);
                surfaceData.emission = emission;

                half4 color = UniversalFragmentPBR(inputData, surfaceData);
                color.rgb = MixFog(color.rgb, inputData.fogCoord);

                float viewDepth = length(GetCameraPositionWS() - input.positionWS);
                color.rgb = TFA_ApplyHaze(color.rgb, viewDepth, _HazeColor.rgb,
                                          _HazeStart, _HazeEnd, _HazeStrength);

                return half4(color.rgb, 1.0);
            }
            ENDHLSL
        }

        // ------------------------------------------------------- ShadowCaster
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex ShadowVertex
            #pragma fragment ShadowFragment
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #pragma multi_compile_instancing
            #pragma multi_compile _ LOD_FADE_CROSSFADE

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            struct VaryingsShadow
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            VaryingsShadow ShadowVertex(Attributes input)
            {
                VaryingsShadow output = (VaryingsShadow)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);

            #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                float3 lightDirectionWS = normalize(_LightPosition - positionWS);
            #else
                float3 lightDirectionWS = _LightDirection;
            #endif

                float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirectionWS));
            #if UNITY_REVERSED_Z
                positionCS.z = min(positionCS.z, UNITY_NEAR_CLIP_VALUE);
            #else
                positionCS.z = max(positionCS.z, UNITY_NEAR_CLIP_VALUE);
            #endif

                output.positionCS = positionCS;
                output.uv = TRANSFORM_TEX(input.texcoord, _BaseMap);
                return output;
            }

            half4 ShadowFragment(VaryingsShadow input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                TFA_AlphaClip(SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).a);

            #if defined(LOD_FADE_CROSSFADE)
                LODFadeCrossFade(input.positionCS);
            #endif
                return 0;
            }
            ENDHLSL
        }

        // ----------------------------------------------------------- DepthOnly
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ColorMask R
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex DepthVertex
            #pragma fragment DepthFragment
            #pragma multi_compile_instancing
            #pragma multi_compile _ LOD_FADE_CROSSFADE

            struct VaryingsDepth
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            VaryingsDepth DepthVertex(Attributes input)
            {
                VaryingsDepth output = (VaryingsDepth)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = TRANSFORM_TEX(input.texcoord, _BaseMap);
                return output;
            }

            half4 DepthFragment(VaryingsDepth input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                TFA_AlphaClip(SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).a);

            #if defined(LOD_FADE_CROSSFADE)
                LODFadeCrossFade(input.positionCS);
            #endif
                return 0;
            }
            ENDHLSL
        }

        // -------------------------------------------------------- DepthNormals
        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }

            ZWrite On
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex DepthNormalsVertex
            #pragma fragment DepthNormalsFragment
            #pragma multi_compile_instancing
            #pragma multi_compile _ LOD_FADE_CROSSFADE

            struct VaryingsDN
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                half3  normalWS   : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            VaryingsDN DepthNormalsVertex(Attributes input)
            {
                VaryingsDN output = (VaryingsDN)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                VertexPositionInputs vp = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs vn = GetVertexNormalInputs(input.normalOS, input.tangentOS);

                output.positionCS = vp.positionCS;
                output.normalWS = vn.normalWS;
                output.uv = TRANSFORM_TEX(input.texcoord, _BaseMap);
                return output;
            }

            half4 DepthNormalsFragment(VaryingsDN input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                TFA_AlphaClip(SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).a);

            #if defined(LOD_FADE_CROSSFADE)
                LODFadeCrossFade(input.positionCS);
            #endif
                return half4(NormalizeNormalPerPixel(input.normalWS), 0.0);
            }
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Lit"
}
