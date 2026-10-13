using System;
using System.Collections.Generic;
using NightShift.Data;
using NightShift.RemotePC;

namespace NightShift.Systems
{
    public enum TicketState { New, InProgress, Answered, Resolved, Failed, Unresolved }

    public class TicketMessage
    {
        public float Time;
        public string From;
        public string Text;
    }

    public class Ticket
    {
        public string TicketId;
        public HelpDeskDefinition Definition;
        public HelpDeskType Type => Definition.type;
        public EmployeeData Employee;
        public string Subject;
        public float CreatedAt;
        public float FirstResponseAt = -1;
        public TicketState State = TicketState.New;
        public readonly List<TicketMessage> Messages = new List<TicketMessage>();

        // TYPE A
        public string[] Options = new string[2];
        public int CorrectIndex;
        public int AnsweredIndex = -1;

        // TYPE B
        public VirtualPCState PC;
        public string FaultLabel;
        public int Reports;
        public bool ReminderSent;

        public bool IsOpen => State == TicketState.New || State == TicketState.InProgress;
        public string StateLabel
        {
            get
            {
                switch (State)
                {
                    case TicketState.New: return "未対応";
                    case TicketState.InProgress: return "対応中";
                    case TicketState.Answered: return "回答済";
                    case TicketState.Resolved: return "完了";
                    case TicketState.Failed: return "未解決";
                    default: return "未処理";
                }
            }
        }
    }

    public class HelpDeskManager
    {
        readonly ShiftSession _s;
        readonly List<Ticket> _tickets = new List<Ticket>();
        int _counter;

        public IReadOnlyList<Ticket> Tickets => _tickets;
        public event Action<Ticket> Changed;
        public RemoteSession ActiveSession { get; private set; }
        public event Action<RemoteSession> SessionChanged;

        public HelpDeskManager(ShiftSession s) { _s = s; }

        public static string Fill(string text, EmployeeData e) =>
            (text ?? "").Replace("{name}", e?.fullName ?? "").Replace("{dept}", e?.department ?? "").Replace("{device}", e?.deviceId ?? "");

        public Ticket Create(HelpDeskDefinition def, EmployeeData employee)
        {
            var t = new Ticket
            {
                TicketId = $"HD-{++_counter:0000}",
                Definition = def,
                Employee = employee,
                CreatedAt = _s.Time.CurrentMinute
            };
            // 社員ごとの口調で、件名と最初の文面を組み立てる（質問の中身は変えない）
            t.Subject = Voice.Subject(employee, def.subject, Voice.RngFor(employee, t.TicketId + "s"));
            t.Messages.Add(new TicketMessage { Time = t.CreatedAt, From = employee.fullName, Text = Voice.Open(employee, Fill(def.message, employee), Voice.RngFor(employee, t.TicketId + "o")) });

            if (def.type == HelpDeskType.Binary)
            {
                bool swap = _s.Rng.Next(2) == 0;
                t.Options[swap ? 1 : 0] = def.correctOption;
                t.Options[swap ? 0 : 1] = def.incorrectOption;
                t.CorrectIndex = swap ? 1 : 0;
            }
            else
            {
                t.PC = VirtualPCState.Create(def.pcProfile, employee, _s.Employees.GetIp(employee), _s.Rng);
                ApplyFault(t, def);
                // 隔離済みの端末なら、後から作った PC も通信できない状態で始まる
                t.PC.Quarantined = _s.Security.IsIsolated(employee.deviceId);
                var tt = t;
                t.PC.ProcessKilled += p => OnProcessKilled(tt, p);
                // 感染している端末なら、見慣れないプロセスが動いている（手がかり）
                string virus = _s.Security.InfectionOf(employee.deviceId);
                if (virus != null)
                {
                    t.PC.StartProcess(VirusCatalog.ProcessOf(virus) ?? "svch0st.exe");
                    t.PC.MalwareState = virus;
                    _s.Discover(virus, DiscoveryStage.Observed);
                }
                // DAY13〜: 見慣れないファイル（二重の拡張子）がデスクトップに置かれていることがある（経路 D の手がかり）
                if (_s.Day.dayNumber >= 13 && (virus != null || _s.Rng.NextDouble() < 0.2))
                {
                    string[] baits = { "請求書_最終.doc.exe", "写真.jpg.exe", "議事録.txt.exe" };
                    t.PC.BaitFile = baits[_s.Rng.Next(baits.Length)];
                    t.PC.Files.Add(new FileEntry { folder = "デスクトップ", name = t.PC.BaitFile, sizeKb = 48 });
                }
            }

            _tickets.Add(t);
            _s.Evaluation.RecordTicketCreated();
            _s.Logs.Add(t.CreatedAt, employee.deviceId, "HELPDESK", $"{employee.fullName} ({employee.employeeId})", "TICKET", "OPEN", $"{t.TicketId} {t.Subject}");
            _s.Notifications.Notify(NotificationType.HelpDeskTicket, "ITサポート受付",
                $"{t.TicketId} {employee.fullName}: {t.Subject}", t.CreatedAt, () => _s.Ui?.OpenHelpDesk(t));
            Changed?.Invoke(t);
            return t;
        }

