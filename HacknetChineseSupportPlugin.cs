using BepInEx;
using BepInEx.Hacknet;
using BepInEx.Logging;
using hacknet_the_cage_inside.Patcher;
using System;
using System.IO;
using System.Reflection;
using Hacknet.Extensions;

namespace HacknetChineseSupport
{
    [BepInPlugin(ModGUID, ModName, ModVer)]
    public class HacknetChineseSupportPlugin : HacknetPlugin
    {
        public const string ModGUID = "HacknetChineseSupportPlugin";
        public const string ModName = "HacknetChineseSupportPlugin";
        public const string ModVer = "1.0.1";
        public static HacknetChineseSupportPlugin Instance { get; private set; }
        public static ManualLogSource Logger => Instance.Log;
        public override bool Load()
        {
            Instance = this;
            AppDomain.CurrentDomain.AssemblyResolve += CurrentDomain_AssemblyResolve;
            GameFontReplace.Init();
            HarmonyInstance.PatchAll(Instance.GetType().Assembly);
            return true;
        }

        private static Assembly CurrentDomain_AssemblyResolve(object sender, ResolveEventArgs args)
        {
            if (ExtensionLoader.ActiveExtensionInfo == null)
            {
                return null;
            }

            var folder = Path.Combine(ExtensionLoader.ActiveExtensionInfo.FolderPath, "plugins");
            var dllFile = Path.GetFullPath(Path.Combine(folder, new AssemblyName(args.Name).Name + ".dll"));
            if (!File.Exists(dllFile))
            {
                return null;
            }

            return Assembly.LoadFile(dllFile);
        }
    }

}
