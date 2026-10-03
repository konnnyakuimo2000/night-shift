using System;

namespace NightShift.Core
{
    /// <summary>
    /// ゲーム内時計。リアルタイム進行。停止するのは Pause Menu を明示的に開いた時のみ。
    /// Unity の Time.timeScale は使わず、unscaledDeltaTime から独自に進める（UIアニメーションを止めないため）。
    /// </summary>
    public class TimeManager
    {
        public float CurrentMinute { get; private set; }
        public float StartMinute { get; }
        public float EndMinute { get; }
        public float RealSecondsPerGameMinute { get; set; }
        public float DebugTimeScale { get; set; } = 1f;
        public bool Running { get; private set; }
        public bool Paused { get; set; }

        public event Action<int> MinuteChanged;

        int _lastWholeMinute = -1;

        public TimeManager(float start, float end, float realSecondsPerGameMinute)
        {
            StartMinute = start;
            EndMinute = end;
            CurrentMinute = start;
            RealSecondsPerGameMinute = Math.Max(0.01f, realSecondsPerGameMinute);
        }

        public void Start() { Running = true; }
        public void Stop() { Running = false; }

        /// <summary>このフレームで進んだゲーム内分</summary>
        public float Tick(float unscaledDeltaTime)
        {
            if (!Running || Paused) return 0f;
            float delta = unscaledDeltaTime / RealSecondsPerGameMinute * DebugTimeScale;
            CurrentMinute += delta;
            int whole = (int)Math.Floor(CurrentMinute);
            if (whole != _lastWholeMinute)
            {
                _lastWholeMinute = whole;
                MinuteChanged?.Invoke(whole);
            }
            return delta;
        }

        public void JumpTo(float minute)
        {
            CurrentMinute = Math.Max(StartMinute, minute);
        }

        public static string Format(float minute)
        {
            int m = (int)Math.Floor(minute);
            if (m < 0) m = 0;
            return $"{m / 60:00}:{m % 60:00}";
        }

        public static string FormatWithSeconds(float minute)
        {
            int total = (int)Math.Floor(minute * 60f);
            if (total < 0) total = 0;
            return $"{total / 3600:00}:{total / 60 % 60:00}:{total % 60:00}";
        }

        public string Now => Format(CurrentMinute);
    }
}
