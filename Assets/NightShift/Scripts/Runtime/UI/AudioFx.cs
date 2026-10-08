using System.Collections.Generic;
using UnityEngine;

namespace NightShift.UI
{
    /// <summary>操作・通知・機器の音。すべてオリジナルの合成音（外部素材なし）。</summary>
    public static class AudioFx
    {
        static AudioSource _src;
        static readonly Dictionary<string, AudioClip> _clips = new Dictionary<string, AudioClip>();
        static float _volume = 0.6f;
        public static float Volume
        {
            get => _volume;
            set { _volume = Mathf.Clamp01(value); if (_src != null) _src.volume = _volume; }
        }
        public static bool Focused = true;
        static readonly Dictionary<string, float> LastPlayed = new Dictionary<string, float>();
        public static bool ClickEnabled = true;
        public static bool NotifyEnabled = true;

        public static void Init(GameObject host)
        {
            if (_src != null) return;
            _src = host.AddComponent<AudioSource>();
            _src.playOnAwake = false;
            _src.spatialBlend = 0;
            _src.volume = Volume;
            _src.priority = 40;
            Focused = true;
            Soundscape.Attach(host);
        }

        public static void Shutdown()
        {
            if (_src != null) _src.Stop();
            _src = null;
            foreach (var clip in _clips.Values)
                if (clip != null) { if (Application.isPlaying) Object.Destroy(clip); else Object.DestroyImmediate(clip); }
            _clips.Clear(); LastPlayed.Clear();
        }

        // キーボード、リレー、ディスクの短い過渡音。耳につく矩形波の連打を避ける。
        static AudioClip Mechanical(string key, float length, float pitch, float level)
        {
            if (_clips.TryGetValue(key, out var clip) && clip != null) return clip;
            const int rate = 22050;
            var samples = new float[(int)(rate * length)];
            var random = new System.Random(1998);
            float filtered = 0;
            for (int i = 0; i < samples.Length; i++)
            {
                float t = i / (float)rate;
                filtered = filtered * 0.65f + ((float)random.NextDouble() * 2 - 1) * 0.35f;
                float env = Mathf.Min(1, t / 0.002f) * Mathf.Exp(-t * 7 / length) * Mathf.Min(1, (length - t) / 0.01f);
                samples[i] = (filtered * 0.7f + Mathf.Sin(2 * Mathf.PI * pitch * t) * 0.3f) * env * level;
            }
            clip = AudioClip.Create(key, samples.Length, 1, rate, false);
            clip.SetData(samples, 0); _clips[key] = clip;
            return clip;
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
            if (_src == null || clip == null || !Focused || Volume <= 0) return;
            float now = Time.unscaledTime;
            float interval = clip.name == "alert" ? 0.8f : clip.name == "notify" || clip.name == "ticket" ? 0.18f : 0.035f;
            if (LastPlayed.TryGetValue(clip.name, out var last) && now - last < interval) return;
            LastPlayed[clip.name] = now;
            _src.PlayOneShot(clip, Mathf.Clamp01(vol));
            if (clip.name == "alert" || clip.name == "notify" || clip.name == "ticket") Soundscape.Duck();
        }

        public static void Click() { if (ClickEnabled) Play(Mechanical("click", 0.045f, 900, 0.3f), 0.5f); }
        public static void Notify() { if (NotifyEnabled) Play(Tone("notify", new[] { 880f, 1175f }, 0.07f, 0.015f, 0.22f, false)); }
        public static void Ticket() { if (NotifyEnabled) Play(Tone("ticket", new[] { 660f, 880f, 660f }, 0.06f, 0.01f, 0.2f, false)); }
        public static void Alert() { if (NotifyEnabled) Play(Tone("alert", new[] { 1046f, 784f, 1046f, 784f }, 0.11f, 0.02f, 0.18f)); }
        public static void Error() => Play(Tone("error", new[] { 220f, 196f }, 0.12f, 0.0f, 0.22f));
        public static void Chime() { if (NotifyEnabled) Play(Tone("chime", new[] { 523f, 659f, 784f, 1046f }, 0.18f, 0.02f, 0.2f, false)); }
        public static void Stamp() => Play(Mechanical("stamp", 0.18f, 95, 0.65f));
        public static void Startup() => Play(Tone("startup", new[] { 392f, 523f, 659f, 784f, 0f, 1046f }, 0.12f, 0.0f, 0.18f, false));
        public static void Connect() => Play(Tone("connect", new[] { 1200f, 0f, 1200f, 1500f }, 0.05f, 0.01f, 0.12f));

        // ---- リザルト画面用 ----
        public static void Type() { if (ClickEnabled) Play(Mechanical("type", 0.025f, 1450, 0.2f), 0.4f); }
        public static void Tick() => Play(Tone("tick", new[] { 1500f }, 0.015f, 0f, 0.1f), 0.5f);
        public static void Coin() => Play(Tone("coin", new[] { 1319f, 1976f }, 0.05f, 0.0f, 0.12f), 0.6f);
        public static void BigStamp() => Play(Mechanical("bigstamp", 0.28f, 70, 0.8f));
        public static void Fanfare() => Play(Tone("fanfare", new[] { 523f, 659f, 784f, 0f, 784f, 1046f }, 0.12f, 0.01f, 0.2f, false));
        // ---- ホラー演出の音（「急な大きな音」がオフなら音量を下げる） ----
        static float HorrorVol => Core.GameSettings.Current.loudSounds ? 1f : 0.35f;

        /// <summary>低いハム音（ブルースクリーン）</summary>
        public static void Hum() => Play(Tone("hum", new[] { 55f, 55f, 55f, 55f }, 0.6f, 0f, 0.35f, false), HorrorVol);
        /// <summary>CRT の高い「ジーッ」という音（ブラックアウト）</summary>
        public static void CrtWhine() => Play(Tone("crt", new[] { 2300f, 2310f, 2300f }, 0.5f, 0f, 0.025f, false), HorrorVol);
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
