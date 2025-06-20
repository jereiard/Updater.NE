using AutoUpdater.Core.Configuration;
using AutoUpdater.Core.Interfaces;
using AutoUpdater.Core.Models;
using AutoUpdater.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

Console.WriteLine("=== 자동 업데이터 클라이언트 예제 ===");
Console.WriteLine();

// 서비스 컨테이너 구성
var services = new ServiceCollection();

// 로깅 설정
services.AddLogging(builder =>
{
    builder.AddConsole();
    builder.SetMinimumLevel(LogLevel.Information);
});

// HTTP 클라이언트 등록
services.AddHttpClient();

// 업데이터 설정
var configuration = new UpdaterConfiguration
{
    ServerUrl = "https://192.168.8.210:7001", // 업데이트 서버 URL
    ApplicationId = "TestApp",
    CurrentVersion = "1.0.0",
    CheckIntervalMinutes = 0, // 수동 확인
    AutoDownload = false,
    AutoInstall = false,
    EnableLogging = true,
    DebugMode = true
};

services.AddSingleton(configuration);

// AutoUpdater 서비스 등록
services.AddTransient<IUpdateChecker, WebUpdateChecker>();
services.AddTransient<IUpdateDownloader, HttpUpdateDownloader>();
services.AddTransient<IUpdateInstaller, DefaultUpdateInstaller>();
services.AddTransient<IAutoUpdater, AutoUpdaterService>();

var serviceProvider = services.BuildServiceProvider();

try
{
    // AutoUpdater 서비스 가져오기
    var autoUpdater = serviceProvider.GetRequiredService<IAutoUpdater>();
    var logger = serviceProvider.GetRequiredService<ILogger<Program>>();

    // 이벤트 구독
    autoUpdater.UpdateStatusChanged += (sender, result) =>
    {
        Console.WriteLine($"[이벤트] 상태 변경: {result.ResultType}");
        if (result.Progress > 0)
        {
            Console.WriteLine($"[이벤트] 진행률: {result.Progress}%");
        }
        if (!string.IsNullOrEmpty(result.ErrorMessage))
        {
            Console.WriteLine($"[이벤트] 오류: {result.ErrorMessage}");
        }
    };

    // 업데이트 요청 생성
    var updateRequest = new UpdateRequest
    {
        ApplicationId = configuration.ApplicationId,
        CurrentVersion = configuration.CurrentVersion,
        Platform = Environment.OSVersion.Platform.ToString(),
        Architecture = Environment.Is64BitProcess ? "x64" : "x86",
        Language = System.Globalization.CultureInfo.CurrentCulture.Name,
        ClientId = Environment.MachineName
    };

    Console.WriteLine($"현재 버전: {updateRequest.CurrentVersion}");
    Console.WriteLine($"플랫폼: {updateRequest.Platform} ({updateRequest.Architecture})");
    Console.WriteLine($"서버 URL: {configuration.ServerUrl}");
    Console.WriteLine();

    // 메뉴 루프
    while (true)
    {
        Console.WriteLine("=== 메뉴 ===");
        Console.WriteLine("1. 업데이트 확인");
        Console.WriteLine("2. 업데이트 확인 및 다운로드");
        Console.WriteLine("3. 업데이트 확인, 다운로드 및 설치");
        Console.WriteLine("4. 자기 업데이트 (Self Update)");
        Console.WriteLine("5. 설정 변경");
        Console.WriteLine("0. 종료");
        Console.Write("선택: ");

        var choice = Console.ReadLine();
        Console.WriteLine();

        try
        {
            switch (choice)
            {
                case "1":
                    await CheckForUpdate(autoUpdater, updateRequest);
                    break;

                case "2":
                    await CheckAndDownload(autoUpdater, updateRequest);
                    break;

                case "3":
                    await CheckDownloadAndInstall(autoUpdater, updateRequest);
                    break;

                case "4":
                    await PerformSelfUpdate(autoUpdater, updateRequest);
                    break;

                case "5":
                    ChangeSettings(configuration, updateRequest);
                    break;

                case "0":
                    Console.WriteLine("프로그램을 종료합니다.");
                    return;

                default:
                    Console.WriteLine("잘못된 선택입니다.");
                    break;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"오류 발생: {ex.Message}");
            logger.LogError(ex, "작업 실행 중 오류 발생");
        }

        Console.WriteLine();
        Console.WriteLine("아무 키나 누르면 계속...");
        Console.ReadKey();
        Console.Clear();
    }
}
catch (Exception ex)
{
    Console.WriteLine($"초기화 오류: {ex.Message}");
}
finally
{
    serviceProvider.Dispose();
}

