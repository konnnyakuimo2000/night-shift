using System.Collections.Generic;
using NightShift.Events;
using NightShift.Systems;
using NUnit.Framework;

namespace NightShift.Tests
{
    /// <summary>接続要求の申請理由: 読み物として付くが、判断の手がかりにはならない</summary>
    public class RequestReasonTests : NightShiftTest
    {
        [Test]
        public void Reasons_AreAttached_Deterministic_AndNotTiedToMaliciousness()
        {
            var s = TestData.Session(20, 3);
            var legit = new HashSet<string>();
            var hostile = new HashSet<string>();
            var emp = s.Employees.All[0];
            for (int i = 0; i < 400; i++)
            {
                foreach (bool bad in new[] { false, true })
                {
                    var e = new EventInstance { RequestId = $"REQ-{i:0000}", Employee = emp, DisplayUser = emp.fullName, AccessType = "共有ファイル（閲覧）", IsMalicious = bad };
                    string r = RequestReasons.For(e, 3);
                    Assert.IsNotEmpty(r);
                    Assert.AreEqual(r, RequestReasons.For(e, 3), "同じ要求なら同じ理由");
                    (bad ? hostile : legit).Add(r);
                }
            }
            // 不正かどうかで理由の集まりが変わらない（理由で見分けられない）
            CollectionAssert.AreEquivalent(legit, hostile);
            Assert.GreaterOrEqual(legit.Count, 8, "理由に十分なバリエーションがある");
        }

        [Test]
        public void SubmittedRequests_GetReasons_ExceptQuestTargets()
        {
            var s = TestData.Session(20, 5);
            s.StartShift();
            var def = s.Day.routinePool[0];
            EventInstance e = null;
            for (int i = 0; i < 30 && e == null; i++) e = s.Factory.Build(def);
            Assert.IsNotNull(e);
            s.Requests.Submit(e);
            Assert.IsNotEmpty(e.Reason);
            var q = new EventInstance { QuestTag = "SQ01-TEST", Employee = e.Employee, Device = e.Device, SourceIp = e.SourceIp, Destination = e.Destination, AccessType = e.AccessType };
            s.Requests.Submit(q);
            Assert.IsNull(q.Reason, "サブクエストの対象要求には付けない");
        }
    }
}
