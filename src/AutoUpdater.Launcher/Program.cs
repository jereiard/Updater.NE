using AutoUpdater.Core.Models;
using AutoUpdater.Core.Utilities;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.IO.Compression;
using System.Text.Json;

namespace AutoUpdater.Launcher;

/// <summary>
/// 자기 업데이트를 위한 업데이트 런처
/// </summary>
public class Program
{
    private static ILogger<Program>? _logger;

    public static async Task<int> Main(string[] args)
    {
        // 로깅 설정
        using var loggerFactory = LoggerFactory.Create(builder =>
            builder.AddConsole().SetMinimumLevel(LogLevel.Information));
        _logger = loggerFactory.CreateLogger<Program>();

        try
        {
            _logger.LogInformation("AutoUpdater Launcher 시작");

            if (args.Length == 0)
            {
                _logger.LogError("사용법: AutoUpdater.Launcher.exe <launcher-info-file>");
                return 1;
            }

            var infoFilePath = args[0];
            if (!File.Exists(infoFilePath))
            {
                _logger.LogError("런처 정보 파일을 찾을 수 없습니다: {FilePath}", infoFilePath);
                return 1;
            }

            // 런처 정보 로드
            var launcherInfo = await LoadLauncherInfoAsync(infoFilePath);
            if (launcherInfo == null)
            {
                return 1;
            }

            _logger.LogInformation("업데이트 대상: {TargetPath}", launcherInfo.TargetExecutablePath);
            _logger.LogInformation("업데이트 파일: {UpdatePath}", launcherInfo.UpdateFilePath);

            // 업데이트 프로세스 실행
            var success = await PerformUpdateAsync(launcherInfo);

            // 임시 파일 정리
            try
            {
                File.Delete(infoFilePath);
                _logger.LogDebug("런처 정보 파일 삭제: {FilePath}", infoFilePath);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "런처 정보 파일 삭제 실패: {FilePath}", infoFilePath);
            }

            return success ? 0 : 1;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "업데이트 런처에서 예상치 못한 오류가 발생했습니다.");
            return 1;
        }
    }

    /// <summary>
    /// 런처 정보 파일 로드
    /// </summary>
    private static async Task<UpdateLauncherInfo?> LoadLauncherInfoAsync(string filePath)
    {
        try
        {
            var jsonContent = await File.ReadAllTextAsync(filePath);
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };

            var launcherInfo = JsonSerializer.Deserialize<UpdateLauncherInfo>(jsonContent, options);
            if (launcherInfo == null)
            {
                _logger?.LogError("런처 정보 파일이 비어있거나 잘못된 형식입니다.");
                return null;
            }

            return launcherInfo;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "런처 정보 파일 로드 실패: {FilePath}", filePath);
            return null;
        }
    }

    /// <summary>
    /// 업데이트 수행
    /// </summary>
    private static async Task<bool> PerformUpdateAsync(UpdateLauncherInfo launcherInfo)
    {
        try
        {
            // 1. 대상 프로세스 종료 대기
            _logger?.LogInformation("대상 프로세스 종료 대기 중... (PID: {ProcessId})", launcherInfo.ProcessId);
            var processExited = await WaitForProcessToExitAsync(launcherInfo.ProcessId, launcherInfo.WaitTimeoutSeconds);
            
            if (!processExited)
            {
                if (launcherInfo.AllowForceKill)
                {
                    _logger?.LogWarning("프로세스를 강제 종료합니다. (PID: {ProcessId})", launcherInfo.ProcessId);
                    await ForceKillProcessAsync(launcherInfo.ProcessId);
                }
                else
                {
                    _logger?.LogError("프로세스 종료 시간 초과. 업데이트를 취소합니다.");
                    return false;
                }
            }

            // 2. 백업 생성
            if (!string.IsNullOrEmpty(launcherInfo.BackupDirectory))
            {
                _logger?.LogInformation("백업 생성 중...");
                if (!await CreateBackupAsync(launcherInfo.TargetExecutablePath, launcherInfo.BackupDirectory))
                {
                    _logger?.LogWarning("백업 생성에 실패했지만 업데이트를 계속 진행합니다.");
                }
            }

            // 3. 업데이트 파일 설치
            _logger?.LogInformation("업데이트 설치 중...");
            var installSuccess = await InstallUpdateAsync(launcherInfo);
            
            if (!installSuccess)
            {
                _logger?.LogError("업데이트 설치 실패");
                
                // 백업에서 복원 시도
                if (!string.IsNullOrEmpty(launcherInfo.BackupDirectory))
                {
                    _logger?.LogInformation("백업에서 복원 시도 중...");
                    await RestoreFromBackupAsync(launcherInfo.BackupDirectory, launcherInfo.TargetExecutablePath);
                }
                
                return false;
            }

            // 4. 애플리케이션 재시작
            if (launcherInfo.RestartAfterUpdate)
            {
                _logger?.LogInformation("애플리케이션 재시작 중...");
                await RestartApplicationAsync(launcherInfo.TargetExecutablePath, launcherInfo.RestartArguments);
            }

            _logger?.LogInformation("업데이트가 성공적으로 완료되었습니다.");
            return true;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "업데이트 수행 중 오류 발생");
            return false;
        }
    }

    /// <summary>
    /// 프로세스 종료 대기
    /// </summary>
    private static async Task<bool> WaitForProcessToExitAsync(int processId, int timeoutSeconds)
    {
        try
        {
            var process = Process.GetProcessById(processId);
            var timeout = TimeSpan.FromSeconds(timeoutSeconds);
            var startTime = DateTime.Now;

            while (!process.HasExited && DateTime.Now - startTime < timeout)
            {
                await Task.Delay(500);
                try
                {
                    process.Refresh();
                }
                catch (InvalidOperationException)
                {
                    // 프로세스가 이미 종료됨
                    return true;
                }
            }

            return process.HasExited;
        }
        catch (ArgumentException)
        {
            // 프로세스가 존재하지 않음 (이미 종료됨)
            return true;
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "프로세스 종료 대기 중 오류 발생 (PID: {ProcessId})", processId);
            return false;
        }
    }

    /// <summary>
    /// 프로세스 강제 종료
    /// </summary>
    private static async Task ForceKillProcessAsync(int processId)
    {
        try
        {
            var process = Process.GetProcessById(processId);
            process.Kill();
            await process.WaitForExitAsync();
            _logger?.LogInformation("프로세스가 강제 종료되었습니다. (PID: {ProcessId})", processId);
        }
        catch (ArgumentException)
        {
            // 프로세스가 이미 존재하지 않음
            _logger?.LogDebug("프로세스가 이미 존재하지 않습니다. (PID: {ProcessId})", processId);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "프로세스 강제 종료 실패 (PID: {ProcessId})", processId);
        }
    }

    /// <summary>
    /// 백업 생성
    /// </summary>
    private static async Task<bool> CreateBackupAsync(string targetPath, string backupDirectory)
    {
        try
        {
            FileHelper.EnsureDirectoryExists(backupDirectory);

            if (File.Exists(targetPath))
            {
                var backupFileName = Path.GetFileName(targetPath);
                var backupFilePath = Path.Combine(backupDirectory, backupFileName);
                
                await Task.Run(() => File.Copy(targetPath, backupFilePath, overwrite: true));
                _logger?.LogInformation("백업 생성 완료: {BackupPath}", backupFilePath);
                return true;
            }
            else if (Directory.Exists(targetPath))
            {
                await Task.Run(() => FileHelper.CopyDirectory(targetPath, backupDirectory, overwrite: true));
                _logger?.LogInformation("디렉토리 백업 생성 완료: {BackupPath}", backupDirectory);
                return true;
            }

            _logger?.LogWarning("백업할 대상을 찾을 수 없습니다: {TargetPath}", targetPath);
            return false;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "백업 생성 실패: {TargetPath} -> {BackupPath}", targetPath, backupDirectory);
            return false;
        }
    }

    /// <summary>
    /// 백업에서 복원
    /// </summary>
    private static async Task<bool> RestoreFromBackupAsync(string backupDirectory, string targetPath)
    {
        try
        {
            if (!Directory.Exists(backupDirectory))
            {
                _logger?.LogWarning("백업 디렉토리가 존재하지 않습니다: {BackupDirectory}", backupDirectory);
                return false;
            }

            var targetFileName = Path.GetFileName(targetPath);
            var backupFilePath = Path.Combine(backupDirectory, targetFileName);

            if (File.Exists(backupFilePath))
            {
                await Task.Run(() => File.Copy(backupFilePath, targetPath, overwrite: true));
                _logger?.LogInformation("파일 복원 완료: {BackupPath} -> {TargetPath}", backupFilePath, targetPath);
                return true;
            }
            else if (Directory.Exists(backupDirectory))
            {
                var targetDirectory = Path.GetDirectoryName(targetPath);
                if (!string.IsNullOrEmpty(targetDirectory))
                {
                    await Task.Run(() => FileHelper.CopyDirectory(backupDirectory, targetDirectory, overwrite: true));
                    _logger?.LogInformation("디렉토리 복원 완료: {BackupPath} -> {TargetPath}", backupDirectory, targetDirectory);
                    return true;
                }
            }

            _logger?.LogWarning("복원할 백업을 찾을 수 없습니다: {BackupDirectory}", backupDirectory);
            return false;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "백업 복원 실패: {BackupDirectory} -> {TargetPath}", backupDirectory, targetPath);
            return false;
        }
    }

    /// <summary>
    /// 업데이트 설치
    /// </summary>
    private static async Task<bool> InstallUpdateAsync(UpdateLauncherInfo launcherInfo)
    {
        try
        {
            var updateFileType = launcherInfo.UpdateFileType.ToLowerInvariant();
            
            switch (updateFileType)
            {
                case ".zip":
                    return await InstallZipUpdateAsync(launcherInfo);
                case ".exe":
                    return await InstallExeUpdateAsync(launcherInfo);
                case ".msi":
                    return await InstallMsiUpdateAsync(launcherInfo);
                default:
                    return await InstallGenericUpdateAsync(launcherInfo);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "업데이트 설치 중 오류 발생");
            return false;
        }
    }

    /// <summary>
    /// ZIP 업데이트 설치
    /// </summary>
    private static async Task<bool> InstallZipUpdateAsync(UpdateLauncherInfo launcherInfo)
    {
        try
        {
            var targetDirectory = Path.GetDirectoryName(launcherInfo.TargetExecutablePath);
            if (string.IsNullOrEmpty(targetDirectory))
            {
                _logger?.LogError("대상 디렉토리를 확인할 수 없습니다.");
                return false;
            }

            var tempExtractPath = Path.Combine(Path.GetTempPath(), $"AutoUpdater_Extract_{Guid.NewGuid():N}");
            
            try
            {
                // ZIP 압축 해제
                await Task.Run(() => ZipFile.ExtractToDirectory(launcherInfo.UpdateFilePath, tempExtractPath));
                _logger?.LogInformation("ZIP 파일 압축 해제 완료: {ExtractPath}", tempExtractPath);

                // 파일 복사
                await Task.Run(() => FileHelper.CopyDirectory(tempExtractPath, targetDirectory, overwrite: true));
                _logger?.LogInformation("파일 복사 완료: {TempPath} -> {TargetPath}", tempExtractPath, targetDirectory);

                return true;
            }
            finally
            {
                // 임시 디렉토리 정리
                try
                {
                    if (Directory.Exists(tempExtractPath))
                    {
                        Directory.Delete(tempExtractPath, recursive: true);
                    }
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "임시 디렉토리 정리 실패: {TempPath}", tempExtractPath);
                }
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "ZIP 업데이트 설치 실패");
            return false;
        }
    }

    /// <summary>
    /// EXE 업데이트 설치
    /// </summary>
    private static async Task<bool> InstallExeUpdateAsync(UpdateLauncherInfo launcherInfo)
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = launcherInfo.UpdateFilePath,
                Arguments = "/S", // 사일런트 설치
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var process = Process.Start(startInfo);
            if (process == null)
            {
                _logger?.LogError("EXE 설치 프로세스를 시작할 수 없습니다.");
                return false;
            }

            await process.WaitForExitAsync();
            
            if (process.ExitCode == 0)
            {
                _logger?.LogInformation("EXE 설치 완료");
                return true;
            }
            else
            {
                var error = await process.StandardError.ReadToEndAsync();
                _logger?.LogError("EXE 설치 실패 (코드: {ExitCode}): {Error}", process.ExitCode, error);
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "EXE 업데이트 설치 실패");
            return false;
        }
    }

    /// <summary>
    /// MSI 업데이트 설치
    /// </summary>
    private static async Task<bool> InstallMsiUpdateAsync(UpdateLauncherInfo launcherInfo)
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "msiexec.exe",
                Arguments = $"/i \"{launcherInfo.UpdateFilePath}\" /quiet /norestart",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var process = Process.Start(startInfo);
            if (process == null)
            {
                _logger?.LogError("MSI 설치 프로세스를 시작할 수 없습니다.");
                return false;
            }

            await process.WaitForExitAsync();
            
            if (process.ExitCode == 0)
            {
                _logger?.LogInformation("MSI 설치 완료");
                return true;
            }
            else
            {
                var error = await process.StandardError.ReadToEndAsync();
                _logger?.LogError("MSI 설치 실패 (코드: {ExitCode}): {Error}", process.ExitCode, error);
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "MSI 업데이트 설치 실패");
            return false;
        }
    }

    /// <summary>
    /// 일반 파일 업데이트 설치 (단순 파일 복사)
    /// </summary>
    private static async Task<bool> InstallGenericUpdateAsync(UpdateLauncherInfo launcherInfo)
    {
        try
        {
            await Task.Run(() => File.Copy(launcherInfo.UpdateFilePath, launcherInfo.TargetExecutablePath, overwrite: true));
            _logger?.LogInformation("파일 복사 완료: {UpdatePath} -> {TargetPath}", 
                launcherInfo.UpdateFilePath, launcherInfo.TargetExecutablePath);
            return true;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "일반 파일 업데이트 설치 실패");
            return false;
        }
    }

    /// <summary>
    /// 애플리케이션 재시작
    /// </summary>
    private static async Task RestartApplicationAsync(string executablePath, string arguments)
    {
        try
        {
            // 약간의 지연 후 재시작 (파일 잠금 해제 대기)
            await Task.Delay(2000);

            var startInfo = new ProcessStartInfo
            {
                FileName = executablePath,
                Arguments = arguments,
                UseShellExecute = true,
                CreateNoWindow = false
            };

            Process.Start(startInfo);
            _logger?.LogInformation("애플리케이션 재시작: {ExecutablePath}", executablePath);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "애플리케이션 재시작 실패: {ExecutablePath}", executablePath);
        }
    }
}
