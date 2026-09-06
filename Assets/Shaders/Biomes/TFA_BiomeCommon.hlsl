#ifndef TFA_BIOME_COMMON_INCLUDED
#define TFA_BIOME_COMMON_INCLUDED

// -----------------------------------------------------------------------------
// Tree Frog Adventure 3D - shared biome shader helpers.
//
// Include AFTER URP Core.hlsl (needs _Time and the core math macros).
// Every TFA_Biome* shader pulls its noise / colour / wind math from here so the
// four biomes stay visually consistent and there is one place to tune.
// -----------------------------------------------------------------------------

// ---------------------------------------------------------------- hashing ----

float TFA_Hash11(float p)
{
    p = frac(p * 0.1031);
    p *= p + 33.33;
    p *= p + p;
    return frac(p);
}

float TFA_Hash21(float2 p)
{
    float3 p3 = frac(p.xyx * 0.1031);
    p3 += dot(p3, p3.yzx + 33.33);
    return frac((p3.x + p3.y) * p3.z);
}

float TFA_Hash31(float3 p)
{
    p = frac(p * 0.1031);
    p += dot(p, p.zyx + 31.32);
    return frac((p.x + p.y) * p.z);
}

// ------------------------------------------------------------------ noise ----

float TFA_ValueNoise2(float2 p)
{
    float2 i = floor(p);
    float2 f = frac(p);
    f = f * f * (3.0 - 2.0 * f);

    float a = TFA_Hash21(i);
    float b = TFA_Hash21(i + float2(1.0, 0.0));
    float c = TFA_Hash21(i + float2(0.0, 1.0));
    float d = TFA_Hash21(i + float2(1.0, 1.0));

    return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
}

float TFA_ValueNoise3(float3 p)
{
    float3 i = floor(p);
    float3 f = frac(p);
    f = f * f * (3.0 - 2.0 * f);

    float n000 = TFA_Hash31(i + float3(0.0, 0.0, 0.0));
    float n100 = TFA_Hash31(i + float3(1.0, 0.0, 0.0));
    float n010 = TFA_Hash31(i + float3(0.0, 1.0, 0.0));
    float n110 = TFA_Hash31(i + float3(1.0, 1.0, 0.0));
    float n001 = TFA_Hash31(i + float3(0.0, 0.0, 1.0));
    float n101 = TFA_Hash31(i + float3(1.0, 0.0, 1.0));
    float n011 = TFA_Hash31(i + float3(0.0, 1.0, 1.0));
    float n111 = TFA_Hash31(i + float3(1.0, 1.0, 1.0));

    float x00 = lerp(n000, n100, f.x);
    float x10 = lerp(n010, n110, f.x);
    float x01 = lerp(n001, n101, f.x);
    float x11 = lerp(n011, n111, f.x);

    return lerp(lerp(x00, x10, f.y), lerp(x01, x11, f.y), f.z);
}

// Fixed-octave fbm, unrolled rather than looped so every target compiles the
// same way and there is no dynamic-branch cost.

float TFA_FBM2(float2 p)
{
    float s = 0.0;
    float a = 0.5;
    s += a * TFA_ValueNoise2(p); p *= 2.03; a *= 0.5;
    s += a * TFA_ValueNoise2(p); p *= 2.01; a *= 0.5;
    s += a * TFA_ValueNoise2(p); p *= 2.05; a *= 0.5;
    s += a * TFA_ValueNoise2(p);
    return s / 0.9375;
}

float TFA_FBM3(float3 p)
{
    float s = 0.0;
    float a = 0.5;
    s += a * TFA_ValueNoise3(p); p *= 2.02; a *= 0.5;
    s += a * TFA_ValueNoise3(p); p *= 2.04; a *= 0.5;
    s += a * TFA_ValueNoise3(p);
    return s / 0.875;
}

// Domain-warped fbm. Gives lava and cloud layers their curling, non-uniform
// look instead of the obvious grid that plain fbm produces.
float TFA_WarpedFBM2(float2 p, float warp)
{
    float2 q = float2(TFA_FBM2(p), TFA_FBM2(p + float2(5.2, 1.3)));
    return TFA_FBM2(p + warp * q);
}

// ------------------------------------------------------------------ colour ---

float TFA_Luminance(float3 c)
{
    return dot(c, float3(0.2126, 0.7152, 0.0722));
}

float3 TFA_RGBToHSV(float3 c)
{
    float4 K = float4(0.0, -1.0 / 3.0, 2.0 / 3.0, -1.0);
    float4 p = lerp(float4(c.bg, K.wz), float4(c.gb, K.xy), step(c.b, c.g));
    float4 q = lerp(float4(p.xyw, c.r), float4(c.r, p.yzx), step(p.x, c.r));
    float d = q.x - min(q.w, q.y);
    float e = 1.0e-10;
    return float3(abs(q.z + (q.w - q.y) / (6.0 * d + e)), d / (q.x + e), q.x);
}

