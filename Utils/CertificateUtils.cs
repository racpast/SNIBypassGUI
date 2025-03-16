using System;
using System.IO;
using System.Security.Cryptography.X509Certificates;
using static SNIBypassGUI.Utils.LogManager;

namespace SNIBypassGUI.Utils
{
    public static class CertificateUtils
    {
        /// <summary>
        /// 检查指定的证书是否已安装
        /// </summary>
        /// <param name="thumbprint">证书指纹</param>
        public static bool IsCertificateInstalled(string thumbprint)
        {
            X509Store store = new(StoreName.Root, StoreLocation.LocalMachine);
            try
            {
                store.Open(OpenFlags.MaxAllowed);
                X509Certificate2Collection collection = store.Certificates;
                X509Certificate2Collection fcollection = collection.Find(X509FindType.FindByThumbprint, thumbprint, false);
                return fcollection?.Count > 0;
            }
            catch (Exception ex)
            {
                WriteLog($"检查证书 {thumbprint} 遇到异常。", LogLevel.Error, ex);
                return false;
            }
            finally
            {
                store.Close();
            }
        }

        /// <summary>
        /// 安装指定的证书
        /// <param name="certificatePath">证书路径</param>
        /// </summary>
        public static void InstallCertificate(string certificatePath)
        {
            X509Store store = new(StoreName.Root, StoreLocation.LocalMachine);
            try
            {
                store.Open(OpenFlags.MaxAllowed);
                if (File.Exists(certificatePath)) store.Add(new X509Certificate2(certificatePath));
            }
            catch (Exception ex)
            {
                WriteLog($"安装证书 {certificatePath} 遇到异常。", LogLevel.Error, ex);
                throw;
            }
            finally
            {
                store.Close();
            }
        }

        /// <summary>
        /// 卸载指定的证书
        /// </summary>
        /// <param name="thumbprint">证书指纹</param>
        public static void UninstallCertificate(string thumbprint)
        {
            X509Store store = new(StoreName.Root, StoreLocation.LocalMachine);
            try
            {
                store.Open(OpenFlags.MaxAllowed);
                X509Certificate2Collection collection = store.Certificates;
                X509Certificate2Collection fcollection = collection.Find(X509FindType.FindByThumbprint, thumbprint, false);
                if (fcollection?.Count > 0) store.RemoveRange(fcollection);
            }
            catch (Exception ex)
            {
                WriteLog($"卸载证书 {thumbprint} 遇到异常。", LogLevel.Error, ex);
                throw;
            }
            finally
            {
                store.Close();
            }
        }
    }
}
