// 수영장 바닥 물그림자(커스틱) — 바닥 위에 얹는 덧그리기 쿼드 1장용 가산 셰이더.
// Geometry+10 큐라서 물 셰이더의 굴절(오파크 텍스처 샘플)에 함께 잡혀 물을 통해 비쳐 보인다.
// 두 겹을 서로 다른 속도로 흘려 min으로 합쳐 그물 무늬를 만든다. 텍스처 샘플은 2번뿐.
Shader "Custom/PoolCaustic"
{
    Properties
    {
        [MainTexture] _BaseMap ("Caustic Texture", 2D) = "white" {}
        _Tint ("Tint (additive)", Color) = (0.10, 0.18, 0.20, 1)
        _Scroll ("Second Layer Scroll", Float) = 0.02
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry+10" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "Caustic"
            Tags { "LightMode" = "UniversalForward" }

            Blend One One
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _Tint;
                float _Scroll;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };

            Varyings vert(Attributes i)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(i.positionOS.xyz);
                o.uv = TRANSFORM_TEX(i.uv, _BaseMap);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float a = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv).r;
                float b = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv * 1.37 + 0.3 - _Time.y * _Scroll).r;
                float c = min(a, b);
                return half4(c * c * 2.0 * _Tint.rgb, 1);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
