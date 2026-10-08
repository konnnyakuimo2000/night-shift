using System;
using System.Collections.Generic;
using NightShift.Events;

namespace NightShift.Systems
{
    /// <summary>
    /// 接続要求に添えられる「申請理由」。社員ごとの口調（Voice）で書かれ、夜の社内の様子が見える読み物にする。
    /// 判断の材料にはしない: 正しい要求にも不正な要求にも、同じ理由の集まりから同じ確率で付く（理由で見分けられない）。
    /// </summary>
    public static class RequestReasons
    {
        static readonly Dictionary<string, string[]> ByAccess = new Dictionary<string, string[]>
        {
            ["共有ファイル"] = new[]
            {
                "明日の朝イチの会議資料を仕上げたいので", "取引先から急ぎで差し替えを頼まれたので", "共有フォルダに置き忘れた見積書を取りに",
                "部長に「朝までに」と言われた資料のために", "昼間はフォルダが重くて開けなかったので", "家で続きをやろうとして、ファイルを持ち帰り忘れたので",
                "表の数字が合わなくて眠れないので", "新人さん向けのマニュアルを直したくて"
            },
            ["開発環境"] = new[]
            {
                "本番リリース前の最終確認です", "バグが直らなくて眠れないので", "コードレビューの指摘を今夜中に直したくて",
                "テストが全部赤くなっていて…", "明朝のデモ用にビルドしたいので", "海外チームとの時差の関係で"
            },
            ["人事システム"] = new[]
            {
                "来月の入社手続きの書類確認です", "有給の残りの日数を確認したくて", "評価面談の資料をまとめるため", "シフト表の入力を忘れていたので"
            },
            ["経理システム"] = new[]
            {
                "月末の締めに間に合わせたいので", "経費精算の領収書を入力し忘れていて", "請求書の金額が1桁ずれていたので", "監査の前に数字を見直したくて"
            },
            ["VPN接続"] = new[]
            {
                "出張先のホテルからです", "子どもが寝たので、続きを家でやります", "障害対応の呼び出しがあったので",
                "台風で出社できなかった分を取り戻したくて", "終電を逃して…いえ、自宅からです"
            },
            ["Web閲覧"] = new[]
            {
                "取引先のホームページを確認したくて", "調べものです（決して競馬ではありません）", "プリンターのドライバーを探しに", "明日の天気の確認です。傘、いりますか"
            },
            ["メール"] = new[]
            {
                "海外からの返信を待っているので", "送り忘れたメールがあって", "上司から「返信まだ？」と言われたので"
            },
            ["管理"] = new[]
            {
                "サーバーの再起動を頼まれたので", "ログの容量がいっぱいだと通知が来たので", "昼に変えた設定を元に戻したくて", "夜間メンテナンスの手順確認です"
            },
        };

        static readonly string[] Generic = { "急ぎです", "すぐ終わります", "5分だけお願いします", "理由は…聞かないでください" };

        /// <summary>まれに混ざる、夜勤のちょっとした息抜き</summary>
        static readonly string[] Rare =
        {
            "会社のパソコンのほうがゲームが…いえ、仕事です", "夜食のカップ麺が伸びる前に終わらせます", "観葉植物の水やり当番表を更新したくて",
            "猫がキーボードに乗って何か送信したかもしれないので", "占いで「今夜は仕事運が最高」と出たので", "明日の朝礼のスピーチ原稿を探しに"
        };

        static string[] PoolFor(string access)
        {
            if (string.IsNullOrEmpty(access)) return Generic;
            foreach (var kv in ByAccess) if (access.Contains(kv.Key)) return kv.Value;
            if (access.Contains("ログイン") || access.Contains("リモート")) return ByAccess["管理"];
            return Generic;
        }

        /// <summary>この要求の申請理由（同じ要求なら、やり直しても同じ）</summary>
        public static string For(EventInstance e, int seed)
        {
            if (e == null || e.IsOperatorAccount) return "（記入なし）";
            int h = seed;
            foreach (char c in (e.RequestId ?? "") + (e.DisplayUser ?? "") + (e.AccessType ?? "")) h = unchecked(h * 31 + c);
            var r = new Random(h);
            if (r.Next(12) == 0) return "（記入なし）";
            var pool = r.Next(30) == 0 ? Rare : r.Next(6) == 0 ? Generic : PoolFor(e.AccessType);
            string reason = pool[r.Next(pool.Length)];
            if (e.Employee == null) return "「" + reason + "」";
            return "「" + Styled(Voice.Of(e.Employee), reason) + "」";
        }

        /// <summary>社員の口調で締める</summary>
        static string Styled(Persona p, string reason)
        {
            bool open = reason.EndsWith("…") || reason.EndsWith("）");
            switch (p)
            {
                case Persona.Polite: return reason + (open ? " 接続のご許可をお願いいたします。" : "、接続のご許可をお願いいたします。");
                case Persona.Casual: return reason + "！よろしく〜";
                case Persona.Anxious: return reason + (open ? " すみません、お願いします…" : "…すみません、お願いします…");
                case Persona.Curt: return reason + (open ? "" : "。");
                case Persona.Veteran: return reason + (open ? " 頼みます。" : "。頼みます。");
                case Persona.Cheerful: return reason + "♪ お願いしまーす！";
                default: return reason + (open ? " お願いします。" : "、お願いします。");
            }
        }
    }
}
