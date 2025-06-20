using AutoUpdater.Core.Utilities;
using Xunit;

namespace AutoUpdater.Tests;

/// <summary>
/// VersionHelper 테스트 클래스
/// </summary>
public class VersionHelperTests
{
    [Theory]
    [InlineData("1.0.0", "2.0.0", true)]
    [InlineData("1.0.0", "1.1.0", true)]
    [InlineData("1.0.0", "1.0.1", true)]
    [InlineData("2.0.0", "1.0.0", false)]
    [InlineData("1.0.0", "1.0.0", false)]
    [InlineData("1.0.1", "1.0.0", false)]
    [InlineData("1.0.0-beta", "1.0.0", true)]  // 프리릴리스 → 정식 (업데이트)
    [InlineData("1.0.0", "1.0.0-beta", false)] // 정식 → 프리릴리스 (다운그레이드)
    [InlineData("1.0.0-alpha", "1.0.0-beta", true)] // alpha < beta (알파벳순)
    [InlineData("", "1.0.0", true)]            // 빈 현재 버전
    [InlineData("1.0.0", "", false)]           // 빈 새 버전
    [InlineData("", "", false)]                // 둘 다 빈 버전
    [InlineData("invalid", "1.0.0", false)]    // 잘못된 현재 버전
    [InlineData("1.0.0", "invalid", false)]    // 잘못된 새 버전
    public void IsNewerVersion_ShouldReturnCorrectResult(string currentVersion, string newVersion, bool expected)
    {
        // Act
        var result = VersionHelper.IsNewerVersion(currentVersion, newVersion);

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("1.0.0")]
    [InlineData("1.0.0.0")]
    [InlineData("1.2.3")]
    [InlineData("1.2.3.4")]
    [InlineData("10.20.30")]
    public void IsValidVersion_WithValidVersions_ShouldReturnTrue(string version)
    {
        // Act
        var result = VersionHelper.IsValidVersion(version);

        // Assert
        Assert.True(result);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("invalid")]
    [InlineData("1.0")]
    [InlineData("1.0.0.0.0")]
    public void IsValidVersion_WithInvalidVersions_ShouldReturnFalse(string version)
    {
        // Act
        var result = VersionHelper.IsValidVersion(version);

        // Assert
        Assert.False(result);
    }

    [Theory]
    [InlineData("1.0.0", "1.0.0", true)]
    [InlineData("1.1.0", "1.0.0", true)]
    [InlineData("2.0.0", "1.0.0", true)]
    [InlineData("0.9.0", "1.0.0", false)]
    [InlineData("1.0.0", "", true)] // 최소 버전이 없으면 항상 만족
    public void MeetsMinimumVersion_ShouldReturnCorrectResult(string currentVersion, string minimumVersion, bool expected)
    {
        // Act
        var result = VersionHelper.MeetsMinimumVersion(currentVersion, minimumVersion);

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("1.0.0", "2.0.0", -1)]
    [InlineData("2.0.0", "1.0.0", 1)]
    [InlineData("1.0.0", "1.0.0", 0)]
    [InlineData("1.0.1", "1.0.0", 1)]
    [InlineData("1.0.0", "1.0.1", -1)]
    public void CompareVersions_ShouldReturnCorrectResult(string version1, string version2, int expected)
    {
        // Act
        var result = VersionHelper.CompareVersions(version1, version2);

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("1.0.0")]
    [InlineData("1.2.3")]
    [InlineData("10.20.30.40")]
    public void ParseVersion_WithValidVersion_ShouldNotThrow(string versionString)
    {
        // Act & Assert
        var exception = Record.Exception(() => VersionHelper.ParseVersion(versionString));
        Assert.Null(exception);
    }

    [Theory]
    [InlineData("")]
    [InlineData("invalid")]
    [InlineData("1.0")]
    public void ParseVersion_WithInvalidVersion_ShouldThrow(string versionString)
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => VersionHelper.ParseVersion(versionString));
    }
}
 