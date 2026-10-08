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
        public int taskPay;          // 出来高手当（こなした仕事の件数分）
        public int tasksDone;
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
        public List<LedgerEntry> ledger = new List<LedgerEntry>(); // 給与以外の入出金（サブクエストの個別入金など）
        public int version; // 給与体系の版。古い版の明細は読み込み時に破棄する

        const int CurrentVersion = 2;   // 2: ブラック企業の給与体系（日当制・罰金）

        const string Key = "NightShift.Wallet";

        public int TotalEarned
        {
            get { int s = 0; foreach (var p in slips) s += p.total; return s; }
        }

        public int Balance => TotalEarned - spent + LedgerTotal;

        public int LedgerTotal
        {
            get { int s = 0; foreach (var e in ledger) s += e.amount; return s; }
        }

        /// <summary>給与以外の入出金を記録する。同じ txId は一度だけ。記録したら true</summary>
        public bool AddEntry(string txId, int day, string source, string label, int amount)
        {
            if (ledger.Exists(e => e.txId == txId)) return false;
            ledger.Add(new LedgerEntry { txId = txId, day = day, source = source, label = label, amount = amount });
            Save();
            return true;
        }

        public int SumSource(string sourcePrefix)
        {
            int s = 0;
            foreach (var e in ledger) if (e.source != null && e.source.StartsWith(sourcePrefix)) s += e.amount;
            return s;
        }

        public PaySlip Find(int day) => slips.Find(p => p.day == day);

        public static OperatorWallet Load()
        {
            try
            {
                var json = PlayerPrefs.GetString(Key, "");
                if (!string.IsNullOrEmpty(json))
                {
                    var w = JsonUtility.FromJson<OperatorWallet>(json);
                    if (w != null && w.version == CurrentVersion) return w;
                }
            }
            catch { }
            return new OperatorWallet { version = CurrentVersion };
        }

        public void Save()
        {
            if (CareerRecord.SaveDisabled) return; // テスト中は保存しない
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
        /// <summary>デバッグ用: 所持金を増やす</summary>
        public void DebugAddMoney(int amount)
        {
            spent -= amount;
            Save();
        }

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
            ledger.Clear();
            Save();
        }

        public string Snapshot() => JsonUtility.ToJson(this);

        public void Restore(string json)
        {
            try
            {
                var w = JsonUtility.FromJson<OperatorWallet>(json);
                if (w == null) return;
                slips = w.slips; spent = w.spent; version = w.version;
                ledger = w.ledger ?? new List<LedgerEntry>();
                Save();
            }
            catch { }
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
                basePay = p.BasePayFor(day),
                performancePay = r.Overall * p.payPerScorePoint,
                tasksDone = ev.TasksDone,
                taskPay = ev.TasksDone * p.payPerTask
            };
            switch (r.Rank)
            {
                case "S": slip.rankBonus = p.rankBonusS; break;
                case "A": slip.rankBonus = p.rankBonusA; break;
                case "B": slip.rankBonus = p.rankBonusB; break;
            }
            if (ev.SeniorHelped && slip.rankBonus > 0)
            {
                slip.rankBonus = 0;
                slip.deductionNotes.Add("先輩の手伝いがあったため精勤手当なし");
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
            if (ev.ExpiredLegit > 0 && p.deductionPerExpired > 0)
            {
                slip.deduction += ev.ExpiredLegit * p.deductionPerExpired;
                slip.deductionNotes.Add($"期限切れ {ev.ExpiredLegit} 件");
            }
            if (ev.Unresolved > 0 && p.deductionPerUnresolved > 0)
            {
                slip.deduction += ev.Unresolved * p.deductionPerUnresolved;
                slip.deductionNotes.Add($"未解決の問い合わせ {ev.Unresolved} 件");
            }
            if (p.transferFee > 0)
            {
                slip.deduction += p.transferFee;
                slip.deductionNotes.Add("振込手数料");
            }
            if (p.maxDeductionPerNight > 0 && slip.deduction > p.maxDeductionPerNight)
            {
                slip.deduction = p.maxDeductionPerNight;
                slip.deductionNotes.Add($"（上限 {OperatorWallet.Yen(p.maxDeductionPerNight)}）");
            }
            slip.total = Mathf.Max(p.minimumPay, slip.basePay + slip.performancePay + slip.rankBonus + slip.taskPay - slip.deduction);
            return slip;
        }
    }
}
