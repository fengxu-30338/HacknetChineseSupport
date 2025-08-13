using FontStashSharp;
using Hacknet;
using Hacknet.Extensions;
using Hacknet.Localization;
using HacknetChineseSupport;
using HacknetChineseSupport.Parser;
using HacknetChineseSupport.Util;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using Sprache;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

namespace hacknet_the_cage_inside.Patcher
{
    [HarmonyPatch]
    public static class GameFontReplace
    {
        private static bool defaultInstalled = false;
        private static readonly FontSystem fontSystem = new FontSystem();
        private static readonly Dictionary<SpriteFont, DynamicSpriteFont> defaultFontMap = new Dictionary<SpriteFont, DynamicSpriteFont>();
        private static readonly Dictionary<SpriteFont, DynamicSpriteFont> localeFontMap = new Dictionary<SpriteFont, DynamicSpriteFont>();
        private static FontConfig fontConfig;
        private static readonly SpecialTextParser specialTextParser = new SpecialTextParser();
        private static readonly SpecialTextCache specialTextCache = new SpecialTextCache(300);
        private const int AddToCacheMinLength = 5;

        public static void Init()
        {
            Interlocked.MemoryBarrier();
            fontConfig = FontConfig.Load();
            if (fontConfig.FontFilePath == null)
            {
                HacknetChineseSupportPlugin.Logger.LogError("Font file not found.");
                return;
            }
            fontSystem.AddFont(File.ReadAllBytes(fontConfig.FontFilePath));
            FixDefaultFont();
            HacknetChineseSupportPlugin.Logger.LogInfo($"Font loaded from: {fontConfig.FontFilePath}");
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(Game1), nameof(Game1.LoadContent))]
        private static void PostFixGameLoadContent()
        {
            FixDefaultFont();
            FixLocaleFont(GuiData.ActiveFontConfig);
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(GuiData), nameof(GuiData.ActivateFontConfig), typeof(GuiData.FontCongifOption))]
        private static void FixLocaleFont(GuiData.FontCongifOption config)
        {
            var baseChangeFontSizeInterval = config.name == "default" ? 0 : (config.name == "medium" ? fontConfig.ChangeFontSizeInterval : fontConfig.ChangeFontSizeInterval * 2);
            ClearLocaleFontSystemDic();
            config.tinyFontCharHeight = fontConfig.UIFontSize;
            if (config.detailFont != null)
            {
                localeFontMap[config.detailFont] = fontSystem.GetFont(fontConfig.DetailFontSize);
            }

            if (config.smallFont != null)
            {
                localeFontMap[config.smallFont] = fontSystem.GetFont(fontConfig.SmallFontSize + baseChangeFontSizeInterval);
            }

            if (config.tinyFont != null)
            {
                localeFontMap[config.tinyFont] = fontSystem.GetFont(fontConfig.UIFontSize + baseChangeFontSizeInterval);
            }

            if (config.bigFont != null)
            {
                localeFontMap[config.bigFont] = fontSystem.GetFont(fontConfig.LargeFontSize);
            }
            
            GuiData.ActiveFontConfig.tinyFontCharHeight = fontConfig.UIFontSize + baseChangeFontSizeInterval;
            UpdateLocaleFontSystemDic();
        }

        private static void ClearLocaleFontSystemDic()
        {
            var keys = localeFontMap.Keys.ToList();
            keys.ForEach(font =>
            {
                defaultFontMap.Remove(font);
                localeFontMap.Remove(font);
            });
        }

        private static void UpdateLocaleFontSystemDic()
        {
            var keys = localeFontMap.Keys.ToList();
            keys.ForEach(font =>
            {
                defaultFontMap[font] = localeFontMap[font];
            });
        }


        private static void FixDefaultFont()
        {
            if (GuiData.font != null && !defaultInstalled)
            {
                // 连接到xxx字样
                defaultFontMap[GuiData.font] = fontSystem.GetFont(fontConfig.LargeFontSize);
                // 您是本系统管理员字样
                defaultFontMap[GuiData.smallfont] = fontSystem.GetFont(fontConfig.SmallFontSize);
                // UI等字样
                defaultFontMap[GuiData.tinyfont] = fontSystem.GetFont(fontConfig.UIFontSize);
                // appbar,ram模块等字样
                defaultFontMap[GuiData.detailfont] = fontSystem.GetFont(fontConfig.DetailFontSize);
                defaultInstalled = true;
            }
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(SpriteFont), nameof(SpriteFont.MeasureString), typeof(string))]
        private static bool PrefixMeasureString(SpriteFont __instance, string text, ref Vector2 __result)
        {
            if (!defaultFontMap.TryGetValue(__instance, out var dynamicSpriteFont))
            {
                return true;
            }

            if (!CheckNeedHandleSpecialText(text))
            {
                __result = dynamicSpriteFont.MeasureString(text);
            }
            else
            {
                HandleSpecialText(text, out var parserResult);
                __result = dynamicSpriteFont.MeasureString(parserResult.Text);
            }

            return false;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(SpriteFont), nameof(SpriteFont.MeasureString), typeof(StringBuilder))]
        private static bool PrefixMeasureStringBuilder(SpriteFont __instance, StringBuilder text, ref Vector2 __result)
        {
            if (!defaultFontMap.TryGetValue(__instance, out var dynamicSpriteFont))
            {
                return true;
            }

            var content = text.ToString();

            if (!CheckNeedHandleSpecialText(content))
            {
                __result = dynamicSpriteFont.MeasureString(text);
            }
            else
            {
                HandleSpecialText(content, out var parserResult);
                __result = dynamicSpriteFont.MeasureString(parserResult.Text);
            }

            return false;
        }

        private static bool CheckNeedHandleSpecialText(string text)
        {
            return fontConfig.OpenMultiColorFontParse && !string.IsNullOrWhiteSpace(text) &&
                   text.StartsWith(SpecialTextParser.FirstChar);
        }

        private static void HandleSpecialText(string text, out SpecialFontParserResult parserResult, bool addToCache = false)
        {
            if (specialTextCache.TryGetSpecialTextResult(text, out parserResult))
            {
                return;
            }

            if (parserResult != null) return;
            parserResult = specialTextParser.ParseText(text);
            if (text.Length >= AddToCacheMinLength && addToCache)
            {
                specialTextCache.AddToCache(text, parserResult);
            }
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(SpriteBatch), nameof(SpriteBatch.DrawString), 
            typeof(SpriteFont), typeof(string), typeof(Vector2), typeof(Color), typeof(float), 
            typeof(Vector2), typeof(Vector2), typeof(SpriteEffects), typeof(float))]
        private static bool PreFixDawString(SpriteBatch __instance,
            SpriteFont spriteFont,
            string text,
            Vector2 position,
            Color color,
            float rotation,
            Vector2 origin,
            Vector2 scale,
            SpriteEffects effects,
            float layerDepth)
        {
            if (!defaultFontMap.TryGetValue(spriteFont, out var dynamicSpriteFont))
            {
                return true;
            }

            if (!CheckNeedHandleSpecialText(text))
            {
                __instance.DrawString(dynamicSpriteFont, text, position, color, rotation, origin, scale, layerDepth);
                return false;
            }

            specialTextParser.DefaultCharProp.color = color;
            HandleSpecialText(text, out var parserResult, true);
            if (!parserResult.IsSuccess || !parserResult.IsSpecial)
            {
                __instance.DrawString(dynamicSpriteFont, parserResult.Text, position, color, rotation, origin, scale, layerDepth);
                return false;
            }

            __instance.DrawString(dynamicSpriteFont, parserResult.Text, position, parserResult.Colors, rotation, origin, scale, layerDepth);

            return false;
        }


        [HarmonyPrefix]
        [HarmonyPatch(typeof(LocalizedFileLoader), nameof(LocalizedFileLoader.SafeFilterString))]
        private static bool PrefixSafeFilterString(string data, ref string __result)
        {
            __result = data;
            return false;
        }

        /// <summary>
        /// 使 “您是系统管理员字样居中，获取其Y值”
        /// </summary>
        /// <returns></returns>
        private static float GetDoConnectHeaderY(ref Rectangle rect, ref Vector2 measure)
        {
            return rect.Y + rect.Height / 2 - measure.Y / 2;
        }

        [HarmonyILManipulator]
        [HarmonyPatch(typeof(DisplayModule), nameof(DisplayModule.doConnectHeader))]
        private static void FixDoConnectHeader(ILContext il)
        {
            ILCursor c = new ILCursor(il);
            c.GotoNext(MoveType.Before,
                x => x.MatchLdfld(typeof(Rectangle), nameof(Rectangle.Y)),
                x => x.MatchConvR4(),
                x => x.MatchNewobj(AccessTools.Constructor(typeof(Vector2), new []{ typeof(float), typeof(float) }))
            );

            c.RemoveRange(2);
            // load measure
            c.Emit(OpCodes.Ldloca, 4);
            c.Emit(OpCodes.Call, AccessTools.Method(typeof(GameFontReplace), nameof(GetDoConnectHeaderY)));

            c.GotoNext(MoveType.Before,
                x => x.MatchLdcR4(2f),
                x => x.MatchSub(),
                x => x.MatchStfld(typeof(Vector2), nameof(Vector2.Y))
            );
            c.RemoveRange(1);
            c.Emit(OpCodes.Ldc_R4, 0.5f);
        }

        /// <summary>
        /// 计算右上角文字居中位置的Y值
        /// </summary>
        private static float GetLocationTextY(ref Vector2 measure)
        {
            var rect = OS.currentInstance.topBar;
            return rect.Y + rect.Height / 2 - measure.Y / 2 + 0.5f;
        }

        [HarmonyILManipulator]
        [HarmonyPatch(typeof(OS), nameof(OS.drawModules))]
        private static void FixDrawModules(ILContext il)
        {
            ILCursor c = new ILCursor(il);
            c.GotoNext(MoveType.Before,
                x => x.MatchLdfld(typeof(Vector2), nameof(Vector2.Y)),
                x => x.MatchLdcR4(3f),
                x => x.MatchSub()
            );

            c.RemoveRange(3);
            c.Emit(OpCodes.Nop);
            // load measure
            c.Emit(OpCodes.Ldloca, 2);
            c.Emit(OpCodes.Call, AccessTools.Method(typeof(GameFontReplace), nameof(GetLocationTextY)));
        }
    }
}


