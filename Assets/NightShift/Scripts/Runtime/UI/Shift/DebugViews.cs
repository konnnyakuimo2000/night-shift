using System.Collections.Generic;
using System.Text;
using NightShift.Core;
using NightShift.Data;
using NightShift.Events;
using NightShift.Systems;
using UnityEngine;
using UnityEngine.UI;

namespace NightShift.UI.Shift
{
    /// <summary>
    /// 開発用デバッグパネル（F12）。時間・イベント・攻撃・演出・記録・画面の確認・日の移動。
    /// 記録や所持金を書き換える操作は、プレイヤーのセーブにも反映される（デバッグモードでのみ使える）。
    /// </summary>
    public class DebugPanelView : MonoBehaviour
    {
        public const string Id = "debug";
        ShiftDesktop _d;
        RetroWindow _w;
        Text _info;
        int _remoteCursor, _binaryCursor;
        readonly List<ThreatScenarioDefinition> _threats = new List<ThreatScenarioDefinition>();

        public static void Open(ShiftDesktop d)
        {
            var w = d.OpenWindow(new WindowSpec
            {
                Id = Id, Title = "DEBUG", Icon = "debug", Width = 600, Height = 700, Maximizable = false, StatusCells = 1,
                Position = new Vector2(660, 0)
            }, out bool created);
            if (!created) return;
            var v = w.gameObject.AddComponent<DebugPanelView>();
            v._d = d;
            v._w = w;
            v.Build(w);
        }

        void Say(string msg) => _w.SetStatus(0, msg);

