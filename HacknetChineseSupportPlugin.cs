using BepInEx;
using BepInEx.Hacknet;
using BepInEx.Logging;
using hacknet_the_cage_inside.Patcher;
using Pathfinder.Event.BepInEx;
using Pathfinder.Meta.Load;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HacknetChineseSupport
{
    [BepInPlugin(ModGUID, ModName, ModVer)]
    public class HacknetChineseSupportPlugin : HacknetPlugin
    {
        public const string ModGUID = "HacknetChineseSupportPlugin";
        public const string ModName = "HacknetChineseSupportPlugin";
        public const string ModVer = "1.0.0";
        public static HacknetChineseSupportPlugin Instance { get; private set; }
        public static ManualLogSource Logger => Instance.Log;
        public override bool Load()
        {
            Instance = this;
            GameFontReplace.Init();
            HarmonyInstance.PatchAll(Instance.GetType().Assembly);
            return true;
        }
    }

}
