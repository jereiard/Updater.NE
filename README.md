# AutoUpdater - 웹서버 기반 자동 업데이트 라이브러리

.NET 8.0 호환 웹서버 기반 자동 업데이트 라이브러리입니다. 파일 기반 스토리지를 사용하여 업데이트 정보를 관리하며, 192.168.8.0/24 네트워크에서의 안전한 업데이트를 지원합니다.

## 🚀 주요 기능

### 📦 핵심 라이브러리 (AutoUpdater.Core)
- **웹서버 기반 업데이트 확인**: HTTP/HTTPS를 통한 업데이트 정보 조회
- **자동 다운로드 및 설치**: 백그라운드 다운로드, 진행률 추적, 자동 설치
- **자기 업데이트 (Self-Update)**: 실행 중인 애플리케이션 자체 업데이트 지원
- **파일 무결성 검증**: SHA256 해시를 통한 다운로드 파일 검증
- **다양한 설치 형식 지원**: MSI, EXE, ZIP 파일 지원
- **백업 및 복원**: 업데이트 실패 시 자동 롤백
- **재시도 메커니즘**: 네트워크 오류 시 자동 재시도
- **이벤트 기반 알림**: 업데이트 상태 실시간 알림

### 🖥️ 업데이트 서버 (AutoUpdater.Server)
- **파일 기반 스토리지**: JSON 파일을 통한 업데이트 정보 관리
- **REST API**: 업데이트 정보 CRUD 작업 지원
- **네트워크 보안**: 192.168.8.0/24 네트워크만 접근 허용
- **파일 다운로드**: 직접 파일 다운로드 엔드포인트 제공
- **자동 예제 데이터**: 개발 환경에서 자동으로 테스트 데이터 생성
- **환경별 설정**: Development/Production 환경별 설정 분리

### 🎯 클라이언트 예제
- **대화형 콘솔**: 사용자 친화적인 메뉴 인터페이스
- **실시간 진행률**: 다운로드 및 설치 진행률 표시
- **설정 관리**: 런타임 설정 변경 기능

## 📁 프로젝트 구조

```
AutoUpdater/
├── src/
│   ├── AutoUpdater.Core/           # 핵심 라이브러리
│   │   ├── Configuration/          # 설정 클래스
│   │   ├── Interfaces/            # 인터페이스 정의
│   │   ├── Models/                # 데이터 모델
│   │   ├── Services/              # 서비스 구현
│   │   └── Utilities/             # 유틸리티 클래스
│   ├── AutoUpdater.Launcher/       # 자기 업데이트 런처
│   └── AutoUpdater.Server/         # 업데이트 서버
│       ├── Controllers/           # API 컨트롤러
│       ├── Services/              # 서버 서비스
│       ├── Scripts/               # 초기화 스크립트
│       └── Data/                  # 파일 스토리지
├── examples/
│   └── AutoUpdater.Client.Example/ # 클라이언트 예제
└── tests/
    └── AutoUpdater.Tests/          # 단위 테스트
```

## 🛠️ 설치 및 설정

### 1. 솔루션 빌드
```bash
dotnet build AutoUpdater.sln
```

### 2. 서버 설정
서버 설정은 `appsettings.json`에서 관리합니다:

```json
{
  "ServerSettings": {
    "BaseUrl": "https://192.168.8.210:7001",
    "HttpsPort": 7001,
    "HttpPort": 7002,
    "BindAddress": "0.0.0.0",
    "AllowedNetworks": [
      "192.168.8.0/24",
      "127.0.0.1/32"
    ]
  },
  "UpdateStorage": {
    "DataDirectory": "Data/Updates"
  },
  "UpdateFiles": {
    "BaseDirectory": "Data/UpdateFiles"
  }
}
```

#### 포트 설정
- **HttpsPort**: HTTPS 포트 (기본값: 7001)
- **HttpPort**: HTTP 포트 (기본값: 7002)
- **BindAddress**: 바인딩 주소 (개발: localhost, 프로덕션: 0.0.0.0)

### 3. 환경별 설정
- `appsettings.Development.json`: 로컬 개발용 (localhost:7001)
- `appsettings.Production.json`: 프로덕션용 (실제 서버 IP)

## 🚀 사용 방법

### 서버 실행
```bash
cd src/AutoUpdater.Server
dotnet run
```