        void Build(RetroWindow w)
        {
            var c = w.Content;
            var s = _d.S;
            var game = _d.Game;
            const float BW = 108, BH = 22, GX = 112;

            // ---------------- 時間 ----------------
            var tg = UIFactory.Group(c, "時間", 0, 0, 580, 54);
            float[] scales = { 0.5f, 1, 2, 5, 10, 30 };
            for (int i = 0; i < scales.Length; i++)
            {
                float sc = scales[i];
                UIFactory.Button(tg, "x" + sc, i * 46, 0, 42, BH, () => { s.Time.DebugTimeScale = sc; Say($"時間の速さ x{sc}"); });
            }
            var input = UIFactory.Input(tg, 284, 0, 70, BH, "03:45");
            UIFactory.Button(tg, "時刻へ移動", 358, 0, 90, BH, () =>
            {
                var parts = input.text.Split(':');
                if (parts.Length == 2 && int.TryParse(parts[0], out int h) && int.TryParse(parts[1], out int m)) { s.Time.JumpTo(h * 60 + m); Say($"{input.text} へ移動"); }
            });
            UIFactory.Button(tg, "+30分", 452, 0, 56, BH, () => s.Time.JumpTo(s.Time.CurrentMinute + 30));
            UIFactory.Button(tg, "終業", 512, 0, 56, BH, () => { if (s.Phase == ShiftPhase.Running) s.EndShift(); else s.Complete(); });

            // ---------------- イベント ----------------
            var eg = UIFactory.Group(c, "イベント発生", 0, 58, 580, 84);
            UIFactory.Button(eg, "通常の通信 x5", 0, 0, BW, BH, () => { for (int i = 0; i < 5; i++) s.Director.Normal.SpawnEmployeeTraffic(); });
            UIFactory.Button(eg, "接続要求", GX, 0, BW, BH, () =>
            {
                var def = s.Director.FindDefinition(x => x.kind == EventKind.ConnectionRequest && x.destinationMode == DestinationMode.FromEmployeeNormalTargets);
                if (def != null) s.Director.Spawn(def);
            });
            UIFactory.Button(eg, "セキュリティ警告", GX * 2, 0, BW, BH, () =>
            {
                var def = s.Director.FindDefinition(x => x.notifyAsSecurityAlert);
                if (def != null) s.Director.Spawn(def);
            });
            UIFactory.Button(eg, "二択の問い合わせ", GX * 3, 0, BW, BH, () => SpawnHelpDesk(HelpDeskType.Binary));
            UIFactory.Button(eg, "リモート操作", GX * 4, 0, BW, BH, () => SpawnHelpDesk(HelpDeskType.Remote));
            UIFactory.Button(eg, "偽メール(フォーム)", 0, 28, BW, BH, () => { PhishingMail.Send(s, false); Say("偽メール（再登録フォーム）を送りました"); });
            UIFactory.Button(eg, "偽メール(添付)", GX, 28, BW, BH, () => { PhishingMail.SendAttachment(s); Say("偽メール（添付ファイル）を送りました"); });
            UIFactory.Button(eg, "パスワード変更依頼", GX * 2, 28, BW, BH, () => { s.Director.SpawnPasswordTask(); Say("パスワード変更の依頼を出しました"); });
            UIFactory.Button(eg, $"DAY{s.Day.dayNumber} やり直し", GX * 3, 28, BW, BH, () => game.RestartDay());
            UIFactory.Button(eg, "オーバーレイ(F11)", GX * 4, 28, BW, BH, () => _d.ToggleOverlay());

            // ---------------- 攻撃シナリオ（全日から選べる） ----------------
            var ag = UIFactory.Group(c, "攻撃シナリオ（今夜の解禁に関係なく発生）", 0, 146, 580, 54);
            foreach (var day in game.Database.days)
                if (day != null)
                    foreach (var t in day.threats)
                        if (t != null && !_threats.Contains(t)) _threats.Add(t);
            _threats.Sort((a, b) => string.CompareOrdinal(a.scenarioId, b.scenarioId));
            var names = new List<string>();
            foreach (var t in _threats) names.Add(t.scenarioId);
            if (names.Count == 0) names.Add("（なし）");
            var threatDd = RetroDropdown.Create(ag, 0, 1, 300, names, 0);
            UIFactory.Button(ag, "発生させる", 308, 0, 100, BH, () =>
            {
                if (threatDd.Index < 0 || threatDd.Index >= _threats.Count) return;
                var def = _threats[threatDd.Index];
                s.Director.Threats.Schedule(s.Director.Scheduler, def, s.Time.CurrentMinute);
                Say($"{def.scenarioId} を発生させました");
            });

            // ---------------- 演出 ----------------
            var hg = UIFactory.Group(c, "ホラー演出", 0, 204, 580, 54);
            UIFactory.Button(hg, "ブルースクリーン", 0, 0, BW, BH, () => s.RequestHorror("BSOD"));
            UIFactory.Button(hg, "停電", GX, 0, BW, BH, () => s.RequestBlackout());
            UIFactory.Button(hg, "文字化けノイズ", GX * 2, 0, BW, BH, () => s.RequestHorror("GLITCH"));
            UIFactory.Button(hg, "オルゴール", GX * 3, 0, BW, BH, () => s.RequestHorror("MUSICBOX"));
            UIFactory.Button(hg, "もう1つのカーソル", GX * 4, 0, BW, BH, () =>
            {
                s.OperatorCompromised = !s.OperatorCompromised;
                Say(s.OperatorCompromised ? "もう1つのカーソル: 表示" : "もう1つのカーソル: 非表示");
            });

            // ---------------- 記録・お金（セーブに反映） ----------------
            var rg = UIFactory.Group(c, "30日間の記録・所持金（セーブに反映）", 0, 262, 580, 84);
            var career = CareerRecord.Current;
            UIFactory.Button(rg, "所持金 +¥10,000", 0, 0, BW, BH, () => { game.Wallet.DebugAddMoney(10000); Say($"所持金 {OperatorWallet.Yen(game.Wallet.Balance)}"); });
            UIFactory.Button(rg, "ツールを全部入手", GX, 0, BW, BH, () =>
            {
                GrantAllTools(career, game.Database, s.Day.dayNumber);
                _d.RebuildMonitors();
                Say("ツールとサブモニターを全部入手しました");
            });
            UIFactory.Button(rg, "信用 +10", GX * 2, 0, BW, BH, () => { career.AddTrust(10); career.Save(); Say($"会社の信用 {career.trust}"); });
            UIFactory.Button(rg, "信用 −10", GX * 3, 0, BW, BH, () => { career.AddTrust(-10); career.Save(); Say($"会社の信用 {career.trust}"); });
            UIFactory.Button(rg, "手がかりを全部", GX * 4, 0, BW, BH, () =>
            {
                foreach (var id in new[] { "C1", "C2", "C3", "C4" }) if (!career.clues.Contains(id)) career.clues.Add(id);
                career.Save();
                Say("前任者の手がかり 4/4");
            });
            UIFactory.Button(rg, "前夜までD×2", 0, 28, BW, BH, () =>
            {
                SetPreviousRanks(career, s.Day.dayNumber, "D", 2);
                Say("前の2夜をDにしました（次の夜は先輩の手伝い・GO1の一歩手前）");
            });
            UIFactory.Button(rg, "自分のID被害+1", GX, 28, BW, BH, () => { career.accountCompromised++; career.Save(); Say($"自分のIDの被害 {career.accountCompromised} 回"); });
            UIFactory.Button(rg, "残業5夜連続", GX * 2, 28, BW, BH, () =>
            {
                SetPreviousRanks(career, s.Day.dayNumber, null, 5, overtime: true);
                Say("前の5夜を残業ありにしました");
            });
            UIFactory.Button(rg, "記録を表示", GX * 3, 28, BW, BH, () => Say($"信用 {career.trust} / 所持金 {OperatorWallet.Yen(game.Wallet.Balance)} / 手がかり {career.clues.Count} / ID被害 {career.accountCompromised} / 記録 {career.nights.Count} 夜"));

            // ---------------- 画面の確認・日の移動 ----------------
            var pg = UIFactory.Group(c, "画面の確認・日の移動・サブクエスト", 0, 350, 580, 82);
            var previews = new List<string> { "GO1", "GO2", "GO3", "GO4", "END1", "END2", "END3", "END4", "END6" };
            foreach (var se in Systems.SideQuests.SubEndings.All) if (se.Terminal) previews.Add(se.Id);
            var previewDd = RetroDropdown.Create(pg, 0, 1, 90, previews, 0);
            UIFactory.Button(pg, "画面を表示", 96, 0, 96, BH, () => game.DebugPreview(previewDd.Value));
            var days = new List<string>();
            foreach (var day in game.Database.days) if (day != null) days.Add($"DAY{day.dayNumber}");
            var dayDd = RetroDropdown.Create(pg, 240, 1, 110, days, Mathf.Clamp(s.Day.dayNumber, 1, days.Count) - 1, null);
            UIFactory.Button(pg, "その日を最初から", 356, 0, 130, BH, () =>
            {
                if (int.TryParse(dayDd.Value.Replace("DAY", ""), out int n)) game.StartDay(n);
            });
            // サブクエスト（条件を待たずに案内を出す）
            var quests = new List<string> { "SQ03", "SQ04", "SQ05" };
            var questDd = RetroDropdown.Create(pg, 0, 29, 90, quests, 0, null);
            UIFactory.Button(pg, "案内を出す", 96, 28, 96, BH, () => { s.Quests.DebugOffer(questDd.Value); Say($"{questDd.Value} の案内を出しました"); });

            // ---------------- 情報 ----------------
            var ig = UIFactory.Group(c, "保留中の要求（正解と理由）・次のイベント", 0, 436, 580, 200);
            _info = UIFactory.Label(ig, "", 11, TextAnchor.UpperLeft);
            _info.rectTransform.Stretch();
            _info.font = RetroTheme.MonoFont;
            Say("開発用機能です。");
        }

