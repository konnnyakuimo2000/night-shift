using System.Collections.Generic;
using NightShift.Core;
using NightShift.Systems;
using UnityEngine;
using UnityEngine.UI;

namespace NightShift.UI.Shift
{
    /// <summary>
    /// 画面に作用するウイルスの演出（GLASS・ECHO・BLACK INK・MIRROR・MOLT・WHITEOUT）。状態は ShiftSession.Display が持ち、ここは見せるだけ。
    /// - すべてゲーム画面の中だけ。クリックは一切受けない（審査ボタンの位置・当たり判定・対象は変わらない）
    /// - 描画する部品の数には上限がある（亀裂 10・残像 4・剥がれ 6・染み 12・複製 4重・偽の警告 3枚・白 1枚）。長時間でも増え続けない
    /// - 重なる順: 複製（下）→ 残像 → 剥がれ → 亀裂 → 染み → 白（上）。白は最大でも 0.75 までで、最低限の操作は見える
    /// - GLASS は駆除の後も亀裂が残る。画面の下の［画面の再起動］（この演出の外にある本物のボタン）で消える
    /// - 「点滅を減らす」「控えめ」でも、強さ（進み具合）は同じ。動き・ちらつきだけを止める
    /// - タスクバー（一時停止・設定への入口）には重ねない
    /// </summary>
    public class DisplayVirusFx : MonoBehaviour
    {
        public const int MaxStains = 12, MaxMirrorDepth = 4, MaxFakeAlerts = 3, MaxCracks = 10, MaxEchoes = 4, MaxMoltPatches = 6;
        public const float MaxWhite = 0.75f;

        ShiftDesktop _d;
        RectTransform _mirrorLayer, _echoLayer, _moltLayer, _glassLayer, _inkLayer, _whiteLayer;
        readonly List<Image> _cracks = new List<Image>();
        readonly List<GameObject> _echoes = new List<GameObject>();
        readonly List<GameObject> _molts = new List<GameObject>();
        RetroButton _restart;
        readonly List<Image> _stains = new List<Image>();
        readonly List<Image> _mirrors = new List<Image>();
        readonly List<GameObject> _alerts = new List<GameObject>();
        Image _white;
        float _t;

        static GameSettings Set => GameSettings.Current;

        /// <summary>今ある演出の部品の数（上限の確認用）</summary>
        public int ObjectCount => _stains.Count + _mirrors.Count + _alerts.Count + _cracks.Count + _echoes.Count + _molts.Count + (_white != null ? 1 : 0);
        public const int MaxObjects = MaxStains + MaxMirrorDepth + MaxFakeAlerts + MaxCracks + MaxEchoes + MaxMoltPatches + 1;

        public static DisplayVirusFx Attach(ShiftDesktop d, RectTransform layer)
        {
            var fx = layer.gameObject.AddComponent<DisplayVirusFx>();
            fx._d = d;
            var cg = layer.gameObject.AddComponent<CanvasGroup>();
            cg.blocksRaycasts = false;
            cg.interactable = false;
            fx._mirrorLayer = UIFactory.Rect("Mirror", layer).Stretch();
            fx._echoLayer = UIFactory.Rect("Echo", layer).Stretch();
            fx._moltLayer = UIFactory.Rect("Molt", layer).Stretch();
            fx._glassLayer = UIFactory.Rect("Glass", layer).Stretch();
            fx._inkLayer = UIFactory.Rect("Ink", layer).Stretch();
            fx._whiteLayer = UIFactory.Rect("White", layer).Stretch();
            // 画面の再起動（復旧の操作）は、クリックを通す演出の層の外に置く
            fx._restart = UIFactory.Button(layer.parent, "画面の再起動", () => fx.Restart(), "pc");
            var rt = fx._restart.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0); rt.pivot = new Vector2(0.5f, 0);
            rt.sizeDelta = new Vector2(150, 26);
            rt.anchoredPosition = new Vector2(0, Taskbar.Height + 6);
            fx._restart.gameObject.SetActive(false);
            return fx;
        }

