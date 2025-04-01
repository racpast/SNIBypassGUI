using System;
using System.Linq;

namespace SNIBypassGUI.Utils
{
    public static class CommandLineUtils
    {
        /// <summary>
        /// 尝试获取参数的值
        /// </summary>
        public static bool TryGetArgumentValue(string[] args, string argName, out string value)
        {
            value = null;
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i].Equals(argName, StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                {
                    value = args[i + 1];
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// 判断是否含有参数
        /// </summary>
        public static bool ContainsArgument(string[] args, string argName)
        {
            return args.Contains(argName, StringComparer.OrdinalIgnoreCase);
        }
    }
}
