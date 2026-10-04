using System;
using System.Collections.Generic;
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
        [Tooltip("この文書を添付として表示する条件（Features の ID。空なら常に）")]
        public string requiredFeature;
        [Tooltip("前書き（常に表示）")]
        [TextArea(4, 20)] public string body;
        [Tooltip("ページ（業務ごと）。requiredFeature が解放されているページだけ表示する")]
        public List<DocumentSection> sections = new List<DocumentSection>();
    }

    [Serializable]
    public class DocumentSection
    {
        [Tooltip("ページのタイトル（目次に表示）")]
        public string title;
        public string requiredFeature;
        [TextArea(4, 30)] public string text;
    }
}
