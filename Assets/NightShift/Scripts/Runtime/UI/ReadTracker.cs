using System;
using UnityEngine;
using UnityEngine.UI;

namespace NightShift.UI
{
    /// <summary>
    /// スクロール領域を最後まで読んだ（一番下までスクロールした）ことを検出する。
    /// 本文が短くてスクロール不要な場合は、表示しただけで読了とする。
    /// </summary>
    public class ReadTracker : MonoBehaviour
    {
        ScrollRect _scroll;
        Action _onRead;
        bool _done;
        int _frames;

        public static ReadTracker Attach(ScrollRect scroll, Action onRead)
        {
            var t = scroll.gameObject.AddComponent<ReadTracker>();
            t._scroll = scroll;
            t._onRead = onRead;
            return t;
        }

        void Update()
        {
            if (_done || _scroll == null) return;
            // レイアウト確定（コンテンツ高さの計算）を数フレーム待つ
            if (++_frames < 4) return;
            var content = _scroll.content;
            var viewport = _scroll.viewport;
            if (content == null || viewport == null) return;
            bool fits = content.rect.height <= viewport.rect.height + 2f;
            bool bottom = _scroll.verticalNormalizedPosition <= 0.01f;
            if (fits || bottom)
            {
                _done = true;
                _onRead?.Invoke();
            }
        }
    }
}
