using System.Net.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace NatureProtector.Shared.Configuration;

/// <summary>
/// Validates a TLS peer against an explicitly mounted private root CA without
/// weakening hostname validation or trusting every certificate presented by
/// the peer.
/// </summary>
public sealed class PrivateCertificateAuthorityValidator
{
    private readonly X509Certificate2 _rootCertificate;
    private readonly bool _checkRevocation;

    private PrivateCertificateAuthorityValidator(
        X509Certificate2 rootCertificate,
        bool checkRevocation)
    {
        _rootCertificate = rootCertificate;
        _checkRevocation = checkRevocation;
    }

    public static PrivateCertificateAuthorityValidator? Create(
        string? certificateAuthorityPath,
        bool checkRevocation = false)
    {
        if (string.IsNullOrWhiteSpace(certificateAuthorityPath))
        {
            return null;
        }

        if (!File.Exists(certificateAuthorityPath))
        {
            throw new FileNotFoundException(
                "The configured private certificate authority file does not exist.",
                certificateAuthorityPath);
        }

        X509Certificate2 root;
        try
        {
            root = X509Certificate2.CreateFromPemFile(certificateAuthorityPath);
        }
        catch (CryptographicException)
        {
            root = new X509Certificate2(certificateAuthorityPath);
        }

        return new PrivateCertificateAuthorityValidator(root, checkRevocation);
    }

    public bool Validate(
        object? sender,
        X509Certificate? certificate,
        X509Chain? presentedChain,
        SslPolicyErrors policyErrors)
    {
        _ = sender;
        if (certificate is null ||
            policyErrors.HasFlag(SslPolicyErrors.RemoteCertificateNotAvailable) ||
            policyErrors.HasFlag(SslPolicyErrors.RemoteCertificateNameMismatch))
        {
            return false;
        }

        using var leaf = new X509Certificate2(certificate);
        using var validationChain = new X509Chain();
        validationChain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
        validationChain.ChainPolicy.CustomTrustStore.Add(_rootCertificate);
        validationChain.ChainPolicy.RevocationMode = _checkRevocation
            ? X509RevocationMode.Online
            : X509RevocationMode.NoCheck;
        validationChain.ChainPolicy.RevocationFlag = X509RevocationFlag.ExcludeRoot;
        validationChain.ChainPolicy.VerificationFlags = X509VerificationFlags.NoFlag;

        if (presentedChain is not null)
        {
            foreach (var element in presentedChain.ChainElements.Cast<X509ChainElement>().Skip(1))
            {
                validationChain.ChainPolicy.ExtraStore.Add(element.Certificate);
            }
        }

        return validationChain.Build(leaf);
    }
}
