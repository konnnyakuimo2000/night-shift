using System.Collections.Generic;
using NightShift.Core;
using NightShift.Systems;
using UnityEngine;
using UnityEngine.UI;

namespace NightShift.UI.Shift
{
    /// <summary>
    /// 画面に作用するウイルスの演出（BLACK INK・MIRROR・WHITEOUT）。状態は ShiftSession.Display が持ち、ここは見せるだけ。
    /// - すべてゲーム画面の中だけ。クリックは一切受けない（審査ボタンの位置・当たり判定・対象は変わらない）
    /// - 描画する部品の数には上限がある（染み 12・複製 4重・偽の警告 3枚・白 1枚）。長時間でも増え続けない
    /// - 重なる順: 複製（下）→ 染み → 白（上）。白は最大でも 0.75 までで、最低限の操作は見える
    /// - 「点滅を減らす」「控えめ」でも、強さ（進み具合）は同じ。動き・ちらつきだけを止める
    /// - タスクバー（一時停止・設定への入口）には重ねない
    /// </summary>
    public class DisplayVirusFx : MonoBehaviour
    {
        public const int MaxStains = 12, MaxMirrorDepth = 4, MaxFakeAlerts = 3;
        public const float MaxWhite = 0.75f;

        ShiftDesktop _d;
        RectTransform _mirrorLayer, _inkLayer, _whiteLayer;
        readonly List<Image> _stains = new List<Image>();
        readonly List<Image> _mirrors = new List<Image>();
        readonly List<GameObject> _alerts = new List<GameObject>();
        Image _white;
        float _t;

        static GameSettings Set => GameSettings.Current;

        /// <summary>今ある演出の部品の数（上限の確認用）</summary>
        public int ObjectCount => _stains.Count + _mirrors.Count + _alerts.Count + (_white != null ? 1 : 0);

        public static DisplayVirusFx Attach(ShiftDesktop d, RectTransform layer)
        {
            var fx = layer.gameObject.AddComponent<DisplayVirusFx>();
            fx._d = d;
            var cg = layer.gameObject.AddComponent<CanvasGroup>();
            cg.blocksRaycasts = false;
            cg.interactable = false;
            fx._mirrorLayer = UIFactory.Rect("Mirror", layer).Stretch();
            fx._inkLayer = UIFactory.Rect("Ink", layer).Stretch();
            fx._whiteLayer = UIFactory.Rect("White", layer).Stretch();
            return fx;
        }

        void Update()
        {
            if (_d == null || _d.S == null) return;
            _t += Time.unscaledDeltaTime;
            var ops = _d.S.Display;
            UpdateMirror(ops.Level("V12"));
            UpdateInk(ops.Level("V11"));
            UpdateWhite(ops.Level("V14"));
        }

        // ---------------- BLACK INK: 隅から黒い染みが広がる ----------------
        void UpdateInk(float level)
        {
            int want = Mathf.Clamp(Mathf.CeilToInt(level * MaxStains), 0, MaxStains);
            while (_stains.Count < want)
            {
                int i = _stains.Count;
                var img = UIFactory.Image(_inkLayer, Stain, new Color(0.02f, 0.02f, 0.04f, 0.92f), "Stain");
                img.raycastTarget = false;
                var rt = img.rectTransform;
                // 四隅から順に。同じ位置は乱数の種で決める（毎回同じ広がり方）
                var r = new System.Random(i * 7919 + 13);
                Vector2 corner = new Vector2(i % 2, (i / 2) % 2);
                rt.anchorMin = rt.anchorMax = corner;
                rt.pivot = corner;
                rt.anchoredPosition = new Vector2((corner.x > 0 ? -1 : 1) * r.Next(0, 260), (corner.y > 0 ? -1 : 1) * r.Next(0, 200));
                rt.sizeDelta = Vector2.zero;
                rt.localRotation = Quaternion.Euler(0, 0, r.Next(0, 360));
                _stains.Add(img);
            }
            while (_stains.Count > want)
            {
                Destroy(_stains[_stains.Count - 1].gameObject);
                _stains.RemoveAt(_stains.Count - 1);
            }
            for (int i = 0; i < _stains.Count; i++)
            {
                // 進み具合に合わせて大きくなる（控えめ・点滅を減らす: 揺らさない）
                float target = 120 + 260 * level * (0.6f + 0.4f * ((i * 37) % 10) / 10f);
                bool calm = Set.reduceFlashing || Set.horrorMild;
                float wobble = calm ? 0 : Mathf.Sin(_t * 0.7f + i) * 6f;
                var rt = _stains[i].rectTransform;
                float s = Mathf.MoveTowards(rt.sizeDelta.x, target + wobble, 80 * Time.unscaledDeltaTime);
                rt.sizeDelta = new Vector2(s, s * 0.8f);
            }
        }

