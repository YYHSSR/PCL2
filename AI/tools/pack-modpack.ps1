<#
  把 PCL 的一个版本隔离实例打包成 MCBBS 格式整合包（.zip），可在 PCL2 / HMCL 中直接导入。

  压缩包结构：
    mcbbs.packmeta        游戏版本、Forge 版本等安装信息（导入时由启动器下载原版和 Forge）
    overrides/...         实例目录里的 mods、config、scripts 等全部内容，原样带走
    overrides/PCL/Setup.ini  仅携带实例的内存与 JVM/游戏参数设置（不含 Java 路径等本机信息）

  用法（在 PowerShell 中）：
    & 'E:\PCL2\AI\tools\pack-modpack.ps1'
    & 'E:\PCL2\AI\tools\pack-modpack.ps1' -IncludeSaves          # 连同存档一起打包
    & 'E:\PCL2\AI\tools\pack-modpack.ps1' -PackVersion 1.0 -OutDir 'D:\备份'
#>
param(
    [string]$Instance = 'E:\PCL2\.minecraft\versions\Chapter of Yuusha 3',
    [string]$OutDir = 'E:\PCL2\AI\modpacks',
    [string]$PackName,
    [string]$PackVersion = (Get-Date -Format 'yyyy.MM.dd-HHmm'),
    [switch]$IncludeSaves,
    [switch]$Force
)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem

