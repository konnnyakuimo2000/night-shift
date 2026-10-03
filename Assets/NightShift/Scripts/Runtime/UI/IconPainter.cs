using UnityEngine;

namespace NightShift.UI
{
    /// <summary>オリジナルの16x16業務用アイコン（コードで描画）</summary>
    public static class IconPainter
    {
        static Color C(int r, int g, int b) => new Color(r / 255f, g / 255f, b / 255f, 1f);
        static readonly Color K = C(0, 0, 0), W = C(255, 255, 255), G = C(128, 128, 128), L = C(200, 200, 200), D = C(64, 64, 64);
        static readonly Color Nv = C(0, 0, 128), Bl = C(40, 90, 200), Cy = C(0, 200, 220), Ye = C(250, 210, 40), Ys = C(200, 150, 0);
        static readonly Color Rd = C(200, 30, 30), Gr = C(30, 150, 50), Br = C(150, 90, 40), Sk = C(240, 200, 160), Bg = C(236, 226, 196);

        public static void Paint(string name, RetroTheme.IconCanvas c)
        {
            switch (name)
            {
                case "pc": Pc(c); break;
                case "network": Network(c); break;
                case "people": People(c); break;
                case "log": Doc(c, true); break;
                case "doc": Doc(c, false); break;
                case "helpdesk": HelpDesk(c); break;
                case "shield": Shield(c); break;
                case "gear": Gear(c); break;
                case "exit": Exit(c); break;
                case "folder": Folder(c); break;
                case "printer": Printer(c); break;
                case "terminal": Terminal(c); break;
                case "globe": Globe(c); break;
                case "warning": Warning(c); break;
                case "info": Info(c); break;
                case "server": Server(c); break;
                case "mail": Mail(c); break;
                case "lock": Lock(c); break;
                case "run": Run(c); break;
                case "taskmgr": TaskMgr(c); break;
                case "search": Search(c); break;
                case "remote": Remote(c); break;
                case "clock": Clock(c); break;
                case "check": Check(c); break;
                case "wifi": Wifi(c, true); break;
                case "wifi_off": Wifi(c, false); break;
                case "drive": Drive(c); break;
                case "start": Start(c); break;
                case "debug": Debug(c); break;
                case "firewall": Firewall(c); break;
                case "internet": Globe(c); break;
                case "cloud": Globe(c); break;
                default: c.Box(2, 2, 12, 12, L, D); break;
            }
        }

        static void Pc(RetroTheme.IconCanvas c)
        {
            c.Box(1, 1, 14, 10, L, D);
            c.Rect(3, 3, 10, 6, Nv);
            c.Rect(4, 4, 3, 1, Cy);
            c.Rect(6, 11, 4, 2, G);
            c.Box(3, 13, 10, 3, L, D);
        }

        static void Network(RetroTheme.IconCanvas c)
        {
            c.Box(5, 0, 6, 5, L, D); c.Rect(6, 1, 4, 2, Nv);
            c.Box(0, 10, 6, 5, L, D); c.Rect(1, 11, 4, 2, Nv);
            c.Box(10, 10, 6, 5, L, D); c.Rect(11, 11, 4, 2, Nv);
            c.Line(8, 5, 8, 8, K); c.Line(3, 8, 13, 8, K); c.Line(3, 8, 3, 10, K); c.Line(13, 8, 13, 10, K);
        }

        static void People(RetroTheme.IconCanvas c)
        {
            c.Circle(11, 4.5f, 2.5f, Sk); c.Rect(8, 8, 7, 7, Gr);
            c.Circle(5.5f, 5.5f, 3f, Sk); c.Rect(1, 10, 9, 6, Bl);
            c.Set(4, 5, K); c.Set(7, 5, K);
        }

        static void Doc(RetroTheme.IconCanvas c, bool lined)
        {
            c.Box(3, 1, 10, 14, W, D);
            c.Rect(10, 1, 3, 3, L);
            for (int y = 4; y < 14; y += 2) c.Rect(5, y, lined ? 6 : 5, 1, lined ? Nv : G);
        }

