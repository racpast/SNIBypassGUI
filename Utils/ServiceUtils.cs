using Microsoft.Win32;

namespace SNIBypassGUI.Utils
{
    public static class ServiceUtils
    {
        /// <summary>
        /// 获取服务的二进制路径
        /// </summary>
        /// <param name="serviceName"></param>
        /// <returns></returns>
        public static string GetServiceBinaryPath(string serviceName)
        {
            string regPath = $@"SYSTEM\CurrentControlSet\Services\{serviceName}";
            using RegistryKey key = Registry.LocalMachine.OpenSubKey(regPath);
            if (key != null)
            {
                object binaryPathValue = key.GetValue("ImagePath");
                if (binaryPathValue != null) return binaryPathValue.ToString();
            }
            return null;
        }
    }
}
