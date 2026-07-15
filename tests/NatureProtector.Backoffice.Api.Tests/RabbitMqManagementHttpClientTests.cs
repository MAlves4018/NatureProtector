using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using NatureProtector.Backoffice.Api.Configuration;
using NatureProtector.Shared.Configuration;

namespace NatureProtector.Backoffice.Api.Tests;

public sealed class RabbitMqManagementHttpClientTests : IDisposable
{
    private readonly string _temporaryDirectory = Path.Combine(
        Path.GetTempPath(),
        $"natureprotector-rabbitmq-management-tls-{Guid.NewGuid():N}");

    [Fact]
    public void BuildQueuesUri_uses_typed_scheme_host_and_port()
    {
        var uri = RabbitMqManagementHttpClient.BuildQueuesUri(new RabbitMqOptions
        {
            HostName = "amqp.internal",
            ManagementScheme = "https",
            ManagementHost = "management.internal",
            ManagementPort = 15671
        });

        Assert.Equal(new Uri("https://management.internal:15671/api/queues"), uri);
    }

    [Fact]
    public async Task Dedicated_handler_accepts_private_ca_and_matching_hostname()
    {
        using var certificates = CertificateBundle.Create("localhost");
        await using var server = new TemporaryHttpsServer(certificates.Leaf);
        var caPath = WriteCertificate("trusted-root.pem", certificates.Root);
        using var client = CreateClient(caPath);

        HttpResponseMessage response;
        try
        {
            response = await client.GetAsync(server.Uri);
        }
        catch (Exception exception)
        {
            await server.Completion;
            throw new InvalidOperationException($"TLS server failed: {server.Failure}", exception);
        }

        using (response)
        {
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("[]", await response.Content.ReadAsStringAsync());
        }
    }

    [Fact]
    public async Task Dedicated_handler_rejects_wrong_private_ca()
    {
        using var serverCertificates = CertificateBundle.Create("localhost");
        using var unrelatedCertificates = CertificateBundle.Create("localhost");
        await using var server = new TemporaryHttpsServer(serverCertificates.Leaf);
        var caPath = WriteCertificate("wrong-root.pem", unrelatedCertificates.Root);
        using var client = CreateClient(caPath);

        await Assert.ThrowsAsync<HttpRequestException>(() => client.GetAsync(server.Uri));
    }

    [Fact]
    public async Task Dedicated_handler_rejects_hostname_mismatch()
    {
        using var certificates = CertificateBundle.Create("rabbitmq.internal");
        await using var server = new TemporaryHttpsServer(certificates.Leaf);
        var caPath = WriteCertificate("hostname-root.pem", certificates.Root);
        using var client = CreateClient(caPath);

        await Assert.ThrowsAsync<HttpRequestException>(() => client.GetAsync(server.Uri));
    }

    [Fact]
    public async Task Dedicated_handler_rejects_expired_leaf_certificate()
    {
        using var certificates = CertificateBundle.Create(
            "localhost",
            DateTimeOffset.UtcNow.AddDays(-2),
            DateTimeOffset.UtcNow.AddDays(-1));
        await using var server = new TemporaryHttpsServer(certificates.Leaf);
        var caPath = WriteCertificate("expired-root.pem", certificates.Root);
        using var client = CreateClient(caPath);

        await Assert.ThrowsAsync<HttpRequestException>(() => client.GetAsync(server.Uri));
    }

    public void Dispose()
    {
        if (Directory.Exists(_temporaryDirectory))
        {
            Directory.Delete(_temporaryDirectory, recursive: true);
        }
    }

    private HttpClient CreateClient(string caPath)
    {
        var options = new RabbitMqOptions
        {
            ManagementScheme = "https",
            ManagementCertificateAuthorityPath = caPath,
            ManagementTimeoutSeconds = 5
        };
        return new HttpClient(RabbitMqManagementHttpClient.CreatePrimaryHandler(options))
        {
            Timeout = TimeSpan.FromSeconds(5)
        };
    }

    private string WriteCertificate(string fileName, X509Certificate2 certificate)
    {
        Directory.CreateDirectory(_temporaryDirectory);
        var path = Path.Combine(_temporaryDirectory, fileName);
        File.WriteAllText(path, certificate.ExportCertificatePem());
        return path;
    }

    private sealed class CertificateBundle : IDisposable
    {
        private CertificateBundle(X509Certificate2 root, X509Certificate2 leaf)
        {
            Root = root;
            Leaf = leaf;
        }

        public X509Certificate2 Root { get; }
        public X509Certificate2 Leaf { get; }

