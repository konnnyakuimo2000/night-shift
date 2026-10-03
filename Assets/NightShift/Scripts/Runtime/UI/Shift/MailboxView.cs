using System;
using System.Collections.Generic;
using NightShift.Systems;
using UnityEngine;
using UnityEngine.UI;

namespace NightShift.UI.Shift
{
    /// <summary>
    /// メール（受信トレイ）。業務連絡・作業依頼はすべてここに届く。
    /// 業務開始前は今日の業務連絡を開いた状態で表示し、START SHIFT もここから行う。
    /// </summary>
    public class MailboxView : MonoBehaviour
    {
        public const string Id = "mailbox";
        ShiftDesktop _d;
        RetroWindow _w;
        TableView _table;
        RectTransform _preview;
        RetroButton _inboxButton;
        RetroButton _start;
        Action _onStart;
        bool _dirty;
        MailMessage _shown;

        public static void Open(ShiftDesktop d, Action onStart = null, MailMessage focus = null)
        {
            bool preShift = !d.ShiftStarted && onStart != null;
            var w = d.OpenWindow(new WindowSpec
            {
                Id = Id, Title = "メール - 受信トレイ", Icon = "mail", Width = 1180, Height = 740,
                Closable = !preShift, Minimizable = !preShift, Maximizable = false, Position = new Vector2(50, 4),
                Menus = ShiftDesktop.StandardMenus(d), StatusCells = 2, StatusWidths = new float[] { 160 }
            }, out bool created);
            var v = created ? w.gameObject.AddComponent<MailboxView>() : w.GetComponent<MailboxView>();
            if (created)
            {
                v._d = d; v._w = w; v._onStart = preShift ? onStart : null;
                v.Build();
            }
            var target = focus ?? d.S.Mail.TodaysBriefing(d.S.Day.dayNumber);
            v.Refresh();
            if (target != null) v._table.SelectData(target);
        }

        void Build()
        {
            var c = _w.Content;

            // ツールバー
            UIFactory.Button(c, "受信(F5)", 0, 0, 90, 24, Refresh, "mail");
            foreach (var (label, x) in new[] { ("返信", 94f), ("転送", 168f), ("削除", 242f) })
                UIFactory.Button(c, label, x, 0, 70, 24, null).Interactable = false;
            if (_onStart != null)
            {
                _start = UIFactory.Button(c, "START SHIFT(S)", 0, 0, 190, 28, StartShift, "clock");
                _start.Rect.AnchorTopRight(0, 0, 190, 28);
                _start.Label.fontStyle = FontStyle.Bold;
                _w.Accelerators[KeyCode.S] = StartShift;
            }

            // フォルダー
            var folders = UIFactory.Image(c, RetroTheme.Field, Color.white, "Folders");
            folders.rectTransform.Place(0, 32, 110, 634);
            _inboxButton = UIFactory.FlatButton(folders.transform, "受信トレイ", Refresh, "mail");
            _inboxButton.Label.alignment = TextAnchor.MiddleLeft;
            _inboxButton.Rect.Place(2, 2, 106, 22);
            _inboxButton.SetLatched(true);
            var sent = UIFactory.FlatButton(folders.transform, "送信済み", null, "folder");
            sent.Label.alignment = TextAnchor.MiddleLeft; sent.Rect.Place(2, 26, 106, 22); sent.Interactable = false;
            var trash = UIFactory.FlatButton(folders.transform, "ごみ箱", null, "folder");
            trash.Label.alignment = TextAnchor.MiddleLeft; trash.Rect.Place(2, 50, 106, 22); trash.Interactable = false;

            // 一覧
            // 一覧は左の細い列、本文は右側に縦いっぱい
            _table = TableView.Create(c, 114, 32, 330, 634,
                new TableColumn("", 18), new TableColumn("件名", 188), new TableColumn("受信日時", 90));
            _table.SelectionChanged += o => Show(o as MailMessage);

            // プレビュー
            _preview = UIFactory.Rect("Preview", c).Place(450, 32, 720, 634);

            _w.Accelerators[KeyCode.UpArrow] = () => _table.MoveSelection(-1);
            _w.Accelerators[KeyCode.DownArrow] = () => _table.MoveSelection(1);
            _w.Accelerators[KeyCode.F5] = Refresh;
            if (_onStart == null) _w.Accelerators[KeyCode.C] = _w.RequestClose;

            _d.S.Mail.Changed += OnMailChanged;
        }

        void OnDestroy()
        {
            if (_d != null && _d.S != null) _d.S.Mail.Changed -= OnMailChanged;
        }

        void OnMailChanged() => _dirty = true;

        void StartShift()
        {
            if (_onStart == null) return;
            var start = _onStart;
            _onStart = null;
            if (_start != null) _start.gameObject.SetActive(false);
            _w.Closable = true;
            _w.Accelerators.Remove(KeyCode.S);
            _w.Accelerators[KeyCode.C] = _w.RequestClose;
            start();
            _w.Minimize();
        }

