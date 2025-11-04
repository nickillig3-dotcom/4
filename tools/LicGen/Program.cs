using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

static class Program
{
    static int Main(string[] args)
    {
        if (args.Length == 0 || Is(args, "init"))
            return Init();

        if (Is(args, "make"))
            return Make(args);

        Console.WriteLine("Usage:");
        Console.WriteLine("  licgen init");
        Console.WriteLine("  licgen make --email <addr> [--edition Pro] [--expires YYYY-MM-DD] [--priv path] [--out file]");
        return 1;
    }

    static bool Is(string[] a, string cmd) => a[0].Equals(cmd, StringComparison.OrdinalIgnoreCase);

    static int Init()
    {
        Directory.CreateDirectory("tools/LicGen");
        using var rsa = RSA.Create(2048);
        var priv = rsa.ExportPkcs8PrivateKeyPem();
        var pub  = rsa.ExportSubjectPublicKeyInfoPem();
        File.WriteAllText("tools/LicGen/private.pem", priv);
        File.WriteAllText("tools/LicGen/public.pem",  pub);
        Console.WriteLine("Generated keys:");
        Console.WriteLine("  tools/LicGen/private.pem");
        Console.WriteLine("  tools/LicGen/public.pem");
        return 0;
    }

    static string Arg(string[] a, string name, string? dflt = null)
    {
        var i = Array.IndexOf(a, name);
        if (i >= 0 && i + 1 < a.Length) return a[i + 1];
        if (dflt is not null) return dflt;
        throw new ArgumentException($"Missing {name}");
    }

    static int Make(string[] a)
    {
        var email   = Arg(a, "--email");
        var edition = Arg(a, "--edition", "Pro");
        var expires = Arg(a, "--expires", DateTime.UtcNow.AddYears(10).ToString("yyyy-MM-dd"));
        var priv    = Arg(a, "--priv", "tools/LicGen/private.pem");
        var output  = Arg(a, "--out",  $"license_{email.Replace('@','_').Replace('.','_')}.lic");

        var payload = $"{email}|{edition}|{expires}";
        var data    = Encoding.UTF8.GetBytes(payload);

        using var rsa = RSA.Create();
        rsa.ImportFromPem(File.ReadAllText(priv));

        var sig = rsa.SignData(data, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        var obj = new { email, edition, expires, sig = Convert.ToBase64String(sig) };
        var json = JsonSerializer.Serialize(obj, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(output, json);
        Console.WriteLine("Wrote license: " + output);
        return 0;
    }
}
