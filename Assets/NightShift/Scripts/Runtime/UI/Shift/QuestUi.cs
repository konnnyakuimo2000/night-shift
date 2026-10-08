using System;
using NightShift.Core;
using NightShift.Data;
using NightShift.Systems.SideQuests;
using UnityEngine;
using UnityEngine.UI;

namespace NightShift.UI.Shift
{
    /// <summary>
    /// サブクエストの画面側（SideQuestManager.UiRequested を受ける）。
    /// 怪異の演出（SUBQUEST_SPEC.md §5.4）はゲーム画面の中だけ。OS・音量・プロセスには触れない。
    /// 全面の演出中はゲーム時計と承認の入力を止め、終わったら一括で戻す。設定（控えめ・点滅を減らす・大きな音）を反映する。
    /// </summary>
    public class QuestUi : MonoBehaviour
    {
        ShiftDesktop _d;
        RectTransform _layer;
        GameObject _full;           // 全面の演出（赤い画面・接近・机の映像）
        float _fullLeft, _fullTotal;
        bool _pausedByFx;
        string _fullKind;
        Image _fullImage;
        RectTransform _figure;
        GameObject _scan, _frame, _after;
        float _scanLeft, _frameLeft, _afterLeft;

        static GameSettings Set => GameSettings.Current;

        public static QuestUi Attach(ShiftDesktop d, RectTransform layer)
        {
            var ui = layer.gameObject.AddComponent<QuestUi>();
            ui._d = d;
            ui._layer = layer;
            d.S.Quests.UiRequested += ui.OnRequest;
            return ui;
        }

        void OnDestroy()
        {
            if (_d != null && _d.S != null) _d.S.Quests.UiRequested -= OnRequest;
            EndFull();
        }

        void OnRequest(string kind, object arg)
        {
            switch (kind)
            {
                case "REMOTE": if (arg is Sq03Route r) HorrorRemoteView.Open(_d, r); break;
                case "RECORD": TextWindow.Open(_d, "quest-record", "対応記録", arg as string ?? "", "doc"); break;
                case "HINT": TextWindow.Open(_d, "quest-hint", "ヒント", arg as string ?? "", "info"); break;
                case "LOAN": LeisureAccountView.Open(_d); break;
                case "CHOICE":
                    if (arg is MorningChoice choice)
                    {
                        // 時計は要求の受理時に停止済み。二択の確定まで業務へ戻さない。
                        _d.Game.SaveQuestChoiceCheckpoint();
                        ShowChoice(choice);
                    }
                    break;
                case "CONFIRM_TRUE":
                    if (arg is Sq03Route t)
                        _d.ShowModal("担当変更",
                            "自分の症状は止まりますが、次の担当者へ同じメールと症状を引き継ぎます。\n現在の周回は終了します。\n\n転送しますか？",
                            "warning", "転送する", t.ConfirmTrue, "やめる", null);
                    break;
                case "FX": Play(arg as string); break;
            }
        }

        // ================= その場の二択 =================
        GameObject _choice;
        float _choiceArm;
        FlatButton[] _choiceButtons;

        /// <summary>
        /// 周回が終わる二択（SQ06 の加入／離脱）。Enter・Esc では決めない（Esc は一時停止のまま使える）。
        /// 表示直後の入力を持ち越さないよう、ボタンは少し待ってから押せるようにする。
        /// </summary>
        void ShowChoice(MorningChoice c)
        {
            if (_choice != null) Destroy(_choice);
            var dim = UIFactory.Solid(_layer, new Color(0, 0, 0, 0.6f), "QuestChoice");
            dim.rectTransform.Stretch();
            var box = UIFactory.Panel(dim.transform, RetroTheme.Raised, "Box");
            box.rectTransform.anchorMin = box.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            box.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            box.rectTransform.sizeDelta = new Vector2(560, 260);
            var bar = UIFactory.Image(box.transform, RetroTheme.GradientSprite(RetroTheme.TitleActiveA, RetroTheme.TitleActiveB), Color.white, "Title");
            bar.rectTransform.StretchH(3, 18, 3, 3);
            UIFactory.Label(bar.transform, " " + c.Title, 12, TextAnchor.MiddleLeft, Color.white).rectTransform.Stretch();
            UIFactory.Label(box.transform, c.Text, 20, 32, 520, 150, 13, TextAnchor.UpperLeft);
            var accept = ToolStyle.Button(box.transform, c.Accept, 40, 200, 220, 34, () => Choose(c, true), ToolStyle.Red, false);
            var decline = ToolStyle.Button(box.transform, c.Decline, 300, 200, 220, 34, () => Choose(c, false), ToolStyle.Slate, false);
            _choiceButtons = new[] { accept, decline };
            foreach (var b in _choiceButtons) b.Interactable = false;
            _choiceArm = 1.0f;
            _choice = dim.gameObject;
            _d.QuestBlocking = true;
            PopupMenu.CloseAll();
        }

