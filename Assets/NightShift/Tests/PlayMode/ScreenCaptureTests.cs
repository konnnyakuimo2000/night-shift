using System;
using System.Collections;
using System.IO;
using System.Linq;
using NightShift.Core;
using NightShift.Data;
using NightShift.Systems;
using NightShift.UI;
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

        IEnumerator Setup(bool clean = true)
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
            if (clean) foreach (var f in Directory.GetFiles(_dir, "*.png")) File.Delete(f);
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

            // タイトル（デバッグモードの開始日の選択）・デバッグパネル
            Clear(); _gm.ShowTitle();
            yield return Shot("D01_タイトル", 1f);
            var dbg = NewDesktop(20, null, 60f);
            DebugPanelView.Open(dbg);
            yield return Shot("D02_デバッグパネル", 1f);

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
        /// <summary>ストアと各ツールの画面（ツールを全部持たせた一時的な記録で撮る。セーブには書き込まない）</summary>
        [UnityTest, Category("Screenshots"), Explicit("画面の確認用"), Timeout(600000)]
        public IEnumerator CaptureStoreAndTools()
        {
            _original = CareerRecord.Current;
            CareerRecord.SaveDisabled = true;
            try
            {
                yield return Setup(false);
                var db = _gm.Database;
                var career = new CareerRecord { seed = 7, trust = 70 };
                career.vaccineDefinitionDay = 20;
                CareerRecord.SetCurrentForTests(career);

                // ストア（買う前: モニター1台目だけ持っている）
                career.AddItem(Items.Monitor1);
                var d = NewDesktop(25, null, 30f);
                StoreView.Open(d);
                yield return Shot("S01_ストア", 1f);

                // ツールを全部持たせて、サブモニターで1つずつ起動する
                foreach (var it in db.storeItems)
                    if (it != null && !it.consumable && !career.Owns(it.itemId)) career.AddItem(it.itemId);
                string[][] tools =
                {
                    new[] { Items.ProfilePanel, "社員情報パネル" }, new[] { Items.AutoReview, "自動審査ツール" }, new[] { Items.LogAnalyzer, "ログ解析" },
                    new[] { Items.FwConsole, "遮断コンソール" }, new[] { Items.MailInspector, "メール差出人チェッカー" }, new[] { Items.RemoteKit, "リモート診断キット" },
                    new[] { Items.Isolator, "端末隔離ツール" }, new[] { Items.ServerMonitor, "サーバー監視ダッシュボード" }, new[] { Items.Correlator, "相関タイムライン" },
                    new[] { Items.Vaccine, "VACCINE99" }, new[] { Items.SocAi, "SOC-AI" },
                };
                int n = 1;
                foreach (var t in tools)
                {
                    d = NewDesktop(25, null, 150f);
                    // 情報を出すツールのために、保留中の要求を1つ選んでおく
                    if (d.S.Requests.Pending.Count > 0) d.SelectRequest(d.S.Requests.Pending[0]);
                    // 社員情報パネル用: 名簿にいる社員の要求を選ぶ（顔写真が出る）
                    if (t[0] == Items.ProfilePanel)
                    {
                        var all = new System.Collections.Generic.List<NightShift.Events.EventInstance>(d.S.Requests.Pending);
                        all.AddRange(d.S.Requests.Processed);
                        var withEmp = all.Find(x => x.Employee != null);
                        if (withEmp != null) d.SelectRequest(withEmp);
                    }
                    // ログ解析・メール差出人チェッカー用: 社外からのログの行と、偽メールを選んでおく
                    foreach (var l in d.S.Logs.Entries) if (l.Source != null && l.Source.StartsWith("203.")) { d.SelectLog(l); break; }
                    if (t[0] == Items.MailInspector) d.SelectMail(PhishingMail.Send(d.S, false));
                    if (t[0] == Items.LogAnalyzer && d.SelectedLog == null && d.S.Logs.Entries.Count > 0) d.SelectLog(d.S.Logs.Entries[d.S.Logs.Entries.Count - 1]);
                    d.RebuildMonitors();
                    var ws = d.Stations[0];
                    d.ShowMonitor(1);
                    Tools.Launch(ws, t[0]);
                    yield return Shot($"T{n:00}_{t[1]}", 1.2f);
                    n++;
                }
                // サブモニター全体（デスクトップのアイコン）
                d = NewDesktop(25, null, 30f);
                d.RebuildMonitors();
                d.ShowMonitor(1);
                yield return Shot("T00_サブモニター", 1f);
            }
            finally
            {
                CareerRecord.SetCurrentForTests(_original);
                CareerRecord.SaveDisabled = false;
            }
        }
        /// <summary>サブクエスト（SQ01〜SQ06）のメール・接続要求・怪異の演出・専用ツール・ストア・二択・エンディングを撮る。出力: Logs/Screenshots/Q*.png</summary>
        [UnityTest, Category("Screenshots"), Explicit("画面の確認用"), Timeout(900000)]
        public IEnumerator CaptureSideQuests()
        {
            _original = CareerRecord.Current;
            var originalWallet = NightShift.Systems.SideQuests.SideQuestManager.WalletProvider;
            CareerRecord.SaveDisabled = true;
            try
            {
                yield return Setup(false);
                foreach (var f in Directory.GetFiles(_dir, "Q*.png")) File.Delete(f);
                var db = _gm.Database;
                var wallet = new OperatorWallet();
                wallet.DebugAddMoney(80000);
                NightShift.Systems.SideQuests.SideQuestManager.WalletProvider = () => wallet;
                CareerRecord Fresh()
                {
                    var c = new CareerRecord { seed = 7, trust = 70, vaccineDefinitionDay = 20 };
                    c.AddItem(Items.Monitor1);
                    CareerRecord.SetCurrentForTests(c);
                    return c;
                }
                void Advance(ShiftSession s, float minutes)
                {
                    float end = s.Time.CurrentMinute + minutes;
                    for (float t = s.Time.CurrentMinute; t <= end; t += 0.5f) { s.Time.Paused = false; s.Time.JumpTo(t); s.Quests.Tick(t, 1f); }
                }
                IEnumerator Mail(ShiftDesktop dd, string id, string name)
                {
                    var m = dd.S.Mail.Find(id);
                    if (m == null) { Debug.LogWarning("メールがありません: " + id); yield break; }
                    MailboxView.Open(dd, null, m);
                    yield return Shot(name, 1f);
                }

                // ================= メール =================
                Fresh();
                var d = NewDesktop(20, null, 10f);
                var q = d.S.Quests;
                q.DebugOffer("SQ01");
                yield return Mail(d, "SQ01-" + q.Route<NightShift.Systems.SideQuests.Sq01Route>().CurrentMailId, "Q01_メール_SQ01依頼");

                Fresh();
                d = NewDesktop(20, null, 10f); q = d.S.Quests;
                q.DebugOffer("SQ02");
                yield return Mail(d, "SQ02-INVITE", "Q02_メール_SQ02相談");

                Fresh();
                d = NewDesktop(20, null, 10f); q = d.S.Quests;
                q.DebugOffer("SQ06");
                yield return Mail(d, "SQ06-INVITE", "Q03_メール_SQ06予告");

                Fresh();
                d = NewDesktop(20, null, 10f); q = d.S.Quests;
                q.DebugOffer("SQ04");
                yield return Mail(d, "SQ04-INVITE", "Q04_メール_SQ04招待");

                Fresh();
                d = NewDesktop(20, null, 10f); q = d.S.Quests;
                q.DebugOffer("SQ05");
                yield return Mail(d, "SQ05-INVITE", "Q05_メール_SQ05案内");

                // チェーンメール風に飾ったギャンブル・FXのメール
                Fresh();
                d = NewDesktop(20, null, 10f); q = d.S.Quests;
                q.DebugOffer("SQ04");
                var inv4 = d.S.Mail.Find("SQ04-INVITE");
                d.S.Mail.SendReply(inv4, 0, inv4.Replies[0]);
                Advance(d.S, 2);
                yield return Mail(d, "SQ04-GUIDE", "Q06_メール_SQ04手引き");
                var r4 = q.Route<NightShift.Systems.SideQuests.Sq04Route>();
                r4.LoanOffer();
                Advance(d.S, 3);
                yield return Mail(d, "SQ04-LOAN", "Q07_メール_追加資金");
                CareerRecord.Current.leisure.gamblePlays = 5;
                r4.OnBetResolved(true);
                Advance(d.S, 2);
                yield return Mail(d, "SQ04-VIP", "Q08_メール_VIP席");
                var fxCareer = Fresh();
                fxCareer.AddItem(Items.FxTerminal);
                d = NewDesktop(20, null, 10f); q = d.S.Quests;
                q.DebugOffer("SQ05");
                q.Route<NightShift.Systems.SideQuests.Sq05Route>().OnShiftStart();
                yield return Mail(d, "SQ05-OUTLOOK-20", "Q09_メール_SQ05今夜の見通し");

                // ================= SQ03 在籍確認 =================
                var career = Fresh();
                d = NewDesktop(20, null, 10f); q = d.S.Quests;
                q.DebugOffer("SQ03");
                yield return Mail(d, "SQ03-OFFER", "Q10_SQ03_最初のメール（返信の選択）");
                var offer = d.S.Mail.Find("SQ03-OFFER");
                d.S.Mail.SendReply(offer, 0, offer.Replies[0]);
                Advance(d.S, 2.5f);
                var seen = d.S.Mail.Find("SQ03-H1-SEEN");
                if (seen != null) d.S.Quests.OnMailAction(seen, "VERIFY");
                Advance(d.S, 3.5f);
                yield return Mail(d, "SQ03-VERIFY-REPLY", "Q10b_SQ03_総務からの照会の返事");
                Advance(d.S, 1);
                yield return Mail(d, "SQ03-H2-REMOTE", "Q11_SQ03_リモート依頼");
                var r3 = q.Route<NightShift.Systems.SideQuests.Sq03Route>();
                HorrorRemoteView.Open(d, r3);
                yield return Shot("Q12_SQ03_リモート画面（社員証）", 1.2f);
                foreach (var b in d.GetComponentsInChildren<RetroButton>()) if (b.Label.text == "閉じる") { b.Button.onClick.Invoke(); break; }
                yield return Shot("Q13_SQ03_応答なし（強制終了）", 0.6f);
                foreach (var b in d.GetComponentsInChildren<RetroButton>()) if (b.Label.text == "強制終了") { b.Button.onClick.Invoke(); break; }
                yield return Shot("Q14_SQ03_接近（FX-H02）", 0.5f);
                yield return Shot("Q15_SQ03_強制終了の後（元の送信時刻）", 2.5f);
                var remote = d.Windows.Find(HorrorRemoteView.Id);
                if (remote != null) remote.Close();
                d.S.Quests.RequestUi("FX", "DESK");
                yield return Shot("Q16_SQ03_切断の後の映像（FX-H03）", 1.6f);
                // 全面の演出（時計が止まる）が終わってから、時刻を進める
                for (float until = Time.realtimeSinceStartup + 2.5f; Time.realtimeSinceStartup < until;) yield return null;
                d.S.Time.Paused = false;
                Advance(d.S, 25);
                yield return Shot("Q17_SQ03_対応記録", 1f);
                yield return Mail(d, "SQ03-H3-A", "Q18_SQ03_手がかりA（固定表示）");
                yield return Mail(d, "SQ03-H3-ANSWER", "Q19_SQ03_解読の返信欄");
                var atm = d.S.Mail.Inbox.FirstOrDefault(m => m.MailId.StartsWith("SQ03-ATM"));
                if (atm != null) yield return Mail(d, atm.MailId, "Q20_SQ03_文字化けしたメール");
                ConnectionMonitorView.Open(d, d.S.Requests.Pending.FirstOrDefault(e => e.QuestTag == "SQ03-PERSIST"));
                yield return Shot("Q21_SQ03_永続接続の要求", 1f);
                yield return Mail(d, "SQ03-H3-TRUE", "Q22_SQ03_担当変更");
                var answer = d.S.Mail.Find("SQ03-H3-ANSWER");
                if (answer != null) d.S.Mail.SendReply(answer, 0, "ここにはいない");
                yield return Mail(d, "SQ03-RETURNED", "Q23b_SQ03_配送エラー（GOOD）");
                d.S.Quests.RequestUi("CONFIRM_TRUE", r3);
                yield return Shot("Q23_SQ03_担当変更の確認", 0.8f);

                // 演出（赤い画面・走査線のずれ・残る窓）
                Fresh();
                d = NewDesktop(20, null, 30f);
                ConnectionMonitorView.Open(d, null);
                d.S.Quests.RequestUi("FX", "RED");
                yield return Shot("Q24_SQ03_赤い画面（FX-H06）", 2.0f);
                Fresh();
                d = NewDesktop(20, null, 30f);
                ConnectionMonitorView.Open(d, null);
                d.S.Quests.RequestUi("FX", "SCAN");
                d.S.Quests.RequestUi("FX", "FRAME");
                yield return Shot("Q25_SQ03_走査線と残る窓（FX-H07・H09）", 0.8f);

                // 機器保護札（ワークステーション）
                career = Fresh();
                career.AddItem(Items.Charm, 2);
                d = NewDesktop(20, null, 5f);
                d.RebuildMonitors();
                d.ShowMonitor(1);
                yield return Shot("Q26_SQ03_機器保護札（サブモニター）", 1f);

                // ================= SQ01 / SQ02 / SQ06 の対象要求・二択 =================
                Fresh();
                d = NewDesktop(20, null, 10f); q = d.S.Quests;
                q.DebugOffer("SQ06");
                var inv = d.S.Mail.Find("SQ06-INVITE");
                var target = q.SpawnRequest(new NightShift.Systems.SideQuests.SideQuestManager.RequestSpec
                {
                    Tag = "SQ06-SHOT", MailId = "SQ06-INVITE", Employee = d.S.Employees.All.First(e => e.IsActive && e.onShift),
                    SourceIp = NightShift.Systems.SideQuests.Sq06Route.SourceIp, NodeId = "ADMIN-SRV01", AccessType = "リモート管理",
                    Malicious = true, Note = "全社停止を含む処理 / 許可後3分 / 完了後は取り消せません",
                    Confirm = "全社サービス停止を含む処理を許可します。"
                });
                ConnectionMonitorView.Open(d, target);
                yield return Shot("Q30_SQ06_対象の接続要求（依頼メールを確認）", 1f);
                q.RequestUi("CHOICE", new NightShift.Systems.SideQuests.MorningChoice
                {
                    QuestId = "SQ06", Key = "JOIN", Title = "NULL SIGNAL / 最終回答", Accept = "集団へ加わる", Decline = "加入せず離脱する",
                    Text = "全社停止は完了しました。被害とあなたの許可記録は残ります。\n加入・離脱のどちらを選んでも現在の周回を終了します。"
                });
                yield return Shot("Q31_SQ06_加入か離脱かの二択", 1.5f);

                // ================= ツール =================
                career = Fresh();
                foreach (var id in new[] { Items.GambleTerminal, Items.FxTerminal, Items.SignalDesk }) career.AddItem(id);
                d = NewDesktop(20, null, 60f); q = d.S.Quests;
                q.Leisure.Deposit(20000);
                q.Leisure.PlaceBet(1000);
                d.RebuildMonitors();
                d.ShowMonitor(1);
                Tools.Launch(d.Stations[0], Items.GambleTerminal);
                yield return Shot("Q40_ツール_NIGHT CLUB", 1.2f);
                // 結果が出た状態（リールが止まる）
                d.S.Time.JumpTo(d.S.Time.CurrentMinute + 3f);
                q.Tick(d.S.Time.CurrentMinute, 1f);
                yield return Shot("Q40b_ツール_NIGHT CLUB（結果）", 0.6f);

                career = Fresh();
                foreach (var id in new[] { Items.GambleTerminal, Items.FxTerminal }) career.AddItem(id);
                d = NewDesktop(20, null, 90f); q = d.S.Quests;
                q.Leisure.Deposit(20000);
                q.Leisure.PlaceOrder(1, 3000, 5);
                for (int i = 0; i < 30; i++) d.S.Tick(0.5f);
                d.RebuildMonitors();
                d.ShowMonitor(1);
                Tools.Launch(d.Stations[0], Items.FxTerminal);
                yield return Shot("Q41_ツール_MIDNIGHT FX", 1.2f);

                d.ShowMonitor(0);
                LeisureAccountView.Open(d);
                yield return Shot("Q42_ツール_遊興口座（借入）", 1f);

                career = Fresh();
                career.AddItem(Items.SignalDesk);
                d = NewDesktop(20, null, 10f); q = d.S.Quests;
                q.DebugOffer("SQ06");
                d.RebuildMonitors();
                d.ShowMonitor(1);
                Tools.Launch(d.Stations[0], Items.SignalDesk);
                yield return Shot("Q43_ツール_SIGNAL DESK", 1.2f);

                // ストア（業務開始前）: 招待・案内のあとにだけ並ぶ商品
                career = Fresh();
                foreach (var flag in new[] { "SQ04_INVITED", "SQ05_INVITED", "SQ06_INVITED" }) career.SetFlag(flag);
                var st3 = career.Quest("SQ03"); st3.Mark("FX_DAY"); st3.nextTaskDay = 19;
                Clear();
                var ss = new ShiftSession(db, db.GetDay(20), 7, "night01");
                var sd = ShiftDesktop.Build(_gm.ScreenLayer, _gm, ss);
                ss.Ui = sd;
                StoreView.Open(sd);
                foreach (var b in sd.GetComponentsInChildren<FlatButton>()) if (b.Label.text == "ツール") { b.OnClick?.Invoke(); break; }
                yield return Shot("Q44_ストア_サブクエストの商品", 1f);

                // ================= エンディング =================
                Fresh();
                d = NewDesktop(20, null, 10f);
                d.S.Quests.SendMail("SQ01", "EPILOGUE-SHOT", "（後日談）", null, "【共犯の席】", NightShift.Systems.SideQuests.SubEndings.Get("SE-E01").Text);
                yield return Mail(d, "SQ01-EPILOGUE-SHOT", "Q50_後日談のメール");

                Clear();
                var choice = new NightShift.Systems.SideQuests.MorningChoice
                {
                    QuestId = "SQ04", Key = "KING", Title = "VIP招待", Accept = "夜勤を辞める", Decline = "夜勤を続ける",
                    Text = "賭けた夜: 4 夜 / ギャンブル純利益: ¥23,500 / 借入残: なし\n\n次の席が用意されています。夜勤を辞めますか？\n（辞めると、この周回はここで終わります）"
                };
                MorningChoiceScreen.Build(_gm.ScreenLayer, choice, () => { }, () => { });
                yield return Shot("Q51_朝の進路選択", 0.8f);

                // エンディングの1枚絵（本編・ゲームオーバー・サブエンド）を一覧で
                Clear();
                var sheet = UIFactory.Solid(_gm.ScreenLayer, new Color(0.08f, 0.08f, 0.1f), "ArtSheet");
                sheet.rectTransform.Stretch();
                var ids = new System.Collections.Generic.List<string> { "END1", "END2", "END3", "END4", "END6", "GO1", "GO2", "GO3", "GO4" };
                foreach (var e in NightShift.Systems.SideQuests.SubEndings.All) ids.Add(e.Id);
                for (int i = 0; i < ids.Count; i++)
                {
                    int cx = i % 5, cy = i / 5;
                    var img = UIFactory.Image(sheet.transform, EndingArt.Get(ids[i]), Color.white, ids[i]);
                    img.rectTransform.Place(14 + cx * 252, 8 + cy * 158, 240, 150);
                    UIFactory.Label(sheet.transform, ids[i], 14 + cx * 252, 8 + cy * 158 + 134, 240, 16, 11, TextAnchor.MiddleLeft, Color.white, true);
                }
                yield return Shot("Q60_エンディング絵_一覧", 0.8f);
                _gm.DebugPreview("GO2");
                yield return Shot("Q61_ゲームオーバー（絵入り）", 0.8f);
                _gm.DebugPreview("END6");
                yield return Shot("Q62_本編エンド（絵入りの新聞）", 1.0f);
                CareerRecord.SetCurrentForTests(new CareerRecord { seed = 7 });

                // 横長の画面（16:9）でも、ログイン画面・本編エンドが中央に来るか
                IEnumerator WideShot(string name, System.Action build)
                {
                    var wide = new RenderTexture(1600, 900, 24);
                    _cam.targetTexture = wide;
                    yield return null;
                    build();
                    for (int i = 0; i < 6; i++) yield return null;
                    Canvas.ForceUpdateCanvases();
                    _cam.Render();
                    var prevRt = RenderTexture.active;
                    RenderTexture.active = wide;
                    var tex = new Texture2D(1600, 900, TextureFormat.RGB24, false);
                    tex.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0);
                    tex.Apply();
                    RenderTexture.active = prevRt;
                    File.WriteAllBytes(Path.Combine(_dir, name + ".png"), tex.EncodeToPNG());
                    UnityEngine.Object.Destroy(tex);
                    _cam.targetTexture = _rt;
                    wide.Release();
                    yield return null;
                }
                yield return WideShot("Q65_横長画面_ログイン", () => { Clear(); _gm.ShowLogin(); });
                {
                    // 評価画面（業務記録を日本語の要点だけに）
                    Clear();
                    var es = new ShiftSession(db, db.GetDay(20), 7, "night01");
                    var ev = es.Evaluation;
                    ev.CorrectAllow = 31; ev.CorrectBlock = 12; ev.MaliciousAllow = 1; ev.ExpiredLegit = 2;
                    ev.HelpDeskCorrect = 3; ev.RemoteResolved = 1; ev.SecurityActionsCorrect = 2;
                    var er = ev.Compute();
                    var eslip = Payroll.Compute(20, er, ev, db.payroll);
                    EvaluationScreen.Build(_gm.ScreenLayer, db.GetDay(20), er, eslip, null, true, 52300, () => { }, () => { }, "");
                    yield return Shot("Q69_評価画面（業務記録）", 9f);
                }
                Clear(); _gm.ShowTitle();
                yield return Shot("Q67_タイトル（ロゴ）", 1.0f);
                Clear(); _gm.ShowClockIn();
                yield return Shot("Q68_タイムカード（ロゴ）", 1.0f);
                yield return WideShot("Q66_横長画面_本編エンド", () => _gm.DebugPreview("END6"));
                CareerRecord.SetCurrentForTests(new CareerRecord { seed = 7 });

                // 一時停止（Esc）のメニュー: 設定とデバッグはここからだけ
                bool dbg = GameSettings.Current.debugMode;
                GameSettings.Current.debugMode = true;
                d = NewDesktop(20, null, 5f);
                yield return Shot("Q63_メインメニュー（パスワードの変更）", 0.6f);
                d.OpenPause();
                yield return Shot("Q64_一時停止メニュー（設定・デバッグ）", 0.6f);
                GameSettings.Current.debugMode = dbg;
                CareerRecord.SetCurrentForTests(new CareerRecord { seed = 7 });

                int n = 52;
                foreach (var e in NightShift.Systems.SideQuests.SubEndings.All)
                {
                    if (!e.Terminal) continue;
                    Clear();
                    CareerRecord.Current.SeeSubEnding(e.Id);
                    SubEndingScreen.Build(_gm.ScreenLayer, e.Id, "night01", "その夜からやり直す", () => { }, () => { });
                    yield return Shot($"Q{n++}_エンド_{e.Id}_{e.Title}", 0.8f);
                }
            }
            finally
            {
                NightShift.Systems.SideQuests.SideQuestManager.WalletProvider = originalWallet;
                CareerRecord.SetCurrentForTests(_original);
                CareerRecord.SaveDisabled = false;
            }
        }

        /// <summary>マニュアルの全ページを撮る（DAY20 時点で見えるページ）。出力: Logs/Screenshots/M*.png</summary>
        [UnityTest, Category("Screenshots"), Explicit("画面の確認用"), Timeout(900000)]
        public IEnumerator CaptureManuals()
        {
            _original = CareerRecord.Current;
            CareerRecord.SaveDisabled = true;
            try
            {
                yield return Setup(false);
                foreach (var f in Directory.GetFiles(_dir, "M*.png")) File.Delete(f);
                CareerRecord.SetCurrentForTests(new CareerRecord { seed = 7, trust = 70 });
                var d = NewDesktop(20, null, 1f);
                int docNo = 1;
                foreach (var doc in d.S.Day.attachments)
                {
                    if (doc == null) continue;
                    d = NewDesktop(20, null, 1f);
                    DocumentView.Open(d, doc);
                    yield return Shot($"M{docNo}_00_{doc.documentId}", 0.8f);
                    int pages = d.VisiblePages(doc).Count;
                    for (int i = 1; i < pages; i++)
                    {
                        var next = d.GetComponentsInChildren<RetroButton>().FirstOrDefault(b => b.Label.text.StartsWith("次のページ"));
                        if (next == null) break;
                        next.Button.onClick.Invoke();
                        yield return Shot($"M{docNo}_{i:00}_{doc.documentId}", 0.5f);
                    }
                    docNo++;
                }
            }
            finally
            {
                CareerRecord.SetCurrentForTests(_original);
                CareerRecord.SaveDisabled = false;
            }
        }

        /// <summary>ツールの動き（連続した画像。あとで GIF にする）: 検査中の VACCINE・考えている SOC-AI・サーバーの負荷メーター</summary>
        [UnityTest, Category("Screenshots"), Explicit("画面の確認用"), Timeout(600000)]
        public IEnumerator CaptureToolAnimations()
        {
            _original = CareerRecord.Current;
            CareerRecord.SaveDisabled = true;
            try
            {
                yield return Setup(false);
                var db = _gm.Database;
                var career = new CareerRecord { seed = 7, trust = 70, vaccineDefinitionDay = 20 };
                foreach (var it in db.storeItems) if (it != null && !it.consumable) career.AddItem(it.itemId);
                CareerRecord.SetCurrentForTests(career);

                FlatButton FindButton(string startsWith)
                {
                    foreach (var b in UnityEngine.Object.FindObjectsByType<FlatButton>(FindObjectsSortMode.None))
                        if (b.Label != null && b.Label.text.StartsWith(startsWith)) return b;
                    return null;
                }

                // VACCINE 99: 検査中（虫眼鏡が回り、ファイル名が流れ、進行状況バーが伸びる）
                var d = NewDesktop(25, null, 30f);
                d.RebuildMonitors();
                d.ShowMonitor(1);
                Tools.Launch(d.Stations[0], Items.Vaccine);
                yield return Shot("A_vaccine_00", 0.5f);
                FindButton("ワークステーション")?.OnClick?.Invoke();
                for (int i = 1; i <= 14; i++)
                {
                    d.S.Tick(0.6f);
                    yield return Shot($"A_vaccine_{i:00}", 0.12f);
                }

                // SOC-AI: 考えている間
                d = NewDesktop(25, null, 90f);
                if (d.S.Requests.Pending.Count > 0) d.SelectRequest(d.S.Requests.Pending[0]);
                d.RebuildMonitors();
                d.ShowMonitor(1);
                Tools.Launch(d.Stations[0], Items.SocAi);
                yield return Shot("A_socai_00", 0.5f);
                FindButton("選んだ要求を審査")?.OnClick?.Invoke();
                for (int i = 1; i <= 12; i++) yield return Shot($"A_socai_{i:00}", 0.12f);

                // サーバー監視: 負荷メーターとランプ
                d = NewDesktop(25, null, 30f);
                d.RebuildMonitors();
                d.ShowMonitor(1);
                Tools.Launch(d.Stations[0], Items.ServerMonitor);
                for (int i = 0; i <= 12; i++) yield return Shot($"A_server_{i:00}", i == 0 ? 0.8f : 0.12f);
            }
            finally
            {
                CareerRecord.SetCurrentForTests(_original);
                CareerRecord.SaveDisabled = false;
            }
        }
    }
}
