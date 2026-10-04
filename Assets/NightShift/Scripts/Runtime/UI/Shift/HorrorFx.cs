using System.Text;
using NightShift.Core;
using NightShift.Systems;
using UnityEngine;
using UnityEngine.UI;

namespace NightShift.UI.Shift
{
    /// <summary>
    /// ウイルスの被害のホラー演出（VIRUS_CATALOG.md）。すべてゲーム画面の中の演出で、実際のPCには触れない。
    /// 画面にメッセージの文字を浮かび上がらせることはしない（怖さは音・光・動き・意味のない文字化けで出す）。
    /// 設定: 控えめ／点滅を減らす／急な大きな音。演出中も Esc（一時停止）は使える。
    /// </summary>
    public class HorrorFx : MonoBehaviour
    {
        ShiftDesktop _d;
        RectTransform _layer;
        GameObject _bsod, _blackout, _ghost;
        float _bsodEnd = -1, _blackoutEnd = -1, _humTimer, _cryTimer = 20f, _ghostTimer;
        Image _crtGlow;
        Vector2 _ghostPos, _ghostTarget;

        static GameSettings Set => GameSettings.Current;

        public static HorrorFx Attach(ShiftDesktop d, RectTransform layer)
        {
            var fx = layer.gameObject.AddComponent<HorrorFx>();
            fx._d = d;
            fx._layer = layer;
            d.GlitchRequested += fx.Glitch;
            d.S.HorrorRequested += fx.OnHorror;
            return fx;
        }

        void OnDestroy()
        {
            if (_d == null) return;
            _d.GlitchRequested -= Glitch;
            if (_d.S != null) _d.S.HorrorRequested -= OnHorror;
        }

        void OnHorror(string what)
        {
            switch (what)
            {
                case "BSOD": Bsod(5f); break;
                case "BLACKOUT": Blackout(10f); break;
                case "MUSICBOX": AudioFx.MusicBox(); break;
                case "GLITCH": Glitch(); break;
            }
        }

        // ---------------- 文字化け ----------------
        static readonly string[] Garbage = { "縺ゅ＞", "繧ｦ繧ｩ", "譁�ｭ�", "縺�∴", "繝ｻ繝ｻ", "螟憺", "ｷ繧ｹ", "縺翫°" };

        /// <summary>意味のない文字化け（元の文は意味を持たない文字列から作る）</summary>
        public static string Garble(string s, int seed)
        {
            if (string.IsNullOrEmpty(s)) return s;
            var sb = new StringBuilder();
            var r = new System.Random(seed);
            for (int i = 0; i < Mathf.Max(6, s.Length / 2); i++) sb.Append(Garbage[r.Next(Garbage.Length)]);
            return sb.ToString();
        }

        /// <summary>画面の端が一瞬だけ化ける</summary>
        public void Glitch()
        {
            var host = UIFactory.Rect("Glitch", _layer).Stretch();
            var r = new System.Random(Time.frameCount);
            for (int i = 0; i < 7; i++)
            {
                bool left = r.Next(2) == 0;
                var t = UIFactory.Label(host, Garble("xxxxxxxxxx", r.Next()), 13, left ? TextAnchor.MiddleLeft : TextAnchor.MiddleRight, new Color(0.9f, 0.95f, 1f, 0.75f), true);
                t.rectTransform.anchorMin = new Vector2(left ? 0 : 0.6f, 0); t.rectTransform.anchorMax = new Vector2(left ? 0.4f : 1, 0);
                t.rectTransform.pivot = new Vector2(0.5f, 0);
                t.rectTransform.sizeDelta = new Vector2(0, 18);
                t.rectTransform.anchoredPosition = new Vector2(0, 40 + r.Next(0, 700));
                t.raycastTarget = false;
            }
            var cg = host.gameObject.AddComponent<CanvasGroup>();
            cg.blocksRaycasts = false;
            var fade = host.gameObject.AddComponent<FadeAndDie>();
            fade.Group = cg;
            fade.Life = Set.reduceFlashing ? 1.2f : 0.35f;
            AudioFx.Tick();
        }

