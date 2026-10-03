using System;
using System.Collections.Generic;
using NightShift.Data;
using NightShift.Systems;
using UnityEngine;

namespace NightShift.Core
{
    /// <summary>1日分の給与明細</summary>
    [Serializable]
    public class PaySlip
    {
        public int day;
        public int score;
        public string rank;
        public int basePay;
        public int performancePay;
        public int rankBonus;
        public int deduction;
        public int total;
        public List<string> deductionNotes = new List<string>();
    }

    /// <summary>
    /// 運用担当者の給与・所持金。スコアに応じて給与が支払われ、後のストアシステムで使う。
    /// 同じ日を何度やり直しても稼げないよう、日ごとに「最高支給額」だけを計上する。
    /// </summary>
    [Serializable]
    public class OperatorWallet
    {
        public List<PaySlip> slips = new List<PaySlip>();
        public int spent;   // ストアでの支出（後で使う）

        const string Key = "NightShift.Wallet";

        public int TotalEarned
        {
            get { int s = 0; foreach (var p in slips) s += p.total; return s; }
        }

        public int Balance => TotalEarned - spent;

        public PaySlip Find(int day) => slips.Find(p => p.day == day);

        public static OperatorWallet Load()
        {
            try
            {
                var json = PlayerPrefs.GetString(Key, "");
                if (!string.IsNullOrEmpty(json))
                {
                    var w = JsonUtility.FromJson<OperatorWallet>(json);
                    if (w != null) return w;
                }
            }
            catch { }
            return new OperatorWallet();
        }

        public void Save()
        {
            try
            {
                PlayerPrefs.SetString(Key, JsonUtility.ToJson(this));
                PlayerPrefs.Save();
            }
            catch { }
        }

        /// <summary>明細を計上する。前回より高い場合だけ更新。戻り値は前回の明細（無ければ null）。</summary>
        public PaySlip Record(PaySlip slip, out bool updated)
        {
            var prev = Find(slip.day);
            updated = prev == null || slip.total > prev.total;
            if (updated)
            {
                if (prev != null) slips.Remove(prev);
                slips.Add(slip);
                slips.Sort((a, b) => a.day.CompareTo(b.day));
                Save();
            }
            return prev;
        }

        /// <summary>ストア用（後で使う）。残高が足りなければ false。</summary>
        public bool TrySpend(int amount)
        {
            if (amount <= 0 || amount > Balance) return false;
            spent += amount;
            Save();
            return true;
        }

        public void ResetAll()
        {
            slips.Clear();
            spent = 0;
            Save();
        }

        public static string Yen(int v) => (v < 0 ? "−" : "") + "¥" + Math.Abs(v).ToString("N0");
    }

    /// <summary>評価から給与明細を計算する</summary>
    public static class Payroll
    {
        public static PaySlip Compute(int day, EvaluationResult r, EvaluationManager ev, PayrollSettings p)
        {
            var slip = new PaySlip
            {
                day = day,
                score = r.Overall,
                rank = r.Rank,
                basePay = p.basePay,
                performancePay = r.Overall * p.payPerScorePoint
            };
            switch (r.Rank)
            {
                case "S": slip.rankBonus = p.rankBonusS; break;
                case "A": slip.rankBonus = p.rankBonusA; break;
                case "B": slip.rankBonus = p.rankBonusB; break;
            }
            if (ev.MaliciousAllow > 0)
            {
                slip.deduction += ev.MaliciousAllow * p.deductionPerMaliciousAllow;
                slip.deductionNotes.Add($"不正な接続の許可 {ev.MaliciousAllow} 件");
            }
            if (ev.PolicyViolation > 0)
            {
                slip.deduction += ev.PolicyViolation * p.deductionPerPolicyViolation;
                slip.deductionNotes.Add($"ルール違反の接続の許可 {ev.PolicyViolation} 件");
            }
            slip.total = Mathf.Max(p.minimumPay, slip.basePay + slip.performancePay + slip.rankBonus - slip.deduction);
            return slip;
        }
    }
}
