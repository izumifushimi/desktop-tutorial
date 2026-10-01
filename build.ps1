param([ValidateSet("win-x64", "win-arm64")][string]$Runtime = "win-x64")
$ErrorActionPreference = "Stop"
$env:DOTNET_CLI_TELEMETRY_OPTOUT = "1"
$ProjectRoot = Split-Path -Parent $PSScriptRoot
$TargetDir = Join-Path $ProjectRoot "dist\$Runtime"
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw "请先安装 .NET 10 SDK：https://dotnet.microsoft.com/download/dotnet/10.0"
}
& dotnet run --project (Join-Path $PSScriptRoot "WhalePet.StateTests") -c Release
if ($LASTEXITCODE -ne 0) { throw "状态检查失败" }
if (Test-Path $TargetDir) { Remove-Item $TargetDir -Recurse -Force }
& dotnet publish (Join-Path $PSScriptRoot "WhalePet.Windows") -c Release -r $Runtime --self-contained false -p:UseAppHost=false -p:PublishSingleFile=false -o $TargetDir
if ($LASTEXITCODE -ne 0) { throw "编译失败" }
@'
@echo off
cd /d "%~dp0"
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0start.ps1"
'@ | Set-Content -Path (Join-Path $TargetDir "start.cmd") -Encoding ascii
@'
$ErrorActionPreference = "Stop"
$DotnetHost = (Get-Command dotnet -ErrorAction Stop).Source
$AssemblyPath = Join-Path $PSScriptRoot "WhalePet.dll"
Start-Process -FilePath $DotnetHost -ArgumentList ('"' + $AssemblyPath + '"') -WindowStyle Hidden
'@ | Set-Content -Path (Join-Path $TargetDir "start.ps1") -Encoding ascii
$InvalidFiles = @(Get-ChildItem $TargetDir -File -Recurse | Where-Object { $_.Extension -in '.exe', '.zip' -or $_.Length -ge 25000000 })
if ($InvalidFiles.Count -gt 0) { throw "输出包含 EXE、ZIP 或大于等于 25 MB 的文件" }
Write-Host "构建完成：$TargetDir\WhalePet.dll；安装 .NET 10 Desktop Runtime 后双击 start.cmd"