        // ---------------- ブルースクリーン（V4 BLUESTONE） ----------------
        public void Bsod(float gameMinutes)
        {
            if (_bsod != null) return;
            AudioFx.PowerDown();
            var bg = UIFactory.Solid(_layer, new Color(0f, 0f, 0.62f), "BSOD");
            bg.rectTransform.Stretch();
            var text = UIFactory.Label(bg.transform,
                "*** STOP: 0x0000007B (0xF8A3C52C, 0xC0000034, 0x00000000, 0x00000000)\nINACCESSIBLE_BOOT_DEVICE\n\n" +
                "If this is the first time you've seen this Stop error screen,\nrestart your computer. If this screen appears again, follow these steps:\n\n" +
                "Check for viruses on your computer. Remove any newly installed\nhard drives or hard drive controllers.\n\n" +
                "Beginning dump of physical memory\nPhysical memory dump complete.\nContact your system administrator or technical support group.",
                16, TextAnchor.UpperLeft, Color.white, true);
            text.rectTransform.Stretch(60, 60, 60, 60);
            _bsod = bg.gameObject;
            FadeIn(_bsod);
            _bsodEnd = _d.S.Time.CurrentMinute + gameMinutes;
            _humTimer = 0;
        }

        // ---------------- ブラックアウト（V5 REDACT） ----------------
        public void Blackout(float gameMinutes)
        {
            if (_blackout != null) return;
            AudioFx.PowerDown();
            var bg = UIFactory.Solid(_layer, Color.black, "Blackout");
            bg.rectTransform.Stretch();
            // 非常灯の緑の光
            var exit = UIFactory.Solid(bg.transform, new Color(0.1f, 0.9f, 0.3f, 0.75f), "ExitLight");
            exit.rectTransform.anchorMin = exit.rectTransform.anchorMax = new Vector2(0, 1);
            exit.rectTransform.pivot = new Vector2(0, 1);
            exit.rectTransform.sizeDelta = new Vector2(70, 26);
            exit.rectTransform.anchoredPosition = new Vector2(40, -30);
            // 電源の切れたはずのCRTが、かすかに光っている
            _crtGlow = UIFactory.Solid(bg.transform, new Color(0.6f, 0.75f, 0.8f, 0f), "CrtGlow");
            _crtGlow.rectTransform.anchorMin = new Vector2(0.18f, 0.18f); _crtGlow.rectTransform.anchorMax = new Vector2(0.82f, 0.82f);
            _crtGlow.rectTransform.offsetMin = _crtGlow.rectTransform.offsetMax = Vector2.zero;
            _blackout = bg.gameObject;
            FadeIn(_blackout);
            _blackoutEnd = _d.S.Time.CurrentMinute + gameMinutes;
            _humTimer = 1.5f;
        }

        /// <summary>「点滅を減らす」なら、一瞬で切り替えず 1秒かけて暗転する</summary>
        static void FadeIn(GameObject go)
        {
            if (!Set.reduceFlashing) return;
            var cg = go.AddComponent<CanvasGroup>();
            cg.alpha = 0;
            go.AddComponent<FadeInGroup>().Group = cg;
        }

        void EndBlackout()
        {
            Destroy(_blackout);
            _blackout = null;
            _blackoutEnd = -1;
            // 停電からの復旧: サーバーのサービスは止まったまま。起動し直す必要がある
            var s = _d.S;
            foreach (var st in s.Servers.Servers) foreach (var svc in st.Services) svc.Running = false;
            s.Logs.Add(s.Time.CurrentMinute, "FACILITY", "-", "-", "POWER", "RESTORED", "停電から復旧。サーバーのサービスを起動し直してください");
            s.Notifications.Notify(NotificationType.SecurityAlert, "停電から復旧", "サーバーのサービスが止まっています。サーバー運用管理で起動し直してください。", s.Time.CurrentMinute);
        }

