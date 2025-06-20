using System.Security.Cryptography;
using System.Text;

namespace AutoUpdater.Core.Utilities;

/// <summary>
/// 파일 관련 유틸리티 클래스
/// </summary>
public static class FileHelper
{
    /// <summary>
    /// 파일의 SHA256 해시 계산
    /// </summary>
    /// <param name="filePath">파일 경로</param>
    /// <returns>SHA256 해시 문자열</returns>
    public static async Task<string> CalculateFileHashAsync(string filePath)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException($"파일을 찾을 수 없습니다: {filePath}");

        using var sha256 = SHA256.Create();
        using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 4096, useAsync: true);
        
        var hash = await Task.Run(() => sha256.ComputeHash(fileStream));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    /// <summary>
    /// 파일 해시 검증
    /// </summary>
    /// <param name="filePath">파일 경로</param>
    /// <param name="expectedHash">예상 해시값</param>
    /// <returns>검증 성공 여부</returns>
    public static async Task<bool> VerifyFileHashAsync(string filePath, string expectedHash)
    {
        try
        {
            var actualHash = await CalculateFileHashAsync(filePath);
            return string.Equals(actualHash, expectedHash, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 디렉토리 생성 (존재하지 않는 경우)
    /// </summary>
    /// <param name="directoryPath">디렉토리 경로</param>
    public static void EnsureDirectoryExists(string directoryPath)
    {
        if (!Directory.Exists(directoryPath))
        {
            Directory.CreateDirectory(directoryPath);
        }
    }

    /// <summary>
    /// 파일을 안전하게 이동 (덮어쓰기 포함)
    /// </summary>
    /// <param name="sourceFile">원본 파일</param>
    /// <param name="destinationFile">대상 파일</param>
    /// <param name="overwrite">덮어쓰기 여부</param>
    public static void SafeMove(string sourceFile, string destinationFile, bool overwrite = true)
    {
        if (!File.Exists(sourceFile))
            throw new FileNotFoundException($"원본 파일을 찾을 수 없습니다: {sourceFile}");

        var destinationDir = Path.GetDirectoryName(destinationFile);
        if (!string.IsNullOrEmpty(destinationDir))
        {
            EnsureDirectoryExists(destinationDir);
        }

        if (File.Exists(destinationFile))
        {
            if (!overwrite)
                throw new InvalidOperationException($"대상 파일이 이미 존재합니다: {destinationFile}");

            File.Delete(destinationFile);
        }

        File.Move(sourceFile, destinationFile);
    }

    /// <summary>
    /// 파일을 안전하게 복사
    /// </summary>
    /// <param name="sourceFile">원본 파일</param>
    /// <param name="destinationFile">대상 파일</param>
    /// <param name="overwrite">덮어쓰기 여부</param>
    public static void SafeCopy(string sourceFile, string destinationFile, bool overwrite = true)
    {
        if (!File.Exists(sourceFile))
            throw new FileNotFoundException($"원본 파일을 찾을 수 없습니다: {sourceFile}");

        var destinationDir = Path.GetDirectoryName(destinationFile);
        if (!string.IsNullOrEmpty(destinationDir))
        {
            EnsureDirectoryExists(destinationDir);
        }

        File.Copy(sourceFile, destinationFile, overwrite);
    }

    /// <summary>
    /// 디렉토리를 재귀적으로 복사
    /// </summary>
    /// <param name="sourceDir">원본 디렉토리</param>
    /// <param name="destinationDir">대상 디렉토리</param>
    /// <param name="overwrite">덮어쓰기 여부</param>
    public static void CopyDirectory(string sourceDir, string destinationDir, bool overwrite = true)
    {
        if (!Directory.Exists(sourceDir))
            throw new DirectoryNotFoundException($"원본 디렉토리를 찾을 수 없습니다: {sourceDir}");

        EnsureDirectoryExists(destinationDir);

        // 파일 복사
        foreach (var file in Directory.GetFiles(sourceDir))
        {
            var fileName = Path.GetFileName(file);
            var destFile = Path.Combine(destinationDir, fileName);
            SafeCopy(file, destFile, overwrite);
        }

        // 하위 디렉토리 복사
        foreach (var subDir in Directory.GetDirectories(sourceDir))
        {
            var dirName = Path.GetFileName(subDir);
            var destSubDir = Path.Combine(destinationDir, dirName);
            CopyDirectory(subDir, destSubDir, overwrite);
        }
    }

    /// <summary>
    /// 파일 크기 가져오기
    /// </summary>
    /// <param name="filePath">파일 경로</param>
    /// <returns>파일 크기 (바이트)</returns>
    public static long GetFileSize(string filePath)
    {
        if (!File.Exists(filePath))
            return 0;

        var fileInfo = new FileInfo(filePath);
        return fileInfo.Length;
    }

    /// <summary>
    /// 임시 파일 경로 생성
    /// </summary>
    /// <param name="extension">파일 확장자</param>
    /// <returns>임시 파일 경로</returns>
    public static string GetTempFilePath(string extension = ".tmp")
    {
        var fileName = Path.GetRandomFileName();
        if (!extension.StartsWith('.'))
            extension = "." + extension;

        fileName = Path.ChangeExtension(fileName, extension);
        return Path.Combine(Path.GetTempPath(), fileName);
    }

    /// <summary>
    /// 파일이 사용 중인지 확인
    /// </summary>
    /// <param name="filePath">파일 경로</param>
    /// <returns>사용 중이면 true</returns>
    public static bool IsFileInUse(string filePath)
    {
        if (!File.Exists(filePath))
            return false;

        try
        {
            using var fileStream = File.Open(filePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            return false;
        }
        catch (IOException)
        {
            return true;
        }
    }
} 