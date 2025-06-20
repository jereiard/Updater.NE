namespace AutoUpdater.Core.Configuration;

/// <summary>
/// 자동 업데이터 설정 클래스
/// </summary>
public class UpdaterConfiguration
{
    /// <summary>
    /// 업데이트 서버 URL
    /// </summary>
    public string ServerUrl { get; set; } = string.Empty;

    /// <summary>
    /// API 키 (인증이 필요한 경우)
    /// </summary>
    public string? ApiKey { get; set; }

    /// <summary>
    /// 애플리케이션 ID
    /// </summary>
    public string ApplicationId { get; set; } = string.Empty;

    /// <summary>
    /// 현재 버전
    /// </summary>
    public string CurrentVersion { get; set; } = "1.0.0";

    /// <summary>
    /// 업데이트 확인 간격 (분)
    /// </summary>
    public int CheckIntervalMinutes { get; set; } = 60;

    /// <summary>
    /// 자동 다운로드 여부
    /// </summary>
    public bool AutoDownload { get; set; } = true;

    /// <summary>
    /// 자동 설치 여부
    /// </summary>
    public bool AutoInstall { get; set; } = false;

    /// <summary>
    /// 다운로드 디렉토리
    /// </summary>
    public string DownloadDirectory { get; set; } = Path.Combine(Path.GetTempPath(), "AutoUpdater");

    /// <summary>
    /// 백업 디렉토리
    /// </summary>
    public string BackupDirectory { get; set; } = Path.Combine(Path.GetTempPath(), "AutoUpdater", "Backup");

    /// <summary>
    /// 타임아웃 (초)
    /// </summary>
    public int TimeoutSeconds { get; set; } = 300;

    /// <summary>
    /// 다운로드 타임아웃 (초)
    /// </summary>
    public int DownloadTimeoutSeconds { get; set; } = 300;

    /// <summary>
    /// 재시도 횟수
    /// </summary>
    public int RetryCount { get; set; } = 3;

    /// <summary>
    /// 최대 재시도 횟수
    /// </summary>
    public int MaxRetryAttempts { get; set; } = 3;

    /// <summary>
    /// 백업 활성화 여부
    /// </summary>
    public bool BackupEnabled { get; set; } = true;

    /// <summary>
    /// 프록시 설정
    /// </summary>
    public ProxyConfiguration? Proxy { get; set; }

    /// <summary>
    /// 사용자 에이전트
    /// </summary>
    public string UserAgent { get; set; } = "AutoUpdater/1.0";

    /// <summary>
    /// 추가 HTTP 헤더
    /// </summary>
    public Dictionary<string, string>? AdditionalHeaders { get; set; }

    /// <summary>
    /// 로깅 활성화 여부
    /// </summary>
    public bool EnableLogging { get; set; } = true;

    /// <summary>
    /// 디버그 모드 여부
    /// </summary>
    public bool DebugMode { get; set; } = false;

    /// <summary>
    /// 프로세스 종료 대기 시간 (초)
    /// </summary>
    public int ProcessTerminationTimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// 강제 프로세스 종료 허용 여부
    /// </summary>
    public bool AllowForceProcessTermination { get; set; } = false;

    /// <summary>
    /// 자기 업데이트 활성화 여부
    /// </summary>
    public bool EnableSelfUpdate { get; set; } = true;

    /// <summary>
    /// 업데이트 런처 파일명
    /// </summary>
    public string LauncherFileName { get; set; } = "AutoUpdater.Launcher.exe";

    /// <summary>
    /// SSL 인증서 설정
    /// </summary>
    public SslConfiguration Ssl { get; set; } = new();
}

/// <summary>
/// 프록시 설정 클래스
/// </summary>
public class ProxyConfiguration
{
    /// <summary>
    /// 프록시 서버 URL
    /// </summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>
    /// 사용자명
    /// </summary>
    public string? Username { get; set; }

    /// <summary>
    /// 비밀번호
    /// </summary>
    public string? Password { get; set; }

    /// <summary>
    /// 도메인
    /// </summary>
    public string? Domain { get; set; }
}

/// <summary>
/// SSL 인증서 설정 클래스
/// </summary>
public class SslConfiguration
{
    /// <summary>
    /// 사설 인증서 허용 여부
    /// </summary>
    public bool AllowSelfSignedCertificates { get; set; } = false;

    /// <summary>
    /// 인증서 체인 검증 무시 여부
    /// </summary>
    public bool IgnoreCertificateChainErrors { get; set; } = false;

    /// <summary>
    /// 인증서 이름 불일치 무시 여부
    /// </summary>
    public bool IgnoreCertificateNameMismatch { get; set; } = false;

    /// <summary>
    /// 모든 SSL 오류 무시 여부 (개발용, 프로덕션에서는 사용 금지)
    /// </summary>
    public bool IgnoreAllSslErrors { get; set; } = false;

    /// <summary>
    /// 신뢰할 인증서 지문 목록 (SHA256)
    /// </summary>
    public List<string> TrustedCertificateThumbprints { get; set; } = new();

    /// <summary>
    /// 클라이언트 인증서 파일 경로 (.pfx 또는 .p12)
    /// </summary>
    public string? ClientCertificatePath { get; set; }

    /// <summary>
    /// 클라이언트 인증서 비밀번호
    /// </summary>
    public string? ClientCertificatePassword { get; set; }
} 