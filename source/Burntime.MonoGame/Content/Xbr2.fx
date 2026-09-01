// Based on Hyllian's xBR-lv2 shader.
// Copyright (C) 2011-2016 Hyllian (sergiogdb@gmail.com), MIT License.

#if OPENGL
#define PS_SHADERMODEL ps_3_0
#else
#define PS_SHADERMODEL ps_4_0_level_9_1
#endif

#define XBR_SCALE 2.0
#define XBR_Y_WEIGHT 48.0
#define XBR_EQ_THRESHOLD 25.0
#define XBR_LV2_COEFFICIENT 2.0

float2 TextureSize;
sampler2D SpriteTexture : register(s0);

struct PixelShaderInput
{
    float4 Color : COLOR0;
    float2 TextureCoordinates : TEXCOORD0;
};

static const float4 Ao = float4(1.0, -1.0, -1.0, 1.0);
static const float4 Bo = float4(1.0, 1.0, -1.0, -1.0);
static const float4 Co = float4(1.5, 0.5, -0.5, 0.5);
static const float4 Ax = float4(1.0, -1.0, -1.0, 1.0);
static const float4 Bx = float4(0.5, 2.0, -0.5, -2.0);
static const float4 Cx = float4(1.0, 1.0, -0.5, 0.0);
static const float4 Ay = float4(1.0, -1.0, -1.0, 1.0);
static const float4 By = float4(2.0, 0.5, -2.0, -0.5);
static const float4 Cy = float4(2.0, 0.0, -1.0, 0.5);
static const float4 Ci = float4(0.25, 0.25, 0.25, 0.25);
static const float3 LumaWeight =
    XBR_Y_WEIGHT * float3(0.2126, 0.7152, 0.0722);

float4 Difference(float4 a, float4 b)
{
    return abs(a - b);
}

bool4 Equal(float4 a, float4 b)
{
    return Difference(a, b) < XBR_EQ_THRESHOLD;
}

float4 WeightedDistance(float4 a, float4 b, float4 c, float4 d,
    float4 e, float4 f, float4 g, float4 h)
{
    return Difference(a, b) + Difference(a, c) + Difference(d, e) +
        Difference(d, f) + 4.0 * Difference(g, h);
}

float ColorDistance(float3 a, float3 b)
{
    float3 difference = abs(a - b);
    return difference.r + difference.g + difference.b;
}