서버는 설정 파일에 따라 다음 포트에서 실행됩니다:
- **개발 환경**: `https://localhost:7001`, `http://localhost:7002`
- **프로덕션 환경**: `https://0.0.0.0:7001`, `http://0.0.0.0:7002`

포트는 `appsettings.json`에서 변경할 수 있습니다:
```json
{
  "ServerSettings": {
    "HttpsPort": 7001,  // HTTPS 포트 변경
    "HttpPort": 7002    // HTTP 포트 변경
  }
}
```

또는 환경 변수로도 설정 가능합니다:
```bash
# Windows
set ServerSettings__HttpsPort=8001
set ServerSettings__HttpPort=8002
dotnet run

# Linux/macOS
export ServerSettings__HttpsPort=8001
export ServerSettings__HttpPort=8002
dotnet run
```

### 클라이언트 실행
```bash
cd examples/AutoUpdater.Client.Example
dotnet run
```

### API 엔드포인트

#### 업데이트 정보 조회
```http
GET /api/updates/{applicationId}?currentVersion=1.0.0&platform=Windows&architecture=x64
```

#### 업데이트 정보 등록
```http
POST /api/updates/{applicationId}
Content-Type: application/json

{
  "version": "2.0.0",
  "downloadUrl": "https://example.com/update.zip",
  "fileSize": 1048576,
  "fileHash": "sha256hash",
  "releaseNotes": "새로운 기능 추가",
  "mandatory": false,
  "minimumVersion": "1.0.0",
  "releaseDate": "2024-01-01T00:00:00Z"
}
```

#### 파일 다운로드
```http
GET /api/updates/{applicationId}/{version}/download/{fileName}
```

## 📊 파일 기반 스토리지

업데이트 정보는 다음과 같은 구조로 저장됩니다:

```
Data/
├── Updates/                    # 업데이트 메타데이터
│   ├── AutoUpdaterClientExample/
│   │   ├── 1.1.0.json
│   │   └── 2.0.0.json
│   └── TestApp/
│       ├── 1.5.0.json
│       └── 2.0.0-beta.json
└── UpdateFiles/               # 실제 업데이트 파일
    ├── AutoUpdaterClientExample/
    │   ├── 1.1.0/
    │   │   └── AutoUpdaterClientExample_v1.1.0.zip
    │   └── 2.0.0/
    │       └── AutoUpdaterClientExample_v2.0.0.zip
    └── TestApp/
        ├── 1.5.0/
        │   └── TestApp_v1.5.0.msi
        └── 2.0.0-beta/
            └── TestApp_v2.0.0-beta.zip
```

## 🔒 보안 기능

### 네트워크 접근 제어
- **허용된 네트워크**: 192.168.8.0/24 (192.168.8.1 ~ 192.168.8.254)
- **로컬호스트**: 개발용으로 허용 (127.0.0.1, ::1)
- **접근 거부**: 다른 모든 네트워크에서 403 Forbidden 응답

### 파일 무결성 검증
- SHA256 해시를 통한 다운로드 파일 검증
- 파일 크기 검증
- 손상된 파일 자동 재다운로드

### HTTPS 지원
- 프로덕션 환경에서 HTTPS 강제
- 보안 헤더 설정
- CORS 정책 적용

## 🧪 테스트

### 단위 테스트 실행
```bash
dotnet test
```

테스트 커버리지:
- ✅ 버전 비교 로직 (51개 테스트)
- ✅ 파일 처리 유틸리티
- ✅ 웹 업데이트 체커 (Moq 사용)

### 예제 데이터
개발 환경에서 서버 실행 시 자동으로 생성되는 테스트 데이터:
- **AutoUpdaterClientExample**: v1.1.0, v2.0.0
- **TestApp**: v1.5.0, v2.0.0-beta

## 📝 설정 옵션

### UpdaterConfiguration
```csharp
public class UpdaterConfiguration
{
    public string ServerUrl { get; set; }           // 서버 URL
    public string ApplicationId { get; set; }       // 애플리케이션 ID
    public string CurrentVersion { get; set; }      // 현재 버전
    public int CheckIntervalMinutes { get; set; }   // 확인 간격 (분)
    public bool AutoDownload { get; set; }          // 자동 다운로드
    public bool AutoInstall { get; set; }           // 자동 설치
    public int DownloadTimeoutSeconds { get; set; } // 다운로드 타임아웃
    public int MaxRetryAttempts { get; set; }       // 최대 재시도 횟수
    public bool BackupEnabled { get; set; }         // 백업 활성화
    public bool EnableLogging { get; set; }         // 로깅 활성화
}
```

