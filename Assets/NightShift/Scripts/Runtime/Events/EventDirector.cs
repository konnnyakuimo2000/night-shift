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
            foreach (var d in pool) if (d != null && d.IsAvailableOn(day)) total += d.weight;
            if (total <= 0) return null;
            double r = _s.Rng.NextDouble() * total;
            foreach (var d in pool)
            {
                if (d == null || !d.IsAvailableOn(day)) continue;
                r -= d.weight;
                if (r <= 0) return d;
            }
            return pool[pool.Count - 1];
        }

        float Rand(float a, float b) => a + (b - a) * (float)_s.Rng.NextDouble();

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