float4 Xbr2PixelShader(PixelShaderInput input) : COLOR0
{
    float2 uv = input.TextureCoordinates;
    float2 pixel = 1.0 / TextureSize;
    float dx = pixel.x;
    float dy = pixel.y;

    float3 A1 = tex2D(SpriteTexture, uv + float2(-dx, -2.0 * dy)).rgb;
    float3 B1 = tex2D(SpriteTexture, uv + float2(0.0, -2.0 * dy)).rgb;
    float3 C1 = tex2D(SpriteTexture, uv + float2(dx, -2.0 * dy)).rgb;
    float3 A = tex2D(SpriteTexture, uv + float2(-dx, -dy)).rgb;
    float3 B = tex2D(SpriteTexture, uv + float2(0.0, -dy)).rgb;
    float3 C = tex2D(SpriteTexture, uv + float2(dx, -dy)).rgb;
    float3 D = tex2D(SpriteTexture, uv + float2(-dx, 0.0)).rgb;
    float3 E = tex2D(SpriteTexture, uv).rgb;
    float3 F = tex2D(SpriteTexture, uv + float2(dx, 0.0)).rgb;
    float3 G = tex2D(SpriteTexture, uv + float2(-dx, dy)).rgb;
    float3 H = tex2D(SpriteTexture, uv + float2(0.0, dy)).rgb;
    float3 I = tex2D(SpriteTexture, uv + float2(dx, dy)).rgb;
    float3 G5 = tex2D(SpriteTexture, uv + float2(-dx, 2.0 * dy)).rgb;
    float3 H5 = tex2D(SpriteTexture, uv + float2(0.0, 2.0 * dy)).rgb;
    float3 I5 = tex2D(SpriteTexture, uv + float2(dx, 2.0 * dy)).rgb;
    float3 A0 = tex2D(SpriteTexture, uv + float2(-2.0 * dx, -dy)).rgb;
    float3 D0 = tex2D(SpriteTexture, uv + float2(-2.0 * dx, 0.0)).rgb;
    float3 G0 = tex2D(SpriteTexture, uv + float2(-2.0 * dx, dy)).rgb;
    float3 C4 = tex2D(SpriteTexture, uv + float2(2.0 * dx, -dy)).rgb;
    float3 F4 = tex2D(SpriteTexture, uv + float2(2.0 * dx, 0.0)).rgb;
    float3 I4 = tex2D(SpriteTexture, uv + float2(2.0 * dx, dy)).rgb;

    float4 b = float4(dot(B, LumaWeight), dot(D, LumaWeight),
        dot(H, LumaWeight), dot(F, LumaWeight));
    float4 c = float4(dot(C, LumaWeight), dot(A, LumaWeight),
        dot(G, LumaWeight), dot(I, LumaWeight));
    float4 e = dot(E, LumaWeight).xxxx;
    float4 d = b.yzwx;
    float4 f = b.wxyz;
    float4 g = c.zwxy;
    float4 h = b.zwxy;
    float4 i = c.wxyz;
    float4 i4 = float4(dot(I4, LumaWeight), dot(C1, LumaWeight),
        dot(A0, LumaWeight), dot(G5, LumaWeight));
    float4 i5 = float4(dot(I5, LumaWeight), dot(C4, LumaWeight),
        dot(A1, LumaWeight), dot(G0, LumaWeight));
    float4 h5 = float4(dot(H5, LumaWeight), dot(F4, LumaWeight),
        dot(B1, LumaWeight), dot(D0, LumaWeight));
    float4 f4 = h5.yzwx;

    float2 fp = frac(uv * TextureSize);
    float4 fx = Ao * fp.y + Bo * fp.x;
    float4 fxLeft = Ax * fp.y + Bx * fp.x;
    float4 fxUp = Ay * fp.y + By * fp.x;

    bool4 restriction0 = (e != f) && (e != h);
    bool4 restrictionLeft = (e != g) && (d != g);
    bool4 restrictionUp = (e != c) && (b != c);

    float4 delta = 1.0 / XBR_SCALE;
    float4 deltaLeft = float4(0.5, 1.0, 0.5, 1.0) / XBR_SCALE;
    float4 deltaUp = deltaLeft.yxwz;
    float4 fx45i = saturate((fx + delta - Co - Ci) / (2.0 * delta));
    float4 fx45 = saturate((fx + delta - Co) / (2.0 * delta));
    float4 fx30 = saturate((fxLeft + deltaLeft - Cx) / (2.0 * deltaLeft));
    float4 fx60 = saturate((fxUp + deltaUp - Cy) / (2.0 * deltaUp));

    float4 wd1 = WeightedDistance(e, c, g, i, h5, f4, h, f);
    float4 wd2 = WeightedDistance(h, d, i5, f, i4, b, e, i);
    bool4 edri = (wd1 <= wd2) && restriction0;
    bool4 edr = (wd1 < wd2) && restriction0;
    edr = edr && (!edri.yzwx || !edri.wxyz);
    bool4 edrLeft = (XBR_LV2_COEFFICIENT * Difference(f, g) <=
        Difference(h, c)) && restrictionLeft && edr &&
        (!edri.yzwx && Equal(e, c));
    bool4 edrUp = (Difference(f, g) >= XBR_LV2_COEFFICIENT *
        Difference(h, c)) && restrictionUp && edr &&
        (!edri.wxyz && Equal(e, g));

    fx45 *= (float4)edr;
    fx30 *= (float4)edrLeft;
    fx60 *= (float4)edrUp;
    fx45i *= (float4)edri;
    bool4 useF = Difference(e, f) <= Difference(e, h);
    float4 blend = max(max(fx30, fx60), max(fx45, fx45i));

    float3 result1 = E;
    result1 = lerp(result1, lerp(H, F, (float)useF.x), blend.x);
    result1 = lerp(result1, lerp(B, D, (float)useF.z), blend.z);
    float3 result2 = E;
    result2 = lerp(result2, lerp(F, B, (float)useF.y), blend.y);
    result2 = lerp(result2, lerp(D, H, (float)useF.w), blend.w);
    float3 result = lerp(result1, result2,
        step(ColorDistance(E, result1), ColorDistance(E, result2)));

    return float4(result, 1.0) * input.Color;
}

technique Xbr2
{
    pass Pass0
    {
        PixelShader = compile PS_SHADERMODEL Xbr2PixelShader();
    }
}