public class FontConfig
{
    public string FontFilePath { get; private set; }

    /// <summary>
    /// 大字体；如Display面板中的连接到xxx字样
    /// </summary>
    public int LargeFontSize { get; private set; } = 34;

    /// <summary>
    /// 小字体；如您是本系统管理员字样
    /// </summary>
    public int SmallFontSize { get; private set; } = 20;

    /// <summary>
    /// UI字体大小
    /// </summary>
    public int UIFontSize { get; private set; } = 18;

    /// <summary>
    /// 左上角Ram模块，AppBar等字样字体大小
    /// </summary>
    public int DetailFontSize { get; private set; } = 14;

    /// <summary>
    /// 修改字体大小时的间隔
    /// </summary>
    public int ChangeFontSizeInterval { get; private set; } = 2;

    /// <summary>
    /// 是否开启多色字体解析
    /// </summary>
    public bool OpenMultiColorFontParse { get; private set; } = false;


    private static string GetSearchFolder()
    {
        if (ExtensionLoader.ActiveExtensionInfo != null)
        {
            return Path.Combine(ExtensionLoader.ActiveExtensionInfo.FolderPath, "Plugins/Font");
        }

        return Path.Combine(Path.GetDirectoryName(AssemblyPathHelper.GetCleanAssemblyPath(typeof(FontConfig).Assembly)), "Font");
    }

