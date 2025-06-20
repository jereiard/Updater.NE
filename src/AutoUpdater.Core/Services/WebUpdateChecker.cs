using System.Net;
using System.Text.Json;
using AutoUpdater.Core.Configuration;
using AutoUpdater.Core.Interfaces;
using AutoUpdater.Core.Models;
using AutoUpdater.Core.Utilities;
using Microsoft.Extensions.Logging;

namespace AutoUpdater.Core.Services;

/// <summary>
/// 웹 기반 업데이트 확인 구현 클래스
/// </summary>
public class WebUpdateChecker : IUpdateChecker, IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly UpdaterConfiguration _configuration;
    private readonly ILogger<WebUpdateChecker> _logger;
    private readonly bool _ownsHttpClient;

    /// <summary>
    /// 생성자 (HttpClient를 직접 받는 경우)
    /// </summary>
    public WebUpdateChecker(
        HttpClient httpClient,
        UpdaterConfiguration configuration,
        ILogger<WebUpdateChecker> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _ownsHttpClient = false;

        ConfigureHttpClient();
    }

    /// <summary>
    /// 생성자 (설정으로부터 HttpClient 생성)
    /// </summary>
    public WebUpdateChecker(
        UpdaterConfiguration configuration,
        ILogger<WebUpdateChecker> logger)
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
    /// 업데이트 확인
    /// </summary>
    public async Task<UpdateResult> CheckForUpdateAsync(UpdateRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("업데이트 확인 시작: {ApplicationId} v{CurrentVersion}", 
                request.ApplicationId, request.CurrentVersion);

            var updateInfo = await GetUpdateInfoAsync(request, cancellationToken);
            
            if (updateInfo == null)
            {
                _logger.LogInformation("업데이트가 필요하지 않습니다.");
                return UpdateResult.Success(UpdateResultType.NoUpdateAvailable);
            }

            // 버전 비교
            if (!VersionHelper.IsNewerVersion(request.CurrentVersion, updateInfo.Version))
            {
                _logger.LogInformation("현재 버전이 최신입니다: {CurrentVersion}", request.CurrentVersion);
                return UpdateResult.Success(UpdateResultType.NoUpdateAvailable);
            }

            // 최소 버전 요구사항 확인
            if (!string.IsNullOrEmpty(updateInfo.MinimumVersion) &&
                !VersionHelper.MeetsMinimumVersion(request.CurrentVersion, updateInfo.MinimumVersion))
            {
                _logger.LogWarning("현재 버전이 최소 요구 버전을 만족하지 않습니다. 현재: {CurrentVersion}, 최소: {MinimumVersion}",
                    request.CurrentVersion, updateInfo.MinimumVersion);
            }

            _logger.LogInformation("새로운 업데이트를 발견했습니다: v{NewVersion}", updateInfo.Version);
            return UpdateResult.Success(UpdateResultType.UpdateAvailable, updateInfo);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "업데이트 확인 중 오류가 발생했습니다.");
            return UpdateResult.Failure($"업데이트 확인 실패: {ex.Message}");
        }
    }

    /// <summary>
    /// 업데이트 정보 가져오기
    /// </summary>
    public async Task<UpdateInfo?> GetUpdateInfoAsync(UpdateRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            var requestUrl = BuildRequestUrl(request);
            _logger.LogDebug("업데이트 정보 요청: {RequestUrl}", requestUrl);

            using var response = await _httpClient.GetAsync(requestUrl, cancellationToken);
            
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                _logger.LogInformation("업데이트 정보를 찾을 수 없습니다.");
                return null;
            }

            response.EnsureSuccessStatusCode();

            var jsonContent = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogDebug("서버 응답: {Response}", jsonContent);

            var updateInfo = JsonSerializer.Deserialize<UpdateInfo>(jsonContent, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (updateInfo == null)
            {
                _logger.LogWarning("업데이트 정보를 파싱할 수 없습니다.");
                return null;
            }

            // 기본값 설정
            if (updateInfo.ReleaseDate == default)
                updateInfo.ReleaseDate = DateTime.UtcNow;

            return updateInfo;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP 요청 중 오류가 발생했습니다.");
            throw;
        }
        catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
        {
            _logger.LogError(ex, "요청 시간이 초과되었습니다.");
            throw new TimeoutException("업데이트 확인 요청 시간 초과", ex);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "JSON 파싱 중 오류가 발생했습니다.");
            throw new InvalidOperationException("서버 응답을 파싱할 수 없습니다.", ex);
        }
    }

    /// <summary>
    /// HTTP 클라이언트 설정
    /// </summary>
    private void ConfigureHttpClient()
    {
        _httpClient.Timeout = TimeSpan.FromSeconds(_configuration.TimeoutSeconds);
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(_configuration.UserAgent);

        // API 키 설정
        if (!string.IsNullOrEmpty(_configuration.ApiKey))
        {
            _httpClient.DefaultRequestHeaders.Add("X-API-Key", _configuration.ApiKey);
        }

        // 추가 헤더 설정
        if (_configuration.AdditionalHeaders != null)
        {
            foreach (var header in _configuration.AdditionalHeaders)
            {
                _httpClient.DefaultRequestHeaders.Add(header.Key, header.Value);
            }
        }
    }

    /// <summary>
    /// 요청 URL 구성
    /// </summary>
    private string BuildRequestUrl(UpdateRequest request)
    {
        var baseUrl = _configuration.ServerUrl.TrimEnd('/');
        var url = $"{baseUrl}/api/updates/{request.ApplicationId}";

        var queryParams = new List<string>
        {
            $"currentVersion={Uri.EscapeDataString(request.CurrentVersion)}",
            $"platform={Uri.EscapeDataString(request.Platform)}",
            $"architecture={Uri.EscapeDataString(request.Architecture)}"
        };

        if (!string.IsNullOrEmpty(request.Language))
            queryParams.Add($"language={Uri.EscapeDataString(request.Language)}");

        if (!string.IsNullOrEmpty(request.ClientId))
            queryParams.Add($"clientId={Uri.EscapeDataString(request.ClientId)}");

        return $"{url}?{string.Join("&", queryParams)}";
    }

    public void Dispose()
    {
        if (_ownsHttpClient)
        {
            _httpClient.Dispose();
        }
    }
} 