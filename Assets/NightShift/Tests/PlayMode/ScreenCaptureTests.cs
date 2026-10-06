using System;
using System.Collections;
using System.IO;
using NightShift.Core;
using NightShift.Data;
using NightShift.Systems;
using NightShift.UI.Screens;
using NightShift.UI.Shift;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace NightShift.Tests
{
    /// <summary>
    /// 各イベント・ゲームオーバー・エンディングの画面を PNG に書き出す（確認用）。
    /// プレイヤーのセーブには書き込まない（記録の保存を止め、セーブ地点を作る流れは通らない）。
    /// 実行: -runTests -testPlatform PlayMode -testFilter NightShift.Tests.ScreenCaptureTests（-nographics なし）
    /// 出力: Logs/Screenshots/*.png
    /// </summary>
    public class ScreenCaptureTests
    {
        const int W = 1280, H = 800;
        GameManager _gm;
        Camera _cam;
        RenderTexture _rt;
        string _dir;
        CareerRecord _original;

        [UnityTest, Category("Screenshots"), Explicit("画面の確認用"), Timeout(600000)]
        public IEnumerator CaptureAll()
        {
            _original = CareerRecord.Current;
            CareerRecord.SaveDisabled = true;
            try
            {
                yield return Setup();
                yield return Events();
                yield return GameOvers();
                yield return Endings();
            }
            finally
            {
                CareerRecord.SetCurrentForTests(_original);
                CareerRecord.SaveDisabled = false;
            }
        }

        IEnumerator Setup()
        {
            for (int i = 0; i < 10 && GameManager.Instance == null; i++) yield return null;
            _gm = GameManager.Instance ?? new GameObject("NightShift.GameManager").AddComponent<GameManager>();
            yield return null;
            _rt = new RenderTexture(W, H, 24);
            _cam = new GameObject("CaptureCamera").AddComponent<Camera>();
            _cam.clearFlags = CameraClearFlags.SolidColor;
            _cam.backgroundColor = Color.black;
            _cam.targetTexture = _rt;
            _gm.Canvas.renderMode = RenderMode.ScreenSpaceCamera;
            _gm.Canvas.worldCamera = _cam;
            _gm.Canvas.planeDistance = 10;
            _dir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs", "Screenshots"));
            Directory.CreateDirectory(_dir);
            foreach (var f in Directory.GetFiles(_dir, "*.png")) File.Delete(f);
        }

        void Clear()
        {
            foreach (Transform t in _gm.ScreenLayer) UnityEngine.Object.Destroy(t.gameObject);
            foreach (Transform t in _gm.OverlayLayer) UnityEngine.Object.Destroy(t.gameObject);
        }

        IEnumerator Shot(string name, float wait = 0.3f)
        {
            // 演出のアニメーションは最後まで飛ばす
            foreach (var p in UnityEngine.Object.FindObjectsByType<TimelinePlayer>(FindObjectsSortMode.None)) p.Skip();
            float end = Time.realtimeSinceStartup + wait;
            while (Time.realtimeSinceStartup < end) yield return null;
            foreach (var p in UnityEngine.Object.FindObjectsByType<TimelinePlayer>(FindObjectsSortMode.None)) p.Skip();
            yield return null;
            Canvas.ForceUpdateCanvases();
            _cam.Render();
            var prev = RenderTexture.active;
            RenderTexture.active = _rt;
            var tex = new Texture2D(W, H, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, W, H), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;
            File.WriteAllBytes(Path.Combine(_dir, name + ".png"), tex.EncodeToPNG());
            UnityEngine.Object.Destroy(tex);
        }

        ShiftDesktop NewDesktop(int day, Action<ShiftSession> prepare = null, float playRealSeconds = 0)
        {
            Clear();
            var db = _gm.Database;
            var s = new ShiftSession(db, db.GetDay(day), 7, "night01");
            prepare?.Invoke(s);
            var d = ShiftDesktop.Build(_gm.ScreenLayer, _gm, s);
            s.Ui = d;
            s.StartShift();
            d.OnShiftStarted();
            // 時計を進めて、要求や問い合わせが届いた状態にする
            for (float t = 0; t < playRealSeconds; t += 0.5f) s.Tick(0.5f);
            return d;
        }

        // ---------------- イベント ----------------
        IEnumerator Events()
        {
            var db = _gm.Database;
            CareerRecord.SetCurrentForTests(new CareerRecord { seed = 7, trust = 70 });

            // 朝のネットニュース（ふつう／改ざん・停電の翌朝）
            Clear(); NewsScreen.Build(_gm.ScreenLayer, db, db.GetDay(8), () => { });
            yield return Shot("E01_ニュース_DAY8", 7f);
            Clear(); NewsScreen.Build(_gm.ScreenLayer, db, db.GetDay(20), () => { });
            yield return Shot("E02_ニュース_DAY20_身代金ウイルス", 7f);
            CareerRecord.Current.SetFlag("NEWS_DEFACED");
            Clear(); NewsScreen.Build(_gm.ScreenLayer, db, db.GetDay(18), () => { });
            yield return Shot("E03_ニュース_改ざんされた翌朝", 7f);
            CareerRecord.Current.ClearFlag("NEWS_DEFACED");
            CareerRecord.Current.SetFlag("NEWS_BLACKOUT");
            Clear(); NewsScreen.Build(_gm.ScreenLayer, db, db.GetDay(20), () => { });
            yield return Shot("E04_ニュース_停電の翌朝", 7f);
            CareerRecord.Current.ClearFlag("NEWS_BLACKOUT");

            // 業務中: 接続監視（残り時間の列）
            var d = NewDesktop(20, null, 150f);
            yield return Shot("E05_業務画面_DAY20", 2.5f);
            ConnectionMonitorView.Open(d, null);
            yield return Shot("E06_接続監視", 1f);

            // 前任者の手がかり
            d = NewDesktop(20);
            d.ShowClue("C1", "ワークステーションの裏に貼られていた、前任者の付箋。\n「夜は長い。全部を一度に見ようとしないこと」\n— E-0004");
            yield return Shot("E07_手がかり_前任者の付箋", 0.5f);

            // 偽のログイン画面（フィッシング）
            d = NewDesktop(13);
            PhishFormView.Open(d, new MailMessage { MailId = "capture", Subject = "【重要】アカウント再登録のお願い" });
            yield return Shot("E08_偽の再登録画面", 0.5f);

            // ホラー演出
            d = NewDesktop(17);
            yield return null;
            d.S.RequestHorror("GLITCH");
            yield return Shot("E09_文字化けのノイズ", 0.05f);
            d = NewDesktop(13);
            yield return null;
            d.S.RequestHorror("BSOD");
            yield return Shot("E10_ブルースクリーン_BLUESTONE", 1.5f);
            d = NewDesktop(19);
            yield return null;
            d.S.RequestBlackout();
            yield return Shot("E11_停電_REDACT", 1.5f);
            d = NewDesktop(14);
            d.S.OperatorCompromised = true;
            yield return Shot("E12_もう1つのカーソル_DOPPELGANGER", 1.5f);

            // 夜の終わり: 評価と振り返り
            Clear();
            var s = new ShiftSession(db, db.GetDay(20), 7, "night01");
            s.StartShift();
            s.Complete();
            var result = s.Evaluation.Compute();
            var slip = Payroll.Compute(20, result, s.Evaluation, db.payroll);
            var root = EvaluationScreen.Build(_gm.ScreenLayer, db.GetDay(20), result, slip, null, true, 52300, () => { }, () => { },
                "今夜の見落とし・誤り: 2 件\n\n<b>REQ-0012</b>  01:42  佐藤 蓮（E-0021） 社内 (10.10.4.84) → FILE-SRV01 FILE\n   <color=#B00000>攻撃の要求を許可した</color>\n   正しい対応: 拒否 — 理由: 接続元IPが端末IPと違う\n\n<b>HD-0003</b>  バックアップはもういらない？（田中 美咲）\n   <color=#B00000>回答が誤り</color>\n   正しい回答: 定期的に外部の記録媒体へバックアップし、ネットワークから切り離して保管する\n");
            yield return Shot("E13_評価画面", 1f);
            ReviewPanel.Show((RectTransform)root.transform, "DAY20 の振り返り", "今夜の見落とし・誤り: 2 件\n\n<b>REQ-0012</b>  01:42  佐藤 蓮（E-0021） 社内 (10.10.4.84) → FILE-SRV01 FILE\n   <color=#B00000>攻撃の要求を許可した</color>\n   正しい対応: 拒否 — 理由: 接続元IPが端末IPと違う\n\n<b>HD-0003</b>  バックアップはもういらない？（田中 美咲）\n   <color=#B00000>回答が誤り</color>\n   正しい回答: 定期的に外部の記録媒体へバックアップし、ネットワークから切り離して保管する");
            yield return Shot("E14_振り返り", 0.3f);
        }

        // ---------------- ゲームオーバー ----------------
        IEnumerator GameOvers()
        {
            var db = _gm.Database;
            EndingResolver.GameOver Check(Action<CareerRecord, ShiftSession> setup)
            {
                var c = new CareerRecord { seed = 7, trust = 70 };
                CareerRecord.SetCurrentForTests(c);
                var s = new ShiftSession(db, db.GetDay(20), 7, "night01");
                setup(c, s);
                return EndingResolver.CheckGameOver(c, s);
            }
            var gos = new[]
            {
                Check((c, s) => { for (int d = 18; d <= 20; d++) c.nights.Add(new NightRecord { day = d, rank = "D" }); }),
                Check((c, s) => s.FatalIncident = "全社のファイルが暗号化された（BRIAR ROSE）"),
                Check((c, s) => c.accountCompromised = 3),
                Check((c, s) => { for (int d = 14; d <= 20; d++) c.nights.Add(new NightRecord { day = d, rank = "B", overtime = true }); }),
            };
            foreach (var go in gos)
            {
                Assert.IsNotNull(go);
                Clear();
                GameOverScreen.Build(_gm.ScreenLayer, go, "night01", () => { }, () => { }, () => { });
                yield return Shot($"{go.Id}_{go.Title}", 4f);
            }
        }

        // ---------------- エンディング ----------------
        IEnumerator Endings()
        {
            CareerRecord Career(string rank, int trust, int reports = 0, bool clues = false)
            {
                var c = new CareerRecord { seed = 7, trust = trust };
                for (int d = 8; d <= 30; d++) c.nights.Add(new NightRecord { day = d, rank = rank, correctReports = reports });
                if (clues) c.clues.AddRange(new[] { "C1", "C2", "C3", "C4" });
                return c;
            }
            var cases = new (CareerRecord, FinalOutcome)[]
            {
                (Career("S", 90), FinalOutcome.Critical),
                (Career("B", 20), FinalOutcome.Defended),
                (Career("S", 90, 0, true), FinalOutcome.Defended),
                (Career("S", 90, 1), FinalOutcome.Defended),
                (Career("C", 70), FinalOutcome.PartialDamage),
            };
            foreach (var (career, outcome) in cases)
            {
                CareerRecord.SetCurrentForTests(career);
                var ending = EndingResolver.Resolve(career, outcome);
                Clear();
                EndingScreen.Build(_gm.ScreenLayer, ending, outcome, "night01", _gm.Wallet, () => { });
                if (ending.TrueRoute)
                {
                    // 前任者との対話（最後まで）→ 選んだ後の新聞
                    yield return Shot($"{ending.Id}_{ending.Title}_1対話", 20f);
                    ending.TrueRoute = false;
                    ending.Text += "\n\nあなたは通報した。前任者は逮捕されたが、裁判で会社の不正もすべて明るみに出た。あなたは夜勤を辞めた。\n\nそして、夜が明けた。";
                    Clear();
                    EndingScreen.Build(_gm.ScreenLayer, ending, outcome, "night01", _gm.Wallet, () => { });
                    yield return Shot($"{ending.Id}_{ending.Title}_2新聞", 5f);
                }
                else yield return Shot($"{ending.Id}_{ending.Title}", 5f);
            }
        }
    }
}
