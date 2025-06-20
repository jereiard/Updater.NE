using System.Net;
using System.Text.Json;
using AutoUpdater.Core.Configuration;
using AutoUpdater.Core.Models;
using AutoUpdater.Core.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using Xunit;

namespace AutoUpdater.Tests;

/// <summary>
/// WebUpdateChecker 테스트 클래스
/// </summary>
public class WebUpdateCheckerTests
{
    private readonly Mock<ILogger<WebUpdateChecker>> _mockLogger;
    private readonly UpdaterConfiguration _configuration;

    public WebUpdateCheckerTests()
    {
        _mockLogger = new Mock<ILogger<WebUpdateChecker>>();
        _configuration = new UpdaterConfiguration
        {
            ServerUrl = "https://test-server.com",
            ApplicationId = "TestApp",
            CurrentVersion = "1.0.0",
            TimeoutSeconds = 30,
            UserAgent = "TestAgent/1.0"
        };
    }

    [Fact]
    public async Task CheckForUpdateAsync_WithNewerVersionAvailable_ShouldReturnUpdateAvailable()
    {
        // Arrange
        var updateInfo = new UpdateInfo
        {
            Version = "2.0.0",
            DownloadUrl = "https://example.com/update.zip",
            FileSize = 1024,
            FileHash = "abc123",
            ReleaseNotes = "New version",
            Mandatory = false,
            ReleaseDate = DateTime.UtcNow
        };

        var httpClient = CreateMockHttpClient(HttpStatusCode.OK, JsonSerializer.Serialize(updateInfo));
        var checker = new WebUpdateChecker(httpClient, _configuration, _mockLogger.Object);

        var request = new UpdateRequest
        {
            ApplicationId = "TestApp",
            CurrentVersion = "1.0.0",
            Platform = "Windows",
            Architecture = "x64"
        };

        // Act
        var result = await checker.CheckForUpdateAsync(request);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(UpdateResultType.UpdateAvailable, result.ResultType);
        Assert.NotNull(result.UpdateInfo);
        Assert.Equal("2.0.0", result.UpdateInfo.Version);
    }

    [Fact]
    public async Task CheckForUpdateAsync_WithSameVersion_ShouldReturnNoUpdateAvailable()
    {
        // Arrange
        var updateInfo = new UpdateInfo
        {
            Version = "1.0.0", // 같은 버전
            DownloadUrl = "https://example.com/update.zip",
            FileSize = 1024,
            FileHash = "abc123",
            ReleaseNotes = "Current version",
            Mandatory = false,
            ReleaseDate = DateTime.UtcNow
        };

        var httpClient = CreateMockHttpClient(HttpStatusCode.OK, JsonSerializer.Serialize(updateInfo));
        var checker = new WebUpdateChecker(httpClient, _configuration, _mockLogger.Object);

        var request = new UpdateRequest
        {
            ApplicationId = "TestApp",
            CurrentVersion = "1.0.0",
            Platform = "Windows",
            Architecture = "x64"
        };

        // Act
        var result = await checker.CheckForUpdateAsync(request);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(UpdateResultType.NoUpdateAvailable, result.ResultType);
    }

    [Fact]
    public async Task CheckForUpdateAsync_WithNotFound_ShouldReturnNoUpdateAvailable()
    {
        // Arrange
        var httpClient = CreateMockHttpClient(HttpStatusCode.NotFound, "");
        var checker = new WebUpdateChecker(httpClient, _configuration, _mockLogger.Object);

        var request = new UpdateRequest
        {
            ApplicationId = "TestApp",
            CurrentVersion = "1.0.0",
            Platform = "Windows",
            Architecture = "x64"
        };

        // Act
        var result = await checker.CheckForUpdateAsync(request);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(UpdateResultType.NoUpdateAvailable, result.ResultType);
    }

    [Fact]
    public async Task CheckForUpdateAsync_WithHttpError_ShouldReturnFailure()
    {
        // Arrange
        var httpClient = CreateMockHttpClient(HttpStatusCode.InternalServerError, "Server Error");
        var checker = new WebUpdateChecker(httpClient, _configuration, _mockLogger.Object);

        var request = new UpdateRequest
        {
            ApplicationId = "TestApp",
            CurrentVersion = "1.0.0",
            Platform = "Windows",
            Architecture = "x64"
        };

        // Act
        var result = await checker.CheckForUpdateAsync(request);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(UpdateResultType.Failed, result.ResultType);
        Assert.NotNull(result.ErrorMessage);
    }

    [Fact]
    public async Task GetUpdateInfoAsync_WithValidResponse_ShouldReturnUpdateInfo()
    {
        // Arrange
        var updateInfo = new UpdateInfo
        {
            Version = "2.0.0",
            DownloadUrl = "https://example.com/update.zip",
            FileSize = 1024,
            FileHash = "abc123",
            ReleaseNotes = "New version",
            Mandatory = false,
            ReleaseDate = DateTime.UtcNow
        };

        var httpClient = CreateMockHttpClient(HttpStatusCode.OK, JsonSerializer.Serialize(updateInfo));
        var checker = new WebUpdateChecker(httpClient, _configuration, _mockLogger.Object);

        var request = new UpdateRequest
        {
            ApplicationId = "TestApp",
            CurrentVersion = "1.0.0",
            Platform = "Windows",
            Architecture = "x64"
        };

        // Act
        var result = await checker.GetUpdateInfoAsync(request);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("2.0.0", result.Version);
        Assert.Equal("https://example.com/update.zip", result.DownloadUrl);
    }

    [Fact]
    public async Task GetUpdateInfoAsync_WithInvalidJson_ShouldThrowException()
    {
        // Arrange
        var httpClient = CreateMockHttpClient(HttpStatusCode.OK, "invalid json");
        var checker = new WebUpdateChecker(httpClient, _configuration, _mockLogger.Object);

        var request = new UpdateRequest
        {
            ApplicationId = "TestApp",
            CurrentVersion = "1.0.0",
            Platform = "Windows",
            Architecture = "x64"
        };

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => 
            checker.GetUpdateInfoAsync(request));
    }

    private HttpClient CreateMockHttpClient(HttpStatusCode statusCode, string content)
    {
        var mockHandler = new Mock<HttpMessageHandler>();
        
        mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = statusCode,
                Content = new StringContent(content)
            });

        return new HttpClient(mockHandler.Object);
    }
} 