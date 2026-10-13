using NightShift.Core;
using NightShift.Systems.SideQuests;
using UnityEngine;

namespace NightShift.UI
{
    /// <summary>音だけを管理する。ゲーム時計・要求・ルートの状態には書き込まない。</summary>
    public sealed class Soundscape : MonoBehaviour
    {
        public static Soundscape Instance { get; private set; }
        readonly AudioSource[] _music = new AudioSource[2];
        readonly float[] _levels = new float[2];
        AudioSource _room;
        int _front;
        bool _started, _focused = true;
        float _duckUntil, _pressureUntil;
        float _duckGain = 1;
        SoundtrackScene _scene;
        public SoundtrackScene CurrentScene => _scene;
        public const float FadeSeconds = 2.5f;

        public static void Attach(GameObject host)
        {
            if (Instance == null) host.AddComponent<Soundscape>();
        }
        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
            // カメラを持たないUI専用シーンでも聞こえるようにする。
            if (FindAnyObjectByType<AudioListener>() == null) gameObject.AddComponent<AudioListener>();
            for (int i = 0; i < 2; i++) _music[i] = Source();
            _room = Source();
        }
        AudioSource Source()
        {
            var source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false; source.loop = true; source.spatialBlend = 0;
            source.volume = 0; source.priority = 180;
            return source;
        }

        public static void Duck(float seconds = 0.8f)
        {
            if (Instance != null) Instance._duckUntil = Mathf.Max(Instance._duckUntil, Time.unscaledTime + seconds);
        }

        /// <summary>画面に出ている状態だけから曲調を選ぶ。正解・悪意・未来のイベントを先読みしない。</summary>
        public static SoundtrackScene Select(GameState state, bool anomaly, bool pressure)
        {
            if (state == GameState.Title) return SoundtrackScene.Title;
            if (state == GameState.Evaluation || state == GameState.Complete) return SoundtrackScene.Dawn;
            if (state == GameState.Shift && anomaly) return SoundtrackScene.Anomaly;
            if (state == GameState.Shift && pressure) return SoundtrackScene.Pressure;
            return SoundtrackScene.Office;
        }

        void Update()
        {
            var game = GameManager.Instance;
            if (game == null) return;
            var settings = GameSettings.Current;
            var session = game.Session;
            bool work = game.State == GameState.Shift;
            if (work && session != null && session.Requests.Pending.Count >= 5) _pressureUntil = Time.unscaledTime + 8;
            var horror = CareerRecord.Current.FindQuest("SQ03");
            bool anomaly = horror != null && horror.status == QuestStatus.Active && horror.Has("FX_DAY") && !settings.horrorMild;
            var scene = SoundtrackCue.Active != null ? SoundtrackCue.Active.Scene : Select(game.State, anomaly, work && Time.unscaledTime < _pressureUntil);
            if (!work) _pressureUntil = 0;
            if (!_started || _scene != scene) Switch(scene);
            if (!_room.isPlaying && (work || game.State == GameState.Briefing))
            {
                _room.clip = SoundtrackLibrary.Get(SoundtrackScene.Room); _room.Play();
            }
            float master = Mathf.Clamp01(settings.volume) * (_focused ? 1 : 0);
            float quiet = session != null && session.Time.Paused && work ? 0.25f : 1;
            _duckGain = Mathf.MoveTowards(_duckGain, Time.unscaledTime < _duckUntil ? 0.35f : 1, Time.unscaledDeltaTime * 4);
            for (int i = 0; i < 2; i++)
            {
                _levels[i] = Mathf.MoveTowards(_levels[i], i == _front ? 1 : 0, Time.unscaledDeltaTime / FadeSeconds);
                _music[i].volume = _levels[i] * master * Mathf.Clamp01(settings.musicVolume) * quiet * _duckGain;
                if (i != _front && _levels[i] == 0 && _music[i].isPlaying) _music[i].Stop();
            }
            float room = (work || game.State == GameState.Briefing) ? master * Mathf.Clamp01(settings.ambienceVolume) * quiet : 0;
            _room.volume = master <= 0 || settings.ambienceVolume <= 0 ? 0 : Mathf.MoveTowards(_room.volume, room, Time.unscaledDeltaTime * 0.3f);
            if (room == 0 && _room.volume == 0 && _room.isPlaying) _room.Stop();
        }

        void Switch(SoundtrackScene scene)
        {
            // 急な往復では前のトラックを再利用し、不要な再生成と再起動を避ける。
            var clip = SoundtrackLibrary.Get(scene);
            int back = 1 - _front;
            if (_music[back].clip != clip || !_music[back].isPlaying)
            {
                _music[back].Stop(); _music[back].clip = clip; _music[back].volume = 0;
                _levels[back] = 0; _music[back].Play();
            }
            _front = back; _scene = scene; _started = true;
        }
        void OnApplicationFocus(bool focus) { _focused = focus; AudioFx.Focused = focus; }
        void OnDestroy()
        {
            if (Instance != this) return;
            foreach (var source in _music) if (source != null) source.Stop();
            if (_room != null) _room.Stop();
            SoundtrackLibrary.Release();
            AudioFx.Shutdown();
            Instance = null;
        }
    }

    /// <summary>終端の音は画面の寿命に結び付ける。タイトル・再挑戦へ持ち越さない。</summary>
    public sealed class SoundtrackCue : MonoBehaviour
    {
        public static SoundtrackCue Active { get; private set; }
        public SoundtrackScene Scene { get; private set; }
        public static void Attach(GameObject screen, SoundtrackScene scene)
        {
            var cue = screen.AddComponent<SoundtrackCue>(); cue.Scene = scene; Active = cue;
        }
        void OnDisable() { if (Active == this) Active = null; }
    }
}
