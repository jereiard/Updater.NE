using AutoUpdater.Core.Utilities;
using Xunit;

namespace AutoUpdater.Tests;

/// <summary>
/// FileHelper 테스트 클래스
/// </summary>
public class FileHelperTests : IDisposable
{
    private readonly string _tempDirectory;

    public FileHelperTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), $"AutoUpdaterTests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDirectory);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, recursive: true);
        }
    }

    [Fact]
    public void EnsureDirectoryExists_WithNonExistentDirectory_ShouldCreateDirectory()
    {
        // Arrange
        var testDir = Path.Combine(_tempDirectory, "test_dir");

        // Act
        FileHelper.EnsureDirectoryExists(testDir);

        // Assert
        Assert.True(Directory.Exists(testDir));
    }

    [Fact]
    public void EnsureDirectoryExists_WithExistingDirectory_ShouldNotThrow()
    {
        // Arrange
        var testDir = Path.Combine(_tempDirectory, "existing_dir");
        Directory.CreateDirectory(testDir);

        // Act & Assert
        var exception = Record.Exception(() => FileHelper.EnsureDirectoryExists(testDir));
        Assert.Null(exception);
    }

    [Fact]
    public async Task CalculateFileHashAsync_WithValidFile_ShouldReturnHash()
    {
        // Arrange
        var testFile = Path.Combine(_tempDirectory, "test.txt");
        var testContent = "Hello, World!";
        await File.WriteAllTextAsync(testFile, testContent);

        // Act
        var hash = await FileHelper.CalculateFileHashAsync(testFile);

        // Assert
        Assert.NotNull(hash);
        Assert.NotEmpty(hash);
        Assert.Equal(64, hash.Length); // SHA256 해시는 64자
    }

    [Fact]
    public async Task CalculateFileHashAsync_WithNonExistentFile_ShouldThrow()
    {
        // Arrange
        var nonExistentFile = Path.Combine(_tempDirectory, "nonexistent.txt");

        // Act & Assert
        await Assert.ThrowsAsync<FileNotFoundException>(() => 
            FileHelper.CalculateFileHashAsync(nonExistentFile));
    }

    [Fact]
    public async Task VerifyFileHashAsync_WithMatchingHash_ShouldReturnTrue()
    {
        // Arrange
        var testFile = Path.Combine(_tempDirectory, "test.txt");
        var testContent = "Hello, World!";
        await File.WriteAllTextAsync(testFile, testContent);
        
        var expectedHash = await FileHelper.CalculateFileHashAsync(testFile);

        // Act
        var result = await FileHelper.VerifyFileHashAsync(testFile, expectedHash);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task VerifyFileHashAsync_WithNonMatchingHash_ShouldReturnFalse()
    {
        // Arrange
        var testFile = Path.Combine(_tempDirectory, "test.txt");
        var testContent = "Hello, World!";
        await File.WriteAllTextAsync(testFile, testContent);
        
        var wrongHash = "wrong_hash";

        // Act
        var result = await FileHelper.VerifyFileHashAsync(testFile, wrongHash);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void SafeCopy_WithValidFiles_ShouldCopyFile()
    {
        // Arrange
        var sourceFile = Path.Combine(_tempDirectory, "source.txt");
        var destFile = Path.Combine(_tempDirectory, "dest.txt");
        var testContent = "Test content";
        
        File.WriteAllText(sourceFile, testContent);

        // Act
        FileHelper.SafeCopy(sourceFile, destFile);

        // Assert
        Assert.True(File.Exists(destFile));
        Assert.Equal(testContent, File.ReadAllText(destFile));
    }

    [Fact]
    public void SafeMove_WithValidFiles_ShouldMoveFile()
    {
        // Arrange
        var sourceFile = Path.Combine(_tempDirectory, "source.txt");
        var destFile = Path.Combine(_tempDirectory, "dest.txt");
        var testContent = "Test content";
        
        File.WriteAllText(sourceFile, testContent);

        // Act
        FileHelper.SafeMove(sourceFile, destFile);

        // Assert
        Assert.False(File.Exists(sourceFile));
        Assert.True(File.Exists(destFile));
        Assert.Equal(testContent, File.ReadAllText(destFile));
    }

    [Fact]
    public void GetFileSize_WithValidFile_ShouldReturnCorrectSize()
    {
        // Arrange
        var testFile = Path.Combine(_tempDirectory, "test.txt");
        var testContent = "Hello, World!";
        File.WriteAllText(testFile, testContent);
        var expectedSize = new FileInfo(testFile).Length;

        // Act
        var actualSize = FileHelper.GetFileSize(testFile);

        // Assert
        Assert.Equal(expectedSize, actualSize);
    }

    [Fact]
    public void GetFileSize_WithNonExistentFile_ShouldReturnZero()
    {
        // Arrange
        var nonExistentFile = Path.Combine(_tempDirectory, "nonexistent.txt");

        // Act
        var size = FileHelper.GetFileSize(nonExistentFile);

        // Assert
        Assert.Equal(0, size);
    }

    [Fact]
    public void GetTempFilePath_ShouldReturnValidPath()
    {
        // Act
        var tempPath = FileHelper.GetTempFilePath(".txt");

        // Assert
        Assert.NotNull(tempPath);
        Assert.EndsWith(".txt", tempPath);
        Assert.True(Path.IsPathRooted(tempPath));
    }

    [Fact]
    public void IsFileInUse_WithNonExistentFile_ShouldReturnFalse()
    {
        // Arrange
        var nonExistentFile = Path.Combine(_tempDirectory, "nonexistent.txt");

        // Act
        var result = FileHelper.IsFileInUse(nonExistentFile);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void CopyDirectory_WithValidDirectories_ShouldCopyAllFiles()
    {
        // Arrange
        var sourceDir = Path.Combine(_tempDirectory, "source");
        var destDir = Path.Combine(_tempDirectory, "dest");
        
        Directory.CreateDirectory(sourceDir);
        File.WriteAllText(Path.Combine(sourceDir, "file1.txt"), "Content 1");
        File.WriteAllText(Path.Combine(sourceDir, "file2.txt"), "Content 2");
        
        var subDir = Path.Combine(sourceDir, "subdir");
        Directory.CreateDirectory(subDir);
        File.WriteAllText(Path.Combine(subDir, "file3.txt"), "Content 3");

        // Act
        FileHelper.CopyDirectory(sourceDir, destDir);

        // Assert
        Assert.True(Directory.Exists(destDir));
        Assert.True(File.Exists(Path.Combine(destDir, "file1.txt")));
        Assert.True(File.Exists(Path.Combine(destDir, "file2.txt")));
        Assert.True(File.Exists(Path.Combine(destDir, "subdir", "file3.txt")));
        
        Assert.Equal("Content 1", File.ReadAllText(Path.Combine(destDir, "file1.txt")));
        Assert.Equal("Content 2", File.ReadAllText(Path.Combine(destDir, "file2.txt")));
        Assert.Equal("Content 3", File.ReadAllText(Path.Combine(destDir, "subdir", "file3.txt")));
    }
} 