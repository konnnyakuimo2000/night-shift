using System;
using System.Collections.Generic;
using NightShift.Core;
using NightShift.Data;
using NightShift.Events;

namespace NightShift.Systems.SideQuests
{
    /// <summary>
    /// メール起点のサブクエスト（SUBQUEST_SPEC.md / GAMBLING_FX_SPEC.md / HACKER_ROUTE_SPEC.md）を動かす。1夜に1つ作る。
    /// - 案内の予約: 1夜の新しい案内は1本、進行中は最大2本。初回案内は DAY23 まで、最終作業は DAY29 まで
    /// - メール: 夜をまたいで受信トレイに残し、返信・既読・本文の表示・報告を記録する
    /// - 操作の判定: 接続要求の許可／拒否、端末の隔離、接続元の遮断、報告を、対象の要求・端末ごとに照合する（手動の操作だけを意図的な行動として数える）
    /// - 取得・転送などの進み具合（ゲーム内の分）。隔離・遮断で止まる
    /// - 個別入金（給与とは別。夜の終わりにまとめて入金）、サブエンド（後日談はメール、途中終了は終端画面）
    /// どれもゲーム内の架空のデータで、実際の通信・ファイルには触れない。
    /// </summary>
    public class SideQuestManager
    {
        public const int FirstOfferDayLimit = 23, LastTaskDay = 29, MaxOpen = 2;

        /// <summary>財布（GameManager が設定する。テストでは差し替える）</summary>
        public static Func<OperatorWallet> WalletProvider;
        public OperatorWallet Wallet => WalletProvider?.Invoke();

        public ShiftSession S { get; }
        public int Day => S.Day.dayNumber;
        public CareerRecord Career => CareerRecord.Current;
        public readonly List<QuestRoute> Routes = new List<QuestRoute>();
        public LeisureAccount Leisure { get; }

        /// <summary>途中終了型のサブエンドが決まった（業務中。画面側が終端画面を出す）</summary>
        public event Action<string> TerminalEnding;
        /// <summary>夜の終わりに決まった途中終了（翌朝の前に終端画面を出す）</summary>
        public string PendingTerminal { get; private set; }
        /// <summary>周回が終わった（終端が決まった）。以後の処理はしない</summary>
        public bool Ended { get; private set; }
        /// <summary>画面への要求（kind, 引数）: REMOTE（怪異のリモート対応）・RECORD（対応記録）・CHOICE（その場の選択）・FX（怪異の演出）・LOAN（借入）</summary>
        public event Action<string, object> UiRequested;
        public event Action Changed;

        readonly Dictionary<string, QuestMailRecord> _records = new Dictionary<string, QuestMailRecord>();
        readonly List<QuestJob> _jobs = new List<QuestJob>();
        readonly List<(float time, string label, Action action)> _timers = new List<(float, string, Action)>();
        readonly List<(string tx, int amount, string label, string source)> _pay = new List<(string, int, string, string)>();
        public readonly List<string> Summary = new List<string>();   // 今夜のサブクエストの結果（評価画面に出す）
        bool _offeredTonight;

        public SideQuestManager(ShiftSession s)
        {
            S = s;
            Leisure = new LeisureAccount(this);
            Routes.Add(new Sq04Route(this));
            Routes.Add(new Sq03Route(this));
            Routes.Add(new Sq05Route(this));
            Routes.Add(new Sq01Route(this));
            Routes.Add(new Sq02Route(this));
            Routes.Add(new Sq06Route(this));
            // これまでのサブクエストのメールを受信トレイに戻す
            foreach (var r in Career.questMails)
                if (r.day < Day || (r.day == Day && r.time < 0)) AddToInbox(r, notify: false);
            S.Mail.Replied += OnReplied;
            S.Requests.Resolved += OnResolved;
            S.Security.DeviceIsolated += d => { foreach (var q in Routes) if (q.State.IsOpen) q.OnIsolated(d); CheckJobs(); };
            S.Security.SourceBlocked += t => { foreach (var q in Routes) if (q.State.IsOpen) q.OnBlocked(t); CheckJobs(); };
            S.Security.QuestTargetCheck = x => { foreach (var q in Routes) if (q.State.IsOpen && q.IsTarget(x)) return true; return false; };
            S.ShiftEnded += () => { if (Ended) return; Leisure.OnShiftEnd(); foreach (var q in Routes) if (q.State.IsOpen) q.OnShiftEnd(); };
        }

