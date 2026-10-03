using UnityEngine;

namespace NightShift.Data
{
    /// <summary>
    /// 業務連絡に添付する文書（マニュアル・資料）。本文はリッチテキスト可。
    /// 置換トークン: {SERVER_LIST} … ネットワーク定義から生成するサーバー一覧 / {OPERATOR} … プレイヤーのユーザー名
    /// </summary>
    [CreateAssetMenu(menuName = "NightShift/Document", fileName = "Document")]
    public class DocumentDefinition : ScriptableObject
    {
        public string documentId;
        public string title;
        [Tooltip("添付ファイル名として表示")]
        public string fileName;
        public string icon = "doc";
        [TextArea(10, 40)] public string body;
    }
}
