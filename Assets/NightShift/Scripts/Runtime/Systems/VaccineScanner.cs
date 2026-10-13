using System;
using System.Collections.Generic;
using NightShift.Data;
using NightShift.RemotePC;

namespace NightShift.Systems
{
    public enum ScanTarget { Workstation, Remote, Server }

    /// <summary>
    /// VACCINE 99 の検査（ゲーム内の仮想の端末だけ。実際のファイルは調べない）。
    /// 検査対象は開始時に固定する。リモートの検査中に接続が切れた・別のPCへ接続した場合は中断し、
    /// 未完了を「見つかりませんでした」とは表示しない。定義ファイルに載っているウイルスだけを検出する。
    /// </summary>
    public class VaccineScanner
    {
        public const string WorkstationHost = "NIGHT-OPS-WS";
        public const float WorkstationMinutes = 5f, RemoteMinutes = 2f, ServerMinutes = 5f;

        readonly ShiftSession _s;
        VirtualPCState _pc;     // リモート検査の対象（開始時に固定）

        public bool Running { get; private set; }
        public ScanTarget Target { get; private set; }
        public string TargetHost { get; private set; }
        public float StartedAt { get; private set; }
        public float DoneAt { get; private set; }

        /// <summary>直前の検査の結果（null = まだ検査していない）</summary>
        public ScanResult Last { get; private set; }
        /// <summary>検査が終わった・中断した</summary>
        public event Action<ScanResult> Finished;

        public class ScanResult
        {
            public ScanTarget Target;
            public string Host;
            public bool Completed;           // 最後まで検査した（false = 中断）
            public string AbortReason;
            public readonly List<string> Found = new List<string>();
            public bool MissedUnknown;       // 定義ファイルにないウイルスがいた（演出用。画面には出さない）
            public int Serial;
        }
        int _serial;

        public VaccineScanner(ShiftSession s) { _s = s; }

        static Core.CareerRecord Career => Core.CareerRecord.Current;

        /// <summary>検査を始める。戻り値はメッセージ（始められない理由も含む）</summary>
        public string Begin(ScanTarget target)
        {
            if (Running) return "検査中です。終わるまでお待ちください。";
            if (!Career.ToolActive(Items.Vaccine)) return "VACCINE 99 が使えません。";
            float minutes;
            switch (target)
            {
                case ScanTarget.Remote:
                {
                    var session = _s.HelpDesk.ActiveSession;
                    if (session == null || !session.Connected) return "リモート接続中のPCがありません。";
                    _pc = session.PC;
                    TargetHost = _pc.Hostname;
                    minutes = RemoteMinutes;
                    break;
                }
                case ScanTarget.Server:
                    if (!Career.ToolActive(Items.VaccineServer)) return "サーバーの検査には、サーバー検査の追加ライセンスが必要です。";
                    _pc = null;
                    TargetHost = "社内サーバー";
                    minutes = ServerMinutes;
                    break;
                default:
                    _pc = null;
                    TargetHost = WorkstationHost;
                    minutes = WorkstationMinutes;
                    break;
            }
            Target = target;
            Running = true;
            StartedAt = _s.Time.CurrentMinute;
            DoneAt = StartedAt + minutes;
            _s.Logs.Add(StartedAt, "NIGHT-OPS", TargetHost, _s.OperatorName, "VACCINE", "SCAN", "ウイルス検査を開始");
            return $"{TargetHost} の検査を始めました。";
        }

        public void Tick(float now)
        {
            if (!Running) return;
            if (Target == ScanTarget.Remote)
            {
                var session = _s.HelpDesk.ActiveSession;
                if (session == null || !session.Connected) { Abort("リモート接続が切れました"); return; }
                if (!ReferenceEquals(session.PC, _pc)) { Abort("別のPCに接続しました"); return; }
            }
            if (now >= DoneAt) Finish();
        }

        /// <summary>検査を中断する（未完了。見つからなかったことにはしない）</summary>
        public void Abort(string reason)
        {
            if (!Running) return;
            Running = false;
            var r = new ScanResult { Target = Target, Host = TargetHost, Completed = false, AbortReason = reason, Serial = ++_serial };
            _s.Logs.Add(_s.Time.CurrentMinute, "NIGHT-OPS", TargetHost, _s.OperatorName, "VACCINE", "ABORTED", $"検査を中断（{reason}）");
            _pc = null;
            Last = r;
            Finished?.Invoke(r);
        }

        void Finish()
        {
            Running = false;
            var r = new ScanResult { Target = Target, Host = TargetHost, Completed = true, Serial = ++_serial };
            switch (Target)
            {
                case ScanTarget.Workstation: CleanDevice(WorkstationHost, null, r); break;
                case ScanTarget.Remote: CleanDevice(_pc.Hostname, _pc, r); break;
                case ScanTarget.Server:
                    foreach (var st in _s.Servers.Servers)
                    {
                        var bad = st.Processes.FindAll(p => p.Suspicious && p.Tag == "T9");
                        if (bad.Count == 0) continue;
                        if (!VirusCatalog.Detectable("V2")) { r.MissedUnknown = true; continue; }
                        foreach (var p in bad) _s.Servers.KillProcess(st, p); // 不審なプロセスの終了は、そのたびに正しい対応として数える
                        r.Found.Add($"{VirusCatalog.Name("V2")} — {st.Id}（駆除しました。{VirusCatalog.Get("V2").Recovery}）");
                        _s.Discover("V2", DiscoveryStage.Detected);
                        _s.Discover("V2", DiscoveryStage.Removed);
                    }
                    break;
            }
            _s.Logs.Add(_s.Time.CurrentMinute, "NIGHT-OPS", TargetHost, _s.OperatorName, "VACCINE", r.Found.Count > 0 ? "FOUND" : "CLEAN", r.Found.Count > 0 ? string.Join(" / ", r.Found) : "ウイルスは見つかりませんでした");
            _pc = null;
            Last = r;
            Finished?.Invoke(r);
        }

        /// <summary>端末の感染を検査・駆除する。駆除の加点は、実際に駆除できたときに一度だけ</summary>
        void CleanDevice(string host, VirtualPCState pc, ScanResult r)
        {
            string v = _s.Security.InfectionOf(host);
            if (v == null) return;
            if (!VirusCatalog.Detectable(v)) { r.MissedUnknown = true; return; }
            _s.Discover(v, DiscoveryStage.Detected);
            if (!_s.Security.ClearInfection(host)) return;
            _s.Evaluation.SecurityActionsCorrect++;
            _s.Discover(v, DiscoveryStage.Removed);
            // 同じ端末のリモートPC（問い合わせごとに作られる）からもプロセスを消す
            string proc = VirusCatalog.ProcessOf(v);
            foreach (var t in _s.HelpDesk.Tickets)
            {
                if (t.PC == null || t.PC.Hostname != host) continue;
                t.PC.MalwareState = "None";
                if (proc != null) t.PC.RunningProcesses.RemoveAll(p => p.Name == proc);
                t.PC.NotifyChanged();
            }
            if (pc != null && pc.MalwareState != "None") { pc.MalwareState = "None"; if (proc != null) pc.RunningProcesses.RemoveAll(p => p.Name == proc); pc.NotifyChanged(); }
            var e = VirusCatalog.Get(v);
            r.Found.Add($"{e.Name} — {host}（駆除しました。{e.Recovery}）");
        }
    }
}