        void Refresh()
        {
            var list = new List<MailMessage>(_d.S.Mail.Inbox);
            list.Reverse(); // 新しい順
            _table.SetRows(list, m => new[] { m.Read ? "" : "●", m.Subject, ShortDate(m.DateLabel) });
            int unread = _d.S.Mail.UnreadCount;
            _inboxButton.SetText(unread > 0 ? $"受信トレイ ({unread})" : "受信トレイ");
            _w.SetStatus(1, $"{list.Count} 通 / 未読 {unread} 通");
            _dirty = false;
        }

        void Show(MailMessage m)
        {
            if (m == _shown && m != null) return;
            _shown = m;
            foreach (Transform t in _preview) Destroy(t.gameObject);
            if (m == null) return;
            _d.S.Mail.MarkRead(m);

            float y = 0;
            void Row(string label, string value)
            {
                UIFactory.Label(_preview, label, 0, y, 56, 20);
                UIFactory.ValueBox(_preview, 58, y, 662, 20, value, false);
                y += 22;
            }
            Row("差出人:", m.From);
            Row("宛先:", m.To);
            Row("日付:", m.DateLabel);
            Row("件名:", m.Subject);
            y += 4;

            // 添付ファイル（解放済みのものだけ。目立つ枠に大きく表示）
            var docs = new List<Data.DocumentDefinition>();
            foreach (var doc in m.Attachments)
                if (doc != null && (string.IsNullOrEmpty(doc.requiredFeature) || _d.Unlocked(doc.requiredFeature))) docs.Add(doc);
            if (docs.Count > 0)
            {
                var box = UIFactory.Image(_preview, RetroTheme.Field, new Color(1f, 0.97f, 0.82f), "Attachments");
                box.rectTransform.Place(0, y, 720, 88);
                UIFactory.IconImage(box.transform, "doc", 8, 6);
                var head = UIFactory.Label(box.transform, $"添付ファイル（{docs.Count}）— まずはこちらのマニュアルに目を通してください", 28, 4, 680, 20, 13);
                head.fontStyle = FontStyle.Bold;
                head.color = new Color(0.55f, 0.3f, 0f);
                float x = 8;
                foreach (var doc in docs)
                {
                    var dd = doc;
                    var tile = UIFactory.Button(box.transform, doc.fileName, () => DocumentView.Open(_d, dd), doc.icon);
                    tile.Rect.Place(x, 28, 230, 52);
                    tile.Icon.sprite = RetroTheme.BigIcon(doc.icon);
                    tile.Icon.rectTransform.sizeDelta = new Vector2(32, 32);
                    tile.Label.rectTransform.Stretch(44, 2, 4, 2);
                    tile.Label.alignment = TextAnchor.MiddleLeft;
                    tile.Label.fontSize = 12;
                    tile.Label.fontStyle = FontStyle.Bold;
                    x += 236;
                }
                y += 94;
            }

            // メール内の操作ボタン
            if (!string.IsNullOrEmpty(m.ActionKey))
            {
                var action = UIFactory.Button(_preview, m.ActionLabel ?? "開く", 0, y, 240, 28, () => RunAction(m.ActionKey), "lock");
                action.Label.fontStyle = FontStyle.Bold;
                y += 34;
            }

            // 図はそのメールの日のルールで描く（過去の業務連絡は当時の内容のまま）
            var ctx = _d.FigureContext;
            var mailDay = m.IsBriefing ? _d.Game.Database.FindDay(m.Day) : null;
            if (mailDay != null)
            {
                ctx.Day = mailDay;
                ctx.Unlocked = f => mailDay.IsUnlocked(f);
            }
            var sr = DocumentRenderer.Render(_preview, m.Body, (id, holder) => ManualFigures.Build(id, holder, ctx));
            ((RectTransform)sr.transform).Stretch(0, y, 0, 0);
            _w.SetStatus(0, m.IsBriefing ? $"DAY {m.Day} の業務連絡" : "");
        }

        /// <summary>一覧用の短い日時（"1998/05/19 (火) 21:30" → "05/19 21:30"）</summary>
        static string ShortDate(string label)
        {
            if (string.IsNullOrEmpty(label)) return "";
            var parts = label.Split(' ');
            string date = parts[0].Length >= 10 ? parts[0].Substring(5) : parts[0];
            string time = parts.Length > 0 ? parts[parts.Length - 1] : "";
            return time.Contains(":") ? date + " " + time : date;
        }

        void RunAction(string key)
        {
            switch (key)
            {
                case MailManager.ActionPasswordChange: PasswordChangeView.Open(_d); break;
            }
        }

        void Update()
        {
            if (_dirty && Time.frameCount % 10 == 0)
            {
                var keep = _shown;
                Refresh();
                if (keep != null) _table.SelectData(keep);
            }
        }
    }
}