$Instance = (Resolve-Path -LiteralPath $Instance).Path.TrimEnd('\')
$InstanceName = Split-Path -Leaf $Instance
if (-not $PackName) { $PackName = $InstanceName }
if (-not (Test-Path -LiteralPath (Join-Path $Instance 'mods'))) { throw "不是有效的 Mod 实例目录（缺少 mods）：$Instance" }

# ---------- 游戏正在运行时不打包，避免文件被占用或打进半写入的配置 ----------
$running = Get-CimInstance Win32_Process -Filter "Name='java.exe' OR Name='javaw.exe'" |
    Where-Object { $_.CommandLine -and $_.CommandLine.IndexOf($Instance, [StringComparison]::OrdinalIgnoreCase) -ge 0 }
if ($running) { throw '游戏正在运行，请先退出 Minecraft 再打包。' }

# ---------- 读取游戏版本与 Forge 版本 ----------
function Read-Ini([string]$Path) {
    $map = [ordered]@{}
    if (Test-Path -LiteralPath $Path) {
        foreach ($line in [IO.File]::ReadAllLines($Path, [Text.Encoding]::UTF8)) {
            $i = $line.IndexOf(':')
            if ($i -gt 0) { $map[$line.Substring(0, $i)] = $line.Substring($i + 1) }
        }
    }
    return $map
}
$ini = Read-Ini (Join-Path $Instance 'PCL\Setup.ini')
$mcVersion = $ini['VersionVanillaName']
$forgeVersion = $ini['VersionForge']
$versionJson = Join-Path $Instance "$InstanceName.json"
if ((-not $mcVersion -or -not $forgeVersion) -and (Test-Path -LiteralPath $versionJson)) {
    $gameArgs = @((Get-Content -LiteralPath $versionJson -Raw -Encoding UTF8 | ConvertFrom-Json).arguments.game | Where-Object { $_ -is [string] })
    for ($i = 0; $i -lt $gameArgs.Count - 1; $i++) {
        if ($gameArgs[$i] -eq '--fml.mcVersion' -and -not $mcVersion) { $mcVersion = $gameArgs[$i + 1] }
        if ($gameArgs[$i] -eq '--fml.forgeVersion' -and -not $forgeVersion) { $forgeVersion = $gameArgs[$i + 1] }
    }
}
if (-not $mcVersion) { throw '无法确定 Minecraft 版本。' }
if (-not $forgeVersion) { throw '无法确定 Forge 版本（目前只支持 Forge 实例）。' }

# ---------- 排除规则（相对实例目录，不区分大小写） ----------
# 由启动器在导入时重新生成，或属于本机运行时数据，不应带走
$excludeDirs = @('PCL', 'logs', 'crash-reports', 'screenshots', 'debug', '.mixin.out', 'backups', 'downloads')
if (-not $IncludeSaves) { $excludeDirs += 'saves' }
$excludeFiles = @("$InstanceName.jar", "$InstanceName.json", 'command_history.txt', 'usercache.json', 'usernamecache.json')
function Test-Excluded([string]$Rel) {
    $first = $Rel.Split('\')[0]
    if ($Rel -notlike '*\*') {
        if ($excludeFiles -contains $Rel) { return $true }
        if ($Rel -like 'hs_err_pid*.log') { return $true }
    }
    if ($excludeDirs -contains $first -and $Rel -like '*\*') { return $true }
    if ($first -like '*natives*' -or $first -like '_removed_mod_configs*') { return $true }
    return $false
}

$files = Get-ChildItem -LiteralPath $Instance -Recurse -File -Force | ForEach-Object {
    $rel = $_.FullName.Substring($Instance.Length + 1)
    if (-not (Test-Excluded $rel)) { [pscustomobject]@{ Full = $_.FullName; Rel = $rel; Length = $_.Length } }
}
$totalBytes = ($files | Measure-Object Length -Sum).Sum

# ---------- 只携带内存与参数设置（Java 路径因电脑而异，不携带） ----------
$carryKeys = 'VersionRamType', 'VersionRamCustom', 'VersionAdvanceJvm', 'VersionAdvanceGame'
$carried = @($carryKeys | Where-Object { $ini.Contains($_) -and $ini[$_] -ne '' } | ForEach-Object { "${_}:$($ini[$_])" })

# ---------- 安装信息 ----------
$meta = [ordered]@{
    manifestType    = 'minecraftModpack'
    manifestVersion = 2
    name            = $PackName
    version         = $PackVersion
    author          = ''
    description     = "Minecraft $mcVersion / Forge $forgeVersion"
    addons          = @([ordered]@{ id = 'game'; version = $mcVersion }, [ordered]@{ id = 'forge'; version = $forgeVersion })
    files           = @()
}
$metaJson = $meta | ConvertTo-Json -Depth 5

# ---------- 写入压缩包（先写临时文件，成功后再改名） ----------
New-Item -ItemType Directory -Force -Path $OutDir | Out-Null
$zipPath = Join-Path $OutDir "$PackName $PackVersion.zip"
if ((Test-Path -LiteralPath $zipPath) -and -not $Force) { throw "文件已存在：$zipPath（加 -Force 覆盖）" }
$partPath = "$zipPath.partial"
if (Test-Path -LiteralPath $partPath) { Remove-Item -LiteralPath $partPath -Force }

$utf8 = New-Object Text.UTF8Encoding($false)
$storeExt = '.jar', '.zip', '.png', '.ogg', '.jpg', '.ysm', '.mrpack', '.7z', '.gz'
$minTime = [DateTimeOffset]'1980-01-02'
Write-Host "实例：$Instance"
Write-Host ("版本：Minecraft {0} + Forge {1}；文件 {2} 个，共 {3:N0} MB" -f $mcVersion, $forgeVersion, $files.Count, ($totalBytes / 1MB))
$stream = [IO.File]::Open($partPath, [IO.FileMode]::CreateNew)
try {
    # 文件名编码必须传 Encoding.UTF8 本身：.NET Framework 只在这种情况下给中文文件名打上 UTF-8 标志，
    # 否则资源管理器、HMCL 等会按 GBK 解读，解压出乱码文件名
    $zip = New-Object IO.Compression.ZipArchive($stream, [IO.Compression.ZipArchiveMode]::Create, $false, [Text.Encoding]::UTF8)
    try {
        function Add-Text([string]$Name, [string]$Text) {
            $w = New-Object IO.StreamWriter($zip.CreateEntry($Name).Open(), $utf8)
            try { $w.Write($Text) } finally { $w.Dispose() }
        }
        Add-Text 'mcbbs.packmeta' $metaJson
        if ($carried.Count) { Add-Text 'overrides/PCL/Setup.ini' (($carried -join "`r`n") + "`r`n") }
        $done = 0L; $n = 0
        foreach ($f in $files) {
            $n++
            if ($n % 50 -eq 0) { Write-Progress -Activity '正在打包' -Status $f.Rel -PercentComplete ([int](100 * $done / [Math]::Max(1, $totalBytes))) }
            $ext = [IO.Path]::GetExtension($f.Rel).ToLowerInvariant()
            $level = if ($storeExt -contains $ext) { [IO.Compression.CompressionLevel]::NoCompression } else { [IO.Compression.CompressionLevel]::Optimal }
            $entry = $zip.CreateEntry('overrides/' + $f.Rel.Replace('\', '/'), $level)
            $time = [DateTimeOffset](Get-Item -LiteralPath $f.Full -Force).LastWriteTime
            $entry.LastWriteTime = if ($time -lt $minTime) { [DateTimeOffset]::Now } else { $time }
            $src = [IO.File]::Open($f.Full, [IO.FileMode]::Open, [IO.FileAccess]::Read, [IO.FileShare]::ReadWrite)
            try { $dst = $entry.Open(); try { $src.CopyTo($dst) } finally { $dst.Dispose() } } finally { $src.Dispose() }
            $done += $f.Length
        }
        Write-Progress -Activity '正在打包' -Completed
    } finally { $zip.Dispose() }
} finally { $stream.Dispose() }

# ---------- 校验 ----------
$check = [IO.Compression.ZipFile]::OpenRead($partPath)
try {
    $entries = @($check.Entries)
    $r = New-Object IO.StreamReader($check.GetEntry('mcbbs.packmeta').Open(), $utf8)
    try { $parsed = $r.ReadToEnd() | ConvertFrom-Json } finally { $r.Dispose() }
    $modsInZip = @($entries | Where-Object { $_.FullName -like 'overrides/mods/*' -and $_.FullName -notlike 'overrides/mods/*/*' }).Count
    $modsOnDisk = @(Get-ChildItem -LiteralPath (Join-Path $Instance 'mods') -File -Force).Count
    if ($entries.Count -ne $files.Count + 1 + [int]($carried.Count -gt 0)) { throw "条目数量不符：$($entries.Count)" }
    if ($modsInZip -ne $modsOnDisk) { throw "mod 数量不符：压缩包 $modsInZip / 实例 $modsOnDisk" }
    if (($parsed.addons | Where-Object id -eq 'forge').version -ne $forgeVersion) { throw 'mcbbs.packmeta 校验失败' }
} finally { $check.Dispose() }
Move-Item -LiteralPath $partPath -Destination $zipPath -Force

Write-Host ''
Write-Host "打包完成：$zipPath" -ForegroundColor Green
Write-Host ("大小 {0:N0} MB；mod {1} 个；携带设置：{2}" -f ((Get-Item -LiteralPath $zipPath).Length / 1MB), $modsInZip, $(if ($carried.Count) { $carried -join '，' } else { '无（使用导入方电脑的全局设置）' }))
Write-Host ("未打包：{0}" -f (($excludeDirs | ForEach-Object { "$_\" }) + $excludeFiles -join '、'))
