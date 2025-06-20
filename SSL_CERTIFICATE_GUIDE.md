# SSL 인증서 설정 가이드

AutoUpdater에서 HTTPS를 통한 업데이트 시 사설인증서를 신뢰하도록 설정하는 방법을 설명합니다.

## 🔧 설정 방법

### 1. 기본 설정

```json
{
  "AutoUpdater": {
    "ServerUrl": "https://your-server.com",
    "ApplicationId": "YourApp",
    "Ssl": {
      "AllowSelfSignedCertificates": false,
      "IgnoreCertificateChainErrors": false,
      "IgnoreCertificateNameMismatch": false,
      "IgnoreAllSslErrors": false,
      "TrustedCertificateThumbprints": [],
      "ClientCertificatePath": "",
      "ClientCertificatePassword": ""
    }
  }
}
```

### 2. 사설 인증서 허용

자체 서명된 인증서를 허용하려면:

```json
{
  "Ssl": {
    "AllowSelfSignedCertificates": true
  }
}
```

### 3. 특정 인증서 신뢰

특정 인증서의 지문(Thumbprint)을 신뢰하려면:

```json
{
  "Ssl": {
    "TrustedCertificateThumbprints": [
      "1234567890ABCDEF1234567890ABCDEF12345678",
      "FEDCBA0987654321FEDCBA0987654321FEDCBA09"
    ]
  }
}
```

### 4. 인증서 이름 불일치 무시

호스트명과 인증서 이름이 다를 때 무시하려면:

```json
{
  "Ssl": {
    "IgnoreCertificateNameMismatch": true
  }
}
```

### 5. 클라이언트 인증서 사용

클라이언트 인증서가 필요한 경우:

```json
{
  "Ssl": {
    "ClientCertificatePath": "path/to/client.pfx",
    "ClientCertificatePassword": "certificate_password"
  }
}
```

### 6. 개발 환경 설정

**⚠️ 주의: 프로덕션에서는 절대 사용하지 마세요!**

```json
{
  "Ssl": {
    "IgnoreAllSslErrors": true
  }
}
```

## 🔍 인증서 지문 확인 방법

### Windows PowerShell
```powershell
# 웹사이트 인증서 확인
$cert = Invoke-WebRequest -Uri "https://your-server.com" | Select-Object -ExpandProperty Certificates
$cert.Thumbprint
```

### OpenSSL
```bash
# 웹사이트 인증서 확인
echo | openssl s_client -servername your-server.com -connect your-server.com:443 2>/dev/null | openssl x509 -fingerprint -noout -sha1
```

### 브라우저에서 확인
1. 웹사이트 방문
2. 주소창의 자물쇠 아이콘 클릭
3. "인증서" 또는 "Certificate" 클릭
4. "지문" 또는 "Thumbprint" 확인

## 💡 사용 예제

### C# 코드에서 설정

```csharp
var configuration = new UpdaterConfiguration
{
    ServerUrl = "https://your-server.com",
    ApplicationId = "YourApp",
    Ssl = new SslConfiguration
    {
        AllowSelfSignedCertificates = true,
        IgnoreCertificateNameMismatch = false,
        TrustedCertificateThumbprints = new List<string>
        {
            "1234567890ABCDEF1234567890ABCDEF12345678"
        }
    }
};

// SSL 설정이 적용된 HttpClient 생성
var httpClient = HttpClientFactory.CreateHttpClient(configuration);

// 또는 서비스에서 직접 사용
var updateChecker = new WebUpdateChecker(configuration, logger);
```

### DI 컨테이너에서 설정

```csharp
// Program.cs 또는 Startup.cs
services.Configure<UpdaterConfiguration>(configuration.GetSection("AutoUpdater"));

services.AddHttpClient<IUpdateChecker, WebUpdateChecker>()
    .ConfigurePrimaryHttpMessageHandler(serviceProvider =>
    {
        var config = serviceProvider.GetRequiredService<IOptions<UpdaterConfiguration>>().Value;
        var handler = new HttpClientHandler();
        HttpClientFactory.ConfigureSslSettings(handler, config.Ssl);
        return handler;
    });
```

## 🛡️ 보안 고려사항

### 1. 프로덕션 환경
- `IgnoreAllSslErrors`는 절대 사용하지 마세요
- 가능한 한 유효한 CA 인증서를 사용하세요
- 사설 인증서 사용 시 지문을 명시적으로 지정하세요

### 2. 개발 환경
- 개발 중에만 `AllowSelfSignedCertificates` 사용
- 테스트 서버에는 임시 인증서 사용 가능

### 3. 인증서 관리
- 인증서 만료일 모니터링
- 정기적인 인증서 갱신
- 지문 목록 업데이트

## 🔧 문제 해결

### 일반적인 오류

1. **"RemoteCertificateChainErrors"**
   - 자체 서명된 인증서 또는 신뢰할 수 없는 CA
   - 해결: `AllowSelfSignedCertificates: true` 또는 지문 추가

2. **"RemoteCertificateNameMismatch"**
   - 호스트명과 인증서 이름 불일치
   - 해결: `IgnoreCertificateNameMismatch: true`

3. **"RemoteCertificateNotAvailable"**
   - 서버에서 인증서를 제공하지 않음
   - 해결: 서버 설정 확인

### 로그 확인

SSL 관련 로그를 확인하려면:

```csharp
// 로거 설정
SslCertificateValidator.SetLogger(logger);
HttpClientFactory.SetLogger(logger);
```

로그 레벨을 `Debug`로 설정하면 상세한 SSL 검증 과정을 확인할 수 있습니다.

## 📝 참고 자료

- [X.509 Certificate Documentation](https://docs.microsoft.com/en-us/dotnet/api/system.security.cryptography.x509certificates.x509certificate2)
- [HttpClientHandler SSL Configuration](https://docs.microsoft.com/en-us/dotnet/api/system.net.http.httpclienthandler.servercertificatecustomvalidationcallback)
- [SSL/TLS Best Practices](https://owasp.org/www-project-cheat-sheets/cheatsheets/Transport_Layer_Protection_Cheat_Sheet.html) 