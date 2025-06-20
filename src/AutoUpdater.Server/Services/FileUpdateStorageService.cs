using AutoUpdater.Core.Models;
using AutoUpdater.Core.Utilities;
using System.Text.Json;

namespace AutoUpdater.Server.Services;

/// <summary>
/// 파일 기반 업데이트 정보 스토리지 서비스
/// </summary>
public interface IUpdateStorageService
{
    Task<UpdateInfo?> GetUpdateInfoAsync(string applicationId, string currentVersion, string platform, string architecture);
    Task SaveUpdateInfoAsync(string applicationId, UpdateInfo updateInfo);
    Task<bool> DeleteUpdateInfoAsync(string applicationId, string version);
    Task<List<UpdateInfo>> GetAllUpdateInfoAsync(string applicationId);
    Task<List<string>> GetApplicationIdsAsync();
}

public class FileUpdateStorageService : IUpdateStorageService
{
    private readonly string _dataDirectory;
    private readonly ILogger<FileUpdateStorageService> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    public FileUpdateStorageService(ILogger<FileUpdateStorageService> logger, IConfiguration configuration)
    {
        _logger = logger;
        _dataDirectory = configuration.GetValue<string>("UpdateStorage:DataDirectory") ?? "Data/Updates";
        
        // 데이터 디렉토리 생성
        Directory.CreateDirectory(_dataDirectory);
        
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true,
            PropertyNameCaseInsensitive = true
        };