        public QuestRoute Route(string id) => Routes.Find(r => r.Id == id);
        public T Route<T>() where T : QuestRoute => Routes.Find(r => r is T) as T;
        public bool IsMain => Day >= DefaultDataFactory.MainStartDay;

        // ================= 夜の始まり =================
        /// <summary>業務開始時（攻撃などの予約が済んだ後）に呼ぶ</summary>
        public void OnShiftStart()
        {
            if (!IsMain || Ended) return;
            foreach (var q in Routes) if (q.State.IsOpen) q.OnShiftStart();
            TryOffer();
            Changed?.Invoke();
        }

        public int OpenCount() { int n = 0; foreach (var q in Routes) if (q.State.IsOpen) n++; return n; }

        /// <summary>新しい案内を出せるか。出せない理由（null = 出せる）</summary>
        public string OfferBlockedReason()
        {
            if (_offeredTonight) return "今夜はすでに案内が届いています";
            if (Day > FirstOfferDayLimit) return "新しい案内の受付は終わりました";
            if (OpenCount() >= MaxOpen) return "進行中のサブクエストが2本あるため、新しい案内は保留中です";
            return null;
        }

        void TryOffer()
        {
            if (OfferBlockedReason() != null) return;
            foreach (var q in Routes)
            {
                if (q.State.status != QuestStatus.Locked || Day < q.EarliestDay || !q.Ready()) continue;
                _offeredTonight = true;
                q.State.status = QuestStatus.Offered;
                q.State.offerDay = Day;
                q.Offer();
                return;
            }
        }

        /// <summary>デバッグ: 指定のサブクエストの案内を今すぐ出す</summary>
        public void DebugOffer(string id)
        {
            var q = Route(id);
            if (q == null || q.State.IsOpen) return;
            q.State.status = QuestStatus.Offered;
            q.State.offerDay = Day;
            q.Offer();
            Changed?.Invoke();
        }

        // ================= メール =================
        /// <summary>サブクエストのメールを送る。time: ゲーム内の分（-1 = 業務開始前に届いている）。nextMorning: 翌夜の業務開始前に届く</summary>
        public QuestMailRecord SendMail(string questId, string mailId, string from, string address, string subject, string body,
            float time = -1, bool nextMorning = false, string[] replies = null, bool freeReply = false, bool pinned = false, bool horror = false,
            string actionKey = null, string actionLabel = null, string sentLabel = null)
        {
            string id = $"{questId}-{mailId}";
            var existing = Career.questMails.Find(r => r.mailId == id);
            if (existing != null) return existing;   // 同じメールは二度送らない
            var rec = new QuestMailRecord
            {
                mailId = id, questId = questId, day = nextMorning ? Day + 1 : Day, time = nextMorning ? -1 : time,
                from = from, address = address, subject = subject, body = body, freeReply = freeReply, pinned = pinned, horror = horror,
                actionKey = actionKey, actionLabel = actionLabel, sentLabel = sentLabel
            };
            if (replies != null) rec.replies.AddRange(replies);
            Career.questMails.Add(rec);
            if (nextMorning) return rec;
            if (time < 0 || time <= S.Time.CurrentMinute)
            {
                if (time >= 0) rec.time = S.Time.CurrentMinute;
                AddToInbox(rec, notify: S.Phase != ShiftPhase.NotStarted);
            }
            else At(time, "QUEST MAIL " + id, () => AddToInbox(rec, notify: true));
            return rec;
        }

        void AddToInbox(QuestMailRecord r, bool notify)
        {
            if (S.Mail.Find(r.mailId) != null) return;
            var dayDef = S.Database.FindDay(r.day);
            var m = new MailMessage
            {
                MailId = r.mailId, QuestId = r.questId, From = r.from, FromAddress = r.address,
                To = MailManager.Recipient(S.OperatorName), Subject = r.subject, Body = r.body,
                DateLabel = $"{(dayDef != null ? dayDef.dateLabel : S.Day.dateLabel)} {(r.time >= 0 ? TimeManager.Format(r.time) : "21:00")}",
                Day = r.day, Read = r.read, Reported = r.reported,
                Replies = r.replies.Count > 0 ? new List<string>(r.replies) : null, FreeReply = r.freeReply,
                RepliedIndex = r.repliedIndex, RepliedText = r.repliedText, Horror = r.horror, Pinned = r.pinned,
                ActionKey = r.actionKey, ActionLabel = r.actionLabel, ReceivedAt = r.time
            };
            _records[r.mailId] = r;
            S.Mail.Add(m, notify);
            if (notify)
                S.Notifications.Notify(NotificationType.Info, "新着メール", $"{r.from}: {r.subject}", S.Time.CurrentMinute, () => S.Ui?.OpenMailbox(m));
        }

