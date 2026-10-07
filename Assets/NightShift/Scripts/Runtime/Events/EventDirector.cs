using System.Collections.Generic;
using NightShift.Data;
using NightShift.Systems;

namespace NightShift.Events
{
    /// <summary>
    /// EventDirector
    /// ├ TimelineScheduler      … 固定骨格＋ランダム枠を時刻で発火
    /// ├ NormalEventGenerator   … 背景通信（会社が動いている感）
    /// ├ HelpDeskGenerator      … 問い合わせ
    /// └ ThreatScenarioManager  … 攻撃チェーン
    /// </summary>
    public class EventDirector
    {
        readonly ShiftSession _s;
        public TimelineScheduler Scheduler { get; } = new TimelineScheduler();
        public NormalEventGenerator Normal { get; }
        public HelpDeskGenerator HelpDeskGen { get; }
        public ThreatScenarioManager Threats { get; }
        public bool Stopped { get; private set; }
        float _tasksClearedAt;
        bool _hadOpenTasks;

        /// <summary>プレイヤーが対応すべきタスク（保留中の要求・未対応の問い合わせ）の数</summary>
        public int OpenPlayerTasks => _s.Requests.Pending.Count + _s.HelpDesk.OpenCount + _s.OpenTaskCount;

        bool IsGated(float time) => time < _s.Day.tutorialGateUntilMinute;

        public EventDirector(ShiftSession s)
        {
            _s = s;
            Normal = new NormalEventGenerator(s);
            HelpDeskGen = new HelpDeskGenerator(s);
            Threats = new ThreatScenarioManager(s);
            _s.Requests.Resolved += OnResolved;
        }

        public void ScheduleDay(DayDefinition day)
        {
            foreach (var def in day.fixedEvents)
            {
                if (def == null || !def.IsAvailableOn(day.dayNumber)) continue;
                ScheduleDefinition(def, def.triggerWindow);
            }
            foreach (var slot in day.randomSlots)
            {
                var def = PickWeighted(slot.pool, day.dayNumber);
                if (def == null) continue;
                ScheduleDefinition(def, slot.window, slot.label);
            }
            if (day.randomEvents) PlanRandomRequests(day);
            HelpDeskGen.ScheduleDay(Scheduler, day);
            if (day.passwordChangeAtMinute >= 0)
            {
                float t = day.passwordChangeAtMinute;
                if (IsGated(t)) Scheduler.AddGated(t, day.tutorialGateMaxDelayMinutes, "TASK PASSWORD", SpawnPasswordTask);
                else Scheduler.Add(t, "TASK PASSWORD", SpawnPasswordTask);
            }
            Threats.ScheduleDay(Scheduler, day);
            if (day.randomEvents) { ScheduleFlagConsequences(day); ScheduleCarriedInfections(); }
        }

        /// <summary>間違えた問い合わせの、後日の影響（HELPDESK_CATALOG.md の「間違えると」）</summary>
        void ScheduleFlagConsequences(DayDefinition day)
        {
            var career = Core.CareerRecord.Current;
            EventDefinition Find(string id)
            {
                foreach (var d in day.violationPool) if (d != null && d.eventId == id) return d;
                return null;
            }
            void Infect(string virus)
            {
                var emp = _s.Employees.RandomOnShift();
                if (emp != null) career.infections.Add(virus + ":" + emp.deviceId);
            }
            if (career.HasFlag("WRONG_A3")) { career.ClearFlag("WRONG_A3"); Infect("V3"); }
            if (career.HasFlag("WRONG_A20")) { career.ClearFlag("WRONG_A20"); Infect("V3"); }
            if (career.HasFlag("WRONG_A9")) { career.ClearFlag("WRONG_A9"); Infect("V1"); }
            if (career.HasFlag("WRONG_A4") && Find("T1_SPOOFED_SOURCE") is EventDefinition spoof)
            {
                career.ClearFlag("WRONG_A4");
                ScheduleDefinition(spoof, new UnityEngine.Vector2(30, 300), "AFTERMATH A4");
            }
            if (career.HasFlag("WRONG_A13") && Find("T6_NOT_ACTIVE_EMPLOYEE") is EventDefinition retired)
            {
                career.ClearFlag("WRONG_A13");
                ScheduleDefinition(retired, new UnityEngine.Vector2(30, 300), "AFTERMATH A13");
            }
            if (career.HasFlag("WRONG_A7"))
            {
                career.ClearFlag("WRONG_A7");
                Scheduler.Add(Rand(40, 300), "AFTERMATH A7", () => PhishingMail.SendAttachment(_s));
            }
        }