        void Restart()
        {
            if (_d == null || _d.S == null) return;
            bool any = _d.S.Display.RestartScreens();
            _d.ShowStatus(any ? "監視画面の表示を再起動しました。亀裂は消えました。" : "監視画面の表示を再起動しました。");
            AudioFx.Click();
        }

        void Update()
        {
            if (_d == null || _d.S == null) return;
            _t += Time.unscaledDeltaTime;
            var ops = _d.S.Display;
            if (_restart != null && _restart.gameObject.activeSelf != ops.NeedsScreenRestart) _restart.gameObject.SetActive(ops.NeedsScreenRestart);
            UpdateMirror(ops.Level("V12"));
            UpdateEcho(ops.Level("V10"));
            UpdateMolt(ops.Level("V13"), SourceOf(ops, "V13"));
            UpdateGlass(ops.Level("V9"));
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
            _stains.Clear(); _mirrors.Clear(); _alerts.Clear(); _cracks.Clear(); _echoes.Clear(); _molts.Clear();
            if (_restart != null) Destroy(_restart.gameObject);
        }

        static string SourceOf(DisplayVirusOps ops, string id)
        {
            foreach (var d in ops.Active) if (d.VirusId == id && d.Source != null) return d.Source.deviceId;
            return null;
        }

        // ---------------- GLASS: ウィンドウに亀裂が入り、内部がタイル状にずれる ----------------
        void UpdateGlass(float level)
        {
            int want = Mathf.Clamp(Mathf.CeilToInt(level * MaxCracks), 0, MaxCracks);
            while (_cracks.Count < want)
            {
                int i = _cracks.Count;
                var r = new System.Random(i * 3571 + 7);
                // ずれたタイル（下の画面がずれて見えるように、半透明の板を少しずらして置く）
                var tile = UIFactory.Image(_glassLayer, RetroTheme.White, new Color(0.55f, 0.65f, 0.75f, 0.28f), "Tile");
                tile.raycastTarget = false;
                tile.rectTransform.anchorMin = tile.rectTransform.anchorMax = new Vector2((float)r.NextDouble(), (float)r.NextDouble());
                tile.rectTransform.sizeDelta = new Vector2(r.Next(90, 220), r.Next(40, 110));
                tile.rectTransform.anchoredPosition = new Vector2(r.Next(-12, 12), r.Next(-8, 8));
                var crack = UIFactory.Image(tile.transform, Crack, new Color(0.95f, 0.97f, 1f, 0.9f), "Crack");
                crack.raycastTarget = false;
                crack.rectTransform.Stretch(-20, -20, -20, -20);
                crack.rectTransform.localRotation = Quaternion.Euler(0, 0, r.Next(0, 360));
                _cracks.Add(tile);
            }
            while (_cracks.Count > want) { Destroy(_cracks[_cracks.Count - 1].gameObject); _cracks.RemoveAt(_cracks.Count - 1); }
        }

