using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace NightShift.UI
{
    /// <summary>
    /// ツール画面の「動き」（1998年ごろの市販ソフト風）。点滅するランプ、回る虫眼鏡、稼働メーター、届いたものの点灯など。
    /// どれも時間は Time.unscaledTime で動く（一時停止中も画面の飾りとして動き続ける）。「点滅を減らす」設定では穏やかにする。
    /// </summary>
    public static class ToolFx
    {
        static bool Calm => Core.GameSettings.Current.reduceFlashing;

        public static readonly Color LedGreen = new Color(0.10f, 0.85f, 0.15f);
        public static readonly Color LedRed = new Color(0.95f, 0.10f, 0.08f);
        public static readonly Color LedAmber = new Color(1.00f, 0.70f, 0.00f);
        public static readonly Color LedOff = new Color(0.35f, 0.38f, 0.35f);

        static Sprite _led;
        /// <summary>丸いランプの絵（縁は暗く、中は白。色はあとで付ける）</summary>
        public static Sprite LedSprite
        {
            get
            {
                if (_led) return _led;
                const int n = 12;
                var t = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontSave };
                var px = new Color[n * n];
                var c = new Vector2(5.5f, 5.5f);
                for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float d = Vector2.Distance(new Vector2(x, y), c);
                    Color col = new Color(0, 0, 0, 0);
                    if (d <= 5.6f) col = d > 4.4f ? new Color(0.22f, 0.22f, 0.22f) : Color.white;
                    px[y * n + x] = col;
                }
                t.SetPixels(px);
                t.Apply();
                _led = Sprite.Create(t, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
                return _led;
            }
        }

        /// <summary>しばらく黄色く光ってから元に戻す（新しく届いた・数が変わった）</summary>
        public static void Flash(Graphic g, float seconds = 0.8f)
        {
            if (g == null) return;
            var f = g.GetComponent<ToolFlash>() ?? g.gameObject.AddComponent<ToolFlash>();
            f.Begin(g, seconds);
        }

        /// <summary>parent の上を、上から下へ青い光が走る（照合中・読み込み中の演出）</summary>
        public static void Sweep(RectTransform parent, float seconds = 0.6f)
        {
            if (parent == null) return;
            var bar = UIFactory.Solid(parent, new Color(0.35f, 0.55f, 1f, Calm ? 0.12f : 0.22f), "Sweep");
            bar.raycastTarget = false;
            var s = bar.gameObject.AddComponent<ToolSweep>();
            s.Duration = seconds;
        }

        public static bool BlinkPhase(float hz) => hz <= 0 || Mathf.Repeat(Time.unscaledTime * hz, 1f) < 0.5f;
        public static bool CalmMode => Calm;
    }

    /// <summary>状態ランプ（LED）。点灯・消灯・点滅</summary>
    public class ToolLed : MonoBehaviour
    {
        Image _lamp, _shine;
        Color _color = ToolFx.LedOff;
        float _hz;
        float _pulseUntil = -1;

        public static ToolLed Create(Transform p, float x, float y, float size = 12)
        {
            var img = UIFactory.Image(p, ToolFx.LedSprite, ToolFx.LedOff, "Led");
            img.raycastTarget = false;
            img.rectTransform.Place(x, y, size, size);
            return Setup(img);
        }

        public static ToolLed CreateRight(Transform p, float right, float y, float size = 12)
        {
            var img = UIFactory.Image(p, ToolFx.LedSprite, ToolFx.LedOff, "Led");
            img.raycastTarget = false;
            img.rectTransform.AnchorTopRight(right, y, size, size);
            return Setup(img);
        }

        static ToolLed Setup(Image img)
        {
            var led = img.gameObject.AddComponent<ToolLed>();
            led._lamp = img;
            // 光の反射（左上の白い点）
            led._shine = UIFactory.Solid(img.transform, new Color(1, 1, 1, 0.75f), "Shine");
            led._shine.raycastTarget = false;
            led._shine.rectTransform.anchorMin = led._shine.rectTransform.anchorMax = new Vector2(0.32f, 0.68f);
            led._shine.rectTransform.sizeDelta = new Vector2(2, 2);
            return led;
        }

        /// <summary>色と点滅（Hz。0 = 点きっぱなし）</summary>
        public void Set(Color c, float blinkHz = 0)
        {
            _color = c;
            _hz = ToolFx.CalmMode ? Mathf.Min(blinkHz, 0.5f) : blinkHz;
        }

        public void Off() => Set(ToolFx.LedOff);

        /// <summary>一瞬だけ強く光る（処理した・届いた）</summary>
        public void Pulse(float seconds = 0.25f) => _pulseUntil = Time.unscaledTime + seconds;

        void Update()
        {
            bool lit = ToolFx.BlinkPhase(_hz);
            Color c = lit ? _color : Color.Lerp(_color, ToolFx.LedOff, 0.75f);
            if (Time.unscaledTime < _pulseUntil) c = Color.Lerp(c, Color.white, 0.55f);
            _lamp.color = c;
            _shine.enabled = _color != ToolFx.LedOff && lit;
        }
    }

    /// <summary>アイコンが小さな円を描いて回る（98 の「検索中」のような待ち表示）</summary>
    public class ToolSpinner : MonoBehaviour
    {
        public bool Running;
        RectTransform _icon;
        float _radius, _angle;

        public static ToolSpinner Create(RectTransform host, string icon, float iconSize = 32, float radius = 6)
        {
            var sp = host.gameObject.AddComponent<ToolSpinner>();
            var img = UIFactory.Image(host, iconSize > 16 ? RetroTheme.BigIcon(icon) : RetroTheme.Icon(icon), Color.white, "SpinIcon");
            img.raycastTarget = false;
            img.rectTransform.anchorMin = img.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            img.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            img.rectTransform.sizeDelta = new Vector2(iconSize, iconSize);
            sp._icon = img.rectTransform;
            sp._radius = radius;
            return sp;
        }

        void Update()
        {
            if (!Running) { _icon.anchoredPosition = Vector2.zero; return; }
            _angle += Time.unscaledDeltaTime * (ToolFx.CalmMode ? 2.5f : 5f);
            _icon.anchoredPosition = new Vector2(Mathf.Cos(_angle), Mathf.Sin(_angle)) * _radius;
        }
    }

    /// <summary>黒地に緑の棒が揺れる稼働メーター（98 のシステムモニター風）。Level のまわりで揺れる</summary>
    public class ToolMeter : MonoBehaviour
    {
        public float Level = 0.25f, Jitter = 0.15f;
        readonly List<RectTransform> _bars = new List<RectTransform>();
        float[] _val, _target;
        float _timer;

        public static ToolMeter Create(Transform p, int bars = 10)
        {
            var frame = UIFactory.Image(p, RetroTheme.Field, Color.white, "Meter");
            var black = UIFactory.Solid(frame.transform, Color.black, "Screen");
            black.raycastTarget = false;
            black.rectTransform.Stretch(2, 2, 2, 2);
            for (int g = 1; g < 4; g++)
            {
                var line = UIFactory.Solid(black.transform, new Color(0, 0.35f, 0, 1), "Grid");
                line.raycastTarget = false;
                line.rectTransform.anchorMin = new Vector2(0, g / 4f); line.rectTransform.anchorMax = new Vector2(1, g / 4f);
                line.rectTransform.offsetMin = Vector2.zero; line.rectTransform.offsetMax = new Vector2(0, 1);
            }
            var m = frame.gameObject.AddComponent<ToolMeter>();
            m._val = new float[bars];
            m._target = new float[bars];
            for (int i = 0; i < bars; i++)
            {
                var b = UIFactory.Solid(black.transform, new Color(0.1f, 0.95f, 0.1f), "Bar");
                b.raycastTarget = false;
                var rt = b.rectTransform;
                rt.anchorMin = new Vector2((i + 0.15f) / bars, 0);
                rt.anchorMax = new Vector2((i + 0.85f) / bars, 0.1f);
                rt.offsetMin = new Vector2(0, 1); rt.offsetMax = Vector2.zero;
                m._bars.Add(rt);
            }
            return m;
        }

        public RectTransform Rect => (RectTransform)transform;

        void Update()
        {
            _timer -= Time.unscaledDeltaTime;
            if (_timer <= 0)
            {
                _timer = ToolFx.CalmMode ? 0.4f : 0.15f;
                // 右へ流れる（新しい値は右端に入る）
                for (int i = 0; i < _target.Length - 1; i++) _target[i] = _target[i + 1];
                _target[_target.Length - 1] = Mathf.Clamp01(Level + Random.Range(-Jitter, Jitter));
            }
            for (int i = 0; i < _bars.Count; i++)
            {
                _val[i] = Mathf.Lerp(_val[i], _target[i], Time.unscaledDeltaTime * 10f);
                var rt = _bars[i];
                rt.anchorMax = new Vector2(rt.anchorMax.x, Mathf.Max(0.04f, _val[i]));
            }
        }
    }

    /// <summary>黄色く光ってから元の色に戻る</summary>
    public class ToolFlash : MonoBehaviour
    {
        Graphic _g;
        Color _base;
        float _t, _dur;
        bool _running;

        public void Begin(Graphic g, float seconds)
        {
            if (!_running) _base = g.color;
            _g = g; _dur = seconds; _t = 0; _running = true;
        }

        void Update()
        {
            if (!_running || _g == null) return;
            _t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(_t / _dur);
            var hi = _g is Text ? new Color(0.85f, 0.45f, 0f) : new Color(1f, 0.95f, 0.45f);
            _g.color = Color.Lerp(hi, _base, k);
            if (k >= 1) { _g.color = _base; _running = false; }
        }
    }

    /// <summary>上から下へ走る光（終わったら消える）</summary>
    public class ToolSweep : MonoBehaviour
    {
        public float Duration = 0.6f;
        float _t;
        RectTransform _rt;

        void Awake() => _rt = (RectTransform)transform;

        void Update()
        {
            _t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(_t / Duration);
            _rt.anchorMin = new Vector2(0, 1 - k - 0.12f);
            _rt.anchorMax = new Vector2(1, 1 - k + 0.02f);
            _rt.offsetMin = _rt.offsetMax = Vector2.zero;
            if (k >= 1) Destroy(gameObject);
        }
    }

    /// <summary>点滅する（「NEW」など）</summary>
    public class ToolBlink : MonoBehaviour
    {
        public float Hz = 1.2f;
        Graphic[] _gs;

        void Start() => _gs = GetComponentsInChildren<Graphic>();

        void Update()
        {
            if (_gs == null) return;
            bool on = ToolFx.CalmMode || ToolFx.BlinkPhase(Hz);
            foreach (var g in _gs) if (g) g.enabled = on;
        }
    }

    /// <summary>ゆっくり大きく・小さくなる（常駐している・守っている）</summary>
    public class ToolPulse : MonoBehaviour
    {
        public float Amount = 0.06f, Speed = 2.2f;
        public bool Running = true;

        void Update()
        {
            float k = Running && !ToolFx.CalmMode ? 1 + Mathf.Sin(Time.unscaledTime * Speed) * Amount : 1;
            transform.localScale = new Vector3(k, k, 1);
        }
    }
}
