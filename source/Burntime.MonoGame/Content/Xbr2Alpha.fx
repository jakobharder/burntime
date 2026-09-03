// Alpha-aware variant of Hyllian's xBR-lv2 shader for isolated sprites.
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

float Difference(float a, float b)
{
    return abs(a - b);
}

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

float AlphaLuma(float4 color)
{
    // Ignore undefined RGB in transparent texels, while making an alpha edge
    // significant even when the visible color is black.
    return dot(color.rgb * color.a, LumaWeight) + color.a * XBR_Y_WEIGHT;
}

float4 Premultiply(float4 color)
{
    return float4(color.rgb * color.a, color.a);
}

float ColorDistance(float4 a, float4 b)
{
    float4 difference = abs(a - b);
    return difference.r + difference.g + difference.b + difference.a;
}

float4 Xbr2AlphaPixelShader(PixelShaderInput input) : COLOR0
{
    float2 uv = input.TextureCoordinates;
    float2 pixel = 1.0 / TextureSize;
    float dx = pixel.x;
    float dy = pixel.y;

    float4 A1 = tex2D(SpriteTexture, uv + float2(-dx, -2.0 * dy));
    float4 B1 = tex2D(SpriteTexture, uv + float2(0.0, -2.0 * dy));
    float4 C1 = tex2D(SpriteTexture, uv + float2(dx, -2.0 * dy));
    float4 A = tex2D(SpriteTexture, uv + float2(-dx, -dy));
    float4 B = tex2D(SpriteTexture, uv + float2(0.0, -dy));
    float4 C = tex2D(SpriteTexture, uv + float2(dx, -dy));
    float4 D = tex2D(SpriteTexture, uv + float2(-dx, 0.0));
    float4 E = tex2D(SpriteTexture, uv);
    float4 F = tex2D(SpriteTexture, uv + float2(dx, 0.0));
    float4 G = tex2D(SpriteTexture, uv + float2(-dx, dy));
    float4 H = tex2D(SpriteTexture, uv + float2(0.0, dy));
    float4 I = tex2D(SpriteTexture, uv + float2(dx, dy));
    float4 G5 = tex2D(SpriteTexture, uv + float2(-dx, 2.0 * dy));
    float4 H5 = tex2D(SpriteTexture, uv + float2(0.0, 2.0 * dy));
    float4 I5 = tex2D(SpriteTexture, uv + float2(dx, 2.0 * dy));
    float4 A0 = tex2D(SpriteTexture, uv + float2(-2.0 * dx, -dy));
    float4 D0 = tex2D(SpriteTexture, uv + float2(-2.0 * dx, 0.0));
    float4 G0 = tex2D(SpriteTexture, uv + float2(-2.0 * dx, dy));
    float4 C4 = tex2D(SpriteTexture, uv + float2(2.0 * dx, -dy));
    float4 F4 = tex2D(SpriteTexture, uv + float2(2.0 * dx, 0.0));
    float4 I4 = tex2D(SpriteTexture, uv + float2(2.0 * dx, dy));

    float4 b = float4(AlphaLuma(B), AlphaLuma(D), AlphaLuma(H), AlphaLuma(F));
    float4 c = float4(AlphaLuma(C), AlphaLuma(A), AlphaLuma(G), AlphaLuma(I));
    float4 e = AlphaLuma(E).xxxx;
    float4 d = b.yzwx;
    float4 f = b.wxyz;
    float4 g = c.zwxy;
    float4 h = b.zwxy;
    float4 i = c.wxyz;
    float4 i4 = float4(AlphaLuma(I4), AlphaLuma(C1), AlphaLuma(A0), AlphaLuma(G5));
    float4 i5 = float4(AlphaLuma(I5), AlphaLuma(C4), AlphaLuma(A1), AlphaLuma(G0));
    float4 h5 = float4(AlphaLuma(H5), AlphaLuma(F4), AlphaLuma(B1), AlphaLuma(D0));
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

    float4 pB = Premultiply(B);
    float4 pD = Premultiply(D);
    float4 pE = Premultiply(E);
    float4 pF = Premultiply(F);
    float4 pH = Premultiply(H);
    float4 result1 = pE;
    result1 = lerp(result1, lerp(pH, pF, (float)useF.x), blend.x);
    result1 = lerp(result1, lerp(pB, pD, (float)useF.z), blend.z);
    float4 result2 = pE;
    result2 = lerp(result2, lerp(pF, pB, (float)useF.y), blend.y);
    result2 = lerp(result2, lerp(pD, pH, (float)useF.w), blend.w);
    float4 result = lerp(result1, result2,
        step(ColorDistance(pE, result1), ColorDistance(pE, result2)));

    float alpha = result.a;
    float3 rgb = alpha > 0.00001 ? result.rgb / alpha : float3(0.0, 0.0, 0.0);
    return float4(rgb, alpha) * input.Color;
}

technique Xbr2Alpha
{
    pass Pass0
    {
        PixelShader = compile PS_SHADERMODEL Xbr2AlphaPixelShader();
    }
}
