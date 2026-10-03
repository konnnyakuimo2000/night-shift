using System;
using System.Collections.Generic;
using NightShift.Data;
using UnityEngine;
using UnityEngine.UI;

namespace NightShift.UI
{
    /// <summary>図の描画に必要な情報（その日のルール・解放状況・ネットワーク構成）</summary>
    public class FigureContext
    {
        public DayDefinition Day;
        public Func<string, bool> Unlocked;
        public IReadOnlyList<NetworkNodeDefinition> Nodes;
        public IReadOnlyList<PermissionDefinition> Permissions;
    }

    /// <summary>
    /// マニュアル用の図。画像素材は使わず UI 部品で描く。
    /// 本文中の {FIG:ID} に対応する。その日のルール・解放状況に合わせて内容が変わる図もある。
    /// </summary>
    public static class ManualFigures
    {
        public const float Width = 600;

        static readonly Color Ink = new Color(0.15f, 0.15f, 0.2f);
        static readonly Color Paper = new Color(0.97f, 0.97f, 0.95f);
        static readonly Color StepFill = new Color(0.88f, 0.92f, 1f);
        static readonly Color AskFill = new Color(1f, 0.96f, 0.8f);
        static readonly Color DenyFill = new Color(1f, 0.85f, 0.85f);
        static readonly Color DenyInk = new Color(0.7f, 0f, 0f);
        static readonly Color AllowFill = new Color(0.85f, 0.92f, 1f);
        static readonly Color AllowInk = new Color(0f, 0.19f, 0.63f);
        static readonly Color CheckInk = new Color(0.54f, 0.29f, 0f);
        static readonly Color Callout = new Color(0.8f, 0.1f, 0.1f);

        public static float Build(string id, RectTransform holder, FigureContext ctx)
        {
            switch (id)
            {
                case "REVIEW_FLOW": return ReviewFlow(holder, ctx);
                case "CONNECTION_SCREEN": return ConnectionScreen(holder);
                case "DIRECTORY_SCREEN": return DirectoryScreen(holder, ctx);
                case "PERMISSION_MATCH": return PermissionMatch(holder, ctx);
                case "NETWORK_ZONES": return NetworkZones(holder, ctx);
                case "REMOTE_FLOW": return RemoteFlow(holder);
                default: return 0;
            }
        }

        // ------------------------------------------------------------ primitives
        static RectTransform Frame(RectTransform holder, float h, string caption)
        {
            var bg = UIFactory.Image(holder, RetroTheme.Field, Paper, "FigureFrame");
            bg.rectTransform.Place(0, 0, Width, h);
            if (!string.IsNullOrEmpty(caption))
            {
                var c = UIFactory.Label(holder, "図: " + caption, 11, TextAnchor.MiddleLeft, RetroTheme.Shadow);
                c.rectTransform.Place(4, h + 2, Width, 16);
            }
            return bg.rectTransform;
        }

        static Image Box(RectTransform p, float x, float y, float w, float h, string text, Color fill, Color ink, int size = 12, bool bold = false)
        {
            var border = UIFactory.Solid(p, ink, "Box");
            border.rectTransform.Place(x, y, w, h);
            var inner = UIFactory.Solid(border.transform, fill, "Fill");
            inner.rectTransform.Stretch(1, 1, 1, 1);
            if (!string.IsNullOrEmpty(text))
            {
                var t = UIFactory.Label(inner.transform, text, size, TextAnchor.MiddleCenter, ink);
                t.font = RetroTheme.ReadingFont;
                t.rectTransform.Stretch(3, 1, 3, 1);
                if (bold) t.fontStyle = FontStyle.Bold;
            }
            return border;
        }

        static void Line(RectTransform p, Vector2 a, Vector2 b, Color c, float thickness = 2)
        {
            var img = UIFactory.Solid(p, c, "Line");
            img.raycastTarget = false;
            var rt = img.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0, 0.5f);
            var A = new Vector2(a.x, -a.y);
            var B = new Vector2(b.x, -b.y);
            rt.anchoredPosition = A;
            var d = B - A;
            rt.sizeDelta = new Vector2(d.magnitude, thickness);
            rt.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
        }