/// <summary>
/// 업데이트 확인
/// </summary>
static async Task CheckForUpdate(IAutoUpdater autoUpdater, UpdateRequest request)
{
    Console.WriteLine("업데이트 확인 중...");
    
    var result = await autoUpdater.CheckForUpdateAsync(request);
    
    Console.WriteLine($"결과: {result.ResultType}");
    
    if (result.IsSuccess && result.UpdateInfo != null)
    {
        var updateInfo = result.UpdateInfo;
        Console.WriteLine($"새 버전: {updateInfo.Version}");
        Console.WriteLine($"파일 크기: {updateInfo.FileSize / (1024 * 1024):F1} MB");
        Console.WriteLine($"다운로드 URL: {updateInfo.DownloadUrl}");
        Console.WriteLine($"릴리스 노트: {updateInfo.ReleaseNotes}");
        Console.WriteLine($"강제 업데이트: {(updateInfo.Mandatory ? "예" : "아니오")}");
        Console.WriteLine($"릴리스 날짜: {updateInfo.ReleaseDate:yyyy-MM-dd HH:mm:ss}");
    }
    else if (!result.IsSuccess)
    {
        Console.WriteLine($"오류: {result.ErrorMessage}");
    }
}

/// <summary>
/// 업데이트 확인 및 다운로드
/// </summary>
static async Task CheckAndDownload(IAutoUpdater autoUpdater, UpdateRequest request)
{
    Console.WriteLine("업데이트 확인 및 다운로드 중...");
    
    var checkResult = await autoUpdater.CheckForUpdateAsync(request);
    
    if (checkResult.IsSuccess && checkResult.ResultType == UpdateResultType.UpdateAvailable)
    {
        Console.WriteLine($"새 버전 발견: {checkResult.UpdateInfo!.Version}");
        Console.WriteLine("다운로드를 시작합니다...");
        
        var downloadResult = await autoUpdater.DownloadUpdateAsync(checkResult.UpdateInfo!);
        
        if (downloadResult.IsSuccess)
        {
            Console.WriteLine("다운로드 완료!");
            var filePath = downloadResult.AdditionalData?["FilePath"]?.ToString();
            Console.WriteLine($"다운로드된 파일: {filePath}");
        }
        else
        {
            Console.WriteLine($"다운로드 실패: {downloadResult.ErrorMessage}");
        }
    }
    else if (checkResult.ResultType == UpdateResultType.NoUpdateAvailable)
    {
        Console.WriteLine("업데이트가 필요하지 않습니다.");
    }
    else
    {
        Console.WriteLine($"업데이트 확인 실패: {checkResult.ErrorMessage}");
    }
}

/// <summary>
/// 업데이트 확인, 다운로드 및 설치
/// </summary>
static async Task CheckDownloadAndInstall(IAutoUpdater autoUpdater, UpdateRequest request)
{
    Console.WriteLine("전체 업데이트 프로세스를 시작합니다...");
    
    var result = await autoUpdater.CheckAndUpdateAsync(request, autoInstall: true);
    
    Console.WriteLine($"최종 결과: {result.ResultType}");
    
    if (!result.IsSuccess)
    {
        Console.WriteLine($"오류: {result.ErrorMessage}");
    }
    else
    {
        switch (result.ResultType)
        {
            case UpdateResultType.NoUpdateAvailable:
                Console.WriteLine("업데이트가 필요하지 않습니다.");
                break;
            case UpdateResultType.Completed:
                Console.WriteLine("업데이트가 성공적으로 완료되었습니다!");
                Console.WriteLine("애플리케이션을 재시작해야 할 수 있습니다.");
                break;
            default:
                Console.WriteLine($"업데이트 상태: {result.ResultType}");
                break;
        }
    }
}

