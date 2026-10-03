using UnityEngine;

namespace NightShift.Data
{
    public enum PermissionCategory
    {
        SystemUse,      // システム利用権限
        Administrator   // 管理者権限
    }

    /// <summary>
    /// 1つの権限。社員は Permission の Collection を持つ。
    /// 表示名・区分・並び順はすべてデータ側で管理し、UIはこの定義から行を生成する。
    /// </summary>
    [CreateAssetMenu(menuName = "NightShift/Permission", fileName = "Permission")]
    public class PermissionDefinition : ScriptableObject
    {
        [Tooltip("例: FILE_ACCESS, SERVER_ADMIN")]
        public string permissionId;
        public string displayName;
        public PermissionCategory category;
        public int sortOrder;
        [TextArea] public string description;
    }
}
