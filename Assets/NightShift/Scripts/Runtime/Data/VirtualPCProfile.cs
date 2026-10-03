using System;
using System.Collections.Generic;
using UnityEngine;

namespace NightShift.Data
{
    [Serializable]
    public class ProcessEntry
    {
        public string name;
        public int memoryKb;
        public bool system;
        [Tooltip("終了不可（アクセス拒否）")]
        public bool critical;
    }

    [Serializable]
    public class FileEntry
    {
        public string folder;   // 例: "マイ ドキュメント"
        public string name;
        public int sizeKb;
    }

    [Serializable]
    public class PrintJobEntry
    {
        public string document;
        public int pages = 1;
    }

    /// <summary>リモートPCの基準状態。故障は HelpDeskDefinition 側で上書きする。</summary>
    [CreateAssetMenu(menuName = "NightShift/Virtual PC Profile", fileName = "PCProfile")]
    public class VirtualPCProfile : ScriptableObject
    {
        public string osName = "NEXUS Workstation 2000";
        public string wifiAdapterName = "NX Wireless LAN Adapter 802.11g";
        public string ethernetAdapterName = "NX Fast Ethernet Adapter";
        public string ssid = "NEXUS-CORP";
        public string gateway = "10.10.0.1";
        public string dns = "10.10.0.10";
        public string subnetMask = "255.255.0.0";
        public string domain = "nexus.local";
        public bool dhcpEnabled = true;
        public int diskUsagePercent = 62;
        public string printerName = "NX-LaserPrinter 3F";
        public List<ProcessEntry> processes = new List<ProcessEntry>();
        public List<FileEntry> files = new List<FileEntry>();
        public List<PrintJobEntry> printJobs = new List<PrintJobEntry>();
    }
}