        // ---------------- MIRROR: 画面の中に画面が奥へ複製される・偽の警告窓 ----------------
        void UpdateMirror(float level)
        {
            int depth = Mathf.Clamp(Mathf.CeilToInt(level * MaxMirrorDepth), 0, MaxMirrorDepth);
            while (_mirrors.Count < depth)
            {
                int i = _mirrors.Count;
                var frame = UIFactory.Image(_mirrorLayer, RetroTheme.Raised, new Color(0.75f, 0.8f, 0.9f, 0.35f), "MirrorFrame");
                frame.raycastTarget = false;
                float inset = 60 + i * 70;
                frame.rectTransform.Stretch(inset, inset * 0.7f, inset, inset * 0.7f + 40);
                var bar = UIFactory.Image(frame.transform, RetroTheme.GradientSprite(RetroTheme.TitleActiveA, RetroTheme.TitleActiveB), new Color(1, 1, 1, 0.5f), "Bar");
                bar.raycastTarget = false;
                bar.rectTransform.StretchH(3, 18, 3, 3);
                _mirrors.Add(frame);
            }
            while (_mirrors.Count > depth)
            {
                Destroy(_mirrors[_mirrors.Count - 1].gameObject);
                _mirrors.RemoveAt(_mirrors.Count - 1);
            }
            int alerts = Mathf.Clamp(Mathf.FloorToInt(level * (MaxFakeAlerts + 0.99f)), 0, MaxFakeAlerts);
            while (_alerts.Count < alerts)
            {
                // 偽の警告窓: 押せるボタンは置かない（クリックはそのまま下の画面へ届く）。審査ボタンのある右下は避ける
                int i = _alerts.Count;
                var box = UIFactory.Panel(_mirrorLayer, RetroTheme.Raised, "FakeAlert");
                box.raycastTarget = false;
                var rt = box.rectTransform;
                rt.anchorMin = rt.anchorMax = new Vector2(0, 1);
                rt.pivot = new Vector2(0, 1);
                rt.sizeDelta = new Vector2(260, 96);
                rt.anchoredPosition = new Vector2(80 + i * 150, -90 - i * 70);
                var bar = UIFactory.Image(box.transform, RetroTheme.GradientSprite(RetroTheme.TitleActiveA, RetroTheme.TitleActiveB), Color.white, "Bar");
                bar.raycastTarget = false;
                bar.rectTransform.StretchH(3, 18, 3, 3);
                var ic = UIFactory.Image(box.transform, RetroTheme.BigIcon("warning"), Color.white, "Icon");
                ic.raycastTarget = false;
                ic.rectTransform.Place(12, 34, 32, 32);
                // 文字は出さない（意味のない文字化けだけ）
                var t = UIFactory.Label(box.transform, HorrorFx.Garble("xxxxxxxx", i + 101), 12, TextAnchor.MiddleLeft, Color.black);
                t.raycastTarget = false;
                t.rectTransform.Place(52, 30, 200, 40);
                _alerts.Add(box.gameObject);
            }
            while (_alerts.Count > alerts)
            {
                Destroy(_alerts[_alerts.Count - 1]);
                _alerts.RemoveAt(_alerts.Count - 1);
            }
        }

        // ---------------- WHITEOUT: 色と文字が白く薄れる ----------------
        void UpdateWhite(float level)
        {
            if (level <= 0.001f)
            {
                if (_white != null) { Destroy(_white.gameObject); _white = null; }
                return;
            }
            if (_white == null)
            {
                _white = UIFactory.Solid(_whiteLayer, Color.white, "Whiteout");
                _white.raycastTarget = false;
                _white.rectTransform.Stretch();
            }
            // 急な明暗の変化はしない（ゆっくり近づく）
            float target = MaxWhite * level;
            var c = _white.color;
            c.a = Mathf.MoveTowards(c.a, target, 0.2f * Time.unscaledDeltaTime);
            _white.color = c;
        }

        void OnDestroy()
        {
            _stains.Clear(); _mirrors.Clear(); _alerts.Clear();
        }

        static Sprite _stain;
        /// <summary>インクの染み（ドット絵の円の集まり）</summary>
        static Sprite Stain
        {
            get
            {
                if (_stain != null) return _stain;
                var c = new PixelCanvas(48, 40);
                var ink = new Color(1, 1, 1, 1);
                c.Disc(24, 20, 14, ink);
                c.Disc(12, 14, 7, ink);
                c.Disc(36, 26, 8, ink);
                c.Disc(30, 8, 5, ink);
                c.Disc(16, 30, 6, ink);
                c.Rect(23, 30, 2, 9, ink); // しずく
                _stain = c.ToSprite();
                return _stain;
            }
        }
    }
}
