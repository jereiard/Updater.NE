using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace AutoUpdater.Core.Utilities;

/// <summary>
/// 버전 비교 유틸리티 클래스
/// </summary>
public static class VersionHelper
{
    private static readonly Regex VersionRegex = new(@"^(\d+)\.(\d+)\.(\d+)(?:\.(\d+))?(?:-([a-zA-Z0-9\-\.]+))?$", RegexOptions.Compiled);
    private static ILogger? _logger;

    /// <summary>
    /// 로거 설정 (선택사항)
    /// </summary>
    /// <param name="logger">로거 인스턴스</param>
    public static void SetLogger(ILogger logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// 버전 정보를 담는 내부 클래스
    /// </summary>
    private class VersionInfo
    {
        public Version Version { get; set; }
        public string? PreRelease { get; set; }

        public VersionInfo(Version version, string? preRelease = null)
        {
            Version = version;
            PreRelease = preRelease;
        }
    }

    /// <summary>
    /// 버전 문자열을 Version 객체로 변환
    /// </summary>
    /// <param name="versionString">버전 문자열</param>
    /// <returns>Version 객체</returns>
    public static Version ParseVersion(string versionString)
    {
        var versionInfo = ParseVersionInfo(versionString);
        return versionInfo.Version;
    }

    /// <summary>
    /// 버전 문자열을 VersionInfo 객체로 변환 (프리릴리스 정보 포함)
    /// </summary>
    /// <param name="versionString">버전 문자열</param>
    /// <returns>VersionInfo 객체</returns>
    private static VersionInfo ParseVersionInfo(string versionString)
    {
        if (string.IsNullOrWhiteSpace(versionString))
            throw new ArgumentException("버전 문자열이 비어있습니다.", nameof(versionString));

        var match = VersionRegex.Match(versionString);
        if (!match.Success)
        {
            throw new ArgumentException($"잘못된 버전 형식입니다: {versionString}", nameof(versionString));
        }

        var major = int.Parse(match.Groups[1].Value);
        var minor = int.Parse(match.Groups[2].Value);
        var build = int.Parse(match.Groups[3].Value);
        var revision = match.Groups[4].Success ? int.Parse(match.Groups[4].Value) : 0;
        var preRelease = match.Groups[5].Success ? match.Groups[5].Value : null;

        var version = new Version(major, minor, build, revision);
        return new VersionInfo(version, preRelease);
    }

    /// <summary>
    /// 새 버전이 현재 버전보다 새로운지 확인
    /// </summary>
    /// <param name="currentVersion">현재 버전</param>
    /// <param name="newVersion">새 버전</param>
    /// <returns>새 버전이 더 새로우면 true</returns>
    public static bool IsNewerVersion(string currentVersion, string newVersion)
    {
        if (string.IsNullOrWhiteSpace(currentVersion))
        {
            _logger?.LogWarning("현재 버전이 비어있습니다.");
            return !string.IsNullOrWhiteSpace(newVersion);
        }

        if (string.IsNullOrWhiteSpace(newVersion))
        {
            _logger?.LogWarning("새 버전이 비어있습니다.");
            return false;
        }

        try
        {
            var current = ParseVersionInfo(currentVersion);
            var newVer = ParseVersionInfo(newVersion);
            
            // 먼저 버전 번호 비교
            var versionComparison = newVer.Version.CompareTo(current.Version);
            
            bool result;
            if (versionComparison != 0)
            {
                // 버전 번호가 다르면 버전 번호로만 비교
                result = versionComparison > 0;
            }
            else
            {
                // 버전 번호가 같으면 프리릴리스 정보로 비교
                result = ComparePreRelease(current.PreRelease, newVer.PreRelease) < 0;
            }
            
            _logger?.LogDebug("버전 비교: {CurrentVersion} vs {NewVersion} = {Result}", 
                currentVersion, newVersion, result);
            
            return result;
        }
        catch (ArgumentException ex)
        {
            _logger?.LogError(ex, "버전 파싱 실패: 현재={CurrentVersion}, 새버전={NewVersion}", 
                currentVersion, newVersion);
            return false;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "버전 비교 중 예상치 못한 오류 발생");
            return false;
        }
    }

    /// <summary>
    /// 프리릴리스 버전 비교
    /// </summary>
    /// <param name="current">현재 프리릴리스</param>
    /// <param name="new">새 프리릴리스</param>
    /// <returns>비교 결과 (-1: current가 낮음, 0: 같음, 1: current가 높음)</returns>
    private static int ComparePreRelease(string? current, string? @new)
    {
        // 둘 다 정식 릴리스면 같음
        if (string.IsNullOrEmpty(current) && string.IsNullOrEmpty(@new))
            return 0;

        // 정식 릴리스가 프리릴리스보다 높음
        if (string.IsNullOrEmpty(current) && !string.IsNullOrEmpty(@new))
            return 1;

        if (!string.IsNullOrEmpty(current) && string.IsNullOrEmpty(@new))
            return -1;

        // 둘 다 프리릴리스면 문자열 비교 (알파벳순)
        return string.Compare(current, @new, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 버전이 최소 요구 버전을 만족하는지 확인
    /// </summary>
    /// <param name="currentVersion">현재 버전</param>
    /// <param name="minimumVersion">최소 요구 버전</param>
    /// <returns>최소 요구 버전을 만족하면 true</returns>
    public static bool MeetsMinimumVersion(string currentVersion, string minimumVersion)
    {
        if (string.IsNullOrWhiteSpace(minimumVersion))
            return true;

        try
        {
            var current = ParseVersionInfo(currentVersion);
            var minimum = ParseVersionInfo(minimumVersion);
            
            var versionComparison = current.Version.CompareTo(minimum.Version);
            
            if (versionComparison != 0)
            {
                return versionComparison >= 0;
            }
            else
            {
                // 버전 번호가 같으면 프리릴리스 정보로 비교
                return ComparePreRelease(current.PreRelease, minimum.PreRelease) >= 0;
            }
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 버전 비교
    /// </summary>
    /// <param name="version1">버전 1</param>
    /// <param name="version2">버전 2</param>
    /// <returns>비교 결과 (-1: version1이 낮음, 0: 같음, 1: version1이 높음)</returns>
    public static int CompareVersions(string version1, string version2)
    {
        try
        {
            var v1 = ParseVersionInfo(version1);
            var v2 = ParseVersionInfo(version2);
            
            var versionComparison = v1.Version.CompareTo(v2.Version);
            
            if (versionComparison != 0)
            {
                return versionComparison;
            }
            else
            {
                // 버전 번호가 같으면 프리릴리스 정보로 비교
                return ComparePreRelease(v1.PreRelease, v2.PreRelease);
            }
        }
        catch
        {
            return string.Compare(version1, version2, StringComparison.OrdinalIgnoreCase);
        }
    }

    /// <summary>
    /// 버전 문자열이 유효한지 확인
    /// </summary>
    /// <param name="versionString">버전 문자열</param>
    /// <returns>유효하면 true</returns>
    public static bool IsValidVersion(string versionString)
    {
        if (string.IsNullOrWhiteSpace(versionString))
            return false;

        try
        {
            ParseVersion(versionString);
            return true;
        }
        catch
        {
            return false;
        }
    }
} 