    private static string FindFirstFileWithExtension(string dir, string ext)
    {
        foreach (var file in Directory.GetFiles(dir))
        {
            if (file.EndsWith(ext))
            {
                return file;
            }
        }

        foreach (var directory in Directory.GetDirectories(dir))
        {
            var res = FindFirstFileWithExtension(directory, ext);
            if (!string.IsNullOrEmpty(res))
            {
                return res;
            }
        }

        return null;
    }

    public static FontConfig Load()
    {
        var fontConfig = new FontConfig();
        var searchDir = GetSearchFolder();
        if (!Directory.Exists(searchDir))
        {
            HacknetChineseSupportPlugin.Logger.LogError($"Not Find Font Dir[{searchDir}]");
            return fontConfig;
        }

        fontConfig.FontFilePath = FindFirstFileWithExtension(searchDir, ".ttf");
        if (string.IsNullOrEmpty(fontConfig.FontFilePath))
        {
            HacknetChineseSupportPlugin.Logger.LogError($"Not Find font file in dir[{searchDir}]");
            return fontConfig;
        }

        var configFile = FindFirstFileWithExtension(searchDir, ".ini");
        if (string.IsNullOrEmpty(configFile))
        {
            HacknetChineseSupportPlugin.Logger.LogInfo($"Not Find Config file in dir[{searchDir}], use default config");
            return fontConfig;
        }
        var iniConfig = new IniConfig();
        iniConfig.Load(configFile);
        fontConfig.LargeFontSize = iniConfig.GetInt("default", nameof(LargeFontSize), fontConfig.LargeFontSize);
        fontConfig.SmallFontSize = iniConfig.GetInt("default", nameof(SmallFontSize), fontConfig.SmallFontSize);
        fontConfig.UIFontSize = iniConfig.GetInt("default", nameof(UIFontSize), fontConfig.UIFontSize);
        fontConfig.DetailFontSize = iniConfig.GetInt("default", nameof(DetailFontSize), fontConfig.DetailFontSize);
        fontConfig.ChangeFontSizeInterval = iniConfig.GetInt("default", nameof(ChangeFontSizeInterval), fontConfig.ChangeFontSizeInterval);
        fontConfig.OpenMultiColorFontParse = iniConfig.GetBool("default", nameof(OpenMultiColorFontParse), fontConfig.OpenMultiColorFontParse);

        return fontConfig;
    }
}
