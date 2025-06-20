using AutoUpdater.Core.Configuration;
using AutoUpdater.Core.Utilities;
using System.Net.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Xunit;

namespace AutoUpdater.Tests;

/// <summary>
/// SSL 인증서 검증 테스트 클래스
/// </summary>
public class SslCertificateValidatorTests
{
    [Fact]
    public void CreateValidationCallback_ShouldReturnValidCallback()
    {
        // Arrange
        var sslConfig = new SslConfiguration
        {
            AllowSelfSignedCertificates = true
        };

        // Act
        var callback = SslCertificateValidator.CreateValidationCallback(sslConfig);

        // Assert
        Assert.NotNull(callback);
    }

    [Fact]
    public void ValidateCertificate_WithIgnoreAllSslErrors_ShouldReturnTrue()
    {
        // Arrange
        var sslConfig = new SslConfiguration
        {
            IgnoreAllSslErrors = true
        };
        var callback = SslCertificateValidator.CreateValidationCallback(sslConfig);

        // Act
        var result = callback(null!, null, null, SslPolicyErrors.RemoteCertificateChainErrors);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void ValidateCertificate_WithNoErrors_ShouldReturnTrue()
    {
        // Arrange
        var sslConfig = new SslConfiguration();
        var callback = SslCertificateValidator.CreateValidationCallback(sslConfig);

        // Act
        var result = callback(null!, null, null, SslPolicyErrors.None);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void ValidateCertificate_WithNullCertificate_ShouldReturnFalse()
    {
        // Arrange
        var sslConfig = new SslConfiguration();
        var callback = SslCertificateValidator.CreateValidationCallback(sslConfig);

        // Act
        var result = callback(null!, null, null, SslPolicyErrors.RemoteCertificateNotAvailable);

        // Assert
        Assert.False(result);
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(false, false)]
    public void ValidateCertificate_WithSelfSignedCertificate_ShouldRespectConfiguration(
        bool allowSelfSigned, bool expectedResult)
    {
        // Arrange
        var sslConfig = new SslConfiguration
        {
            AllowSelfSignedCertificates = allowSelfSigned
        };
        var callback = SslCertificateValidator.CreateValidationCallback(sslConfig);

        // Create a self-signed certificate for testing
        using var cert = CreateSelfSignedCertificate();

        // Act
        var result = callback(null!, cert, null, SslPolicyErrors.RemoteCertificateChainErrors);

        // Assert
        Assert.Equal(expectedResult, result);
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(false, false)]
    public void ValidateCertificate_WithNameMismatch_ShouldRespectConfiguration(
        bool ignoreNameMismatch, bool expectedResult)
    {
        // Arrange
        var sslConfig = new SslConfiguration
        {
            IgnoreCertificateNameMismatch = ignoreNameMismatch
        };
        var callback = SslCertificateValidator.CreateValidationCallback(sslConfig);

        // Create a certificate for testing
        using var cert = CreateSelfSignedCertificate();

        // Act
        var result = callback(null!, cert, null, SslPolicyErrors.RemoteCertificateNameMismatch);

        // Assert
        Assert.Equal(expectedResult, result);
    }

    [Fact]
    public void ValidateCertificate_WithTrustedThumbprint_ShouldReturnTrue()
    {
        // Arrange
        using var cert = CreateSelfSignedCertificate();
        var sslConfig = new SslConfiguration
        {
            TrustedCertificateThumbprints = { cert.Thumbprint }
        };
        var callback = SslCertificateValidator.CreateValidationCallback(sslConfig);

        // Act
        var result = callback(null!, cert, null, SslPolicyErrors.RemoteCertificateChainErrors);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void GetCertificateThumbprint_ShouldReturnCorrectThumbprint()
    {
        // Arrange
        using var cert = CreateSelfSignedCertificate();

        // Act
        var thumbprint = SslCertificateValidator.GetCertificateThumbprint(cert);

        // Assert
        Assert.Equal(cert.Thumbprint, thumbprint);
        Assert.NotEmpty(thumbprint);
    }

    /// <summary>
    /// 테스트용 자체 서명된 인증서 생성
    /// </summary>
    /// <returns>X509Certificate2 인스턴스</returns>
    private static X509Certificate2 CreateSelfSignedCertificate()
    {
        // 테스트용 간단한 인증서 생성
        // 실제 환경에서는 더 복잡한 인증서를 사용
        var distinguishedName = new X500DistinguishedName("CN=test");
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest(distinguishedName, rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        
        var certificate = request.CreateSelfSigned(DateTimeOffset.Now, DateTimeOffset.Now.AddDays(1));
        return certificate;
    }
} 