        /// <summary>水平または垂直の矢印</summary>
        static void Arrow(RectTransform p, Vector2 a, Vector2 b, Color c, string label = null)
        {
            Line(p, a, b, c);
            string head = Mathf.Abs(b.x - a.x) > Mathf.Abs(b.y - a.y) ? (b.x > a.x ? "▶" : "◀") : (b.y > a.y ? "▼" : "▲");
            var h = UIFactory.Label(p, head, 11, TextAnchor.MiddleCenter, c);
            h.rectTransform.Place(b.x - 8, b.y - 8, 16, 16);
            if (!string.IsNullOrEmpty(label))
            {
                var t = UIFactory.Label(p, label, 11, TextAnchor.MiddleCenter, c);
                t.fontStyle = FontStyle.Bold;
                var mid = (a + b) / 2;
                bool horizontal = Mathf.Abs(b.x - a.x) > Mathf.Abs(b.y - a.y);
                t.rectTransform.Place(horizontal ? mid.x - 30 : mid.x + 4, horizontal ? mid.y - 18 : mid.y - 8, 60, 16);
                if (!horizontal) t.alignment = TextAnchor.MiddleLeft;
            }
        }

        static void Badge(RectTransform p, float x, float y, string number)
        {
            var b = UIFactory.Solid(p, Callout, "Badge");
            b.rectTransform.Place(x, y, 18, 18);
            var t = UIFactory.Label(b.transform, number, 11, TextAnchor.MiddleCenter, Color.white);
            t.fontStyle = FontStyle.Bold;
            t.rectTransform.Stretch();
        }

        static Text Text(RectTransform p, string s, float x, float y, float w, float h, int size = 11, TextAnchor anchor = TextAnchor.MiddleLeft, Color? color = null)
        {
            var t = UIFactory.Label(p, s, size, anchor, color ?? Ink);
            t.rectTransform.Place(x, y, w, h);
            return t;
        }

        /// <summary>簡易ウィンドウ（画面図用）</summary>
        static RectTransform MockWindow(RectTransform p, float x, float y, float w, float h, string title)
        {
            var win = UIFactory.Image(p, RetroTheme.Raised, Color.white, "MockWindow");
            win.rectTransform.Place(x, y, w, h);
            var bar = UIFactory.Image(win.transform, RetroTheme.GradientSprite(RetroTheme.TitleActiveA, RetroTheme.TitleActiveB), Color.white, "Bar");
            bar.rectTransform.StretchH(3, 14, 3, 3);
            var t = UIFactory.Label(bar.transform, " " + title, 10, TextAnchor.MiddleLeft, Color.white);
            t.rectTransform.Stretch();
            t.fontStyle = FontStyle.Bold;
            return win.rectTransform;
        }

        static void MockButton(RectTransform p, float x, float y, float w, string label)
        {
            var b = UIFactory.Image(p, RetroTheme.Raised, Color.white, "MockButton");
            b.rectTransform.Place(x, y, w, 18);
            var t = UIFactory.Label(b.transform, label, 10, TextAnchor.MiddleCenter);
            t.rectTransform.Stretch();
        }

        static void MockRows(RectTransform p, float x, float y, float w, int rows, int selected, float[] cols)
        {
            var bg = UIFactory.Image(p, RetroTheme.Field, Color.white, "MockTable");
            bg.rectTransform.Place(x, y, w, 14 + rows * 13 + 4);
            var head = UIFactory.Solid(bg.transform, RetroTheme.Face, "Head");
            head.rectTransform.Place(2, 2, w - 4, 12);
            for (int r = 0; r < rows; r++)
            {
                var row = UIFactory.Solid(bg.transform, r == selected ? RetroTheme.Selection : Color.white, "Row");
                row.rectTransform.Place(2, 16 + r * 13, w - 4, 12);
                float cx = 4;
                foreach (var cw in cols)
                {
                    var cell = UIFactory.Solid(row.transform, r == selected ? new Color(0.7f, 0.75f, 0.9f) : new Color(0.75f, 0.75f, 0.75f), "Cell");
                    cell.rectTransform.Place(cx, 4, cw * 0.7f, 4);
                    cx += cw;
                }
            }
        }

