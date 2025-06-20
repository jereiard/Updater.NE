using AutoUpdater.Core.Configuration;
using AutoUpdater.Core.Interfaces;
using AutoUpdater.Core.Models;
using AutoUpdater.Core.Utilities;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Text.Json;

namespace AutoUpdater.Core.Services;

/// <summary>
/// 메인 자동 업데이터 서비스 클래스
/// </summary>
public class AutoUpdaterService : IAutoUpdater, IDisposable
{
    private readonly IUpdateChecker _updateChecker;
    private readonly IUpdateDownloader _updateDownloader;
    private readonly IUpdateInstaller _updateInstaller;
    private readonly UpdaterConfiguration _configuration;
    private readonly ILogger<AutoUpdaterService> _logger;
    private readonly Timer? _checkTimer;

    private bool _disposed;

    /// <summary>
    /// 업데이트 상태 변경 이벤트
    /// </summary>
    public event EventHandler<UpdateResult>? UpdateStatusChanged;

    public AutoUpdaterService(
        IUpdateChecker updateChecker,
        IUpdateDownloader updateDownloader,
        IUpdateInstaller updateInstaller,
        UpdaterConfiguration configuration,
        ILogger<AutoUpdaterService> logger)
    {
        _updateChecker = updateChecker ?? throw new ArgumentNullException(nameof(updateChecker));
        _updateDownloader = updateDownloader ?? throw new ArgumentNullException(nameof(updateDownloader));
        _updateInstaller = updateInstaller ?? throw new ArgumentNullException(nameof(updateInstaller));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // 이벤트 구독
        _updateDownloader.ProgressChanged += OnDownloadProgressChanged;
        _updateInstaller.ProgressChanged += OnInstallProgressChanged;

        // 자동 확인 타이머 설정
        if (_configuration.CheckIntervalMinutes > 0)
        {
            var interval = TimeSpan.FromMinutes(_configuration.CheckIntervalMinutes);
            _checkTimer = new Timer(OnTimerElapsed, null, interval, interval);
            _logger.LogInformation("자동 업데이트 확인이 {Interval}분 간격으로 설정되었습니다.", _configuration.CheckIntervalMinutes);
        }
    }