## 🔄 업데이트 프로세스

### 일반 업데이트
1. **업데이트 확인**: 서버에서 최신 버전 정보 조회
2. **버전 비교**: 현재 버전과 비교하여 업데이트 필요성 판단
3. **다운로드**: 백그라운드에서 업데이트 파일 다운로드
4. **무결성 검증**: SHA256 해시로 파일 무결성 확인
5. **백업**: 현재 버전 백업 (옵션)
6. **설치**: 업데이트 파일 설치 (MSI/EXE/ZIP)
7. **정리**: 임시 파일 정리 및 로그 기록

### 자기 업데이트 (Self-Update)
실행 중인 애플리케이션이 자기 자신을 업데이트하는 특별한 프로세스:

1. **업데이트 확인**: 현재 실행 중인 애플리케이션에서 업데이트 확인
2. **다운로드**: 업데이트 파일을 임시 디렉토리에 다운로드
3. **런처 준비**: `AutoUpdater.Launcher.exe`에 업데이트 정보 전달
4. **프로세스 종료**: 현재 애플리케이션 프로세스 종료
5. **런처 실행**: 별도 프로세스로 업데이트 런처 실행
6. **프로세스 대기**: 런처가 대상 프로세스 완전 종료 대기
7. **백업 생성**: 현재 실행 파일 백업
8. **파일 교체**: 새 버전으로 파일 교체
9. **애플리케이션 재시작**: 업데이트된 애플리케이션 자동 재시작

#### 자기 업데이트 사용 예제
```csharp
// 자기 업데이트 시작 (현재 실행 중인 애플리케이션 업데이트)
var result = await autoUpdater.InitiateSelfUpdateAsync(updateInfo);

// 이 지점 이후로는 코드가 실행되지 않습니다 (프로세스 종료)
// 성공하면 애플리케이션이 자동으로 재시작됩니다
```

#### 런처 파일 배포
자기 업데이트를 사용하려면 `AutoUpdater.Launcher.exe`를 메인 애플리케이션과 같은 디렉토리에 배포해야 합니다:

```
MyApplication/
├── MyApplication.exe           # 메인 애플리케이션
├── AutoUpdater.Launcher.exe    # 업데이트 런처 (필수)
├── AutoUpdater.Core.dll        # 라이브러리
└── ... (기타 파일들)
```

## 🌐 네트워크 요구사항

- **서버**: 192.168.8.0/24 네트워크에 배치
- **클라이언트**: 같은 네트워크 내에서 접근
- **포트**: 7001 (HTTPS), 7002 (HTTP)
- **방화벽**: 해당 포트 개방 필요

## 📋 TODO

- [ ] 데이터베이스 스토리지 지원 (SQL Server, PostgreSQL)
- [ ] 디지털 서명 검증
- [ ] 다중 채널 지원 (stable, beta, alpha)
- [ ] 웹 관리 인터페이스
- [ ] 클러스터링 지원
- [ ] 메트릭 및 모니터링

## 🤝 기여 방법

1. Fork the repository
2. Create a feature branch
3. Commit your changes
4. Push to the branch
5. Create a Pull Request

## 📄 라이선스

이 프로젝트는 MIT 라이선스 하에 배포됩니다.

## 🆘 문제 해결

### 일반적인 문제들

#### 1. 네트워크 접근 거부 (403 Forbidden)
- 클라이언트가 192.168.8.0/24 네트워크에 있는지 확인
- 서버 설정에서 허용된 네트워크 확인

#### 2. 업데이트 파일을 찾을 수 없음 (404 Not Found)
- `Data/UpdateFiles` 디렉토리에 실제 파일이 있는지 확인
- 파일명이 메타데이터의 DownloadUrl과 일치하는지 확인

#### 3. 파일 무결성 검증 실패
- SHA256 해시값이 올바른지 확인
- 파일이 손상되지 않았는지 확인

#### 4. 서버 시작 실패
- 포트 7001, 7002가 사용 중이 아닌지 확인
- 관리자 권한으로 실행 (Windows)

### 로그 확인
서버와 클라이언트 모두 상세한 로그를 제공합니다:
```bash
# 서버 로그 레벨 설정
"Logging": {
  "LogLevel": {
    "AutoUpdater": "Debug"
  }
}
```

---

**개발자**: AutoUpdater Team  
**버전**: 1.0.0  
**최종 업데이트**: 2024년 1월 