        // ------------------------------------------------------------ figures

        /// <summary>接続審査の判断フロー（その日のルールに合わせて段階が増える）</summary>
        static float ReviewFlow(RectTransform holder, FigureContext ctx)
        {
            var policy = ctx.Day != null ? ctx.Day.policy : new PolicyRuleSet();
            bool device = ctx.Unlocked(Features.DeviceCheck) && policy.blockUnregisteredDevice;
            bool permission = ctx.Unlocked(Features.EmployeePermissions) && policy.requirePermission;
            bool external = policy.blockExternalToAdministrative;
            bool vpn = policy.requireVpnPermission;

            var asks = new List<(string q, string no)>();
            asks.Add(("社員として登録されている？", "いいえ"));
            if (device) asks.Add(("端末は本人の登録端末？", "いいえ"));
            if (permission) asks.Add(("必要な権限が、社員情報で ○？", "いいえ"));
            if (external) asks.Add(("社外から管理系システムへの接続ではない？", "いいえ"));
            if (vpn) asks.Add(("社外からなら、VPN許可のある社員の登録端末？", "いいえ"));

            const float stepH = 30, gap = 18, cx = 60, cw = 330, denyX = 450, denyW = 110;
            float h = 20 + (2 + asks.Count + 1) * (stepH + gap);
            var f = Frame(holder, h, "接続審査の流れ（本日のルール）");
            float y = 12;
            void Down(float fromY) => Arrow(f, new Vector2(cx + cw / 2, fromY), new Vector2(cx + cw / 2, fromY + gap), Ink);

            Box(f, cx, y, cw, stepH, "① 接続要求を一覧から選ぶ", StepFill, Ink); Down(y + stepH); y += stepH + gap;
            Box(f, cx, y, cw, stepH, "② 社員情報で利用者を検索する", StepFill, Ink); Down(y + stepH); y += stepH + gap;
            foreach (var (q, no) in asks)
            {
                Box(f, cx, y, cw, stepH, q, AskFill, CheckInk, 12, true);
                Arrow(f, new Vector2(cx + cw, y + stepH / 2), new Vector2(denyX, y + stepH / 2), DenyInk, no);
                Box(f, denyX, y, denyW, stepH, "拒否", DenyFill, DenyInk, 13, true);
                Down(y + stepH);
                Text(f, "はい", cx + cw / 2 + 6, y + stepH + 1, 40, 16, 11, TextAnchor.MiddleLeft, AllowInk).fontStyle = FontStyle.Bold;
                y += stepH + gap;
            }
            Box(f, cx, y, cw, stepH, "許可", AllowFill, AllowInk, 13, true);
            return h + 20;
        }

        /// <summary>ネットワーク接続監視の画面図</summary>
        static float ConnectionScreen(RectTransform holder)
        {
            const float h = 250;
            var f = Frame(holder, h, "ネットワーク接続監視の画面");
            var w = MockWindow(f, 20, 10, 400, 228, "ネットワーク接続監視 - 接続要求");
            Text(w, "接続要求一覧", 8, 22, 120, 12, 10);
            MockRows(w, 8, 36, 384, 4, 1, new float[] { 30, 50, 70, 70, 80, 60 });
            Text(w, "要求内容", 8, 112, 120, 12, 10);
            for (int i = 0; i < 4; i++)
            {
                Text(w, "■■■:", 8, 128 + i * 20, 40, 14, 9);
                var v = UIFactory.Image(w, RetroTheme.Field, Color.white, "V"); v.rectTransform.Place(48, 128 + i * 20, 100, 14);
                Text(w, "■■■:", 156, 128 + i * 20, 40, 14, 9);
                var v2 = UIFactory.Image(w, RetroTheme.Field, Color.white, "V"); v2.rectTransform.Place(196, 128 + i * 20, 90, 14);
            }
            MockButton(w, 296, 128, 96, "接続を許可(A)");
            MockButton(w, 296, 150, 96, "接続を拒否(B)");
            MockButton(w, 296, 180, 96, "社員情報を参照(R)");

            Badge(f, 6, 50, "1");
            Badge(f, 6, 140, "2");
            Badge(f, 424, 140, "3");
            Badge(f, 424, 190, "4");
            Text(f, "① 接続要求の一覧", 448, 30, 150, 18, 12).fontStyle = FontStyle.Bold;
            Text(f, "② 選んだ要求の内容\n　（利用者・社員番号・接続先など）", 448, 52, 150, 34, 11);
            Text(f, "③ 確認してから\n　許可／拒否", 448, 92, 150, 34, 11);
            Text(f, "④ 利用者を社員情報で\n　検索する", 448, 132, 150, 34, 11);
            return h + 20;
        }