        public QuestMailRecord Record(MailMessage m) => m != null && m.MailId != null && _records.TryGetValue(m.MailId, out var r) ? r : null;
        public QuestMailRecord Record(string questId, string mailId) => Career.questMails.Find(r => r.mailId == questId + "-" + mailId);
        public MailMessage Message(string questId, string mailId) => S.Mail.Find(questId + "-" + mailId);

        /// <summary>メールの本文を開いて表示した（一覧の既読とは別。自動プレビューではなく、選んで読んだ）</summary>
        public void OnMailShown(MailMessage m)
        {
            var r = Record(m);
            if (r == null) return;
            r.read = true;
            if (!r.viewed) { r.viewed = true; Route(r.questId)?.OnViewed(r); }
        }

        void OnReplied(MailMessage m, int index, string text)
        {
            var r = Record(m);
            if (r == null || Ended) return;
            r.repliedIndex = index;
            r.repliedText = text;
            Route(r.questId)?.OnReply(r, index, text);
            Changed?.Invoke();
        }

        /// <summary>サブクエストのメールを報告した。戻り値: 報告として正しいか（null = 誤報にも正報にもしない）</summary>
        public bool? OnMailReported(MailMessage m)
        {
            var r = Record(m);
            if (r == null) return null;
            r.reported = true;
            var res = Ended ? null : Route(r.questId)?.OnReported(r);
            Changed?.Invoke();
            return res;
        }

        /// <summary>メール内のボタン（サブクエストの操作）</summary>
        public void OnMailAction(MailMessage m, string key)
        {
            var r = Record(m);
            if (r == null || Ended) return;
            Route(r.questId)?.OnAction(r, key);
            Changed?.Invoke();
        }

        public MorningChoice PendingChoice { get; private set; }
        bool _pausedBeforeChoice;
        public void RequestUi(string kind, object arg = null)
        {
            if (kind == "CHOICE" && arg is MorningChoice choice)
            {
                if (Ended || PendingChoice != null || EndingResolver.CheckGameOver(Career, S) != null) return;
                PendingChoice = choice;
                _pausedBeforeChoice = S.Time.Paused;
                S.Time.Paused = true;
            }
            UiRequested?.Invoke(kind, arg);
        }

        // ================= 接続要求 =================
        public class RequestSpec
        {
            public string Tag, MailId, Note, Confirm;
            public EmployeeData Employee;
            public string DisplayUser, DisplayEmployeeId, Device, SourceIp, NodeId, AccessType;
            public NetworkNodeDefinition Destination;   // ゲーム内だけの架空の転送先
            public SourceZone Zone = SourceZone.Internal;
            public ExpectedAction Expected = ExpectedAction.PolicyDerived;
            public bool Malicious, SecurityAlert;
            public float Timeout = 10f;
        }

        /// <summary>サブクエストの対象要求を出す（自動審査・AIの自動処理の対象外）</summary>
        public EventInstance SpawnRequest(RequestSpec r)
        {
            if (Ended || S.Phase != ShiftPhase.Running) return null;
            var def = UnityEngine.ScriptableObject.CreateInstance<EventDefinition>();
            def.eventId = r.Tag;
            def.kind = EventKind.ConnectionRequest;
            def.expectedAction = r.Expected;
            def.isMalicious = r.Malicious;
            def.timeoutMinutes = r.Timeout;
            def.hideFlags = UnityEngine.HideFlags.DontSave;
            var node = r.Destination ?? S.Database.nodes.Find(n => n != null && n.nodeId == r.NodeId);
            def.destination = node;
            def.accessType = r.AccessType;
            var e = new EventInstance
            {
                Definition = def, Category = EventCategory.Normal, Kind = EventKind.ConnectionRequest, IsMalicious = r.Malicious,
                NotifyAsSecurityAlert = r.SecurityAlert, Employee = r.Employee,
                DisplayUser = r.DisplayUser ?? r.Employee?.fullName ?? "UNKNOWN",
                DisplayEmployeeId = r.DisplayEmployeeId ?? r.Employee?.employeeId ?? "-",
                Device = r.Device ?? r.Employee?.deviceId,
                SourceIp = r.SourceIp ?? (r.Employee != null ? S.Employees.GetIp(r.Employee) : "10.10.99.50"),
                Zone = r.Zone, Destination = node, AccessType = r.AccessType, Note = r.Note,
                QuestTag = r.Tag, QuestMailId = r.MailId, QuestConfirm = r.Confirm
            };
            e.ServiceId = node?.FindAccess(r.AccessType)?.serviceId;
            if (e.Employee != null) e.ExpectedDeviceIp = S.Employees.GetIp(e.Employee);
            S.Requests.Submit(e);
            return e;
        }

