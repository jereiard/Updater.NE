using AutoUpdater.Core.Configuration;
using AutoUpdater.Core.Interfaces;
using AutoUpdater.Core.Services;
using AutoUpdater.Core.Utilities;
using AutoUpdater.Server.Services;
using AutoUpdater.Server.Scripts;
using System.Net;
using Microsoft.Extensions.Configuration;

var builder = WebApplication.CreateBuilder(args);

// 설정에서 포트 및 바인딩 주소 읽기
var httpsPort = builder.Configuration.GetValue<int>("ServerSettings:HttpsPort", 7001);
var httpPort = builder.Configuration.GetValue<int>("ServerSettings:HttpPort", 7002);
var bindAddress = builder.Configuration.GetValue<string>("ServerSettings:BindAddress", "0.0.0.0");

// 동적 URL 구성
var httpsUrl = $"https://{bindAddress}:{httpsPort}";
var httpUrl = $"http://{bindAddress}:{httpPort}";

builder.WebHost.UseUrls(httpsUrl, httpUrl);

var logger = LoggerFactory.Create(config => config.AddConsole()).CreateLogger<Program>();
logger.LogInformation("서버 바인딩 설정: HTTPS={HttpsUrl}, HTTP={HttpUrl}", httpsUrl, httpUrl);

// 서비스 등록
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { 
        Title = "AutoUpdater Server API", 
        Version = "v1",
        Description = "웹서버 기반 자동 업데이트 서비스 API (192.168.8.0/24 전용)"
    });
});

// CORS 설정 - 192.168.8.0/24 네트워크만 허용
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.SetIsOriginAllowed(origin =>
        {
            if (Uri.TryCreate(origin, UriKind.Absolute, out var uri))
            {
                var host = uri.Host;
                
                // 로컬호스트는 개발용으로 허용
                if (host == "localhost" || host == "127.0.0.1")
                    return true;
                
                // 192.168.8.0/24 네트워크 확인
                if (IPAddress.TryParse(host, out var ip))
                {
                    var bytes = ip.GetAddressBytes();
                    if (bytes.Length == 4 && bytes[0] == 192 && bytes[1] == 168 && bytes[2] == 8)
                    {
                        return true;
                    }
                }
            }
            return false;
        })
        .AllowAnyMethod()
        .AllowAnyHeader()
        .AllowCredentials();
    });
});

// 로깅 설정
builder.Services.AddLogging(configure =>
{
    configure.AddConsole();
    configure.AddDebug();
});

// HTTP 클라이언트 등록 - SSL 설정 적용
builder.Services.AddHttpClient<IUpdateChecker, WebUpdateChecker>((serviceProvider, httpClient) =>
{
    var configuration = serviceProvider.GetRequiredService<UpdaterConfiguration>();
    var logger = serviceProvider.GetRequiredService<ILogger<WebUpdateChecker>>();
    
    // SSL 설정 적용
    HttpClientFactory.SetLogger(logger);
    SslCertificateValidator.SetLogger(logger);
    
    // HttpClient 기본 설정만 적용 (SSL은 Handler에서 처리)
    httpClient.Timeout = TimeSpan.FromSeconds(configuration.TimeoutSeconds);
    httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(configuration.UserAgent);
    
    if (!string.IsNullOrEmpty(configuration.ApiKey))
    {
        httpClient.DefaultRequestHeaders.Add("X-API-Key", configuration.ApiKey);
    }
})
.ConfigurePrimaryHttpMessageHandler(serviceProvider =>
{
    var configuration = serviceProvider.GetRequiredService<UpdaterConfiguration>();
    var handler = new HttpClientHandler();
    
    // SSL 설정 적용
    HttpClientFactory.ConfigureSslSettings(handler, configuration.Ssl);
    
    // 프록시 설정 적용
    if (configuration.Proxy != null)
    {
        HttpClientFactory.ConfigureProxy(handler, configuration.Proxy);
    }
    
    return handler;
});

builder.Services.AddHttpClient<IUpdateDownloader, HttpUpdateDownloader>((serviceProvider, httpClient) =>
{
    var configuration = serviceProvider.GetRequiredService<UpdaterConfiguration>();
    httpClient.Timeout = TimeSpan.FromSeconds(configuration.DownloadTimeoutSeconds);
})
.ConfigurePrimaryHttpMessageHandler(serviceProvider =>
{
    var configuration = serviceProvider.GetRequiredService<UpdaterConfiguration>();
    var handler = new HttpClientHandler();
    
    // SSL 설정 적용
    HttpClientFactory.ConfigureSslSettings(handler, configuration.Ssl);
    
    return handler;
});

