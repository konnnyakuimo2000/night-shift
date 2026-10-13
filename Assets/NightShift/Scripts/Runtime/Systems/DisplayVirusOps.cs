using System.Collections.Generic;
using NightShift.Data;

namespace NightShift.Systems
{
    /// <summary>画面に作用する感染1件（感染した社員PC → 監視画面）</summary>
    public class DisplayInfection
    {
        public Infection Infection;
        public EmployeeData Source;
        public string VirusId => Infection.VirusId;
        /// <summary>症状の進み具合 0〜1（1 = 最大。画面演出の強さ）</summary>
        public float Progress;
        /// <summary>隔離・切断で広がりが止まっている</summary>
        public bool Paused;
        /// <summary>駆除された（症状が引いていく）</summary>
        public bool Receding;
        public bool MaxDamageRecorded;
    }

    /// <summary>
    /// 画面に作用するウイルス（BLACK INK・MIRROR・WHITEOUT）の進行。状態はここだけが持ち、画面演出（DisplayVirusFx）は読むだけ。
    /// 進行 → 隔離で停止 → 駆除で症状が引く。最大まで進んだら、その夜に一度だけ被害を記録する。
    /// 演出の軽減設定があっても、進み方・止まる条件・戻る条件は同じ。
    /// </summary>
    public class DisplayVirusOps
    {
        readonly ShiftSession _s;
        readonly List<DisplayInfection> _list = new List<DisplayInfection>();
        public IReadOnlyList<DisplayInfection> Active => _list;
        public const float RecedeMinutes = 2f;

        public DisplayVirusOps(ShiftSession s) { _s = s; }

        /// <summary>最大になるまでの時間（ゲーム内の分）</summary>
        public static float GrowMinutes(string id) => id == "V14" ? 40f : 30f;

        public DisplayInfection Track(Infection inf, EmployeeData source)
        {
            if (inf == null || !VirusCatalog.IsDisplayVirus(inf.VirusId)) return null;
            var cur = _list.Find(x => ReferenceEquals(x.Infection, inf));
            if (cur != null) return cur;
            cur = new DisplayInfection { Infection = inf, Source = source };
            _list.Add(cur);
            return cur;
        }

        /// <summary>その種類の症状の強さ（同じ種類が複数あるときは最も強いもの）</summary>
        public float Level(string virusId)
        {
            float v = 0;
            foreach (var d in _list) if (d.VirusId == virusId && d.Progress > v) v = d.Progress;
            return v;
        }

        public void Tick(float delta)
        {
            for (int i = _list.Count - 1; i >= 0; i--)
            {
                var d = _list[i];
                if (!_s.Security.IsActive(d.Infection))
                {
                    // 駆除された: 症状が引く。消えきったら記録を「対応済み」に
                    d.Receding = true;
                    d.Progress -= delta / RecedeMinutes;
                    if (d.Progress <= 0)
                    {
                        _list.RemoveAt(i);
                        _s.Discover(d.VirusId, DiscoveryStage.Recovered);
                    }
                    continue;
                }
                d.Paused = Events.ThreatScripts.Disconnected(_s, d.Source);
                if (d.Paused) continue;
                if (d.Progress > 0.05f) _s.Discover(d.VirusId, DiscoveryStage.Observed);
                d.Progress = System.Math.Min(1f, d.Progress + delta / GrowMinutes(d.VirusId));
                if (d.Progress >= 1f && !d.MaxDamageRecorded)
                {
                    d.MaxDamageRecorded = true;
                    _s.AddDamage($"監視画面が読めなくなった（{VirusCatalog.Name(d.VirusId)}）");
                }
            }
        }

        /// <summary>夜の終わり（演出・入力の制限を残さない）</summary>
        public void Clear() => _list.Clear();
    }
}