float3 TFA_HSVToRGB(float3 c)
{
    float4 K = float4(1.0, 2.0 / 3.0, 1.0 / 3.0, 3.0);
    float3 p = abs(frac(c.xxx + K.xyz) * 6.0 - K.www);
    return c.z * lerp(K.xxx, saturate(p - K.xxx), c.y);
}

// Hue rotate + saturate/desaturate + brighten. The workhorse for turning
// Emerald Forest art into another biome without touching the source textures.
float3 TFA_HueShift(float3 rgb, float hueShift, float satMul, float valMul)
{
    float3 hsv = TFA_RGBToHSV(rgb);
    hsv.x = frac(hsv.x + hueShift);
    hsv.y = saturate(hsv.y * satMul);
    hsv.z = hsv.z * valMul;
    return TFA_HSVToRGB(hsv);
}

// Three-stop colour ramp. Feeding this the LUMINANCE of a shared texture atlas
// recolours every object using that atlas at once - this is how the single
// UNS_Color_Palette material becomes forest stone, alpine granite or basalt.
float3 TFA_Ramp3(float t, float3 shadowCol, float3 midCol, float3 highCol, float midPoint)
{
    t = saturate(t);
    midPoint = clamp(midPoint, 0.001, 0.999);

    float lowT  = smoothstep(0.0, 1.0, saturate(t / midPoint));
    float highT = smoothstep(0.0, 1.0, saturate((t - midPoint) / (1.0 - midPoint)));

    float3 low  = lerp(shadowCol, midCol, lowT);
    float3 high = lerp(midCol, highCol, highT);

    return (t < midPoint) ? low : high;
}

// ------------------------------------------------------------------- masks ---

// Coverage on upward-facing surfaces: snow caps, ash fall, moss on flat tops.
// coverage 0 = nothing, 1 = everything including undersides.
float TFA_UpMask(float3 normalWS, float coverage, float softness)
{
    float threshold = lerp(1.05, -1.05, saturate(coverage));
    float w = max(softness, 1.0e-3);
    return smoothstep(threshold - w, threshold + w, normalWS.y);
}

// 0 at bottom, 1 at top of a world-space band. Drives root-to-tip foliage
// gradients and the waterline slime on swamp geometry.
float TFA_HeightMask(float y, float bottom, float top)
{
    return saturate((y - bottom) / max(top - bottom, 1.0e-4));
}

float TFA_Pulse(float speed, float phase)
{
    return 0.5 + 0.5 * sin(_Time.y * speed + phase);
}

// -------------------------------------------------------------------- wind ---

// windParams: x = strength, y = speed, z = spatial frequency, w = flutter.
// mask is 0 at the trunk base and 1 at the leaf tips, so trunks stay planted.
float3 TFA_WindOffset(float3 positionWS, float mask, float4 windParams, float3 windDir)
{
    if (mask <= 0.0 || windParams.x <= 0.0)
        return float3(0.0, 0.0, 0.0);

    float t = _Time.y * windParams.y;
    float phase = dot(positionWS.xz, float2(0.35, 0.27)) * windParams.z;

    // Two sines at incommensurable rates so the sway never visibly loops.
    float sway = sin(t + phase) + 0.5 * sin(t * 1.73 + phase * 2.31);
    float flutter = sin(t * 4.7 + positionWS.y * 3.1 + phase * 5.0) * windParams.w;

    float3 dir = normalize(windDir + float3(1.0e-4, 0.0, 0.0));
    float3 offset = dir * (sway * 0.5 + flutter * 0.25) * windParams.x * mask;

    // Foliage dips slightly as it is pushed; pure lateral sway reads as sliding.
    offset.y -= abs(sway) * 0.08 * windParams.x * mask;

    return offset;
}

// ------------------------------------------------------------- atmosphere ----

// Distance haze applied in the surface shader, on top of URP fog. Lets each
// biome push a signature tint into the far field (swamp murk, ember glow)
// without changing global fog settings that also affect UI and skybox.
float3 TFA_ApplyHaze(float3 color, float viewDepth, float3 hazeColor, float hazeStart, float hazeEnd, float hazeStrength)
{
    float f = saturate((viewDepth - hazeStart) / max(hazeEnd - hazeStart, 1.0e-4));
    return lerp(color, hazeColor, f * saturate(hazeStrength));
}

#endif // TFA_BIOME_COMMON_INCLUDED