        /// <summary>ストアの機材・ツールを全部持たせる（消耗品は除く）</summary>
        public static void GrantAllTools(CareerRecord career, GameDatabase db, int day)
        {
            foreach (var it in db.storeItems)
                if (it != null && !it.consumable && string.IsNullOrEmpty(it.questFlag) && !career.Owns(it.itemId)) career.AddItem(it.itemId);
            career.vaccineDefinitionDay = day;
            career.Save();
        }

        /// <summary>今夜より前の n 夜の記録を作る（ランク・残業）。ゲームオーバーや先輩の手伝いの確認用</summary>
        public static void SetPreviousRanks(CareerRecord career, int today, string rank, int n, bool overtime = false)
        {
            for (int d = today - n; d < today; d++)
            {
                if (d < 1) continue;
                var r = career.Night(d);
                if (r == null) { r = new NightRecord { day = d, rank = rank ?? "B" }; career.nights.Add(r); }
                if (rank != null) r.rank = rank;
                if (overtime) r.overtime = true;
            }
            career.nights.Sort((a, b) => a.day.CompareTo(b.day));
            career.Save();
        }

        void SpawnHelpDesk(HelpDeskType type)
        {
            var list = _d.S.Day.helpDesk.FindAll(h => h != null && h.type == type);
            if (list.Count == 0) list = _d.S.Day.helpDeskPool.FindAll(h => h != null && h.type == type);
            if (list.Count == 0) { Say("この日に出せる問い合わせがありません"); return; }
            int idx = type == HelpDeskType.Remote ? _remoteCursor++ : _binaryCursor++;
            _d.S.Director.HelpDeskGen.Spawn(list[idx % list.Count]);
        }

