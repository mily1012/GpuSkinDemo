Shader "GPUSkinning/SimpleLit"
{
	Properties
	{
		[MainTexture] _BaseMap("Base Map (RGB) Smoothness / Alpha (A)", 2D) = "white" {}
		[MainColor] _BaseColor("Base Color", Color) = (1, 1, 1, 1)
		//("Specular Color", Color) = (0.5, 0.5, 0.5, 0.5)
        [HDR] _TintColor("_TintColor", Color) = (0,0,0,0)
		_GPUSkinning_TextureSize_NumPixelsPerFrame("_GPUSkinning_TextureSize_NumPixelsPerFrame", Vector) = (0,0,0,0)
		_GPUSkinning_TextureMatrix("_GPUSkinning_TextureMatrix", 2D) = "white" {}

        [Toggle(_SOBEL_OUTLINE)]_SobelOutline("Use Sobel Outline", float) = 0.0
        _OutlineWidth("Outline Width", Range(1, 10)) = 3.0
        [HDR]_OutlineColor("Outine Color", Color) = (1, 1, 1, 1)

        [Enum(One, 1, SrcAlpha, 5)] _SrcBlend("SrcBlend", Float) = 1.0
        [Enum(Zero, 0, OneMinusSrcAlpha, 10)] _DstBlend("DstBlend", Float) = 0.0
        [Enum(On, 1, Off, 0)] _ZWrite("ZWrite", Float) = 1.0
		[Toggle(_FORCE_BLEND_OFF)] _ForceBlendOff("Force Blend Off", Float) = 0.0
		[KeywordEnum(TWO, ONE)] _BoneCount("Bone Count Per Vertex", Float) = 0.0

        _DitherFactor("DitherFactor", Range(0, 1)) = 1.0
		[Toggle]_Breathing("Breathing", Float) = 0
		[HDR]_BreathingColor("BreathingColor", Color) = (1, 1, 1, 1)
		_BreathingSpeed("BreathingSpeed", Float) = 10
	}

	SubShader
	{
		Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }

		Cull Back
		HLSLINCLUDE
		#pragma target 3.0
		#pragma exclude_renderers gles
		#pragma exclude_renderers d3d11_9x
		ENDHLSL

		Pass
		{
			Name "Forward"
			Tags { "LightMode"="UniversalForward" }

            Blend[_SrcBlend][_DstBlend]
            ZWrite[_ZWrite]

			HLSLPROGRAM
			#pragma vertex vert
			#pragma fragment frag

			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
			//#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/UnityInstancing.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
			//#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ShaderGraphFunctions.hlsl"
			#include "GPUSkinningInclude.hlsl"
			// #include "DCommon.hlsl"

			#pragma multi_compile_instancing
            #pragma multi_compile _ _SEAMLESS_DITHERING
			#pragma multi_compile _ _USE_CUSTOM_FOG
			#pragma multi_compile _ _SOBEL_OUTLINE
			#pragma multi_compile _ _FORCE_BLEND_OFF
			#pragma multi_compile _BONECOUNT_TWO _BONECOUNT_ONE

			CBUFFER_START(UnityPerMaterial)
			//float4 _BaseMap_ST;
			float4 _NormalMap_ST;
			float4 _NormalMap_TexelSize;
			float4 _EmissionColor;
			float4 _MixMap_ST;
			float4 _EmissionMap_ST;
			float _EmissionScale;
			float _Metallic;
			float _Smoothness;
			CBUFFER_END

			// FP Custom Fog, Keep in sync with DCommmon.hlsl
			float3 _FogCenter;
			float4 _FogFadeParams; // x: horizontal start, y: horizontal end, z: vertical start, w: vertical end
			TEXTURE2D(_FogRampMap); SAMPLER(sampler_FogRampMap);
			half _OutlineWidth;
			half4 _OutlineColor;
			half _Breathing;
			half3 _BreathingColor;
			half _BreathingSpeed;

			half3 MixFPCustomFog(half3 fragColor, float3 positionWS)
			{
			#if defined(_USE_CUSTOM_FOG)
				float hdist = length(positionWS.xz - _FogCenter.xz);
				float hfade = saturate((hdist - _FogFadeParams.x) / (_FogFadeParams.y - _FogFadeParams.x));
				float vdist = positionWS.y - _FogCenter.y;
				float vdistSign = sign(vdist);
				float vdistAbs = abs(vdist);
				float vfade = saturate((vdistAbs - _FogFadeParams.z) / (_FogFadeParams.w - _FogFadeParams.z));
				float2 fogUV = float2(hfade, 0.5 + vdistSign * 0.5 * vfade);
				half3 fogColor = SAMPLE_TEXTURE2D(_FogRampMap, sampler_FogRampMap, fogUV).rgb;
				float fogFactor = vdist > 0 ? hfade * (1 - vfade) : saturate(hfade - vfade);
				fragColor = lerp(fragColor, fogColor, fogFactor);
			#endif
				return fragColor;
			}

			// ref: https://github.com/ssell/UnitySobelOutline
			float SobelDepth(float ldc, float ldl, float ldr, float ldu, float ldd)
			{
				return abs(ldl - ldc)
				+ abs(ldr - ldc)
				+ abs(ldu - ldc)
				+ abs(ldd - ldc);
			}

			float SobelSampleDepth(Texture2D t, SamplerState s, float2 uv, float3 offset)
			{
				float pixelCenter = LinearEyeDepth(t.Sample(s, uv).r, _ZBufferParams);
				float pixelLeft   = LinearEyeDepth(t.Sample(s, uv - offset.xz).r, _ZBufferParams);
				float pixelRight  = LinearEyeDepth(t.Sample(s, uv + offset.xz).r, _ZBufferParams);
				float pixelUp     = LinearEyeDepth(t.Sample(s, uv + offset.zy).r, _ZBufferParams);
				float pixelDown   = LinearEyeDepth(t.Sample(s, uv - offset.zy).r, _ZBufferParams);

				return SobelDepth(pixelCenter, pixelLeft, pixelRight, pixelUp, pixelDown);
			}

			struct VertexInput
			{
				float4 vertex : POSITION;
				float3 normal : NORMAL;
				float2 uv0 : TEXCOORD0;
				float4 uv1 : TEXCOORD1;
				float4 uv2 : TEXCOORD2;
				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct VertexOutput
			{
				float4 positionCS : SV_POSITION;
				float2 uv : TEXCOORD0;
				float3 normalWS : TEXCOORD1;
				float3 viewDir : TEXCOORD2;
				float3 vertexSH : TEXCOORD3;
				float3 positionWS : TEXCOORD4;
				UNITY_VERTEX_INPUT_INSTANCE_ID
				UNITY_VERTEX_OUTPUT_STEREO
			};


			VertexOutput vert(VertexInput v)
			{
				VertexOutput o = (VertexOutput)0;

				UNITY_SETUP_INSTANCE_ID(v);
				UNITY_TRANSFER_INSTANCE_ID(v, o);
				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
#ifdef _BONECOUNT_TWO
				float4 pos = skin2(v.vertex, v.uv1, v.uv2);
				float3 normal = skin2(float4(v.normal,0), v.uv1, v.uv2);
#elif defined(_BONECOUNT_ONE)
				float4 pos = skin1(v.vertex, v.uv1, v.uv2);
				float3 normal = skin1(float4(v.normal,0), v.uv1, v.uv2);
#else
				float4 pos = skin2(v.vertex, v.uv1, v.uv2);
				float3 normal = skin2(float4(v.normal,0), v.uv1, v.uv2);
#endif
				//tangent = skin4(tangent, v.uv1, v.uv2);

				VertexPositionInputs vertexInput = GetVertexPositionInputs(pos);

				o.positionCS = vertexInput.positionCS;
				o.normalWS = TransformObjectToWorldNormal(normal);
				o.normalWS = NormalizeNormalPerVertex(o.normalWS);
				o.uv = TRANSFORM_TEX(v.uv0, _BaseMap);
				o.positionWS = vertexInput.positionWS;

				half3 viewDirWS = GetCameraPositionWS() - vertexInput.positionWS;
				o.viewDir = viewDirWS;
				o.vertexSH = SampleSHVertex(o.normalWS.xyz);
				return o;
			}

			half4 SampleSpecularSmoothness(half2 uv, half4 specColor)
			{
			    half4 specularSmoothness = half4(0.0h, 0.0h, 0.0h, 1.0h);

			    specularSmoothness = specColor;
			    specularSmoothness.a = exp2(10 * specularSmoothness.a + 1);

			    return specularSmoothness;
			}

			void Dither_4x4(uint2 ditherSeed, float ditherFactor)
			{
			    float DITHER_THRESHOLDS[16] =
			    {
			        1.0 / 17.0,  9.0 / 17.0,  3.0 / 17.0, 11.0 / 17.0,
			        13.0 / 17.0,  5.0 / 17.0, 15.0 / 17.0,  7.0 / 17.0,
			        4.0 / 17.0, 12.0 / 17.0,  2.0 / 17.0, 10.0 / 17.0,
			        16.0 / 17.0,  8.0 / 17.0, 14.0 / 17.0,  6.0 / 17.0
			    };
			    uint index = (ditherSeed.x % 4) * 4 + ditherSeed.y % 4;
			    float f = ditherFactor - DITHER_THRESHOLDS[index];
			    clip(f);
			}

			half4 frag(VertexOutput IN) : SV_TARGET
			{
				UNITY_SETUP_INSTANCE_ID(IN);
				UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(IN);
				#ifdef _SEAMLESS_DITHERING
					Dither_4x4(IN.positionCS.xy, _DitherFactor);
				#endif
				half3 viewDirWS = IN.viewDir;
				viewDirWS = SafeNormalize(viewDirWS);
				IN.normalWS = NormalizeNormalPerPixel(IN.normalWS);
				half3 bakedGI =  SampleSHPixel(IN.vertexSH, IN.normalWS);

				Light mainLight = GetMainLight();

				// diffuse
				half4 diffuseAlpha = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv);
				half3 diffuse = diffuseAlpha.rgb * _BaseColor.rgb;

				half3 lightColor = mainLight.color;
				half3 diffuseColor = bakedGI + LightingLambert(lightColor, mainLight.direction, IN.normalWS);

				// specular
				//half4 specular = SampleSpecularSmoothness( IN.uv, _SpecColor);
				//half smoothness = specular.a;
				//half3 specularColor = LightingSpecular(lightColor, mainLight.direction, IN.normalWS, viewDirWS, specular, smoothness);

				half3 finalColor = diffuseColor * diffuse;// + specularColor;

				half4 color = UNITY_ACCESS_INSTANCED_PROP(GPUSkinningProperties0, _TintColor);
				half rimWeight = 1-saturate(dot(viewDirWS, IN.normalWS));
				half4 finalRGBA = half4(finalColor + color.rgb * rimWeight, diffuseAlpha.a * _BaseColor.a);

				#ifdef _SOBEL_OUTLINE
					half2 screenUV = IN.positionCS.xy / _ScaledScreenParams.xy;
					float3 offset = float3((1.0 / _ScaledScreenParams.x), (1.0 / _ScaledScreenParams.y), 0.0) * _OutlineWidth;
					float sobelDepth = SobelSampleDepth(_CameraDepthTexture, sampler_CameraDepthTexture, screenUV, offset);
					half outlineFactor = saturate(sobelDepth);
					half3 outlineColorRGB = outlineFactor * _OutlineColor.rgb;
					finalRGBA.rgb += outlineColorRGB;
					finalRGBA.a = lerp(color.a, _OutlineColor.a, outlineFactor);
				#endif

				if (_Breathing > 0)
				{
					finalRGBA.rgb += diffuse * _BreathingColor * sin(_BreathingSpeed * _Time.y);
					finalRGBA.rgb = max(half3(0, 0, 0), finalRGBA.rgb);
				}

				#ifdef _USE_CUSTOM_FOG
					finalRGBA.rgb = MixFPCustomFog(finalRGBA.rgb, IN.positionWS);
				#endif

				return finalRGBA;
			}
			ENDHLSL
		}

		Pass
		{
			Name "DepthOnly"
			Tags{"LightMode" = "DepthOnly"}

			ZWrite On
			ColorMask 0

			HLSLPROGRAM
			// Required to compile gles 2.0 with standard srp library
			#pragma prefer_hlslcc gles
			#pragma exclude_renderers d3d11_9x
			#pragma target 2.0

			#pragma vertex DepthOnlyVertex
			#pragma fragment DepthOnlyFragment

			//--------------------------------------
			// GPU Instancing
			#pragma multi_compile_instancing

			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/UnityInstancing.hlsl"
			#include "GPUSkinningInclude.hlsl"

			struct Attributes
			{
				float4 vertex : POSITION;
				float3 normal : NORMAL;
				float2 uv0 : TEXCOORD0;
				float4 uv1 : TEXCOORD1;
				float4 uv2 : TEXCOORD2;
				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct Varyings
			{
				float4 positionCS : SV_POSITION;
				float2 uv : TEXCOORD0;
				UNITY_VERTEX_INPUT_INSTANCE_ID
				UNITY_VERTEX_OUTPUT_STEREO
			};

			Varyings DepthOnlyVertex(Attributes input)
			{
				Varyings o = (Varyings)0;

				UNITY_SETUP_INSTANCE_ID(input);
				UNITY_TRANSFER_INSTANCE_ID(input, o);
				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

				float4 pos = skin2(input.vertex, input.uv1, input.uv2);

				VertexPositionInputs vertexInput = GetVertexPositionInputs(pos);
				o.positionCS = vertexInput.positionCS;
				return o;
			}

			half4 DepthOnlyFragment(Varyings input) : SV_Target
			{
				return 0;
			}

			ENDHLSL
		}
	}
}