        // ---------------- もう1つのカーソル（V7 DOPPELGANGER） ----------------
        void UpdateGhost()
        {
            if (_ghost == null)
            {
                // 本物と同じ絵のカーソル（文字ではなく、RetroCursor の矢印）
                var img = UIFactory.Image(_layer, RetroCursor.ArrowSprite(), Color.white, "GhostCursor");
                img.raycastTarget = false;
                img.rectTransform.anchorMin = img.rectTransform.anchorMax = new Vector2(0, 0);
                img.rectTransform.pivot = new Vector2(0, 1);
                img.rectTransform.sizeDelta = new Vector2(32, 32);
                _ghost = img.gameObject;
                _ghostPos = new Vector2(600, 400);
                _ghostTarget = _ghostPos;
            }
            if ((_ghostPos - _ghostTarget).sqrMagnitude < 25)
                _ghostTarget = new Vector2(Random.Range(80f, 1200f), Random.Range(80f, 720f));
            _ghostPos = Vector2.MoveTowards(_ghostPos, _ghostTarget, 60f * Time.unscaledDeltaTime);
            ((RectTransform)_ghost.transform).anchoredPosition = _ghostPos;
            _ghost.transform.SetAsLastSibling();
            // ときどき、サブモニターのツールを勝手に閉じる
            _ghostTimer += Time.unscaledDeltaTime;
            if (_ghostTimer > 45f)
            {
                _ghostTimer = 0;
                foreach (var ws in _d.Stations)
                {
                    var w = ws.Windows.Active;
                    if (w != null && w.Id != null && w.Id.StartsWith("tool:")) { w.Close(); break; }
                }
            }
        }

        void Update()
        {
            if (_d == null || _d.S == null) return;
            var s = _d.S;
            float now = s.Time.CurrentMinute;
            bool paused = s.Time.Paused;
            _d.EffectBlocking = _bsod != null || _blackout != null;

            if (_bsod != null)
            {
                _humTimer -= Time.unscaledDeltaTime;
                if (_humTimer <= 0 && !paused) { AudioFx.Hum(); _humTimer = 2.4f; }
                if (now >= _bsodEnd) { Destroy(_bsod); _bsod = null; AudioFx.WarpedStartup(); }
            }
            if (_blackout != null)
            {
                _humTimer -= Time.unscaledDeltaTime;
                if (_humTimer <= 0 && !paused) { AudioFx.CrtWhine(); _humTimer = 4f; }
                if (_crtGlow != null)
                {
                    float speed = Set.reduceFlashing ? 0.3f : 0.8f;
                    float a = Set.horrorMild ? 0f : 0.05f + 0.04f * Mathf.Sin(Time.unscaledTime * speed);
                    _crtGlow.color = new Color(0.6f, 0.75f, 0.8f, a);
                }
                if (now >= _blackoutEnd) EndBlackout();
            }

            // 自分のIDが奪われている間は、もう1つのカーソルが動く
            if (s.OperatorCompromised && !Set.horrorMild) UpdateGhost();
            else if (_ghost != null) { Destroy(_ghost); _ghost = null; }

            // 感染が広がっている間は、遠くで泣き声のようなノイズ
            _cryTimer -= Time.unscaledDeltaTime;
            if (_cryTimer <= 0)
            {
                _cryTimer = 40f + Random.Range(0f, 30f);
                foreach (var kv in s.Security.Infections)
                    if (kv.Value == "V3" && !s.Security.IsIsolated(kv.Key)) { AudioFx.Cry(); break; }
            }
        }
    }

    public class FadeInGroup : MonoBehaviour
    {
        public CanvasGroup Group;
        public float Duration = 1f;
        float _t;
        void Update()
        {
            _t += Time.unscaledDeltaTime;
            if (Group != null) Group.alpha = Mathf.Clamp01(_t / Duration);
            if (_t >= Duration) Destroy(this);
        }
    }

    public class FadeAndDie : MonoBehaviour
    {
        public CanvasGroup Group;
        public float Life = 0.4f;
        float _t;
        void Update()
        {
            _t += Time.unscaledDeltaTime;
            if (Group != null) Group.alpha = 1f - Mathf.Clamp01(_t / Life);
            if (_t >= Life) Destroy(gameObject);
        }
    }
}
