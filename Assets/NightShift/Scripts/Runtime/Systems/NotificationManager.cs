using System;
using System.Collections.Generic;

namespace NightShift.Systems
{
    public enum NotificationType { ConnectionRequest, HelpDeskTicket, SecurityAlert, ShiftNotice, Info }

    public class Notification
    {
        public NotificationType Type;
        public string Title;
        public string Body;
        public float GameTime;
        public Action OnClick;
    }

    public class NotificationManager
    {
        readonly List<Notification> _history = new List<Notification>();
        public IReadOnlyList<Notification> History => _history;
        public event Action<Notification> Raised;

        public void Notify(NotificationType type, string title, string body, float time, Action onClick = null)
        {
            var n = new Notification { Type = type, Title = title, Body = body, GameTime = time, OnClick = onClick };
            _history.Add(n);
            Raised?.Invoke(n);
        }
    }
}
