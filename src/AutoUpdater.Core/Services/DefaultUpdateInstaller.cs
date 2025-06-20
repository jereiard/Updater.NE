using System.Diagnostics;
using AutoUpdater.Core.Interfaces;
using AutoUpdater.Core.Models;
using AutoUpdater.Core.Utilities;
using Microsoft.Extensions.Logging;

namespace AutoUpdater.Core.Services;

/// <summary>
/// 기본 업데이트 설치 구현 클래스
/// </summary>
public class DefaultUpdateInstaller : IUpdateInstaller
{
  private readonly ILogger<DefaultUpdateInstaller> _logger;

  /// <summary>
  /// 설치 진행률 이벤트
  /// </summary>
  public event EventHandler<int>? ProgressChanged;

  public DefaultUpdateInstaller(ILogger<DefaultUpdateInstaller> logger)
  {
    _logger = logger ?? throw new ArgumentNullException(nameof(logger));
  }

  /// <summary>
  /// 업데이트 설치
  /// </summary>
  public async Task<UpdateResult> InstallUpdateAsync(string updateFilePath, UpdateInfo updateInfo, CancellationToken cancellationToken = default)
  {
    ArgumentException.ThrowIfNullOrEmpty(updateFilePath);
    ArgumentNullException.ThrowIfNull(updateInfo);

    if (!File.Exists(updateFilePath))
    {
      return UpdateResult.Failure($"업데이트 파일을 찾을 수 없습니다: {updateFilePath}");
    }

    _logger.LogInformation("업데이트 설치 시작: {UpdateFile}", updateFilePath);

    try
    {
      ProgressChanged?.Invoke(this, 0);

      // 파일 확장자에 따른 설치 방법 결정
      var extension = Path.GetExtension(updateFilePath).ToLowerInvariant();

      UpdateResult result = extension switch
      {
        ".msi" => await InstallMsiAsync(updateFilePath, cancellationToken),
        ".exe" => await InstallExeAsync(updateFilePath, cancellationToken),
        ".zip" => await InstallZipAsync(updateFilePath, updateInfo, cancellationToken),
        _ => await InstallGenericAsync(updateFilePath, updateInfo, cancellationToken)
      };


      if (result.IsSuccess)
      {
        ProgressChanged?.Invoke(this, 100);
        _logger.LogInformation("업데이트 설치 완료");
      }
      else
      {
        _logger.LogError("업데이트 설치 실패: {Error}", result.ErrorMessage);
      }

      return result;
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "업데이트 설치 중 오류가 발생했습니다.");
      return UpdateResult.Failure($"설치 중 오류 발생: {ex.Message}");
    }
  }

  /// <summary>
  /// 백업 생성
  /// </summary>
  public async Task<bool> CreateBackupAsync(string targetPath, string backupPath)
  {
    try
    {
      _logger.LogInformation("백업 생성: {TargetPath} -> {BackupPath}", targetPath, backupPath);

      if (File.Exists(targetPath))
      {
        FileHelper.SafeCopy(targetPath, backupPath, overwrite: true);
      }
      else if (Directory.Exists(targetPath))
      {
        await Task.Run(() => FileHelper.CopyDirectory(targetPath, backupPath, overwrite: true));
      }
      else
      {
        _logger.LogWarning("백업할 대상이 존재하지 않습니다: {TargetPath}", targetPath);
        return false;
      }

      _logger.LogInformation("백업 생성 완료");
      return true;
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "백업 생성 중 오류가 발생했습니다.");
      return false;
    }
  }

  /// <summary>
  /// 백업 복원
  /// </summary>
  public async Task<bool> RestoreBackupAsync(string backupPath, string targetPath)
  {
    try
    {
      _logger.LogInformation("백업 복원: {BackupPath} -> {TargetPath}", backupPath, targetPath);

      if (File.Exists(backupPath))
      {
        FileHelper.SafeCopy(backupPath, targetPath, overwrite: true);
      }
      else if (Directory.Exists(backupPath))
      {
        await Task.Run(() => FileHelper.CopyDirectory(backupPath, targetPath, overwrite: true));
      }
      else
      {
        _logger.LogWarning("복원할 백업이 존재하지 않습니다: {BackupPath}", backupPath);
        return false;
      }

      _logger.LogInformation("백업 복원 완료");
      return true;
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "백업 복원 중 오류가 발생했습니다.");
      return false;
    }
  }

  /// <summary>
  /// MSI 설치
  /// </summary>
  private async Task<UpdateResult> InstallMsiAsync(string msiPath, CancellationToken cancellationToken)
  {
    try
    {
      ProgressChanged?.Invoke(this, 25);

      // 현재 실행 중인 프로세스 확인
      var currentProcess = Process.GetCurrentProcess();
      var currentProcessName = currentProcess.ProcessName;
      var currentExecutablePath = Environment.ProcessPath ?? currentProcess.MainModule?.FileName ?? string.Empty;

      // 동일한 이름의 다른 프로세스들 확인
      var relatedProcesses = GetRelatedProcesses(currentProcessName, currentProcess.Id);
      
      if (relatedProcesses.Any() || IsCurrentProcessTargetForUpdate(currentExecutablePath))
      {
        _logger.LogWarning("업데이트 대상 프로세스가 실행 중입니다. 프로세스 종료를 시도합니다.");
        
        // 관련 프로세스들을 우아하게 종료 시도
        var processesTerminated = await TerminateRelatedProcessesAsync(relatedProcesses, timeoutSeconds: 30);
        
        if (!processesTerminated)
        {
          _logger.LogWarning("일부 프로세스 종료에 실패했지만 설치를 계속 진행합니다.");
        }
      }

      ProgressChanged?.Invoke(this, 50);

      var startInfo = new ProcessStartInfo
      {
        FileName = "msiexec.exe",
        Arguments = $"/i \"{msiPath}\" /quiet /norestart /l*v \"{Path.Combine(Path.GetTempPath(), "msi_install.log")}\"",
        UseShellExecute = false,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        CreateNoWindow = true,
        Verb = "runas" // 관리자 권한으로 실행
      };

      ProgressChanged?.Invoke(this, 70);

      using var process = Process.Start(startInfo);
      if (process == null)
      {
        return UpdateResult.Failure("MSI 설치 프로세스를 시작할 수 없습니다.");
      }

      await process.WaitForExitAsync(cancellationToken);

      ProgressChanged?.Invoke(this, 90);

      if (process.ExitCode == 0)
      {
        return UpdateResult.Success(UpdateResultType.Completed);
      }
      else
      {
        var error = await process.StandardError.ReadToEndAsync(cancellationToken);
        var logPath = Path.Combine(Path.GetTempPath(), "msi_install.log");
        var logContent = File.Exists(logPath) ? await File.ReadAllTextAsync(logPath, cancellationToken) : "로그 파일 없음";
        
        return UpdateResult.Failure($"MSI 설치 실패 (코드: {process.ExitCode}): {error}\n로그: {logContent}");
      }
    }
    catch (Exception ex)
    {
      return UpdateResult.Failure($"MSI 설치 중 오류 발생: {ex.Message}");
    }
  }

  /// <summary>
  /// EXE 설치
  /// </summary>
  private async Task<UpdateResult> InstallExeAsync(string exePath, CancellationToken cancellationToken)
  {
    try
    {
      ProgressChanged?.Invoke(this, 25);

      var startInfo = new ProcessStartInfo
      {
        FileName = exePath,
        Arguments = "/S", // 일반적인 사일런트 설치 옵션
        UseShellExecute = false,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        CreateNoWindow = true
      };

      ProgressChanged?.Invoke(this, 50);

      using var process = Process.Start(startInfo);
      if (process == null)
      {
        return UpdateResult.Failure("EXE 설치 프로세스를 시작할 수 없습니다.");
      }

      await process.WaitForExitAsync(cancellationToken);

      ProgressChanged?.Invoke(this, 90);

      if (process.ExitCode == 0)
      {
        return UpdateResult.Success(UpdateResultType.Completed);
      }
      else
      {
        var error = await process.StandardError.ReadToEndAsync(cancellationToken);
        return UpdateResult.Failure($"EXE 설치 실패 (코드: {process.ExitCode}): {error}");
      }
    }
    catch (Exception ex)
    {
      return UpdateResult.Failure($"EXE 설치 중 오류 발생: {ex.Message}");
    }
  }

  /// <summary>
  /// ZIP 설치 (압축 해제)
  /// </summary>
  private async Task<UpdateResult> InstallZipAsync(string zipPath, UpdateInfo updateInfo, CancellationToken cancellationToken)
  {
    try
    {
      ProgressChanged?.Invoke(this, 25);

      var extractPath = Path.Combine(Path.GetTempPath(), $"AutoUpdater_Extract_{Guid.NewGuid():N}");
      FileHelper.EnsureDirectoryExists(extractPath);

      ProgressChanged?.Invoke(this, 50);

      // ZIP 압축 해제
      await Task.Run(() => System.IO.Compression.ZipFile.ExtractToDirectory(zipPath, extractPath), cancellationToken);

      ProgressChanged?.Invoke(this, 75);

      // 현재 애플리케이션 디렉토리로 파일 복사
      var currentDir = AppDomain.CurrentDomain.BaseDirectory;
      await Task.Run(() => FileHelper.CopyDirectory(extractPath, currentDir, overwrite: true), cancellationToken);

      ProgressChanged?.Invoke(this, 90);

      // 임시 디렉토리 정리
      Directory.Delete(extractPath, recursive: true);

      return UpdateResult.Success(UpdateResultType.Completed);
    }
    catch (Exception ex)
    {
      return UpdateResult.Failure($"ZIP 설치 중 오류 발생: {ex.Message}");
    }
  }

  /// <summary>
  /// 일반 파일 설치
  /// </summary>
  private async Task<UpdateResult> InstallGenericAsync(string filePath, UpdateInfo updateInfo, CancellationToken cancellationToken)
  {
    try
    {
      ProgressChanged?.Invoke(this, 50);

      // 기본적으로 실행 파일로 실행 시도
      var startInfo = new ProcessStartInfo
      {
        FileName = filePath,
        UseShellExecute = true
      };

      using var process = Process.Start(startInfo);
      if (process != null)
      {
        await process.WaitForExitAsync(cancellationToken);
        ProgressChanged?.Invoke(this, 90);

        if (process.ExitCode == 0)
        {
          return UpdateResult.Success(UpdateResultType.Completed);
        }
        else
        {
          return UpdateResult.Failure($"설치 실패 (코드: {process.ExitCode})");
        }
      }

      return UpdateResult.Failure("설치 프로세스를 시작할 수 없습니다.");
    }
    catch (Exception ex)
    {
      return UpdateResult.Failure($"일반 설치 중 오류 발생: {ex.Message}");
    }
  }

  /// <summary>
  /// 관련 프로세스 목록 가져오기
  /// </summary>
  private List<Process> GetRelatedProcesses(string processName, int currentProcessId)
  {
    try
    {
      return Process.GetProcessesByName(processName)
        .Where(p => p.Id != currentProcessId)
        .ToList();
    }
    catch (Exception ex)
    {
      _logger.LogWarning(ex, "관련 프로세스 검색 중 오류 발생");
      return new List<Process>();
    }
  }

  /// <summary>
  /// 현재 프로세스가 업데이트 대상인지 확인
  /// </summary>
  private bool IsCurrentProcessTargetForUpdate(string currentExecutablePath)
  {
    if (string.IsNullOrEmpty(currentExecutablePath))
      return false;

    // 현재 실행 파일이 업데이트 대상 디렉토리에 있는지 확인
    var currentDir = Path.GetDirectoryName(currentExecutablePath);
    var appDir = AppDomain.CurrentDomain.BaseDirectory;

    return string.Equals(currentDir, appDir, StringComparison.OrdinalIgnoreCase);
  }

  /// <summary>
  /// 관련 프로세스들을 우아하게 종료
  /// </summary>
  private async Task<bool> TerminateRelatedProcessesAsync(List<Process> processes, int timeoutSeconds = 30)
  {
    if (!processes.Any())
      return true;

    var allTerminated = true;

    foreach (var process in processes)
    {
      try
      {
        _logger.LogInformation("프로세스 종료 시도: {ProcessName} (PID: {ProcessId})", process.ProcessName, process.Id);

        // 우아한 종료 시도
        if (!process.HasExited)
        {
          process.CloseMainWindow();
          
          // 종료 대기
          var terminated = await WaitForProcessExitAsync(process, timeoutSeconds / 2);
          
          if (!terminated && !process.HasExited)
          {
            _logger.LogWarning("우아한 종료 실패, 강제 종료 시도: {ProcessName} (PID: {ProcessId})", process.ProcessName, process.Id);
            process.Kill();
            terminated = await WaitForProcessExitAsync(process, timeoutSeconds / 2);
          }

          if (!terminated)
          {
            _logger.LogError("프로세스 종료 실패: {ProcessName} (PID: {ProcessId})", process.ProcessName, process.Id);
            allTerminated = false;
          }
          else
          {
            _logger.LogInformation("프로세스 종료 완료: {ProcessName} (PID: {ProcessId})", process.ProcessName, process.Id);
          }
        }
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "프로세스 종료 중 오류 발생: {ProcessName} (PID: {ProcessId})", process.ProcessName, process.Id);
        allTerminated = false;
      }
      finally
      {
        process.Dispose();
      }
    }

    return allTerminated;
  }

  /// <summary>
  /// 프로세스 종료 대기
  /// </summary>
  private async Task<bool> WaitForProcessExitAsync(Process process, int timeoutSeconds)
  {
    try
    {
      var timeout = TimeSpan.FromSeconds(timeoutSeconds);
      using var cts = new CancellationTokenSource(timeout);
      
      await process.WaitForExitAsync(cts.Token);
      return true;
    }
    catch (OperationCanceledException)
    {
      return false;
    }
    catch (Exception ex)
    {
      _logger.LogWarning(ex, "프로세스 종료 대기 중 오류 발생");
      return false;
    }
  }
}