        void OnResolved(EventInstance e)
        {
            if (e?.QuestTag == null || Ended) return;
            string id = e.QuestTag.Split('-')[0];
            Route(id)?.OnRequestResolved(e);
            Changed?.Invoke();
        }

        /// <summary>手動で許可した（自動審査・遮断リストなどの自動処理でない）</summary>
        public static bool ManualAllow(EventInstance e) => e.State == EventState.Allowed && !e.AutoDecided;
        public static bool ManualBlock(EventInstance e) => e.State == EventState.Blocked && !e.AutoDecided;

        // ================= 予定・取得や転送の進み具合 =================
        /// <summary>ゲーム内時刻に処理を予約する（一時停止・全面演出中は進まない）</summary>
        public void At(float time, string label, Action action) => _timers.Add((time, label, action));

        /// <summary>時間制限付きの作業は一夜一本。確定した日だけを予告に使う。</summary>
        public int ReserveTaskDay(QuestRoute route, int first, int last = LastTaskDay)
        {
            for (int day = first; day <= Math.Min(last, LastTaskDay); day++)
            {
                if (Career.leisure.spikeDay == day) continue;
                if (TaskDayReserved(day, route)) continue;
                route.State.nextTaskDay = day;
                return day;
            }
            route.State.nextTaskDay = 0;
            return 0;
        }

        public bool TaskDayReserved(int day, QuestRoute except = null) => Routes.Exists(r => r != except && r.State.IsOpen && r.State.nextTaskDay == day);

        /// <summary>固定の脅威は残し、ランダムな通常要求の枠を置き換える。</summary>
        public void ReplaceRoutineSlots(int count)
        {
            foreach (var item in S.Director.Scheduler.Items)
            {
                if (count <= 0) break;
                if (item.Fired || item.Time < S.Time.CurrentMinute || !item.Label.StartsWith("ROUTINE ")) continue;
                item.Fired = true;
                count--;
            }
        }

        public void CancelWork(string routeId)
        {
            string prefix = routeId + "-";
            _timers.RemoveAll(t => t.label.StartsWith(prefix));
            foreach (var j in _jobs) if (j.Id.StartsWith(prefix)) j.Finished = true;
        }

        public class QuestJob
        {
            public string Id, Label, Device, SourceIp, Target;
            public EventInstance Request;
            public float Start, Minutes;
            public Action Done, Aborted;
            public bool Finished, Succeeded;
            public int LastStep;
            public float Progress(float now) => Math.Min(1f, (now - Start) / Math.Max(0.01f, Minutes));
        }

        public IReadOnlyList<QuestJob> Jobs => _jobs;

        public QuestJob StartJob(string id, string label, string device, string sourceIp, string target, float minutes, Action done, Action aborted)
        {
            var j = new QuestJob { Id = id, Label = label, Device = device, SourceIp = sourceIp, Target = target, Start = S.Time.CurrentMinute, Minutes = minutes, Done = done, Aborted = aborted };
            _jobs.Add(j);
            S.Logs.Add(j.Start, sourceIp, target, "-", "TRANSFER", "START", $"{label}（0%）");
            return j;
        }

        public QuestJob Job(string id) => _jobs.Find(j => j.Id == id);

        /// <summary>サブクエストでプレイヤーが今していること（解読の期限・結果待ちの賭け・建玉）</summary>
        public bool HasWork =>
            !Ended && (Route<Sq03Route>()?.DeadlineRunning == true || Leisure.PendingBet != null || Leisure.Open != null);
        public bool JobRunning(string id) => _jobs.Exists(j => j.Id == id && !j.Finished);

