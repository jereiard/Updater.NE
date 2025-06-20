using System.Diagnostics;
using System.IO.Compression;
using AutoUpdater.Core.Models;
using AutoUpdater.Core.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace AutoUpdater.Tests;

/// <summary>
/// DefaultUpdateInstaller 테스트 클래스
/// </summary>
public class DefaultUpdateInstallerTests
{
    private readonly Mock<ILogger<DefaultUpdateInstaller>> _mockLogger;
    private readonly DefaultUpdateInstaller _installer;

    public DefaultUpdateInstallerTests()
    {
        _mockLogger = new Mock<ILogger<DefaultUpdateInstaller>>();
        _installer = new DefaultUpdateInstaller(_mockLogger.Object);
    }

    [Fact]
    public async Task InstallUpdateAsync_WithNullUpdateFilePath_ShouldThrowArgumentNullException()
    {
        // Arrange
        var updateInfo = new UpdateInfo { Version = "1.0.0" };

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => 
            _installer.InstallUpdateAsync(null!, updateInfo));
    }

    [Fact]
    public async Task InstallUpdateAsync_WithNullUpdateInfo_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => 
            _installer.InstallUpdateAsync("test.msi", null!));
    }

    [Fact]
    public async Task InstallUpdateAsync_WithNonExistentFile_ShouldReturnFailure()
    {
        // Arrange
        var updateInfo = new UpdateInfo { Version = "1.0.0" };
        var nonExistentFile = "non_existent_file.msi";

        // Act
        var result = await _installer.InstallUpdateAsync(nonExistentFile, updateInfo);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Contains("업데이트 파일을 찾을 수 없습니다", result.ErrorMessage);
    }

    [Fact]
    public async Task CreateBackupAsync_WithValidFile_ShouldCreateBackup()
    {
        // Arrange
        var tempDir = Path.GetTempPath();
        var sourceFile = Path.Combine(tempDir, $"test_source_{Guid.NewGuid():N}.txt");
        var backupFile = Path.Combine(tempDir, $"test_backup_{Guid.NewGuid():N}.txt");

        try
        {
            // 테스트 파일 생성
            await File.WriteAllTextAsync(sourceFile, "Test content");

            // Act
            var result = await _installer.CreateBackupAsync(sourceFile, backupFile);

            // Assert
            Assert.True(result);
            Assert.True(File.Exists(backupFile));
            
            var backupContent = await File.ReadAllTextAsync(backupFile);
            Assert.Equal("Test content", backupContent);
        }
        finally
        {
            // 정리
            if (File.Exists(sourceFile)) File.Delete(sourceFile);
            if (File.Exists(backupFile)) File.Delete(backupFile);
        }
    }

    [Fact]
    public async Task RestoreBackupAsync_WithValidBackup_ShouldRestoreFile()
    {
        // Arrange
        var tempDir = Path.GetTempPath();
        var backupFile = Path.Combine(tempDir, $"test_backup_{Guid.NewGuid():N}.txt");
        var targetFile = Path.Combine(tempDir, $"test_target_{Guid.NewGuid():N}.txt");

        try
        {
            // 백업 파일 생성
            await File.WriteAllTextAsync(backupFile, "Backup content");

            // Act
            var result = await _installer.RestoreBackupAsync(backupFile, targetFile);

            // Assert
            Assert.True(result);
            Assert.True(File.Exists(targetFile));
            
            var restoredContent = await File.ReadAllTextAsync(targetFile);
            Assert.Equal("Backup content", restoredContent);
        }
        finally
        {
            // 정리
            if (File.Exists(backupFile)) File.Delete(backupFile);
            if (File.Exists(targetFile)) File.Delete(targetFile);
        }
    }

    [Fact]
    public async Task InstallUpdateAsync_WithZipFile_ShouldExtractAndInstall()
    {
        // Arrange
        var tempDir = Path.GetTempPath();
        var zipFile = Path.Combine(tempDir, $"test_update_{Guid.NewGuid():N}.zip");
        var testFile = Path.Combine(tempDir, "test_content.txt");
        var updateInfo = new UpdateInfo { Version = "1.0.0" };

        try
        {
            // 테스트용 파일 생성
            await File.WriteAllTextAsync(testFile, "Test content for zip");

            // ZIP 파일 생성
            using (var archive = ZipFile.Open(zipFile, ZipArchiveMode.Create))
            {
                var entry = archive.CreateEntry("test_content.txt");
                using var entryStream = entry.Open();
                using var fileStream = File.OpenRead(testFile);
                await fileStream.CopyToAsync(entryStream);
            }

            // Act
            var result = await _installer.InstallUpdateAsync(zipFile, updateInfo);

            // Assert
            Assert.True(result.IsSuccess);
            Assert.Equal(UpdateResultType.Completed, result.ResultType);
        }
        finally
        {
            // 정리
            if (File.Exists(zipFile)) File.Delete(zipFile);
            if (File.Exists(testFile)) File.Delete(testFile);
        }
    }

    [Fact]
    public void ProgressChanged_Event_ShouldBeRaised()
    {
        // Arrange
        var progressValues = new List<int>();
        _installer.ProgressChanged += (sender, progress) => progressValues.Add(progress);

        // Act - 직접 이벤트 발생시켜 테스트
        var progressChangedField = typeof(DefaultUpdateInstaller)
            .GetField("ProgressChanged", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        
        var progressChangedEvent = (EventHandler<int>?)progressChangedField?.GetValue(_installer);
        progressChangedEvent?.Invoke(_installer, 50);

        // Assert
        Assert.Single(progressValues);
        Assert.Equal(50, progressValues[0]);
    }

    [Fact]
    public async Task CreateBackupAsync_WithNonExistentSource_ShouldReturnFalse()
    {
        // Arrange
        var nonExistentSource = "non_existent_source.txt";
        var backupPath = Path.Combine(Path.GetTempPath(), "backup.txt");

        // Act
        var result = await _installer.CreateBackupAsync(nonExistentSource, backupPath);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task RestoreBackupAsync_WithNonExistentBackup_ShouldReturnFalse()
    {
        // Arrange
        var nonExistentBackup = "non_existent_backup.txt";
        var targetPath = Path.Combine(Path.GetTempPath(), "target.txt");

        // Act
        var result = await _installer.RestoreBackupAsync(nonExistentBackup, targetPath);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task InstallUpdateAsync_ShouldLogProgress()
    {
        // Arrange
        var tempDir = Path.GetTempPath();
        var zipFile = Path.Combine(tempDir, $"test_progress_{Guid.NewGuid():N}.zip");
        var updateInfo = new UpdateInfo { Version = "1.0.0" };

        try
        {
            // 빈 ZIP 파일 생성
            using (var archive = ZipFile.Open(zipFile, ZipArchiveMode.Create))
            {
                // 빈 아카이브
            }

            var progressValues = new List<int>();
            _installer.ProgressChanged += (sender, progress) => progressValues.Add(progress);

            // Act
            await _installer.InstallUpdateAsync(zipFile, updateInfo);

            // Assert
            Assert.NotEmpty(progressValues);
            Assert.Contains(0, progressValues); // 시작 진행률
            Assert.Contains(100, progressValues); // 완료 진행률
        }
        finally
        {
            if (File.Exists(zipFile)) File.Delete(zipFile);
        }
    }
} 