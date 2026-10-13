using System.Collections;
using System.Linq;
using NightShift.Core;
using NightShift.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace NightShift.Tests
{
    /// <summary>隔離コピーで、再生コンポーネント・画面切替・ミュートを検証する。</summary>
    public class SoundscapePlayModeTests
    {
        GameManager _game;
        string _settings;
        GameObject _ending;

        [UnitySetUp]
        public IEnumerator Setup()
        {
            if (!AuditGuard.Allowed) Assert.Ignore("隔離コピーで実行してください（Run-PlayModeAudit.ps1）。");
            yield return null;
            _game = GameManager.Instance;
            Assert.IsNotNull(_game);
            _settings = JsonUtility.ToJson(GameSettings.Current);
            _game.ShowTitle();
            Soundscape.Instance.SendMessage("OnApplicationFocus", true);
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            if (_ending != null) Object.Destroy(_ending);
            if (_settings != null)
            {
                JsonUtility.FromJsonOverwrite(_settings, GameSettings.Current);
                GameSettings.Current.ApplyAudio();
            }
            if (_game != null) _game.ShowTitle();
            yield return null;
        }

        [UnityTest]
        public IEnumerator InitDoesNotDuplicateListenerOrAudioSources()
        {
            int sources = _game.GetComponents<AudioSource>().Length;
            int listeners = Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None).Length;
            AudioFx.Init(_game.gameObject);
            Soundscape.Attach(_game.gameObject);
            yield return null;
            Assert.AreEqual(sources, _game.GetComponents<AudioSource>().Length);
            Assert.AreEqual(1, listeners);
            Assert.AreEqual(listeners, Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None).Length);
            Assert.AreEqual(3, _game.GetComponents<AudioSource>().Count(s => s.loop));
        }

        [UnityTest]
        public IEnumerator EndingCueIsRemovedWhenItsScreenCloses()
        {
            Assert.AreEqual(SoundtrackScene.Title, Soundscape.Instance.CurrentScene);
            _ending = new GameObject("AudioTestEnding");
            SoundtrackCue.Attach(_ending, SoundtrackScene.Aftermath);
            yield return null;
            Assert.AreEqual(SoundtrackScene.Aftermath, Soundscape.Instance.CurrentScene);
            Object.Destroy(_ending);
            yield return null;
            yield return null;
            Assert.AreEqual(SoundtrackScene.Title, Soundscape.Instance.CurrentScene);
            Assert.IsNull(SoundtrackCue.Active);
        }

        [UnityTest]
        public IEnumerator MasterMuteAndFocusMuteReachAllSources()
        {
            var settings = GameSettings.Current;
            settings.volume = 1; settings.musicVolume = 1; settings.effectsVolume = 1;
            settings.ApplyAudio();
            yield return new WaitForSecondsRealtime(0.4f);
            Assert.IsTrue(_game.GetComponents<AudioSource>().Any(s => s.loop && s.volume > 0));
            settings.volume = 0; settings.ApplyAudio();
            yield return null;
            Assert.IsTrue(_game.GetComponents<AudioSource>().All(s => s.volume == 0));
            settings.volume = 1; settings.ApplyAudio();
            Soundscape.Instance.SendMessage("OnApplicationFocus", false);
            yield return null;
            Assert.IsTrue(_game.GetComponents<AudioSource>().All(s => s.volume == 0));
            Soundscape.Instance.SendMessage("OnApplicationFocus", true);
        }
    }
}
