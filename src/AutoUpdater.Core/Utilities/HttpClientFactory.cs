using System.Net;
using System.Security.Cryptography.X509Certificates;
using AutoUpdater.Core.Configuration;
using Microsoft.Extensions.Logging;

namespace AutoUpdater.Core.Utilities;

/// <summary>
/// SSL 설정이 적용된 HttpClient 팩토리
/// </summary>
public static class HttpClientFactory
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
    /// SSL 설정이 적용된 HttpClient 생성
    /// </summary>
    /// <param name="configuration">업데이터 설정</param>
    /// <returns>설정된 HttpClient</returns>
    public static HttpClient CreateHttpClient(UpdaterConfiguration configuration)
    {
        var handler = new HttpClientHandler();

        // SSL 설정 적용
        ConfigureSslSettings(handler, configuration.Ssl);

        // 프록시 설정 적용
        ConfigureProxy(handler, configuration.Proxy);

        var httpClient = new HttpClient(handler);

        // 기본 설정 적용
        ConfigureHttpClient(httpClient, configuration);

        return httpClient;
    }

    /// <summary>
    /// SSL 설정 적용
    /// </summary>
    /// <param name="handler">HttpClientHandler</param>
    /// <param name="sslConfig">SSL 설정</param>
    public static void ConfigureSslSettings(HttpClientHandler handler, SslConfiguration sslConfig)
    {
        if (sslConfig == null)
        {
            _logger?.LogWarning("SSL 설정이 null입니다. 기본 설정을 사용합니다.");
            return;
        }

        _logger?.LogInformation("SSL 설정 적용 중...");
        _logger?.LogInformation("SSL 설정 - IgnoreAllSslErrors: {IgnoreAllSslErrors}, IgnoreCertificateNameMismatch: {IgnoreCertificateNameMismatch}, AllowSelfSignedCertificates: {AllowSelfSignedCertificates}, IgnoreCertificateChainErrors: {IgnoreCertificateChainErrors}", 
            sslConfig.IgnoreAllSslErrors, sslConfig.IgnoreCertificateNameMismatch, sslConfig.AllowSelfSignedCertificates, sslConfig.IgnoreCertificateChainErrors);

        // 인증서 검증 콜백 설정
        handler.ServerCertificateCustomValidationCallback = SslCertificateValidator.CreateHttpClientValidationCallback(sslConfig);

        // 클라이언트 인증서 설정
        if (!string.IsNullOrEmpty(sslConfig.ClientCertificatePath))
        {
            try
            {
                var clientCert = LoadClientCertificate(sslConfig.ClientCertificatePath, sslConfig.ClientCertificatePassword);
                if (clientCert != null)
                {
                    handler.ClientCertificates.Add(clientCert);
                    _logger?.LogInformation("클라이언트 인증서가 추가되었습니다: {Subject}", clientCert.Subject);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "클라이언트 인증서 로드 실패: {CertPath}", sslConfig.ClientCertificatePath);
                throw;
            }
        }

        _logger?.LogDebug("SSL 설정 적용 완료");
    }

    /// <summary>
    /// 클라이언트 인증서 로드
    /// </summary>
    /// <param name="certPath">인증서 파일 경로</param>
    /// <param name="password">인증서 비밀번호</param>
    /// <returns>X509Certificate2 인스턴스</returns>
    private static X509Certificate2? LoadClientCertificate(string certPath, string? password)
    {
        if (!File.Exists(certPath))
        {
            _logger?.LogError("클라이언트 인증서 파일을 찾을 수 없습니다: {CertPath}", certPath);
            return null;
        }

        try
        {
            var cert = string.IsNullOrEmpty(password) 
                ? new X509Certificate2(certPath)
                : new X509Certificate2(certPath, password);

            _logger?.LogDebug("클라이언트 인증서 로드 성공: {Subject}", cert.Subject);
            return cert;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "클라이언트 인증서 로드 중 오류 발생: {CertPath}", certPath);
            throw;
        }
    }

    /// <summary>
    /// 프록시 설정 적용
    /// </summary>
    /// <param name="handler">HttpClientHandler</param>
    /// <param name="proxyConfig">프록시 설정</param>
    public static void ConfigureProxy(HttpClientHandler handler, ProxyConfiguration? proxyConfig)
    {
        if (proxyConfig == null || string.IsNullOrEmpty(proxyConfig.Url))
        {
            _logger?.LogDebug("프록시 설정이 없습니다.");
            return;
        }

        try
        {
            var proxy = new WebProxy(proxyConfig.Url);

            if (!string.IsNullOrEmpty(proxyConfig.Username))
            {
                proxy.Credentials = string.IsNullOrEmpty(proxyConfig.Domain)
                    ? new NetworkCredential(proxyConfig.Username, proxyConfig.Password)
                    : new NetworkCredential(proxyConfig.Username, proxyConfig.Password, proxyConfig.Domain);
            }

            handler.Proxy = proxy;
            handler.UseProxy = true;

            _logger?.LogInformation("프록시 설정 적용: {ProxyUrl}", proxyConfig.Url);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "프록시 설정 중 오류 발생: {ProxyUrl}", proxyConfig.Url);
            throw;
        }
    }

    /// <summary>
    /// HttpClient 기본 설정 적용
    /// </summary>
    /// <param name="httpClient">HttpClient</param>
    /// <param name="configuration">업데이터 설정</param>
    private static void ConfigureHttpClient(HttpClient httpClient, UpdaterConfiguration configuration)
    {
        // 타임아웃 설정
        httpClient.Timeout = TimeSpan.FromSeconds(configuration.TimeoutSeconds);

        // User-Agent 설정
        httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(configuration.UserAgent);

        // API 키 설정
        if (!string.IsNullOrEmpty(configuration.ApiKey))
        {
            httpClient.DefaultRequestHeaders.Add("X-API-Key", configuration.ApiKey);
        }

        // 추가 헤더 설정
        if (configuration.AdditionalHeaders != null)
        {
            foreach (var header in configuration.AdditionalHeaders)
            {
                httpClient.DefaultRequestHeaders.Add(header.Key, header.Value);
            }
        }

        _logger?.LogDebug("HttpClient 기본 설정 적용 완료");
    }
} 