        /// <summary>前の夜に駆除・隔離しなかった感染の続き（定期通信 → 30分後に周りへ広がる）</summary>
        void ScheduleCarriedInfections()
        {
            foreach (var entry in Core.CareerRecord.Current.infections)
            {
                var parts = entry.Split(':');
                if (parts.Length != 2) continue;
                var emp = _s.Employees.FindByDevice(parts[1]);
                if (emp == null) continue;
                _s.Security.MarkInfected(parts[1], parts[0]);
                var def = UnityEngine.ScriptableObject.CreateInstance<ThreatScenarioDefinition>();
                def.scenarioId = "CARRY_" + parts[0] + "_" + parts[1];
                def.minimumDay = 1; def.maximumDay = 99;
                def.sourceIpPrefix = "203.0.113.";
                def.stopOnBlock = false;
                def.steps.Add(new ThreatStep { label = "定期通信", offsetMinutes = 0, kind = ThreatStepKind.Script, script = "BEACON", repeat = 10, repeatIntervalMinutes = 3f });
                def.steps.Add(new ThreatStep { label = "拡大の判定", offsetMinutes = 31, kind = ThreatStepKind.Script, script = "SPREAD_CHECK" });
                var run = Threats.Schedule(Scheduler, def, Rand(5f, 40f));
                run.Victim = emp;
            }
        }

        /// <summary>
        /// 本番業務（DAY8〜）: 接続要求をランダムに配置する。違反の割合と、同時に重なる確率は日ごとの設定で決まる。
        /// </summary>
        void PlanRandomRequests(DayDefinition day)
        {
            float from = day.startMinute + 4f, to = day.endMinute - 15f;
            var times = new List<float>();
            for (int i = 0; i < day.requestCount; i++)
            {
                float t = Rand(from, to);
                times.Add(t);
                // 同時発生: 近い時刻にもう1件（件数には含めない）
                if (_s.Rng.NextDouble() < day.burstChance) times.Add(System.Math.Min(to, t + Rand(0.3f, 2.5f)));
            }
            times.Sort();
            foreach (var t in times)
            {
                bool violation = day.violationPool.Count > 0 && _s.Rng.NextDouble() < day.violationRatio;
                var def = PickWeighted(violation ? day.violationPool : day.routinePool, day.dayNumber)
                          ?? PickWeighted(day.routinePool, day.dayNumber);
                if (def == null) continue;
                var d = def;
                Scheduler.Add(t, (violation ? "VIOLATION " : "ROUTINE ") + def.eventId, () => Spawn(d));
            }
        }

        void ScheduleDefinition(EventDefinition def, UnityEngine.Vector2 window, string label = null)
        {
            var d = def;
            float t = Rand(window.x, window.y);
            if (def.preferWhileRemoteActive)
                Scheduler.AddPreferRemote(window.x, window.y, label ?? def.eventId, () => Spawn(d));
            else if (def.kind == EventKind.ConnectionRequest && IsGated(t))
                Scheduler.AddGated(t, _s.Day.tutorialGateMaxDelayMinutes, label ?? def.eventId, () => Spawn(d));
            else
                Scheduler.Add(t, label ?? def.eventId, () => Spawn(d));
        }

        EventDefinition PickWeighted(List<EventDefinition> pool, int day)
        {
            float total = 0;
            foreach (var d in pool) if (d != null && d.IsAvailableOn(day)) total += WeightOf(d);
            if (total <= 0) return null;
            double r = _s.Rng.NextDouble() * total;
            foreach (var d in pool)
            {
                if (d == null || !d.IsAvailableOn(day)) continue;
                r -= WeightOf(d);
                if (r <= 0) return d;
            }
            return pool[pool.Count - 1];
        }

