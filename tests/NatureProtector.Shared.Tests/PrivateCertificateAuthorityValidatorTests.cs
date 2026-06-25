using System.Net.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using NatureProtector.Shared.Configuration;

namespace NatureProtector.Shared.Tests;

public sealed class PrivateCertificateAuthorityValidatorTests : IDisposable
{
    private readonly string _temporaryDirectory = Path.Combine(
        Path.GetTempPath(),
        $"natureprotector-private-ca-{Guid.NewGuid():N}");

    [Fact]
    public void Create_returns_null_when_no_private_ca_is_configured()
    {
        Assert.Null(PrivateCertificateAuthorityValidator.Create(null));
        Assert.Null(PrivateCertificateAuthorityValidator.Create("   "));
    }

    [Fact]
    public void Create_fails_closed_when_the_configured_ca_file_is_missing()
    {
        var missing = Path.Combine(_temporaryDirectory, "missing-ca.pem");
        Assert.Throws<FileNotFoundException>(() =>
            PrivateCertificateAuthorityValidator.Create(missing));
    }

    [Fact]
    public void Validate_accepts_a_leaf_signed_by_the_configured_private_root()
    {
        Directory.CreateDirectory(_temporaryDirectory);
        using var rootKey = RSA.Create(2048);
        var rootRequest = new CertificateRequest(
            "CN=NatureProtector Test Root",
            rootKey,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);
        rootRequest.CertificateExtensions.Add(
            new X509BasicConstraintsExtension(true, false, 0, true));
        rootRequest.CertificateExtensions.Add(
            new X509KeyUsageExtension(X509KeyUsageFlags.KeyCertSign, true));
        rootRequest.CertificateExtensions.Add(
            new X509SubjectKeyIdentifierExtension(rootRequest.PublicKey, false));
        using var root = rootRequest.CreateSelfSigned(
            DateTimeOffset.UtcNow.AddMinutes(-5),
            DateTimeOffset.UtcNow.AddDays(2));

        using var leafKey = RSA.Create(2048);
        var leafRequest = new CertificateRequest(
            "CN=rabbitmq.staging.natureprotector.internal",
            leafKey,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);
        leafRequest.CertificateExtensions.Add(
            new X509BasicConstraintsExtension(false, false, 0, true));
        leafRequest.CertificateExtensions.Add(
            new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature, true));
        leafRequest.CertificateExtensions.Add(
            new X509EnhancedKeyUsageExtension(
                new OidCollection { new("1.3.6.1.5.5.7.3.1") },
                true));
        var san = new SubjectAlternativeNameBuilder();
        san.AddDnsName("rabbitmq.staging.natureprotector.internal");
        leafRequest.CertificateExtensions.Add(san.Build());
        var serial = RandomNumberGenerator.GetBytes(16);
        using var unsignedLeaf = leafRequest.Create(
            root,
            DateTimeOffset.UtcNow.AddMinutes(-5),
            DateTimeOffset.UtcNow.AddDays(1),
            serial);
        using var leaf = unsignedLeaf.CopyWithPrivateKey(leafKey);

        var caPath = Path.Combine(_temporaryDirectory, "root-ca.pem");
        File.WriteAllText(caPath, root.ExportCertificatePem());
        var validator = PrivateCertificateAuthorityValidator.Create(caPath);

        using var presentedChain = new X509Chain();
        presentedChain.ChainPolicy.ExtraStore.Add(root);
        _ = presentedChain.Build(leaf);

        Assert.NotNull(validator);
        Assert.True(validator!.Validate(
            sender: null,
            certificate: leaf,
            presentedChain,
            SslPolicyErrors.RemoteCertificateChainErrors));
    }

    [Fact]
    public void Validate_rejects_name_mismatch_before_chain_acceptance()
    {
        Directory.CreateDirectory(_temporaryDirectory);
        using var certificate = CreateSelfSignedCertificate("CN=unexpected.example");
        var caPath = Path.Combine(_temporaryDirectory, "root-ca.pem");
        File.WriteAllText(caPath, certificate.ExportCertificatePem());
        var validator = PrivateCertificateAuthorityValidator.Create(caPath);

        Assert.NotNull(validator);
        Assert.False(validator!.Validate(
            sender: null,
            certificate,
            presentedChain: null,
            SslPolicyErrors.RemoteCertificateNameMismatch));
    }

    public void Dispose()
    {
        if (Directory.Exists(_temporaryDirectory))
        {
            Directory.Delete(_temporaryDirectory, recursive: true);
        }
    }

    private static X509Certificate2 CreateSelfSignedCertificate(string subject)
    {
        using var key = RSA.Create(2048);
        var request = new CertificateRequest(
            subject,
            key,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(
            new X509BasicConstraintsExtension(true, false, 0, true));
        return request.CreateSelfSigned(
            DateTimeOffset.UtcNow.AddMinutes(-5),
            DateTimeOffset.UtcNow.AddHours(1));
    }
}
