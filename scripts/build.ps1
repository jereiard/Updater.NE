#!/usr/bin/env pwsh
<#
.SYNOPSIS
    AutoUpdater 솔루션 빌드 스크립트

.DESCRIPTION
    전체 솔루션을 빌드하고 NuGet 패키지를 생성합니다.

.PARAMETER Configuration
    빌드 구성 (Debug 또는 Release)

.PARAMETER Clean
    빌드 전 정리 수행

.PARAMETER PackageOnly
    패키지만 생성 (빌드 스킵)

.EXAMPLE
    .\build.ps1
    .\build.ps1 -Configuration Release -Clean
    .\build.ps1 -PackageOnly
#>

param(
    [Parameter()]
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",
    
    [Parameter()]
    [switch]$Clean,
    
    [Parameter()]
    [switch]$PackageOnly
)

$ErrorActionPreference = "Stop"

# 프로젝트 루트 디렉토리
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Definition
$projectRoot = Split-Path -Parent $scriptDir
$solutionPath = Join-Path $projectRoot "AutoUpdater.sln"
$coreProjectPath = Join-Path $projectRoot "src\AutoUpdater.Core"

Write-Host "🏗️  AutoUpdater 빌드 시작" -ForegroundColor Green
Write-Host "📁 프로젝트 루트: $projectRoot" -ForegroundColor Cyan
Write-Host "⚙️  구성: $Configuration" -ForegroundColor Cyan

# 정리
if ($Clean) {
    Write-Host "🧹 정리 중..." -ForegroundColor Yellow
    dotnet clean $solutionPath --configuration $Configuration --verbosity minimal
    
    # bin, obj 디렉토리 삭제
    Get-ChildItem -Path $projectRoot -Recurse -Directory -Name "bin", "obj" | 
        ForEach-Object { 
            $fullPath = Join-Path $projectRoot $_
            if (Test-Path $fullPath) {
                Write-Host "   🗑️  삭제: $fullPath" -ForegroundColor Gray
                Remove-Item $fullPath -Recurse -Force
            }
        }
}

if (-not $PackageOnly) {
    # 복원
    Write-Host "📦 패키지 복원 중..." -ForegroundColor Yellow
    dotnet restore $solutionPath --verbosity minimal
    
    if ($LASTEXITCODE -ne 0) {
        Write-Host "❌ 패키지 복원 실패!" -ForegroundColor Red
        exit 1
    }
    
    # 빌드
    Write-Host "🔨 솔루션 빌드 중..." -ForegroundColor Yellow
    dotnet build $solutionPath --configuration $Configuration --no-restore --verbosity minimal
    
    if ($LASTEXITCODE -ne 0) {
        Write-Host "❌ 빌드 실패!" -ForegroundColor Red
        exit 1
    }
    
    Write-Host "✅ 빌드 성공!" -ForegroundColor Green
}

# 패키지 생성 (AutoUpdater.Core만)
Write-Host "📦 NuGet 패키지 생성 중..." -ForegroundColor Yellow

try {
    Push-Location $coreProjectPath
    
    # 패키지 생성
    dotnet pack --configuration $Configuration --no-build --verbosity minimal
    
    if ($LASTEXITCODE -ne 0) {
        Write-Host "❌ 패키지 생성 실패!" -ForegroundColor Red
        exit 1
    }
    
    # 생성된 패키지 파일 찾기
    $packagePath = Join-Path $coreProjectPath "bin\$Configuration"
    $packageFiles = Get-ChildItem -Path $packagePath -Filter "*.nupkg" | Sort-Object LastWriteTime -Descending
    
    if ($packageFiles.Count -gt 0) {
        $latestPackage = $packageFiles[0]
        Write-Host "✅ 패키지 생성 완료!" -ForegroundColor Green
        Write-Host "📦 패키지: $($latestPackage.Name)" -ForegroundColor White
        Write-Host "📁 경로: $($latestPackage.FullName)" -ForegroundColor Gray
        Write-Host "📊 크기: $([math]::Round($latestPackage.Length / 1KB, 2)) KB" -ForegroundColor Gray
    } else {
        Write-Host "⚠️  패키지 파일을 찾을 수 없습니다." -ForegroundColor Yellow
    }
    
} finally {
    Pop-Location
}

# 테스트 실행 (옵션)
$testProjects = Get-ChildItem -Path $projectRoot -Recurse -Filter "*.Tests.csproj"
if ($testProjects.Count -gt 0 -and -not $PackageOnly) {
    Write-Host "🧪 테스트 실행 중..." -ForegroundColor Yellow
    dotnet test $solutionPath --configuration $Configuration --no-build --verbosity minimal
    
    if ($LASTEXITCODE -eq 0) {
        Write-Host "✅ 모든 테스트 통과!" -ForegroundColor Green
    } else {
        Write-Host "⚠️  일부 테스트 실패" -ForegroundColor Yellow
    }
}

Write-Host "🎉 빌드 완료!" -ForegroundColor Green 