        /// <summary>
        /// 出やすさ。自分のIDの不正利用（T4）は、研修で決めたパスワードが弱い・付箋に書いた・初期化が多いほど出やすい。
        /// 防御AIへの偽の指示（T15）は、防御AIを持っていなければ普通のなりすましと同じ扱い。
        /// </summary>
        float WeightOf(EventDefinition d)
        {
            float w = d.weight;
            if (d.userType == UserType.OperatorAccount)
            {
                var acc = Core.GameManager.Instance != null ? Core.GameManager.Instance.Account : null;
                if (acc != null)
                {
                    if (!acc.Changed) w *= 3f;
                    else w *= 1f + (4 - acc.Strength) * 0.5f;
                    if (acc.WrittenDown) w *= 2f;
                    w *= 1f + acc.ResetCount * 0.3f;
                }
            }
            return w;
        }

        float Rand(float a, float b) => a + (b - a) * (float)_s.Rng.NextDouble();

        /// <summary>手が空いたときに追加で届けた接続要求の数</summary>
        public int ExtraSpawned { get; private set; }

        /// <summary>
        /// 手が空いたプレイヤーに、仕事を1件届ける。
        /// 研修（1件ずつの夜）は、予定している次の仕事を前倒しする。予定が尽きたら、その夜の練習用の要求から追加する。
        /// 本番は、その夜の配分（違反の割合）どおりに接続要求を1件追加する。
        /// </summary>
        public EventInstance SpawnExtra()
        {
            if (Stopped) return null;
            if (!_s.Day.randomEvents && PullNextGated()) return _s.Requests.Pending.Count > 0 ? _s.Requests.Pending[_s.Requests.Pending.Count - 1] : null;
            var day = _s.Day;
            for (int i = 0; i < 6; i++)
            {
                EventDefinition def;
                if (day.randomEvents)
                {
                    bool violation = day.violationPool.Count > 0 && _s.Rng.NextDouble() < day.violationRatio;
                    def = PickWeighted(violation ? day.violationPool : day.routinePool, day.dayNumber) ?? PickWeighted(day.routinePool, day.dayNumber);
                }
                else def = PickWeighted(PracticePool(day), day.dayNumber);
                if (def == null) return null;
                var e = _s.Factory.Build(def);
                if (e == null) continue;
                _s.Requests.Submit(e);
                ExtraSpawned++;
                return e;
            }
            return null;
        }

        /// <summary>研修: まだ出ていない、予定の接続要求・問い合わせを今に前倒しする</summary>
        bool PullNextGated()
        {
            ScheduledItem next = null;
            foreach (var it in Scheduler.Items)
                if (!it.Fired && it.Gated && (next == null || it.Time < next.Time)) next = it;
            if (next == null) return false;
            next.Time = _s.Time.CurrentMinute;
            next.Latest = next.Time;
            next.GateDeadline = next.Time;
            int before = OpenPlayerTasks;
            Scheduler.Tick(_s.Time.CurrentMinute, false, true);
            return OpenPlayerTasks > before;
        }

        /// <summary>研修の夜の練習用の要求（その夜のランダム枠に入っている接続要求。攻撃シナリオは含めない）</summary>
        static List<EventDefinition> PracticePool(DayDefinition day)
        {
            var list = new List<EventDefinition>();
            foreach (var slot in day.randomSlots)
                foreach (var d in slot.pool)
                    if (d != null && d.kind == EventKind.ConnectionRequest && d.category != EventCategory.Threat && !list.Contains(d)) list.Add(d);
            foreach (var d in day.routinePool) if (d != null && !list.Contains(d)) list.Add(d);
            return list;
        }

        /// <summary>定義から即時に1件発生させる</summary>
        public EventInstance Spawn(EventDefinition def)
        {
            if (Stopped) return null;
            var e = _s.Factory.Build(def);
            if (e == null) return null;
            switch (def.kind)
            {
                case EventKind.ConnectionRequest:
                    _s.Requests.Submit(e);
                    break;
                case EventKind.LogEntry:
                    int n = 2 + _s.Rng.Next(3);
                    string[] ports = { "TCP 21", "TCP 23", "TCP 135", "TCP 139", "TCP 445", "TCP 1433", "UDP 161" };
                    for (int i = 0; i < n; i++)
                    {
                        float t = i * 0.3f;
                        string port = ports[_s.Rng.Next(ports.Length)];
                        string src = e.SourceIp;
                        Scheduler.Add(_s.Time.CurrentMinute + t, "log", () =>
                        {
                            _s.Network.Emit(true, e.Destination, TrafficResult.Denied);
                            _s.Logs.Add(_s.Time.CurrentMinute, src, e.DestinationLabel, "-", def.logEventType ?? "FW", def.logResult ?? "DENY", port);
                        });
                    }
                    break;
                case EventKind.BackgroundTraffic:
                    _s.Network.Emit(e.Zone == SourceZone.External, e.Destination, TrafficResult.Delivered);
                    break;
            }
            if (def.followUp != null)
            {
                var f = def.followUp;
                Scheduler.Add(_s.Time.CurrentMinute + def.followUpDelayMinutes, "followup " + f.eventId, () => Spawn(f));
            }
            return e;
        }