        void Choose(MorningChoice c, bool accept)
        {
            if (_choiceArm > 0 || _d.PauseOpen) return;
            _d.S.Quests.ChooseMorning(c, accept);
            if (_d.S.Quests.PendingChoice != null) return;   // 確定しなかった（本編の終端が先に決まっていたなど）
            Destroy(_choice);
            _choice = null;
            _d.QuestBlocking = _full != null;
        }

        // ================= 演出 =================
        /// <summary>全面の演出で時計を止めている</summary>
        public bool HoldsClock => _full != null;

        public void Play(string fx)
        {
            switch (fx)
            {
                case "CHARM": _d.ShowStatus("モニターの側面に貼った札の文字が、少し薄くなった。"); break;
                case "AFTERIMAGE": AfterImage(); break;
                case "SCAN": Scan(); break;
                case "FRAME": Frame(); break;
                case "GARBLE": _d.RequestGlitch(); break;
                case "RED": Full("RED", 5f); break;
                case "APPROACH": Full("APPROACH", 1.6f); break;
                case "DESK": Full("DESK", 3.2f); break;
            }
        }

        /// <summary>FX-H01 焼き付き: 差出人の残像（判断情報は隠さない・クリックを遮らない）</summary>
        void AfterImage()
        {
            if (_after != null) Destroy(_after);
            var t = UIFactory.Label(_layer, Sq03Route.From, 22, TextAnchor.UpperLeft, new Color(1, 1, 1, Set.horrorMild ? 0.08f : 0.16f), true);
            t.rectTransform.Place(560, 90, 400, 40);
            t.raycastTarget = false;
            _after = t.gameObject;
            _afterLeft = 4f;
        }

        /// <summary>FX-H07 走査線のずれ（怪異ウィンドウの近くだけ。8秒以内に自動復帰・「表示を再描画」で解消）</summary>
        void Scan()
        {
            if (_scan != null) return;
            var host = UIFactory.Rect("QuestScan", _layer).Stretch();
            var r = new System.Random(Time.frameCount);
            for (int i = 0; i < 9; i++)
            {
                var bar = UIFactory.Solid(host, new Color(r.Next(3) == 0 ? 1f : 0.2f, 0.25f, r.Next(2) == 0 ? 1f : 0.3f, Set.horrorMild ? 0.12f : 0.28f), "Tear");
                bar.raycastTarget = false;
                bar.rectTransform.anchorMin = new Vector2(0, 0); bar.rectTransform.anchorMax = new Vector2(1, 0);
                bar.rectTransform.pivot = new Vector2(0, 0);
                bar.rectTransform.sizeDelta = new Vector2(0, 2 + r.Next(5));
                bar.rectTransform.anchoredPosition = new Vector2((r.Next(2) == 0 ? -1 : 1) * r.Next(4, 30), 80 + r.Next(620));
            }
            var fix = UIFactory.Button(host, "表示を再描画", 0, 0, 120, 24, EndScan, "pc");
            fix.Rect.anchorMin = fix.Rect.anchorMax = new Vector2(1, 0);
            fix.Rect.pivot = new Vector2(1, 0);
            fix.Rect.anchoredPosition = new Vector2(-12, 40);
            _scan = host.gameObject;
            _scanLeft = 8f;
        }

        void EndScan() { if (_scan != null) Destroy(_scan); _scan = null; }

        /// <summary>FX-H09 残る窓: 閉じたメールの外枠だけが残る（クリックを遮らない・6秒で消える）</summary>
        void Frame()
        {
            if (_frame != null) Destroy(_frame);
            var img = UIFactory.Image(_layer, RetroTheme.Etched, new Color(1, 1, 1, Set.horrorMild ? 0.25f : 0.6f), "QuestFrame");
            img.raycastTarget = false;
            img.rectTransform.Place(50, 4, 1180, 680);
            _frame = img.gameObject;
            _frameLeft = 6f;
        }

