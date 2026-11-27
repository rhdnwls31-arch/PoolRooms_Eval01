using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 소리 파일 가공 계산(2026-11-27 사운드 단계). VARCO Sound에서 받은 소리를 게임에 맞게 다듬는 데 쓴다.
/// 에디터 패스(GreyboxGraphicsPass.Audio1127)가 원본을 읽어 이 계산으로 다듬은 뒤 Assets/Audio/Game에 새 파일로 쓴다.
/// 모든 배열은 채널이 섞여 있는(interleaved) 샘플이다. 길이 = 프레임 수 × 채널 수.
/// </summary>
public static class AudioCraft
{
    /// <summary>
    /// 부드럽게 눌러 키우기(작은 소리를 크게, 큰 소리는 덜 키움). drive가 클수록 평균 크기가 커진다. 최대 크기는 그대로(±1 안).
    /// 2026-11-27: 노크처럼 꼭 들려야 하는 소리에 쓴다.
    /// </summary>
    public static void Drive(float[] s, float drive)
    {
        if (drive <= 0f) return;
        float norm = (float)System.Math.Tanh(drive);
        for (int i = 0; i < s.Length; i++) s[i] = (float)System.Math.Tanh(s[i] * drive) / norm;
    }

    /// <summary>가장 큰 샘플 크기(0~1).</summary>
    public static float Peak(float[] s)
    {
        float p = 0f;
        for (int i = 0; i < s.Length; i++) { float a = Mathf.Abs(s[i]); if (a > p) p = a; }
        return p;
    }

    /// <summary>가장 큰 샘플이 target이 되도록 키우거나 줄인다. 무음이면 그대로 둔다.</summary>
    public static void Normalize(float[] s, float target)
    {
        float p = Peak(s);
        if (p < 1e-5f) return;
        float k = target / p;
        for (int i = 0; i < s.Length; i++) s[i] *= k;
    }

    /// <summary>
    /// 앞의 무음을 잘라 낸다(threshold보다 작은 샘플이 이어지는 구간). 버튼을 누르자마자 소리가 나게.
    /// 소리가 시작하기 직전 keepSeconds만큼은 남긴다.
    /// </summary>
    public static float[] TrimLeadingSilence(float[] s, int channels, int freq, float threshold, float keepSeconds)
    {
        int frames = s.Length / channels;
        int first = frames;
        for (int f = 0; f < frames && first == frames; f++)
            for (int c = 0; c < channels; c++)
                if (Mathf.Abs(s[f * channels + c]) >= threshold) { first = f; break; }
        if (first >= frames) return s;
        int start = Mathf.Max(0, first - Mathf.RoundToInt(keepSeconds * freq));
        if (start == 0) return s;
        var o = new float[(frames - start) * channels];
        System.Array.Copy(s, start * channels, o, 0, o.Length);
        return o;
    }

    /// <summary>maxSeconds보다 길면 자르고, 끝 fadeSeconds 동안 줄여서 뚝 끊기지 않게 한다. 짧으면 끝만 살짝 줄인다.</summary>
    public static float[] TrimWithFade(float[] s, int channels, int freq, float maxSeconds, float fadeSeconds)
    {
        int frames = s.Length / channels;
        int keep = maxSeconds > 0f ? Mathf.Min(frames, Mathf.RoundToInt(maxSeconds * freq)) : frames;
        var o = new float[keep * channels];
        System.Array.Copy(s, o, o.Length);
        int fade = Mathf.Min(keep, Mathf.RoundToInt(fadeSeconds * freq));
        for (int f = 0; f < fade; f++)
        {
            float k = (float)f / Mathf.Max(1, fade); // 끝에서 0
            int idx = keep - 1 - f;
            for (int c = 0; c < channels; c++) o[idx * channels + c] *= k;
        }
        return o;
    }