        /// <summary>社員情報の検索画面の図</summary>
        static float DirectoryScreen(RectTransform holder, FigureContext ctx)
        {
            bool detail = ctx.Unlocked(Features.EmployeeDetail);
            const float h = 210;
            var f = Frame(holder, h, "社員情報の検索・参照の画面");
            var w = MockWindow(f, 20, 10, 400, 190, "社員情報の検索・参照");
            Text(w, "社員番号:", 8, 24, 50, 14, 9);
            var a = UIFactory.Image(w, RetroTheme.Field, Color.white, "F"); a.rectTransform.Place(58, 24, 80, 14);
            Text(w, "氏名:", 150, 24, 30, 14, 9);
            var b = UIFactory.Image(w, RetroTheme.Field, Color.white, "F"); b.rectTransform.Place(180, 24, 100, 14);
            MockButton(w, 296, 22, 96, "検索開始(F)");
            MockRows(w, 8, 48, 384, 6, 2, new float[] { 50, 70, 80, 70, 60 });
            if (detail) MockButton(w, 296, 160, 96, "詳細表示(D)");
            else Text(w, "件数:  n 件", 8, 160, 100, 16, 10);

            Badge(f, 6, 30, "1");
            Badge(f, 424, 30, "2");
            Badge(f, 6, 100, "3");
            Text(f, "① 社員番号や氏名を入力", 448, 24, 150, 18, 12).fontStyle = FontStyle.Bold;
            Text(f, "② 検索開始", 448, 46, 150, 18, 11);
            Text(f, "③ 結果の一覧\n　見つからなければ\n　社員ではない", 448, 68, 150, 52, 11);
            if (detail)
            {
                Badge(f, 424, 168, "4");
                Text(f, "④ 詳細（顔写真・登録端末\n　など）を表示", 448, 126, 150, 34, 11);
            }
            return h + 20;
        }

        /// <summary>権限の照合の図</summary>
        static float PermissionMatch(RectTransform holder, FigureContext ctx)
        {
            const float h = 150;
            var f = Frame(holder, h, "権限の照らし合わせ方（例）");
            if (ctx.Unlocked(Features.PermissionHint))
            {
                // DAY3: 要求に表示された「必要な権限」と社員の権限欄を見比べるだけ
                Box(f, 40, 16, 230, 74, "接続要求\n利用システム: 共有ファイル（閲覧）\n必要な権限: 共有ファイル", AskFill, CheckInk, 12);
                Arrow(f, new Vector2(270, 53), new Vector2(328, 53), Ink, "見比べる");
                Box(f, 330, 16, 230, 74, "社員情報（権限欄）\n共有ファイル: ○", StepFill, Ink, 12);
            }
            else
            {
                Box(f, 12, 16, 170, 74, "接続要求\n接続先: FILE-SRV01\n利用システム: 共有ファイル（閲覧）", StepFill, Ink, 11);
                Arrow(f, new Vector2(182, 53), new Vector2(214, 53), Ink);
                Box(f, 216, 16, 170, 74, "社内サーバー構成資料\n（早見表）\n共有ファイル（閲覧）→ 共有ファイル", AskFill, CheckInk, 11);
                Arrow(f, new Vector2(386, 53), new Vector2(418, 53), Ink);
                Box(f, 420, 16, 168, 74, "社員情報（権限欄）\n共有ファイル: ○", StepFill, Ink, 11);
            }
            Text(f, "○ なら", 300, 100, 60, 18, 12, TextAnchor.MiddleRight, AllowInk).fontStyle = FontStyle.Bold;
            Box(f, 362, 98, 70, 22, "許可", AllowFill, AllowInk, 12, true);
            Text(f, "× なら", 436, 100, 60, 18, 12, TextAnchor.MiddleRight, DenyInk).fontStyle = FontStyle.Bold;
            Box(f, 498, 98, 70, 22, "拒否", DenyFill, DenyInk, 12, true);
            return h + 20;
        }