        public static CertificateBundle Create(
            string dnsName,
            DateTimeOffset? leafNotBefore = null,
            DateTimeOffset? leafNotAfter = null)
        {
            using var rootKey = RSA.Create(2048);
            var rootRequest = new CertificateRequest(
                "CN=NatureProtector RabbitMQ Management Root",
                rootKey,
                HashAlgorithmName.SHA256,
                RSASignaturePadding.Pkcs1);
            rootRequest.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
            rootRequest.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.KeyCertSign, true));
            rootRequest.CertificateExtensions.Add(new X509SubjectKeyIdentifierExtension(rootRequest.PublicKey, false));
            var root = rootRequest.CreateSelfSigned(
                DateTimeOffset.UtcNow.AddDays(-3),
                DateTimeOffset.UtcNow.AddDays(3));

            using var leafKey = RSA.Create(2048);
            var leafRequest = new CertificateRequest(
                $"CN={dnsName}",
                leafKey,
                HashAlgorithmName.SHA256,
                RSASignaturePadding.Pkcs1);
            leafRequest.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, true));
            leafRequest.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature, true));
            leafRequest.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(
                new OidCollection { new("1.3.6.1.5.5.7.3.1") },
                true));
            var san = new SubjectAlternativeNameBuilder();
            san.AddDnsName(dnsName);
            leafRequest.CertificateExtensions.Add(san.Build());
            var unsignedLeaf = leafRequest.Create(
                root,
                leafNotBefore ?? DateTimeOffset.UtcNow.AddMinutes(-5),
                leafNotAfter ?? DateTimeOffset.UtcNow.AddDays(1),
                RandomNumberGenerator.GetBytes(16));
            using var leafWithPrivateKey = unsignedLeaf.CopyWithPrivateKey(leafKey);
            var leaf = X509CertificateLoader.LoadPkcs12(
                leafWithPrivateKey.Export(X509ContentType.Pkcs12),
                password: null,
                X509KeyStorageFlags.UserKeySet | X509KeyStorageFlags.Exportable);
            unsignedLeaf.Dispose();

            return new CertificateBundle(root, leaf);
        }

        public void Dispose()
        {
            Leaf.Dispose();
            Root.Dispose();
        }
    }

    private sealed class TemporaryHttpsServer : IAsyncDisposable
    {
        private readonly TcpListener _listener;
        private readonly Task _serverTask;

        public TemporaryHttpsServer(X509Certificate2 certificate)
        {
            _listener = new TcpListener(IPAddress.Loopback, 0);
            _listener.Start();
            var port = ((IPEndPoint)_listener.LocalEndpoint).Port;
            Uri = new Uri($"https://localhost:{port}/api/queues");
            _serverTask = ServeOnceAsync(certificate);
        }

        public Uri Uri { get; }

        public Task Completion => _serverTask;

        public Exception? Failure { get; private set; }

        public async ValueTask DisposeAsync()
        {
            _listener.Stop();
            try
            {
                await _serverTask.WaitAsync(TimeSpan.FromSeconds(2));
            }
            catch (Exception exception) when (
                exception is OperationCanceledException or TimeoutException or SocketException or IOException or AuthenticationException)
            {
                // Negative TLS tests intentionally terminate the connection.
            }
        }

        private async Task ServeOnceAsync(X509Certificate2 certificate)
        {
            try
            {
                using var client = await _listener.AcceptTcpClientAsync();
                await using var stream = new SslStream(client.GetStream(), leaveInnerStreamOpen: false);
                await stream.AuthenticateAsServerAsync(new SslServerAuthenticationOptions
                {
                    ServerCertificate = certificate,
                    ClientCertificateRequired = false,
                    EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13,
                    CertificateRevocationCheckMode = X509RevocationMode.NoCheck
                });

                var buffer = new byte[4096];
                var received = new MemoryStream();
                while (received.Length < 16_384)
                {
                    var read = await stream.ReadAsync(buffer);
                    if (read == 0)
                    {
                        return;
                    }

                    received.Write(buffer, 0, read);
                    if (Encoding.ASCII.GetString(received.ToArray()).Contains("\r\n\r\n", StringComparison.Ordinal))
                    {
                        break;
                    }
                }

                const string body = "[]";
                var response = Encoding.ASCII.GetBytes(
                    "HTTP/1.1 200 OK\r\n" +
                    "Content-Type: application/json\r\n" +
                    $"Content-Length: {body.Length}\r\n" +
                    "Connection: close\r\n\r\n" +
                    body);
                await stream.WriteAsync(response);
                await stream.FlushAsync();
            }
            catch (Exception exception) when (
                exception is OperationCanceledException or SocketException or IOException or AuthenticationException or ObjectDisposedException)
            {
                Failure = exception;
                // Expected for client-side certificate rejection cases.
            }
        }
    }
}
