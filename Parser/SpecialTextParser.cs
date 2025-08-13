using HarmonyLib;
using Microsoft.Xna.Framework;
using Sprache;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Pathfinder.Util;

namespace HacknetChineseSupport.Parser
{
    public class SpecialTextParser
    {
        private static readonly Parser<string> LBParser = Parse.Char('{')
            .Then(_ => Parse.WhiteSpace.Many()).Select(_ => string.Empty);

        private static readonly Parser<string> RBParser = Parse.WhiteSpace.Many()
            .Then(_ => Parse.Char('}')).Select(_ => string.Empty);

        private static readonly Parser<KeyValuePair<string, string>> PropContentParse =
            from key in Parse.LetterOrDigit.AtLeastOnce().Token().Text()
            from _ in Parse.Char(':').Token()
            from value in Parse.Char(ch => char.IsLetterOrDigit(ch) || ch == ' ', "prop value").AtLeastOnce().Token().Text()
            select new KeyValuePair<string, string>(key, value);

        private static readonly Parser<IEnumerable<KeyValuePair<string, string>>> MultiPropContentParse =
            from prop in PropContentParse.Optional()
            from item in (
                from _ in Parse.Char(',').Token()
                from prop2 in PropContentParse
                select prop2
            ).Many()
            select item.Concat(prop.IsDefined ? new List<KeyValuePair<string, string>> { prop.Get() } : new List<KeyValuePair<string, string>>());

        private static Parser<IEnumerable<CharInfo>> TextParser;

        private readonly Parser<IEnumerable<CharInfo>> SpecTextParser;

        private readonly Stack<CharProp> PropStack = new Stack<CharProp>();

        private bool _containsSpecial = false;

        /// <summary>
        /// 使用其来观察是否为特殊开始字符
        /// </summary>
        public static readonly string FirstChar = "";

        public CharProp DefaultCharProp { get; private set; }
        public bool IsDebug { get; set; }

        public SpecialTextParser()
        {
            DefaultCharProp = new CharProp(){color = Color.White};
            // 属性开始处解析
            var propStartParser = from lb in LBParser
                                  from item in MultiPropContentParse
                                  from rb in RBParser
                                  select GenerateCurrentProp(item);

            // 属性结束处解析
            Parser<IEnumerable<CharInfo>> propEndParse = Parse.String("{/}")
                .Select(_ =>
                {
                    var chProp = PropStack.Pop();
                    if (IsDebug)
                    {
                        Console.WriteLine($"pop: {chProp}, cout={PropStack.Count}");
                    }
                    return Array.Empty<CharInfo>();
                });

            // 普通字符解析
            var commonTextParser = Parse.CharExcept('{').Many().Select(GetCurrentCharProp);

            /* 解析流程
             * Text = (字符串 + Spec? + 字符串)*
             * Spec = {xxx}Text{/}
             */

            // 文本解析
            TextParser = commonTextParser.Then(res => SpecTextParser.Optional().Select(res2 => res2.IsDefined ? res.Concat(res2.Get()) : res)
                .Then(res2 => commonTextParser.Select(res2.Concat))
            ).Many().Select(res => res.SelectMany(item => item));

            // 特殊字符串解析
            SpecTextParser = propStartParser.Then(_ => TextParser).Then(res => propEndParse.Select(_ => res));
        }

        private IEnumerable<CharInfo> GenerateCurrentProp(IEnumerable<KeyValuePair<string, string>> props)
        {
            var chProp = (CharProp)GetNewCharProp().Clone();
            foreach (var pair in props)
            {
                if (pair.Key == nameof(CharProp.color))
                {
                    chProp.SetColor(pair.Value.Trim());
                    _containsSpecial = true;
                    continue;
                }
            }
            PropStack.Push(chProp);

            if (IsDebug)
            {
                Console.WriteLine($"push: {chProp}, cout={PropStack.Count}");
            }

            return Array.Empty<CharInfo>();
        }

        private CharProp GetNewCharProp()
        {
            if (PropStack.Count > 0)
            {
                return PropStack.Peek();
            }

            return DefaultCharProp;
        }

        private IEnumerable<CharInfo> GetCurrentCharProp(IEnumerable<char> chars)
        {
            var curChProp = GetNewCharProp();
            if (IsDebug)
            {
                Console.WriteLine($"cur chars:{new string(chars.ToArray())}, use prop: {curChProp}");
            }
            return chars.Select(ch => new CharInfo(curChProp, ch));
        }

        public SpecialFontParserResult ParseText(string text)
        {
            _containsSpecial = false;
            PropStack.Clear();
            if (IsDebug)
            {
                Console.WriteLine($"开始解析：[{text}]");
            }

            try
            {
                var chInfo = TextParser.Parse(text);
                if (IsDebug)
                {
                    Console.WriteLine("======================== 解析结果 =========================");
                    foreach (var charInfo in chInfo)
                    {
                        Console.WriteLine($"char:{charInfo.Char}, prop: {charInfo.CharPropInfo.color}");
                    }
                }
                var sb = new StringBuilder();
                var colors = new List<Color>(chInfo.Count());
                foreach (var charInfo in chInfo)
                {
                    sb.Append(charInfo.Char);
                    colors.Add(charInfo.CharPropInfo.color);
                }

                return new SpecialFontParserResult(true, sb.ToString(), colors.ToArray(), _containsSpecial);
            }
            catch (Exception)
            {
                return new SpecialFontParserResult(false, text, Array.Empty<Color>(), false);
            }
        }
    }



    public class CharProp : ICloneable
    {
        public Color color { get; set; }

        public object Clone()
        {
            return new CharProp() { color = this.color };
        }

        public void SetColor(string colorStr)
        {
            try
            {
                var property = typeof(Color).GetProperty(colorStr, BindingFlags.Static | BindingFlags.Public);
                if (property == null || property.PropertyType != typeof(Color))
                {
                    throw new ArgumentException($"Color has no property named {colorStr}");
                }

                this.color = (Color)property.GetValue(null);
            }
            catch (Exception)
            {
                this.color = (Color)XMLTypeConverter.ConvertToType(typeof(Color), colorStr);
            }
        }

        public override string ToString()
        {
            return $"CharProp@{{ color={color} }}";
        }
    }

    public class CharInfo
    {
        public CharProp CharPropInfo { get; }

        public char Char { get; }

        public CharInfo(CharProp charProp, char ch)
        {
            this.CharPropInfo = charProp;
            this.Char = ch;
        }
    }

    public class SpecialFontParserResult
    {
        public bool IsSuccess { get; set; }

        public bool IsSpecial { get; set; }

        public string Text { get; set; }

        public Color[] Colors { get; set; }

        public DateTime AddToCacheTime { get; set; }

        public SpecialFontParserResult(bool isSuccess, string text, Color[] colors, bool isSpecial)
        {
            IsSuccess = isSuccess;
            Text = text;
            Colors = colors;
            IsSpecial = isSpecial;
        }
    }
}
