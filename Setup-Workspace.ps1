# Рабочее место TNov на новой машине: клонирует недостающие репозитории рядом с TNovDeploy,
# собирает TNovDeploy (автодеплой после сборки TNov) и TNovReleaser (проверка совместимости
# TNovCommon и выпуск), создаёт папку локальной сборки TNovBuild.
#   powershell -ExecutionPolicy Bypass -File Setup-Workspace.ps1
param(
    [string]$Owner = 'novabimperm-creator',
    # Без ключа уже клонированные репозитории не трогаются; с ключом — git pull --ff-only.
    [switch]$Pull
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$root = Split-Path $PSScriptRoot -Parent

# Решение TNov (ProjectReference ..\<репозиторий>) и инструменты вокруг него.
$repos = @(
    'TNov', 'TNovCommon', 'TNovApi',
    'TNovBeams', 'TNovBIMUtils', 'TNovElectrical', 'TNovFinishing', 'TNovMEPSpec',
    'TNovParking', 'TNovPiles', 'TNovRooms', 'TNovSS', 'TNovTasks',
    'TNovUtils', 'TNovUtilsAR', 'TNovUtilsST', 'TNovVent', 'TNovViewsSheets',
    'TNovClient', 'TNovReleaser', 'TNovDesktop'
)

if (-not (Get-Command git -ErrorAction SilentlyContinue)) { throw 'Не найден git' }

foreach ($repo in $repos) {
    $path = Join-Path $root $repo
    if (Test-Path (Join-Path $path '.git')) {
        if ($Pull) {
            Write-Output "[pull]  $repo"
            git -C $path pull --ff-only
        }
        continue
    }
    Write-Output "[clone] $repo"
    git clone "https://github.com/$Owner/$repo.git" $path
    if ($LASTEXITCODE -ne 0) { throw "git clone ${repo}: код $LASTEXITCODE" }
}

New-Item -ItemType Directory -Force (Join-Path $root 'TNovBuild\2022'), (Join-Path $root 'TNovBuild\2027') | Out-Null

Write-Output '[build] TNovDeploy'
dotnet build (Join-Path $PSScriptRoot 'TNovDeploy.csproj') -c Release -nologo -v:q
if ($LASTEXITCODE -ne 0) { throw "сборка TNovDeploy: код $LASTEXITCODE" }

# TNovReleaser — .NET Framework с WPF: нужен MSBuild из Visual Studio.
$vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
$msbuild = if (Test-Path $vswhere) { & $vswhere -latest -products * -find 'MSBuild\**\Bin\MSBuild.exe' | Select-Object -First 1 }
if ($msbuild) {
    Write-Output '[build] TNovReleaser'
    & $msbuild (Join-Path $root 'TNovReleaser\TNovReleaser.csproj') -restore -p:Configuration=Release -nologo -v:q
    if ($LASTEXITCODE -ne 0) { throw "сборка TNovReleaser: код $LASTEXITCODE" }
} else {
    Write-Warning 'MSBuild (Visual Studio) не найден — TNovReleaser не собран: проверка совместимости TNovCommon будет пропускаться.'
}

Write-Output ''
Write-Output "Готово. Решение: $(Join-Path $root 'TNov\TNov.sln'), сборка: $(Join-Path $root 'TNovBuild')"
