param([int]$PlayerProcessId,[string]$OutputPath)
$ErrorActionPreference='Stop'
while (Get-Process -Id $PlayerProcessId -ErrorAction SilentlyContinue) {
    $samples=(Get-Counter -Counter '\GPU Process Memory(*)\Dedicated Usage','\GPU Process Memory(*)\Shared Usage' -SampleInterval 1 -MaxSamples 1).CounterSamples
    $playerCounters=$samples | Where-Object {$_.InstanceName -like ('pid_'+$PlayerProcessId+'_*')}
    $dedicated=($playerCounters | Where-Object {$_.Path -like '*\dedicated usage'} | Measure-Object CookedValue -Sum).Sum
    $shared=($playerCounters | Where-Object {$_.Path -like '*\shared usage'} | Measure-Object CookedValue -Sum).Sum
    [pscustomobject]@{utc=[DateTime]::UtcNow.ToString('o');pid=$PlayerProcessId;dedicatedBytes=$dedicated;sharedBytes=$shared;samples=$playerCounters.Count} | Export-Csv -NoTypeInformation -Append -Encoding UTF8 -LiteralPath $OutputPath
}
