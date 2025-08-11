using FontStashSharp;
using Hacknet;
using Hacknet.Extensions;
using Hacknet.Localization;
using HacknetChineseSupport;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using HacknetChineseSupport.Util;

namespace hacknet_the_cage_inside.Patcher
{
    [HarmonyPatch]
    public static class GameFontReplace
    {
        private static bool defaultInstalled = false;
        private static bool localeInstalled = false;
        private static readonly FontSystem fontSystem = new FontSystem();
        private static readonly Dictionary<SpriteFont, DynamicSpriteFont> fontMap = new Dictionary<SpriteFont, DynamicSpriteFont>();
        private static FontConfig fontConfig;

        public static void Init()
        {
            fontConfig = FontConfig.Load();
            if (fontConfig.FontFilePath == null)
            {
                HacknetChineseSupportPlugin.Logger.LogError("Font file not found.");
                return;
            }
            fontSystem.AddFont(File.ReadAllBytes(fontConfig.FontFilePath));
            StartFixFont();
            HacknetChineseSupportPlugin.Logger.LogInfo($"Font loaded from: {fontConfig.FontFilePath}");
        }


        [HarmonyPostfix]
        [HarmonyPatch(typeof(Game1), nameof(Game1.LoadContent))]
        private static void PostFixGameLoadContent()
        {
            StartFixFont();
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(LocaleFontLoader), nameof(LocaleFontLoader.LoadFontConfigForLocale))]
        private static void PostFixLoadFontConfigForLocale()
        {
            StartFixFont();
        }

        private static void StartFixFont()
        {
            if (GuiData.font != null && !defaultInstalled)
            {
                // 连接到xxx字样
                fontMap[GuiData.font] = fontSystem.GetFont(fontConfig.LargeFontSize);
                // 您是本系统管理员字样
                fontMap[GuiData.smallfont] = fontSystem.GetFont(fontConfig.SmallFontSize);
                // UI等字样
                fontMap[GuiData.tinyfont] = fontSystem.GetFont(fontConfig.UIFontSize);
                // appbar,ram模块等字样
                fontMap[GuiData.detailfont] = fontSystem.GetFont(fontConfig.DetailFontSize);
                defaultInstalled = true;
            }


            if (GuiData.LocaleFontConfigs.ContainsKey("zh-cn") && !localeInstalled)
            {
                var options = GuiData.LocaleFontConfigs["zh-cn"];
                foreach (var config in options)
                {
                    if (config.name == "default")
                    {
                        fontMap[config.detailFont] = fontSystem.GetFont(fontConfig.DetailFontSize);
                        fontMap[config.smallFont] = fontSystem.GetFont(fontConfig.SmallFontSize);
                        fontMap[config.tinyFont] = fontSystem.GetFont(fontConfig.UIFontSize);
                        fontMap[config.bigFont] = fontSystem.GetFont(fontConfig.LargeFontSize);
                        continue;
                    }

                    if (config.name == "medium")
                    {
                        fontMap[config.detailFont] = fontSystem.GetFont(fontConfig.DetailFontSize);
                        fontMap[config.smallFont] = fontSystem.GetFont(fontConfig.SmallFontSize + fontConfig.ChangeFontSizeInterval);
                        fontMap[config.tinyFont] = fontSystem.GetFont(fontConfig.UIFontSize + fontConfig.ChangeFontSizeInterval);
                        fontMap[config.bigFont] = fontSystem.GetFont(fontConfig.LargeFontSize);
                        continue;
                    }

                    if (config.name == "large")
                    {
                        fontMap[config.detailFont] = fontSystem.GetFont(fontConfig.DetailFontSize);
                        fontMap[config.smallFont] = fontSystem.GetFont(fontConfig.SmallFontSize + fontConfig.ChangeFontSizeInterval * 2);
                        fontMap[config.tinyFont] = fontSystem.GetFont(fontConfig.UIFontSize + fontConfig.ChangeFontSizeInterval * 2);
                        fontMap[config.bigFont] = fontSystem.GetFont(fontConfig.LargeFontSize);
                        continue;
                    }
                }
                localeInstalled = true;
            }
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(SpriteFont), nameof(SpriteFont.MeasureString), typeof(string))]
        private static bool PrefixMeasureString(SpriteFont __instance, string text, ref Vector2 __result)
        {
            if (!fontMap.TryGetValue(__instance, out var dynamicSpriteFont))
            {
                return true;
            }

            __result = dynamicSpriteFont.MeasureString(text);

            return false;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(SpriteFont), nameof(SpriteFont.MeasureString), typeof(StringBuilder))]
        private static bool PrefixMeasureStringBuilder(SpriteFont __instance, StringBuilder text, ref Vector2 __result)
        {
            if (!fontMap.TryGetValue(__instance, out var dynamicSpriteFont))
            {
                return true;
            }

            __result = dynamicSpriteFont.MeasureString(text);

            return false;
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
            if (!fontMap.TryGetValue(spriteFont, out var dynamicSpriteFont))
            {
                return true;
            }
            __instance.DrawString(dynamicSpriteFont, text, position, color, rotation, origin, scale, layerDepth);

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
        /// 使 “您是系统管理员字样居住，获取其Y值”
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

        return fontConfig;
    }
}
