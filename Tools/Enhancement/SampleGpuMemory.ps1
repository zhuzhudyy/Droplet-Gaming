param([Parameter(Mandatory=$true)][int]$PlayerProcessId,[Parameter(Mandatory=$true)][string]$OutputPath)
$ErrorActionPreference='Stop'
$player=Get-Process -Id $PlayerProcessId
if($player.Path -notlike '*\Builds\Windows-Enhanced-20260919\DropletGaming.exe') { throw 'Only the authored Enhancement player may be sampled.' }
$null=$player.Handle
while(!$player.HasExited) {
    $result=Get-Counter -Counter '\GPU Process Memory(*)\Dedicated Usage','\GPU Process Memory(*)\Shared Usage' -SampleInterval 1 -MaxSamples 1 -ErrorAction SilentlyContinue
    $samples=@($result.CounterSamples | Where-Object {$_.Status -eq 0 -and $_.InstanceName -like ('pid_'+$PlayerProcessId+'_*')})
    if($samples.Count -eq 0) { if($player.HasExited){break}; continue }
    $dedicated=($samples | Where-Object {$_.Path -like '*\dedicated usage'} | Measure-Object CookedValue -Sum).Sum
    $shared=($samples | Where-Object {$_.Path -like '*\shared usage'} | Measure-Object CookedValue -Sum).Sum
    [pscustomobject]@{utc=[DateTime]::UtcNow.ToString('o');pid=$PlayerProcessId;dedicatedBytes=$dedicated;sharedBytes=$shared;samples=$samples.Count} |
        Export-Csv -NoTypeInformation -Append -Encoding UTF8 -LiteralPath $OutputPath
    $player.Refresh()
}
$player.WaitForExit()
$exit=[pscustomobject]@{pid=$PlayerProcessId;utc=[DateTime]::UtcNow.ToString('o');exitCode=$player.ExitCode}
[IO.File]::WriteAllText(($OutputPath+'.exit.json'),($exit | ConvertTo-Json),[Text.UTF8Encoding]::new($false))
$exit | ConvertTo-Json
