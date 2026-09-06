// Tree Frog Adventure 3D - biome water.
//
// The project ships no water textures, so every wave, ripple and foam edge here
// is procedural. Shallow/deep colour and the shore foam line come from scene
// depth, which is why this needs Depth Texture enabled on the URP asset
// (it already is on PC_RPAsset).
Shader "TreeFrogAdventure/Biome/Water"
{
    Properties
    {
        [Header(Colour)][Space(4)]
        _ShallowColor       ("Shallow Colour", Color) = (0.35, 0.72, 0.78, 0.35)
        _DeepColor          ("Deep Colour", Color) = (0.05, 0.20, 0.30, 0.92)
        _DepthFadeDistance  ("Depth Fade Distance", Float) = 3.0
        _Murkiness          ("Murkiness", Range(0, 1)) = 0.0

        [Header(Waves)][Space(4)]
        _WaveScale          ("Wave Scale", Float) = 0.35
        _WaveSpeed          ("Wave Speed", Float) = 0.35
        _WaveStrength       ("Wave Normal Strength", Range(0, 8)) = 2.0
        _WaveHeight         ("Vertex Wave Height", Float) = 0.0
        _WaveDirection      ("Wave Direction (XZ)", Vector) = (1, 0.7, 0, 0)

        [Header(Shore Foam)][Space(4)]
        _FoamColor          ("Foam Colour", Color) = (1, 1, 1, 1)
        _FoamDistance       ("Foam Width", Float) = 0.6
        _FoamScale          ("Foam Noise Scale", Float) = 3.0
        _FoamSpeed          ("Foam Noise Speed", Float) = 0.4
        _FoamCutoff         ("Foam Hardness", Range(0, 1)) = 0.35

        [Header(Surface)][Space(4)]
        _Smoothness         ("Smoothness", Range(0, 1)) = 0.9
        _Metallic           ("Metallic", Range(0, 1)) = 0
        _FresnelPower       ("Fresnel Power", Range(0.5, 12)) = 4
        _FresnelStrength    ("Fresnel Strength", Range(0, 2)) = 0.5
        _FresnelColor       ("Fresnel Colour", Color) = (0.7, 0.9, 1.0, 1)

        [Header(Refraction)][Space(4)]
        _RefractionStrength ("Refraction Strength", Range(0, 1)) = 0.25

        [Header(Emission   bioluminescence)][Space(4)]
        [HDR] _EmissionColor ("Emission Colour", Color) = (0, 0, 0, 1)
        _EmissionScale       ("Emission Noise Scale", Float) = 1.5
        _EmissionThreshold   ("Emission Threshold", Range(0, 1)) = 0.72
        _EmissionPulseSpeed  ("Emission Pulse Speed", Float) = 1.0
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "ShaderModel" = "4.5"
        }
        LOD 300

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Back

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex WaterVertex
            #pragma fragment WaterFragment

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ EVALUATE_SH_MIXED EVALUATE_SH_VERTEX
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _LIGHT_COOKIES
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"
            #include "TFA_BiomeCommon.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4  _ShallowColor;
                half4  _DeepColor;
                float  _DepthFadeDistance;
                half   _Murkiness;

                float  _WaveScale;
                float  _WaveSpeed;
                half   _WaveStrength;
                float  _WaveHeight;
                float4 _WaveDirection;

                half4  _FoamColor;
                float  _FoamDistance;
                float  _FoamScale;
                float  _FoamSpeed;
                half   _FoamCutoff;

                half   _Smoothness;
                half   _Metallic;
                half   _FresnelPower;
                half   _FresnelStrength;
                half4  _FresnelColor;

                half   _RefractionStrength;

                half4  _EmissionColor;
                float  _EmissionScale;
                half   _EmissionThreshold;
                half   _EmissionPulseSpeed;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 texcoord   : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3  normalWS   : TEXCOORD1;
                float4 screenPos  : TEXCOORD2;
                half   fogFactor  : TEXCOORD3;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            // Two counter-scrolling fbm layers. Counter-scrolling matters: two
            // layers moving the same way just reads as the whole plane sliding.
            float WaveHeightAt(float2 p)
            {
                float2 dir = normalize(_WaveDirection.xy + float2(1.0e-4, 0.0));
                float t = _Time.y * _WaveSpeed;
                float a = TFA_FBM2(p + dir * t);
                float b = TFA_FBM2(p * 2.3 - dir.yx * (t * 0.8));
                return a * 0.6 + b * 0.4;
            }

            half3 WaveNormal(float2 worldXZ)
            {
                float2 p = worldXZ * _WaveScale;
                const float e = 0.06;

                float hx = WaveHeightAt(p + float2(e, 0)) - WaveHeightAt(p - float2(e, 0));
                float hz = WaveHeightAt(p + float2(0, e)) - WaveHeightAt(p - float2(0, e));

                return normalize(half3(-hx * _WaveStrength, 1.0, -hz * _WaveStrength));
            }

            Varyings WaterVertex(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                VertexPositionInputs vp = GetVertexPositionInputs(input.positionOS.xyz);
                float3 positionWS = vp.positionWS;

                if (_WaveHeight > 0.0)
                {
                    positionWS.y += (WaveHeightAt(positionWS.xz * _WaveScale) - 0.5) * _WaveHeight;
                }

                output.positionWS = positionWS;
                output.positionCS = TransformWorldToHClip(positionWS);
                output.normalWS   = TransformObjectToWorldNormal(input.normalOS);
                output.screenPos  = ComputeScreenPos(output.positionCS);
                output.fogFactor  = ComputeFogFactor(output.positionCS.z);
                return output;
            }

            half4 WaterFragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float2 screenUV = input.screenPos.xy / max(input.screenPos.w, 1.0e-5);

                half3 waveN = WaveNormal(input.positionWS.xz);
                half3 geoN = NormalizeNormalPerPixel(input.normalWS);

                // WaveNormal returns a y-up normal. Reorient it onto the
                // surface's own frame so tilted water planes still work.
                half3 helper = (abs(geoN.y) > 0.99) ? half3(1, 0, 0) : half3(0, 1, 0);
                half3 tangentWS = normalize(cross(geoN, helper));
                half3 bitangentWS = cross(geoN, tangentWS);
                half3 normalWS = normalize(waveN.x * tangentWS + waveN.y * geoN + waveN.z * bitangentWS);

                // --- how deep is the water under this pixel -------------------
                float sceneDepthRaw = SampleSceneDepth(screenUV);
                float sceneDepthEye = LinearEyeDepth(sceneDepthRaw, _ZBufferParams);
                float surfaceDepthEye = input.screenPos.w;
                float waterDepth = max(sceneDepthEye - surfaceDepthEye, 0.0);

                float depthFade = saturate(waterDepth / max(_DepthFadeDistance, 1.0e-4));

                half4 waterCol = lerp(_ShallowColor, _DeepColor, depthFade);

                // --- refraction ----------------------------------------------
                half3 refracted = waterCol.rgb;
                if (_RefractionStrength > 0.0)
                {
                    float2 offset = waveN.xz * _RefractionStrength * 0.06;
                    // Fade the distortion out at the shoreline, otherwise the
                    // beach smears over the water edge.
                    offset *= saturate(waterDepth * 2.0);
                    half3 sceneCol = SampleSceneColor(screenUV + offset);
                    refracted = lerp(sceneCol, waterCol.rgb, saturate(depthFade + _Murkiness));
                }

                half alpha = lerp(waterCol.a, 1.0, _Murkiness * depthFade);
                alpha = saturate(alpha);

                // --- shore foam ----------------------------------------------
                float foamNoise = TFA_FBM2(input.positionWS.xz * _FoamScale
                                           + _Time.y * _FoamSpeed * float2(0.6, -0.4));
                float foamWidth = _FoamDistance * (0.55 + foamNoise * 0.9);
                float foam = 1.0 - smoothstep(0.0, max(foamWidth, 1.0e-4), waterDepth);
                foam = smoothstep(_FoamCutoff, 1.0, foam) * _FoamColor.a;

                half3 albedo = lerp(refracted, _FoamColor.rgb, foam);
                alpha = saturate(alpha + foam);

                // --- lighting -------------------------------------------------
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
                inputData.normalizedScreenSpaceUV = screenUV;
                inputData.bakedGI = SampleSH(normalWS);
                inputData.shadowMask = half4(1, 1, 1, 1);

                half3 emission = half3(0, 0, 0);
                if (max(_EmissionColor.r, max(_EmissionColor.g, _EmissionColor.b)) > 0.0)
                {
                    float n = TFA_FBM2(input.positionWS.xz * _EmissionScale
                                       + _Time.y * 0.05 * float2(0.3, 0.2));
                    float m = smoothstep(_EmissionThreshold, _EmissionThreshold + 0.12, n);
                    emission = _EmissionColor.rgb * m * TFA_Pulse(_EmissionPulseSpeed, n * 6.28);
                }

                SurfaceData surfaceData = (SurfaceData)0;
                surfaceData.albedo = albedo;
                surfaceData.alpha = 1.0;
                surfaceData.metallic = _Metallic;
                surfaceData.smoothness = lerp(_Smoothness, 0.2, foam);
                surfaceData.occlusion = 1.0;
                surfaceData.normalTS = half3(0, 0, 1);
                surfaceData.emission = emission;

                half4 color = UniversalFragmentPBR(inputData, surfaceData);

                // Grazing-angle rim. Water seen edge-on is mostly sky.
                half fresnel = pow(1.0 - saturate(dot(normalWS, inputData.viewDirectionWS)),
                                   _FresnelPower);
                color.rgb += _FresnelColor.rgb * fresnel * _FresnelStrength * (1.0 - foam);
                alpha = saturate(alpha + fresnel * _FresnelStrength * 0.5);

                color.rgb = MixFog(color.rgb, inputData.fogCoord);

                return half4(color.rgb, alpha);
            }
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Lit"
}