/// <summary>
/// 자기 업데이트 수행
/// </summary>
static async Task PerformSelfUpdate(IAutoUpdater autoUpdater, UpdateRequest request)
{
    Console.WriteLine("=== 자기 업데이트 ===");
    Console.WriteLine("주의: 이 기능은 현재 실행 중인 애플리케이션을 업데이트하고 재시작합니다.");
    Console.WriteLine("계속하시겠습니까? (y/N)");
    
    var confirm = Console.ReadLine();
    if (!string.Equals(confirm, "y", StringComparison.OrdinalIgnoreCase) && 
        !string.Equals(confirm, "yes", StringComparison.OrdinalIgnoreCase))
    {
        Console.WriteLine("자기 업데이트가 취소되었습니다.");
        return;
    }

    Console.WriteLine("업데이트를 확인하는 중...");
    
    // 1. 업데이트 확인
    var checkResult = await autoUpdater.CheckForUpdateAsync(request);
    
    if (!checkResult.IsSuccess)
    {
        Console.WriteLine($"업데이트 확인 실패: {checkResult.ErrorMessage}");
        return;
    }

    if (checkResult.ResultType == UpdateResultType.NoUpdateAvailable)
    {
        Console.WriteLine("업데이트가 필요하지 않습니다.");
        return;
    }

    var updateInfo = checkResult.UpdateInfo!;
    Console.WriteLine($"새 버전 발견: {updateInfo.Version}");
    Console.WriteLine($"파일 크기: {updateInfo.FileSize / (1024 * 1024):F1} MB");
    Console.WriteLine($"릴리스 노트: {updateInfo.ReleaseNotes}");
    
    if (updateInfo.Mandatory)
    {
        Console.WriteLine("⚠️  이것은 강제 업데이트입니다.");
    }

    Console.WriteLine();
    Console.WriteLine("자기 업데이트를 시작합니다...");
    Console.WriteLine("이 프로세스는 현재 애플리케이션을 종료하고 업데이트 런처를 실행합니다.");
    
    try
    {
        // 2. 자기 업데이트 시작
        var result = await autoUpdater.InitiateSelfUpdateAsync(updateInfo);
        
        // 이 지점에 도달하면 자기 업데이트가 실패한 것입니다.
        // 성공하면 Environment.Exit(0)가 호출되어 프로세스가 종료됩니다.
        Console.WriteLine($"자기 업데이트 실패: {result.ErrorMessage}");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"자기 업데이트 중 오류 발생: {ex.Message}");
    }
}

/// <summary>
/// 설정 변경
/// </summary>
static void ChangeSettings(UpdaterConfiguration configuration, UpdateRequest request)
{
    Console.WriteLine("=== 설정 변경 ===");
    Console.WriteLine($"1. 서버 URL (현재: {configuration.ServerUrl})");
    Console.WriteLine($"2. 애플리케이션 ID (현재: {configuration.ApplicationId})");
    Console.WriteLine($"3. 현재 버전 (현재: {configuration.CurrentVersion})");
    Console.WriteLine("0. 돌아가기");
    Console.Write("선택: ");
    
    var choice = Console.ReadLine();
    
    switch (choice)
    {
        case "1":
            Console.Write("새 서버 URL: ");
            var newUrl = Console.ReadLine();
            if (!string.IsNullOrWhiteSpace(newUrl))
            {
                configuration.ServerUrl = newUrl.Trim();
                Console.WriteLine("서버 URL이 변경되었습니다.");
            }
            break;
            
        case "2":
            Console.Write("새 애플리케이션 ID: ");
            var newAppId = Console.ReadLine();
            if (!string.IsNullOrWhiteSpace(newAppId))
            {
                configuration.ApplicationId = newAppId.Trim();
                request.ApplicationId = newAppId.Trim();
                Console.WriteLine("애플리케이션 ID가 변경되었습니다.");
            }
            break;
            
        case "3":
            Console.Write("새 버전: ");
            var newVersion = Console.ReadLine();
            if (!string.IsNullOrWhiteSpace(newVersion))
            {
                configuration.CurrentVersion = newVersion.Trim();
                request.CurrentVersion = newVersion.Trim();
                Console.WriteLine("현재 버전이 변경되었습니다.");
            }
            break;
            
        case "0":
            break;
            
        default:
            Console.WriteLine("잘못된 선택입니다.");
            break;
    }
} 