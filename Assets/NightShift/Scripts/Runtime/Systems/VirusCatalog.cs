using System.Collections.Generic;

namespace NightShift.Systems
{
    /// <summary>カタログ上の種類（ウイルスか、ウイルスではない事象か）</summary>
    public enum VirusKind
    {
        Virus,          // VACCINE の定義に載れば検出・駆除できる
        NotDetectable,  // 感染の形をとらない（アカウント乗っ取り・停電など）。VACCINE では検出しない
        Unclassified    // 分類不能の観測記録（雨宮リカ）
    }

    /// <summary>どこを検査すれば見つかるか</summary>
    public enum VirusHost { EmployeePc, Workstation, Server, None }

    /// <summary>ウイルス1種類の情報（VIRUS_CATALOG.md）。検査・図鑑・SOC-AI・持ち越しで共通に使う</summary>
    public class VirusEntry
    {
        public string Id;            // V1 など（セーブの識別子。変えない）
        public string Name;          // 英語の名前だけ（日本語訳・読みは付けない）
        public VirusKind Kind;
        public int DefinitionDay;    // 定義ファイルに載る日（-1 = 載らない）
        public VirusHost Host;
        public string Process;       // 感染した端末で動くプロセス名（null = なし）
        public int Danger;           // 危険度 1〜3（カタログの説明。現在の感染を判定する警告ではない）
        public string Route;         // 感染経路
        public string Spread;        // 拡大の条件
        public string Fix;           // 対処
        public string Recovery;      // 駆除のあとに残る被害と復旧
        public string Symptom;       // 観測できる症状（症状を観測した段階で公開）
        public bool Implemented = true; // ゲームに出るか（後続候補は false）
    }

