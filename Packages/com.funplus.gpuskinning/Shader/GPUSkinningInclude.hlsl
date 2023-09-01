// Upgrade NOTE: upgraded instancing buffer 'GPUSkinningProperties0' to new syntax.
// Upgrade NOTE: upgraded instancing buffer 'GPUSkinningProperties1' to new syntax.
// Upgrade NOTE: upgraded instancing buffer 'GPUSkinningProperties2' to new syntax.

#ifndef GPUSKINNING_INCLUDE
#define GPUSKINNING_INCLUDE

Texture2D _BaseMap;
SamplerState sampler_BaseMap;
sampler2D _GPUSkinning_TextureMatrix;

//CBUFFER_START(UnityPerMaterial)
half4 _BaseMap_ST;
half4 _BaseColor;
float4 _GPUSkinning_TextureSize_NumPixelsPerFrame;
float _DitherFactor;
//CBUFFER_END

UNITY_INSTANCING_BUFFER_START(GPUSkinningProperties0)
	UNITY_DEFINE_INSTANCED_PROP(float2, _GPUSkinning_FrameIndex_PixelSegmentation)
    // Foundation: 命名重复了，并且这个cbuffer中的prop和gpu skin有啥关系。
    UNITY_DEFINE_INSTANCED_PROP(half4, _TintColor)
    UNITY_DEFINE_INSTANCED_PROP(half, _GPUSkinning_BlendOn)
    UNITY_DEFINE_INSTANCED_PROP(float3, _GPUSkinning_CrossFade)
UNITY_INSTANCING_BUFFER_END(GPUSkinningProperties0)

inline float4 indexToUV(float index)
{
	int row = (int)(index / _GPUSkinning_TextureSize_NumPixelsPerFrame.x);
	float col = index - row * _GPUSkinning_TextureSize_NumPixelsPerFrame.x;
	return float4((col + 0.5) / _GPUSkinning_TextureSize_NumPixelsPerFrame.x, (row + 0.5) / _GPUSkinning_TextureSize_NumPixelsPerFrame.y, 0, 0);
}

inline float4x4 getMatrix(int frameStartIndex, float boneIndex)
{
	float matStartIndex = frameStartIndex + boneIndex * 3;
	float4 row0 = tex2Dlod(_GPUSkinning_TextureMatrix, indexToUV(matStartIndex));
	float4 row1 = tex2Dlod(_GPUSkinning_TextureMatrix, indexToUV(matStartIndex + 1));
	float4 row2 = tex2Dlod(_GPUSkinning_TextureMatrix, indexToUV(matStartIndex + 2));
	float4 row3 = float4(0, 0, 0, 1);
	float4x4 mat = float4x4(row0, row1, row2, row3);
	return mat;
}

inline float getFrameStartIndex()
{
	float2 frameIndex_segment = UNITY_ACCESS_INSTANCED_PROP(GPUSkinningProperties0, _GPUSkinning_FrameIndex_PixelSegmentation);
	float segment = frameIndex_segment.y;
	float frameIndex = frameIndex_segment.x;
	float frameStartIndex = segment + frameIndex * _GPUSkinning_TextureSize_NumPixelsPerFrame.z;
	return frameStartIndex;
}

inline float getCrossFadeFrameStartIndex()
{
    float3 frameIndex_segment = UNITY_ACCESS_INSTANCED_PROP(GPUSkinningProperties0, _GPUSkinning_CrossFade);
    float segment = frameIndex_segment.y;
    float frameIndex = frameIndex_segment.x;
    float frameStartIndex = segment + frameIndex * _GPUSkinning_TextureSize_NumPixelsPerFrame.z;
    return frameStartIndex;
}

#define blendOn UNITY_ACCESS_INSTANCED_PROP(GPUSkinningProperties0, _GPUSkinning_BlendOn)
#define crossFadeBlend UNITY_ACCESS_INSTANCED_PROP(GPUSkinningProperties0, _GPUSkinning_CrossFade).z

#define textureMatrix(uv2, uv3) float frameStartIndex = getFrameStartIndex(); \
								float4x4 mat0 = getMatrix(frameStartIndex, uv2.x); \
								float4x4 mat1 = getMatrix(frameStartIndex, uv2.z); \
								float4x4 mat2 = getMatrix(frameStartIndex, uv3.x); \
								float4x4 mat3 = getMatrix(frameStartIndex, uv3.z);

#define crossFadeTextureMatrix(uv2, uv3) float crossFadeFrameStartIndex = getCrossFadeFrameStartIndex(); \
											float4x4 mat0_crossFade = getMatrix(crossFadeFrameStartIndex, uv2.x); \
											float4x4 mat1_crossFade = getMatrix(crossFadeFrameStartIndex, uv2.z); \
											float4x4 mat2_crossFade = getMatrix(crossFadeFrameStartIndex, uv3.x); \
											float4x4 mat3_crossFade = getMatrix(crossFadeFrameStartIndex, uv3.z);

#define skin1Pos(mat0, mat1, mat2, mat3) mul(mat0, vertex) * uv2.y;

#define skin2Pos(mat0, mat1, mat2, mat3) mul(mat0, vertex) * uv2.y + \
									            mul(mat1, vertex) * uv2.w;

#define skin4Pos(mat0, mat1, mat2, mat3) mul(mat0, vertex) * uv2.y + \
												mul(mat1, vertex) * uv2.w + \
												mul(mat2, vertex) * uv3.y + \
												mul(mat3, vertex) * uv3.w;

#define skin_blend(pos0, pos1) pos1.xyz + (pos0.xyz - pos1.xyz) * crossFadeBlend

#define BlendOff(quality) textureMatrix(uv2, uv3); \
                        return skin##quality##Pos(mat0, mat1, mat2, mat3);

#define BlendOn(quality) textureMatrix(uv2, uv3); \
                        crossFadeTextureMatrix(uv2, uv3); \
                        float4 pos0 = skin##quality##Pos(mat0, mat1, mat2, mat3); \
                        float4 pos1 = skin##quality##Pos(mat0_crossFade, mat1_crossFade, mat2_crossFade, mat3_crossFade); \
                        return float4(skin_blend(pos0, pos1), 1);

inline float4 skin1(float4 vertex, float4 uv2, float4 uv3)
{
#ifdef _FORCE_BLEND_OFF
    BlendOff(1)
#else
    if(blendOn != 0)
    {
        BlendOn(1)
    }
    else
    {
        BlendOff(1)
    }
#endif
}

inline float4 skin2(float4 vertex, float4 uv2, float4 uv3)
{
#ifdef _FORCE_BLEND_OFF
    BlendOff(2)
#else
    if(blendOn != 0)
    {
        BlendOn(2)
    }
    else
    {
        BlendOff(2)
    }
#endif
}

inline float4 skin4(float4 vertex, float4 uv2, float4 uv3)
{
#ifdef _FORCE_BLEND_OFF
    BlendOff(4)
#else
    if(blendOn != 0)
    {
        BlendOn(4)
    }
    else
    {
        BlendOff(4)
    }
#endif
}

#endif