using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace HacknetChineseSupport.Util
{
    public class AssemblyPathHelper
    {
        /// <summary>
        /// 从程序集的CodeBase获取干净的绝对路径
        /// </summary>
        public static string GetCleanAssemblyPath(Assembly assembly)
        {
            // 优先尝试使用Location属性
            if (!string.IsNullOrEmpty(assembly.Location))
            {
                return Path.GetFullPath(assembly.Location);
            }

            // 处理CodeBase的URI格式
            if (!string.IsNullOrEmpty(assembly.CodeBase))
            {
                return CleanCodeBasePath(assembly.CodeBase);
            }

            throw new FileNotFoundException("无法确定程序集路径");
        }

        private static string CleanCodeBasePath(string codeBase)
        {
            // 处理URI格式路径
            if (Uri.TryCreate(codeBase, UriKind.Absolute, out var uri) && uri.IsFile)
            {
                return Uri.UnescapeDataString(uri.LocalPath);
            }

            // 自定义处理常见前缀格式
            if (TryMatchPrefix(codeBase, out var cleanPath))
            {
                return cleanPath;
            }

            // 回退到字符串替换方式
            return FinalSanitization(codeBase);
        }

        private static bool TryMatchPrefix(string codeBase, out string path)
        {
            const string pattern = @"^(?:file:\/\/\/|file:\/\/|file:\/|file:\|)";

            if (Regex.IsMatch(codeBase, pattern, RegexOptions.IgnoreCase))
            {
                path = Regex.Replace(codeBase, pattern, "", RegexOptions.IgnoreCase);
                return true;
            }

            path = null;
            return false;
        }

        private static string FinalSanitization(string path)
        {
            return path
                .TrimStart('"')
                .Replace('/', Path.DirectorySeparatorChar)
                .Replace('\\', Path.DirectorySeparatorChar);
        }
    }
}
