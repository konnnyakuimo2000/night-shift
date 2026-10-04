using System;
using System.Collections.Generic;
using System.Text;

namespace NightShift.RemotePC
{
    /// <summary>
    /// ゲーム内疑似ターミナルのコマンド。本物のOSコマンドは一切実行せず、VirtualPCState を参照して出力を作る。
    /// コマンドは Register で追加できる（nslookup / tasklist / dir などの拡張用）。
    /// </summary>
    public static class TerminalCommands
    {
        public class Context
        {
            public VirtualPCState PC;
            public string[] Args;
            public string Raw;
            public Action Clear;
            public Action Exit;
            public Random Rng;
        }

        static readonly Dictionary<string, Func<Context, string>> _commands = new Dictionary<string, Func<Context, string>>(StringComparer.OrdinalIgnoreCase);

        static readonly Dictionary<string, string> KnownHosts = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "intranet", "10.10.0.15" }, { "intranet.nexus.local", "10.10.0.15" },
            { "file-srv01", "10.10.0.30" }, { "mail-srv01", "10.10.0.20" }, { "auth-srv01", "10.10.0.5" },
            { "app-srv01", "10.10.0.40" }, { "www.nexus-corp.example", "172.16.0.10" }, { "web-srv01", "172.16.0.10" },
            { "www.example.com", "93.184.216.34" }, { "example.com", "93.184.216.34" }
        };

        static TerminalCommands()
        {
            Register("help", Help);
            Register("ipconfig", IpConfig);
            Register("ping", Ping);
            Register("hostname", c => c.PC.Hostname);
            Register("whoami", c => c.PC.UserName);
            Register("ver", c => $"\n{c.PC.OsName} [Version 5.0.2195]\n");
            Register("cls", c => { c.Clear?.Invoke(); return null; });
            Register("exit", c => { c.Exit?.Invoke(); return null; });
        }

        public static void Register(string name, Func<Context, string> handler) => _commands[name] = handler;

        public static string Execute(string line, VirtualPCState pc, Action clear, Action exit, Random rng)
        {
            line = (line ?? "").Trim();
            if (line.Length == 0) return "";
            var parts = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            string name = parts[0];
            if (name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) name = name.Substring(0, name.Length - 4);
            var args = new string[parts.Length - 1];
            Array.Copy(parts, 1, args, 0, args.Length);
            if (!_commands.TryGetValue(name, out var fn))
                return $"'{parts[0]}' は、内部コマンドまたは外部コマンド、\n操作可能なプログラムまたはバッチ ファイルとして認識されていません。";
            return fn(new Context { PC = pc, Args = args, Raw = line, Clear = clear, Exit = exit, Rng = rng });
        }

        static string Help(Context c)
        {
            var sb = new StringBuilder();
            sb.AppendLine("利用可能なコマンド:");
            sb.AppendLine("  IPCONFIG   ネットワーク設定を表示します。(/all で詳細)");
            sb.AppendLine("  PING       指定したホストへの到達性を確認します。");
            sb.AppendLine("  HOSTNAME   コンピューター名を表示します。");
            sb.AppendLine("  WHOAMI     ログイン中のユーザーを表示します。");
            sb.AppendLine("  VER        バージョンを表示します。");
            sb.AppendLine("  CLS        画面を消去します。");
            sb.AppendLine("  EXIT       コマンド プロンプトを終了します。");
            return sb.ToString();
        }

        static string IpConfig(Context c)
        {
            var pc = c.PC;
            bool all = c.Args.Length > 0 && c.Args[0].Equals("/all", StringComparison.OrdinalIgnoreCase);
            var sb = new StringBuilder();
            sb.AppendLine();
            sb.AppendLine($"{pc.OsName} IP 構成");
            sb.AppendLine();
            if (all)
            {
                sb.AppendLine($"   ホスト名 . . . . . . . . . . . . : {pc.Hostname}");
                sb.AppendLine($"   プライマリ DNS サフィックス . . . : {pc.Domain}");
                sb.AppendLine();
            }
            sb.AppendLine("イーサネット アダプター ローカル エリア接続:");
            sb.AppendLine();
            sb.AppendLine("   メディアの状態. . . . . . . . . . : メディアは接続されていません");
            if (all) sb.AppendLine($"   説明. . . . . . . . . . . . . . . : {pc.EthernetAdapterName}");
            sb.AppendLine();
            if (pc.NetworkAdapterEnabled)
            {
                sb.AppendLine("Wireless LAN アダプター ワイヤレス ネットワーク接続:");
                sb.AppendLine();
                if (!pc.WiFiEnabled)
                {
                    sb.AppendLine("   メディアの状態. . . . . . . . . . : メディアは接続されていません");
                    if (all)
                    {
                        sb.AppendLine($"   説明. . . . . . . . . . . . . . . : {pc.WifiAdapterName}");
                        sb.AppendLine($"   物理アドレス. . . . . . . . . . . : {pc.MacAddress}");
                    }
                }
                else
                {
                    if (all)
                    {
                        sb.AppendLine($"   説明. . . . . . . . . . . . . . . : {pc.WifiAdapterName}");
                        sb.AppendLine($"   物理アドレス. . . . . . . . . . . : {pc.MacAddress}");
                        sb.AppendLine($"   DHCP 有効 . . . . . . . . . . . . : {(pc.DHCPEnabled ? "はい" : "いいえ")}");
                    }
                    sb.AppendLine($"   接続固有の DNS サフィックス . . . : {pc.Domain}");
                    sb.AppendLine($"   IP アドレス . . . . . . . . . . . : {pc.IPAddress}");
                    sb.AppendLine($"   サブネット マスク . . . . . . . . : {pc.SubnetMask}");
                    sb.AppendLine($"   デフォルト ゲートウェイ . . . . . : {pc.Gateway}");
                    if (all) sb.AppendLine($"   DNS サーバー. . . . . . . . . . . : {pc.DNS}");
                }
                sb.AppendLine();
            }
            return sb.ToString();
        }

        static string Ping(Context c)
        {
            var pc = c.PC;
            if (c.Args.Length == 0) return "使用法: ping ホスト名 または IP アドレス";
            string target = c.Args[c.Args.Length - 1];
            bool loopback = target == "127.0.0.1" || target.Equals("localhost", StringComparison.OrdinalIgnoreCase);
            string ip = target;
            bool isIp = System.Text.RegularExpressions.Regex.IsMatch(target, @"^\d{1,3}(\.\d{1,3}){3}$");
            if (!isIp && !loopback)
            {
                // DNS サーバーの設定が間違っていると、名前から IP アドレスを引けない
                if (!pc.InternetConnectivity || !pc.DnsAuto || !KnownHosts.TryGetValue(target, out ip))
                    return $"ping 要求ではホスト {target} が見つかりませんでした。名前を確認してから再実行してください。";
            }
            if (loopback) ip = "127.0.0.1";
            var sb = new StringBuilder();
            sb.AppendLine();
            sb.AppendLine($"{target} [{ip}] に ping を送信しています 32 バイトのデータ:");
            bool reachable = loopback || (pc.InternetConnectivity && (ip == pc.Gateway || ip.StartsWith("10.") || ip.StartsWith("172.16.") || KnownHosts.ContainsValue(ip)));
            if (ip == "10.10.0.30" && !pc.FileServerReachable) reachable = false; // ファイルサーバー側の障害
            if (!pc.InternetConnectivity && !loopback)
            {
                for (int i = 0; i < 4; i++) sb.AppendLine("PING: 転送に失敗しました。一般エラーです。");
                sb.AppendLine();
                sb.AppendLine($"{ip} の ping 統計:");
                sb.AppendLine("    パケット数: 送信 = 4、受信 = 0、損失 = 4 (100% の損失)");
                return sb.ToString();
            }
            int recv = 0;
            for (int i = 0; i < 4; i++)
            {
                if (reachable)
                {
                    int ms = loopback ? 0 : c.Rng.Next(1, ip.StartsWith("10.") ? 4 : 40);
                    sb.AppendLine($"{ip} からの応答: バイト数 =32 時間 {(ms == 0 ? "<1" : "=" + ms)}ms TTL={(loopback ? 128 : 64)}");
                    recv++;
                }
                else sb.AppendLine("要求がタイムアウトしました。");
            }
            sb.AppendLine();
            sb.AppendLine($"{ip} の ping 統計:");
            sb.AppendLine($"    パケット数: 送信 = 4、受信 = {recv}、損失 = {4 - recv} ({(4 - recv) * 25}% の損失)");
            return sb.ToString();
        }
    }
}
