using System;
using UnityEngine;

namespace NightShift.Data
{
    /// <summary>
    /// 給与の計算式（GameDatabase で調整可能）。給与は後のストアシステムで使う。
    /// 舞台は 1990年代末のブラック企業。日当制（深夜手当込み）・罰金の天引き・振込手数料の本人負担。
    /// </summary>
    [Serializable]
    public class PayrollSettings
    {
        [Tooltip("研修期間（trainingLastDay まで）の日当（深夜手当込み）")]
        public int basePay = 3000;
        [Tooltip("本番業務（6時間勤務）の日当（深夜手当込み）")]
        public int basePayFullShift = 5000;
        [Tooltip("研修期間の最終日")]
        public int trainingLastDay = 7;
        [Tooltip("総合評価 1 点あたりの評価手当")]
        public int payPerScorePoint = 10;
        [Tooltip("精勤手当（ランク別）")]
        public int rankBonusS = 1000;
        public int rankBonusA = 500;
        public int rankBonusB = 0;
        [Tooltip("罰金: 悪意ある接続を許可した場合（1件あたり）")]
        public int deductionPerMaliciousAllow = 1000;
        [Tooltip("罰金: ルール違反の接続を許可した場合（1件あたり）")]
        public int deductionPerPolicyViolation = 300;
        [Tooltip("罰金: 正規の要求を期限切れにした場合（1件あたり）")]
        public int deductionPerExpired = 200;
        [Tooltip("罰金: 問い合わせを未解決のまま終えた場合（1件あたり）")]
        public int deductionPerUnresolved = 300;
        [Tooltip("振込手数料（毎回、本人負担）")]
        public int transferFee = 210;
        [Tooltip("最低支給額")]
        public int minimumPay = 1000;

        public int BasePayFor(int day) => day <= trainingLastDay ? basePay : basePayFullShift;
    }
}
