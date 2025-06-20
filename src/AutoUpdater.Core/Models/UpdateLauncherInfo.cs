using System.Text.Json.Serialization;

namespace AutoUpdater.Core.Models;

/// <summary>
/// 업데이트 런처에 전달할 정보
/// </summary>
public class UpdateLauncherInfo
{
    /// <summary>
    /// 다운로드된 업데이트 파일 경로
    /// </summary>
    [JsonPropertyName("updateFilePath")]
    public string UpdateFilePath { get; set; } = string.Empty;

    /// <summary>
    /// 업데이트할 대상 실행 파일 경로
    /// </summary>
    [JsonPropertyName("targetExecutablePath")]
    public string TargetExecutablePath { get; set; } = string.Empty;

    /// <summary>
    /// 종료할 프로세스 ID
    /// </summary>
    [JsonPropertyName("processId")]
    public int ProcessId { get; set; }

    /// <summary>
    /// 업데이트 후 애플리케이션 재시작 여부
    /// </summary>
    [JsonPropertyName("restartAfterUpdate")]
    public bool RestartAfterUpdate { get; set; } = true;

    /// <summary>
    /// 업데이트 파일 형식 (zip, exe, msi 등)
    /// </summary>
    [JsonPropertyName("updateFileType")]
    public string UpdateFileType { get; set; } = string.Empty;

    /// <summary>
    /// 백업 디렉토리 경로
    /// </summary>
    [JsonPropertyName("backupDirectory")]
    public string BackupDirectory { get; set; } = string.Empty;

    /// <summary>
    /// 프로세스 종료 대기 시간(초)
    /// </summary>
    [JsonPropertyName("waitTimeoutSeconds")]
    public int WaitTimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// 강제 종료 허용 여부
    /// </summary>
    [JsonPropertyName("allowForceKill")]
    public bool AllowForceKill { get; set; } = false;

    /// <summary>
    /// 업데이트 후 실행할 추가 명령어 인수
    /// </summary>
    [JsonPropertyName("restartArguments")]
    public string RestartArguments { get; set; } = string.Empty;
} 