        // ---------------- ECHO: 処理済みの要求が残像として重なる（押せない・時刻に「残像」） ----------------
        void UpdateEcho(float level)
        {
            int want = Mathf.Clamp(Mathf.CeilToInt(level * MaxEchoes), 0, MaxEchoes);
            var past = _d.S.Requests.Processed;
            while (_echoes.Count < want && past.Count > 0)
            {
                int i = _echoes.Count;
                var e = past[Mathf.Max(0, past.Count - 1 - i)];
                var box = UIFactory.Panel(_echoLayer, RetroTheme.Raised, "Echo");
                box.raycastTarget = false;
                box.color = new Color(1, 1, 1, 0.38f);
                var rt = box.rectTransform;
                rt.anchorMin = rt.anchorMax = new Vector2(0, 1); rt.pivot = new Vector2(0, 1);
                rt.sizeDelta = new Vector2(330, 70);
                rt.anchoredPosition = new Vector2(260 + i * 60, -140 - i * 90);
                var t = UIFactory.Label(box.transform, $"［残像 {TimeManager.Format(e.GeneratedTime)}］ {e.RequestId}\n{e.DisplayUser}　{e.SourceLabel} → {e.DestinationLabel}", 12, TextAnchor.UpperLeft, new Color(0.1f, 0.1f, 0.2f, 0.75f));
                t.raycastTarget = false;
                t.rectTransform.Stretch(10, 8, 8, 6);
                _echoes.Add(box.gameObject);
            }
            while (_echoes.Count > want) { Destroy(_echoes[_echoes.Count - 1]); _echoes.RemoveAt(_echoes.Count - 1); }
            // 残像はゆっくり揺らぐ（軽減設定では止める）
            if (!(Set.reduceFlashing || Set.horrorMild))
                for (int i = 0; i < _echoes.Count; i++)
                    if (_echoes[i]) _echoes[i].GetComponent<Image>().color = new Color(1, 1, 1, 0.3f + 0.08f * Mathf.Sin(_t * 0.8f + i));
        }

        // ---------------- MOLT: 表面が剥がれ、別の社員のデスクトップが現れる（端末名が手がかり） ----------------
        void UpdateMolt(float level, string sourceDevice)
        {
            int want = Mathf.Clamp(Mathf.CeilToInt(level * MaxMoltPatches), 0, MaxMoltPatches);
            while (_molts.Count < want)
            {
                int i = _molts.Count;
                var r = new System.Random(i * 6151 + 29);
                var patch = UIFactory.Solid(_moltLayer, new Color(0.0f, 0.5f, 0.5f, 0.85f), "Molt");
                patch.raycastTarget = false;
                var rt = patch.rectTransform;
                rt.anchorMin = rt.anchorMax = new Vector2(0.1f + 0.8f * (float)r.NextDouble(), 0.15f + 0.75f * (float)r.NextDouble());
                rt.sizeDelta = new Vector2(r.Next(140, 240), r.Next(90, 150));
                // 剥がれた縁
                var edge = UIFactory.Solid(patch.transform, new Color(0.85f, 0.82f, 0.75f, 0.9f), "Edge");
                edge.raycastTarget = false;
                edge.rectTransform.StretchH(0, 6, 0, 0);
                // 別の社員のデスクトップ（アイコンと、その端末の名前）
                var ic = UIFactory.Image(patch.transform, RetroTheme.BigIcon("pc"), new Color(1, 1, 1, 0.85f), "Icon");
                ic.raycastTarget = false;
                ic.rectTransform.Place(10, 14, 32, 32);
                var name = UIFactory.Label(patch.transform, "\\\\" + (sourceDevice ?? "???") + "\nデスクトップ", 12, TextAnchor.UpperLeft, new Color(1, 1, 1, 0.9f), true);
                name.raycastTarget = false;
                name.rectTransform.Stretch(48, 14, 6, 6);
                _molts.Add(patch.gameObject);
            }
            while (_molts.Count > want) { Destroy(_molts[_molts.Count - 1]); _molts.RemoveAt(_molts.Count - 1); }
        }

        static Sprite _crack;
        /// <summary>放射状の亀裂</summary>
        static Sprite Crack
        {
            get
            {
                if (_crack != null) return _crack;
                var c = new PixelCanvas(64, 64);
                var w = Color.white;
                c.Line(32, 32, 2, 10, w); c.Line(32, 32, 60, 6, w); c.Line(32, 32, 62, 44, w); c.Line(32, 32, 8, 60, w); c.Line(32, 32, 30, 63, w);
                c.Line(16, 20, 4, 30, w); c.Line(48, 18, 56, 28, w);
                _crack = c.ToSprite();
                return _crack;
            }
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
