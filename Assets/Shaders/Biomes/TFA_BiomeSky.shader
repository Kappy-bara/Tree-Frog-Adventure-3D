// Tree Frog Adventure 3D - procedural biome skybox.
//
// The project ships exactly one HDRI, so a per-biome sky is the cheapest large
// visual win available. Everything here is generated: a three-stop vertical
// gradient, a horizon glow band, a sun disc that tracks the scene's directional
// light, a scrolling cloud layer and a star field.
//
// Assign to Window > Rendering > Lighting > Environment > Skybox Material.
Shader "TreeFrogAdventure/Biome/Sky"
{
    Properties
    {
        [Header(Gradient)][Space(4)]
        _ZenithColor        ("Zenith Colour", Color) = (0.19, 0.44, 0.78, 1)
        _HorizonColor       ("Horizon Colour", Color) = (0.72, 0.85, 0.93, 1)
        _GroundColor        ("Ground Colour", Color) = (0.24, 0.26, 0.28, 1)
        _ZenithPower        ("Zenith Falloff", Range(0.1, 6)) = 0.9
        _GroundPower        ("Ground Falloff", Range(0.1, 6)) = 1.2
        _Exposure           ("Exposure", Range(0, 4)) = 1

        [Header(Horizon Glow)][Space(4)]
        [HDR] _HorizonGlowColor ("Horizon Glow Colour", Color) = (1, 0.85, 0.6, 1)
        _HorizonGlowStrength    ("Horizon Glow Strength", Range(0, 4)) = 0
        _HorizonBandFalloff     ("Horizon Band Falloff", Range(1, 60)) = 14

        [Header(Sun)][Space(4)]
        [HDR] _SunColor     ("Sun Colour", Color) = (2.4, 2.2, 1.9, 1)
        _SunSize            ("Sun Size", Range(0.0001, 0.2)) = 0.004
        _SunIntensity       ("Sun Intensity", Range(0, 20)) = 3
        _SunHaloPower       ("Sun Halo Falloff", Range(1, 400)) = 60
        _SunHaloStrength    ("Sun Halo Strength", Range(0, 4)) = 0.7
        _SunDirOverride     ("Sun Dir Override (XYZ, W=use)", Vector) = (0, 1, 0, 0)

        [Header(Clouds)][Space(4)]
        _CloudColor         ("Cloud Colour", Color) = (1, 1, 1, 1)
        _CloudShadowColor   ("Cloud Shadow Colour", Color) = (0.62, 0.66, 0.74, 1)
        _CloudStrength      ("Cloud Strength", Range(0, 1)) = 0.6
        _CloudScale         ("Cloud Scale", Float) = 1.4
        _CloudSpeed         ("Cloud Speed", Float) = 0.012
        _CloudDirection     ("Cloud Direction (XZ)", Vector) = (1, 0.3, 0, 0)
        _CloudCoverage      ("Cloud Coverage", Range(0, 1)) = 0.52
        _CloudSoftness      ("Cloud Softness", Range(0.01, 0.6)) = 0.22
        _CloudWarp          ("Cloud Warp", Range(0, 3)) = 1.1
        _CloudHorizonFade   ("Cloud Horizon Fade", Range(0.001, 0.6)) = 0.06
        _CloudHeightBias    ("Cloud Plane Bias", Range(0.01, 1)) = 0.12

        [Header(Stars)][Space(4)]
        [HDR] _StarColor    ("Star Colour", Color) = (1, 0.95, 0.9, 1)
        _StarStrength       ("Star Strength", Range(0, 4)) = 0
        _StarScale          ("Star Scale", Float) = 90
        _StarDensity        ("Star Density", Range(0.9, 1)) = 0.985
        _StarTwinkle        ("Star Twinkle", Range(0, 1)) = 0.5
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Background"
            "RenderType" = "Background"
            "PreviewType" = "Skybox"
        }

        Cull Off
        ZWrite Off
        ZTest LEqual

        Pass
        {
            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex SkyVertex
            #pragma fragment SkyFragment

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "TFA_BiomeCommon.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4  _ZenithColor;
                half4  _HorizonColor;
                half4  _GroundColor;
                half   _ZenithPower;
                half   _GroundPower;
                half   _Exposure;

                half4  _HorizonGlowColor;
                half   _HorizonGlowStrength;
                half   _HorizonBandFalloff;

                half4  _SunColor;
                half   _SunSize;
                half   _SunIntensity;
                half   _SunHaloPower;
                half   _SunHaloStrength;
                float4 _SunDirOverride;

                half4  _CloudColor;
                half4  _CloudShadowColor;
                half   _CloudStrength;
                float  _CloudScale;
                float  _CloudSpeed;
                float4 _CloudDirection;
                half   _CloudCoverage;
                half   _CloudSoftness;
                half   _CloudWarp;
                half   _CloudHorizonFade;
                half   _CloudHeightBias;

                half4  _StarColor;
                half   _StarStrength;
                float  _StarScale;
                half   _StarDensity;
                half   _StarTwinkle;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 dirWS      : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings SkyVertex(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                // Unity draws the skybox with an identity object-to-world
                // rotation, so the object-space vertex IS the view direction.
                output.dirWS = input.positionOS.xyz;
                return output;
            }

            half3 SampleClouds(float3 dir, half3 sky)
            {
                if (_CloudStrength <= 0.0)
                    return sky;

                // Project the view ray onto a plane at infinity. The bias keeps
                // the projection from exploding as the ray approaches horizontal.
                float2 uv = dir.xz / max(abs(dir.y) + _CloudHeightBias, 0.02);
                uv *= _CloudScale;

                float2 drift = normalize(_CloudDirection.xy + float2(1.0e-4, 0.0));
                uv += drift * (_Time.y * _CloudSpeed * 60.0);

                float c = TFA_WarpedFBM2(uv, _CloudWarp);
                float cover = smoothstep(_CloudCoverage, _CloudCoverage + _CloudSoftness, c);

                // Clouds only above the horizon, fading in as the ray lifts.
                cover *= smoothstep(0.0, _CloudHorizonFade, dir.y);
                cover = saturate(cover * _CloudStrength);

                half3 cloudCol = lerp(_CloudShadowColor.rgb, _CloudColor.rgb, saturate(c * 1.5));
                return lerp(sky, cloudCol, cover);
            }

            half3 SampleStars(float3 dir)
            {
                if (_StarStrength <= 0.0)
                    return half3(0, 0, 0);

                float2 uv = (dir.xz / max(dir.y, 0.08)) * _StarScale;
                float2 cell = floor(uv);

                float r = TFA_Hash21(cell);
                float present = step(_StarDensity, r);

                // Jitter the star inside its cell so the field is not a grid.
                float2 jitter = float2(TFA_Hash21(cell + 3.7), TFA_Hash21(cell + 9.1)) * 0.6 + 0.2;
                float d = length(frac(uv) - jitter);

                float point_ = smoothstep(0.09, 0.0, d) * present;
                float twinkle = lerp(1.0, TFA_Pulse(2.5, r * 40.0), _StarTwinkle);

                // Fade out toward the horizon where haze would wash them out.
                return _StarColor.rgb * point_ * twinkle * _StarStrength * saturate(dir.y * 3.0);
            }

            half4 SkyFragment(Varyings input) : SV_Target
            {
                float3 dir = normalize(input.dirWS);
                float h = dir.y;

                // ---- base gradient ------------------------------------------
                half upT = pow(saturate(h), _ZenithPower);
                half downT = pow(saturate(-h), _GroundPower);

                half3 sky = lerp(_HorizonColor.rgb, _ZenithColor.rgb, upT);
                sky = lerp(sky, _GroundColor.rgb, downT);

                // ---- horizon glow band --------------------------------------
                half band = exp(-abs(h) * _HorizonBandFalloff);
                sky += _HorizonGlowColor.rgb * band * _HorizonGlowStrength;

                // ---- stars (behind the clouds) ------------------------------
                sky += SampleStars(dir);

                // ---- clouds --------------------------------------------------
                sky = SampleClouds(dir, sky);

                // ---- sun -----------------------------------------------------
                float3 sunDir = (_SunDirOverride.w > 0.5)
                    ? normalize(_SunDirOverride.xyz + float3(0, 1.0e-4, 0))
                    : normalize(_MainLightPosition.xyz);

                half sd = saturate(dot(dir, sunDir));

                half disc = smoothstep(1.0 - _SunSize, 1.0 - _SunSize * 0.35, sd);
                half halo = pow(sd, _SunHaloPower) * _SunHaloStrength;

                sky += _SunColor.rgb * (disc * _SunIntensity + halo);

                return half4(sky * _Exposure, 1.0);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
