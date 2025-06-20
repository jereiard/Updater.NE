using AutoUpdater.Core.Models;

namespace AutoUpdater.Core.Interfaces;

/// <summary>
/// 업데이트 설치를 담당하는 인터페이스
/// </summary>
public interface IUpdateInstaller
{
    /// <summary>
    /// 설치 진행률 이벤트
    /// </summary>
    event EventHandler<int>? ProgressChanged;

    /// <summary>
    /// 업데이트 설치
    /// </summary>
    /// <param name="updateFilePath">업데이트 파일 경로</param>
    /// <param name="updateInfo">업데이트 정보</param>
    /// <param name="cancellationToken">취소 토큰</param>
    /// <returns>설치 결과</returns>
    Task<UpdateResult> InstallUpdateAsync(string updateFilePath, UpdateInfo updateInfo, CancellationToken cancellationToken = default);

    /// <summary>
    /// 백업 생성
    /// </summary>
    /// <param name="targetPath">대상 경로</param>
    /// <param name="backupPath">백업 경로</param>
    /// <returns>백업 성공 여부</returns>
    Task<bool> CreateBackupAsync(string targetPath, string backupPath);

    /// <summary>
    /// 백업 복원
    /// </summary>
    /// <param name="backupPath">백업 경로</param>
    /// <param name="targetPath">대상 경로</param>
    /// <returns>복원 성공 여부</returns>
    Task<bool> RestoreBackupAsync(string backupPath, string targetPath);
} 