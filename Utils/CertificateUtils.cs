using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Org.BouncyCastle.Asn1;
using Org.BouncyCastle.Asn1.X509;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Crypto.Operators;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Math;
using Org.BouncyCastle.Pkcs;
using Org.BouncyCastle.Security;
using Org.BouncyCastle.X509;
using static SNIBypassGUI.Utils.LogManager;

namespace SNIBypassGUI.Utils
{
    public static class CertificateUtils
    {
        /// <summary>
        /// 证书用途枚举。
        /// </summary>
        public enum CertificatePurpose
        {
            None,
            ServerAuthentication,
            ClientAuthentication,
            CodeSigning,
            EmailProtection
        }

        private const string DefaultPfxPassword = "password";

        /// <summary>
        /// 生成根证书。
        /// </summary>
        public static X509Certificate2 CreateRootCertificate(Dictionary<string, string> subjectAttributes, int keySize = 2048, DateTime? notBefore = null, DateTime? notAfter = null)
        {
            var random = new SecureRandom();
            var keyPairGenerator = new RsaKeyPairGenerator();
            keyPairGenerator.Init(new KeyGenerationParameters(random, keySize));
            var keyPair = keyPairGenerator.GenerateKeyPair();

            var generator = new X509V3CertificateGenerator();
            generator.SetSerialNumber(new BigInteger(64, random));
            var subjectDn = CreateX509Name(subjectAttributes);
            generator.SetSubjectDN(subjectDn);
            generator.SetIssuerDN(subjectDn);
            var notBeforeValue = notBefore ?? DateTime.UtcNow;
            var notAfterValue = notAfter ?? notBeforeValue.AddYears(1);
            generator.SetNotBefore(notBeforeValue);
            generator.SetNotAfter(notAfterValue);
            generator.SetPublicKey(keyPair.Public);
            generator.AddExtension(X509Extensions.BasicConstraints, true, new BasicConstraints(true));
            generator.AddExtension(X509Extensions.KeyUsage, true, new KeyUsage(KeyUsage.KeyCertSign | KeyUsage.CrlSign));
            var certificate = generator.Generate(new Asn1SignatureFactory("SHA256WithRSA", keyPair.Private, random));
            return CreateX509Certificate2(certificate, keyPair.Private);
        }

        public static X509Certificate2 CreateChildCertificate(
            X509Certificate2 issuerCert,
            Dictionary<string, string> subjectAttributes,
            CertificatePurpose purpose = CertificatePurpose.None,
            List<string> subjectAltNames = null,
            int keySize = 2048,
            DateTime? notBefore = null,
            DateTime? notAfter = null)
        {
            if (!issuerCert.HasPrivateKey) throw new ArgumentException("颁发者证书必须包含私钥。");

            var random = new SecureRandom();
            var keyPairGenerator = new RsaKeyPairGenerator();
            keyPairGenerator.Init(new KeyGenerationParameters(random, keySize));
            var keyPair = keyPairGenerator.GenerateKeyPair();

            var generator = new X509V3CertificateGenerator();
            generator.SetSerialNumber(new BigInteger(64, random));
            var bcIssuerCert = DotNetUtilities.FromX509Certificate(issuerCert);
            var issuerDn = bcIssuerCert.SubjectDN;
            generator.SetIssuerDN(issuerDn);
            var subjectDn = CreateX509Name(subjectAttributes);
            generator.SetSubjectDN(subjectDn);
            var notBeforeValue = notBefore ?? DateTime.UtcNow;
            var notAfterValue = notAfter ?? notBeforeValue.AddYears(1);
            generator.SetNotBefore(notBeforeValue);
            generator.SetNotAfter(notAfterValue);
            generator.SetPublicKey(keyPair.Public);
            generator.AddExtension(X509Extensions.BasicConstraints, true, new BasicConstraints(false));

            if (purpose != CertificatePurpose.None)
            {
                KeyPurposeID ekuid = GetKeyPurposeId(purpose);
                generator.AddExtension(X509Extensions.ExtendedKeyUsage, false, new ExtendedKeyUsage(ekuid));
                if (purpose == CertificatePurpose.ServerAuthentication || purpose == CertificatePurpose.ClientAuthentication) generator.AddExtension(X509Extensions.KeyUsage, true, new KeyUsage(KeyUsage.DigitalSignature | KeyUsage.KeyEncipherment));
                else if (purpose == CertificatePurpose.CodeSigning) generator.AddExtension(X509Extensions.KeyUsage, true, new KeyUsage(KeyUsage.DigitalSignature));
            }

            if (subjectAltNames != null && subjectAltNames.Count > 0)
            {
                var names = subjectAltNames.Select(name => new GeneralName(GeneralName.DnsName, name)).ToArray();
                var san = new GeneralNames(names);
                generator.AddExtension(X509Extensions.SubjectAlternativeName, false, san);
            }

            var issuerPrivateKey = GetPrivateKeyFromX509(issuerCert);
            var certificate = generator.Generate(new Asn1SignatureFactory("SHA256WithRSA", issuerPrivateKey, random));
            return CreateX509Certificate2(certificate, keyPair.Private);
        }