    public static class VirusCatalog
    {
        static readonly List<VirusEntry> _all = new List<VirusEntry>
        {
            new VirusEntry { Id = "V1", Name = "TINNITUS", Kind = VirusKind.Virus, DefinitionDay = 9, Host = VirusHost.EmployeePc, Process = "tinnitus.scr", Danger = 1,
                Route = "不審な添付・誤った回答による感染（問い合わせ A9）", Spread = "広がらない。駆除するまで夜をまたいで残る",
                Fix = "VACCINE 99 で検査・駆除。または不審なプロセスを終了", Recovery = "駆除すれば残る被害はない",
                Symptom = "夜中にPCが勝手に鳴る。アイコンが少しずつ中央へ寄っていく" },
            new VirusEntry { Id = "V2", Name = "GARBLE", Kind = VirusKind.Virus, DefinitionDay = 17, Host = VirusHost.Server, Process = "httpd_cache.exe", Danger = 2,
                Route = "公開Webへの探索のあと、不審なファイルを書き込まれる（T9）", Spread = "書き込みから約25分でページとログが書き換わる",
                Fix = "不審なプロセスを止める（VACCINE 99 のサーバー検査ライセンスでも駆除できる）", Recovery = "書き換えられたファイル・ログはバックアップから復元するまで戻らない",
                Symptom = "ログの文字が化ける。公開Webのページが意味のない文字列に変わる" },
            new VirusEntry { Id = "V3", Name = "NIGHTCRY", Kind = VirusKind.Virus, DefinitionDay = 16, Host = VirusHost.EmployeePc, Process = "svch0st.exe", Danger = 2,
                Route = "感染した社員PC（T7）・誤った回答（A3・A20）", Spread = "約30分で同じ部署の通信できるPCへ広がる（隔離・切断した端末へは広がらない）",
                Fix = "まず隔離して拡大を止め、1台ずつ駆除", Recovery = "駆除すれば残る被害はない。広がった先も駆除が必要",
                Symptom = "社外へ数分おきの定期通信（POST /gate.php）。PCが重い" },
            new VirusEntry { Id = "V4", Name = "BLUESTONE", Kind = VirusKind.Virus, DefinitionDay = 13, Host = VirusHost.Workstation, Process = "update32.exe", Danger = 2,
                Route = "担当者宛ての偽メールの添付を開く（経路 C）・リモートPCで不審なファイルを実行（経路 D）", Spread = "広がらない。駆除するまで夜をまたいで再発する",
                Fix = "ワークステーション（またはそのPC）を VACCINE 99 で検査・駆除。定義が新しければ常駐監視が実行前に止める", Recovery = "駆除すれば再発しない",
                Symptom = "画面の端が青くちらつき、ブルースクリーンになる" },
            new VirusEntry { Id = "V5", Name = "REDACT", Kind = VirusKind.NotDetectable, DefinitionDay = -1, Host = VirusHost.None, Danger = 3,
                Route = "管理者アカウントの乗っ取り（T10）", Spread = "UPS の設定変更から約14分で停電",
                Fix = "退勤中の管理者のIDの要求を拒否し、アカウントを一時停止（ウイルス対策ソフトでは止まらない）", Recovery = "停電で止まったサービスを再起動",
                Symptom = "深夜の UPS 設定変更。蛍光灯が一度だけ瞬く" },
            new VirusEntry { Id = "V6", Name = "BRIAR ROSE", Kind = VirusKind.Virus, DefinitionDay = 20, Host = VirusHost.EmployeePc, Process = "briar.exe", Danger = 3,
                Route = "身代金ウイルスに感染した社員PC（T11）", Spread = "1分ごとに共有フォルダのファイルを暗号化。45分で全社に広がる（致命的）",
                Fix = "FILE WRITE を出している端末をすぐ隔離。駆除すれば以後の暗号化は止まる", Recovery = "暗号化されたファイルは駆除しても戻らない。バックアップから復元する",
                Symptom = "1台の端末から FILE WRITE が連続し、ファイル名が .briar に変わる" },
            new VirusEntry { Id = "V7", Name = "DOPPELGANGER", Kind = VirusKind.NotDetectable, DefinitionDay = -1, Host = VirusHost.None, Danger = 3,
                Route = "自分のアカウントを奪われる（T4 の許可・偽フォームへの入力）", Spread = "自分のIDで次々に接続される",
                Fix = "自分のIDの要求を拒否し、パスワードを変更（ウイルス対策ソフトでは止まらない）", Recovery = "パスワードを変えるまで続く",
                Symptom = "自分から自分宛てのメール。自分のIDからの接続要求" },
            new VirusEntry { Id = "V8", Name = "DAYBREAK", Kind = VirusKind.NotDetectable, DefinitionDay = -1, Host = VirusHost.None, Danger = 3,
                Route = "自律型の攻撃AI（DAY28〜30）", Spread = "拒否されると別の範囲から再挑戦する",
                Fix = "接続元を範囲ごと遮断（ウイルス対策ソフトでは止まらない）", Recovery = "夜の結果で本編の結末が決まる",
                Symptom = "遮断のたびに別の範囲から同じ手口の要求が届く" },
        };

        public static IReadOnlyList<VirusEntry> All => _all;

        public static VirusEntry Get(string id) => _all.Find(x => x.Id == id);

        public static string Name(string id) => Get(id)?.Name ?? id;

        public static int DefinitionDay(string id) => Get(id)?.DefinitionDay ?? -1;

        /// <summary>この版の定義ファイルで検出できるか（ウイルスではない事象・後続候補は検出しない）</summary>
        public static bool Detectable(string id, int definitionDay)
        {
            var v = Get(id);
            return v != null && v.Kind == VirusKind.Virus && v.Implemented && v.DefinitionDay > 0 && definitionDay >= v.DefinitionDay;
        }

        /// <summary>今の定義ファイル（キャリアの記録）で検出できるか</summary>
        public static bool Detectable(string id) => Detectable(id, Core.CareerRecord.Current.vaccineDefinitionDay);

        public static string ProcessOf(string id) => Get(id)?.Process;
    }
}
