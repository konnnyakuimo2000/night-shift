using System;
using System.Collections.Generic;
using NightShift.Core;
using NightShift.Data;
using UnityEngine;
using UnityEngine.UI;

namespace NightShift.UI.Screens
{
    /// <summary>
    /// 朝のネットニュース（DAY8〜、タイムカードの前）。1998年のポータルサイトのパロディ「YAHHO! NEWS」。
    /// 出勤前に自宅のPCで見ている、という設定。読むのは任意。
    /// </summary>
    public static class NewsScreen
    {
        static readonly Color Link = new Color(0.05f, 0.1f, 0.75f);
        static readonly Color Paper = new Color(0.98f, 0.98f, 0.96f);

        public static GameObject Build(RectTransform parent, GameDatabase db, DayDefinition day, Action onDone)
        {
            var root = UIFactory.Solid(parent, new Color(0.1f, 0.12f, 0.14f), "News");
            root.rectTransform.Stretch();

            // ブラウザーの枠
            var win = UIFactory.Panel(root.transform, RetroTheme.Raised, "Browser");
            win.rectTransform.anchorMin = win.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            win.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            win.rectTransform.sizeDelta = new Vector2(1060, 720);
            var bar = UIFactory.Image(win.transform, RetroTheme.GradientSprite(RetroTheme.TitleActiveA, RetroTheme.TitleActiveB), Color.white, "Title");
            bar.rectTransform.StretchH(3, 18, 3, 3);
            UIFactory.Label(bar.transform, " YAHHO! NEWS - NetVoyager 3.0", 12, TextAnchor.MiddleLeft, Color.white).rectTransform.Stretch();
            float bx = 6;
            foreach (var b in new[] { "戻る", "進む", "中止", "更新", "ホーム" })
            {
                UIFactory.Button(win.transform, b, bx, 24, 56, 24, null).Interactable = false;
                bx += 58;
            }
            UIFactory.Label(win.transform, "アドレス:", 304, 26, 56, 20, 11);
            UIFactory.ValueBox(win.transform, 360, 26, 690, 20, "http://news.yahho.co.jp/");

            var page = UIFactory.Solid(win.transform, Paper, "Page");
            page.rectTransform.Stretch(4, 52, 4, 26);
            var status = UIFactory.Label(win.transform, "", 11, TextAnchor.MiddleLeft);
            status.rectTransform.anchorMin = new Vector2(0, 0); status.rectTransform.anchorMax = new Vector2(1, 0);
            status.rectTransform.pivot = new Vector2(0, 0);
            status.rectTransform.offsetMin = new Vector2(8, 4); status.rectTransform.offsetMax = new Vector2(-8, 22);

            BuildPage(page.rectTransform, db, day, onDone);

            // ダイヤルアップ接続
            var dial = UIFactory.Solid(win.transform, new Color(0, 0, 0, 0.55f), "Dialup");
            dial.rectTransform.Stretch(4, 52, 4, 26);
            var box = UIFactory.Panel(dial.transform, RetroTheme.Raised, "Box");
            box.rectTransform.anchorMin = box.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            box.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            box.rectTransform.sizeDelta = new Vector2(340, 110);
            UIFactory.Label(box.transform, "ダイヤルアップ接続", 10, 6, 320, 20, 12).fontStyle = FontStyle.Bold;
            var msg = UIFactory.Label(box.transform, "ダイヤルしています… ピー ヒョロロロ…", 10, 34, 320, 40, 12, TextAnchor.UpperLeft);
            status.text = "接続中…";
            bool first = PlayerPrefs.GetInt("NightShift.NewsSeen", 0) == 0;
            float wait = first ? 2.4f : 0.9f;
            Delay.Run(root.gameObject, wait * 0.5f, () => { if (msg) msg.text = "ユーザー名とパスワードを確認しています…"; });
            Delay.Run(root.gameObject, wait, () =>
            {
                if (dial) UnityEngine.Object.Destroy(dial.gameObject);
                if (status) status.text = "ドキュメント: 完了";
                PlayerPrefs.SetInt("NightShift.NewsSeen", 1);
            });
            var skip = dial.gameObject.AddComponent<ClickHandler>();
            skip.OnClick = () => { if (dial) UnityEngine.Object.Destroy(dial.gameObject); status.text = "ドキュメント: 完了"; };
            return root.gameObject;
        }

