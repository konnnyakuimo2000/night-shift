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

            // ---- 画面に作用するウイルス（TOOL_REWORK_CLAUDE.md §7）。感染した社員PCが、監視画面へ壊れた画面データを送り続ける ----
            new VirusEntry { Id = "V9", Name = "GLASS", Kind = VirusKind.Virus, DefinitionDay = 22, Host = VirusHost.EmployeePc, Process = "pane32.exe", Danger = 2,
                Route = "社員PCの感染（T19）。そのPCから監視画面へ、割れた画面データが送られる（ログの DISPLAY）", Spread = "約30分かけて、ウィンドウに亀裂が増え、内部がタイル状にずれる（最大10本）",
                Fix = "送信元の端末を隔離すると増えなくなる。VACCINE 99 で駆除するか、そのPCで pane32.exe を終了", Recovery = "駆除しても亀裂は残る。画面の下の［画面の再起動］で消える",
                Symptom = "ウィンドウに亀裂が入り、内部がタイル状にずれる" },
            new VirusEntry { Id = "V10", Name = "ECHO", Kind = VirusKind.Virus, DefinitionDay = 25, Host = VirusHost.EmployeePc, Process = "reverb.exe", Danger = 2,
                Route = "社員PCの感染（T20）。そのPCから監視画面へ、過去の画面データが送り返される（ログの DISPLAY）", Spread = "約30分かけて、処理済みの要求の残像が重なる（最大4枚）",
                Fix = "残像は押せない・時刻に「残像」と出るので、今の要求と見比べる。送信元を隔離し、VACCINE 99 か reverb.exe の終了で駆除", Recovery = "駆除すれば残像は消える。審査の待ち行列に偽の要求は入らない",
                Symptom = "過去の画面が残像として重なり、閉じた画面が戻ってくる" },
            new VirusEntry { Id = "V11", Name = "BLACK INK", Kind = VirusKind.Virus, DefinitionDay = 23, Host = VirusHost.EmployeePc, Process = "inkwell.exe", Danger = 2,
                Route = "社員PCの感染（T16）。そのPCから監視画面へ、壊れた画面データが送られる（ログの DISPLAY）", Spread = "約30分かけて、画面の隅から黒い染みが広がる（ほかのPCへは広がらない）",
                Fix = "送信元の端末を隔離すると広がりが止まる。VACCINE 99 で駆除するか、そのPCで inkwell.exe を終了すると染みが引く", Recovery = "駆除すれば染みは消え、残る被害はない",
                Symptom = "黒いインク状の染みが画面の隅から広がり、文字や顔写真を覆う" },
            new VirusEntry { Id = "V12", Name = "MIRROR", Kind = VirusKind.Virus, DefinitionDay = 24, Host = VirusHost.EmployeePc, Process = "mirror32.exe", Danger = 2,
                Route = "社員PCの感染（T17）。そのPCから監視画面へ、複製された画面データが送られる（ログの DISPLAY）", Spread = "時間とともに、画面の中の画面が奥へ増える（最大4重・偽の警告窓は最大3枚）",
                Fix = "送信元の端末を隔離すると増えなくなる。そのPCのタスク マネージャーで mirror32.exe を終了するか、VACCINE 99 で駆除", Recovery = "駆除すれば複製と偽の警告は消える",
                Symptom = "デスクトップの中に同じデスクトップが現れ、奥へ複製される。偽の警告窓が出る（押しても何も起きない）" },
            new VirusEntry { Id = "V13", Name = "MOLT", Kind = VirusKind.Virus, DefinitionDay = 27, Host = VirusHost.EmployeePc, Process = "molt.exe", Danger = 3,
                Route = "社員PCの感染（T21）。そのPCの画面が、監視画面に混ざり込む（ログの DISPLAY）", Spread = "約35分かけて、画面の表面が剥がれ、別の社員のデスクトップが現れる（最大6か所）",
                Fix = "剥がれた所に映る端末名と、ログの DISPLAY の送信元から発生元を特定して隔離。VACCINE 99 か molt.exe の終了で駆除（操作先は変わらない）", Recovery = "駆除すれば剥がれた所は元に戻る",
                Symptom = "壁紙やウィンドウの表面が剥がれ、別の社員のデスクトップが現れる" },
            new VirusEntry { Id = "V14", Name = "WHITEOUT", Kind = VirusKind.Virus, DefinitionDay = 26, Host = VirusHost.EmployeePc, Process = "bleach.exe", Danger = 3,
                Route = "社員PCの感染（T18）。そのPCから監視画面へ、白く焼き付いた画面データが送られる（ログの DISPLAY）", Spread = "約40分かけて、色と文字が白く薄れていく（最後まで最低限の操作はできる）",
                Fix = "システムログの DISPLAY の送信元を隔離して止め、VACCINE 99 またはそのPCで bleach.exe を終了して駆除", Recovery = "駆除すれば色が戻る",
                Symptom = "色や文字が少しずつ薄れ、白く焼き付いた画面になっていく" },
        };

        /// <summary>画面に作用するウイルス（監視画面の表示を壊す）</summary>
        public static bool IsDisplayVirus(string id) => id == "V9" || id == "V10" || id == "V11" || id == "V12" || id == "V13" || id == "V14";

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
