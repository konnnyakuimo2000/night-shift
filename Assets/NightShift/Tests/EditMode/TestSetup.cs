using NightShift.Core;
using NUnit.Framework;

namespace NightShift.Tests
{
    /// <summary>
    /// すべてのテストの前後処理。プレイヤーのセーブ（30日間の記録）に触れないよう、
    /// テスト中は新しい記録に差し替え、保存を止める。
    /// </summary>
    [SetUpFixture]
    public class TestSetup
    {
        CareerRecord _original;

        [OneTimeSetUp]
        public void Before()
        {
            _original = CareerRecord.Current;
            CareerRecord.SaveDisabled = true;
        }

        [OneTimeTearDown]
        public void After()
        {
            CareerRecord.SetCurrentForTests(_original);
            CareerRecord.SaveDisabled = false;
        }
    }

    /// <summary>各テストの前に、まっさらな記録にする</summary>
    public abstract class NightShiftTest
    {
        [SetUp]
        public void FreshCareer() => CareerRecord.SetCurrentForTests(new CareerRecord { seed = 42, trust = 70 });
    }
}
