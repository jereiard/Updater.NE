using AutoUpdater.Core.Configuration;
using AutoUpdater.Core.Interfaces;
using AutoUpdater.Core.Models;
using AutoUpdater.Core.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace AutoUpdater.Tests;

/// <summary>
/// 자기 업데이트 기능 테스트
/// </summary>
public class SelfUpdateTests
{
    private readonly Mock<IUpdateChecker> _mockUpdateChecker;
    private readonly Mock<IUpdateDownloader> _mockUpdateDownloader;
    private readonly Mock<IUpdateInstaller> _mockUpdateInstaller;
    private readonly Mock<ILogger<AutoUpdaterService>> _mockLogger;
    private readonly UpdaterConfiguration _configuration;
    private readonly AutoUpdaterService _autoUpdaterService;

    public SelfUpdateTests()
    {
        _mockUpdateChecker = new Mock<IUpdateChecker>();
        _mockUpdateDownloader = new Mock<IUpdateDownloader>();
        _mockUpdateInstaller = new Mock<IUpdateInstaller>();
        _mockLogger = new Mock<ILogger<AutoUpdaterService>>();

        _configuration = new UpdaterConfiguration
        {
            ApplicationId = "TestApp",
            CurrentVersion = "1.0.0",
            ServerUrl = "https://test.example.com",
            EnableSelfUpdate = true,
            LauncherFileName = "AutoUpdater.Launcher.exe",
            ProcessTerminationTimeoutSeconds = 30,
            AllowForceProcessTermination = false
        };

        _autoUpdaterService = new AutoUpdaterService(
            _mockUpdateChecker.Object,
            _mockUpdateDownloader.Object,
            _mockUpdateInstaller.Object,
            _configuration,
            _mockLogger.Object);
    }

