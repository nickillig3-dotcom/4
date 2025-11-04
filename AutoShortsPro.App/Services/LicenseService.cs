using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace AutoShortsPro.App.Services
{
    public static class LicenseService
    {
        // Platzhalter – später echten Public Key einfügen
        private static readonly string PublicKeyPem = @"-----BEGIN PUBLIC KEY-----
MIIBIjANBgkqhkiG9w0BAQEFAAOCAQ8AMIIBCgKCAQEAwb+6x9+dummyplaceholderkey+7x
-----END PUBLIC KEY-----";

        private static bool _isPro = false;
        public static bool IsPro => _isPro;

        public static bool LoadAndVerify(string licPath)
        {
            try
            {
                var json = File.ReadAllText(licPath);
                var model = JsonSerializer.Deserialize<LicenseModel>(json);
                if (model == null) return false;

                var payload = $"{model.email}|{model.edition}|{model.expires}";
                var data = Encoding.UTF8.GetBytes(payload);
                using var rsa = RSA.Create();
                rsa.ImportFromPem(PublicKeyPem);

                var sig = Convert.FromBase64String(model.sig);
                bool ok = rsa.VerifyData(data, sig, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
                if (!ok) return false;

                if (!string.Equals(model.edition, "Pro", StringComparison.OrdinalIgnoreCase)) return false;
                if (DateTime.TryParse(model.expires, out var dt) && dt.ToUniversalTime() < DateTime.UtcNow) return false;

                _isPro = true;
                return true;
            }
            catch
            {
                return false;
            }
        }
    }

    public class LicenseModel
    {
        public string email { get; set; } = "";
        public string edition { get; set; } = "";
        public string expires { get; set; } = "2099-12-31";
        public string sig { get; set; } = "";
    }
}
