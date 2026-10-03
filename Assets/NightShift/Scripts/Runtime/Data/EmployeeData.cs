using System;
using System.Collections.Generic;
using UnityEngine;

namespace NightShift.Data
{
    [Serializable]
    public class ServiceWeight
    {
        [Tooltip("WEB / MAIL / FILE / DEV / HR / ACCOUNTING など。NetworkNodeDefinition の serviceId と対応")]
        public string serviceId;
        [Min(0f)] public float weight = 1f;
    }

    /// <summary>
    /// 社員の「普段の行動」。権限（Permissions）とは別概念。
    /// DAY2以降の「権限はあるが行動がおかしい」判定はここを基準にする。
    /// </summary>
    [Serializable]
    public class NormalBehaviorProfile
    {
        [Tooltip("1ゲーム時間あたりの背景通信の平均回数")]
        [Min(0f)] public float activityPerHour = 6f;
        public List<ServiceWeight> serviceWeights = new List<ServiceWeight>();
        [Tooltip("社外(VPN)から接続することがあるか")]
        public bool usesVpn;
    }

    [CreateAssetMenu(menuName = "NightShift/Employee", fileName = "Employee")]
    public class EmployeeData : ScriptableObject
    {
        [Header("基本情報")]
        public string employeeId;
        public string fullName;
        public string kana;
        public int age;
        public string department;
        public string position;

        [Tooltip("顔写真（未設定なら社員番号から自動生成）")]
        public Sprite portrait;

        [Header("端末")]
        [Tooltip("登録端末（社内PC）")]
        public string deviceId;
        [Tooltip("空欄なら自動生成")]
        public string deviceIp;
        [Tooltip("VPN用の登録ノートPC（任意）")]
        public string laptopDeviceId;

        [Header("勤務")]
        public string workHours = "09:00-18:00";
        [Tooltip("夜間帯に勤務中か（勤務状態の表示・イベント選択に使用）")]
        public bool onShift = true;
        public bool vpnEnabled;

        [Header("特性 (1-5)")]
        [Range(1, 5)] public int itLiteracy = 3;
        [Range(1, 5)] public int securityAwareness = 3;

        [Header("普段使うシステム（行動）")]
        public List<string> normalAccessTargets = new List<string>();
        public NormalBehaviorProfile normalBehavior = new NormalBehaviorProfile();

        [Header("権限（Permission の Collection）")]
        public List<PermissionDefinition> permissions = new List<PermissionDefinition>();

        public bool HasPermission(string permissionId)
        {
            if (string.IsNullOrEmpty(permissionId)) return true;
            foreach (var p in permissions)
                if (p != null && p.permissionId == permissionId) return true;
            return false;
        }

        public bool UsesNormally(string serviceId)
        {
            return normalAccessTargets.Contains(serviceId);
        }

        public bool OwnsDevice(string device)
        {
            if (string.IsNullOrEmpty(device)) return false;
            return device == deviceId || (!string.IsNullOrEmpty(laptopDeviceId) && device == laptopDeviceId);
        }

        /// <summary>ログイン名（社員番号から生成）</summary>
        public string LoginName => "nexus\\" + (employeeId ?? "").Replace("-", "").ToLowerInvariant();
    }
}
