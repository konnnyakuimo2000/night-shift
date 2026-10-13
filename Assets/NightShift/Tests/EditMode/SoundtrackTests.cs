using System;
using NightShift.Core;
using NightShift.UI;
using NUnit.Framework;

namespace NightShift.Tests
{
    /// <summary>音量・旧設定・曲調の選択・生成波形。ゲームの判定や乱数を変更しないことを確認する。</summary>
    public class SoundtrackTests
    {
        [Test]
        public void PackagedLoopsAreAvailableWithoutRuntimeSynthesis()
        {
            foreach (SoundtrackScene scene in Enum.GetValues(typeof(SoundtrackScene)))
            {
                var clip = UnityEngine.Resources.Load<UnityEngine.AudioClip>("Audio/NightShift/" + scene);
                Assert.IsNotNull(clip, scene.ToString());
                Assert.AreEqual(scene == SoundtrackScene.Room ? 8 : 32, clip.length, 0.02f);
                Assert.AreEqual(1, clip.channels);
            }
        }

        [TestCase(SoundtrackScene.Title)]
        [TestCase(SoundtrackScene.Office)]
        [TestCase(SoundtrackScene.Pressure)]
        [TestCase(SoundtrackScene.Anomaly)]
        [TestCase(SoundtrackScene.Dawn)]
        [TestCase(SoundtrackScene.Aftermath)]
        [TestCase(SoundtrackScene.Room)]
        public void OriginalLoopsAreFiniteBoundedAndHaveQuietSeams(SoundtrackScene scene)
        {
            var samples = SoundtrackLibrary.Render(scene, 8000);
            Assert.AreEqual((scene == SoundtrackScene.Room ? 8 : 32) * 8000, samples.Length);
            double energy = 0;
            foreach (float sample in samples)
            {
                Assert.IsFalse(float.IsNaN(sample) || float.IsInfinity(sample));
                Assert.LessOrEqual(Math.Abs(sample), 0.3f);
                energy += sample * sample;
            }
            Assert.Greater(energy / samples.Length, 0.000001, "無音の音源にしない");
            Assert.AreEqual(0, samples[0]);
            Assert.AreEqual(0, samples[samples.Length - 1]);
        }

        [Test]
        public void SynthesisIsDeterministicAndDoesNotConsumeGameRandom()
        {
            var before = UnityEngine.Random.state;
            var a = SoundtrackLibrary.Render(SoundtrackScene.Office, 8000);
            var b = SoundtrackLibrary.Render(SoundtrackScene.Office, 8000);
            CollectionAssert.AreEqual(a, b);
            Assert.AreEqual(UnityEngine.JsonUtility.ToJson(before), UnityEngine.JsonUtility.ToJson(UnityEngine.Random.state));
        }

        [TestCase(GameState.Title, true, true, SoundtrackScene.Title)]
        [TestCase(GameState.Briefing, true, true, SoundtrackScene.Office)]
        [TestCase(GameState.Shift, false, false, SoundtrackScene.Office)]
        [TestCase(GameState.Shift, false, true, SoundtrackScene.Pressure)]
        [TestCase(GameState.Shift, true, true, SoundtrackScene.Anomaly)]
        [TestCase(GameState.Evaluation, true, true, SoundtrackScene.Dawn)]
        public void SceneSelectionPrioritizesVisibleScreen(GameState state, bool anomaly, bool pressure, SoundtrackScene expected)
            => Assert.AreEqual(expected, Soundscape.Select(state, anomaly, pressure));

        [Test]
        public void OldSettingsKeepNewChannelDefaultsAndExistingMasterMute()
        {
            var s = GameSettings.FromJson("{\"volume\":0,\"notifySound\":false}");
            Assert.AreEqual(0, s.volume);
            Assert.AreEqual(0.55f, s.musicVolume);
            Assert.AreEqual(0.4f, s.ambienceVolume);
            Assert.AreEqual(1, s.effectsVolume);
            Assert.IsFalse(s.notifySound);
        }

        [Test]
        public void SavedChannelMuteSurvivesReload()
        {
            var s = GameSettings.FromJson("{\"volume\":0.6,\"musicVolume\":0,\"ambienceVolume\":0,\"effectsVolume\":0}");
            Assert.AreEqual(0, s.musicVolume);
            Assert.AreEqual(0, s.ambienceVolume);
            Assert.AreEqual(0, s.effectsVolume);
        }

        [TestCase(0f, 1f, 0f)]
        [TestCase(1f, 0f, 0f)]
        [TestCase(0.6f, 0.5f, 0.3f)]
        public void EffectsRespectMasterAndOwnFader(float master, float effects, float expected)
        {
            float before = AudioFx.Volume;
            bool clicks = AudioFx.ClickEnabled, notifications = AudioFx.NotifyEnabled;
            try
            {
                new GameSettings { volume = master, effectsVolume = effects }.ApplyAudio();
                Assert.AreEqual(expected, AudioFx.Volume, 0.00001f);
            }
            finally { AudioFx.Volume = before; AudioFx.ClickEnabled = clicks; AudioFx.NotifyEnabled = notifications; }
        }
    }
}
