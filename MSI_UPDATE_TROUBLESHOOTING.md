# MSI 업데이트 문제 해결 가이드

## 개요

MSI(Microsoft Installer) 파일을 통한 업데이트 시 발생할 수 있는 일반적인 문제들과 해결 방법을 설명합니다.

## 주요 문제 및 해결 방법

### 1. 오류 코드 1603: 설치 실패

**문제**: 업데이트 대상 프로그램이 실행 중일 때 MSI 설치가 실패합니다.

**원인**: 
- 업데이트하려는 파일이 다른 프로세스에 의해 사용 중
- 파일이 잠겨있어 덮어쓸 수 없음
- 관리자 권한 부족

**해결 방법**:

#### 자동 프로세스 종료 (권장)
```csharp
// DefaultUpdateInstaller에서 자동으로 처리됨
// 1. 관련 프로세스 감지
// 2. 우아한 종료 시도 (CloseMainWindow)
// 3. 필요시 강제 종료 (Kill)
// 4. MSI 설치 진행
```

#### 수동 프로세스 종료
```powershell
# PowerShell에서 프로세스 종료
Get-Process "YourAppName" | Stop-Process -Force

# 또는 작업 관리자에서 프로세스 종료
```

### 2. 관리자 권한 문제

**문제**: MSI 설치 시 관리자 권한이 필요합니다.

**해결 방법**:
- 업데이트를 관리자 권한으로 실행
- UAC(사용자 계정 컨트롤) 프롬프트 허용
- `runas` 옵션 사용 (코드에서 자동 처리됨)

### 3. 설치 로그 확인

**로그 위치**: `%TEMP%\msi_install.log`

**로그 내용 확인**:
```powershell
Get-Content "$env:TEMP\msi_install.log" | Select-Object -Last 50
```

## 업데이트된 기능

### 프로세스 자동 종료
- 업데이트 대상 프로세스 자동 감지
- 우아한 종료 시도 (30초 대기)
- 필요시 강제 종료
- 상세한 로깅

### 향상된 오류 처리
- MSI 설치 로그 자동 수집
- 상세한 오류 메시지 제공
- 실패 시 백업에서 복원 시도

### 관리자 권한 자동 요청
- `runas` 옵션으로 UAC 프롬프트 자동 표시
- 관리자 권한 필요 시 자동 승격

## 설정 옵션

### UpdaterConfiguration 설정
```csharp
var config = new UpdaterConfiguration
{
    // 프로세스 종료 대기 시간 (초)
    ProcessTerminationTimeoutSeconds = 30,
    
    // 강제 종료 허용 여부
    AllowForceProcessTermination = true,
    
    // 백업 활성화
    BackupEnabled = true
};
```

## 테스트 방법

### 단위 테스트 실행
```bash
dotnet test tests/AutoUpdater.Tests/DefaultUpdateInstallerTests.cs
```

### 통합 테스트
1. 테스트 애플리케이션 실행
2. MSI 업데이트 파일 준비
3. 업데이트 실행
4. 로그 확인

## 모니터링 및 로깅

### 로그 레벨
- **Information**: 일반적인 업데이트 진행 상황
- **Warning**: 프로세스 종료 실패 등 경고
- **Error**: 치명적인 오류

### 주요 로그 메시지
```
업데이트 대상 프로세스가 실행 중입니다. 프로세스 종료를 시도합니다.
프로세스 종료 시도: {ProcessName} (PID: {ProcessId})
우아한 종료 실패, 강제 종료 시도: {ProcessName} (PID: {ProcessId})
MSI 설치 실패 (코드: 1603): {Error}
```

## 문제 해결 체크리스트

1. [ ] 업데이트 대상 프로그램이 완전히 종료되었는지 확인
2. [ ] 관리자 권한으로 실행되고 있는지 확인
3. [ ] MSI 파일이 손상되지 않았는지 확인
4. [ ] 디스크 공간이 충분한지 확인
5. [ ] 바이러스 백신이 설치를 차단하지 않는지 확인
6. [ ] Windows Installer 서비스가 실행 중인지 확인

## 추가 리소스

- [Windows Installer 오류 코드](https://docs.microsoft.com/en-us/windows/win32/msi/error-codes)
- [MSI 로그 분석 도구](https://docs.microsoft.com/en-us/windows/win32/msi/windows-installer-logging)
- [UAC 및 관리자 권한](https://docs.microsoft.com/en-us/windows/security/identity-protection/user-account-control/)

## 지원

문제가 지속되는 경우:
1. 상세한 로그 파일 수집
2. 시스템 환경 정보 확인
3. 재현 가능한 테스트 케이스 작성
4. GitHub Issues에 문제 보고 