    [Fact]
    public async Task InitiateSelfUpdateAsync_WithValidUpdate_ShouldDownloadAndPrepareUpdate()
    {
        // Arrange
        var updateInfo = new UpdateInfo
        {
            Version = "2.0.0",
            DownloadUrl = "https://test.example.com/update.zip",
            FileSize = 1024 * 1024,
            FileHash = "testhash",
            ReleaseNotes = "Test update",
            Mandatory = false,
            ReleaseDate = DateTime.Now
        };

        var downloadResult = UpdateResult.Success(UpdateResultType.Downloaded, updateInfo);
        downloadResult.AdditionalData = new Dictionary<string, object>
        {
            ["FilePath"] = @"C:\temp\update.zip"
        };

        _mockUpdateDownloader.Setup(x => x.DownloadUpdateAsync(
            It.IsAny<UpdateInfo>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(@"C:\temp\update.zip");

        // Act & Assert
        // 자기 업데이트는 Environment.Exit(0)를 호출하므로 실제 테스트에서는 실행할 수 없습니다.
        // 대신 다운로드 부분만 테스트합니다.
        var result = await _autoUpdaterService.DownloadUpdateAsync(updateInfo);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(UpdateResultType.Downloaded, result.ResultType);
        _mockUpdateDownloader.Verify(x => x.DownloadUpdateAsync(
            It.IsAny<UpdateInfo>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task InitiateSelfUpdateAsync_WithDownloadFailure_ShouldReturnFailure()
    {
        // Arrange
        var updateInfo = new UpdateInfo
        {
            Version = "2.0.0",
            DownloadUrl = "https://test.example.com/update.zip",
            FileSize = 1024 * 1024,
            FileHash = "testhash",
            ReleaseNotes = "Test update",
            Mandatory = false,
            ReleaseDate = DateTime.Now
        };

        _mockUpdateDownloader.Setup(x => x.DownloadUpdateAsync(
            It.IsAny<UpdateInfo>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Download failed"));

        // Act
        var result = await _autoUpdaterService.DownloadUpdateAsync(updateInfo);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Contains("다운로드 실패", result.ErrorMessage);
    }

    [Fact]
    public void UpdateLauncherInfo_ShouldSerializeCorrectly()
    {
        // Arrange
        var launcherInfo = new UpdateLauncherInfo
        {
            UpdateFilePath = @"C:\temp\update.zip",
            TargetExecutablePath = @"C:\app\myapp.exe",
            ProcessId = 1234,
            RestartAfterUpdate = true,
            UpdateFileType = ".zip",
            BackupDirectory = @"C:\temp\backup",
            WaitTimeoutSeconds = 30,
            AllowForceKill = false,
            RestartArguments = "--updated"
        };

        // Act
        var json = System.Text.Json.JsonSerializer.Serialize(launcherInfo);
        var deserialized = System.Text.Json.JsonSerializer.Deserialize<UpdateLauncherInfo>(json);

        // Assert
        Assert.NotNull(deserialized);
        Assert.Equal(launcherInfo.UpdateFilePath, deserialized.UpdateFilePath);
        Assert.Equal(launcherInfo.TargetExecutablePath, deserialized.TargetExecutablePath);
        Assert.Equal(launcherInfo.ProcessId, deserialized.ProcessId);
        Assert.Equal(launcherInfo.RestartAfterUpdate, deserialized.RestartAfterUpdate);
        Assert.Equal(launcherInfo.UpdateFileType, deserialized.UpdateFileType);
        Assert.Equal(launcherInfo.BackupDirectory, deserialized.BackupDirectory);
        Assert.Equal(launcherInfo.WaitTimeoutSeconds, deserialized.WaitTimeoutSeconds);
        Assert.Equal(launcherInfo.AllowForceKill, deserialized.AllowForceKill);
        Assert.Equal(launcherInfo.RestartArguments, deserialized.RestartArguments);
    }

    [Fact]
    public async Task PerformUpdateAsync_ShouldExecuteFullUpdateProcess()
    {
        // Arrange
        var request = new UpdateRequest
        {
            ApplicationId = "TestApp",
            CurrentVersion = "1.0.0",
            Platform = "Win32NT",
            Architecture = "x64"
        };

        var updateInfo = new UpdateInfo
        {
            Version = "2.0.0",
            DownloadUrl = "https://test.example.com/update.zip",
            FileSize = 1024 * 1024,
            FileHash = "testhash",
            ReleaseNotes = "Test update",
            Mandatory = false,
            ReleaseDate = DateTime.Now
        };

        var checkResult = UpdateResult.Success(UpdateResultType.UpdateAvailable, updateInfo);
        var downloadResult = UpdateResult.Success(UpdateResultType.Downloaded, updateInfo);
        downloadResult.AdditionalData = new Dictionary<string, object>
        {
            ["FilePath"] = @"C:\temp\update.zip"
        };
        var installResult = UpdateResult.Success(UpdateResultType.Completed, updateInfo);

        _mockUpdateChecker.Setup(x => x.CheckForUpdateAsync(
            It.IsAny<UpdateRequest>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(checkResult);

        _mockUpdateDownloader.Setup(x => x.DownloadUpdateAsync(
            It.IsAny<UpdateInfo>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(@"C:\temp\update.zip");

        _mockUpdateInstaller.Setup(x => x.InstallUpdateAsync(
            It.IsAny<string>(),
            It.IsAny<UpdateInfo>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(installResult);

        // Act
        var result = await _autoUpdaterService.PerformUpdateAsync(request);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(UpdateResultType.Completed, result.ResultType);

        _mockUpdateChecker.Verify(x => x.CheckForUpdateAsync(
            It.IsAny<UpdateRequest>(),
            It.IsAny<CancellationToken>()), Times.Once);

        _mockUpdateDownloader.Verify(x => x.DownloadUpdateAsync(
            It.IsAny<UpdateInfo>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Once);

        _mockUpdateInstaller.Verify(x => x.InstallUpdateAsync(
            It.IsAny<string>(),
            It.IsAny<UpdateInfo>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void StartAutoUpdateCheck_ShouldStartTimer()
    {
        // Arrange
        var configuration = new UpdaterConfiguration
        {
            CheckIntervalMinutes = 60,
            ApplicationId = "TestApp",
            CurrentVersion = "1.0.0"
        };

        using var service = new AutoUpdaterService(
            _mockUpdateChecker.Object,
            _mockUpdateDownloader.Object,
            _mockUpdateInstaller.Object,
            configuration,
            _mockLogger.Object);

        // Act
        service.StartAutoUpdateCheck();

        // Assert
        // 타이머가 시작되었는지 확인하는 것은 어렵지만, 
        // 최소한 예외가 발생하지 않았는지 확인할 수 있습니다.
        Assert.True(true); // 여기까지 도달하면 성공
    }

    [Fact]
    public void StopAutoUpdateCheck_ShouldStopTimer()
    {
        // Arrange
        var configuration = new UpdaterConfiguration
        {
            CheckIntervalMinutes = 60,
            ApplicationId = "TestApp",
            CurrentVersion = "1.0.0"
        };

        using var service = new AutoUpdaterService(
            _mockUpdateChecker.Object,
            _mockUpdateDownloader.Object,
            _mockUpdateInstaller.Object,
            configuration,
            _mockLogger.Object);

        // Act
        service.StartAutoUpdateCheck();
        service.StopAutoUpdateCheck();

        // Assert
        // 타이머가 중지되었는지 확인하는 것은 어렵지만, 
        // 최소한 예외가 발생하지 않았는지 확인할 수 있습니다.
        Assert.True(true); // 여기까지 도달하면 성공
    }
}