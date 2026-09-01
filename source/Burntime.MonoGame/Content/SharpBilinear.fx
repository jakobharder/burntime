#if OPENGL
#define PS_SHADERMODEL ps_3_0
#else
#define PS_SHADERMODEL ps_4_0_level_9_1
#endif

float2 TextureSize;
float2 OutputSize;
sampler2D SpriteTexture : register(s0);

struct PixelShaderInput
{
    float4 Color : COLOR0;
    float2 TextureCoordinates : TEXCOORD0;
};

float4 SharpBilinearPixelShader(PixelShaderInput input) : COLOR0
{
    float2 texel = input.TextureCoordinates * TextureSize;
    float2 scale = OutputSize / TextureSize;
    float2 nearestRegion = 0.5 - 0.5 / scale;
    float2 centerDistance = frac(texel) - 0.5;
    float2 filtered = (centerDistance - clamp(centerDistance,
        -nearestRegion, nearestRegion)) * scale + 0.5;
    float2 sharpCoordinates = (floor(texel) + filtered) / TextureSize;

    return tex2D(SpriteTexture, sharpCoordinates) * input.Color;
}

technique SharpBilinear
{
    pass Pass0
    {
        PixelShader = compile PS_SHADERMODEL SharpBilinearPixelShader();
    }
}