        bool Stopped(QuestJob j) => S.Security.IsIsolated(j.Device) || S.Security.IsBlocked(j.SourceIp) || (j.Target != null && S.Security.IsBlocked(j.Target));

        public void Tick(float now, float realDelta)
        {
            if (Ended || S.Time.Paused) return;
            for (int i = 0; i < _timers.Count; i++)
            {
                if (now < _timers[i].time) continue;
                var t = _timers[i];
                _timers.RemoveAt(i--);
                try { t.action(); } catch (Exception ex) { UnityEngine.Debug.LogException(ex); }
                if (Ended || S.Time.Paused) return;
            }
            foreach (var j in _jobs.ToArray())
            {
                if (j.Finished) continue;
                if (Stopped(j)) { AbortJob(j, "切断により中断"); continue; }
                float k = j.Progress(now);
                int step = (int)Math.Floor(k * 3);
                if (step != j.LastStep && step > 0 && step < 3)
                    S.Logs.Add(now, j.SourceIp, j.Target, "-", "TRANSFER", "PROGRESS", $"{j.Label}（{step * 33}%）");
                j.LastStep = step;
                if (k >= 1f)
                {
                    j.Finished = true; j.Succeeded = true;
                    S.Logs.Add(now, j.SourceIp, j.Target, "-", "TRANSFER", "DONE", $"{j.Label}（100%）");
                    j.Done?.Invoke();
                    Changed?.Invoke();
                    if (Ended || S.Time.Paused) return;
                }
            }
            Leisure.Tick(now);
            foreach (var q in Routes) if (q.State.IsOpen && !Ended) q.Tick(now, realDelta);
        }

        void AbortJob(QuestJob j, string why)
        {
            j.Finished = true;
            S.Logs.Add(S.Time.CurrentMinute, j.SourceIp, j.Target, "-", "TRANSFER", "ABORTED", $"{j.Label}（{why}）");
            j.Aborted?.Invoke();
            Changed?.Invoke();
        }

        /// <summary>隔離・遮断した直後に、止まった進行を確定する（次のフレームを待たない）</summary>
        public void CheckJobs()
        {
            foreach (var j in _jobs.ToArray()) if (!j.Finished && Stopped(j)) AbortJob(j, "切断により中断");
        }

        // ================= 入金・結末 =================
        /// <summary>個別入金（夜の終わりにまとめて入金。txId で一度だけ）</summary>
        public void Pay(string txId, int amount, string label, string source)
        {
            if (_pay.Exists(p => p.tx == txId) || Career.doneTx.Contains(txId)) return;
            _pay.Add((txId, amount, label, source));
            Note($"個別入金（翌朝）: {OperatorWallet.Yen(amount)}（{label}）");
        }

        /// <summary>個別入金を今すぐ記録する（途中終了の直前など、朝を迎えない場合）</summary>
        public void PayNow(string txId, int amount, string label, string source)
        {
            if (!Career.TryOnce(txId)) return;
            Wallet?.AddEntry(txId, Day, source, label, amount);
            Note($"個別入金: {OperatorWallet.Yen(amount)}（{label}）");
        }

        public int PendingPay(string source) { int s = 0; foreach (var p in _pay) if (p.source == source) s += p.amount; return s; }

        public void Note(string line) { if (!Summary.Contains(line)) Summary.Add(line); }

        /// <summary>結末なしで決着する（辞退・放置など）</summary>
        public void Close(QuestRoute q, string result, string note = null)
        {
            var st = q.State;
            if (st.status == QuestStatus.Resolved) return;
            st.status = QuestStatus.Resolved;
            st.result = result;
            CancelWork(q.Id);
            if (note != null) Note($"{q.Title}: {note}");
            q.OnClosed();
            Changed?.Invoke();
        }

        /// <summary>サブエンドを決める。後日談型は翌朝のメール、途中終了型はその場で終端（atNightEnd なら翌朝の前）</summary>
        public void Resolve(QuestRoute q, string result, string endingId, bool atNightEnd = false)
        {
            var st = q.State;
            if (st.status == QuestStatus.Resolved || Ended) return;
            var def = SubEndings.Get(endingId);
            if (def != null && def.Terminal && EndingResolver.CheckGameOver(Career, S) != null) return;
            if (def != null && def.Terminal && atNightEnd) { PendingTerminal = endingId; _pendingRoute = q; _pendingResult = result; return; }
            st.status = QuestStatus.Resolved;
            st.result = result;
            st.endingId = endingId;
            CancelWork(q.Id);
            q.OnClosed();
            if (!Career.TryOnce("END-" + q.Id)) return;
            Career.SeeSubEnding(endingId);
            Note($"{q.Title}: サブエンド「{def?.Title ?? endingId}」");
            if (def != null && def.Terminal)
            {
                Ended = true;
                foreach (var j in _jobs) j.Finished = true;
                _timers.Clear();
                TerminalEnding?.Invoke(endingId);
            }
            else if (def != null)
                SendMail(q.Id, "EPILOGUE", "（後日談）", null, $"【{def.Title}】", def.Text, nextMorning: true);
            Changed?.Invoke();
        }