        /// <summary>運用担当者へのパスワード変更依頼</summary>
        public void SpawnPasswordTask()
        {
            if (_s.HasOpenTask(OperatorTask.PasswordChange) || _s.PasswordChangedThisShift) return;
            _s.AddTask(OperatorTask.PasswordChange, "ログインパスワードの変更");
            _s.Logs.Add(_s.Time.CurrentMinute, "AUTH-SRV01", "-", _s.OperatorName, "ACCOUNT", "NOTICE", "初期パスワードの変更依頼");
            var mail = _s.Mail.Add(new MailMessage
            {
                MailId = "TASK-PASSWORD-" + _s.Day.dayNumber,
                From = MailManager.Sender("情報システム部 セキュリティ担当"),
                FromAddress = "security@" + MailManager.CompanyDomain,
                To = MailManager.Recipient(_s.OperatorName),
                Subject = "【依頼】初期パスワードの変更について",
                DateLabel = $"{_s.Day.dateLabel} {_s.Time.Now}",
                Body = $"{_s.OperatorName} 様\n\n" +
                       "お使いのアカウントのログインパスワードが、<b>初期設定のまま</b>になっています。\n" +
                       "<b>本日中に</b>、[システム設定] → [パスワードの変更] から変更してください。\n\n" +
                       "・パスワードは他人に教えないでください。\n" +
                       "・社内のパスワード規程に従ってください（規程は変更画面に表示されます）。\n" +
                       MailManager.Signature("情報システム部 セキュリティ担当"),
                Day = _s.Day.dayNumber,
                ActionKey = MailManager.ActionPasswordChange,
                ActionLabel = "パスワードの変更を開く",
                ReceivedAt = _s.Time.CurrentMinute
            });
            _s.Notifications.Notify(NotificationType.Info, "新着メール",
                $"情報システム部: {mail.Subject}", _s.Time.CurrentMinute, () => _s.Ui?.OpenMailbox(mail));
        }

        void OnResolved(EventInstance e)
        {
            // Threat シナリオ外の不正接続を許可した場合も痕跡をログに残す（プレイヤーが後から気付ける）
            if (e.Owner == null && e.State == EventState.Allowed && e.IsMalicious)
                _s.Logs.Add(_s.Time.CurrentMinute + 0.2f, e.SourceIp, e.DestinationLabel, e.DisplayUser, "AUTH", "SUCCESS", "管理ログイン成功 (外部)");
        }

        public void Tick(float now, float delta)
        {
            if (Stopped) return;
            bool remoteActive = _s.HelpDesk.ActiveSession != null && _s.HelpDesk.ActiveSession.Connected;
            int open = OpenPlayerTasks;
            if (open > 0) _hadOpenTasks = true;
            else if (_hadOpenTasks) { _hadOpenTasks = false; _tasksClearedAt = now; }
            bool gateOpen = open == 0 && now - _tasksClearedAt >= _s.Day.tutorialGateBreathMinutes;
            Scheduler.Tick(now, remoteActive, gateOpen);
            Normal.Tick(delta);
        }

        /// <summary>04:00: 新規イベント生成を停止</summary>
        public void StopNewEvents()
        {
            Stopped = true;
            Scheduler.AcceptNew = false;
            Normal.Enabled = false;
        }

        // ---------------- Debug ----------------
        public EventDefinition FindDefinition(System.Predicate<EventDefinition> pred)
        {
            foreach (var d in _s.Day.fixedEvents) if (d != null && pred(d)) return d;
            foreach (var slot in _s.Day.randomSlots) foreach (var d in slot.pool) if (d != null && pred(d)) return d;
            return null;
        }
    }
}
