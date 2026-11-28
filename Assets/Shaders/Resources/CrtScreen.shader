// 화면 전체를 오래된 브라운관 TV 화면처럼 보이게 하는 후처리(2026-11-20).
// 볼록하게 휜 유리, 둥근 모서리와 검은 베젤, 가로 주사선, 천천히 내려가는 밝은 띠, 가끔 옆으로 밀리는 줄.
// 값은 CrtScreen 볼륨 컴포넌트(Assets/Scripts/CrtScreen.cs)에서 넘어온다. Resources에 둬야 빌드에서 Shader.Find로 찾힌다.
Shader "Hidden/Shader/CrtScreen"
{
    Properties
    {
        _MainTex("Main Texture", 2DArray) = "grey" {}
    }

    HLSLINCLUDE

    #pragma target 4.5
    #pragma only_renderers d3d11 playstation xboxone xboxseries vulkan metal switch switch2

    #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
    #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
    #include "Packages/com.unity.render-pipelines.high-definition/Runtime/ShaderLibrary/ShaderVariables.hlsl"
    #include "Packages/com.unity.render-pipelines.high-definition/Runtime/PostProcessing/Shaders/RTUpscale.hlsl"

    struct Attributes
    {
        uint vertexID : SV_VertexID;
        UNITY_VERTEX_INPUT_INSTANCE_ID
    };

    struct Varyings
    {
        float4 positionCS : SV_POSITION;
        float2 texcoord   : TEXCOORD0;
        UNITY_VERTEX_OUTPUT_STEREO
    };

    Varyings Vert(Attributes input)
    {
        Varyings output;
        UNITY_SETUP_INSTANCE_ID(input);
        UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
        output.positionCS = GetFullScreenTriangleVertexPosition(input.vertexID);
        output.texcoord = GetFullScreenTriangleTexCoord(input.vertexID);
        return output;
    }

    float _Intensity;      // 전체 섞는 정도(0이면 원본)
    float _Curvature;      // 유리 휨
    float _CornerRadius;   // 화면 모서리 둥글기(짧은 변 기준 비율)
    float _Scanline;       // 주사선 진하기
    float _ScanlineCount;  // 화면 높이당 주사선 수
    float _Mask;           // RGB 세로 줄무늬(형광체) 진하기
    float _RollBand;       // 내려가는 밝은 띠 세기
    float _Jitter;         // 가끔 옆으로 밀리는 줄 세기
    float _EdgeDarken;     // 유리 가장자리 어두워짐
    float _Bezel;          // 베젤 두께(위아래는 화면 높이, 양옆은 화면 너비 기준 비율). 화면 비율은 그대로 두고 보이는 화면을 좁힌다(2026-11-28)
    TEXTURE2D_X(_MainTex);

    float Hash(float n) { return frac(sin(n) * 43758.5453); }

    // 둥근 사각형 안쪽까지의 거리(안이 음수).
    float RoundRectSdf(float2 p, float2 halfSize, float r)
    {
        float2 q = abs(p) - halfSize + r;
        return length(max(q, 0.0)) + min(max(q.x, q.y), 0.0) - r;
    }

    float4 CustomPostProcess(Varyings input) : SV_Target
    {
        UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

        float2 uv = input.texcoord.xy;
        float aspect = _ScreenSize.x / max(_ScreenSize.y, 1.0);

        // 1) 볼록한 유리: 가운데는 그대로, 가장자리로 갈수록 바깥으로 밀린다.
        float2 c = uv * 2.0 - 1.0;
        float2 cc = c * float2(aspect, 1.0);
        float r2 = dot(cc, cc) / (aspect * aspect + 1.0);
        float2 cw = c * (1.0 + _Curvature * r2);
        cw /= (1.0 + _Curvature * 0.5); // 가운데 크기가 너무 커지지 않게 되돌린다
        float2 suv = cw * 0.5 + 0.5;

        // 2) 가끔 한 줄 띠가 옆으로 밀린다(테이프 트래킹).
        float t = _Time.y;
        float bandY = frac(t * 0.07);
        float slot = floor(t * 1.3);
        float glitchOn = step(0.82, Hash(slot));
        float lineMask = smoothstep(0.03, 0.0, abs(suv.y - Hash(slot + 7.0)));
        suv.x += (Hash(slot + 3.0) - 0.5) * 0.02 * _Jitter * glitchOn * lineMask;

        float3 col = SAMPLE_TEXTURE2D_X(_MainTex, s_linear_clamp_sampler, ClampAndScaleUVForBilinearPostProcessTexture(saturate(suv))).xyz;
        float3 src = SAMPLE_TEXTURE2D_X(_MainTex, s_linear_clamp_sampler, ClampAndScaleUVForBilinearPostProcessTexture(uv)).xyz;

        // 3) 주사선: 줄 사이가 살짝 어둡다. 밝은 곳은 덜 어두워진다(번짐).
        float lum = Luminance(col);
        float s = sin(suv.y * _ScanlineCount * PI);
        float scan = lerp(1.0, 0.55 + 0.45 * s * s, _Scanline * (1.0 - 0.5 * saturate(lum)));
        col *= scan;

        // 4) 형광체 세로 줄무늬(R/G/B 번갈아).
        float px = floor(suv.x * _ScreenSize.x);
        float m = fmod(px, 3.0);
        float3 mask = m < 1.0 ? float3(1.0, 0.82, 0.82) : (m < 2.0 ? float3(0.82, 1.0, 0.82) : float3(0.82, 0.82, 1.0));
        col *= lerp(1.0.xxx, mask * 1.12, _Mask);

        // 5) 위에서 아래로 천천히 내려가는 밝은 띠.
        float band = smoothstep(0.08, 0.0, abs(suv.y - (1.0 - bandY)));
        col *= 1.0 + band * _RollBand;

        // 6) 유리 가장자리 어두움 + 둥근 모서리 바깥은 베젤(거의 검정, 살짝 반사).
        float2 p = (suv - 0.5) * float2(aspect, 1.0);
        // 베젤만큼 가로·세로를 같은 비율로 줄인다(화면 비율 유지). 네 변에서 똑같이 빼면 화면이 옆으로 길어 보였다(2026-11-28 QA).
        float2 halfSize = float2(aspect, 1.0) * 0.5 * saturate(1.0 - 2.0 * _Bezel);
        float d = RoundRectSdf(p, halfSize, _CornerRadius);
        float edge = saturate(1.0 + d * 6.0);           // 가장자리 근처에서 1에 가까워진다
        col *= 1.0 - _EdgeDarken * edge * edge;
        float aa = max(fwidth(d), 1e-4);
        float inside = saturate(-d / aa);
        // 선형 값이라 0.012만 돼도 화면에서는 회색(sRGB 0.11)으로 보였다. 거의 검정으로 둔다.
        float3 bezel = float3(0.0015, 0.0015, 0.002) + col * 0.02; // 베젤에 화면 빛이 아주 조금 비친다
        col = lerp(bezel, col, inside);

        return float4(lerp(src, col, _Intensity), 1);
    }

    ENDHLSL

    SubShader
    {
        Tags{ "RenderPipeline" = "HDRenderPipeline" }
        Pass
        {
            Name "CrtScreen"

            ZWrite Off
            ZTest Always
            Blend Off
            Cull Off

            HLSLPROGRAM
                #pragma fragment CustomPostProcess
                #pragma vertex Vert
            ENDHLSL
        }
    }
    Fallback Off
}