        void Update()
        {
            if (_d == null || Time.frameCount % 15 != 0) return;
            var sb = new StringBuilder();
            foreach (var e in _d.S.Requests.Pending)
                sb.AppendLine($"{e.RequestId} {e.DisplayUser,-8} → {e.DestinationLabel,-11} {e.Expected,-5} {(e.IsMalicious ? "MAL" : "")} {e.ExpectedReason}");
            var next = _d.S.Director.Scheduler.NextPending();
            sb.AppendLine();
            sb.AppendLine(next != null ? $"次のイベント: {TimeManager.Format(next.Time)} {next.Label}" : "次のイベント: なし");
            foreach (var run in _d.S.Director.Threats.Runs)
                sb.AppendLine($"攻撃: {run.Definition.scenarioId}  開始 {TimeManager.Format(run.StartTime)}  被害者 {(run.Victim != null ? run.Victim.deviceId : "-")}  {(run.Compromised ? "成功" : "")}");
            _info.text = sb.ToString();
        }
    }

    /// <summary>デバッグ用オーバーレイ（F11）</summary>
    public class DebugOverlay : MonoBehaviour
    {
        ShiftDesktop _d;
        Text _text;
        bool _visible;

        public static DebugOverlay Build(RectTransform parent, ShiftDesktop d)
        {
            var bg = UIFactory.Solid(parent, new Color(0, 0, 0, 0.7f), "DebugOverlay");
            bg.raycastTarget = false;
            bg.rectTransform.Place(4, 4, 330, 120);
            var o = bg.gameObject.AddComponent<DebugOverlay>();
            o._d = d;
            o._text = UIFactory.Label(bg.transform, "", 11, TextAnchor.UpperLeft, new Color(0.6f, 1f, 0.6f), true);
            o._text.rectTransform.Stretch(6, 4, 6, 4);
            bg.gameObject.SetActive(false);
            return o;
        }

        public void Toggle()
        {
            _visible = !_visible;
            gameObject.SetActive(_visible);
        }

        public void Tick()
        {
            if (!_visible || _d == null) return;
            var s = _d.S;
            var session = s.HelpDesk.ActiveSession;
            _text.text =
                $"Current Time    : {TimeManager.FormatWithSeconds(s.Time.CurrentMinute)}  x{s.Time.DebugTimeScale}{(s.Time.Paused ? " PAUSED" : "")}\n" +
                $"Phase           : {s.Phase}\n" +
                $"Active Events   : {s.Director.Scheduler.PendingCount} scheduled\n" +
                $"Pending Requests: {s.Requests.Pending.Count}\n" +
                $"Active Tickets  : {s.HelpDesk.OpenCount}\n" +
                $"Remote Session  : {(session != null ? session.PC.Hostname + " (" + session.Ticket.TicketId + ")" : "-")}\n" +
                $"Seed            : {s.Seed}";
        }
    }
}
