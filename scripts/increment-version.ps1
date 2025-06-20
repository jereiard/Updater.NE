#!/usr/bin/env pwsh
<#
.SYNOPSIS
    AutoUpdater.Core 버전 관리 스크립트

.DESCRIPTION
    메이저, 마이너 버전을 증가시키거나 현재 버전을 확인할 수 있는 스크립트입니다.

.PARAMETER Action
    수행할 작업: major, minor, patch, show

.EXAMPLE
    .\increment-version.ps1 -Action show
    .\increment-version.ps1 -Action minor
    .\increment-version.ps1 -Action major
#>

param(
    [Parameter(Mandatory = $true)]
    [ValidateSet("major", "minor", "patch", "show")]
    [string]$Action
)

$ErrorActionPreference = "Stop"

# 프로젝트 루트 디렉토리 찾기
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Definition
$projectRoot = Split-Path -Parent $scriptDir
$versionPropsPath = Join-Path $projectRoot "src\AutoUpdater.Core\Version.props"

Write-Host "🔍 버전 파일 경로: $versionPropsPath" -ForegroundColor Cyan

if (-not (Test-Path $versionPropsPath)) {
    Write-Error "Version.props 파일을 찾을 수 없습니다: $versionPropsPath"
    exit 1
}

# XML 파일 로드
[xml]$versionXml = Get-Content $versionPropsPath

# 현재 버전 읽기
$majorNode = $versionXml.Project.PropertyGroup.MajorVersion
$minorNode = $versionXml.Project.PropertyGroup.MinorVersion

$currentMajor = [int]$majorNode
$currentMinor = [int]$minorNode

Write-Host "📋 현재 버전 정보:" -ForegroundColor Green
Write-Host "   메이저: $currentMajor" -ForegroundColor White
Write-Host "   마이너: $currentMinor" -ForegroundColor White

if ($Action -eq "show") {
    Write-Host "✅ 현재 버전: $currentMajor.$currentMinor.x.x" -ForegroundColor Green
    exit 0
}

# 버전 증가
switch ($Action) {
    "major" {
        $newMajor = $currentMajor + 1
        $newMinor = 0
        Write-Host "🚀 메이저 버전 증가: $currentMajor.$currentMinor -> $newMajor.$newMinor" -ForegroundColor Yellow
    }
    "minor" {
        $newMajor = $currentMajor
        $newMinor = $currentMinor + 1
        Write-Host "📈 마이너 버전 증가: $currentMajor.$currentMinor -> $newMajor.$newMinor" -ForegroundColor Yellow
    }
    "patch" {
        Write-Host "ℹ️  패치 버전은 자동으로 관리됩니다 (빌드 번호 기반)" -ForegroundColor Blue
        exit 0
    }
}

# 버전 업데이트
$majorNode = [string]$newMajor
$minorNode = [string]$newMinor

$versionXml.Project.PropertyGroup.MajorVersion = $majorNode
$versionXml.Project.PropertyGroup.MinorVersion = $minorNode

# 파일 저장
$versionXml.Save($versionPropsPath)

Write-Host "✅ 버전이 업데이트되었습니다!" -ForegroundColor Green
Write-Host "   새 버전: $newMajor.$newMinor.x.x" -ForegroundColor White

# 빌드 테스트
Write-Host "🔨 빌드 테스트 중..." -ForegroundColor Cyan
$coreProjectPath = Join-Path $projectRoot "src\AutoUpdater.Core"

try {
    Push-Location $coreProjectPath
    dotnet build --configuration Release --verbosity minimal
    
    if ($LASTEXITCODE -eq 0) {
        Write-Host "✅ 빌드 성공!" -ForegroundColor Green
    } else {
        Write-Host "❌ 빌드 실패!" -ForegroundColor Red
        exit 1
    }
} finally {
    Pop-Location
}

Write-Host "🎉 버전 업데이트 완료!" -ForegroundColor Green 