using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using AutoUpdater.Core.Configuration;
using Microsoft.Extensions.Logging;

namespace AutoUpdater.Core.Utilities;

/// <summary>
/// SSL 인증서 검증 유틸리티 클래스
/// </summary>
public static class SslCertificateValidator
{
    private static ILogger? _logger;

    /// <summary>
    /// 로거 설정
    /// </summary>
    /// <param name="logger">로거 인스턴스</param>
    public static void SetLogger(ILogger logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// SSL 설정에 따른 인증서 검증 콜백 생성 (HttpClientHandler용)
    /// </summary>
    /// <param name="sslConfig">SSL 설정</param>
    /// <returns>인증서 검증 콜백</returns>
    public static Func<HttpRequestMessage, X509Certificate2?, X509Chain?, SslPolicyErrors, bool> CreateHttpClientValidationCallback(SslConfiguration sslConfig)
    {
        return (request, certificate, chain, sslPolicyErrors) =>
        {
            return ValidateCertificate(certificate, chain, sslPolicyErrors, sslConfig);
        };
    }

    /// <summary>
    /// SSL 설정에 따른 인증서 검증 콜백 생성 (일반 RemoteCertificateValidationCallback용)
    /// </summary>
    /// <param name="sslConfig">SSL 설정</param>
    /// <returns>인증서 검증 콜백</returns>
    public static RemoteCertificateValidationCallback CreateValidationCallback(SslConfiguration sslConfig)
    {
        return (sender, certificate, chain, sslPolicyErrors) =>
        {
            X509Certificate2? cert2 = certificate as X509Certificate2 ?? (certificate != null ? new X509Certificate2(certificate) : null);
            return ValidateCertificate(cert2, chain, sslPolicyErrors, sslConfig);
        };
    }

    /// <summary>
    /// 인증서 검증 로직
    /// </summary>
    /// <param name="certificate">서버 인증서</param>
    /// <param name="chain">인증서 체인</param>
    /// <param name="sslPolicyErrors">SSL 정책 오류</param>
    /// <param name="sslConfig">SSL 설정</param>
    /// <returns>검증 결과</returns>
    private static bool ValidateCertificate(
        X509Certificate2? certificate,
        X509Chain? chain,
        SslPolicyErrors sslPolicyErrors,
        SslConfiguration sslConfig)
    {
        _logger?.LogDebug("SSL 인증서 검증 시작 - 오류: {SslPolicyErrors}", sslPolicyErrors);
        _logger?.LogDebug("SSL 설정 - IgnoreAllSslErrors: {IgnoreAllSslErrors}, IgnoreCertificateNameMismatch: {IgnoreCertificateNameMismatch}, AllowSelfSignedCertificates: {AllowSelfSignedCertificates}, IgnoreCertificateChainErrors: {IgnoreCertificateChainErrors}", 
            sslConfig.IgnoreAllSslErrors, sslConfig.IgnoreCertificateNameMismatch, sslConfig.AllowSelfSignedCertificates, sslConfig.IgnoreCertificateChainErrors);

        // 모든 SSL 오류를 무시하는 경우 (개발용)
        if (sslConfig.IgnoreAllSslErrors)
        {
            _logger?.LogWarning("모든 SSL 오류를 무시합니다. 이는 개발 환경에서만 사용해야 합니다. 오류: {SslPolicyErrors}", sslPolicyErrors);
            return true;
        }

        // 오류가 없으면 유효한 인증서
        if (sslPolicyErrors == SslPolicyErrors.None)
        {
            _logger?.LogDebug("SSL 인증서가 유효합니다.");
            return true;
        }

        if (certificate == null)
        {
            _logger?.LogError("SSL 인증서가 null입니다.");
            return false;
        }

        _logger?.LogDebug("SSL 인증서 검증 중: Subject={Subject}, Issuer={Issuer}, Thumbprint={Thumbprint}",
            certificate.Subject, certificate.Issuer, certificate.Thumbprint);

        // 신뢰할 인증서 지문 목록에 있는지 확인
        if (sslConfig.TrustedCertificateThumbprints.Count > 0)
        {
            var thumbprint = certificate.Thumbprint;
            if (sslConfig.TrustedCertificateThumbprints.Contains(thumbprint, StringComparer.OrdinalIgnoreCase))
            {
                _logger?.LogInformation("신뢰할 인증서 지문 목록에서 인증서를 찾았습니다: {Thumbprint}", thumbprint);
                return true;
            }
        }

        bool isValid = true;

        // 자체 서명된 인증서 처리
        if (sslPolicyErrors.HasFlag(SslPolicyErrors.RemoteCertificateChainErrors))
        {
            if (sslConfig.AllowSelfSignedCertificates || sslConfig.IgnoreCertificateChainErrors)
            {
                _logger?.LogWarning("인증서 체인 오류를 무시합니다.");
            }
            else
            {
                var chainErrors = chain?.ChainStatus?.Select(s => s.StatusInformation).ToArray() ?? Array.Empty<string>();
                _logger?.LogError("인증서 체인 오류: {ChainErrors}", string.Join(", ", chainErrors));
                isValid = false;
            }
        }

        // 인증서 이름 불일치 처리
        if (sslPolicyErrors.HasFlag(SslPolicyErrors.RemoteCertificateNameMismatch))
        {
            if (sslConfig.IgnoreCertificateNameMismatch)
            {
                _logger?.LogWarning("인증서 이름 불일치를 무시합니다. Subject: {Subject}", certificate?.Subject);
            }
            else
            {
                _logger?.LogError("인증서 이름이 호스트명과 일치하지 않습니다. Subject: {Subject}", certificate?.Subject);
                isValid = false;
            }
        }

        // 인증서를 사용할 수 없는 경우
        if (sslPolicyErrors.HasFlag(SslPolicyErrors.RemoteCertificateNotAvailable))
        {
            _logger?.LogError("원격 인증서를 사용할 수 없습니다.");
            isValid = false;
        }

        if (isValid)
        {
            _logger?.LogInformation("SSL 인증서 검증 통과: {Subject}", certificate?.Subject);
        }
        else
        {
            _logger?.LogError("SSL 인증서 검증 실패: {Subject}, Errors: {Errors}", certificate?.Subject, sslPolicyErrors);
        }

        return isValid;
    }

    /// <summary>
    /// 인증서 지문을 SHA256으로 계산
    /// </summary>
    /// <param name="certificate">인증서</param>
    /// <returns>SHA256 지문</returns>
    public static string GetCertificateThumbprint(X509Certificate2 certificate)
    {
        return certificate.Thumbprint;
    }

    /// <summary>
    /// 인증서 정보를 로그로 출력
    /// </summary>
    /// <param name="certificate">인증서</param>
    public static void LogCertificateInfo(X509Certificate2 certificate)
    {
        _logger?.LogInformation("인증서 정보:");
        _logger?.LogInformation("  Subject: {Subject}", certificate.Subject);
        _logger?.LogInformation("  Issuer: {Issuer}", certificate.Issuer);
        _logger?.LogInformation("  Thumbprint: {Thumbprint}", certificate.Thumbprint);
        _logger?.LogInformation("  Valid From: {NotBefore}", certificate.NotBefore);
        _logger?.LogInformation("  Valid To: {NotAfter}", certificate.NotAfter);
        _logger?.LogInformation("  Serial Number: {SerialNumber}", certificate.SerialNumber);
    }
} 