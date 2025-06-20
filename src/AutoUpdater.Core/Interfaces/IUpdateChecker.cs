using AutoUpdater.Core.Models;

namespace AutoUpdater.Core.Interfaces;

/// <summary>
/// 업데이트 확인을 담당하는 인터페이스
/// </summary>
public interface IUpdateChecker
{
    /// <summary>
    /// 업데이트 확인
    /// </summary>
    /// <param name="request">업데이트 요청 정보</param>
    /// <param name="cancellationToken">취소 토큰</param>
    /// <returns>업데이트 결과</returns>
    Task<UpdateResult> CheckForUpdateAsync(UpdateRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// 업데이트 정보 가져오기
    /// </summary>
    /// <param name="request">업데이트 요청 정보</param>
    /// <param name="cancellationToken">취소 토큰</param>
    /// <returns>업데이트 정보</returns>
    Task<UpdateInfo?> GetUpdateInfoAsync(UpdateRequest request, CancellationToken cancellationToken = default);
} 