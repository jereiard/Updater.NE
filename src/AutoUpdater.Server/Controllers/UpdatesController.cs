using AutoUpdater.Core.Models;
using AutoUpdater.Server.Services;
using Microsoft.AspNetCore.Mvc;

namespace AutoUpdater.Server.Controllers;

/// <summary>
/// 업데이트 API 컨트롤러
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class UpdatesController : ControllerBase
{
    private readonly ILogger<UpdatesController> _logger;
    private readonly IUpdateStorageService _storageService;

    public UpdatesController(ILogger<UpdatesController> logger, IUpdateStorageService storageService)
    {
        _logger = logger;
        _storageService = storageService;
    }

    /// <summary>
    /// 업데이트 정보 조회
    /// </summary>
    /// <param name="applicationId">애플리케이션 ID</param>
    /// <param name="currentVersion">현재 버전</param>
    /// <param name="platform">플랫폼</param>
    /// <param name="architecture">아키텍처</param>
    /// <param name="language">언어</param>
    /// <param name="clientId">클라이언트 ID</param>
    /// <returns>업데이트 정보</returns>
    [HttpGet("{applicationId}")]
    public async Task<IActionResult> GetUpdateInfo(
        string applicationId,
        [FromQuery] string currentVersion,
        [FromQuery] string platform,
        [FromQuery] string architecture,
        [FromQuery] string? language = null,
        [FromQuery] string? clientId = null)
    {
        try
        {
            _logger.LogInformation("업데이트 정보 요청: {ApplicationId} v{CurrentVersion} ({Platform}/{Architecture})",
                applicationId, currentVersion, platform, architecture);

            var updateInfo = await _storageService.GetUpdateInfoAsync(applicationId, currentVersion, platform, architecture);

            if (updateInfo == null)
            {
                _logger.LogInformation("업데이트 정보를 찾을 수 없습니다: {ApplicationId}", applicationId);
                return NotFound(new { message = "사용 가능한 업데이트가 없습니다." });
            }

            _logger.LogInformation("업데이트 정보 반환: v{Version}", updateInfo.Version);
            return Ok(updateInfo);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "업데이트 정보 조회 중 오류가 발생했습니다.");
            return StatusCode(500, new { error = "내부 서버 오류가 발생했습니다." });
        }
    }

    /// <summary>
    /// 업데이트 정보 등록/수정
    /// </summary>
    /// <param name="applicationId">애플리케이션 ID</param>
    /// <param name="updateInfo">업데이트 정보</param>
    /// <returns>결과</returns>
    [HttpPost("{applicationId}")]
    public async Task<IActionResult> CreateOrUpdateInfo(string applicationId, [FromBody] UpdateInfo updateInfo)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            _logger.LogInformation("업데이트 정보 등록/수정: {ApplicationId} v{Version}",
                applicationId, updateInfo.Version);

            await _storageService.SaveUpdateInfoAsync(applicationId, updateInfo);

            _logger.LogInformation("업데이트 정보 저장 완료");
            return Ok(new { message = "업데이트 정보가 성공적으로 저장되었습니다." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "업데이트 정보 저장 중 오류가 발생했습니다.");
            return StatusCode(500, new { error = "내부 서버 오류가 발생했습니다." });
        }
    }

    /// <summary>
    /// 업데이트 정보 삭제
    /// </summary>
    /// <param name="applicationId">애플리케이션 ID</param>
    /// <param name="version">버전</param>
    /// <returns>결과</returns>
    [HttpDelete("{applicationId}/{version}")]
    public async Task<IActionResult> DeleteUpdateInfo(string applicationId, string version)
    {
        try
        {
            _logger.LogInformation("업데이트 정보 삭제: {ApplicationId} v{Version}", applicationId, version);

            var deleted = await _storageService.DeleteUpdateInfoAsync(applicationId, version);
            if (!deleted)
            {
                return NotFound(new { message = "삭제할 업데이트 정보를 찾을 수 없습니다." });
            }

            _logger.LogInformation("업데이트 정보 삭제 완료");
            return Ok(new { message = "업데이트 정보가 성공적으로 삭제되었습니다." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "업데이트 정보 삭제 중 오류가 발생했습니다.");
            return StatusCode(500, new { error = "내부 서버 오류가 발생했습니다." });
        }
    }

    /// <summary>
    /// 애플리케이션의 모든 업데이트 정보 조회
    /// </summary>
    /// <param name="applicationId">애플리케이션 ID</param>
    /// <returns>업데이트 정보 목록</returns>
    [HttpGet("{applicationId}/all")]
    public async Task<IActionResult> GetAllUpdateInfo(string applicationId)
    {
        try
        {
            _logger.LogInformation("모든 업데이트 정보 조회: {ApplicationId}", applicationId);

            var updateInfoList = await _storageService.GetAllUpdateInfoAsync(applicationId);
            
            _logger.LogInformation("업데이트 정보 {Count}개 반환", updateInfoList.Count);
            return Ok(updateInfoList);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "업데이트 정보 목록 조회 중 오류가 발생했습니다.");
            return StatusCode(500, new { error = "내부 서버 오류가 발생했습니다." });
        }
    }

    /// <summary>
    /// 모든 애플리케이션 ID 조회
    /// </summary>
    /// <returns>애플리케이션 ID 목록</returns>
    [HttpGet]
    public async Task<IActionResult> GetApplicationIds()
    {
        try
        {
            _logger.LogInformation("애플리케이션 ID 목록 조회");

            var applicationIds = await _storageService.GetApplicationIdsAsync();
            
            _logger.LogInformation("애플리케이션 ID {Count}개 반환", applicationIds.Count);
            return Ok(new { applicationIds });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "애플리케이션 ID 목록 조회 중 오류가 발생했습니다.");
            return StatusCode(500, new { error = "내부 서버 오류가 발생했습니다." });
        }
    }

    /// <summary>
    /// 업데이트 파일 다운로드
    /// </summary>
    /// <param name="applicationId">애플리케이션 ID</param>
    /// <param name="version">버전</param>
    /// <param name="fileName">파일명</param>
    /// <returns>파일</returns>
    [HttpGet("{applicationId}/{version}/download/{fileName}")]
    public async Task<IActionResult> DownloadUpdateFile(string applicationId, string version, string fileName)
    {
        try
        {
            _logger.LogInformation("업데이트 파일 다운로드 요청: {ApplicationId} v{Version} - {FileName}", 
                applicationId, version, fileName);

            // 파일 경로 구성 (실제 구현에서는 보안을 위해 더 엄격한 검증 필요)
            var updatesDirectory = Path.Combine("Data", "UpdateFiles", applicationId, version);
            var filePath = Path.Combine(updatesDirectory, fileName);

            if (!System.IO.File.Exists(filePath))
            {
                _logger.LogWarning("요청된 파일이 존재하지 않음: {FilePath}", filePath);
                return NotFound(new { message = "요청된 파일을 찾을 수 없습니다." });
            }

            // 파일 확장자에 따른 MIME 타입 설정
            var contentType = GetContentType(fileName);
            var fileBytes = await System.IO.File.ReadAllBytesAsync(filePath);

            _logger.LogInformation("업데이트 파일 다운로드 시작: {FileName} ({Size} bytes)", fileName, fileBytes.Length);

            return File(fileBytes, contentType, fileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "업데이트 파일 다운로드 중 오류가 발생했습니다.");
            return StatusCode(500, new { error = "파일 다운로드 중 오류가 발생했습니다." });
        }
    }

    private string GetContentType(string fileName)
    {
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        return extension switch
        {
            ".zip" => "application/zip",
            ".msi" => "application/x-msi",
            ".exe" => "application/octet-stream",
            ".7z" => "application/x-7z-compressed",
            ".tar" => "application/x-tar",
            ".gz" => "application/gzip",
            _ => "application/octet-stream"
        };
    }
} 