using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace NightShift.UI
{
    /// <summary>
    /// オリジナルのドット絵マウスポインター（矢印・指差し・Iビーム）。
    /// ポインターの下にある UI に合わせて自動で切り替える。
    /// </summary>
    public class RetroCursor : MonoBehaviour
    {
        enum Kind { None, Arrow, Hand, Beam }

        // 16x16 の絵を 1.25 倍（20x20）で描く。
        // Windows のハードウェアカーソルは画像を標準サイズ（32x32）に合わせて拡大するため、
        // 画像は 32x32 のまま、左上に 20x20 の絵を描いて残りを透明にする。
        const float Scale = 1.25f;
        const int Canvas = 32;
        const int ArtSize = 20;
        static Texture2D _arrow, _hand, _beam;
        Kind _current = Kind.None;
        readonly List<RaycastResult> _hits = new List<RaycastResult>();

        // B: 濃紺の縁取り / W: 白 / C: 水色のアクセント / S: 影
        static readonly string[] ArrowArt =
        {
            "B...............",
            "BB..............",
            "BWB.............",
            "BWWB............",
            "BWWWB...........",
            "BWWWWB..........",
            "BWWWWWB.........",
            "BWWWWWWB........",
            "BWWWWWWWB.......",
            "BWWWWWCCCB......",
            "BWWWWWBBBBB.....",
            "BWWBWWBS........",
            "BWB.BWWBS.......",
            "BB..BWWBS.......",
            ".....BWWBS......",
            ".....BBBBS......",
        };

        static readonly string[] HandArt =
        {
            "....BB..........",
            "...BWWB.........",
            "...BWWB.........",
            "...BWWB.........",
            "...BWWBBB.......",
            "...BWWBWWBBB....",
            "BB.BWWBWWBWWBB..",
            "BWBBWWWWWWWWWWB.",
            "BWWBWWWWWWWWWWBS",
            ".BWWWWWWWWWWWWBS",
            "..BWWWWWWWWWWWBS",
            "..BWWWWWWWWWWBS.",
            "...BWWWWWWWWWBS.",
            "...BWCCCCCCWBS..",
            "....BWWWWWWWBS..",
            "....BBBBBBBBBS..",
        };

        static readonly string[] BeamArt =
        {
            "...BBB.BBB......",
            "......B.........",
            "......B.........",
            "......B.........",
            "......B.........",
            "......B.........",
            "......B.........",
            "......B.........",
            "......B.........",
            "......B.........",
            "......B.........",
            "......B.........",
            "......B.........",
            "......B.........",
            "...BBB.BBB......",
            "................",
        };

        static Texture2D Make(string[] art)
        {
            int n = Canvas;
            var t = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            t.hideFlags = HideFlags.DontSave;
            var px = new Color[n * n]; // 既定は透明
            var navy = new Color(0.05f, 0.08f, 0.32f, 1f);
            var cyan = new Color(0.2f, 0.85f, 0.95f, 1f);
            var shadow = new Color(0f, 0f, 0f, 0.35f);
            for (int ty = 0; ty < ArtSize; ty++)
            for (int tx = 0; tx < ArtSize; tx++)
            {
                int x = Mathf.Min(15, (int)(tx / Scale));
                int y = Mathf.Min(15, (int)(ty / Scale)); // 上からの行
                char ch = x < art[y].Length ? art[y][x] : '.';
                Color c = ch == 'B' ? navy : ch == 'W' ? Color.white : ch == 'C' ? cyan : ch == 'S' ? shadow : new Color(0, 0, 0, 0);
                px[(n - 1 - ty) * n + tx] = c; // テクスチャは下から
            }
            t.SetPixels(px);
            t.Apply();
            return t;
        }

        static Sprite _arrowSprite;

        /// <summary>矢印カーソルの絵（ホラー演出の「もう1つのカーソル」に使う）</summary>
        public static Sprite ArrowSprite()
        {
            if (_arrowSprite != null) return _arrowSprite;
            var tex = Make(ArrowArt);
            _arrowSprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0, 1), 100f);
            _arrowSprite.hideFlags = HideFlags.DontSave;
            return _arrowSprite;
        }

        public static RetroCursor Install(GameObject host)
        {
            if (_arrow == null) _arrow = Make(ArrowArt);
            if (_hand == null) _hand = Make(HandArt);
            if (_beam == null) _beam = Make(BeamArt);
            return host.AddComponent<RetroCursor>();
        }

        void Set(Kind k)
        {
            if (k == _current) return;
            _current = k;
            switch (k)
            {
                case Kind.Hand: Cursor.SetCursor(_hand, new Vector2(Mathf.Round(4.5f * Scale), 0), CursorMode.Auto); break;
                case Kind.Beam: Cursor.SetCursor(_beam, new Vector2(Mathf.Round(6.5f * Scale), Mathf.Round(7.5f * Scale)), CursorMode.Auto); break;
                default: Cursor.SetCursor(_arrow, Vector2.zero, CursorMode.Auto); break;
            }
        }

        void Update()
        {
            var es = EventSystem.current;
            if (es == null) { Set(Kind.Arrow); return; }
            var ped = new PointerEventData(es) { position = Input.mousePosition };
            _hits.Clear();
            es.RaycastAll(ped, _hits);
            Kind k = Kind.Arrow;
            if (_hits.Count > 0)
            {
                var go = _hits[0].gameObject;
                var input = go.GetComponentInParent<InputField>();
                if (input != null && input.interactable) k = Kind.Beam;
                else
                {
                    var sel = go.GetComponentInParent<Selectable>();
                    if (sel != null && sel.interactable && !(sel is Scrollbar)) k = Kind.Hand;
                    else
                    {
                        // 通知・メニュー項目など（画面全体を覆うクリック受け付け用の透明レイヤーは除く）
                        var ch = go.GetComponentInParent<ClickHandler>();
                        if (ch != null && go.GetComponentInParent<RetroWindow>() == null)
                        {
                            var rt = (RectTransform)ch.transform;
                            var canvas = ch.GetComponentInParent<Canvas>();
                            float scale = canvas != null ? canvas.scaleFactor : 1f;
                            if (rt.rect.width * scale < Screen.width * 0.8f) k = Kind.Hand;
                        }
                    }
                }
            }
            Set(k);
        }

        void OnDisable()
        {
            Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
            _current = Kind.None;
        }
    }
}
