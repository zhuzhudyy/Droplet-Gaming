param([string]$PlayerRun='Player-First')
$ErrorActionPreference='Stop'
$taskRoot=(Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '../..')).Path
$evidence=Join-Path $taskRoot 'docs/verification/NarrativeCombat'
$baseline=Get-Content -LiteralPath (Join-Path $evidence 'protected-baseline.json') -Raw -Encoding UTF8 | ConvertFrom-Json
$changes=@(foreach($entry in $baseline) {
    $target=Join-Path $taskRoot $entry.path
    if(!(Test-Path -LiteralPath $target)) { [pscustomobject]@{path=$entry.path;status='Missing'} }
    elseif((Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash -ne $entry.sha256) { [pscustomobject]@{path=$entry.path;status='Changed'} }
})
$reportFile=Get-ChildItem -LiteralPath (Join-Path $evidence $PlayerRun) -Recurse -Filter 'narrative-combat-validation.json' | Sort-Object LastWriteTime -Descending | Select-Object -First 1
$report=Get-Content -LiteralPath $reportFile.FullName -Raw -Encoding UTF8 | ConvertFrom-Json
$memory=@(Import-Csv -LiteralPath (Join-Path $evidence ($PlayerRun+'-wddm.csv')) | Where-Object {[int]$_.samples -gt 0})
$dedicated=@($memory | ForEach-Object {[double]$_.dedicatedBytes})
$shared=@($memory | ForEach-Object {[double]$_.sharedBytes})
$build=Join-Path $taskRoot 'Builds/Windows-NarrativeCombat'
$buildAudit=Get-Content -LiteralPath (Join-Path $evidence 'windows-build.json') -Raw -Encoding UTF8 | ConvertFrom-Json
$processExit=Get-Content -LiteralPath (Join-Path $evidence ($PlayerRun+'-wddm.csv.exit.json')) -Raw -Encoding UTF8 | ConvertFrom-Json
$delivery=[pscustomobject]@{
    utc=[DateTime]::UtcNow.ToString('o')
    protectedFiles=$baseline.Count
    protectedUnchanged=$baseline.Count-$changes.Count
    protectedChanges=$changes
    buildDirectoryBytes=(Get-ChildItem -LiteralPath $build -File -Recurse | Measure-Object Length -Sum).Sum
    buildDirectoryFiles=(Get-ChildItem -LiteralPath $build -File -Recurse).Count
    build=$buildAudit
    testedProcessExit=$processExit
    playerReport=$reportFile.FullName
    completed=$report.completed
    allChecksPassed=$report.allChecksPassed
    checkCount=$report.checks.Count
    targetCount=$report.totalTargets
    uniqueIds=$report.distinctIds
    nativeResolution=@($report.width,$report.height)
    validationMode=$report.validationMode
    phases=@($report.phases | Select-Object name,frames,focusedFrames,meanFps,frameMs,cpuFrameMs,gpuMs,targets,destroyed,pending,escaped,movedShips,fleeing,fleetVisibleInstancedShips,fleetInstancedDrawCalls,fleetChunkSizeUnits)
    processGpuMemory=[pscustomobject]@{
        source='Windows WDDM GPU Process Memory counters, matched exact tested player PID; timestamped discrete samples'
        scope='Raw-row arithmetic means, not time-weighted integrals. Overlapping samplers are not independent runs. Sampled portion of this run, not a thermal soak. Dedicated allocation is distinct from shared system GPU memory, Unity tracking, and adapter capacity.'
        samples=$memory.Count
        firstUtc=$memory[0].utc
        lastUtc=$memory[-1].utc
        pid=$memory[0].pid
        dedicatedMeanBytes=($dedicated | Measure-Object -Average).Average
        dedicatedPeakBytes=($dedicated | Measure-Object -Maximum).Maximum
        sharedMeanBytes=($shared | Measure-Object -Average).Average
        sharedPeakBytes=($shared | Measure-Object -Maximum).Maximum
    }
}
$json=$delivery | ConvertTo-Json -Depth 8
[IO.File]::WriteAllText((Join-Path $evidence 'delivery-audit.json'),$json,[Text.UTF8Encoding]::new($false))
$json
