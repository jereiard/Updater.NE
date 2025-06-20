namespace AutoUpdater.Core.Models;

/// <summary>
/// 업데이트 결과를 나타내는 열거형
/// </summary>
public enum UpdateResultType
{
    /// <summary>
    /// 업데이트가 필요하지 않음
    /// </summary>
    NoUpdateAvailable,

    /// <summary>
    /// 업데이트가 사용 가능함
    /// </summary>
    UpdateAvailable,

    /// <summary>
    /// 업데이트 다운로드 중
    /// </summary>
    Downloading,

    /// <summary>
    /// 업데이트 다운로드 완료
    /// </summary>
    Downloaded,

    /// <summary>
    /// 업데이트 설치 중
    /// </summary>
    Installing,

    /// <summary>
    /// 업데이트 완료
    /// </summary>
    Completed,

    /// <summary>
    /// 업데이트 실패
    /// </summary>
    Failed,

    /// <summary>
    /// 업데이트 취소됨
    /// </summary>
    Cancelled
}

/// <summary>
/// 업데이트 결과를 담는 클래스
/// </summary>
public class UpdateResult
{
    /// <summary>
    /// 업데이트 결과 타입
    /// </summary>
    public UpdateResultType ResultType { get; set; }

    /// <summary>
    /// 업데이트 정보 (사용 가능한 경우)
    /// </summary>
    public UpdateInfo? UpdateInfo { get; set; }

    /// <summary>
    /// 오류 메시지 (실패한 경우)
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// 진행률 (0-100)
    /// </summary>
    public int Progress { get; set; }

    /// <summary>
    /// 추가 데이터
    /// </summary>
    public Dictionary<string, object>? AdditionalData { get; set; }

    /// <summary>
    /// 성공 여부
    /// </summary>
    public bool IsSuccess => ResultType != UpdateResultType.Failed;

    /// <summary>
    /// 성공 결과 생성
    /// </summary>
    public static UpdateResult Success(UpdateResultType resultType, UpdateInfo? updateInfo = null, int progress = 0)
    {
        return new UpdateResult
        {
            ResultType = resultType,
            UpdateInfo = updateInfo,
            Progress = progress
        };
    }

    /// <summary>
    /// 실패 결과 생성
    /// </summary>
    public static UpdateResult Failure(string errorMessage)
    {
        return new UpdateResult
        {
            ResultType = UpdateResultType.Failed,
            ErrorMessage = errorMessage
        };
    }
} 