        /// <summary>
        /// リモートPCで、感染しているウイルスのプロセスを終了した = 手作業で駆除した（VACCINE がなくても対処できる）。
        /// 加点は実際に感染を消したときに一度だけ
        /// </summary>
        void OnProcessKilled(Ticket t, VirtualProcess p)
        {
            string host = t.PC.Hostname;
            string v = _s.Security.InfectionOf(host);
            if (v == null || VirusCatalog.ProcessOf(v) != p.Name) return;
            if (!_s.Security.ClearInfection(host)) return;
            _s.Evaluation.SecurityActionsCorrect++;
            foreach (var other in _tickets)
                if (other.PC != null && other.PC.Hostname == host) { other.PC.MalwareState = "None"; if (other.PC != t.PC) other.PC.RunningProcesses.RemoveAll(x => x.Name == p.Name); }
            _s.Logs.Add(_s.Time.CurrentMinute, host, "-", _s.OperatorName, "PROCESS", "KILL", $"{p.Name}（手作業で駆除）");
            _s.Discover(v, DiscoveryStage.Removed);
        }

        void ApplyFault(Ticket t, HelpDeskDefinition def)
        {
            if (def.faultVariants.Count == 0) return;
            float total = 0;
            foreach (var v in def.faultVariants) total += v.weight;
            double r = _s.Rng.NextDouble() * total;
            FaultVariant pick = def.faultVariants[0];
            foreach (var v in def.faultVariants)
            {
                r -= v.weight;
                if (r <= 0) { pick = v; break; }
            }
            foreach (var f in pick.faults) t.PC.SetValue(f.key, f.value);
            t.FaultLabel = pick.label;
        }

        void MarkResponded(Ticket t)
        {
            if (t.FirstResponseAt >= 0) return;
            t.FirstResponseAt = _s.Time.CurrentMinute;
            _s.Evaluation.RecordTicketFirstResponse(t.FirstResponseAt - t.CreatedAt);
        }

        public void Answer(Ticket t, int index, string operatorName)
        {
            if (t.Type != HelpDeskType.Binary || !t.IsOpen) return;
            MarkResponded(t);
            t.AnsweredIndex = index;
            t.State = TicketState.Answered;
            float now = _s.Time.CurrentMinute;
            t.Messages.Add(new TicketMessage { Time = now, From = operatorName, Text = t.Options[index] });
            t.Messages.Add(new TicketMessage { Time = now + 0.5f, From = t.Employee.fullName, Text = Voice.Reply(t.Employee, Fill(t.Definition.replyAfterAnswer, t.Employee), Voice.RngFor(t.Employee, t.TicketId + "r")) });
            if (index == t.CorrectIndex) _s.Evaluation.HelpDeskCorrect++; else _s.Evaluation.HelpDeskIncorrect++;
            _s.Logs.Add(now, "HELPDESK", t.Employee.deviceId, t.Employee.fullName, "TICKET", "ANSWERED", t.TicketId);
            Changed?.Invoke(t);
        }

        // 解説と出典（IPA試験の過去問題）は業務中の会話には出さず、夜の振り返り（NightReview）で読む

        public RemoteSession Connect(Ticket t)
        {
            if (t.Type != HelpDeskType.Remote || !t.IsOpen) return null;
            MarkResponded(t);
            if (t.State == TicketState.New) t.State = TicketState.InProgress;
            if (ActiveSession != null && ActiveSession.Ticket == t && ActiveSession.Connected) return ActiveSession;
            if (ActiveSession != null && ActiveSession.Ticket != t) Disconnect();
            ActiveSession = new RemoteSession(t);
            _s.Logs.Add(_s.Time.CurrentMinute, "HELPDESK", t.Employee.deviceId, _s.OperatorName, "REMOTE", "CONNECTED", $"{t.TicketId} リモート接続");
            Changed?.Invoke(t);
            SessionChanged?.Invoke(ActiveSession);
            return ActiveSession;
        }

