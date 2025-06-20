using AutoUpdater.Core.Models;

namespace AutoUpdater.Core.Interfaces;

/// <summary>
/// 업데이트 다운로드를 담당하는 인터페이스
/// </summary>
public interface IUpdateDownloader
{
    /// <summary>
    /// 업데이트 진행률 이벤트
    /// </summary>
    event EventHandler<int>? ProgressChanged;

    /// <summary>
    /// 업데이트 파일 다운로드
    /// </summary>
    /// <param name="updateInfo">업데이트 정보</param>
    /// <param name="downloadPath">다운로드 경로</param>
    /// <param name="cancellationToken">취소 토큰</param>
    /// <returns>다운로드된 파일 경로</returns>
    Task<string> DownloadUpdateAsync(UpdateInfo updateInfo, string downloadPath, CancellationToken cancellationToken = default);

    /// <summary>
    /// 파일 무결성 검증
    /// </summary>
    /// <param name="filePath">파일 경로</param>
    /// <param name="expectedHash">예상 해시값</param>
    /// <returns>검증 결과</returns>
    Task<bool> VerifyFileIntegrityAsync(string filePath, string expectedHash);
} 