// 수영장 물 쉐이더 (URP 전용, 손으로 작성)
// 기능: 두 겹의 물결(노멀맵)이 서로 다른 방향으로 흐름 / 바닥이 일렁이며 비침(굴절) /
//       깊이에 따라 색이 변함 / 벽과 물체가 닿는 곳에 거품 / 천장 조명이 물에 반짝임(스페큘러)
Shader "Custom/PoolWater"
{
    Properties
    {
        [Header(Color)]
        _ShallowColor ("Shallow Color", Color) = (0.30, 0.80, 0.76, 1)
        _DeepColor ("Deep Color", Color) = (0.02, 0.22, 0.28, 1)
        _FresnelColor ("Fresnel Color", Color) = (0.25, 0.55, 0.60, 1)
        _Glow ("Self Glow", Range(0, 1)) = 0.22
        _Opacity ("Opacity", Range(0, 1)) = 0.96

        [Header(Waves)]
        [NoScaleOffset] _NormalMap ("Normal Map", 2D) = "bump" {}
        [NoScaleOffset] _CausticTex ("Caustic / Foam Texture", 2D) = "white" {}
        _NormalTiling ("Normal Tiling (per meter)", Float) = 0.5
        _NormalStrength ("Normal Strength", Range(0, 3)) = 1.4
        _FlowSpeed ("Flow Speed", Float) = 0.12

        [Header(Refraction and Depth)]
        _Distort ("Refraction", Range(0, 0.3)) = 0.06
        _DepthMax ("Depth Fade Distance", Float) = 1.0
        _FoamWidth ("Foam Width", Float) = 0.2

        [Header(Highlights)]
        _Shininess ("Shininess", Float) = 200
        _SpecIntensity ("Specular Intensity", Float) = 2
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"

            TEXTURE2D(_NormalMap);  SAMPLER(sampler_NormalMap);
            TEXTURE2D(_CausticTex); SAMPLER(sampler_CausticTex);

            CBUFFER_START(UnityPerMaterial)
                half4 _ShallowColor;
                half4 _DeepColor;
                half4 _FresnelColor;
                half _Glow;
                half _Opacity;
                float _NormalTiling;
                float _NormalStrength;
                float _FlowSpeed;
                float _Distort;
                float _DepthMax;
                float _FoamWidth;
                float _Shininess;
                float _SpecIntensity;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half fogFactor : TEXCOORD1;
            };

            Varyings vert(Attributes input)
            {
                Varyings o;
                o.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.fogFactor = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float3 posWS = i.positionWS;
                float2 screenUV = GetNormalizedScreenSpaceUV(i.positionCS);

                // 1) 물결: 노멀맵 두 겹을 서로 다른 방향/크기로 흘려서 섞음 (월드 좌표 기준이라 늘어나지 않음)
                float t = _Time.y * _FlowSpeed;
                float2 uvA = posWS.xz * _NormalTiling + t * float2(1.0, 0.55);
                float2 uvB = posWS.xz * _NormalTiling * 1.9 - t * float2(0.8, 1.3) + 0.37;
                float3 nA = UnpackNormalScale(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, uvA), _NormalStrength);
                float3 nB = UnpackNormalScale(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, uvB), _NormalStrength * 0.7);
                float3 nT = normalize(float3(nA.xy + nB.xy, nA.z * nB.z));
                float3 N = normalize(float3(nT.x, nT.z, nT.y));   // 물 표면은 위(+Y)를 보고 있음
                float3 V = normalize(GetCameraPositionWS() - posWS);

                // 2) 깊이: 물 표면과 그 뒤의 물체(바닥/벽) 사이 거리
                float waterEye = -TransformWorldToView(posWS).z;
                float sceneEye = LinearEyeDepth(SampleSceneDepth(screenUV), _ZBufferParams);
                float depthDiff = max(sceneEye - waterEye, 0.0);
                float depthT = saturate(depthDiff / max(_DepthMax, 0.001));

                // 3) 굴절: 물결 때문에 물속 바닥이 일렁이게 보임 (물 앞의 물체가 딸려 오지 않게 검사)
                float2 refrUV = screenUV + nT.xy * _Distort;
                float refrEye = LinearEyeDepth(SampleSceneDepth(refrUV), _ZBufferParams);
                if (refrEye < waterEye) refrUV = screenUV;
                float3 bg = SampleSceneColor(refrUV);

                // 4) 조명: 천장 조명이 물 표면을 밝히고 반짝임을 만든다
                float3 diffuseL = 0;
                float3 specL = 0;
                Light mainL = GetMainLight();
                diffuseL += mainL.color * saturate(dot(N, mainL.direction));
                specL += mainL.color * pow(saturate(dot(N, normalize(mainL.direction + V))), _Shininess);
                #if defined(_ADDITIONAL_LIGHTS)
                    InputData inputData = (InputData)0;
                    inputData.positionWS = posWS;
                    inputData.normalizedScreenSpaceUV = screenUV;
                    uint pixelLightCount = GetAdditionalLightsCount();
                    LIGHT_LOOP_BEGIN(pixelLightCount)
                        Light l = GetAdditionalLight(lightIndex, posWS);
                        float att = l.distanceAttenuation;
                        diffuseL += l.color * att * saturate(dot(N, l.direction));
                        specL += l.color * att * pow(saturate(dot(N, normalize(l.direction + V))), _Shininess);
                    LIGHT_LOOP_END
                #endif

                // 5) 색 합치기: 얕은 곳은 밝은 청록, 깊은 곳은 진한 청록
                float3 tint = lerp(_ShallowColor.rgb, _DeepColor.rgb, depthT);
                float3 col = bg * tint * lerp(1.8, 0.8, depthT);
                col += tint * _Glow;
                col += tint * min(diffuseL, 3.0) * 0.18;

                // 6) 프레넬: 비스듬히 볼수록 표면 색이 강해짐
                float fres = pow(1.0 - saturate(dot(N, V)), 3.0);
                col = lerp(col, _FresnelColor.rgb + diffuseL * 0.05, fres * 0.7);

                // 7) 반짝임
                col += specL * _SpecIntensity * 0.25;

                // 8) 거품: 벽이나 물체와 닿는 가장자리에 하얀 띠
                float foamMask = 1.0 - saturate(depthDiff / max(_FoamWidth, 0.001));
                float noise = SAMPLE_TEXTURE2D(_CausticTex, sampler_CausticTex, posWS.xz * 0.9 + t * float2(-0.5, 0.3)).r;
                float foam = foamMask * smoothstep(0.25, 0.7, noise + foamMask * 0.5);
                col += foam * float3(0.75, 0.95, 0.95);

                col = MixFog(col, i.fogFactor);
                return half4(col, _Opacity);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