        /// <summary>全面の演出。ゲーム時計と承認の入力を止める（演出用の実時間で終わる）</summary>
        void Full(string kind, float seconds)
        {
            if (_full != null) return;
            _fullKind = kind;
            _fullLeft = _fullTotal = seconds;
            var bg = UIFactory.Solid(_layer, new Color(0, 0, 0, 0), "QuestFull");
            bg.rectTransform.Stretch();
            _fullImage = bg;
            _full = bg.gameObject;
            if (kind == "APPROACH" || kind == "DESK") BuildFigure(bg.transform, kind == "DESK");
            if (!_d.S.Time.Paused) { _d.S.Time.Paused = true; _pausedByFx = true; }
            _d.QuestBlocking = true;
            if (Set.loudSounds && kind != "RED") AudioFx.PowerDown(); else AudioFx.Hum();
        }

        /// <summary>折り畳まれた異形の輪郭（細い関節の線）。控えめでは静止した薄い影にする</summary>
        void BuildFigure(Transform parent, bool desk)
        {
            var host = UIFactory.Rect("Figure", parent);
            host.anchorMin = host.anchorMax = new Vector2(0.5f, 0.5f);
            host.sizeDelta = new Vector2(600, 600);
            _figure = host;
            if (desk)
            {
                // 自分の机に似た映像: 机と、無人の椅子
                var desk1 = UIFactory.Solid(host, new Color(0.18f, 0.15f, 0.12f), "Desk");
                desk1.rectTransform.anchorMin = desk1.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                desk1.rectTransform.sizeDelta = new Vector2(520, 40); desk1.rectTransform.anchoredPosition = new Vector2(0, -60);
                var chair = UIFactory.Solid(host, new Color(0.1f, 0.1f, 0.12f), "Chair");
                chair.rectTransform.anchorMin = chair.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                chair.rectTransform.sizeDelta = new Vector2(90, 150); chair.rectTransform.anchoredPosition = new Vector2(-40, -150);
                chair.name = "Chair";
            }
            var r = new System.Random(desk ? 7 : 3);
            Vector2 at = new Vector2(0, desk ? 260 : 0);
            for (int i = 0; i < 9; i++)
            {
                var seg = UIFactory.Solid(host, new Color(0.02f, 0.01f, 0.01f, Set.horrorMild ? 0.4f : 0.95f), "Joint");
                seg.raycastTarget = false;
                float len = 80 + r.Next(90);
                var rt = seg.rectTransform;
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot = new Vector2(0, 0.5f);
                rt.sizeDelta = new Vector2(len, 6 + r.Next(5));
                rt.anchoredPosition = at;
                float ang = (float)(r.NextDouble() * 300 - 150);
                rt.localRotation = Quaternion.Euler(0, 0, ang);
                at += new Vector2(Mathf.Cos(ang * Mathf.Deg2Rad), Mathf.Sin(ang * Mathf.Deg2Rad)) * len * 0.7f;
                at = Vector2.ClampMagnitude(at, 260);
            }
        }