        /// <summary>ネットワーク区分の図（サーバー定義から生成）</summary>
        static float NetworkZones(RectTransform holder, FigureContext ctx)
        {
            var dmz = new List<NetworkNodeDefinition>();
            var internal_ = new List<NetworkNodeDefinition>();
            if (ctx.Nodes != null)
                foreach (var n in ctx.Nodes)
                {
                    if (n == null || n.role != NodeRole.Server) continue;
                    if (n.zone == NodeZone.DMZ) dmz.Add(n); else internal_.Add(n);
                }
            float h = 70 + Mathf.Max(3, internal_.Count) * 26 + 40;
            var f = Frame(holder, h, "社内ネットワークの区分");

            Box(f, 12, 30, 90, 40, "社外\nINTERNET", new Color(0.92f, 0.92f, 0.92f), Ink, 11, true);
            Arrow(f, new Vector2(102, 50), new Vector2(140, 50), Ink);
            Box(f, 142, 24, 90, 52, "FIREWALL", DenyFill, DenyInk, 11, true);

            // DMZ
            var dz = UIFactory.Image(f, RetroTheme.Etched, Color.white, "DMZ");
            dz.rectTransform.Place(270, 10, 310, 50);
            Text(f, "公開 (DMZ)", 276, 12, 120, 14, 10, TextAnchor.MiddleLeft, CheckInk).fontStyle = FontStyle.Bold;
            float x = 280;
            foreach (var n in dmz) { Box(f, x, 28, 110, 24, n.nodeId, Color.white, Ink, 11); x += 116; }
            Arrow(f, new Vector2(232, 38), new Vector2(270, 38), Ink);

            // 社内
            float top = 70;
            float zoneH = Mathf.Max(3, internal_.Count) * 26 + 24;
            var iz = UIFactory.Image(f, RetroTheme.Etched, Color.white, "Internal");
            iz.rectTransform.Place(270, top, 310, zoneH);
            Text(f, "社内（社外からは VPN 経由）", 276, top + 2, 300, 14, 10, TextAnchor.MiddleLeft, CheckInk).fontStyle = FontStyle.Bold;
            float y = top + 20;
            foreach (var n in internal_)
            {
                bool admin = n.isAdministrative;
                Box(f, 280, y, 170, 22, n.nodeId + (admin ? "  （管理系）" : ""), admin ? DenyFill : Color.white, admin ? DenyInk : Ink, 11, admin);
                y += 26;
            }
            Arrow(f, new Vector2(187, 76), new Vector2(187, top + 30), Ink);
            Arrow(f, new Vector2(187, top + 30), new Vector2(270, top + 30), Ink);

            Box(f, 470, top + 24, 100, 46, "社員PC\n(登録端末)", StepFill, Ink, 11, true);
            Arrow(f, new Vector2(470, top + 47), new Vector2(452, top + 47), Ink);
            return h + 20;
        }

        /// <summary>リモートサポートの流れ</summary>
        static float RemoteFlow(RectTransform holder)
        {
            const float h = 96;
            var f = Frame(holder, h, "リモートサポートの流れ");
            string[] steps = { "1. 症状を\n確認", "2. リモート\n接続", "3. 原因を\n調べる", "4. 対処\nする", "5. 動作を\n確認", "6. 報告\nする" };
            float bw = 80, gap = 18, x = 10;
            for (int i = 0; i < steps.Length; i++)
            {
                bool key = i == 4;
                Box(f, x, 24, bw, 48, steps[i], key ? AskFill : StepFill, key ? CheckInk : Ink, 12, key);
                if (i < steps.Length - 1) Arrow(f, new Vector2(x + bw, 48), new Vector2(x + bw + gap, 48), Ink);
                x += bw + gap;
            }
            return h + 20;
        }
    }
}
