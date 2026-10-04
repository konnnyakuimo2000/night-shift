using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace NightShift.UI
{
    /// <summary>
    /// マニュアル用の「実際の画面」図。実際のウィンドウと同じ項目名・配置で再現し、
    /// 見るべき場所を赤枠と番号で示す（値は例）。
    /// </summary>
    public static class ManualUiFigures
    {
        const float W = ManualFigures.Width;
        static readonly Color Red = new Color(0.85f, 0.08f, 0.08f);
        static readonly Color Bg = new Color(0.93f, 0.94f, 0.96f);

        public static float Build(string id, RectTransform holder, FigureContext ctx)
        {
            switch (id)
            {
                case "UI_CONNECTION": return Connection(holder, ctx);
                case "UI_DIRECTORY": return Directory(holder, ctx);
                case "UI_DEVICE": return Device(holder);
                case "UI_DETAIL": return Detail(holder);
                case "UI_PERMISSION": return Permission(holder);
                case "UI_ADMIN": return Admin(holder);
                case "UI_HELPDESK": return HelpDesk(holder);
                case "UI_EXTERNAL": return External(holder);
                case "UI_LOG": return Log(holder);
                case "UI_REMOTE": return Remote(holder);
                default: return 0;
            }
        }

        // ------------------------------------------------------------ toolkit
        static RectTransform Frame(RectTransform holder, float h, string caption)
        {
            var bg = UIFactory.Image(holder, RetroTheme.Field, Bg, "UIFigure");
            bg.rectTransform.Place(0, 0, W, h);
            var c = UIFactory.Label(holder, "図: " + caption + "（値は例）", 11, TextAnchor.MiddleLeft, RetroTheme.Shadow);
            c.rectTransform.Place(4, h + 2, W, 16);
            return bg.rectTransform;
        }

        static RectTransform Window(RectTransform p, float x, float y, float w, float h, string title)
        {
            var win = UIFactory.Image(p, RetroTheme.Raised, Color.white, "Win");
            win.rectTransform.Place(x, y, w, h);
            var bar = UIFactory.Image(win.transform, RetroTheme.GradientSprite(RetroTheme.TitleActiveA, RetroTheme.TitleActiveB), Color.white, "Bar");
            bar.rectTransform.StretchH(3, 15, 3, 3);
            var t = UIFactory.Label(bar.transform, " " + title, 10, TextAnchor.MiddleLeft, Color.white);
            t.fontStyle = FontStyle.Bold;
            t.rectTransform.Stretch();
            return win.rectTransform;
        }

        static Text Lbl(RectTransform p, string s, float x, float y, float w, float h = 16, int size = 10, TextAnchor a = TextAnchor.MiddleLeft, Color? c = null)
        {
            var t = UIFactory.Label(p, s, size, a, c ?? RetroTheme.Text);
            t.rectTransform.Place(x, y, w, h);
            return t;
        }

        static void Field(RectTransform p, float x, float y, float lw, float vw, string label, string value)
        {
            Lbl(p, label, x, y, lw);
            var v = UIFactory.Image(p, RetroTheme.Field, Color.white, "V");
            v.rectTransform.Place(x + lw, y, vw, 16);
            Lbl(v.rectTransform, value, 3, 0, vw - 4);
        }

        static void GroupBox(RectTransform p, float x, float y, float w, float h, string title)
        {
            var e = UIFactory.Image(p, RetroTheme.Etched, Color.white, "G");
            e.raycastTarget = false;
            e.rectTransform.Place(x, y + 6, w, h - 6);
            var tb = UIFactory.Solid(p, RetroTheme.Face, "GT");
            var t = UIFactory.Label(tb.transform, " " + title + " ", 10, TextAnchor.MiddleLeft, RetroTheme.TextBlue);
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            tb.rectTransform.Place(x + 6, y, t.preferredWidth + 2, 12);
            t.rectTransform.Stretch();
        }

        static void Btn(RectTransform p, float x, float y, float w, string label, float h = 18)
        {
            var b = UIFactory.Image(p, RetroTheme.Raised, Color.white, "B");
            b.rectTransform.Place(x, y, w, h);
            var t = UIFactory.Label(b.transform, label, 10, TextAnchor.MiddleCenter);
            t.rectTransform.Stretch();
        }

        static void Table(RectTransform p, float x, float y, float[] cols, string[] headers, string[][] rows, int selected)
        {
            float w = 0; foreach (var c in cols) w += c;
            var bg = UIFactory.Image(p, RetroTheme.Field, Color.white, "T");
            bg.rectTransform.Place(x, y, w + 4, 16 + rows.Length * 14 + 4);
            float cx = 2;
            for (int i = 0; i < cols.Length; i++)
            {
                var h = UIFactory.Image(bg.transform, RetroTheme.Raised, Color.white, "H");
                h.rectTransform.Place(cx, 2, cols[i], 14);
                Lbl(h.rectTransform, headers[i], 2, -1, cols[i] - 2, 14, 9, TextAnchor.MiddleCenter);
                cx += cols[i];
            }
            for (int r = 0; r < rows.Length; r++)
            {
                bool sel = r == selected;
                var row = UIFactory.Solid(bg.transform, sel ? RetroTheme.Selection : Color.white, "R");
                row.rectTransform.Place(2, 16 + r * 14, w, 14);
                cx = 0;
                for (int i = 0; i < cols.Length && i < rows[r].Length; i++)
                {
                    Lbl(row.rectTransform, rows[r][i], cx + 2, -1, cols[i] - 3, 14, 9, TextAnchor.MiddleLeft, sel ? Color.white : RetroTheme.Text);
                    cx += cols[i];
                }
            }
        }

        /// <summary>見るべき場所（赤枠＋番号）</summary>
        static void Mark(RectTransform p, float x, float y, float w, float h, string number)
        {
            const float t = 2;
            void Bar(float bx, float by, float bw, float bh)
            {
                var s = UIFactory.Solid(p, Red, "Mark");
                s.raycastTarget = false;
                s.rectTransform.Place(bx, by, bw, bh);
            }
            Bar(x - t, y - t, w + t * 2, t);
            Bar(x - t, y + h, w + t * 2, t);
            Bar(x - t, y, t, h);
            Bar(x + w, y, t, h);
            var badge = UIFactory.Solid(p, Red, "Badge");
            badge.rectTransform.Place(x - 10, y - 10, 16, 16);
            var n = UIFactory.Label(badge.transform, number, 10, TextAnchor.MiddleCenter, Color.white);
            n.fontStyle = FontStyle.Bold;
            n.rectTransform.Stretch();
        }

        static void Legend(RectTransform p, float x, float y, params string[] lines)
        {
            float ly = y;
            foreach (var l in lines)
            {
                var t = Lbl(p, l, x, ly, W - x - 6, 30, 11, TextAnchor.UpperLeft);
                t.horizontalOverflow = HorizontalWrapMode.Wrap;
                ly += 34;
            }
        }

        static void LinkArrow(RectTransform p, float x1, float y, float x2, string label)
        {
            var line = UIFactory.Solid(p, Red, "Link");
            line.raycastTarget = false;
            line.rectTransform.Place(x1, y, x2 - x1, 2);
            var head = UIFactory.Label(p, "▶", 11, TextAnchor.MiddleCenter, Red);
            head.rectTransform.Place(x2 - 8, y - 7, 16, 16);
            var t = UIFactory.Label(p, label, 11, TextAnchor.MiddleCenter, Red);
            t.fontStyle = FontStyle.Bold;
            t.rectTransform.Place(x1, y - 18, x2 - x1, 16);
        }

        // ------------------------------------------------------------ figures

        static float Connection(RectTransform holder, FigureContext ctx)
        {
            const float h = 290;
            var f = Frame(holder, h, "ネットワーク接続監視");
            var w = Window(f, 10, 10, 430, 270, "ネットワーク接続監視 - 接続要求");
            GroupBox(w, 6, 22, 418, 86, "接続要求一覧");
            Table(w, 10, 36, new float[] { 34, 52, 70, 64, 62, 60, 66 },
                new[] { "受付", "要求番号", "利用者", "端末", "接続元", "接続先", "利用システム" },
                new[]
                {
                    new[] { "00:03", "REQ-0001", "田中 美咲", "SALES-PC02", "社内", "MAIL-SRV01", "メール" },
                    new[] { "00:12", "REQ-0002", "佐藤 蓮", "DEV-PC03", "社内", "FILE-SRV01", "共有ファイル" },
                    new[] { "00:20", "REQ-0003", "木村 進", "SALES-PC14", "社内", "MAIL-SRV01", "メール" },
                }, 1);
            GroupBox(w, 6, 112, 290, 150, "要求内容");
            Field(w, 12, 128, 54, 80, "要求番号:", "REQ-0002");
            Field(w, 150, 128, 50, 90, "受付時刻:", "00:12");
            Field(w, 12, 148, 54, 80, "利用者:", "佐藤 蓮");
            Field(w, 150, 148, 50, 90, "社員番号:", "E-0031");
            Field(w, 12, 168, 54, 80, "端末:", ctx.Unlocked(Data.Features.DeviceCheck) ? "DEV-PC03" : "—");
            Field(w, 150, 168, 50, 90, "接続元:", "社内 (10.10.3.21)");
            Field(w, 12, 188, 54, 80, "接続先:", "FILE-SRV01");
            Field(w, 150, 188, 50, 90, "利用システム:", "共有ファイル");
            GroupBox(w, 302, 112, 122, 150, "処理");
            Btn(w, 310, 130, 106, "接続を許可 [A]", 22);
            Btn(w, 310, 156, 106, "接続を拒否 [B]", 22);
            Btn(w, 310, 196, 106, "社員情報を参照 [R]", 20);

            Mark(f, 18, 60, 418, 46, "1");
            Mark(f, 20, 156, 270, 20, "2");
            Mark(f, 318, 204, 110, 22, "3");
            Mark(f, 318, 138, 110, 50, "4");
            Legend(f, 452, 20, "① 要求を選ぶ", "② 利用者・社員番号", "③ 社員情報で検索", "④ 許可 / 拒否");
            return h + 20;
        }

        static float Directory(RectTransform holder, FigureContext ctx)
        {
            const float h = 230;
            bool device = ctx.Unlocked(Data.Features.DeviceCheck);
            var f = Frame(holder, h, "社員情報の検索・参照");
            var w = Window(f, 10, 10, 430, 210, "社員情報の検索・参照");
            GroupBox(w, 6, 22, 418, 54, "検索条件");
            Field(w, 12, 38, 54, 80, "社員番号:", "E-0031");
            Field(w, 152, 38, 34, 90, "氏名:", "");
            Field(w, 12, 56, 54, 80, "部署:", "(すべて)");
            Btn(w, 330, 36, 86, "検索開始(F)");
            GroupBox(w, 6, 80, 418, 104, "検索結果");
            if (device)
                Table(w, 10, 94, new float[] { 56, 64, 74, 80, 74, 56 }, new[] { "社員番号", "氏名", "所属", "役職", "登録端末", "勤務状態" },
                    new[] { new[] { "E-0031", "佐藤 蓮", "開発部", "エンジニア", "DEV-PC03", "勤務中" } }, 0);
            else
                Table(w, 10, 94, new float[] { 66, 80, 90, 100, 66 }, new[] { "社員番号", "氏名", "所属", "役職", "勤務状態" },
                    new[] { new[] { "E-0031", "佐藤 蓮", "開発部", "エンジニア", "勤務中" } }, 0);
            Lbl(w, "件数:  1 件", 10, 188, 100);
            Mark(f, 20, 46, 236, 40, "1");
            Mark(f, 18, 102, 410, 36, "2");
            Legend(f, 452, 20, "① 社員番号・氏名を入れて [検索開始]", "② 出てくれば社員。出なければ社員ではない");
            return h + 20;
        }

        static RectTransform RequestCard(RectTransform f, float x, float y, float h, params (string label, string value)[] fields)
        {
            var w = Window(f, x, y, 250, h, "接続要求（要求内容）");
            float fy = 24;
            foreach (var (label, value) in fields) { Field(w, 8, fy, 72, 160, label, value); fy += 22; }
            return w;
        }

        static float Device(RectTransform holder)
        {
            const float h = 170;
            var f = Frame(holder, h, "端末の確認");
            RequestCard(f, 10, 12, 110, ("利用者:", "佐藤 蓮"), ("社員番号:", "E-0031"), ("端末:", "DEV-PC03"));
            var w = Window(f, 330, 12, 260, 110, "社員情報の検索・参照");
            Table(w, 6, 24, new float[] { 54, 60, 70, 60 }, new[] { "社員番号", "氏名", "登録端末", "勤務状態" },
                new[] { new[] { "E-0031", "佐藤 蓮", "DEV-PC03", "勤務中" } }, 0);
            Mark(f, 90, 80, 162, 18, "1");
            Mark(f, 452, 52, 70, 16, "2");
            LinkArrow(f, 262, 90, 450, "同じ？");
            Legend(f, 14, 132, "① 要求の「端末」と ② 社員の「登録端末」が同じなら OK。違えば社員本人でも拒否。");
            return h + 20;
        }

        static float Detail(RectTransform holder)
        {
            const float h = 230;
            var f = Frame(holder, h, "社員情報の参照（詳細）");
            var w = Window(f, 10, 10, 430, 210, "社員情報の参照 - E-0031 佐藤 蓮");
            Field(w, 10, 24, 60, 80, "社員番号:", "E-0031");
            Field(w, 160, 24, 52, 70, "勤務状態:", "勤務中");
            Field(w, 10, 44, 60, 150, "氏名:", "佐藤 蓮");
            Field(w, 10, 64, 60, 150, "所属:", "開発部");
            Field(w, 10, 92, 60, 100, "登録端末:", "DEV-PC03");
            Field(w, 10, 112, 60, 100, "VPN利用:", "許可");
            Field(w, 180, 112, 52, 90, "VPN端末:", "NB-DEV03");
            var photo = UIFactory.Image(w, RetroTheme.Field, Color.white, "Photo");
            photo.rectTransform.Place(340, 24, 76, 92);
            var head = UIFactory.Solid(photo.transform, new Color(0.75f, 0.8f, 0.88f), "PhotoBg");
            head.rectTransform.Stretch(3, 3, 3, 3);
            Lbl(photo.rectTransform, "顔写真", 0, 70, 76, 16, 9, TextAnchor.MiddleCenter, RetroTheme.Shadow);
            GroupBox(w, 6, 140, 418, 62, "【システム利用権限】（DAY3 から）");
            Mark(f, 18, 32, 420, 54, "1");
            Mark(f, 18, 100, 312, 38, "2");
            Legend(f, 452, 20, "① 顔写真・所属・勤務状態", "② 登録端末・VPN利用・VPN端末");
            return h + 20;
        }

        static RectTransform PermissionList(RectTransform f, float x, float y, string title, (string name, bool has)[] rows, int highlight, out float rowY)
        {
            var w = Window(f, x, y, 250, 34 + rows.Length * 19, "社員情報の参照（権限欄）");
            Lbl(w, title, 8, 20, 230, 14, 10, TextAnchor.MiddleLeft, RetroTheme.TextBlue);
            float ry = 36;
            rowY = 0;
            for (int i = 0; i < rows.Length; i++)
            {
                Lbl(w, rows[i].name, 14, ry, 120);
                var v = UIFactory.Image(w, RetroTheme.Field, Color.white, "V");
                v.rectTransform.Place(150, ry, 36, 16);
                Lbl(v.rectTransform, rows[i].has ? "  ○" : "  ×", 0, 0, 36);
                if (i == highlight) rowY = y + ry;
                ry += 19;
            }
            return w;
        }

        static float Permission(RectTransform holder)
        {
            const float h = 200;
            var f = Frame(holder, h, "権限の確認");
            RequestCard(f, 10, 12, 110, ("利用者:", "伊藤 さくら"), ("利用システム:", "人事システム"), ("必要な権限:", "人事システム"));
            PermissionList(f, 340, 12, "【システム利用権限】",
                new[] { ("メール・Web", true), ("共有ファイル", true), ("開発環境", false), ("人事システム", true), ("経理システム", false) }, 3, out float rowY);
            Mark(f, 90, 80, 162, 18, "1");
            Mark(f, 352, rowY - 2, 178, 20, "2");
            LinkArrow(f, 262, 90, 350, "同じ名前");
            Legend(f, 14, 150, "① 要求の「必要な権限」と同じ名前の行を ② 権限欄で探す。○ なら許可、× なら拒否。");
            return h + 20;
        }

        static float Admin(RectTransform holder)
        {
            const float h = 190;
            var f = Frame(holder, h, "管理者権限の確認");
            RequestCard(f, 10, 12, 110, ("利用者:", "高橋 優斗"), ("利用システム:", "リモート管理"), ("必要な権限:", "サーバー管理者"));
            PermissionList(f, 340, 12, "【管理者権限】",
                new[] { ("ローカル管理者", true), ("サーバー管理者", false), ("ネットワーク管理", false) }, 1, out float rowY);
            Mark(f, 90, 80, 162, 18, "1");
            Mark(f, 352, rowY - 2, 178, 20, "2");
            LinkArrow(f, 262, 90, 350, "同じ名前");
            Lbl(f, "← ローカル管理者は別物", 532, rowY - 19, 70, 18, 9, TextAnchor.MiddleLeft, Red);
            Legend(f, 14, 140, "① 必要な権限が「サーバー管理者」なら、② 【管理者権限】の「サーバー管理者」を見る（この例は × → 拒否）。");
            return h + 20;
        }

        static float HelpDesk(RectTransform holder)
        {
            const float h = 250;
            var f = Frame(holder, h, "ITサポート受付管理");
            var w = Window(f, 10, 10, 430, 230, "ITサポート受付管理");
            GroupBox(w, 6, 22, 418, 66, "受付一覧");
            Table(w, 10, 36, new float[] { 56, 60, 150, 50, 50 }, new[] { "受付番号", "依頼者", "件名", "区分", "状態" },
                new[] { new[] { "HD-0001", "鈴木 健太", "インターネットにつながらない", "障害", "未対応" }, new[] { "HD-0002", "中村 彩", "パスワードを教えてほしいと…", "相談", "回答済" } }, 0);
            GroupBox(w, 6, 92, 250, 130, "問い合わせ内容");
            Lbl(w, "[00:40] 鈴木 健太:\nさっきから急にインターネットに\nつながらなくなりました…", 14, 108, 236, 60, 10, TextAnchor.UpperLeft);
            GroupBox(w, 262, 92, 162, 130, "対応");
            Btn(w, 270, 112, 146, "リモート接続(R)", 22);
            Btn(w, 270, 140, 146, "対応完了を報告(P)", 20);
            Lbl(w, "（相談のときは回答を選んで\n　[回答を送信]）", 270, 170, 150, 40, 9, TextAnchor.UpperLeft, RetroTheme.Shadow);
            Mark(f, 18, 44, 418, 46, "1");
            Mark(f, 20, 114, 236, 66, "2");
            Mark(f, 278, 120, 150, 46, "3");
            Legend(f, 452, 20, "① 相談か障害かを見る", "② 社員からのメッセージ", "③ 回答する／リモート接続して直す");
            return h + 20;
        }

        static float External(RectTransform holder)
        {
            const float h = 200;
            var f = Frame(holder, h, "社外からの接続");
            RequestCard(f, 10, 12, 132, ("利用者:", "佐藤 蓮"), ("接続元:", "社外 (100.84.2.10)"), ("利用システム:", "VPN接続"), ("端末:", "NB-DEV03"));
            var w = Window(f, 340, 12, 250, 92, "社員情報の参照（詳細）");
            Field(w, 8, 24, 72, 150, "VPN利用:", "許可");
            Field(w, 8, 46, 72, 150, "VPN端末:", "NB-DEV03");
            Mark(f, 90, 80, 162, 18, "1");
            Mark(f, 420, 36, 162, 18, "2");
            Mark(f, 90, 102, 162, 18, "3");
            Mark(f, 420, 58, 162, 18, "3");
            Legend(f, 14, 154, "接続元が「社外」なら: ① VPN接続か　② VPN利用が許可か　③ 端末が VPN端末と同じか。1つでも違えば拒否。");
            return h + 20;
        }

        static float Log(RectTransform holder)
        {
            const float h = 230;
            var f = Frame(holder, h, "システムログの検索・参照");
            var w = Window(f, 10, 10, 430, 210, "システムログの検索・参照");
            GroupBox(w, 6, 22, 418, 40, "検索条件");
            Field(w, 12, 38, 70, 120, "キーワード(K):", "203.0.113.45");
            Btn(w, 330, 36, 86, "検索開始(F)");
            Table(w, 8, 68, new float[] { 54, 80, 76, 34, 40, 120 }, new[] { "時刻", "接続元", "接続先", "種別", "結果", "詳細" },
                new[]
                {
                    new[] { "00:45:10", "203.0.113.45", "ADMIN-SRV01", "FW", "DENY", "TCP 22" },
                    new[] { "00:45:34", "203.0.113.45", "ADMIN-SRV01", "FW", "DENY", "TCP 3389" },
                    new[] { "00:46:02", "203.0.113.45", "ADMIN-SRV01", "FW", "DENY", "TCP 445" },
                    new[] { "00:51:20", "203.0.113.45", "ADMIN-SRV01", "CONN-REQ", "PENDING", "REQ-0005 管理ログイン" },
                }, 3);
            Mark(f, 22, 46, 196, 20, "1");
            Mark(f, 20, 92, 412, 44, "2");
            Legend(f, 452, 20, "① 警告の接続元IPを入れて検索", "② 同じIPが前に何をしていたか（ポートスキャン → 管理ログイン）");
            return h + 20;
        }

        static float Remote(RectTransform holder)
        {
            const float h = 250;
            var f = Frame(holder, h, "リモートサポート");
            var w = Window(f, 10, 10, 430, 230, "リモートサポート - SALES-PC05 (鈴木 健太)");
            Btn(w, 8, 22, 60, "切断(X)");
            Btn(w, 72, 22, 110, "対応完了を報告(P)");
            var screen = UIFactory.Solid(w, RetroTheme.RemoteDesktop, "Desk");
            screen.rectTransform.Place(8, 44, 414, 178);
            var desk = screen.rectTransform;
            string[] icons = { "マイ コンピュータ", "ブラウザー", "プリンター", "コマンド プロンプト" };
            for (int i = 0; i < icons.Length; i++)
            {
                var ic = UIFactory.Solid(desk, new Color(1, 1, 1, 0.85f), "Ic");
                ic.rectTransform.Place(14, 8 + i * 34, 18, 18);
                Lbl(desk, icons[i], 36, 8 + i * 34, 110, 18, 9, TextAnchor.MiddleLeft, Color.white);
            }
            var bar = UIFactory.Image(desk, RetroTheme.ThinRaised, Color.white, "Taskbar");
            bar.rectTransform.Place(0, 156, 414, 22);
            Btn(bar.rectTransform, 2, 2, 60, "スタート");
            var tray = UIFactory.Image(bar.rectTransform, RetroTheme.ThinSunken, Color.white, "Tray");
            tray.rectTransform.Place(330, 2, 82, 18);
            Lbl(tray.rectTransform, "LAN  PRN  01:12", 4, 0, 78, 18, 9);
            Mark(f, 348, 214, 84, 18, "1");
            Mark(f, 20, 214, 62, 18, "2");
            Mark(f, 82, 32, 112, 18, "3");
            Legend(f, 452, 20, "① 通知領域（ネットワーク・プリンター）", "② スタートから設定・コマンド プロンプト", "③ 直ったら報告");
            return h + 20;
        }
    }
}