        static void HelpDesk(RetroTheme.IconCanvas c)
        {
            c.Rect(2, 2, 12, 3, D); c.Rect(1, 3, 3, 4, D); c.Rect(12, 3, 3, 4, D);
            c.Box(3, 7, 10, 8, Bg, D);
            for (int y = 9; y < 14; y += 2) for (int x = 5; x < 12; x += 2) c.Set(x, y, K);
            c.Rect(6, 5, 4, 2, Rd);
        }

        static void Shield(RetroTheme.IconCanvas c)
        {
            for (int y = 1; y < 15; y++)
            {
                int inset = y < 9 ? 1 : (y - 8);
                c.Rect(1 + inset, y, 14 - inset * 2, 1, y == 1 ? D : Bl);
                c.Set(1 + inset, y, D); c.Set(14 - inset, y, D);
            }
            c.Rect(7, 2, 2, 11, Ye);
            c.Rect(3, 6, 10, 2, Ye);
        }

        static void Gear(RetroTheme.IconCanvas c)
        {
            c.Circle(8, 8, 6f, Ys);
            c.Rect(6, 0, 4, 16, Ys); c.Rect(0, 6, 16, 4, Ys);
            c.Circle(8, 8, 4.5f, Ye);
            c.Circle(8, 8, 2f, W);
            c.Circle(8, 8, 1f, G);
        }

        static void Exit(RetroTheme.IconCanvas c)
        {
            c.Box(2, 1, 8, 14, Br, K);
            c.Rect(3, 2, 6, 12, C(180, 120, 60));
            c.Set(8, 8, Ye);
            c.Rect(9, 7, 5, 2, Gr); c.Line(12, 5, 15, 8, Gr); c.Line(12, 10, 15, 7, Gr);
        }

        static void Folder(RetroTheme.IconCanvas c)
        {
            c.Box(1, 3, 6, 3, Ys, D);
            c.Box(1, 5, 14, 10, Ye, D);
            c.Rect(2, 6, 12, 1, C(255, 240, 150));
        }

        static void Printer(RetroTheme.IconCanvas c)
        {
            c.Box(4, 1, 8, 6, W, D);
            c.Box(1, 6, 14, 6, L, D);
            c.Set(12, 8, Gr);
            c.Box(3, 11, 10, 4, W, D);
            c.Rect(5, 13, 6, 1, G);
        }

        static void Terminal(RetroTheme.IconCanvas c)
        {
            c.Box(1, 2, 14, 12, K, L);
            c.Rect(1, 2, 14, 2, Nv);
            c.Line(3, 6, 5, 8, W); c.Line(5, 8, 3, 10, W);
            c.Rect(7, 10, 4, 1, W);
        }

        static void Globe(RetroTheme.IconCanvas c)
        {
            c.Circle(8, 8, 7f, D);
            c.Circle(8, 8, 6f, Bl);
            c.Rect(4, 4, 4, 3, Gr); c.Rect(9, 8, 4, 4, Gr); c.Rect(5, 10, 2, 2, Gr);
            c.Set(5, 4, C(120, 200, 255));
        }

        static void Warning(RetroTheme.IconCanvas c)
        {
            for (int y = 1; y < 15; y++)
            {
                int half = y / 2;
                c.Rect(8 - half - 1, y, half * 2 + 2, 1, Ye);
                c.Set(8 - half - 1, y, K); c.Set(8 + half, y, K);
            }
            c.Rect(1, 14, 14, 1, K);
            c.Rect(7, 5, 2, 5, K); c.Rect(7, 11, 2, 2, K);
        }

        static void Info(RetroTheme.IconCanvas c)
        {
            c.Circle(8, 8, 7f, Nv);
            c.Circle(8, 8, 6f, W);
            c.Rect(7, 3, 2, 2, Nv); c.Rect(7, 6, 2, 7, Nv);
        }

        static void Server(RetroTheme.IconCanvas c)
        {
            c.Box(3, 0, 10, 16, L, D);
            for (int y = 2; y < 10; y += 3) { c.Box(4, y, 8, 3, G, D); c.Set(10, y + 1, Gr); }
            c.Rect(5, 12, 6, 1, D);
        }

        static void Mail(RetroTheme.IconCanvas c)
        {
            c.Box(1, 3, 14, 10, W, D);
            c.Line(1, 3, 8, 9, D); c.Line(14, 3, 8, 9, D);
        }