        /// <summary>
        /// 从 CSR 创建证书。
        /// </summary>
        public static X509Certificate2 CreateCertificateFromCsr(byte[] csrDer, X509Certificate2 issuerCert, DateTime? notBefore = null, DateTime? notAfter = null)
        {
            var csr = new Pkcs10CertificationRequest(csrDer);
            if (!csr.Verify()) throw new ArgumentException("CSR 签名无效。");

            var generator = new X509V3CertificateGenerator();
            generator.SetSerialNumber(new BigInteger(64, new SecureRandom()));
            var bcIssuerCert = DotNetUtilities.FromX509Certificate(issuerCert);
            var issuerDn = bcIssuerCert.SubjectDN;
            generator.SetIssuerDN(issuerDn);
            generator.SetSubjectDN(csr.GetCertificationRequestInfo().Subject);
            var notBeforeValue = notBefore ?? DateTime.UtcNow;
            var notAfterValue = notAfter ?? notBeforeValue.AddYears(1);
            generator.SetNotBefore(notBeforeValue);
            generator.SetNotAfter(notAfterValue);
            generator.SetPublicKey(csr.GetPublicKey());
            generator.AddExtension(X509Extensions.BasicConstraints, true, new BasicConstraints(false));
            var issuerPrivateKey = GetPrivateKeyFromX509(issuerCert);
            var certificate = generator.Generate(new Asn1SignatureFactory("SHA256WithRSA", issuerPrivateKey, new SecureRandom()));
            return new X509Certificate2(certificate.GetEncoded());
        }

        /// <summary>
        /// 创建 X509Certificate2。
        /// </summary>
        private static X509Certificate2 CreateX509Certificate2(Org.BouncyCastle.X509.X509Certificate certificate, AsymmetricKeyParameter privateKey)
        {
            var store = new Pkcs12Store();
            var certEntry = new X509CertificateEntry(certificate);
            store.SetCertificateEntry("cert", certEntry);
            store.SetKeyEntry("key", new AsymmetricKeyEntry(privateKey), [certEntry]);
            using var ms = new MemoryStream();
            store.Save(ms, DefaultPfxPassword.ToCharArray(), new SecureRandom());
            ms.Position = 0;
            return new X509Certificate2(ms.ToArray(), DefaultPfxPassword, X509KeyStorageFlags.Exportable);
        }

        /// <summary>
        /// 获取 X509Certificate2 的私钥。
        /// </summary>
        private static AsymmetricKeyParameter GetPrivateKeyFromX509(X509Certificate2 cert)
        {
            if (cert.PrivateKey is RSA rsa)
            {
                var parameters = rsa.ExportParameters(true);
                return new RsaPrivateCrtKeyParameters(
                    new BigInteger(1, parameters.Modulus),
                    new BigInteger(1, parameters.Exponent),
                    new BigInteger(1, parameters.D),
                    new BigInteger(1, parameters.P),
                    new BigInteger(1, parameters.Q),
                    new BigInteger(1, parameters.DP),
                    new BigInteger(1, parameters.DQ),
                    new BigInteger(1, parameters.InverseQ));
            }
            throw new NotSupportedException("当前仅支持 RSA 密钥。");
        }

        /// <summary>
        /// 创建 X509Name。
        /// </summary>
        private static X509Name CreateX509Name(Dictionary<string, string> attributes)
        {
            var oids = new List<DerObjectIdentifier>();
            var values = new List<string>();
            if (attributes.TryGetValue("C", out var c)) { oids.Add(X509Name.C); values.Add(c); }
            if (attributes.TryGetValue("ST", out var st)) { oids.Add(X509Name.ST); values.Add(st); }
            if (attributes.TryGetValue("L", out var l)) { oids.Add(X509Name.L); values.Add(l); }
            if (attributes.TryGetValue("O", out var o)) { oids.Add(X509Name.O); values.Add(o); }
            if (attributes.TryGetValue("CN", out var cn)) { oids.Add(X509Name.CN); values.Add(cn); }
            return new X509Name(oids, values);
        }

