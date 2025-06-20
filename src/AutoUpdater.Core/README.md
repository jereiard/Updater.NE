# AutoUpdater.Core

.NET 8.0 호환 웹서버 기반 자동 업데이트 라이브러리입니다.

## 🚀 주요 기능

- **웹서버 기반 업데이트 확인**: HTTP/HTTPS를 통한 업데이트 정보 조회
- **자동 다운로드 및 설치**: 백그라운드 다운로드, 진행률 추적, 자동 설치
- **자기 업데이트 (Self-Update)**: 실행 중인 애플리케이션 자체 업데이트 지원
- **파일 무결성 검증**: SHA256 해시를 통한 다운로드 파일 검증
- **다양한 설치 형식 지원**: MSI, EXE, ZIP 파일 지원
- **백업 및 복원**: 업데이트 실패 시 자동 롤백
- **재시도 메커니즘**: 네트워크 오류 시 자동 재시도
- **이벤트 기반 알림**: 업데이트 상태 실시간 알림

## 📦 설치

```bash
dotnet add package AutoUpdater.Core
```

## 🛠️ 사용 방법

### 기본 설정

```csharp
using AutoUpdater.Core.Configuration;
using AutoUpdater.Core.Services;
using Microsoft.Extensions.DependencyInjection;

// 서비스 등록
services.AddSingleton(new UpdaterConfiguration
{
    ServerUrl = "https://your-update-server.com",
    ApplicationId = "YourApp",
    CurrentVersion = "1.0.0"
});

services.AddTransient<IAutoUpdater, AutoUpdaterService>();
```

### 업데이트 확인

```csharp
var autoUpdater = serviceProvider.GetService<IAutoUpdater>();

var request = new UpdateRequest
{
    ApplicationId = "YourApp",
    CurrentVersion = "1.0.0",
    Platform = Environment.OSVersion.Platform.ToString(),
    Architecture = Environment.Is64BitProcess ? "x64" : "x86"
};

var result = await autoUpdater.CheckForUpdateAsync(request);

if (result.IsSuccess && result.ResultType == UpdateResultType.UpdateAvailable)
{
    Console.WriteLine($"새 버전 발견: {result.UpdateInfo.Version}");
}
```

### 자기 업데이트

```csharp
// 자기 업데이트 시작 (현재 실행 중인 애플리케이션 업데이트)
var result = await autoUpdater.InitiateSelfUpdateAsync(updateInfo);

// 성공하면 애플리케이션이 자동으로 재시작됩니다
```

## 📋 요구사항

- .NET 8.0 이상
- 자기 업데이트 사용 시 `AutoUpdater.Launcher.exe` 필요

## 📚 자세한 문서

전체 문서와 예제는 [GitHub 저장소](https://github.com/your-repo/AutoUpdater)에서 확인하세요.

## 📄 라이선스

MIT License 