using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NightShift.Core;
using NightShift.Data;
using NightShift.Systems;
using NightShift.UI;
using NightShift.UI.Shift;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace NightShift.Tests
{
    /// <summary>
    /// 行選択と操作対象の一致・統合したアプリの画面（TOOL_REWORK_CLAUDE.md の受け入れテスト 1〜4・12・15）。
    /// 記録はテスト用に差し替え、保存を止める（プレイヤーのセーブに触れない）。隔離したプロジェクトのコピーで実行する。
    /// </summary>
    public class ToolReworkPlayTests
    {
        GameManager _gm;
        CareerRecord _original;

        [UnitySetUp]
        public IEnumerator Setup()
        {
            if (!AuditGuard.Allowed)
                Assert.Ignore("Run in the isolated project copy described in Docs/PLAYMODE_AUDIT.md.");
            for (int i = 0; i < 10 && GameManager.Instance == null; i++) yield return null;
            _gm = GameManager.Instance ?? new GameObject("NightShift.GameManager").AddComponent<GameManager>();
            yield return null;
            _original = CareerRecord.Current;
            CareerRecord.SaveDisabled = true;
            CareerRecord.SetCurrentForTests(new CareerRecord { seed = 7, trust = 70 });
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            if (_gm != null)
            {
                foreach (Transform t in _gm.ScreenLayer) Object.Destroy(t.gameObject);
                foreach (Transform t in _gm.OverlayLayer) Object.Destroy(t.gameObject);
            }
            CareerRecord.SetCurrentForTests(_original);
            CareerRecord.SaveDisabled = false;
            yield return null;
        }

        ShiftDesktop NewDesktop(int day, float playRealSeconds = 0, params string[] own)
        {
            foreach (Transform t in _gm.ScreenLayer) Object.Destroy(t.gameObject);
            foreach (var id in own) CareerRecord.Current.AddItem(id);
            var db = _gm.Database;
            var s = new ShiftSession(db, db.GetDay(day), 7, "night01");
            var d = ShiftDesktop.Build(_gm.ScreenLayer, _gm, s);
            s.Ui = d;
            s.StartShift();
            d.OnShiftStarted();
            for (float t = 0; t < playRealSeconds; t += 0.5f) s.Tick(0.5f);
            d.RebuildMonitors();
            return d;
        }

        static RetroWindow ToolWindowOf<T>(ShiftDesktop d) where T : ToolWindow
        {
            var t = d.GetComponentsInChildren<T>(true).FirstOrDefault();
            return t != null ? t.Win : null;
        }

        static FlatButton Button(Component root, string label) =>
            root.GetComponentsInChildren<FlatButton>(true).FirstOrDefault(b => b.Label != null && b.Label.text.StartsWith(label));

        static LogEntry LogFrom(ShiftDesktop d, string ip) => d.S.Logs.Add(d.S.Time.CurrentMinute, ip, "FILE-SRV01", "-", "SMB", "OK", "FILE READ test.xls");

        // 1・2
        [UnityTest]
        public IEnumerator Isolation_FollowsTheLastSelected_RequestOrLog_AndClearsOnUnknownRows()
        {
            var d = NewDesktop(20, 0, Items.Monitor1, Items.Isolator);
            var s = d.S;
            var emp = s.Employees.All.First(e => e.IsActive);
            var a = new Events.EventInstance { Employee = emp, DisplayUser = emp.fullName, DisplayEmployeeId = emp.employeeId, Device = emp.deviceId, SourceIp = s.Employees.GetIp(emp), AccessType = "共有ファイル（閲覧）" };
            s.Requests.Submit(a);
            var x = s.Employees.All.First(e => e.IsActive && e.deviceId != a.Device);
            d.SelectRequest(a);
            Tools.Launch(d.Stations[0], Items.Isolator);
            yield return null;
            var list = ToolWindowOf<DeviceManagerApp>(d).GetComponentsInChildren<ToolList>(true).First();
            Assert.AreEqual(a.Device, (list.Selected as EmployeeData)?.deviceId, "要求を選ぶと、その端末");
            d.SelectLog(LogFrom(d, s.Employees.GetIp(x)));
            Assert.AreEqual(x.deviceId, (list.Selected as EmployeeData)?.deviceId, "後から選んだログが勝つ");
            Assert.AreEqual(x.deviceId, d.TargetDevice);
            d.SelectMail(PhishingMail.Send(s, false));
            Assert.AreEqual(x.deviceId, (list.Selected as EmployeeData)?.deviceId, "メールの選択では変わらない");

            d.SelectLog(LogFrom(d, "FIREWALL"));
            yield return null;
            Assert.IsNull(list.Selected, "端末の分からない行では、前の端末を残さない");
            Assert.IsNull(d.TargetDevice);
            var btn = Button(ToolWindowOf<DeviceManagerApp>(d), "この端末を隔離");
            Assert.IsFalse(btn.Interactable, "実行できない");
        }

        // 3
        [UnityTest]
        public IEnumerator BlockDialog_KeepsTypedRange_WhenMailIsSelected()
        {
            var d = NewDesktop(20, 0, Items.Monitor1);
            BlockDialog.Open(d, "198.51.100.23");
            yield return null;
            var input = d.GetComponentsInChildren<InputField>(true).First(i => i.text == "198.51.100.23");
            input.text = "198.51.*";
            d.SelectMail(PhishingMail.Send(d.S, false));
            yield return null;
            Assert.AreEqual("198.51.*", input.text);
        }

        // 4
        [UnityTest]
        public IEnumerator ToolList_ClearsRemovedSelection_AndKeepsTheRest()
        {
            var host = new GameObject("Host", typeof(RectTransform));
            host.transform.SetParent(_gm.ScreenLayer, false);
            var list = ToolList.Create(host.transform, Color.gray, "なし");
            object notified = "none";
            list.SelectionChanged += o => notified = o;
            var a = "A"; var b = "B";
            list.SetItems(new List<string> { a, b }, v => new ToolList.Row(v, v));
            list.Select(b);
            list.SetItems(new List<string> { a, b }, v => new ToolList.Row(v, v + "!"));
            Assert.AreEqual(b, list.Selected, "残っている対象の選択は維持");
            list.SetItems(new List<string> { a }, v => new ToolList.Row(v, v));
            Assert.IsNull(list.Selected);
            Assert.IsNull(notified, "解除を通知する");
            list.Select("Z");
            Assert.IsNull(list.Selected, "一覧にないものは選ばない");
            Object.Destroy(host);
            yield return null;
        }

        // 12・15
        [UnityTest]
        public IEnumerator PartialRights_ShowOnlyOwnedTabs_WithAReasonForTheOthers()
        {
            var d = NewDesktop(25, 0, Items.Monitor1, Items.Isolator, Items.Correlator);
            Tools.Launch(d.Stations[0], Items.Isolator);
            Tools.Launch(d.Stations[0], Items.Correlator);
            yield return null;
            var dev = ToolWindowOf<DeviceManagerApp>(d);
            Assert.IsNotNull(dev, "隔離だけの権利でも端末管理が開く");
            var diag = Button(dev, "接続中のPCの診断");
            diag.OnClick?.Invoke();
            yield return null;
            var locked = dev.GetComponentsInChildren<Text>(false).Select(t => t.text).ToList();
            Assert.IsTrue(locked.Any(t => t.Contains("この機能はまだ使えません")));
            Assert.IsTrue(locked.Any(t => t.Contains("端末管理: 接続中のPCの診断")), "何が必要かを出す");

            var log = ToolWindowOf<LogAnalyzerApp>(d);
            Assert.IsNotNull(log, "相関だけの権利でもログ解析が開く");
            Button(log, "選んだログ").OnClick?.Invoke();
            yield return null;
            Assert.IsTrue(log.GetComponentsInChildren<Text>(false).Any(t => t.text.Contains("この機能には「ログ解析」が必要です")));

            // 15: タブ・ボタンの文字が見切れない
            foreach (var w in new[] { dev, log })
                foreach (var b in w.GetComponentsInChildren<FlatButton>(false))
                    if (b.Label != null && b.Label.horizontalOverflow == HorizontalWrapMode.Overflow)
                        Assert.LessOrEqual(b.Label.preferredWidth, b.Rect.rect.width + 2, b.Label.text);
        }
    
        static void Infect(ShiftSession s, string virus)
        {
            var emp = s.Employees.All.First(e => e.IsActive && e.onShift);
            var def = ScriptableObject.CreateInstance<ThreatScenarioDefinition>();
            var run = new Events.ThreatScenarioRun { Definition = def, Victim = emp };
            Events.ThreatScripts.Run(s.Director.Threats, run, new ThreatStep { kind = ThreatStepKind.Script, script = "INFECT:" + virus }, 0);
        }

        // 18・19・20
        [UnityTest]
        public IEnumerator DisplayVirusFx_NeverTakesClicks_IsCapped_AndSameStateWhenReduced()
        {
            var d = NewDesktop(26, 0, Items.Monitor1);
            ConnectionMonitorView.Open(d, null);
            yield return null;
            var allow = d.GetComponentsInChildren<RetroButton>(true).First(b => b.Label != null && b.Label.text.StartsWith("接続を許可"));
            var before = allow.GetComponent<RectTransform>().position;
            foreach (var v in new[] { "V9", "V10", "V11", "V12", "V13", "V14" })
            {
                // 1台に1種類なので、種類ごとに別の社員を感染させる
                var emp = d.S.Employees.All.Where(e => e.IsActive && e.onShift && !d.S.Security.IsInfected(e.deviceId)).First();
                var run = new Events.ThreatScenarioRun { Definition = ScriptableObject.CreateInstance<ThreatScenarioDefinition>(), Victim = emp };
                Events.ThreatScripts.Run(d.S.Director.Threats, run, new ThreatStep { kind = ThreatStepKind.Script, script = "INFECT:" + v }, 0);
            }
            for (int i = 0; i < 20; i++) d.S.Display.Tick(10); // 最大まで（長時間）
            for (int i = 0; i < 30; i++) yield return null;
            var fx = d.GetComponentInChildren<DisplayVirusFx>();
            Assert.IsNotNull(fx);
            Assert.LessOrEqual(fx.ObjectCount, DisplayVirusFx.MaxObjects, "部品の数に上限");
            Assert.IsFalse(fx.GetComponentsInChildren<Graphic>(true).Any(g => g.raycastTarget), "クリックを受ける部品がない");
            Assert.IsFalse(fx.GetComponent<CanvasGroup>().blocksRaycasts);
            Assert.AreEqual(before, allow.GetComponent<RectTransform>().position, "審査ボタンの位置は変わらない");
            float level = d.S.Display.Level("V11");
            GameSettings.Current.reduceFlashing = true; GameSettings.Current.horrorMild = true;
            for (int i = 0; i < 10; i++) yield return null;
            Assert.AreEqual(level, d.S.Display.Level("V11"), "軽減しても状態は同じ");
            GameSettings.Current.reduceFlashing = false; GameSettings.Current.horrorMild = false;
            d.S.Complete();
            for (int i = 0; i < 5; i++) yield return null;
            Assert.AreEqual(0, fx.ObjectCount, "夜が終わったら演出を残さない");
        }
}
}