        public void Disconnect()
        {
            if (ActiveSession == null) return;
            var t = ActiveSession.Ticket;
            ActiveSession.Connected = false;
            _s.Logs.Add(_s.Time.CurrentMinute, "HELPDESK", t.Employee.deviceId, _s.OperatorName, "REMOTE", "DISCONNECTED", t.TicketId);
            ActiveSession = null;
            SessionChanged?.Invoke(null);
            Changed?.Invoke(t);
        }

        /// <summary>対応完了の報告。最終的なPC状態で判定する。</summary>
        public bool Report(Ticket t, string operatorName)
        {
            if (t.Type != HelpDeskType.Remote || !t.IsOpen) return false;
            MarkResponded(t);
            float now = _s.Time.CurrentMinute;
            t.Reports++;
            t.Messages.Add(new TicketMessage { Time = now, From = operatorName, Text = "対応しました。ご確認をお願いします。" });
            bool ok = t.PC.Satisfies(t.Definition.resolutionConditions);
            if (ok)
            {
                t.State = TicketState.Resolved;
                _s.Evaluation.RemoteResolved++;
                t.Messages.Add(new TicketMessage { Time = now + 0.5f, From = t.Employee.fullName, Text = Voice.Reply(t.Employee, Fill(t.Definition.successReply, t.Employee), Voice.RngFor(t.Employee, t.TicketId + "ok")) });
                _s.Logs.Add(now, "HELPDESK", t.Employee.deviceId, t.Employee.fullName, "TICKET", "RESOLVED", t.TicketId);
                if (ActiveSession != null && ActiveSession.Ticket == t) Disconnect();
            }
            else
            {
                _s.Evaluation.RemoteReportsWrong++;
                t.Messages.Add(new TicketMessage { Time = now + 0.5f, From = t.Employee.fullName, Text = Voice.Reply(t.Employee, Fill(t.Definition.failureReply, t.Employee), Voice.RngFor(t.Employee, t.TicketId + "ng" + t.Reports)) });
                if (t.Reports >= t.Definition.maxReports)
                {
                    t.State = TicketState.Failed;
                    _s.Evaluation.RemoteFailed++;
                    t.Messages.Add(new TicketMessage { Time = now + 1f, From = t.Employee.fullName, Text = "朝になったら別の方法を考えます…。" });
                    _s.Logs.Add(now, "HELPDESK", t.Employee.deviceId, t.Employee.fullName, "TICKET", "FAILED", t.TicketId);
                    if (ActiveSession != null && ActiveSession.Ticket == t) Disconnect();
                }
            }
            Changed?.Invoke(t);
            return ok;
        }

        public void Tick(float now, float delta)
        {
            foreach (var t in _tickets)
            {
                if (t.PC != null) t.PC.Tick(delta);
                if (t.IsOpen && !t.ReminderSent && !string.IsNullOrEmpty(t.Definition.reminderMessage)
                    && t.FirstResponseAt < 0 && now - t.CreatedAt >= t.Definition.reminderAfterMinutes)
                {
                    t.ReminderSent = true;
                    t.Messages.Add(new TicketMessage { Time = now, From = t.Employee.fullName, Text = Voice.Reminder(t.Employee, Fill(t.Definition.reminderMessage, t.Employee), Voice.RngFor(t.Employee, t.TicketId + "m")) });
                    var tt = t;
                    _s.Notifications.Notify(NotificationType.HelpDeskTicket, "ITサポート受付", $"{t.TicketId} {t.Employee.fullName} から追加のメッセージ", now, () => _s.Ui?.OpenHelpDesk(tt));
                    Changed?.Invoke(t);
                }
            }
        }

        public void CloseShift()
        {
            foreach (var t in _tickets)
            {
                if (t.IsOpen)
                {
                    t.State = TicketState.Unresolved;
                    _s.Evaluation.Unresolved++;
                }
            }
            if (ActiveSession != null) Disconnect();
        }

        public int OpenCount
        {
            get { int n = 0; foreach (var t in _tickets) if (t.IsOpen) n++; return n; }
        }
    }

    public class RemoteSession
    {
        public Ticket Ticket;
        public VirtualPCState PC => Ticket.PC;
        public bool Connected = true;
        public RemoteSession(Ticket t) { Ticket = t; }
    }
}
