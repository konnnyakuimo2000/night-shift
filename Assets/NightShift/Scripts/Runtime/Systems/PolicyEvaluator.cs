using NightShift.Data;
using NightShift.Events;

namespace NightShift.Systems
{
    /// <summary>
    /// その日のルールと「実際の社員データ」から、接続要求の期待動作を算出する。
    /// シナリオ側に正解をハードコードしないため、社員データを編集しても判定が破綻しない。
    /// </summary>
    public static class PolicyEvaluator
    {
        public static ExpectedAction Evaluate(EventInstance e, PolicyRuleSet rules, out string reason)
        {
            reason = "";
            if (e.Definition != null && e.Definition.expectedAction != ExpectedAction.PolicyDerived)
            {
                reason = "定義で指定";
                return e.Definition.expectedAction;
            }

            var dest = e.Destination;
            bool external = e.Zone == SourceZone.External;

            if (e.IsOperatorAccount && rules.blockOperatorAccount)
            {
                reason = "運用担当者（自分）のIDからの接続";
                return ExpectedAction.Block;
            }

            if (e.Employee == null)
            {
                if (rules.blockUnknownUser) { reason = "利用者が社員として登録されていない"; return ExpectedAction.Block; }
            }
            else
            {
                if (rules.requireActiveEmployment && !e.Employee.IsActive)
                {
                    reason = "在籍していない社員（" + EmployeeData.EmploymentLabel(e.Employee.employment) + "）";
                    return ExpectedAction.Block;
                }
                if (rules.requireOnShift && !e.Employee.onShift)
                {
                    reason = "退勤している社員";
                    return ExpectedAction.Block;
                }
            }

            if (dest != null && dest.isAdministrative && external && rules.blockExternalToAdministrative)
            {
                reason = "社外から管理系システム";
                return ExpectedAction.Block;
            }

            bool registered = e.Employee != null && e.Employee.OwnsDevice(e.Device);
            if (!registered && rules.blockUnregisteredDevice)
            {
                reason = "本人の登録端末ではない";
                return ExpectedAction.Block;
            }
            if (!registered && dest != null && dest.isCritical && rules.blockUnregisteredToCritical)
            {
                reason = "未登録端末から重要システム";
                return ExpectedAction.Block;
            }

            // なりすまし: 社内の登録端末を名乗っているのに、接続元IPがその端末のIPと違う
            if (rules.requireSourceIpMatch && registered && !external && e.Employee != null
                && e.Device == e.Employee.deviceId && !string.IsNullOrEmpty(e.ExpectedDeviceIp) && e.SourceIp != e.ExpectedDeviceIp)
            {
                reason = "接続元IPが端末IPと違う";
                return ExpectedAction.Block;
            }

            var access = dest != null ? dest.FindAccess(e.AccessType) : null;
            if (access != null && e.Employee != null)
            {
                if (rules.requirePermission && !string.IsNullOrEmpty(access.requiredPermission) && !e.Employee.HasPermission(access.requiredPermission))
                {
                    reason = "権限なし: " + access.requiredPermission;
                    return ExpectedAction.Block;
                }
                if (rules.requireVpnPermission && access.requiresVpnEnabled && !e.Employee.vpnEnabled)
                {
                    reason = "VPN利用許可なし";
                    return ExpectedAction.Block;
                }
                if (rules.requireVpnPermission && access.requiresVpnEnabled && external && e.Device != e.Employee.laptopDeviceId)
                {
                    reason = "VPN端末ではない";
                    return ExpectedAction.Block;
                }
            }

            if (external && dest != null && dest.zone == NodeZone.Internal && (access == null || !access.requiresVpnEnabled) && rules.requireVpnPermission)
            {
                reason = "社外からVPNを経由しない内部接続";
                return ExpectedAction.Block;
            }

            reason = "ルール上問題なし";
            return ExpectedAction.Allow;
        }
    }
}
