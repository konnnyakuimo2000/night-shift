using System;
using UnityEngine;

namespace NightShift.Data
{
    /// <summary>給与の計算式（GameDatabase で調整可能）。給与は後のストアシステムで使う。</summary>
    [Serializable]
    public class PayrollSettings
    {
        [Tooltip("夜勤1回の基本給")]
        public int basePay = 8000;
        [Tooltip("総合評価 1 点あたりの評価手当")]
        public int payPerScorePoint = 60;
        public int rankBonusS = 3000;
        public int rankBonusA = 1500;
        public int rankBonusB = 500;
        [Tooltip("悪意ある接続を許可した場合の減額（1件あたり）")]
        public int deductionPerMaliciousAllow = 2000;
        [Tooltip("ルール違反の接続を許可した場合の減額（1件あたり）")]
        public int deductionPerPolicyViolation = 500;
        [Tooltip("最低支給額")]
        public int minimumPay = 3000;
    }
}