// AutoUpdater 서비스 등록
builder.Services.AddSingleton<UpdaterConfiguration>(provider =>
{
    var configuration = provider.GetRequiredService<IConfiguration>();
    var baseUrl = configuration.GetValue<string>("ServerSettings:BaseUrl") ?? "https://localhost:7001";
    
    return new UpdaterConfiguration
    {
        ServerUrl = baseUrl,
        ApplicationId = "AutoUpdaterServer",
        CurrentVersion = "1.0.0",
        CheckIntervalMinutes = 60,
        AutoDownload = false,
        AutoInstall = false,
        EnableLogging = true,
        DownloadTimeoutSeconds = 300,
        MaxRetryAttempts = 3,
        BackupEnabled = true
    };
});

builder.Services.AddScoped<IUpdateChecker, WebUpdateChecker>();
builder.Services.AddScoped<IUpdateDownloader, HttpUpdateDownloader>();
builder.Services.AddScoped<IUpdateInstaller, DefaultUpdateInstaller>();
builder.Services.AddScoped<IAutoUpdater, AutoUpdaterService>();

// 파일 기반 스토리지 서비스 등록
builder.Services.AddSingleton<IUpdateStorageService, FileUpdateStorageService>();

// 예제 데이터 생성 서비스 등록
builder.Services.AddTransient<CreateSampleData>();

var app = builder.Build();

// 예제 데이터 생성 (개발 환경에서만)
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var sampleDataService = scope.ServiceProvider.GetRequiredService<CreateSampleData>();
    var appLogger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    
    try
    {
        appLogger.LogInformation("예제 데이터 초기화 시작");
        
        // 파일 디렉토리 생성
        sampleDataService.CreateSampleFileDirectories();
        
        // 업데이트 데이터 생성
        await sampleDataService.CreateSampleUpdateDataAsync();
        
        appLogger.LogInformation("예제 데이터 초기화 완료");
    }
    catch (Exception ex)
    {
        appLogger.LogError(ex, "예제 데이터 초기화 중 오류 발생");
    }
}

// IP 필터링 미들웨어 추가
app.Use(async (context, next) =>
{
    var remoteIp = context.Connection.RemoteIpAddress;
    
    if (remoteIp != null)
    {
        // IPv6 로컬호스트를 IPv4로 변환
        if (remoteIp.IsIPv4MappedToIPv6)
        {
            remoteIp = remoteIp.MapToIPv4();
        }
        
        // 로컬호스트는 개발용으로 허용
        if (IPAddress.IsLoopback(remoteIp))
        {
            await next();
            return;
        }
        
        // 192.168.8.0/24 네트워크만 허용
        var bytes = remoteIp.GetAddressBytes();
        if (bytes.Length == 4 && bytes[0] == 192 && bytes[1] == 168 && bytes[2] == 8)
        {
            await next();
            return;
        }
        
        // 접근 거부
        context.Response.StatusCode = 403;
        await context.Response.WriteAsync("Access denied. Only 192.168.8.0/24 network is allowed.");
        return;
    }
    
    await next();
});

// 개발 환경에서 Swagger 활성화
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "AutoUpdater Server API v1");
        c.RoutePrefix = string.Empty; // Swagger UI를 루트 경로에서 제공
    });
}

app.UseHttpsRedirection();
app.UseCors();
app.UseAuthorization();
app.MapControllers();

// 헬스 체크 엔드포인트
app.MapGet("/health", (IConfiguration config) => new { 
    status = "healthy", 
    timestamp = DateTime.UtcNow,
    version = "1.0.0",
    network = "192.168.8.0/24",
    httpsPort = config.GetValue<int>("ServerSettings:HttpsPort", 7001),
    httpPort = config.GetValue<int>("ServerSettings:HttpPort", 7002),
    bindAddress = config.GetValue<string>("ServerSettings:BindAddress", "0.0.0.0")
});

// 루트 엔드포인트
app.MapGet("/", (IConfiguration config) => new { 
    message = "AutoUpdater Server API", 
    version = "1.0.0",
    documentation = "/swagger",
    allowedNetwork = "192.168.8.0/24",
    httpsPort = config.GetValue<int>("ServerSettings:HttpsPort", 7001),
    httpPort = config.GetValue<int>("ServerSettings:HttpPort", 7002)
});

app.Run(); 