// Tree Frog Adventure 3D - biome terrain.
//
// Four-splat Unity Terrain shader that recolours the existing UNS_Grass /
// UNS_Dirt terrain layers per biome, so no new terrain textures are needed.
// On top of the splat blend it adds per-layer tints, an upward-facing overlay
// (snow / ash), two world-height colour bands (mud at the waterline, snow on
// the peaks) and an optional emissive crack network for scorched ground.
//
// IMPORTANT: leave "Draw Instanced" UNCHECKED on the Terrain component. This
// shader uses the non-instanced path (which is what all five terrains in
// Level_1_Forest already use). With Draw Instanced on, the terrain renders flat.
Shader "TreeFrogAdventure/Biome/Terrain"
{
    Properties
    {
        // --- set automatically by the terrain engine, do not edit by hand ---
        [HideInInspector] _Control      ("Control (RGBA)", 2D) = "red" {}
        [HideInInspector] _Splat0       ("Layer 0 (R)", 2D) = "grey" {}
        [HideInInspector] _Splat1       ("Layer 1 (G)", 2D) = "grey" {}
        [HideInInspector] _Splat2       ("Layer 2 (B)", 2D) = "grey" {}
        [HideInInspector] _Splat3       ("Layer 3 (A)", 2D) = "grey" {}
        [HideInInspector] _Normal0      ("Normal 0", 2D) = "bump" {}
        [HideInInspector] _Normal1      ("Normal 1", 2D) = "bump" {}
        [HideInInspector] _Normal2      ("Normal 2", 2D) = "bump" {}
        [HideInInspector] _Normal3      ("Normal 3", 2D) = "bump" {}
        [HideInInspector] _Mask0        ("Mask 0", 2D) = "grey" {}
        [HideInInspector] _Mask1        ("Mask 1", 2D) = "grey" {}
        [HideInInspector] _Mask2        ("Mask 2", 2D) = "grey" {}
        [HideInInspector] _Mask3        ("Mask 3", 2D) = "grey" {}
        [HideInInspector] _Metallic0    ("Metallic 0", Range(0, 1)) = 0
        [HideInInspector] _Metallic1    ("Metallic 1", Range(0, 1)) = 0
        [HideInInspector] _Metallic2    ("Metallic 2", Range(0, 1)) = 0
        [HideInInspector] _Metallic3    ("Metallic 3", Range(0, 1)) = 0
        [HideInInspector] _Smoothness0  ("Smoothness 0", Range(0, 1)) = 0.05
        [HideInInspector] _Smoothness1  ("Smoothness 1", Range(0, 1)) = 0.05
        [HideInInspector] _Smoothness2  ("Smoothness 2", Range(0, 1)) = 0.05
        [HideInInspector] _Smoothness3  ("Smoothness 3", Range(0, 1)) = 0.05
        [HideInInspector] _MainTex      ("BaseMap (RGB)", 2D) = "grey" {}
        [HideInInspector] _BaseColor    ("Main Color", Color) = (1, 1, 1, 1)
        [HideInInspector] _TerrainHolesTexture ("Holes Map", 2D) = "white" {}
        [HideInInspector] _NumLayersCount ("Layer Count", Float) = 1

        [Header(Per Layer Tint)][Space(4)]
        _LayerTint0         ("Layer 0 Tint", Color) = (1, 1, 1, 1)
        _LayerTint1         ("Layer 1 Tint", Color) = (1, 1, 1, 1)
        _LayerTint2         ("Layer 2 Tint", Color) = (1, 1, 1, 1)
        _LayerTint3         ("Layer 3 Tint", Color) = (1, 1, 1, 1)

        [Header(Global Recolour)][Space(4)]
        _HueShift           ("Hue Shift", Range(-0.5, 0.5)) = 0
        _Saturation         ("Saturation", Range(0, 3)) = 1
        _Brightness         ("Brightness", Range(0, 3)) = 1
        _MacroVariation     ("Macro Colour Variation", Range(0, 1)) = 0.12
        _MacroScale         ("Macro Variation Scale", Float) = 0.02

        [Header(Overlay   snow ash)][Space(4)]
        _OverlayColor       ("Overlay Colour (A = strength)", Color) = (1, 1, 1, 0)
        _OverlayCoverage    ("Overlay Coverage", Range(0, 1)) = 0.7
        _OverlaySoftness    ("Overlay Softness", Range(0.01, 1)) = 0.2
        _OverlayNoiseScale  ("Overlay Noise Scale", Float) = 0.15
        _OverlayNoiseAmount ("Overlay Noise Amount", Range(0, 1)) = 0.6
        _OverlaySmoothness  ("Overlay Smoothness", Range(0, 1)) = 0.2

        [Header(Height Bands)][Space(4)]
        _LowBandColor       ("Low Band Colour (A = strength)", Color) = (0.18, 0.16, 0.11, 0)
        _LowBandTop         ("Low Band Top (world Y)", Float) = 3
        _LowBandFalloff     ("Low Band Falloff", Float) = 2
        _LowBandSmoothness  ("Low Band Smoothness", Range(0, 1)) = 0.55
        _HighBandColor      ("High Band Colour (A = strength)", Color) = (0.9, 0.94, 1, 0)
        _HighBandBottom     ("High Band Bottom (world Y)", Float) = 40
        _HighBandFalloff    ("High Band Falloff", Float) = 12

        [Header(Emissive Cracks)][Space(4)]
        [HDR] _CrackColor   ("Crack Emission", Color) = (0, 0, 0, 1)
        _CrackScale         ("Crack Scale", Float) = 0.08
        _CrackWidth         ("Crack Width", Range(0.001, 0.6)) = 0.1
        _CrackPulseSpeed    ("Crack Pulse Speed", Float) = 0.6
        _CrackPulseAmount   ("Crack Pulse Amount", Range(0, 1)) = 0.3

        [Header(Biome Haze)][Space(4)]
        _HazeColor          ("Haze Colour", Color) = (0.6, 0.7, 0.75, 1)
        _HazeStart          ("Haze Start", Float) = 40
        _HazeEnd            ("Haze End", Float) = 250
        _HazeStrength       ("Haze Strength", Range(0, 1)) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Geometry-100"
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "UniversalMaterialType" = "Lit"
            "IgnoreProjector" = "False"
            "TerrainCompatible" = "True"
            "ShaderModel" = "4.5"
        }
        LOD 300

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "TFA_BiomeCommon.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float4 _Control_ST;
            float4 _Control_TexelSize;
            float4 _Splat0_ST;
            float4 _Splat1_ST;
            float4 _Splat2_ST;
            float4 _Splat3_ST;

            half _Metallic0, _Metallic1, _Metallic2, _Metallic3;
            half _Smoothness0, _Smoothness1, _Smoothness2, _Smoothness3;
            half _NumLayersCount;
            half4 _BaseColor;

            half4 _LayerTint0, _LayerTint1, _LayerTint2, _LayerTint3;

            half  _HueShift;
            half  _Saturation;
            half  _Brightness;
            half  _MacroVariation;
            float _MacroScale;

            half4 _OverlayColor;
            half  _OverlayCoverage;
            half  _OverlaySoftness;
            float _OverlayNoiseScale;
            half  _OverlayNoiseAmount;
            half  _OverlaySmoothness;

            half4 _LowBandColor;
            float _LowBandTop;
            float _LowBandFalloff;
            half  _LowBandSmoothness;
            half4 _HighBandColor;
            float _HighBandBottom;
            float _HighBandFalloff;

            half4 _CrackColor;
            float _CrackScale;
            half  _CrackWidth;
            half  _CrackPulseSpeed;
            half  _CrackPulseAmount;

            half4 _HazeColor;
            float _HazeStart;
            float _HazeEnd;
            half  _HazeStrength;
        CBUFFER_END

        TEXTURE2D(_Control);  SAMPLER(sampler_Control);
        TEXTURE2D(_Splat0);   SAMPLER(sampler_Splat0);
        TEXTURE2D(_Splat1);
        TEXTURE2D(_Splat2);
        TEXTURE2D(_Splat3);
        TEXTURE2D(_TerrainHolesTexture); SAMPLER(sampler_TerrainHolesTexture);

        struct Attributes
        {
            float4 positionOS : POSITION;
            float3 normalOS   : NORMAL;
            float2 texcoord   : TEXCOORD0;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };

        void TFA_ClipHoles(float2 uv)
        {
        #ifdef _ALPHATEST_ON
            float hole = SAMPLE_TEXTURE2D(_TerrainHolesTexture, sampler_TerrainHolesTexture, uv).r;
            clip(hole < 0.0005 ? -1 : 1);
        #endif
        }

        // Half-texel inset so the control map does not bleed across terrain
        // tile seams - same correction URP's own TerrainLit applies.
        float2 TFA_ControlUV(float2 uv)
        {
            return (uv * (_Control_TexelSize.zw - 1.0) + 0.5) * _Control_TexelSize.xy;
        }

        void TFA_TerrainShade(float2 uv, float3 positionWS, half3 normalWS,
                              out half3 outAlbedo, out half outSmoothness,
                              out half outMetallic, out half3 outEmission)
        {
            half4 control = SAMPLE_TEXTURE2D(_Control, sampler_Control, TFA_ControlUV(uv));

            // Terrain weights are not guaranteed to sum to 1 after filtering.
            half total = dot(control, half4(1, 1, 1, 1));
            control /= max(total, 1.0e-4);

            half4 s0 = SAMPLE_TEXTURE2D(_Splat0, sampler_Splat0, uv * _Splat0_ST.xy + _Splat0_ST.zw);
            half4 s1 = SAMPLE_TEXTURE2D(_Splat1, sampler_Splat0, uv * _Splat1_ST.xy + _Splat1_ST.zw);
            half4 s2 = SAMPLE_TEXTURE2D(_Splat2, sampler_Splat0, uv * _Splat2_ST.xy + _Splat2_ST.zw);
            half4 s3 = SAMPLE_TEXTURE2D(_Splat3, sampler_Splat0, uv * _Splat3_ST.xy + _Splat3_ST.zw);

            half3 albedo = s0.rgb * _LayerTint0.rgb * control.r
                         + s1.rgb * _LayerTint1.rgb * control.g
                         + s2.rgb * _LayerTint2.rgb * control.b
                         + s3.rgb * _LayerTint3.rgb * control.a;

            outSmoothness = _Smoothness0 * control.r + _Smoothness1 * control.g
                          + _Smoothness2 * control.b + _Smoothness3 * control.a;
            outMetallic   = _Metallic0 * control.r + _Metallic1 * control.g
                          + _Metallic2 * control.b + _Metallic3 * control.a;

            albedo = TFA_HueShift(albedo, _HueShift, _Saturation, _Brightness);

            // Every noise block below is gated on its own feature being enabled.
            // Terrain fills most of the screen, so skipping an unused fbm here
            // is the single cheapest performance win in the set. The conditions
            // are uniform (per-material) constants - no branch divergence.

            // Very low-frequency noise so large terrains do not read as one
            // flat repeated tile from a distance.
            if (_MacroVariation > 0.0)
            {
                half macro = (TFA_FBM2(positionWS.xz * _MacroScale) - 0.5) * _MacroVariation;
                albedo = saturate(albedo * (1.0 + macro * 2.0));
            }

            // Low band: mud / silt around the waterline.
            if (_LowBandColor.a > 0.0)
            {
                half low = saturate((_LowBandTop - positionWS.y) / max(_LowBandFalloff, 1.0e-4));
                low *= _LowBandColor.a;
                low *= lerp(1.0, saturate(TFA_FBM2(positionWS.xz * 0.35) * 1.6), 0.5);
                low = saturate(low);
                albedo = lerp(albedo, _LowBandColor.rgb, low);
                outSmoothness = lerp(outSmoothness, _LowBandSmoothness, low);
            }

            // High band: snow line on the peaks.
            if (_HighBandColor.a > 0.0)
            {
                half high = saturate((positionWS.y - _HighBandBottom) / max(_HighBandFalloff, 1.0e-4));
                high *= _HighBandColor.a;
                high *= lerp(1.0, saturate(TFA_FBM2(positionWS.xz * 0.25) * 1.6), 0.5);
                albedo = lerp(albedo, _HighBandColor.rgb, saturate(high));
            }

            // Slope-driven overlay: snow and ash settle on flats, not cliffs.
            half overlay = 0.0;
            if (_OverlayColor.a > 0.0)
            {
                overlay = TFA_UpMask(normalWS, _OverlayCoverage, _OverlaySoftness);

                if (_OverlayNoiseAmount > 0.0)
                {
                    overlay *= lerp(1.0, saturate(TFA_FBM2(positionWS.xz * _OverlayNoiseScale) * 1.6),
                                    _OverlayNoiseAmount);
                }

                overlay = saturate(overlay) * _OverlayColor.a;
                albedo = lerp(albedo, _OverlayColor.rgb, overlay);
                outSmoothness = lerp(outSmoothness, _OverlaySmoothness, overlay);
            }

            outEmission = half3(0, 0, 0);
            if (max(_CrackColor.r, max(_CrackColor.g, _CrackColor.b)) > 0.0)
            {
                float n = TFA_FBM2(positionWS.xz * _CrackScale);
                half ridge = 1.0 - abs(n * 2.0 - 1.0);
                half crack = smoothstep(1.0 - _CrackWidth, 1.0, ridge) * (1.0 - overlay);
                half pulse = lerp(1.0, TFA_Pulse(_CrackPulseSpeed, n * 6.2831853), _CrackPulseAmount);
                outEmission = _CrackColor.rgb * crack * pulse;
                albedo = lerp(albedo, saturate(_CrackColor.rgb) * 0.5, crack * 0.35);
            }

            outAlbedo = albedo;
        }
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            Cull Back
            ZWrite On

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex TerrainVertex
            #pragma fragment TerrainFragment
            #pragma multi_compile_fragment __ _ALPHATEST_ON

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
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings TerrainVertex(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                VertexPositionInputs vp = GetVertexPositionInputs(input.positionOS.xyz);

                output.positionCS = vp.positionCS;
                output.positionWS = vp.positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.uv = input.texcoord;
                output.fogFactor = ComputeFogFactor(vp.positionCS.z);

                OUTPUT_LIGHTMAP_UV(input.texcoord, unity_LightmapST, output.staticLightmapUV);
                OUTPUT_SH4(vp.positionWS, output.normalWS.xyz,
                           GetWorldSpaceNormalizeViewDir(vp.positionWS),
                           output.vertexSH, output.probeOcclusion);

                return output;
            }

            half4 TerrainFragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                TFA_ClipHoles(input.uv);

                half3 normalWS = NormalizeNormalPerPixel(input.normalWS);

                half3 albedo, emission;
                half smoothness, metallic;
                TFA_TerrainShade(input.uv, input.positionWS, normalWS,
                                 albedo, smoothness, metallic, emission);

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
                surfaceData.metallic = metallic;
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

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Back

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex ShadowVertex
            #pragma fragment ShadowFragment
            #pragma multi_compile_fragment __ _ALPHATEST_ON
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            struct VaryingsShadow
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
            };

            VaryingsShadow ShadowVertex(Attributes input)
            {
                VaryingsShadow output = (VaryingsShadow)0;

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
                output.uv = input.texcoord;
                return output;
            }

            half4 ShadowFragment(VaryingsShadow input) : SV_Target
            {
                TFA_ClipHoles(input.uv);
                return 0;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ColorMask R
            Cull Back

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex DepthVertex
            #pragma fragment DepthFragment
            #pragma multi_compile_fragment __ _ALPHATEST_ON

            struct VaryingsDepth
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
            };

            VaryingsDepth DepthVertex(Attributes input)
            {
                VaryingsDepth output = (VaryingsDepth)0;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.texcoord;
                return output;
            }

            half4 DepthFragment(VaryingsDepth input) : SV_Target
            {
                TFA_ClipHoles(input.uv);
                return 0;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }

            ZWrite On
            Cull Back

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex DepthNormalsVertex
            #pragma fragment DepthNormalsFragment
            #pragma multi_compile_fragment __ _ALPHATEST_ON

            struct VaryingsDN
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                half3  normalWS   : TEXCOORD1;
            };

            VaryingsDN DepthNormalsVertex(Attributes input)
            {
                VaryingsDN output = (VaryingsDN)0;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.uv = input.texcoord;
                return output;
            }

            half4 DepthNormalsFragment(VaryingsDN input) : SV_Target
            {
                TFA_ClipHoles(input.uv);
                return half4(NormalizeNormalPerPixel(input.normalWS), 0.0);
            }
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Terrain/Lit"
}
