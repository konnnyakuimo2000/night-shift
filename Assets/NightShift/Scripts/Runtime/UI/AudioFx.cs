using System.Collections.Generic;
using UnityEngine;

namespace NightShift.UI
{
    /// <summary>仮SE。すべて手続き生成（外部素材なし）。</summary>
    public static class AudioFx
    {
        static AudioSource _src;
        static readonly Dictionary<string, AudioClip> _clips = new Dictionary<string, AudioClip>();
        public static float Volume = 0.6f;
        public static bool ClickEnabled = true;
        public static bool NotifyEnabled = true;

        public static void Init(GameObject host)
        {
            if (_src != null) return;
            _src = host.AddComponent<AudioSource>();
            _src.playOnAwake = false;
            _src.spatialBlend = 0;
        }

        static AudioClip Tone(string key, float[] freqs, float noteLen, float gap = 0.02f, float vol = 0.3f, bool square = true)
        {
            if (_clips.TryGetValue(key, out var c)) return c;
            const int rate = 22050;
            int noteSamples = (int)(noteLen * rate);
            int gapSamples = (int)(gap * rate);
            int total = freqs.Length * (noteSamples + gapSamples);
            var data = new float[total];
            int idx = 0;
            foreach (var f in freqs)
            {
                for (int i = 0; i < noteSamples; i++)
                {
                    float t = i / (float)rate;
                    float env = Mathf.Min(1f, i / 200f) * Mathf.Clamp01((noteSamples - i) / (rate * 0.03f));
                    float s = f <= 0 ? 0 : (square ? Mathf.Sign(Mathf.Sin(2 * Mathf.PI * f * t)) * 0.5f : Mathf.Sin(2 * Mathf.PI * f * t));
                    data[idx++] = s * env * vol;
                }
                idx += gapSamples;
            }
            c = AudioClip.Create(key, total, 1, rate, false);
            c.SetData(data, 0);
            _clips[key] = c;
            return c;
        }

        static void Play(AudioClip clip, float vol = 1f)
        {
            if (_src == null || clip == null) return;
            _src.PlayOneShot(clip, Volume * vol);
        }

        public static void Click() { if (ClickEnabled) Play(Tone("click", new[] { 1800f }, 0.012f, 0, 0.15f), 0.5f); }
        public static void Notify() { if (NotifyEnabled) Play(Tone("notify", new[] { 880f, 1175f }, 0.07f, 0.015f, 0.22f, false)); }
        public static void Ticket() { if (NotifyEnabled) Play(Tone("ticket", new[] { 660f, 880f, 660f }, 0.06f, 0.01f, 0.2f, false)); }
        public static void Alert() { if (NotifyEnabled) Play(Tone("alert", new[] { 1046f, 784f, 1046f, 784f }, 0.11f, 0.02f, 0.18f)); }
        public static void Error() => Play(Tone("error", new[] { 220f, 196f }, 0.12f, 0.0f, 0.22f));
        public static void Chime() { if (NotifyEnabled) Play(Tone("chime", new[] { 523f, 659f, 784f, 1046f }, 0.18f, 0.02f, 0.2f, false)); }
        public static void Stamp() => Play(Tone("stamp", new[] { 120f, 90f }, 0.05f, 0.0f, 0.5f));
        public static void Startup() => Play(Tone("startup", new[] { 392f, 523f, 659f, 784f, 0f, 1046f }, 0.12f, 0.0f, 0.18f, false));
        public static void Connect() => Play(Tone("connect", new[] { 1200f, 0f, 1200f, 1500f }, 0.05f, 0.01f, 0.12f));

        // ---- リザルト画面用 ----
        public static void Type() => Play(Tone("type", new[] { 2400f }, 0.008f, 0f, 0.08f), 0.4f);
        public static void Tick() => Play(Tone("tick", new[] { 1500f }, 0.015f, 0f, 0.1f), 0.5f);
        public static void Coin() => Play(Tone("coin", new[] { 1319f, 1976f }, 0.05f, 0.0f, 0.12f), 0.6f);
        public static void BigStamp() => Play(Tone("bigstamp", new[] { 90f, 70f, 55f }, 0.07f, 0.0f, 0.6f));
        public static void Fanfare() => Play(Tone("fanfare", new[] { 523f, 659f, 784f, 0f, 784f, 1046f }, 0.12f, 0.01f, 0.2f, false));
        // ---- ホラー演出の音（「急な大きな音」がオフなら音量を下げる） ----
        static float HorrorVol => Core.GameSettings.Current.loudSounds ? 1f : 0.35f;

        /// <summary>低いハム音（ブルースクリーン）</summary>
        public static void Hum() => Play(Tone("hum", new[] { 55f, 55f, 55f, 55f }, 0.6f, 0f, 0.35f, false), HorrorVol);
        /// <summary>CRT の高い「ジーッ」という音（ブラックアウト）</summary>
        public static void CrtWhine() => Play(Tone("crt", new[] { 15600f, 15600f, 15600f }, 0.5f, 0f, 0.06f, false), HorrorVol);
        /// <summary>止まる音（ディスク・ファン）</summary>
        public static void PowerDown() => Play(Tone("powerdown", new[] { 440f, 330f, 220f, 140f, 90f, 60f }, 0.09f, 0f, 0.3f, false), HorrorVol);
        /// <summary>遠くの泣き声のようなノイズ（NIGHTCRY）</summary>
        public static void Cry() { if (!Core.GameSettings.Current.horrorMild) Play(Tone("cry", new[] { 520f, 610f, 560f, 480f, 0f, 520f, 640f, 500f }, 0.14f, 0.01f, 0.05f, false), HorrorVol * 0.6f); }
        /// <summary>オルゴールの子守唄（BRIAR ROSE）</summary>
        public static void MusicBox() => Play(Tone("musicbox", new[] { 1319f, 1175f, 1047f, 1175f, 1319f, 1319f, 1319f, 0f, 1175f, 1175f, 1175f }, 0.22f, 0.04f, 0.12f, false), HorrorVol * 0.7f);
        /// <summary>歪んだ起動音（再起動）</summary>
        public static void WarpedStartup() => Play(Tone("warped", new[] { 196f, 262f, 330f, 392f, 0f, 523f }, 0.22f, 0.0f, 0.18f, false), HorrorVol);

        public static void Reveal() => Play(Tone("reveal", new[] { 392f, 523f }, 0.08f, 0.0f, 0.16f, false));
    }
}
