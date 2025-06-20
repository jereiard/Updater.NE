using AutoUpdater.Core.Models;

namespace AutoUpdater.Core.Interfaces;

/// <summary>
/// 자동 업데이트 메인 인터페이스
/// </summary>
public interface IAutoUpdater
{
    /// <summary>
    /// 업데이트 상태 변경 이벤트
    /// </summary>
    event EventHandler<UpdateResult>? UpdateStatusChanged;

    /// <summary>
    /// 업데이트 확인 및 실행
    /// </summary>
    /// <param name="request">업데이트 요청</param>
    /// <param name="autoInstall">자동 설치 여부</param>
    /// <param name="cancellationToken">취소 토큰</param>
    /// <returns>업데이트 결과</returns>
    Task<UpdateResult> CheckAndUpdateAsync(UpdateRequest request, bool autoInstall = false, CancellationToken cancellationToken = default);

    /// <summary>
    /// 업데이트 확인만 수행
    /// </summary>
    /// <param name="request">업데이트 요청</param>
    /// <param name="cancellationToken">취소 토큰</param>
    /// <returns>업데이트 결과</returns>
    Task<UpdateResult> CheckForUpdateAsync(UpdateRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// 업데이트 다운로드
    /// </summary>
    /// <param name="updateInfo">업데이트 정보</param>
    /// <param name="cancellationToken">취소 토큰</param>
    /// <returns>업데이트 결과</returns>
    Task<UpdateResult> DownloadUpdateAsync(UpdateInfo updateInfo, CancellationToken cancellationToken = default);

    /// <summary>
    /// 업데이트 설치
    /// </summary>
    /// <param name="updateFilePath">업데이트 파일 경로</param>
    /// <param name="updateInfo">업데이트 정보</param>
    /// <param name="cancellationToken">취소 토큰</param>
    /// <returns>업데이트 결과</returns>
    Task<UpdateResult> InstallUpdateAsync(string updateFilePath, UpdateInfo updateInfo, CancellationToken cancellationToken = default);

    /// <summary>
    /// 전체 업데이트 프로세스 실행 (확인 + 다운로드 + 설치)
    /// </summary>
    /// <param name="request">업데이트 요청 정보</param>
    /// <param name="cancellationToken">취소 토큰</param>
    /// <returns>업데이트 결과</returns>
    Task<UpdateResult> PerformUpdateAsync(UpdateRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// 자기 업데이트 시작 (현재 실행 중인 애플리케이션 업데이트)
    /// </summary>
    /// <param name="updateInfo">업데이트 정보</param>
    /// <param name="cancellationToken">취소 토큰</param>
    /// <returns>업데이트 결과</returns>
    Task<UpdateResult> InitiateSelfUpdateAsync(UpdateInfo updateInfo, CancellationToken cancellationToken = default);

    /// <summary>
    /// 자동 업데이트 확인 시작
    /// </summary>
    void StartAutoUpdateCheck();

    /// <summary>
    /// 자동 업데이트 확인 중지
    /// </summary>
    void StopAutoUpdateCheck();
} 