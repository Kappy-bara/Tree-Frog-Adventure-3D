// Tree Frog Adventure 3D - flowing lava.
//
// Replaces the water plane in Volcanic Highlands. Fully procedural: a
// domain-warped, counter-scrolling noise field is split into a cooled crust and
// the molten channels between the crust plates, with the hottest emission along
// the crust edges where real lava glows brightest.
//
// Emission is HDR - it needs Bloom on the scene Volume to look right.
Shader "TreeFrogAdventure/Biome/Lava"
{
    Properties
    {
        [Header(Flow)][Space(4)]
        _FlowScale          ("Flow Scale", Float) = 0.15
        _FlowSpeed          ("Flow Speed", Float) = 0.06
        _FlowDirection      ("Flow Direction (XZ)", Vector) = (1, 0.35, 0, 0)
        _WarpAmount         ("Domain Warp", Range(0, 3)) = 1.2

        [Header(Crust)][Space(4)]
        _CrustColor         ("Crust Colour", Color) = (0.045, 0.038, 0.042, 1)
        _CrustThreshold     ("Crust Coverage", Range(0, 1)) = 0.48
        _CrustSoftness      ("Crust Edge Softness", Range(0.01, 0.5)) = 0.12
        _CrustSmoothness    ("Crust Smoothness", Range(0, 1)) = 0.15
        _CrustDetailScale   ("Crust Detail Scale", Float) = 1.6
        _CrustDetailAmount  ("Crust Detail Amount", Range(0, 1)) = 0.35

        [Header(Molten)][Space(4)]
        [HDR] _HotColor     ("Molten Colour", Color) = (3.0, 0.55, 0.06, 1)
        [HDR] _CoreColor    ("Core Colour", Color) = (8.0, 3.2, 0.7, 1)
        [HDR] _EdgeColor    ("Crust Edge Glow", Color) = (5.0, 1.1, 0.15, 1)
        _EmissionPower      ("Emission Falloff", Range(0.5, 8)) = 2.0
        _EmissionIntensity  ("Emission Intensity", Range(0, 12)) = 2.5
        _EdgeWidth          ("Edge Glow Width", Range(0.01, 0.5)) = 0.16

        [Header(Pulse)][Space(4)]
        _PulseSpeed         ("Pulse Speed", Float) = 0.5
        _PulseAmount        ("Pulse Amount", Range(0, 1)) = 0.25

        [Header(Surface)][Space(4)]
        _Metallic           ("Metallic", Range(0, 1)) = 0
        _WaveHeight         ("Vertex Swell Height", Float) = 0.0
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 2
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

        CBUFFER_START(UnityPerMaterial)
            float  _FlowScale;
            float  _FlowSpeed;
            float4 _FlowDirection;
            half   _WarpAmount;

            half4  _CrustColor;
            half   _CrustThreshold;
            half   _CrustSoftness;
            half   _CrustSmoothness;
            float  _CrustDetailScale;
            half   _CrustDetailAmount;

            half4  _HotColor;
            half4  _CoreColor;
            half4  _EdgeColor;
            half   _EmissionPower;
            half   _EmissionIntensity;
            half   _EdgeWidth;

            half   _PulseSpeed;
            half   _PulseAmount;

            half   _Metallic;
            float  _WaveHeight;
            half   _Cull;
        CBUFFER_END

        struct Attributes
        {
            float4 positionOS : POSITION;
            float3 normalOS   : NORMAL;
            float2 texcoord   : TEXCOORD0;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };

        // Counter-scrolling warped fbm. The two layers drift against each other
        // so the crust plates appear to slowly tear open and re-seal.
        float LavaField(float2 worldXZ)
        {
            float2 p = worldXZ * _FlowScale;
            float2 dir = normalize(_FlowDirection.xy + float2(1.0e-4, 0.0));
            float t = _Time.y * _FlowSpeed;

            float a = TFA_WarpedFBM2(p + dir * t, _WarpAmount);
            float b = TFA_WarpedFBM2(p * 1.73 - dir.yx * (t * 0.62) + 11.3, _WarpAmount);

            return saturate(a * 0.65 + b * 0.35);
        }

        void LavaShade(float3 positionWS, out half3 albedo, out half3 emission, out half smoothness)
        {
            float field = LavaField(positionWS.xz);

            half crust = smoothstep(_CrustThreshold - _CrustSoftness,
                                    _CrustThreshold + _CrustSoftness, field);
            half heat = 1.0 - crust;

            // Fine detail so the crust is not a flat colour under the camera.
            half detail = TFA_FBM2(positionWS.xz * _CrustDetailScale);
            half3 crustCol = _CrustColor.rgb * lerp(1.0, 0.55 + detail * 0.9, _CrustDetailAmount);

            albedo = lerp(_HotColor.rgb * 0.25, crustCol, crust);
            smoothness = lerp(0.05, _CrustSmoothness, crust);

            half pulse = lerp(1.0, TFA_Pulse(_PulseSpeed, field * 6.2831853), _PulseAmount);

            // Body glow from the molten channels.
            half3 body = lerp(_CoreColor.rgb, _HotColor.rgb, saturate(crust * 2.0))
                       * pow(saturate(heat), _EmissionPower);

            // Crust rims glow hottest - this is what makes it read as lava
            // rather than an orange noise texture.
            half edge = 1.0 - saturate(abs(field - _CrustThreshold) / max(_EdgeWidth, 1.0e-4));
            edge = smoothstep(0.0, 1.0, edge);

            emission = (body + _EdgeColor.rgb * edge * 0.6) * _EmissionIntensity * pulse;
        }
        ENDHLSL

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
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3  normalWS   : TEXCOORD1;
                half   fogFactor  : TEXCOORD2;
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
                float3 positionWS = vp.positionWS;

                if (_WaveHeight > 0.0)
                {
                    positionWS.y += (LavaField(positionWS.xz) - 0.5) * _WaveHeight;
                }

                output.positionWS = positionWS;
                output.positionCS = TransformWorldToHClip(positionWS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.fogFactor = ComputeFogFactor(output.positionCS.z);
                return output;
            }

            half4 ForwardFragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                half3 albedo, emission;
                half smoothness;
                LavaShade(input.positionWS, albedo, emission, smoothness);

                half3 normalWS = NormalizeNormalPerPixel(input.normalWS);

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
                inputData.bakedGI = SampleSH(normalWS);
                inputData.shadowMask = half4(1, 1, 1, 1);

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
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex ShadowVertex
            #pragma fragment ShadowFragment
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            struct VaryingsShadow
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            VaryingsShadow ShadowVertex(Attributes input)
            {
                VaryingsShadow output = (VaryingsShadow)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                if (_WaveHeight > 0.0)
                {
                    positionWS.y += (LavaField(positionWS.xz) - 0.5) * _WaveHeight;
                }
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
                return output;
            }

            half4 ShadowFragment(VaryingsShadow input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
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
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex DepthVertex
            #pragma fragment DepthFragment
            #pragma multi_compile_instancing

            struct VaryingsDepth
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            VaryingsDepth DepthVertex(Attributes input)
            {
                VaryingsDepth output = (VaryingsDepth)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                if (_WaveHeight > 0.0)
                {
                    positionWS.y += (LavaField(positionWS.xz) - 0.5) * _WaveHeight;
                }
                output.positionCS = TransformWorldToHClip(positionWS);
                return output;
            }

            half4 DepthFragment(VaryingsDepth input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                return 0;
            }
            ENDHLSL
        }

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

            struct VaryingsDN
            {
                float4 positionCS : SV_POSITION;
                half3  normalWS   : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            VaryingsDN DepthNormalsVertex(Attributes input)
            {
                VaryingsDN output = (VaryingsDN)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                if (_WaveHeight > 0.0)
                {
                    positionWS.y += (LavaField(positionWS.xz) - 0.5) * _WaveHeight;
                }
                output.positionCS = TransformWorldToHClip(positionWS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return output;
            }

            half4 DepthNormalsFragment(VaryingsDN input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                return half4(NormalizeNormalPerPixel(input.normalWS), 0.0);
            }
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Lit"
}