        void EndFull()
        {
            if (_full != null) Destroy(_full);
            _full = null; _figure = null;
            if (_d != null)
            {
                _d.QuestBlocking = _choice != null;
                // 一時停止メニュー・二択が残っている間は、時計を再開しない
                if (_pausedByFx && _d.S != null && !_d.PauseOpen && _d.S.Quests.PendingChoice == null) _d.S.Time.Paused = false;
            }
            _pausedByFx = false;
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            if (_choice != null && _choiceArm > 0 && !_d.PauseOpen)
            {
                _choiceArm -= dt;
                if (_choiceArm <= 0) foreach (var b in _choiceButtons) b.Interactable = true;
            }
            if (_after != null && (_afterLeft -= dt) <= 0) { Destroy(_after); _after = null; }
            if (_scan != null && (_scanLeft -= dt) <= 0) EndScan();
            if (_frame != null && (_frameLeft -= dt) <= 0) { Destroy(_frame); _frame = null; }
            if (_full == null) return;
            _fullLeft -= dt;
            float t = 1f - Mathf.Clamp01(_fullLeft / _fullTotal);
            switch (_fullKind)
            {
                case "RED":
                    // 色が少しずつ失われ、深い赤へ（点滅させない）。真っ赤は最大3秒、最大5秒で戻る
                    float a = t < 0.3f ? t / 0.3f : t > 0.85f ? (1f - t) / 0.15f : 1f;
                    var red = Set.horrorMild ? new Color(0.3f, 0.12f, 0.12f) : new Color(0.42f, 0f, 0.02f);
                    _fullImage.color = new Color(red.r, red.g, red.b, 0.88f * a);
                    break;
                case "APPROACH":
                    // 枠の内側まで一瞬で接近する（控えめ・点滅を減らす: ゆっくり現れて静止）
                    _fullImage.color = new Color(0, 0, 0, Set.horrorMild ? 0.5f : 0.8f);
                    if (_figure != null)
                    {
                        float k = Set.horrorMild || Set.reduceFlashing ? 0.6f : Mathf.Lerp(0.2f, 1.6f, t * t);
                        _figure.localScale = Vector3.one * k;
                    }
                    break;
                case "DESK":
                    _fullImage.color = new Color(0.04f, 0.05f, 0.06f, 0.95f);
                    if (_figure != null)
                    {
                        // 椅子が引かれ、何かが画面へ倒れ込む
                        var chair = _figure.Find("Chair") as RectTransform;
                        if (chair != null) chair.anchoredPosition = new Vector2(-40 - 120 * Mathf.Clamp01(t * 2), -150);
                        float fall = Set.horrorMild ? 0.3f : Mathf.Clamp01((t - 0.5f) * 2f);
                        _figure.localScale = Vector3.one * (1f + fall * 0.8f);
                    }
                    break;
            }
            if (_fullLeft <= 0) EndFull();
        }
    }

    /// <summary>読むだけの小さなウィンドウ（対応記録・ヒント）。再表示しても演出は再生しない</summary>
    public static class TextWindow
    {
        public static void Open(ShiftDesktop d, string id, string title, string text, string icon)
        {
            var w = d.OpenWindow(new WindowSpec { Id = id, Title = title, Icon = icon, Width = 520, Height = 340, StatusCells = 1 }, out bool created);
            if (!created) return;
            var p = ToolStyle.Page(w);
            var box = ToolStyle.Box(p, 0, 1, 8, 0);
            ToolStyle.ScrollText(box, 13).text = text;
            w.Accelerators[KeyCode.C] = w.RequestClose;
        }
    }

    /// <summary>
    /// SQ03 H2 のリモート対応（制限付きセッション。接続モードは選ばせない・転送や資格情報の委譲なし）。
    /// 操作は社員証の画像の［閉じる］だけ。反応しなければ同じ場所の［強制終了］。背後のメール本文の輪郭と一覧がヒントになる。
    /// </summary>
    public class HorrorRemoteView : MonoBehaviour
    {
        public const string Id = "quest-remote";
        ShiftDesktop _d;
        RetroWindow _w;
        Sq03Route _r;
        GameObject _card, _list;
        RetroButton _close, _kill;
        bool _closed;

        public static void Open(ShiftDesktop d, Sq03Route r)
        {
            var w = d.OpenWindow(new WindowSpec { Id = Id, Title = $"リモート対応 - {Sq03Route.Device}（制限付きセッション）", Icon = "remote", Width = 720, Height = 520, StatusCells = 1 }, out bool created);
            if (!created) return;
            var v = w.gameObject.AddComponent<HorrorRemoteView>();
            v._d = d; v._w = w; v._r = r;
            v.Build();
            r.OnRemoteConnected();
            w.Closed += v.OnClosedWindow;
        }

