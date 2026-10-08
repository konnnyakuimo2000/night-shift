using NightShift.Core;
using NightShift.Data;
using NightShift.Systems;
using NUnit.Framework;

namespace NightShift.Tests
{
    /// <summary>こなした仕事の数だけ、評価と給与（出来高手当）が上がる</summary>
    public class TaskVolumeTests : NightShiftTest
    {
        [Test]
        public void MoreTasksDone_RaisesScore_WithSameAccuracy()
        {
            var few = new EvaluationManager { CorrectAllow = 8, CorrectBlock = 2 };
            var many = new EvaluationManager { CorrectAllow = 40, CorrectBlock = 10 };
            Assert.AreEqual(10, few.TasksDone);
            Assert.AreEqual(50, many.TasksDone);
            Assert.Greater(many.Compute().Overall, few.Compute().Overall, "正確さが同じなら、多くこなした方が評価が高い");
            Assert.AreEqual(EvaluationManager.MaxTaskPoints, new EvaluationManager { CorrectAllow = 500 }.TaskPoints, "加点には上限がある");
        }

        [Test]
        public void TaskPay_IsPaidPerTaskDone()
        {
            var p = new PayrollSettings();
            var r = new EvaluationResult { Overall = 80, Rank = "A" };
            var none = Payroll.Compute(20, r, new EvaluationManager(), p);
            var ev = new EvaluationManager { CorrectAllow = 20, CorrectBlock = 5, HelpDeskCorrect = 3, RemoteResolved = 2 };
            var slip = Payroll.Compute(20, r, ev, p);
            Assert.AreEqual(30, slip.tasksDone);
            Assert.AreEqual(30 * p.payPerTask, slip.taskPay);
            Assert.AreEqual(none.total + 30 * p.payPerTask, slip.total, "出来高手当の分だけ支給額が増える");
        }

        [Test]
        public void WrongDecisions_DoNotCountAsTasksDone()
        {
            var ev = new EvaluationManager { FalsePositive = 10, MaliciousAllow = 3, HelpDeskIncorrect = 4, RemoteFailed = 2 };
            Assert.AreEqual(0, ev.TasksDone, "間違えた判断は件数に数えない");
        }
    }
}
