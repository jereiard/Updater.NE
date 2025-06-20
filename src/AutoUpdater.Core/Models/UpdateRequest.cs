using System.Text.Json.Serialization;

namespace AutoUpdater.Core.Models;

/// <summary>
/// 업데이트 요청 정보를 담는 모델 클래스
/// </summary>
public class UpdateRequest
{
    /// <summary>
    /// 현재 애플리케이션 버전
    /// </summary>
    [JsonPropertyName("currentVersion")]
    public string CurrentVersion { get; set; } = string.Empty;

    /// <summary>
    /// 애플리케이션 식별자
    /// </summary>
    [JsonPropertyName("applicationId")]
    public string ApplicationId { get; set; } = string.Empty;

    /// <summary>
    /// 플랫폼 정보 (Windows, Linux, macOS 등)
    /// </summary>
    [JsonPropertyName("platform")]
    public string Platform { get; set; } = string.Empty;

    /// <summary>
    /// 아키텍처 정보 (x64, x86, ARM 등)
    /// </summary>
    [JsonPropertyName("architecture")]
    public string Architecture { get; set; } = string.Empty;

    /// <summary>
    /// 언어 코드
    /// </summary>
    [JsonPropertyName("language")]
    public string? Language { get; set; }

    /// <summary>
    /// 클라이언트 고유 식별자
    /// </summary>
    [JsonPropertyName("clientId")]
    public string? ClientId { get; set; }

    /// <summary>
    /// 추가 요청 데이터
    /// </summary>
    [JsonPropertyName("additionalData")]
    public Dictionary<string, object>? AdditionalData { get; set; }
} 