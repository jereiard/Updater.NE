using System.Text.Json.Serialization;

namespace AutoUpdater.Core.Models;

/// <summary>
/// 업데이트 정보를 담는 모델 클래스
/// </summary>
public class UpdateInfo
{
    /// <summary>
    /// 현재 버전
    /// </summary>
    [JsonPropertyName("version")]
    public string Version { get; set; } = string.Empty;

    /// <summary>
    /// 업데이트 다운로드 URL
    /// </summary>
    [JsonPropertyName("downloadUrl")]
    public string DownloadUrl { get; set; } = string.Empty;

    /// <summary>
    /// 업데이트 파일 크기 (바이트)
    /// </summary>
    [JsonPropertyName("fileSize")]
    public long FileSize { get; set; }

    /// <summary>
    /// 파일 해시 (SHA256)
    /// </summary>
    [JsonPropertyName("fileHash")]
    public string FileHash { get; set; } = string.Empty;

    /// <summary>
    /// 릴리스 노트
    /// </summary>
    [JsonPropertyName("releaseNotes")]
    public string ReleaseNotes { get; set; } = string.Empty;

    /// <summary>
    /// 강제 업데이트 여부
    /// </summary>
    [JsonPropertyName("mandatory")]
    public bool Mandatory { get; set; }

    /// <summary>
    /// 최소 지원 버전
    /// </summary>
    [JsonPropertyName("minimumVersion")]
    public string? MinimumVersion { get; set; }

    /// <summary>
    /// 릴리스 날짜
    /// </summary>
    [JsonPropertyName("releaseDate")]
    public DateTime ReleaseDate { get; set; }

    /// <summary>
    /// 추가 메타데이터
    /// </summary>
    [JsonPropertyName("metadata")]
    public Dictionary<string, object>? Metadata { get; set; }
} 