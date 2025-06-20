using AutoUpdater.Core.Configuration;
using AutoUpdater.Core.Interfaces;
using AutoUpdater.Core.Models;
using AutoUpdater.Core.Utilities;
using Microsoft.Extensions.Logging;

namespace AutoUpdater.Core.Services;

/// <summary>
/// HTTP 기반 업데이트 다운로드 구현 클래스
/// </summary>
public class HttpUpdateDownloader : IUpdateDownloader, IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly UpdaterConfiguration _configuration;
    private readonly ILogger<HttpUpdateDownloader> _logger;
    private readonly bool _ownsHttpClient;

    /// <summary>
    /// 다운로드 진행률 이벤트
    /// </summary>
    public event EventHandler<int>? ProgressChanged;

    /// <summary>
    /// 생성자 (HttpClient를 직접 받는 경우)
    /// </summary>
    public HttpUpdateDownloader(
        HttpClient httpClient,
        UpdaterConfiguration configuration,
        ILogger<HttpUpdateDownloader> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _ownsHttpClient = false;
    }

    /// <summary>
    /// 생성자 (설정으로부터 HttpClient 생성)
    /// </summary>
    public HttpUpdateDownloader(
        UpdaterConfiguration configuration,
        ILogger<HttpUpdateDownloader> logger)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        
        // SSL 설정이 적용된 HttpClient 생성
        HttpClientFactory.SetLogger(_logger);
        SslCertificateValidator.SetLogger(_logger);
        _httpClient = HttpClientFactory.CreateHttpClient(_configuration);
        _ownsHttpClient = true;
    }

    /// <summary>
    /// 업데이트 파일 다운로드
    /// </summary>
    public async Task<string> DownloadUpdateAsync(UpdateInfo updateInfo, string downloadPath, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(updateInfo);
        ArgumentException.ThrowIfNullOrEmpty(downloadPath);

        _logger.LogInformation("업데이트 다운로드 시작: {DownloadUrl}", updateInfo.DownloadUrl);

        try
        {
            // 다운로드 디렉토리 생성
            FileHelper.EnsureDirectoryExists(downloadPath);

            // 파일명 추출 또는 생성
            var fileName = ExtractFileNameFromUrl(updateInfo.DownloadUrl) ?? 
                          $"update_{updateInfo.Version}_{Guid.NewGuid():N}.tmp";
            var filePath = Path.Combine(downloadPath, fileName);

            // 재시도 로직과 함께 다운로드
            await DownloadWithRetryAsync(updateInfo.DownloadUrl, filePath, updateInfo.FileSize, cancellationToken);

            // 파일 무결성 검증
            if (!string.IsNullOrEmpty(updateInfo.FileHash))
            {
                _logger.LogInformation("파일 무결성 검증 중...");
                var isValid = await VerifyFileIntegrityAsync(filePath, updateInfo.FileHash);
                if (!isValid)
                {
                    File.Delete(filePath);
                    throw new InvalidOperationException("다운로드된 파일의 무결성 검증에 실패했습니다.");
                }
                _logger.LogInformation("파일 무결성 검증 완료");
            }

            _logger.LogInformation("업데이트 다운로드 완료: {FilePath}", filePath);
            return filePath;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "업데이트 다운로드 중 오류가 발생했습니다.");
            throw;
        }
    }

    /// <summary>
    /// 파일 무결성 검증
    /// </summary>
    public async Task<bool> VerifyFileIntegrityAsync(string filePath, string expectedHash)
    {
        try
        {
            return await FileHelper.VerifyFileHashAsync(filePath, expectedHash);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "파일 무결성 검증 중 오류가 발생했습니다: {FilePath}", filePath);
            return false;
        }
    }

    /// <summary>
    /// 재시도 로직과 함께 다운로드
    /// </summary>
    private async Task DownloadWithRetryAsync(string downloadUrl, string filePath, long expectedSize, CancellationToken cancellationToken)
    {
        var retryCount = 0;
        var maxRetries = _configuration.RetryCount;

        while (retryCount <= maxRetries)
        {
            try
            {
                await DownloadFileAsync(downloadUrl, filePath, expectedSize, cancellationToken);
                return; // 성공시 반환
            }
            catch (Exception ex) when (retryCount < maxRetries)
            {
                retryCount++;
                _logger.LogWarning(ex, "다운로드 실패 (재시도 {RetryCount}/{MaxRetries}): {Error}", 
                    retryCount, maxRetries, ex.Message);

                // 실패한 파일 삭제
                if (File.Exists(filePath))
                {
                    try { File.Delete(filePath); } catch { }
                }

                // 재시도 전 대기
                var delay = TimeSpan.FromSeconds(Math.Pow(2, retryCount)); // 지수 백오프
                await Task.Delay(delay, cancellationToken);
            }
        }

        throw new InvalidOperationException($"최대 재시도 횟수({maxRetries})를 초과했습니다.");
    }

    /// <summary>
    /// 파일 다운로드 (진행률 포함)
    /// </summary>
    private async Task DownloadFileAsync(string downloadUrl, string filePath, long expectedSize, CancellationToken cancellationToken)
    {
        using var response = await _httpClient.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        var totalBytes = response.Content.Headers.ContentLength ?? expectedSize;
        var buffer = new byte[8192];
        var totalBytesRead = 0L;
        var lastProgressReport = 0;

        using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize: 4096, useAsync: true);

        while (true)
        {
            var bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken);
            if (bytesRead == 0)
                break;

            await fileStream.WriteAsync(buffer, 0, bytesRead, cancellationToken);
            totalBytesRead += bytesRead;

            // 진행률 보고 (1% 단위로)
            if (totalBytes > 0)
            {
                var progress = (int)((totalBytesRead * 100) / totalBytes);
                if (progress != lastProgressReport && progress % 1 == 0)
                {
                    lastProgressReport = progress;
                    ProgressChanged?.Invoke(this, progress);
                    _logger.LogDebug("다운로드 진행률: {Progress}% ({BytesRead}/{TotalBytes})", 
                        progress, totalBytesRead, totalBytes);
                }
            }
        }

        // 파일 크기 검증
        if (expectedSize > 0 && totalBytesRead != expectedSize)
        {
            throw new InvalidOperationException($"다운로드된 파일 크기가 예상과 다릅니다. 예상: {expectedSize}, 실제: {totalBytesRead}");
        }

        ProgressChanged?.Invoke(this, 100);
    }

    /// <summary>
    /// URL에서 파일명 추출
    /// </summary>
    private static string? ExtractFileNameFromUrl(string url)
    {
        try
        {
            var uri = new Uri(url);
            var fileName = Path.GetFileName(uri.LocalPath);
            return string.IsNullOrEmpty(fileName) ? null : fileName;
        }
        catch
        {
            return null;
        }
    }

    public void Dispose()
    {
        if (_ownsHttpClient && _httpClient != null)
        {
            _httpClient.Dispose();
        }
    }
} 