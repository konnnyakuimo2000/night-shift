using System;
using System.Collections.Generic;
using NightShift.Data;
using NightShift.Events;

namespace NightShift.Systems
{
    /// <summary>Connection Request の保留キューと判断処理。</summary>
    public class RequestManager
    {
        readonly ShiftSession _s;
        readonly List<EventInstance> _pending = new List<EventInstance>();
        readonly List<EventInstance> _processed = new List<EventInstance>();
        int _counter;

        public IReadOnlyList<EventInstance> Pending => _pending;
        public IReadOnlyList<EventInstance> Processed => _processed;

        public event Action<EventInstance> Created;
        public event Action<EventInstance> Resolved;   // 許可・拒否・期限切れ

        public RequestManager(ShiftSession s) { _s = s; }

        public void Submit(EventInstance e)
        {
            e.RequestId = $"REQ-{++_counter:0000}";
            // 申請理由（サブクエストの対象要求は、依頼メールの内容を備考に持つので付けない）
            if (e.Reason == null && e.QuestTag == null) e.Reason = RequestReasons.For(e, _s.Seed);
            e.Kind = EventKind.ConnectionRequest;
            e.State = EventState.Pending;
            e.GeneratedTime = _s.Time.CurrentMinute;
            e.TimeoutAt = e.GeneratedTime + (e.Definition != null ? e.Definition.timeoutMinutes : 25f);
            e.Expected = PolicyEvaluator.Evaluate(e, _s.Day.policy, out var reason);
            e.ExpectedReason = reason;
            _s.Evaluation.RegisterRequest(e);
            _pending.Add(e);
            if (e.IsMalicious) _s.Security.MarkHostile(e.SourceIp);

            // 遮断中の接続元・停止中のアカウント・隔離した端末からの要求は自動的に拒否（通知しない）
            string auto = _s.Security.IsBlocked(e.SourceIp) ? "遮断中の接続元"
                : e.Employee != null && _s.Security.IsSuspended(e.Employee.employeeId) ? "停止中のアカウント"
                : _s.Security.IsIsolated(e.Device) ? "隔離中の端末" : null;
            if (auto != null)
            {
                _s.Logs.Add(e.GeneratedTime, e.SourceIp, e.DestinationLabel, UserLabel(e), "FW", "DENY", $"{e.RequestId} 自動拒否（{auto}）");
                e.AutoDecided = true;
                Created?.Invoke(e);
                Decide(e, PlayerAction.Block);
                return;
            }

            _s.Logs.Add(e.GeneratedTime, e.SourceIp, e.DestinationLabel, UserLabel(e), "CONN-REQ", "PENDING", $"{e.RequestId} {e.AccessType} / {e.Device}");
            _s.Network.Emit(e.Zone == SourceZone.External, e.Destination, TrafficResult.Pending, e.RequestId);

            if (e.NotifyAsSecurityAlert)
            {
                _s.Notifications.Notify(NotificationType.SecurityAlert, "セキュリティ警告",
                    $"{e.Destination?.nodeId} への接続要求があります ({e.RequestId})", _s.Time.CurrentMinute, () => _s.Ui?.OpenConnectionMonitor(e));
            }
            else
            {
                _s.Notifications.Notify(NotificationType.ConnectionRequest, "新しい接続要求",
                    $"{e.RequestId}  {e.Device} → {e.DestinationLabel}", _s.Time.CurrentMinute, () => _s.Ui?.OpenConnectionMonitor(e));
            }
            Created?.Invoke(e);
        }

        public static string UserLabel(EventInstance e) =>
            string.IsNullOrEmpty(e.DisplayEmployeeId) || e.DisplayEmployeeId == "-" ? e.DisplayUser : $"{e.DisplayUser} ({e.DisplayEmployeeId})";

        public void Decide(EventInstance e, PlayerAction action)
        {
            if (e == null || e.State != EventState.Pending) return;
            float now = _s.Time.CurrentMinute;
            e.PlayerAction = action;
            e.DecidedTime = now;
            e.ResponseTime = now - e.GeneratedTime;
            e.State = action == PlayerAction.Allow ? EventState.Allowed : EventState.Blocked;
            _pending.Remove(e);
            _processed.Add(e);
            _s.Evaluation.RecordDecision(e);

            bool allowed = action == PlayerAction.Allow;
            _s.Logs.Add(now, e.SourceIp, e.DestinationLabel, UserLabel(e), "CONN-REQ", allowed ? "ALLOWED" : "DENIED", $"{e.RequestId} {e.AccessType} (operator: {_s.OperatorName})");
            _s.Network.Emit(e.Zone == SourceZone.External, e.Destination, allowed ? TrafficResult.Delivered : TrafficResult.Denied, e.RequestId);
            Resolved?.Invoke(e);
        }

        public void Tick(float now)
        {
            if (!_s.Day.requestsExpire) return; // チュートリアル中は期限切れなし
            for (int i = _pending.Count - 1; i >= 0; i--)
            {
                var e = _pending[i];
                if (now >= e.TimeoutAt) Expire(e, "TIMEOUT");
            }
        }

        public void ExpireAll()
        {
            for (int i = _pending.Count - 1; i >= 0; i--) Expire(_pending[i], "SHIFT END");
        }

        void Expire(EventInstance e, string why)
        {
            float now = _s.Time.CurrentMinute;
            e.State = EventState.Expired;
            e.ResponseTime = now - e.GeneratedTime;
            _pending.Remove(e);
            _processed.Add(e);
            _s.Evaluation.RecordExpired(e);
            _s.Logs.Add(now, e.SourceIp, e.DestinationLabel, UserLabel(e), "CONN-REQ", "EXPIRED", $"{e.RequestId} 応答なし ({why})");
            Resolved?.Invoke(e);
        }
    }
}