        static void BuildPage(RectTransform page, GameDatabase db, DayDefinition day, Action onDone)
        {
            var career = CareerRecord.Current;
            // ロゴ・検索
            var logo = UIFactory.Label(page, "YAHHO!", 10, 6, 220, 44, 36, TextAnchor.MiddleLeft, new Color(0.15f, 0.2f, 0.55f));
            logo.fontStyle = FontStyle.BoldAndItalic;
            UIFactory.Label(page, "NEWS", 196, 18, 80, 26, 18, TextAnchor.MiddleLeft, new Color(0.7f, 0.15f, 0.1f)).fontStyle = FontStyle.Bold;
            UIFactory.Input(page, 300, 16, 300, 22, "");
            UIFactory.Button(page, "検索", 606, 15, 60, 24, null).Interactable = false;
            float tx = 10;
            foreach (var t in new[] { "トピックス", "国内", "海外", "経済", "コンピュータ", "エンタメ", "スポーツ" })
            {
                var lt = UIFactory.Label(page, t, tx, 54, 90, 18, 12, TextAnchor.MiddleLeft, Link);
                lt.fontStyle = t == "トピックス" ? FontStyle.Bold : FontStyle.Normal;
                tx += t.Length * 13 + 22;
            }
            UIFactory.Solid(page, new Color(0.15f, 0.2f, 0.55f), "Rule").rectTransform.Place(10, 74, 1030, 2);

            // 記事を集める
            var articles = new List<NewsArticle>();
            var featured = NewsCatalog.FeaturedFor(day.dayNumber);
            if (featured != null) articles.Add(featured);
            articles.AddRange(NewsCatalog.Company(career, day.dayNumber));
            articles.AddRange(NewsCatalog.Fillers3(day.dayNumber));

            string dateText = day.dateLabel;
            UIFactory.Label(page, $"■ 今日のトピックス　{dateText}　18時更新", 10, 82, 600, 20, 13).fontStyle = FontStyle.Bold;

            // 記事本文（クリックで切り替え）
            var articleBox = UIFactory.Image(page, RetroTheme.Field, Color.white, "Article");
            articleBox.rectTransform.Place(10, 236, 690, 330);
            var head = UIFactory.Label(articleBox.transform, "", 10, 8, 670, 50, 16, TextAnchor.UpperLeft);
            head.fontStyle = FontStyle.Bold;
            var body = UIFactory.Label(articleBox.transform, "", 10, 62, 670, 180, 13, TextAnchor.UpperLeft);
            var expert = UIFactory.Label(articleBox.transform, "", 10, 246, 670, 76, 13, TextAnchor.UpperLeft, new Color(0.45f, 0.25f, 0f));
            void Show(NewsArticle a)
            {
                head.text = (a.Featured ? "〈注目〉" : "") + a.Headline;
                body.text = string.IsNullOrEmpty(a.Body) ? "（記事は準備中です）" : a.Body;
                expert.text = string.IsNullOrEmpty(a.Expert) ? "" : "専門家のコメント:\n「" + a.Expert + "」";
            }

            float y = 106;
            foreach (var a in articles)
            {
                var art = a;
                var line = UIFactory.FlatButton(page, $"・{(a.Featured ? "〈注目〉" : "")}{a.Headline}{(a.New ? "  NEW" : "")}", () => Show(art));
                line.Label.alignment = TextAnchor.MiddleLeft;
                line.Label.color = Link;
                line.Label.fontSize = 13;
                line.Rect.Place(10, y, 690, 20);
                y += 21;
                if (y > 226) break;
            }
            if (articles.Count > 0) Show(articles[0]);

            // 右の欄
            float rx = 712;
            var rank = UIFactory.Group(page, "アクセスランキング", rx, 82, 330, 150);
            float ry = 0;
            int n = 1;
            foreach (var a in articles)
            {
                if (n > 5) break;
                UIFactory.Label(rank, $"{n}. {a.Headline}", 0, ry, 320, 20, 11, TextAnchor.MiddleLeft, Link);
                ry += 22; n++;
            }
            var stock = UIFactory.Group(page, "株価（ネクサスCorp.）", rx, 238, 330, 60);
            int price = NewsCatalog.StockPrice(career.trust);
            int delta = career.nights.Count > 0 ? career.nights[career.nights.Count - 1].trustDelta * 4 : 0;
            UIFactory.Label(stock, $"¥{price:N0}　{(delta >= 0 ? "▲" : "▼")}{Mathf.Abs(delta)}", 0, 0, 320, 30, 18, TextAnchor.MiddleLeft,
                delta >= 0 ? new Color(0.7f, 0.1f, 0.1f) : new Color(0.1f, 0.3f, 0.7f));
            var info = UIFactory.Group(page, "天気・お知らせ", rx, 304, 330, 80);
            UIFactory.Label(info, $"東京　くもり時々雨\n2000年問題まで あと {DaysTo2000(day.dateLabel)} 日", 0, 0, 320, 50, 12, TextAnchor.UpperLeft);

            // 広告バナー（ストアの商品・ブラック企業の求人）
            var ad = UIFactory.Image(page, RetroTheme.Field, new Color(1f, 0.95f, 0.75f), "Ad");
            ad.rectTransform.Place(rx, 392, 330, 80);
            UIFactory.Label(ad.transform, AdText(db, day), 10, 4, 310, 72, 13, TextAnchor.MiddleCenter).fontStyle = FontStyle.Bold;
            var job = UIFactory.Label(page, "【求人】アットホームな職場です！ 夜間IT担当（日当制・やる気のある方歓迎）", rx, 478, 330, 40, 11, TextAnchor.UpperLeft, new Color(0.4f, 0.4f, 0.4f));

            var go = UIFactory.Button(page, "接続を切って出勤する  ▶", 0, 0, 260, 34, onDone, "clock");
            go.Rect.anchorMin = go.Rect.anchorMax = new Vector2(1, 0);
            go.Rect.pivot = new Vector2(1, 0);
            go.Rect.anchoredPosition = new Vector2(-14, 10);
            go.Label.fontStyle = FontStyle.Bold;
            var k = page.gameObject.AddComponent<KeyAction>(); k.Key = KeyCode.Return; k.Action = onDone;
        }

        static string AdText(GameDatabase db, DayDefinition day)
        {
            var ads = new List<string>();
            foreach (var it in db.storeItems) if (it != null && !string.IsNullOrEmpty(it.adCopy) && it.unlockDay <= day.dayNumber) ads.Add(it.adCopy);
            return ads.Count == 0 ? "社内購買システム　業務用品のご注文はお早めに" : ads[day.dayNumber % ads.Count];
        }

        static int DaysTo2000(string dateLabel)
        {
            try
            {
                var d = DateTime.ParseExact(dateLabel.Split(' ')[0], "yyyy/MM/dd", System.Globalization.CultureInfo.InvariantCulture);
                return (int)(new DateTime(2000, 1, 1) - d).TotalDays;
            }
            catch { return 0; }
        }
    }
}