        static void Lock(RetroTheme.IconCanvas c)
        {
            c.Box(4, 1, 8, 8, C(0, 0, 0, 0), D);
            c.Rect(5, 2, 6, 6, new Color(0, 0, 0, 0));
            c.Box(2, 7, 12, 8, Ye, D);
            c.Rect(7, 9, 2, 4, K);
        }

        static Color C(int r, int g, int b, int a) => new Color(r / 255f, g / 255f, b / 255f, a / 255f);

        static void Run(RetroTheme.IconCanvas c)
        {
            c.Box(1, 2, 14, 12, W, D);
            c.Rect(1, 2, 14, 3, Nv);
            c.Rect(4, 8, 6, 2, Gr); c.Line(8, 6, 11, 9, Gr); c.Line(8, 11, 11, 8, Gr);
        }

        static void TaskMgr(RetroTheme.IconCanvas c)
        {
            c.Box(1, 1, 14, 14, K, L);
            c.Rect(3, 10, 2, 4, Gr); c.Rect(6, 7, 2, 7, Gr); c.Rect(9, 4, 2, 10, Gr); c.Rect(12, 8, 2, 6, Gr);
        }

        static void Search(RetroTheme.IconCanvas c)
        {
            c.Circle(6, 6, 5f, D);
            c.Circle(6, 6, 4f, C(180, 220, 255));
            c.Set(4, 4, W);
            c.Line(9, 9, 14, 14, Br); c.Line(10, 9, 15, 14, Br);
        }

        static void Remote(RetroTheme.IconCanvas c)
        {
            c.Box(0, 0, 10, 8, L, D); c.Rect(1, 1, 8, 5, Nv);
            c.Box(6, 7, 10, 8, L, D); c.Rect(7, 8, 8, 5, C(0, 128, 128));
            c.Line(3, 10, 5, 12, Rd);
        }

        static void Clock(RetroTheme.IconCanvas c)
        {
            c.Circle(8, 8, 7f, D); c.Circle(8, 8, 6f, W);
            c.Line(8, 8, 8, 4, K); c.Line(8, 8, 11, 9, K);
        }

        static void Check(RetroTheme.IconCanvas c)
        {
            c.Line(3, 8, 6, 11, K); c.Line(6, 11, 12, 4, K);
            c.Line(3, 9, 6, 12, K); c.Line(6, 12, 12, 5, K);
        }

        static void Wifi(RetroTheme.IconCanvas c, bool on)
        {
            Color col = on ? K : G;
            c.Rect(2, 4, 12, 1, col); c.Set(1, 5, col); c.Set(14, 5, col);
            c.Rect(4, 7, 8, 1, col); c.Set(3, 8, col); c.Set(12, 8, col);
            c.Rect(6, 10, 4, 1, col);
            c.Rect(7, 12, 2, 2, col);
            if (!on) { c.Line(9, 9, 15, 15, Rd); c.Line(15, 9, 9, 15, Rd); c.Line(10, 9, 15, 14, Rd); }
        }

        static void Drive(RetroTheme.IconCanvas c)
        {
            c.Box(1, 5, 14, 7, L, D);
            c.Rect(3, 8, 6, 1, G); c.Set(12, 8, Gr);
        }

        static void Start(RetroTheme.IconCanvas c)
        {
            c.Rect(2, 2, 5, 5, Rd); c.Rect(9, 2, 5, 5, Gr); c.Rect(2, 9, 5, 5, Bl); c.Rect(9, 9, 5, 5, Ye);
        }

        static void Debug(RetroTheme.IconCanvas c)
        {
            c.Circle(8, 9, 5f, Rd); c.Rect(7, 4, 2, 11, K);
            c.Line(2, 6, 4, 8, K); c.Line(14, 6, 12, 8, K); c.Line(2, 13, 4, 11, K); c.Line(14, 13, 12, 11, K);
        }

        static void Firewall(RetroTheme.IconCanvas c)
        {
            c.Rect(1, 2, 14, 12, Rd);
            for (int y = 2; y < 14; y += 3)
            {
                c.Rect(1, y, 14, 1, L);
                int off = (y / 3) % 2 == 0 ? 4 : 0;
                for (int x = off; x < 16; x += 8) c.Rect(x, y, 1, 3, L);
            }
        }
    }
}
