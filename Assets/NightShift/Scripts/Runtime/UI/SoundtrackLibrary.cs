using System;
using System.Collections.Generic;
using UnityEngine;

namespace NightShift.UI
{
    public enum SoundtrackScene { Title, Office, Pressure, Anomaly, Dawn, Aftermath, Room }

    /// <summary>夜勤用のオリジナル音源。Resources の差し替え音源がなければ、固定の譜面から合成する。</summary>
    public static class SoundtrackLibrary
    {
        public const int SampleRate = 22050;
        public const int LoopSeconds = 32;
        static readonly Dictionary<SoundtrackScene, AudioClip> Clips = new Dictionary<SoundtrackScene, AudioClip>();
        static readonly List<AudioClip> Generated = new List<AudioClip>();

        public static AudioClip Get(SoundtrackScene scene)
        {
            if (Clips.TryGetValue(scene, out var clip) && clip != null) return clip;
            clip = Resources.Load<AudioClip>("Audio/NightShift/" + scene);
            if (clip == null)
            {
                float[] samples = Render(scene);
                clip = AudioClip.Create("NIGHT SHIFT / " + scene, samples.Length, 1, SampleRate, false);
                clip.SetData(samples, 0);
                Generated.Add(clip);
            }
            Clips[scene] = clip;
            return clip;
        }

        public static void Release()
        {
            foreach (var clip in Generated)
                if (clip != null) { if (Application.isPlaying) UnityEngine.Object.Destroy(clip); else UnityEngine.Object.DestroyImmediate(clip); }
            Generated.Clear(); Clips.Clear();
        }

        static double Wave(double hz, double t) => Math.Sin(2 * Math.PI * hz * t);
        static double Hz(int midi) => 440 * Math.Pow(2, (midi - 69) / 12.0);
        static double Ease(double x) => Math.Min(1, Math.Max(0, x));

        /// <summary>固定シード・固定譜面。実時間、ゲーム乱数、隠れた攻撃判定を使わない。</summary>
        public static float[] Render(SoundtrackScene scene, int rate = SampleRate)
        {
            if (rate < 8000 || rate > 48000) throw new ArgumentOutOfRangeException(nameof(rate));
            int seconds = scene == SoundtrackScene.Room ? 8 : LoopSeconds;
            var data = new float[seconds * rate];
            var rng = new System.Random(9801 + (int)scene);
            int[] roots = scene == SoundtrackScene.Dawn ? new[] { 50, 53, 57, 55 } : new[] { 38, 34, 41, 36 };
            int[] motif = scene == SoundtrackScene.Dawn ? new[] { 74, 77, 81, 79, 77, 74, 72, 69 } : new[] { 69, 65, 64, 62, 69, 72, 65, 64 };
            double noise = 0;
            for (int i = 0; i < data.Length; i++)
            {
                double t = i / (double)rate;
                noise = noise * 0.97 + (rng.NextDouble() * 2 - 1) * 0.03;
                double value;
                if (scene == SoundtrackScene.Room)
                {
                    // 空調・電源・遠いファン。会話や判断用の通知に似た音は入れない。
                    value = noise * 0.12 + Wave(50, t) * 0.012 + Wave(100, t) * 0.005;
                    value *= 0.86 + 0.14 * Wave(0.125, t);
                }
                else
                {
                    int chord = (int)(t / 8) % 4;
                    double local = t % 8;
                    double envelope = Ease(local / 1.5) * Ease((8 - local) / 2);
                    int root = roots[chord];
                    double third = scene == SoundtrackScene.Dawn ? 4 : 3;
                    double pad = Wave(Hz(root), t) * 0.055 + Wave(Hz(root + 7), t) * 0.025
                        + Wave(Hz(root + (int)third + 12), t) * 0.018;
                    value = pad * envelope;
                    if (scene == SoundtrackScene.Title || scene == SoundtrackScene.Dawn || scene == SoundtrackScene.Office)
                    {
                        // フェルト鍵盤風。通常業務は半分の音だけを鳴らし、間を残す。
                        int note = (int)(t / 4);
                        double age = t % 4;
                        double f = Hz(motif[note]);
                        double env = Ease(age / 0.025) * Math.Exp(-age * 1.8);
                        if (scene != SoundtrackScene.Office || note % 2 == 0)
                            value += (Wave(f, age) + Wave(f * 2, age) * 0.22 + Wave(f * 3, age) * 0.06) * env * 0.065;
                    }
                    if (scene == SoundtrackScene.Pressure)
                    {
                        double age = t % 1;
                        value += Wave(Hz(root + 12), t) * Math.Exp(-age * 9) * Ease(age / 0.02) * 0.07;
                    }
                    if (scene == SoundtrackScene.Anomaly)
                    {
                        // 言葉を発さない異常。うなりと逆再生に似た膨らみで表現する。
                        value = (Wave(55, t) + Wave(55.7, t)) * 0.025;
                        value += (Wave(146.8, t) + Wave(155.56, t)) * 0.009 * (0.5 + 0.5 * Wave(0.125, t));
                        value += noise * 0.09 * Math.Pow(Ease((t % 8) / 8), 3);
                    }
                    if (scene == SoundtrackScene.Aftermath)
                        value = Wave(55, t) * 0.035 + Wave(82.4, t) * 0.012 * envelope + noise * 0.025;
                }
                // ループ継ぎ目を短く丸める。最大振幅もここで制限する。
                double seam = Ease(i / (rate * 0.04)) * Ease((data.Length - 1 - i) / (rate * 0.04));
                data[i] = (float)(Math.Max(-0.3, Math.Min(0.3, value)) * seam);
            }
            return data;
        }
    }
}
