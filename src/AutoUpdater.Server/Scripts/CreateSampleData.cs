using AutoUpdater.Core.Models;
using AutoUpdater.Server.Services;
using System.Text.Json;

namespace AutoUpdater.Server.Scripts;

/// <summary>
/// 예제 업데이트 데이터 생성 스크립트
/// </summary>
public class CreateSampleData
{
    private readonly IUpdateStorageService _storageService;
    private readonly ILogger<CreateSampleData> _logger;
    private readonly IConfiguration _configuration;
    private readonly string _baseUrl;

    public CreateSampleData(IUpdateStorageService storageService, ILogger<CreateSampleData> logger, IConfiguration configuration)
    {
        _storageService = storageService;
        _logger = logger;
        _configuration = configuration;
        _baseUrl = _configuration.GetValue<string>("ServerSettings:BaseUrl") ?? "https://localhost:7001";
    }

    /// <summary>
    /// 예제 데이터 생성
    /// </summary>
    public async Task CreateSampleUpdateDataAsync()
    {
        _logger.LogInformation("예제 업데이트 데이터 생성 시작");

        // AutoUpdater.Client.Example 애플리케이션용 업데이트 데이터
        var clientExampleUpdates = new[]
        {
            new UpdateInfo
            {
                Version = "1.1.0",
                DownloadUrl = $"{_baseUrl}/api/updates/AutoUpdaterClientExample/1.1.0/download/AutoUpdaterClientExample_v1.1.0.zip",
                FileSize = 1024 * 1024 * 5, // 5MB
                FileHash = "a1b2c3d4e5f6789012345678901234567890abcdef1234567890abcdef123456",
                ReleaseNotes = "버그 수정 및 성능 개선\n- 업데이트 확인 속도 향상\n- UI 개선\n- 로그 출력 최적화",
                Mandatory = false,
                MinimumVersion = "1.0.0",
                ReleaseDate = DateTime.UtcNow.AddDays(-7),
                Metadata = new Dictionary<string, object>
                {
                    ["platform"] = "Win32NT",
                    ["architecture"] = "x64",
                    ["language"] = "ko-KR",
                    ["category"] = "Minor Update"
                }
            },
            new UpdateInfo
            {
                Version = "2.0.0",
                DownloadUrl = $"{_baseUrl}/api/updates/AutoUpdaterClientExample/2.0.0/download/AutoUpdaterClientExample_v2.0.0.zip",
                FileSize = 1024 * 1024 * 12, // 12MB
                FileHash = "b2c3d4e5f6789012345678901234567890abcdef1234567890abcdef1234567",
                ReleaseNotes = "주요 기능 업데이트\n- 새로운 자동 업데이트 엔진\n- 백그라운드 다운로드 지원\n- 롤백 기능 추가\n- 보안 강화",
                Mandatory = true,
                MinimumVersion = "1.0.0",
                ReleaseDate = DateTime.UtcNow.AddDays(-1),
                Metadata = new Dictionary<string, object>
                {
                    ["platform"] = "Win32NT",
                    ["architecture"] = "x64",
                    ["language"] = "ko-KR",
                    ["category"] = "Major Update",
                    ["criticality"] = "High"
                }
            }
        };

        // TestApp 애플리케이션용 업데이트 데이터
        var testAppUpdates = new[]
        {
            new UpdateInfo
            {
                Version = "1.5.0",
                DownloadUrl = $"{_baseUrl}/api/updates/TestApp/1.5.0/download/TestApp_v1.5.0.msi",
                FileSize = 1024 * 1024 * 25, // 25MB
                FileHash = "c3d4e5f6789012345678901234567890abcdef1234567890abcdef12345678901",
                ReleaseNotes = "안정성 개선 및 새로운 기능\n- 메모리 사용량 최적화\n- 새로운 플러그인 시스템\n- 다국어 지원 확장",
                Mandatory = false,
                MinimumVersion = "1.0.0",
                ReleaseDate = DateTime.UtcNow.AddDays(-3),
                Metadata = new Dictionary<string, object>
                {
                    ["platform"] = "Win32NT",
                    ["architecture"] = "x64",
                    ["language"] = "ko-KR",
                    ["installer"] = "MSI"
                }
            },
            new UpdateInfo
            {
                Version = "2.0.0-beta",
                DownloadUrl = $"{_baseUrl}/api/updates/TestApp/2.0.0-beta/download/TestApp_v2.0.0-beta.zip",
                FileSize = 1024 * 1024 * 30, // 30MB
                FileHash = "d4e5f6789012345678901234567890abcdef1234567890abcdef123456789012",
                ReleaseNotes = "베타 버전 - 테스트 목적\n- 새로운 UI 디자인\n- 클라우드 동기화 기능\n- API 2.0 지원\n\n⚠️ 베타 버전이므로 프로덕션 환경에서 사용하지 마세요.",
                Mandatory = false,
                MinimumVersion = "1.5.0",
                ReleaseDate = DateTime.UtcNow,
                Metadata = new Dictionary<string, object>
                {
                    ["platform"] = "Win32NT",
                    ["architecture"] = "x64",
                    ["language"] = "ko-KR",
                    ["prerelease"] = true,
                    ["channel"] = "beta"
                }
            }
        };

        try
        {
            // AutoUpdaterClientExample 업데이트 저장
            foreach (var update in clientExampleUpdates)
            {
                await _storageService.SaveUpdateInfoAsync("AutoUpdaterClientExample", update);
                _logger.LogInformation("AutoUpdaterClientExample v{Version} 업데이트 데이터 저장됨", update.Version);
            }

            // TestApp 업데이트 저장
            foreach (var update in testAppUpdates)
            {
                await _storageService.SaveUpdateInfoAsync("TestApp", update);
                _logger.LogInformation("TestApp v{Version} 업데이트 데이터 저장됨", update.Version);
            }

            _logger.LogInformation("예제 업데이트 데이터 생성 완료");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "예제 업데이트 데이터 생성 중 오류 발생");
            throw;
        }
    }

    /// <summary>
    /// 예제 업데이트 파일 디렉토리 생성
    /// </summary>
    public void CreateSampleFileDirectories()
    {
        _logger.LogInformation("예제 파일 디렉토리 생성 시작");

        var baseDirectory = "Data/UpdateFiles";
        
        var directories = new[]
        {
            Path.Combine(baseDirectory, "AutoUpdaterClientExample", "1.1.0"),
            Path.Combine(baseDirectory, "AutoUpdaterClientExample", "2.0.0"),
            Path.Combine(baseDirectory, "TestApp", "1.5.0"),
            Path.Combine(baseDirectory, "TestApp", "2.0.0-beta")
        };

        foreach (var directory in directories)
        {
            Directory.CreateDirectory(directory);
            _logger.LogInformation("디렉토리 생성됨: {Directory}", directory);

            // 더미 파일 생성 (실제 환경에서는 실제 업데이트 파일을 배치)
            var fileName = directory.Contains("TestApp") && directory.Contains("1.5.0") ? "TestApp_v1.5.0.msi" :
                          directory.Contains("AutoUpdaterClientExample", StringComparison.OrdinalIgnoreCase) ? 
                          $"AutoUpdaterClientExample_v{Path.GetFileName(directory)}.zip" :
                          $"TestApp_v{Path.GetFileName(directory)}.zip";

            var filePath = Path.Combine(directory, fileName);
            
            if (!File.Exists(filePath))
            {
                // 더미 파일 생성 (실제로는 실제 업데이트 파일을 배치해야 함)
                var dummyContent = $"이것은 {Path.GetFileNameWithoutExtension(fileName)}의 더미 파일입니다.\n생성 시간: {DateTime.Now:yyyy-MM-dd HH:mm:ss}";
                File.WriteAllText(filePath, dummyContent);
                _logger.LogInformation("더미 파일 생성됨: {FilePath}", filePath);
            }
        }

        _logger.LogInformation("예제 파일 디렉토리 생성 완료");
    }
} 