        QuestRoute _pendingRoute;
        string _pendingResult;

        /// <summary>夜の終わりに決まった途中終了を確定する（翌朝の前、終端画面を出す直前）</summary>
        public string ConfirmPendingTerminal()
        {
            if (PendingTerminal == null || _pendingRoute == null) return null;
            string id = PendingTerminal;
            PendingTerminal = null;
            Resolve(_pendingRoute, _pendingResult, id);
            return id;
        }

        /// <summary>夜の終わり（評価の確定時）。入金・段階の進行・翌夜の予約・終了処理</summary>
        public void OnNightEnd()
        {
            if (!IsMain || Ended) return;
            foreach (var q in Routes)
            {
                var st = q.State;
                if (!st.IsOpen) continue;
                q.OnNightEnd();
                // 案内に反応しないまま2夜 → 静かに終了
                if (st.status == QuestStatus.Offered && !q.Touched())
                {
                    st.ignoredNights++;
                    if (st.ignoredNights >= 2) Close(q, QuestResult.Abandoned);
                }
                if (st.IsOpen && Day >= LastTaskDay) q.OnFinalDay();
            }
            var wallet = Wallet;
            foreach (var p in _pay)
            {
                if (!Career.TryOnce(p.tx)) continue;
                wallet?.AddEntry(p.tx, Day, p.source, p.label, p.amount);
            }
            _pay.Clear();
            // 遊興口座: 返済日の朝（給与と入金の後）に返済。足りなければ破産
            Leisure.OnNightEnd();
            CharmNightEnd();
            Career.Save();
        }

        /// <summary>貼った札は、効果が発動したかにかかわらず翌朝に破れる（一度だけ）</summary>
        void CharmNightEnd()
        {
            var ch = Career.charm;
            if (ch.postedDay == Day && ch.brokenDay != Day)
            {
                ch.brokenDay = Day;
                ch.torn = true;
                ch.postedDay = 0;
                ch.consumed = false;
                Note("モニターの側面の札は、朝には破れていた。");
            }
            ch.boughtToday = 0;
        }

        /// <summary>朝の進路選択（ギャンブル王・FX戦士・転職など）。無ければ null</summary>
        public MorningChoice PendingMorningChoice()
        {
            if (Ended || PendingTerminal != null) return null;
            foreach (var q in Routes) if (q.State.IsOpen) { var c = q.MorningChoice(); if (c != null) return c; }
            return null;
        }

        /// <summary>朝の進路を選んだ。途中終了になったら、その結末 ID</summary>
        public string ChooseMorning(MorningChoice c, bool accept)
        {
            if (c == null) return null;
            var q = Route(c.QuestId);
            if (q == null || Ended) return null;
            q.OnMorningChoice(c, accept);
            if (PendingChoice == c && q.State.status == QuestStatus.Resolved)
            {
                PendingChoice = null;
                if (!Ended) S.Time.Paused = _pausedBeforeChoice;
            }
            Career.Save();
            return Ended ? q.State.endingId : null;
        }

        // ================= 怪異の演出（機器保護札・頻度の上限） =================
        public int StrongScaresTonight { get; private set; }
        public int EpisodesTonight { get; private set; }

        /// <summary>怪異の演出（PC不調）を1エピソード要求する。札が防いだ・上限に達したら false（静かな反応に置き換える）</summary>
        public bool HorrorEpisode(string fx, bool strong)
        {
            if (Ended) return false;
            var ch = Career.charm;
            if (ch.postedDay == Day && !ch.consumed)
            {
                ch.consumed = true;
                ch.everTriggered = true;
                S.Logs.Add(S.Time.CurrentMinute, "NIGHT-OPS-WS", "-", "-", "DISPLAY", "OK", "表示の乱れを検出しませんでした");
                RequestUi("FX", "CHARM");
                Route<Sq03Route>()?.OnCharmBlocked();
                return false;
            }
            if (strong)
            {
                if (StrongScaresTonight >= 2) return false;
                StrongScaresTonight++;
            }
            else
            {
                if (EpisodesTonight >= 3) return false;
                EpisodesTonight++;
            }
            RequestUi("FX", fx);
            return true;
        }
    }

