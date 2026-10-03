using System;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.UI;

namespace NightShift.UI
{
    /// <summary>
    /// メール本文・マニュアルの描画。リッチテキストと {FIG:ID} で埋め込んだ図を縦に並べる。
    /// </summary>
    public static class DocumentRenderer
    {
        static readonly Regex FigureToken = new Regex(@"\{FIG:([A-Z0-9_]+)\}");

        /// <summary>parent に縦並びのスクロール領域を作って描画する</summary>
        public static ScrollRect Render(Transform parent, string text, Func<string, RectTransform, float> buildFigure)
        {
            var scroll = UIFactory.Scroll(parent, out var content, true);
            var vl = UIFactory.VerticalList(content, 8, 12);
            vl.padding = new RectOffset(14, 14, 10, 16);
            vl.childAlignment = TextAnchor.UpperLeft;

            text = ExpandKeys(text ?? "");
            int pos = 0;
            foreach (Match m in FigureToken.Matches(text))
            {
                AddText(content, text.Substring(pos, m.Index - pos));
                AddFigure(content, m.Groups[1].Value, buildFigure);
                pos = m.Index + m.Length;
            }
            AddText(content, text.Substring(pos));
            return scroll;
        }

        static readonly Regex KeyToken = new Regex(@"\{KEY:([A-Z_]+)\}");

        /// <summary>{KEY:ACTION} を現在のキー設定の表記に置き換える（キーを変更してもマニュアルが合う）</summary>
        public static string ExpandKeys(string text) =>
            KeyToken.Replace(text ?? "", m => "<b>" + Core.GameSettings.KeyLabel(m.Groups[1].Value) + "</b>");

        static void AddText(RectTransform content, string text)
        {
            text = text.Trim('\n', '\r');
            if (string.IsNullOrWhiteSpace(text)) return;
            var t = UIFactory.Label(content, text, RetroTheme.ReadingFontSize, TextAnchor.UpperLeft, new Color(0.1f, 0.1f, 0.12f));
            t.font = RetroTheme.ReadingFont;
            t.lineSpacing = 1.15f;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
        }

        static void AddFigure(RectTransform content, string id, Func<string, RectTransform, float> buildFigure)
        {
            var holder = UIFactory.Rect("Figure:" + id, content);
            var fit = holder.gameObject.AddComponent<LayoutElement>();
            float h = buildFigure != null ? buildFigure(id, holder) : 0;
            if (h <= 0)
            {
                UnityEngine.Object.Destroy(holder.gameObject);
                return;
            }
            fit.minHeight = fit.preferredHeight = h;
            fit.minWidth = fit.preferredWidth = ManualFigures.Width;
        }
    }
}