        _logger.LogInformation("파일 기반 스토리지 초기화됨: {DataDirectory}", _dataDirectory);
    }

    public async Task<UpdateInfo?> GetUpdateInfoAsync(string applicationId, string currentVersion, string platform, string architecture)
    {
        try
        {
            var appDirectory = Path.Combine(_dataDirectory, applicationId);
            if (!Directory.Exists(appDirectory))
            {
                _logger.LogDebug("애플리케이션 디렉토리가 존재하지 않음: {ApplicationId}", applicationId);
                return null;
            }

            // 모든 업데이트 정보 파일 조회
            var updateFiles = Directory.GetFiles(appDirectory, "*.json");
            UpdateInfo? latestUpdate = null;
            string latestVersion = currentVersion;

            foreach (var file in updateFiles)
            {
                try
                {
                    var json = await File.ReadAllTextAsync(file);
                    var updateInfo = JsonSerializer.Deserialize<UpdateInfo>(json, _jsonOptions);
                    
                    if (updateInfo == null) continue;

                    // 플랫폼 및 아키텍처 필터링
                    if (!IsCompatiblePlatform(updateInfo, platform, architecture))
                        continue;

                    // 최소 버전 요구사항 확인
                    if (!string.IsNullOrEmpty(updateInfo.MinimumVersion) && 
                        !VersionHelper.MeetsMinimumVersion(currentVersion, updateInfo.MinimumVersion))
                        continue;

                    // 더 새로운 버전인지 확인
                    if (VersionHelper.IsNewerVersion(latestVersion, updateInfo.Version))
                    {
                        latestUpdate = updateInfo;
                        latestVersion = updateInfo.Version;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "업데이트 파일 읽기 실패: {File}", file);
                }
            }

            if (latestUpdate != null)
            {
                _logger.LogInformation("업데이트 발견: {ApplicationId} v{CurrentVersion} -> v{NewVersion}", 
                    applicationId, currentVersion, latestUpdate.Version);
            }

            return latestUpdate;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "업데이트 정보 조회 실패: {ApplicationId}", applicationId);
            return null;
        }
    }

    public async Task SaveUpdateInfoAsync(string applicationId, UpdateInfo updateInfo)
    {
        try
        {
            var appDirectory = Path.Combine(_dataDirectory, applicationId);
            Directory.CreateDirectory(appDirectory);

            var fileName = $"{updateInfo.Version}.json";
            var filePath = Path.Combine(appDirectory, fileName);

            var json = JsonSerializer.Serialize(updateInfo, _jsonOptions);
            await File.WriteAllTextAsync(filePath, json);

            _logger.LogInformation("업데이트 정보 저장됨: {ApplicationId} v{Version} -> {FilePath}", 
                applicationId, updateInfo.Version, filePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "업데이트 정보 저장 실패: {ApplicationId} v{Version}", 
                applicationId, updateInfo.Version);
            throw;
        }
    }

    public async Task<bool> DeleteUpdateInfoAsync(string applicationId, string version)
    {
        try
        {
            var appDirectory = Path.Combine(_dataDirectory, applicationId);
            var fileName = $"{version}.json";
            var filePath = Path.Combine(appDirectory, fileName);

            if (!File.Exists(filePath))
            {
                _logger.LogDebug("삭제할 파일이 존재하지 않음: {FilePath}", filePath);
                return false;
            }

            await Task.Run(() => File.Delete(filePath));
            _logger.LogInformation("업데이트 정보 삭제됨: {ApplicationId} v{Version}", applicationId, version);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "업데이트 정보 삭제 실패: {ApplicationId} v{Version}", applicationId, version);
            throw;
        }
    }

    public async Task<List<UpdateInfo>> GetAllUpdateInfoAsync(string applicationId)
    {
        try
        {
            var appDirectory = Path.Combine(_dataDirectory, applicationId);
            if (!Directory.Exists(appDirectory))
            {
                return new List<UpdateInfo>();
            }

            var updateInfoList = new List<UpdateInfo>();
            var updateFiles = Directory.GetFiles(appDirectory, "*.json");

            foreach (var file in updateFiles)
            {
                try
                {
                    var json = await File.ReadAllTextAsync(file);
                    var updateInfo = JsonSerializer.Deserialize<UpdateInfo>(json, _jsonOptions);
                    
                    if (updateInfo != null)
                    {
                        updateInfoList.Add(updateInfo);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "업데이트 파일 읽기 실패: {File}", file);
                }
            }

            // 버전 순으로 정렬 (최신 버전이 먼저)
            updateInfoList.Sort((a, b) => VersionHelper.CompareVersions(b.Version, a.Version));

            _logger.LogDebug("업데이트 정보 목록 조회됨: {ApplicationId} ({Count}개)", applicationId, updateInfoList.Count);
            return updateInfoList;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "업데이트 정보 목록 조회 실패: {ApplicationId}", applicationId);
            return new List<UpdateInfo>();
        }
    }

    public async Task<List<string>> GetApplicationIdsAsync()
    {
        try
        {
            if (!Directory.Exists(_dataDirectory))
            {
                return new List<string>();
            }

            var directories = await Task.Run(() => Directory.GetDirectories(_dataDirectory));
            var applicationIds = directories.Select(Path.GetFileName).Where(name => !string.IsNullOrEmpty(name)).ToList();

            _logger.LogDebug("애플리케이션 ID 목록 조회됨: {Count}개", applicationIds.Count);
            return applicationIds!;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "애플리케이션 ID 목록 조회 실패");
            return new List<string>();
        }
    }

    private bool IsCompatiblePlatform(UpdateInfo updateInfo, string platform, string architecture)
    {
        // 메타데이터에서 플랫폼 및 아키텍처 정보 확인
        if (updateInfo.Metadata != null)
        {
            if (updateInfo.Metadata.TryGetValue("platform", out var updatePlatform))
            {
                if (!string.Equals(updatePlatform?.ToString(), platform, StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(updatePlatform?.ToString(), "Any", StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
            }

            if (updateInfo.Metadata.TryGetValue("architecture", out var updateArchitecture))
            {
                if (!string.Equals(updateArchitecture?.ToString(), architecture, StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(updateArchitecture?.ToString(), "Any", StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
            }
        }

        return true;
    }
} 