    public class MorningChoice
    {
        public string QuestId, Key, Title, Text, Accept, Decline, Blocked;
    }

    /// <summary>1本のサブクエスト（ルート）</summary>
    public abstract class QuestRoute
    {
        protected readonly SideQuestManager Q;
        protected QuestRoute(SideQuestManager q) { Q = q; }
        protected ShiftSession S => Q.S;
        protected int Day => Q.Day;
        protected CareerRecord Career => CareerRecord.Current;
        protected float Now => S.Time.CurrentMinute;

        // このセッションで生成した要求だけを受理する。タグの一致だけでは成立させない。
        readonly Dictionary<EventInstance, SideQuestManager.RequestSpec> _taskRequests = new Dictionary<EventInstance, SideQuestManager.RequestSpec>();
        readonly HashSet<EventInstance> _handledTasks = new HashSet<EventInstance>();
        protected bool CanWork => State.IsOpen && !Q.Ended && EndingResolver.CheckGameOver(Career, S) == null;
        protected string TaskMail => State.stage + "-DAY" + State.nextTaskDay;
        protected void Later(float time, string key, Action action)
        {
            string stage = State.stage;
            Q.At(time, Id + "-" + key, () => { if (CanWork && State.stage == stage) action(); });
        }
        protected EventInstance SpawnTask(SideQuestManager.RequestSpec spec)
        {
            if (!CanWork) return null;
            var e = Q.SpawnRequest(spec);
            if (e != null) _taskRequests[e] = spec;
            return e;
        }
        protected bool ConsumeTask(EventInstance e, float start, float end)
        {
            if (!CanWork || e == null || !_taskRequests.TryGetValue(e, out var r) || _handledTasks.Contains(e)) return false;
            if (e.QuestTag != r.Tag || e.Employee != r.Employee || e.Device != (r.Device ?? r.Employee?.deviceId)
                || e.SourceIp != (r.SourceIp ?? (r.Employee != null ? S.Employees.GetIp(r.Employee) : "10.10.99.50")) || e.Destination?.nodeId != r.NodeId || e.AccessType != r.AccessType) return false;
            _handledTasks.Add(e);
            if (Now < start || Now >= end || e.AutoDecided) return false;
            State.Mark($"{State.stage}-DAY{Day}-{e.RequestId}-{e.State}@{T(Now)}");
            return true;
        }
        protected void Work(EventInstance e, string key, float minutes, Action done, Action aborted)
        {
            string stage = State.stage;
            var job = Q.StartJob(Id + "-" + key, e.AccessType, e.Device, e.SourceIp, e.Destination?.nodeId, minutes,
                () => { if (CanWork && State.stage == stage) done(); },
                () => { if (CanWork && State.stage == stage) aborted(); });
            job.Request = e;
            Q.CheckJobs();
        }

        public abstract string Id { get; }
        public abstract string Title { get; }
        public abstract int EarliestDay { get; }
        public QuestState State => Career.Quest(Id);

        /// <summary>必要な機能が解禁されているか（未解禁なら案内を延期）</summary>
        public abstract bool Ready();
        public abstract void Offer();
        public virtual void OnShiftStart() { }
        public virtual void OnShiftEnd() { }
        public virtual void OnViewed(QuestMailRecord r) { }
        public virtual void OnReply(QuestMailRecord r, int index, string text) { }
        public virtual void OnAction(QuestMailRecord r, string key) { }
        /// <summary>報告。戻り値: 正しい報告か（null = 誤報にも正報にもしない）</summary>
        public virtual bool? OnReported(QuestMailRecord r) => true;
        public virtual void OnRequestResolved(EventInstance e) { }
        public virtual void OnIsolated(string device) { }
        public virtual void OnBlocked(string target) { }
        public virtual void Tick(float now, float realDelta) { }
        public virtual void OnNightEnd() { }
        public virtual void OnClosed() { }
        /// <summary>最終作業の夜（DAY29）を過ぎた: 開始済みでも締める</summary>
        public virtual void OnFinalDay() => Q.Close(this, QuestResult.Abandoned);
        public virtual bool IsTarget(string deviceOrIp) => false;
        /// <summary>案内に何か反応したか（返信・対象要求の操作など）</summary>
        public virtual bool Touched() => State.marks.Count > 0;
        public virtual MorningChoice MorningChoice() => null;
        public virtual void OnMorningChoice(MorningChoice c, bool accept) { }
        /// <summary>進行メモ（事実のみ。SIGNAL DESK・デバッグ表示用）</summary>
        public virtual string Memo() => State.stage ?? "";

