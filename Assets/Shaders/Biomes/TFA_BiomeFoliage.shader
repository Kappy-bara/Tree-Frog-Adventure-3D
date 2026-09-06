// Tree Frog Adventure 3D - biome foliage.
// Trees, bushes, grass and flowers. Recolours the shared Emerald Forest art
// per biome via hue shift + root-to-tip gradient, adds wind sway, an
// upward-facing overlay layer (frost / slime / ash) and optional emission.
Shader "TreeFrogAdventure/Biome/Foliage"
{
    Properties
    {
        [MainTexture] _BaseMap          ("Base Map", 2D) = "white" {}
        [MainColor]   _BaseColor        ("Tip Colour", Color) = (0.45, 0.58, 0.22, 1)
        _RootColor                      ("Root Colour", Color) = (0.28, 0.36, 0.15, 1)

        [Header(Recolour)][Space(4)]
        _HueShift                       ("Hue Shift", Range(-0.5, 0.5)) = 0
        _Saturation                     ("Saturation", Range(0, 3)) = 1
        _Brightness                     ("Brightness", Range(0, 3)) = 1
        _TintStrength                   ("Tint Strength", Range(0, 1)) = 1
        _GradientBottom                 ("Gradient Bottom (object Y)", Float) = 0
        _GradientTop                    ("Gradient Top (object Y)", Float) = 3
        _GradientPower                  ("Gradient Power", Range(0.1, 6)) = 1

        [Header(Surface)][Space(4)]
        _Smoothness                     ("Smoothness", Range(0, 1)) = 0.05
        _Metallic                       ("Metallic", Range(0, 1)) = 0
        _AlphaClipEnabled               ("Alpha Clip Enabled", Range(0, 1)) = 1
        _Cutoff                         ("Alpha Cutoff", Range(0, 1)) = 0.5
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 0

        [Header(Overlay   frost slime ash)][Space(4)]
        _OverlayColor                   ("Overlay Colour (A = strength)", Color) = (1, 1, 1, 0)
        _OverlayCoverage                ("Overlay Coverage", Range(0, 1)) = 0.5
        _OverlaySoftness                ("Overlay Softness", Range(0.01, 1)) = 0.3
        _OverlayNoiseScale              ("Overlay Noise Scale", Float) = 0.6
        _OverlayNoiseAmount             ("Overlay Noise Amount", Range(0, 1)) = 0.5
        _OverlaySmoothness              ("Overlay Smoothness", Range(0, 1)) = 0.2

        [Header(Emission)][Space(4)]
        [HDR] _EmissionColor            ("Emission Colour", Color) = (0, 0, 0, 1)
        _EmissionNoiseScale             ("Emission Noise Scale", Float) = 2
        _EmissionThreshold              ("Emission Threshold", Range(0, 1)) = 0.6
        _EmissionPulseSpeed             ("Emission Pulse Speed", Float) = 1.5
        _EmissionPulseAmount            ("Emission Pulse Amount", Range(0, 1)) = 0.6

        [Header(Wind)][Space(4)]
        _WindStrength                   ("Wind Strength", Float) = 0.12
        _WindSpeed                      ("Wind Speed", Float) = 1.2
        _WindScale                      ("Wind Spatial Frequency", Float) = 0.35
        _WindTurbulence                 ("Wind Flutter", Float) = 0.4
        _WindDirection                  ("Wind Direction (XYZ)", Vector) = (1, 0, 0.35, 0)
        _WindMaskBottom                 ("Wind Mask Bottom (object Y)", Float) = 0
        _WindMaskTop                    ("Wind Mask Top (object Y)", Float) = 3
        _WindMaskPower                  ("Wind Mask Power", Range(0.1, 6)) = 1.5

        [Header(Translucency)][Space(4)]
        _TranslucencyColor              ("Translucency Colour", Color) = (0.6, 0.9, 0.35, 1)
        _TranslucencyStrength           ("Translucency Strength", Range(0, 4)) = 0.8
        _TranslucencyPower              ("Translucency Power", Range(1, 16)) = 4

        [Header(Biome Haze)][Space(4)]
        _HazeColor                      ("Haze Colour", Color) = (0.6, 0.7, 0.75, 1)
        _HazeStart                      ("Haze Start", Float) = 25
        _HazeEnd                        ("Haze End", Float) = 160
        _HazeStrength                   ("Haze Strength", Range(0, 1)) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "AlphaTest"
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
            half4  _RootColor;

            half   _HueShift;
            half   _Saturation;
            half   _Brightness;
            half   _TintStrength;
            float  _GradientBottom;
            float  _GradientTop;
            half   _GradientPower;

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

            half4  _EmissionColor;
            float  _EmissionNoiseScale;
            half   _EmissionThreshold;
            half   _EmissionPulseSpeed;
            half   _EmissionPulseAmount;

            half   _WindStrength;
            half   _WindSpeed;
            half   _WindScale;
            half   _WindTurbulence;
            float4 _WindDirection;
            float  _WindMaskBottom;
            float  _WindMaskTop;
            half   _WindMaskPower;

            half4  _TranslucencyColor;
            half   _TranslucencyStrength;
            half   _TranslucencyPower;

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
            float4 color            : COLOR;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };

        // Wind mask: 0 at the trunk base, 1 at the canopy. Object space, so it
        // works on every UNS prefab regardless of where it sits in the world.
        float TFA_FoliageWindMask(float3 positionOS)
        {
            float m = TFA_HeightMask(positionOS.y, _WindMaskBottom, _WindMaskTop);
            return pow(m, _WindMaskPower);
        }

        // Shared by every pass so shadows and depth match the swaying geometry.
        float3 TFA_FoliageDisplaceWS(float3 positionWS, float3 positionOS)
        {
            float mask = TFA_FoliageWindMask(positionOS);
            float4 windParams = float4(_WindStrength, _WindSpeed, _WindScale, _WindTurbulence);
            return positionWS + TFA_WindOffset(positionWS, mask, windParams, _WindDirection.xyz);
        }

        half4 TFA_SampleFoliageAlbedo(float2 uv, float3 positionOS, float3 normalWS, float3 positionWS,
                                      out half outSmoothness)
        {
            half4 tex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv);

            // Recolour: hue/sat/value first, then blend toward the biome ramp.
            half3 shifted = TFA_HueShift(tex.rgb, _HueShift, _Saturation, _Brightness);

            float g = TFA_HeightMask(positionOS.y, _GradientBottom, _GradientTop);
            g = pow(saturate(g), _GradientPower);
            half3 tint = lerp(_RootColor.rgb, _BaseColor.rgb, g);

            half3 albedo = lerp(shifted, shifted * tint * 2.0, _TintStrength);

            outSmoothness = _Smoothness;

            // Overlay layer: frost on alpine leaves, slime on swamp reeds, ash
            // on burnt volcanic scrub. One mechanism, three biomes.
            //
            // Gated on the feature actually being enabled. TFA_FBM3 is three
            // octaves of 3D value noise = 24 hash evaluations per pixel, so
            // running it when the overlay is off is pure waste. The condition is
            // a uniform (per-material) constant, so there is no branch
            // divergence - the GPU skips the whole block.
            if (_OverlayColor.a > 0.0)
            {
                float overlay = TFA_UpMask(normalWS, _OverlayCoverage, _OverlaySoftness);

                if (_OverlayNoiseAmount > 0.0)
                {
                    float on = TFA_FBM3(positionWS * _OverlayNoiseScale);
                    overlay *= lerp(1.0, saturate(on * 1.6), _OverlayNoiseAmount);
                }

                overlay = saturate(overlay) * _OverlayColor.a;

                albedo = lerp(albedo, _OverlayColor.rgb, overlay);
                outSmoothness = lerp(outSmoothness, _OverlaySmoothness, overlay);
            }

            return half4(albedo, tex.a);
        }

        half3 TFA_FoliageEmission(float3 positionWS)
        {
            if (max(_EmissionColor.r, max(_EmissionColor.g, _EmissionColor.b)) <= 0.0)
                return half3(0, 0, 0);

            float n = TFA_FBM3(positionWS * _EmissionNoiseScale);
            float m = smoothstep(_EmissionThreshold, _EmissionThreshold + 0.15, n);

            float phase = TFA_Hash21(floor(positionWS.xz * 0.5)) * 6.2831853;
            float pulse = lerp(1.0, TFA_Pulse(_EmissionPulseSpeed, phase), _EmissionPulseAmount);

            return _EmissionColor.rgb * m * pulse;
        }

        void TFA_AlphaClip(half alpha)
        {
            clip(alpha - _Cutoff * _AlphaClipEnabled - 1.0e-4);
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
                float3 positionOS : TEXCOORD2;
                half3  normalWS   : TEXCOORD3;
                half   fogFactor  : TEXCOORD4;
                DECLARE_LIGHTMAP_OR_SH(staticLightmapUV, vertexSH, 5);
            #ifdef USE_APV_PROBE_OCCLUSION
                float4 probeOcclusion : TEXCOORD6;
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

                float3 positionWS = TFA_FoliageDisplaceWS(vp.positionWS, input.positionOS.xyz);

                output.positionWS = positionWS;
                output.positionOS = input.positionOS.xyz;
                output.positionCS = TransformWorldToHClip(positionWS);
                output.normalWS   = vn.normalWS;
                output.uv         = TRANSFORM_TEX(input.texcoord, _BaseMap);
                output.fogFactor  = ComputeFogFactor(output.positionCS.z);

                OUTPUT_LIGHTMAP_UV(input.staticLightmapUV, unity_LightmapST, output.staticLightmapUV);
                OUTPUT_SH4(positionWS, output.normalWS.xyz, GetWorldSpaceNormalizeViewDir(positionWS),
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

                half smoothness;
                half4 albedo = TFA_SampleFoliageAlbedo(input.uv, input.positionOS, normalWS,
                                                       input.positionWS, smoothness);
                TFA_AlphaClip(albedo.a);

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
                surfaceData.albedo = albedo.rgb;
                surfaceData.alpha = 1.0;
                surfaceData.metallic = _Metallic;
                surfaceData.smoothness = smoothness;
                surfaceData.occlusion = 1.0;
                surfaceData.normalTS = half3(0, 0, 1);
                surfaceData.emission = TFA_FoliageEmission(input.positionWS);

                half4 color = UniversalFragmentPBR(inputData, surfaceData);

                // Cheap back-light translucency. Leaves lit from behind glow -
                // the single biggest stylised win on this art style.
                Light mainLight = GetMainLight();
                half backLit = saturate(dot(mainLight.direction, -inputData.viewDirectionWS));
                half trans = pow(backLit, _TranslucencyPower) * _TranslucencyStrength;
                color.rgb += _TranslucencyColor.rgb * albedo.rgb * trans
                           * mainLight.color * mainLight.distanceAttenuation;

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
                float3 positionOS : TEXCOORD1;
                float3 positionWS : TEXCOORD2;
                half3  normalWS   : TEXCOORD3;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            VaryingsShadow ShadowVertex(Attributes input)
            {
                VaryingsShadow output = (VaryingsShadow)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                VertexPositionInputs vp = GetVertexPositionInputs(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                float3 positionWS = TFA_FoliageDisplaceWS(vp.positionWS, input.positionOS.xyz);

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
                output.positionWS = positionWS;
                output.positionOS = input.positionOS.xyz;
                output.normalWS = normalWS;
                output.uv = TRANSFORM_TEX(input.texcoord, _BaseMap);
                return output;
            }

            half4 ShadowFragment(VaryingsShadow input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                half alpha = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).a;

            #if defined(LOD_FADE_CROSSFADE)
                LODFadeCrossFade(input.positionCS);
            #endif
                TFA_AlphaClip(alpha);
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

                VertexPositionInputs vp = GetVertexPositionInputs(input.positionOS.xyz);
                float3 positionWS = TFA_FoliageDisplaceWS(vp.positionWS, input.positionOS.xyz);

                output.positionCS = TransformWorldToHClip(positionWS);
                output.uv = TRANSFORM_TEX(input.texcoord, _BaseMap);
                return output;
            }

            half4 DepthFragment(VaryingsDepth input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                half alpha = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).a;

            #if defined(LOD_FADE_CROSSFADE)
                LODFadeCrossFade(input.positionCS);
            #endif
                TFA_AlphaClip(alpha);
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
                float3 positionWS = TFA_FoliageDisplaceWS(vp.positionWS, input.positionOS.xyz);

                output.positionCS = TransformWorldToHClip(positionWS);
                output.normalWS = vn.normalWS;
                output.uv = TRANSFORM_TEX(input.texcoord, _BaseMap);
                return output;
            }

            half4 DepthNormalsFragment(VaryingsDN input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                half alpha = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).a;

            #if defined(LOD_FADE_CROSSFADE)
                LODFadeCrossFade(input.positionCS);
            #endif
                TFA_AlphaClip(alpha);
                return half4(NormalizeNormalPerPixel(input.normalWS), 0.0);
            }
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Lit"
}