        void Build()
        {
            var c = _w.Content;
            var screen = UIFactory.Solid(c, new Color(0.05f, 0.05f, 0.07f), "RemoteScreen");
            screen.rectTransform.Stretch(0, 0, 0, 40);
            // 背後のメール本文の輪郭: 各段落の先頭の一文字分だけ白く、右側は黒く欠けている（読める文字は出さない）
            for (int i = 0; i < 7; i++)
            {
                float y = 30 + i * 46;
                var head = UIFactory.Solid(screen.transform, new Color(0.92f, 0.92f, 0.92f), "Head");
                head.rectTransform.Place(30, y, 18, 18);
                var rest = UIFactory.Solid(screen.transform, new Color(0.12f, 0.12f, 0.14f), "Rest");
                rest.rectTransform.Place(52, y, 300 + (i * 37) % 200, 18);
            }
            // 強制終了の後に読める一覧（元の送信時刻だけが並び、受信順は消されている）
            var list = UIFactory.Image(screen.transform, RetroTheme.Field, Color.white, "SentList");
            list.rectTransform.Place(380, 40, 300, 150);
            UIFactory.Label(list.transform, "元の送信時刻　　受信順\n\n　00:01　　　　　\n　00:02　　　　　\n　00:03　　　　　", 8, 8, 284, 134, 14, TextAnchor.UpperLeft).font = RetroTheme.MonoFont;
            _list = list.gameObject;
            _list.SetActive(false);

            // 社員証の画像ビューアー
            var card = UIFactory.Panel(screen.transform, RetroTheme.Raised, "IdCard");
            card.rectTransform.Place(200, 60, 320, 330);
            var title = UIFactory.Image(card.transform, RetroTheme.GradientSprite(RetroTheme.TitleActiveA, RetroTheme.TitleActiveB), Color.white, "Title");
            title.rectTransform.StretchH(3, 18, 3, 3);
            UIFactory.Label(title.transform, " 社員証.bmp - 画像ビューアー", 12, TextAnchor.MiddleLeft, Color.white).rectTransform.Stretch();
            var face = UIFactory.Image(card.transform, PortraitPainter.Get(Fake()), Color.white, "Photo");
            face.rectTransform.Place(20, 30, 120, 150);
            face.color = GameSettings.Current.horrorMild ? new Color(0.85f, 0.85f, 0.85f) : new Color(0.95f, 0.85f, 0.85f);
            // 写真の中の、成立しない関節の線
            if (!GameSettings.Current.horrorMild)
                for (int i = 0; i < 3; i++)
                {
                    var j = UIFactory.Solid(face.transform, new Color(0.05f, 0, 0, 0.8f), "Joint");
                    j.rectTransform.Place(10 + i * 30, 100 - i * 20, 70, 3);
                    j.rectTransform.localRotation = Quaternion.Euler(0, 0, 30 + i * 50);
                }
            UIFactory.Label(card.transform, $"氏名　{Sq03Route.Name}\n所属　総務部\n社員番号　（空欄）", 150, 40, 160, 80, 12, TextAnchor.UpperLeft);
            _close = UIFactory.Button(card.transform, "閉じる", 110, 290, 100, 26, OnClose, "exit");
            _kill = UIFactory.Button(card.transform, "強制終了", 110, 290, 100, 26, OnKill, "warning");
            _kill.gameObject.SetActive(false);
            _card = card.gameObject;

            var disc = UIFactory.Button(c, "切断する", 0, 0, 140, 30, Disconnect, "remote");
            disc.Rect.anchorMin = disc.Rect.anchorMax = new Vector2(1, 0);
            disc.Rect.pivot = new Vector2(1, 0);
            disc.Rect.anchoredPosition = new Vector2(-6, 4);
            _w.SetStatus(0, "社員証の画像が開いたままです。");
        }

        static EmployeeData Fake()
        {
            var e = ScriptableObject.CreateInstance<EmployeeData>();
            e.hideFlags = HideFlags.DontSave;
            e.employeeId = "E-0000"; e.fullName = Sq03Route.Name; e.kana = "アマミヤ リカ"; e.age = 27; e.department = "総務部";
            return e;
        }

        void OnClose()
        {
            AudioFx.Error();
            _w.SetStatus(0, "社員証.bmp は応答していません。");
            _close.gameObject.SetActive(false);
            _kill.gameObject.SetActive(true);
        }

        void OnKill()
        {
            _card.SetActive(false);
            _r.OnForceQuit();
            // 演出の後、背後の一覧が読める
            _list.SetActive(true);
            _w.SetStatus(0, "画像ビューアーを終了しました（記録は自動で保存されました）。");
        }

        void Disconnect()
        {
            if (_closed) return;
            _closed = true;
            _r.OnDisconnected();
            _w.Close();
        }

        void OnClosedWindow()
        {
            if (_closed) return;
            _closed = true;
            _r.OnDisconnected();
        }
    }
}