        /// <summary>
        /// 获取证书用途的 KeyPurposeID。
        /// </summary>
        private static KeyPurposeID GetKeyPurposeId(CertificatePurpose purpose)
        {
            return purpose switch
            {
                CertificatePurpose.ServerAuthentication => KeyPurposeID.IdKPServerAuth,
                CertificatePurpose.ClientAuthentication => KeyPurposeID.IdKPClientAuth,
                CertificatePurpose.CodeSigning => KeyPurposeID.IdKPCodeSigning,
                CertificatePurpose.EmailProtection => KeyPurposeID.IdKPEmailProtection,
                _ => throw new ArgumentException("无效的证书用途。"),
            };
        }

        /// <summary>
        /// 导出 PEM 格式证书。
        /// </summary>
        public static string ExportToPem(X509Certificate2 cert)
        {
            var builder = new System.Text.StringBuilder();
            builder.AppendLine("-----BEGIN CERTIFICATE-----");
            var base64 = Convert.ToBase64String(cert.RawData, Base64FormattingOptions.InsertLineBreaks);
            builder.AppendLine(base64);
            builder.AppendLine("-----END CERTIFICATE-----");
            return builder.ToString();
        }

        /// <summary>
        /// 导出 DER 格式证书。
        /// </summary>
        public static byte[] ExportToDer(X509Certificate2 cert) => cert.RawData;

        /// <summary>
        /// 导出 PFX 格式证书。
        /// </summary>
        public static byte[] ExportToPfx(X509Certificate2 cert, string password)
        {
            if (!cert.HasPrivateKey) throw new InvalidOperationException("证书不包含私钥。");
            return cert.Export(X509ContentType.Pfx, password);
        }

        /// <summary>
        /// 导出 P7B 格式证书。
        /// </summary>
        public static byte[] ExportToP7b(X509Certificate2Collection certs) => certs.Export(X509ContentType.Pkcs7);

        /// <summary>
        /// 加载 PEM 格式证书。
        /// </summary>
        public static X509Certificate2 LoadFromPem(string pem) => new(ExtractDerFromPem(pem));

        /// <summary>
        /// 加载 DER 格式证书。
        /// </summary>
        public static X509Certificate2 LoadFromDer(byte[] derBytes) => new(derBytes);

        /// <summary>
        /// 加载 PFX 格式证书。
        /// </summary>
        public static X509Certificate2 LoadFromPfx(byte[] pfxBytes, string password) => new(pfxBytes, password, X509KeyStorageFlags.Exportable);

        /// <summary>
        /// 加载 P7B 格式证书。
        /// </summary>
        public static X509Certificate2Collection LoadFromP7b(byte[] p7bBytes)
        {
            var collection = new X509Certificate2Collection();
            collection.Import(p7bBytes, null, X509KeyStorageFlags.DefaultKeySet);
            return collection;
        }

        /// <summary>
        /// 提取 PEM 格式证书中的 DER 数据。
        /// </summary>
        private static byte[] ExtractDerFromPem(string pem)
        {
            const string beginCert = "-----BEGIN CERTIFICATE-----";
            const string endCert = "-----END CERTIFICATE-----";
            int start = pem.IndexOf(beginCert);
            if (start == -1) throw new ArgumentException("无效的 PEM 格式：缺少 BEGIN CERTIFICATE。");
            start += beginCert.Length;
            int end = pem.IndexOf(endCert, start);
            if (end == -1) throw new ArgumentException("无效的 PEM 格式：缺少 END CERTIFICATE。");
            string base64 = pem.Substring(start, end - start).Replace("\r", "").Replace("\n", "");
            return Convert.FromBase64String(base64);
        }

        /// <summary>
        /// 验证证书。
        /// </summary>
        public static bool VerifyCertificate(X509Certificate2 cert, X509Certificate2 rootCert)
        {
            var chain = new X509Chain();
            chain.ChainPolicy.ExtraStore.Add(rootCert);
            chain.ChainPolicy.VerificationFlags = X509VerificationFlags.AllowUnknownCertificateAuthority;
            chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
            return chain.Build(cert);
        }

        /// <summary>
        /// 获取证书信息。
        /// </summary>
        public static Dictionary<string, string> GetCertificateInfo(X509Certificate2 cert)
        {
            return new Dictionary<string, string>
            {
                { "Subject", cert.Subject },
                { "Issuer", cert.Issuer },
                { "SerialNumber", cert.SerialNumber },
                { "NotBefore", cert.NotBefore.ToString("yyyy-MM-dd HH:mm:ss") },
                { "NotAfter", cert.NotAfter.ToString("yyyy-MM-dd HH:mm:ss") },
                { "Thumbprint", cert.Thumbprint }
            };
        }

        /// <summary>
        /// 检查证书是否已安装。
        /// </summary>
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
        /// 安装证书。
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
        /// 卸载证书。
        /// </summary>
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