    /// <summary>
    /// 업데이트 확인 및 실행
    /// </summary>
    public async Task<UpdateResult> CheckAndUpdateAsync(UpdateRequest request, bool autoInstall = false, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("업데이트 확인 및 실행 시작");

            // 1. 업데이트 확인
            var checkResult = await CheckForUpdateAsync(request, cancellationToken);
            if (!checkResult.IsSuccess || checkResult.ResultType != UpdateResultType.UpdateAvailable)
            {
                return checkResult;
            }

            var updateInfo = checkResult.UpdateInfo!;

            // 2. 자동 다운로드 또는 사용자 설정에 따른 다운로드
            if (_configuration.AutoDownload || autoInstall)
            {
                var downloadResult = await DownloadUpdateAsync(updateInfo, cancellationToken);
                if (!downloadResult.IsSuccess)
                {
                    return downloadResult;
                }

                var downloadedFilePath = downloadResult.AdditionalData?["FilePath"]?.ToString();
                if (string.IsNullOrEmpty(downloadedFilePath))
                {
                    return UpdateResult.Failure("다운로드된 파일 경로를 찾을 수 없습니다.");
                }

                // 3. 자동 설치
                if (_configuration.AutoInstall || autoInstall)
                {
                    return await InstallUpdateAsync(downloadedFilePath, updateInfo, cancellationToken);
                }
                else
                {
                    return UpdateResult.Success(UpdateResultType.Downloaded, updateInfo);
                }
            }

            return UpdateResult.Success(UpdateResultType.UpdateAvailable, updateInfo);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "업데이트 확인 및 실행 중 오류가 발생했습니다.");
            return UpdateResult.Failure($"업데이트 프로세스 실패: {ex.Message}");
        }
    }

    /// <summary>
    /// 업데이트 확인만 수행
    /// </summary>
    public async Task<UpdateResult> CheckForUpdateAsync(UpdateRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _updateChecker.CheckForUpdateAsync(request, cancellationToken);
            UpdateStatusChanged?.Invoke(this, result);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "업데이트 확인 중 오류가 발생했습니다.");
            var errorResult = UpdateResult.Failure($"업데이트 확인 실패: {ex.Message}");
            UpdateStatusChanged?.Invoke(this, errorResult);
            return errorResult;
        }
    }

    /// <summary>
    /// 업데이트 다운로드
    /// </summary>
    public async Task<UpdateResult> DownloadUpdateAsync(UpdateInfo updateInfo, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("업데이트 다운로드 시작: v{Version}", updateInfo.Version);

            var downloadingResult = UpdateResult.Success(UpdateResultType.Downloading, updateInfo);
            UpdateStatusChanged?.Invoke(this, downloadingResult);

            // 다운로드 디렉토리 확인 및 생성
            FileHelper.EnsureDirectoryExists(_configuration.DownloadDirectory);

            var downloadedFilePath = await _updateDownloader.DownloadUpdateAsync(
                updateInfo, 
                _configuration.DownloadDirectory, 
                cancellationToken);

            var result = UpdateResult.Success(UpdateResultType.Downloaded, updateInfo);
            result.AdditionalData = new Dictionary<string, object>
            {
                ["FilePath"] = downloadedFilePath
            };

            UpdateStatusChanged?.Invoke(this, result);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "업데이트 다운로드 중 오류가 발생했습니다.");
            var errorResult = UpdateResult.Failure($"다운로드 실패: {ex.Message}");
            UpdateStatusChanged?.Invoke(this, errorResult);
            return errorResult;
        }
    }

    /// <summary>
    /// 업데이트 설치
    /// </summary>
    public async Task<UpdateResult> InstallUpdateAsync(string updateFilePath, UpdateInfo updateInfo, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("업데이트 설치 시작: {FilePath}", updateFilePath);

            var installingResult = UpdateResult.Success(UpdateResultType.Installing, updateInfo);
            UpdateStatusChanged?.Invoke(this, installingResult);

            // 백업 생성 (선택적)
            var currentAppPath = AppDomain.CurrentDomain.BaseDirectory;
            var backupPath = Path.Combine(_configuration.BackupDirectory, $"backup_{DateTime.Now:yyyyMMdd_HHmmss}");
            
            if (Directory.Exists(currentAppPath))
            {
                _logger.LogInformation("현재 애플리케이션 백업 생성...");
                FileHelper.EnsureDirectoryExists(_configuration.BackupDirectory);
                await _updateInstaller.CreateBackupAsync(currentAppPath, backupPath);
            }

            var result = await _updateInstaller.InstallUpdateAsync(updateFilePath, updateInfo, cancellationToken);
            UpdateStatusChanged?.Invoke(this, result);

            // 설치 후 임시 파일 정리
            try
            {
                if (File.Exists(updateFilePath))
                {
                    File.Delete(updateFilePath);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "임시 파일 삭제 실패: {FilePath}", updateFilePath);
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "업데이트 설치 중 오류가 발생했습니다.");
            var errorResult = UpdateResult.Failure($"설치 실패: {ex.Message}");
            UpdateStatusChanged?.Invoke(this, errorResult);
            return errorResult;
        }
    }

    /// <summary>
    /// 다운로드 진행률 변경 이벤트 핸들러
    /// </summary>
    private void OnDownloadProgressChanged(object? sender, int progress)
    {
        var result = UpdateResult.Success(UpdateResultType.Downloading, null, progress);
        UpdateStatusChanged?.Invoke(this, result);
    }

    /// <summary>
    /// 설치 진행률 변경 이벤트 핸들러
    /// </summary>
    private void OnInstallProgressChanged(object? sender, int progress)
    {
        var result = UpdateResult.Success(UpdateResultType.Installing, null, progress);
        UpdateStatusChanged?.Invoke(this, result);
    }

    /// <summary>
    /// 타이머 이벤트 핸들러 (자동 업데이트 확인)
    /// </summary>
    private async void OnTimerElapsed(object? state)
    {
        try
        {
            _logger.LogDebug("자동 업데이트 확인 실행");

            var request = CreateDefaultUpdateRequest();
            var result = await CheckForUpdateAsync(request);

            if (result.IsSuccess && result.ResultType == UpdateResultType.UpdateAvailable)
            {
                _logger.LogInformation("자동 업데이트 확인에서 새로운 업데이트를 발견했습니다.");
                
                if (_configuration.AutoDownload)
                {
                    await DownloadUpdateAsync(result.UpdateInfo!, CancellationToken.None);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "자동 업데이트 확인 중 오류가 발생했습니다.");
        }
    }

    /// <summary>
    /// 자기 업데이트 시작 (현재 실행 중인 애플리케이션 업데이트)
    /// </summary>
    public async Task<UpdateResult> InitiateSelfUpdateAsync(UpdateInfo updateInfo, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("자기 업데이트 시작: v{Version}", updateInfo.Version);

            // 1. 업데이트 파일 다운로드
            var downloadResult = await DownloadUpdateAsync(updateInfo, cancellationToken);
            if (!downloadResult.IsSuccess)
            {
                return downloadResult;
            }

            var downloadedFilePath = downloadResult.AdditionalData?["FilePath"]?.ToString();
            if (string.IsNullOrEmpty(downloadedFilePath))
            {
                return UpdateResult.Failure("다운로드된 파일 경로를 찾을 수 없습니다.");
            }

            // 2. 현재 실행 파일 경로 확인
            var currentProcess = Process.GetCurrentProcess();
            var currentExecutablePath = currentProcess.MainModule?.FileName;
            if (string.IsNullOrEmpty(currentExecutablePath))
            {
                return UpdateResult.Failure("현재 실행 파일 경로를 확인할 수 없습니다.");
            }

            // 3. 업데이트 런처 경로 확인
            var currentDirectory = Path.GetDirectoryName(currentExecutablePath) ?? AppDomain.CurrentDomain.BaseDirectory;
            var launcherPath = Path.Combine(currentDirectory, "AutoUpdater.Launcher.exe");
            
            if (!File.Exists(launcherPath))
            {
                return UpdateResult.Failure($"업데이트 런처를 찾을 수 없습니다: {launcherPath}");
            }

            // 4. 백업 디렉토리 설정
            var backupDirectory = Path.Combine(_configuration.BackupDirectory, $"backup_{DateTime.Now:yyyyMMdd_HHmmss}");
            FileHelper.EnsureDirectoryExists(_configuration.BackupDirectory);

            // 5. 런처 정보 생성
            var launcherInfo = new UpdateLauncherInfo
            {
                UpdateFilePath = downloadedFilePath,
                TargetExecutablePath = currentExecutablePath,
                ProcessId = currentProcess.Id,
                RestartAfterUpdate = true,
                UpdateFileType = Path.GetExtension(downloadedFilePath).ToLowerInvariant(),
                BackupDirectory = backupDirectory,
                WaitTimeoutSeconds = _configuration.ProcessTerminationTimeoutSeconds,
                AllowForceKill = _configuration.AllowForceProcessTermination,
                RestartArguments = string.Empty
            };

            // 6. 런처 정보를 임시 파일로 저장
            var tempInfoFile = Path.Combine(Path.GetTempPath(), $"AutoUpdater_LauncherInfo_{Guid.NewGuid():N}.json");
            var jsonOptions = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };
            
            await File.WriteAllTextAsync(tempInfoFile, JsonSerializer.Serialize(launcherInfo, jsonOptions), cancellationToken);

            // 7. 업데이트 런처 실행
            var launcherStartInfo = new ProcessStartInfo
            {
                FileName = launcherPath,
                Arguments = $"\"{tempInfoFile}\"",
                UseShellExecute = false,
                CreateNoWindow = true
            };

            _logger.LogInformation("업데이트 런처 실행: {LauncherPath}", launcherPath);
            
            var launcherProcess = Process.Start(launcherStartInfo);
            if (launcherProcess == null)
            {
                // 임시 파일 정리
                try { File.Delete(tempInfoFile); } catch { }
                return UpdateResult.Failure("업데이트 런처를 시작할 수 없습니다.");
            }

            _logger.LogInformation("자기 업데이트를 위해 현재 프로세스를 종료합니다.");
            
            // 8. 현재 프로세스 종료 (런처가 업데이트 수행)
            // 주의: 이 지점 이후로는 코드가 실행되지 않습니다.
            Environment.Exit(0);

            // 이 라인은 실행되지 않지만 컴파일러 경고 방지용
            return UpdateResult.Success(UpdateResultType.Completed, updateInfo);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "자기 업데이트 시작 중 오류가 발생했습니다.");
            return UpdateResult.Failure($"자기 업데이트 실패: {ex.Message}");
        }
    }

    /// <summary>
    /// 전체 업데이트 프로세스 실행 (확인 + 다운로드 + 설치)
    /// </summary>
    public async Task<UpdateResult> PerformUpdateAsync(UpdateRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("전체 업데이트 프로세스 시작");

            // 1. 업데이트 확인
            var checkResult = await CheckForUpdateAsync(request, cancellationToken);
            if (!checkResult.IsSuccess)
            {
                return checkResult;
            }

            if (checkResult.ResultType == UpdateResultType.NoUpdateAvailable)
            {
                return checkResult;
            }

            var updateInfo = checkResult.UpdateInfo!;

            // 2. 다운로드
            var downloadResult = await DownloadUpdateAsync(updateInfo, cancellationToken);
            if (!downloadResult.IsSuccess)
            {
                return downloadResult;
            }

            var downloadedFilePath = downloadResult.AdditionalData?["FilePath"]?.ToString();
            if (string.IsNullOrEmpty(downloadedFilePath))
            {
                return UpdateResult.Failure("다운로드된 파일 경로를 찾을 수 없습니다.");
            }

            // 3. 설치
            return await InstallUpdateAsync(downloadedFilePath, updateInfo, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "전체 업데이트 프로세스 중 오류가 발생했습니다.");
            return UpdateResult.Failure($"업데이트 프로세스 실패: {ex.Message}");
        }
    }

    /// <summary>
    /// 자동 업데이트 확인 시작
    /// </summary>
    public void StartAutoUpdateCheck()
    {
        if (_checkTimer != null)
        {
            var interval = TimeSpan.FromMinutes(_configuration.CheckIntervalMinutes);
            _checkTimer.Change(TimeSpan.Zero, interval);
            _logger.LogInformation("자동 업데이트 확인이 시작되었습니다.");
        }
        else
        {
            _logger.LogWarning("자동 업데이트 확인 타이머가 설정되지 않았습니다.");
        }
    }

    /// <summary>
    /// 자동 업데이트 확인 중지
    /// </summary>
    public void StopAutoUpdateCheck()
    {
        if (_checkTimer != null)
        {
            _checkTimer.Change(Timeout.Infinite, Timeout.Infinite);
            _logger.LogInformation("자동 업데이트 확인이 중지되었습니다.");
        }
    }

    /// <summary>
    /// 기본 업데이트 요청 생성
    /// </summary>
    private UpdateRequest CreateDefaultUpdateRequest()
    {
        return new UpdateRequest
        {
            ApplicationId = _configuration.ApplicationId,
            CurrentVersion = _configuration.CurrentVersion,
            Platform = Environment.OSVersion.Platform.ToString(),
            Architecture = Environment.Is64BitProcess ? "x64" : "x86",
            Language = System.Globalization.CultureInfo.CurrentCulture.Name,
            ClientId = Environment.MachineName
        };
    }

    /// <summary>
    /// 리소스 해제
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;

        _checkTimer?.Dispose();
        
        if (_updateDownloader is IDisposable disposableDownloader)
            disposableDownloader.Dispose();
            
        if (_updateInstaller is IDisposable disposableInstaller)
            disposableInstaller.Dispose();

        _disposed = true;
        GC.SuppressFinalize(this);
    }
} 