using System;
using System.Collections.Generic;
using NightShift.Systems;
using UnityEngine;
using UnityEngine.UI;

namespace NightShift.UI.Shift
{
    /// <summary>
    /// メール（受信トレイ）。業務連絡・作業依頼はすべてここに届く。
    /// 業務開始前は今日の業務連絡を開いた状態で表示し、「業務開始」もここから行う。
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
        StartButtonPulse _startPulse;
        Text _checklist;
        Action _onStart;
        bool _dirty;
        MailMessage _shown;

        public static void Open(ShiftDesktop d, Action onStart = null, MailMessage focus = null)
        {
            // 業務開始前に閉じても、開き直せば「業務開始」ボタンが残る
            if (onStart == null && !d.ShiftStarted) onStart = d.PendingStart;
            bool preShift = !d.ShiftStarted && onStart != null;
            var w = d.OpenWindow(new WindowSpec
            {
                Id = Id, Title = "メール - 受信トレイ", Icon = "mail", Width = 1180, Height = 740,
                Closable = true, Minimizable = true, Maximizable = false, Position = new Vector2(50, 4),
                ShowInTaskbar = false, // タスクバーの常設 [メール] ボタンがこのウィンドウのボタンを兼ねる
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
            if (_d.Unlocked(Data.Features.Store))
                UIFactory.Button(c, "社内購買システム", 320, 0, 150, 24, () => StoreView.Open(_d), "folder");
            if (_onStart != null)
            {
                _start = UIFactory.Button(c, "業務開始", 0, 0, 240, 30, StartShift, "clock");
                _start.Rect.AnchorTopRight(0, 0, 240, 30);
                _start.Label.fontStyle = FontStyle.Bold;
                _start.Label.fontSize = 15;
                var shadow = _start.Label.gameObject.AddComponent<Shadow>();
                shadow.effectColor = new Color(0, 0, 0, 0.5f);
                shadow.effectDistance = new Vector2(1, -1);
                _startPulse = _start.gameObject.AddComponent<StartButtonPulse>();
                _startPulse.Button = _start;
                _w.Accelerators[KeyCode.S] = StartShift;
                // 業務開始前に読むもののチェックリスト
                _checklist = UIFactory.Label(c, "", 11, TextAnchor.MiddleRight);
                _checklist.rectTransform.AnchorTopRight(246, 0, 520, 30);
                _checklist.horizontalOverflow = HorizontalWrapMode.Wrap;
                _d.ReadingChanged += OnReadingChanged;
                UpdateStartState();
                _docsRead = 0;
                foreach (var r in _d.RequiredReading()) if (r.Read && r.Id != ShiftDesktop.BriefingReadId) _docsRead++;
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
            _w.Accelerators[KeyCode.C] = _w.RequestClose;

            _d.S.Mail.Changed += OnMailChanged;
        }

        void OnDestroy()
        {
            if (_d != null && _d.S != null) _d.S.Mail.Changed -= OnMailChanged;
            if (_d != null) _d.ReadingChanged -= OnReadingChanged;
        }

        void OnMailChanged() => _dirty = true;

        int _docsRead = -1;

        void OnReadingChanged()
        {
            UpdateStartState();
            // マニュアルの既読が変わった時だけ添付ファイルの表示を更新（本文の読了ではスクロール位置を保つ）
            int docsRead = 0;
            foreach (var r in _d.RequiredReading()) if (r.Read && r.Id != ShiftDesktop.BriefingReadId) docsRead++;
            if (docsRead == _docsRead) return;
            _docsRead = docsRead;
            var keep = _shown;
            _shown = null;
            Show(keep);
        }

        /// <summary>メール本文と必要なマニュアルを最後まで読むまで「業務開始」を押せない</summary>
        void UpdateStartState()
        {
            if (_start == null || _onStart == null) return;
            var items = _d.RequiredReading();
            var sb = new System.Text.StringBuilder("開始前に最後まで読む: ");
            bool all = true;
            for (int i = 0; i < items.Count; i++)
            {
                var it = items[i];
                if (!it.Read) all = false;
                if (i > 0) sb.Append("  ");
                sb.Append(it.Read ? "<color=#0030A0>■</color> " : "<color=#B00000>□</color> ").Append(it.Label);
            }
            _checklist.text = sb.ToString();
            _start.Interactable = all;
            _start.SetText(all ? "業務開始  ▶  (S)" : "業務開始（未読あり）");
            if (_startPulse != null) _startPulse.Ready = all;
        }

        void StartShift()
        {
            if (_onStart == null) return;
            if (!_d.ReadingComplete)
            {
                AudioFx.Error();
                _w.SetStatus(0, "業務連絡メールと添付のマニュアルを最後まで読んでから開始してください。");
                return;
            }
            var start = _onStart;
            _onStart = null;
            if (_start != null) _start.gameObject.SetActive(false);
            if (_checklist != null) _checklist.gameObject.SetActive(false);
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
            _d.SelectMail(m);
            if (m != null && m.IsPhish && _d.S.Day.dayNumber >= 26)
                _d.OfferClue("C3", "偽メールの言い回し",
                    "偽メールの中の「夜は長いので」という一文。\n夜勤担当しか使わない言い回しだ。前任者の付箋にも、同じ言葉があった。");
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
            bool showAddr = _d.Unlocked(Data.Features.MailSender) && !string.IsNullOrEmpty(m.FromAddress);
            Row("差出人:", showAddr ? $"{m.From} <{m.FromAddress}>" : m.From);
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
                    string mark = "";
                    if (m.IsBriefing && m.Day == _d.S.Day.dayNumber && _d.IsRequiredReading(doc.documentId))
                        mark = _d.IsRead(doc.documentId) ? "\n<color=#0030A0>既読</color>" : "\n<color=#B00000>未読（要確認）</color>";
                    var tile = UIFactory.Button(box.transform, doc.fileName + mark, () => DocumentView.Open(_d, dd), doc.icon);
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
            bool canReport = _d.Unlocked(Data.Features.Report) && !m.IsBriefing;
            if (!string.IsNullOrEmpty(m.ActionKey) || canReport)
            {
                if (!string.IsNullOrEmpty(m.ActionKey))
                {
                    var mm = m;
                    var action = UIFactory.Button(_preview, m.ActionLabel ?? "開く", 0, y, 240, 28, () => RunAction(mm.ActionKey, mm), "lock");
                    action.Label.fontStyle = FontStyle.Bold;
                }
                if (canReport)
                {
                    var mr = m;
                    var rep = UIFactory.Button(_preview, m.Reported ? "報告済み" : "このメールを報告", 250, y, 160, 28, () => ReportMail(mr), "warning");
                    rep.Interactable = !m.Reported;
                }
                y += 34;
            }

            // メール本文は文章のみ（図はマニュアル側）
            var sr = DocumentRenderer.Render(_preview, m.Body, null);
            ((RectTransform)sr.transform).Stretch(0, y, 0, 0);
            // 今日の業務連絡は、最後までスクロールしたら既読
            if (m.IsBriefing && m.Day == _d.S.Day.dayNumber && !_d.IsRead(ShiftDesktop.BriefingReadId))
                ReadTracker.Attach(sr, () => _d.MarkRead(ShiftDesktop.BriefingReadId));
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

        void RunAction(string key, MailMessage m)
        {
            switch (key)
            {
                case MailManager.ActionPasswordChange: PasswordChangeView.Open(_d); break;
                case MailManager.ActionPhishForm: PhishFormView.Open(_d, m); break;
                case MailManager.ActionMalwareAttachment: PhishingMail.OpenAttachment(_d.S, m, _d.HasTool(Data.Items.Vaccine)); break;
            }
        }

        void ReportMail(MailMessage m)
        {
            if (m.Reported) return;
            m.Reported = true;
            _w.SetStatus(0, _d.S.Security.Report($"不審なメール「{m.Subject}」（{m.FromAddress}）", m.IsPhish));
            var shown = _shown;
            _shown = null;
            Show(shown);
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

namespace NightShift.UI.Shift
{
    /// <summary>業務開始ボタンを目立たせる（読了後はオレンジ色で脈打つ）</summary>
    public class StartButtonPulse : MonoBehaviour
    {
        public RetroButton Button;
        public bool Ready;
        static readonly Color Orange = new Color(1f, 0.62f, 0.15f);
        static readonly Color Disabled = new Color(0.82f, 0.82f, 0.82f);

        void Update()
        {
            if (Button == null) return;
            if (Ready)
            {
                float k = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 4f);
                Button.Background.color = Color.Lerp(Orange, new Color(1f, 0.85f, 0.45f), k * 0.6f);
                Button.Label.color = Color.white;
                float s = 1f + 0.035f * k;
                Button.transform.localScale = new Vector3(s, s, 1);
            }
            else
            {
                Button.Background.color = Disabled;
                Button.transform.localScale = Vector3.one;
            }
        }
    }
}
