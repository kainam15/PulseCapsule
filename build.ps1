param([switch]$Publish, [switch]$Check, [string]$DotnetPath, [string]$OutputDirectory = 'dist')
$ErrorActionPreference = 'Stop'
Set-Location -LiteralPath $PSScriptRoot
$candidates = @($DotnetPath, $env:PULSECAPSULE_DOTNET, (Join-Path $PSScriptRoot '.tools\dotnet\dotnet.exe'), 'D:\my_project\AltTabLock\.tools\dotnet\dotnet.exe')
$dotnet = $candidates | Where-Object { $_ -and (Test-Path -LiteralPath $_) } | Select-Object -First 1
if (-not $dotnet) { $dotnet = (Get-Command dotnet -ErrorAction Stop).Source }
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
if ($Check) {
    & $dotnet run --project tests\PulseCapsule.Checks -c Release
    if ($LASTEXITCODE -ne 0) { throw 'Checks failed.' }
} elseif (-not $Publish) {
    & $dotnet build PulseCapsule.slnx -c Release
    if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
}
if ($Publish) {
    $publishTarget = [IO.Path]::GetFullPath((Join-Path (Join-Path $PSScriptRoot $OutputDirectory) 'PulseCapsule.exe'))
    $running = Get-Process -Name PulseCapsule -ErrorAction SilentlyContinue | Where-Object { $_.Path -eq $publishTarget }
    if ($running) { throw 'PulseCapsule is running from dist. Exit it from the tray before publishing; no process was stopped.' }
    & $dotnet publish src\PulseCapsule\PulseCapsule.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:IncludeAllContentForSelfExtract=true -p:EnableCompressionInSingleFile=true -o $OutputDirectory
    if ($LASTEXITCODE -ne 0) { throw 'Publish failed.' }
    Get-Item -LiteralPath $publishTarget | Select-Object FullName,Length
}
