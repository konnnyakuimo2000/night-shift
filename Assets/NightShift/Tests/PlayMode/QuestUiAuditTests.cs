using System.Collections;
using System.Linq;
using System.Reflection;
using NightShift.Core;
using NightShift.Systems;
using NightShift.Systems.SideQuests;
using NightShift.UI;
using NightShift.UI.Shift;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace NightShift.Tests
{
    /// <summary>サブクエストの二択・全面演出と、一時停止・入力の関係（隔離したプロジェクトのコピーで実行する）</summary>
    public class QuestUiAuditTests
    {
        GameManager gm;
        ShiftDesktop desktop;
        ShiftSession S => gm.Session;

        [UnitySetUp]
        public IEnumerator Setup()
        {
            if (Application.companyName != "NightShiftPlayModeAudit")
                Assert.Ignore("Run in the isolated project copy described in Docs/PLAYMODE_AUDIT.md.");
            yield return null;
            gm = GameManager.Instance;
            Assert.IsNotNull(gm);
            if (Object.FindAnyObjectByType<AudioListener>() == null) gm.gameObject.AddComponent<AudioListener>();
            gm.ShowTitle();
            yield return null;
            PlayerPrefs.DeleteAll();
            typeof(GameManager).GetMethod("StartNewGame", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(gm, null);
            GameSettings.Current.volume = 0;
            gm.StartDay(20);
            yield return null;
            desktop = gm.ScreenLayer.GetComponentInChildren<ShiftDesktop>();
            desktop.PendingStart();
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            if (gm != null)
            {
                typeof(GameManager).GetField("_completeTimer", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(gm, -1f);
                gm.ShowTitle();
                yield return null;
            }
            if (Application.companyName == "NightShiftPlayModeAudit") PlayerPrefs.DeleteAll();
        }

        FlatButton Find(string label) => desktop.GetComponentsInChildren<FlatButton>().FirstOrDefault(b => b.Label.text == label);

        [UnityTest]
        public IEnumerator Choice_IsNotAModal_KeepsClockStoppedThroughPause_AndArmsLater()
        {
            var c = new MorningChoice { QuestId = "SQ06", Key = "JOIN", Title = "二択", Text = "テスト", Accept = "加わる(テスト)", Decline = "離脱(テスト)" };
            S.Quests.RequestUi("CHOICE", c);
            yield return null;
            Assert.IsTrue(S.Time.Paused, "二択の間は時計が止まる");
            Assert.IsFalse(desktop.ModalOpen, "Enter・Esc で決まる共通の確認ダイアログではない");
            Assert.IsTrue(desktop.InputBlocked, "承認などの入力を受け付けない");
            var decline = Find("離脱(テスト)");
            Assert.IsNotNull(decline);
            Assert.IsFalse(decline.Interactable, "表示直後は押せない（入力の持ち越し防止）");

            desktop.OpenPause();
            desktop.ClosePause();
            Assert.IsTrue(S.Time.Paused, "一時停止を閉じても、二択の間は時計を再開しない");

            yield return new WaitForSecondsRealtime(1.3f);
            Assert.IsTrue(decline.Interactable);
            Assert.IsNotNull(S.Quests.PendingChoice);
        }

        [UnityTest]
        public IEnumerator FullEffect_PauseDuringEffect_DoesNotResumeEarlyOrLate()
        {
            S.Quests.RequestUi("FX", "RED");
            yield return null;
            Assert.IsTrue(S.Time.Paused, "全面演出の間は時計が止まる");
            Assert.IsTrue(desktop.InputBlocked);
            desktop.OpenPause();
            desktop.ClosePause();
            Assert.IsTrue(S.Time.Paused, "演出中に一時停止を閉じても再開しない");
            yield return new WaitForSecondsRealtime(5.6f);
            Assert.IsFalse(S.Time.Paused, "演出が終わったら再開する");
            Assert.IsFalse(desktop.InputBlocked);
        }

        [UnityTest]
        public IEnumerator FullEffect_EndingWhilePauseOpen_StaysPaused()
        {
            S.Quests.RequestUi("FX", "APPROACH");
            yield return null;
            desktop.OpenPause();
            yield return new WaitForSecondsRealtime(2.0f);
            Assert.IsTrue(S.Time.Paused, "一時停止メニューが開いている間は、演出が終わっても止まったまま");
            desktop.ClosePause();
            Assert.IsFalse(S.Time.Paused);
        }
    }
}