    /// <summary>
    /// 반복 재생해도 이음매가 들리지 않게 만든다: 끝 xfadeSeconds를 처음과 겹쳐 섞고, 그만큼 짧아진다.
    /// 결과의 마지막 샘플 다음에 첫 샘플이 오면 원래 소리처럼 이어진다.
    /// </summary>
    public static float[] MakeLoop(float[] s, int channels, int freq, float xfadeSeconds)
    {
        int frames = s.Length / channels;
        int x = Mathf.Min(frames / 3, Mathf.RoundToInt(xfadeSeconds * freq));
        if (x <= 0) return s;
        int outFrames = frames - x;
        var o = new float[outFrames * channels];
        System.Array.Copy(s, o, o.Length);
        for (int f = 0; f < x; f++)
        {
            float k = (float)f / x; // 처음 쪽 비중
            for (int c = 0; c < channels; c++)
            {
                float head = s[f * channels + c];
                float tail = s[(outFrames + f) * channels + c];
                o[f * channels + c] = head * k + tail * (1f - k);
            }
        }
        return o;
    }

    /// <summary>
    /// 걸음처럼 툭 튀는 소리가 시작하는 프레임들. windowSeconds 단위로 크기(RMS)를 재서,
    /// 가장 큰 값의 threshold배를 처음 넘는 곳을 고른다. 고른 곳 사이는 적어도 minGapSeconds 떨어진다.
    /// </summary>
    public static List<int> FindOnsets(float[] s, int channels, int freq, float threshold, float minGapSeconds, float windowSeconds = 0.01f)
    {
        int frames = s.Length / channels;
        int win = Mathf.Max(1, Mathf.RoundToInt(windowSeconds * freq));
        int n = frames / win;
        var env = new float[n];
        float max = 0f;
        for (int w = 0; w < n; w++)
        {
            double sum = 0;
            for (int f = w * win; f < (w + 1) * win; f++)
                for (int c = 0; c < channels; c++) { float v = s[f * channels + c]; sum += v * v; }
            env[w] = Mathf.Sqrt((float)(sum / (win * channels)));
            if (env[w] > max) max = env[w];
        }
        var result = new List<int>();
        if (max <= 0f) return result;
        float th = max * threshold;
        int gap = Mathf.Max(1, Mathf.RoundToInt(minGapSeconds / windowSeconds));
        int lastW = -gap;
        bool below = true;
        for (int w = 0; w < n; w++)
        {
            if (env[w] < th * 0.6f) below = true;
            if (env[w] >= th && below && w - lastW >= gap)
            {
                result.Add(w * win);
                lastW = w;
                below = false;
            }
        }
        return result;
    }

    /// <summary>startFrame부터 최대 maxSeconds를 떼어 낸다. 끝 fadeSeconds 동안 줄인다.</summary>
    public static float[] Slice(float[] s, int channels, int freq, int startFrame, float maxSeconds, float fadeSeconds)
    {
        int frames = s.Length / channels;
        startFrame = Mathf.Clamp(startFrame, 0, frames);
        int len = Mathf.Min(frames - startFrame, Mathf.RoundToInt(maxSeconds * freq));
        var part = new float[len * channels];
        System.Array.Copy(s, startFrame * channels, part, 0, part.Length);
        return TrimWithFade(part, channels, freq, 0f, fadeSeconds);
    }

    /// <summary>16비트 PCM WAV 파일 내용을 만든다.</summary>
    public static byte[] ToWav(float[] s, int channels, int freq)
    {
        int dataBytes = s.Length * 2;
        var b = new byte[44 + dataBytes];
        void Str(int at, string v) { for (int i = 0; i < 4; i++) b[at + i] = (byte)v[i]; }
        void I32(int at, int v) { b[at] = (byte)v; b[at + 1] = (byte)(v >> 8); b[at + 2] = (byte)(v >> 16); b[at + 3] = (byte)(v >> 24); }
        void I16(int at, int v) { b[at] = (byte)v; b[at + 1] = (byte)(v >> 8); }
        Str(0, "RIFF"); I32(4, 36 + dataBytes); Str(8, "WAVE");
        Str(12, "fmt "); I32(16, 16); I16(20, 1); I16(22, channels); I32(24, freq);
        I32(28, freq * channels * 2); I16(32, channels * 2); I16(34, 16);
        Str(36, "data"); I32(40, dataBytes);
        for (int i = 0; i < s.Length; i++)
        {
            int v = Mathf.RoundToInt(Mathf.Clamp(s[i], -1f, 1f) * 32767f);
            I16(44 + i * 2, v);
        }
        return b;
    }
}
