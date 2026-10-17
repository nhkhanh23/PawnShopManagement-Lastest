param([switch]$CheckOnly, [switch]$InitOnly, [switch]$NoBrowser)
$ErrorActionPreference = 'Stop'
$server = $null
$exitCode = 0
$previousEnvironment = $env:ASPNETCORE_ENVIRONMENT
$previousLaunchId = $env:PAWN_LAUNCH_ID
$previousInitialize = $env:Database__InitializeOnStartup
function Run-DotNet([string[]]$Arguments) {
    & dotnet @Arguments
    if ($LASTEXITCODE -ne 0) { throw "dotnet failed (exit $LASTEXITCODE). See the error above." }
}
function Find-Project([string]$BaseDirectory = $PSScriptRoot) {
    $direct = Join-Path $BaseDirectory 'PawnShopManagement\PawnShopManagement.csproj'
    if (Test-Path -LiteralPath $direct) { return $direct }
    $adjacent = Join-Path $BaseDirectory 'PawnShopManagement.csproj'
    if (Test-Path -LiteralPath $adjacent) { return $adjacent }
    $matches = @(Get-ChildItem -LiteralPath $BaseDirectory -Filter PawnShopManagement.csproj -Recurse -File)
    if ($matches.Count -ne 1) { throw "Expected one PawnShopManagement.csproj, found $($matches.Count). Extract the full ZIP and keep this launcher beside the PawnShopManagement folder." }
    return $matches[0].FullName
}
function Select-Port {
    $listener = [System.Net.Sockets.TcpListener]::new([System.Net.IPAddress]::Loopback, 5073)
    try { $listener.Start(); return 5073 }
    catch {
        $listener.Stop()
        $listener = [System.Net.Sockets.TcpListener]::new([System.Net.IPAddress]::Loopback, 0)
        $listener.Start()
        return $listener.LocalEndpoint.Port
    }
    finally { $listener.Stop() }
}
try {
    $project = Find-Project
    Write-Host '[PAWNFLOW-RUN-03]'
    Write-Host "Project: $project"
    if ($CheckOnly) { exit 0 }
    if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
        throw 'Missing .NET SDK 10. Install the Windows SDK from https://dotnet.microsoft.com/download/dotnet/10.0 then run again.'
    }
    $sdks = & dotnet --list-sdks
    if ($LASTEXITCODE -ne 0 -or -not ($sdks -match '^10\.')) {
        throw 'Missing .NET SDK 10 (the Runtime alone is insufficient). Install SDK 10, then run again.'
    }
    $projectDir = Split-Path -Parent $project
    Push-Location -LiteralPath $projectDir
    try {
        $env:ASPNETCORE_ENVIRONMENT = 'Development'
        $env:PAWN_LAUNCH_ID = [Guid]::NewGuid().ToString('N')
        $env:Database__InitializeOnStartup = 'false'
        Write-Host '[1/4] Restore NuGet packages (Internet required on first run)...'
        Run-DotNet -Arguments @('restore', $project)
        Write-Host '[2/4] Build...'
        Run-DotNet -Arguments @('build', $project, '--no-restore', '--configuration', 'Debug')
        $dll = Join-Path $projectDir 'bin\Debug\net10.0\PawnShopManagement.dll'
        if (-not (Test-Path -LiteralPath $dll)) { throw "Build output missing: $dll" }
        Write-Host '[3/4] Check/create database without deleting existing data...'
        Run-DotNet -Arguments @($dll, '--contentRoot', $projectDir, '--init-db')
        if ($InitOnly) { Write-Host 'Database ready.'; exit 0 }
        $port = Select-Port
        $baseUrl = "http://localhost:$port"
        $logDir = Join-Path $projectDir 'App_Data\logs'
        New-Item -ItemType Directory -Force -Path $logDir | Out-Null
        $stdout = Join-Path $logDir ("website-" + $env:PAWN_LAUNCH_ID + '.log')
        $stderr = Join-Path $logDir ("website-error-" + $env:PAWN_LAUNCH_ID + '.log')
        $serverArgs = '"{0}" --contentRoot "{1}" --urls "{2}"' -f $dll, $projectDir, $baseUrl
        $server = Start-Process -FilePath (Get-Command dotnet).Source -ArgumentList $serverArgs -WorkingDirectory $projectDir -WindowStyle Hidden -RedirectStandardOutput $stdout -RedirectStandardError $stderr -PassThru
        Write-Host '[4/4] Waiting for the website...'
        $ready = $false
        $deadline = [DateTime]::UtcNow.AddSeconds(60)
        while ([DateTime]::UtcNow -lt $deadline) {
            if ($server.HasExited) { throw "Website exited (code $($server.ExitCode))." }
            try {
                $response = Invoke-WebRequest -Uri "$baseUrl/_launcher/ready" -UseBasicParsing -TimeoutSec 2
                if ($response.StatusCode -eq 200 -and $response.Content.Trim() -eq $env:PAWN_LAUNCH_ID) { $ready = $true; break }
            } catch { }
            Start-Sleep -Milliseconds 400
        }
        if (-not $ready) { throw 'Website did not become ready within 60 seconds.' }
        $url = "$baseUrl/Account/Start"
        Write-Host "Website ready: $url" -ForegroundColor Green
        if (-not $NoBrowser) {
            try { Start-Process -FilePath $url }
            catch { Write-Host "Could not open the browser automatically. Open this address: $url" }
        }
        Write-Host 'First use: create your own admin. Existing database: sign in with its account.'
        Write-Host 'Keep this window open. Press Ctrl+C to stop. Logs:'
        Write-Host $logDir
        while (-not $server.HasExited) { Start-Sleep -Milliseconds 500 }
        if ($server.ExitCode -ne 0) { throw "Website stopped (code $($server.ExitCode))." }
    } finally { Pop-Location }
} catch {
    $exitCode = 1
    Write-Host "`nERROR: $($_.Exception.Message)" -ForegroundColor Red
    if ($stdout -and (Test-Path -LiteralPath $stdout)) { Get-Content -LiteralPath $stdout -Tail 20 }
    if ($stderr -and (Test-Path -LiteralPath $stderr)) { Get-Content -LiteralPath $stderr -Tail 20 }
    Write-Host 'Database errors: install SQL Server Express LocalDB or configure ConnectionStrings:DefaultConnection in PawnShopManagement/appsettings.json.'
    Write-Host 'Do not delete your database. Send a screenshot of the error above for support.'
} finally {
    if ($server -and -not $server.HasExited) { Stop-Process -Id $server.Id -ErrorAction SilentlyContinue }
    $env:ASPNETCORE_ENVIRONMENT = $previousEnvironment
    $env:PAWN_LAUNCH_ID = $previousLaunchId
    $env:Database__InitializeOnStartup = $previousInitialize
}
exit $exitCode
