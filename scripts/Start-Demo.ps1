param(
    [ValidateRange(1024, 65535)][int]$Port = 8771,
    [ValidatePattern('^EZRent_Demo_[A-Za-z0-9_]+$')][string]$Database = 'EZRent_Demo_Team',
    [switch]$NoBuild
)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
if (!(Get-Command SqlLocalDB.exe -ErrorAction SilentlyContinue)) { throw 'Cần SQL Server LocalDB. Xem SETUP.md.' }
$instances = @(SqlLocalDB.exe info | ForEach-Object { $_.Trim() })
if ($LASTEXITCODE -ne 0) { throw 'Không đọc được danh sách LocalDB của tài khoản hiện tại.' }
if ($instances -contains 'EZRentDemo') { SqlLocalDB.exe start EZRentDemo } else { SqlLocalDB.exe create EZRentDemo -s }
if ($LASTEXITCODE -ne 0) { throw 'Không khởi động được LocalDB EZRentDemo.' }
$previousConnection = $env:ConnectionStrings__DefaultConnection
Push-Location $projectRoot
try {
    $env:ConnectionStrings__DefaultConnection = "Server=(localdb)\EZRentDemo;Database=$Database;Integrated Security=True;TrustServerCertificate=True;MultipleActiveResultSets=True"
    Write-Host "Demo dataset: http://127.0.0.1:$Port/review/tenant"
    Write-Host "Tài khoản và ID thật: App_Data/Demo/$Database/demo-links.json."
    $runArguments = @('run', '--project', 'EZ-Rent.csproj', '--no-launch-profile')
    if ($NoBuild) { $runArguments += '--no-build' }
    $runArguments += @('--', '--environment', 'Demo', '--urls', "http://127.0.0.1:$Port")
    & dotnet @runArguments
    if ($LASTEXITCODE -ne 0) { throw 'Demo chưa chạy được; xem thông báo phía trên.' }
} finally {
    Pop-Location
    $env:ConnectionStrings__DefaultConnection = $previousConnection
}