        protected bool Unlocked(string feature) => S.Day.IsUnlocked(feature);
        protected EmployeeData Employee(string id) => id == null ? null : S.Employees.FindById(id);
        protected static string Yen(int v) => OperatorWallet.Yen(v);
        protected static string T(float minute) => TimeManager.Format(minute);

        protected QuestMailRecord Mail(string mailId, string from, string address, string subject, string body, float time = -1, bool nextMorning = false,
            string[] replies = null, bool freeReply = false, bool pinned = false, bool horror = false, string actionKey = null, string actionLabel = null, string sentLabel = null)
            => Q.SendMail(Id, mailId, from, address, subject, body, time, nextMorning, replies, freeReply, pinned, horror, actionKey, actionLabel, sentLabel);

        protected bool Viewed(string mailId) => Q.Record(Id, mailId)?.viewed == true;

        /// <summary>周回ごとに決まる乱数（同じ周回なら、やり直しても同じ）</summary>
        protected Random Rng(string salt)
        {
            int h = 17;
            foreach (char c in Id + salt) h = unchecked(h * 31 + c);
            return new Random(Career.seed ^ h);
        }

        /// <summary>条件に合う社員を、周回ごとに固定で選ぶ</summary>
        protected EmployeeData Pick(string salt, Predicate<EmployeeData> match, string avoidId = null)
        {
            var list = new List<EmployeeData>();
            foreach (var e in S.Employees.All)
                if (e != null && e.IsActive && !e.nameRedacted && !string.IsNullOrEmpty(e.deviceId) && e.employeeId != avoidId && e.employeeId != "E-0004" && match(e)) list.Add(e);
            if (list.Count == 0) return null;
            return list[Rng(salt).Next(list.Count)];
        }

        /// <summary>この社員に権限がない利用システム（接続先, 利用システム）</summary>
        protected (string node, string access) UnauthorizedAccess(EmployeeData e, string preferNode, string salt)
        {
            var options = new List<(string, string)>();
            foreach (var n in S.Database.nodes)
            {
                if (n == null || n.role != NodeRole.Server || n.isAdministrative) continue;
                foreach (var a in n.accessTypes)
                    if (!string.IsNullOrEmpty(a.requiredPermission) && (e == null || !e.HasPermission(a.requiredPermission)) && !a.requiresVpnEnabled)
                        options.Add((n.nodeId, a.accessType));
            }
            var pref = options.FindAll(o => o.Item1 == preferNode);
            var pool = pref.Count > 0 ? pref : options;
            if (pool.Count == 0) return ("FILE-SRV01", "共有ファイル（更新）");
            return pool[Rng(salt).Next(pool.Count)];
        }

        /// <summary>この社員に権限がある利用システム</summary>
        protected (string node, string access) AuthorizedAccess(EmployeeData e, string salt)
        {
            var options = new List<(string, string)>();
            foreach (var n in S.Database.nodes)
            {
                if (n == null || n.role != NodeRole.Server || n.isAdministrative) continue;
                foreach (var a in n.accessTypes)
                    if (!string.IsNullOrEmpty(a.requiredPermission) && e.HasPermission(a.requiredPermission) && !a.requiresVpnEnabled)
                        options.Add((n.nodeId, a.accessType));
            }
            if (options.Count == 0) return ("FILE-SRV01", "共有ファイル（閲覧）");
            return options[Rng(salt).Next(options.Count)];
        }

        /// <summary>メールの「作業の条件」欄（作業日時・対象・期限）</summary>
        protected static string Conditions(params string[] rows)
        {
            var sb = new System.Text.StringBuilder("\n<b>――― 作業の条件 ―――</b>\n");
            foreach (var r in rows) sb.Append("　").AppendLine(r);
            sb.Append("<b>――――――――――――</b>");
            return sb.ToString();
        }
    }
}
