param([string]$ProjectRoot = (Get-Location).Path)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Speech
$radioSynth = New-Object System.Speech.Synthesis.SpeechSynthesizer
$radioVoice = $radioSynth.GetInstalledVoices() | Where-Object { $_.Enabled -and $_.VoiceInfo.Culture.Name -eq 'zh-CN' } | Select-Object -First 1
if ($null -eq $radioVoice) { throw 'No installed offline zh-CN voice. Subtitles remain usable; no paid service is invoked.' }
$radioSynth.SelectVoice($radioVoice.VoiceInfo.Name)
$radioSynth.Rate = 4
$radioSynth.Volume = 88
$radioSource = Join-Path $ProjectRoot 'Assets\_Project\Data\NarrativeCombat\RadioContent.json'
$radioContent = Get-Content -LiteralPath $radioSource -Raw -Encoding UTF8 | ConvertFrom-Json
$radioOutput = Join-Path $ProjectRoot 'Assets\_Project\Audio\NarrativeCombat'
New-Item -ItemType Directory -Path $radioOutput -Force | Out-Null
$radioResults = @()
try {
    foreach ($radioLine in @($radioContent.combat) + @($radioContent.narrative)) {
        $radioWave = Join-Path $radioOutput ($radioLine.id + '.wav')
        $radioSynth.SetOutputToWaveFile($radioWave)
        $radioSynth.Speak($radioLine.text)
        $radioSynth.SetOutputToNull()
        $radioResults += [pscustomobject]@{ id=$radioLine.id; voice=$radioVoice.VoiceInfo.Name; file=$radioWave; bytes=(Get-Item -LiteralPath $radioWave).Length }
    }
} finally { $radioSynth.Dispose() }
function Write-RadioTone([string]$FileName, [bool]$Noise) {
    $toneRate = 22050
    $toneCount = [int]($toneRate * 0.19)
    $toneFile = [System.IO.File]::Create((Join-Path $radioOutput $FileName))
    $toneWriter = New-Object System.IO.BinaryWriter($toneFile)
    $toneRandom = New-Object System.Random(1701)
    try {
        $toneWriter.Write([System.Text.Encoding]::ASCII.GetBytes('RIFF')); $toneWriter.Write([int](36 + $toneCount * 2))
        $toneWriter.Write([System.Text.Encoding]::ASCII.GetBytes('WAVEfmt ')); $toneWriter.Write([int]16)
        $toneWriter.Write([int16]1); $toneWriter.Write([int16]1); $toneWriter.Write([int]$toneRate)
        $toneWriter.Write([int]($toneRate * 2)); $toneWriter.Write([int16]2); $toneWriter.Write([int16]16)
        $toneWriter.Write([System.Text.Encoding]::ASCII.GetBytes('data')); $toneWriter.Write([int]($toneCount * 2))
        for ($toneIndex = 0; $toneIndex -lt $toneCount; $toneIndex++) {
            $toneT = $toneIndex / [double]$toneRate
            $toneEnvelope = [Math]::Sin([Math]::PI * $toneIndex / $toneCount)
            if ($Noise) { $toneValue = ($toneRandom.NextDouble() * 2 - 1) * 0.16 }
            else { $toneFrequency = if ($toneT -lt .095) { 1120 } else { 1530 }; $toneValue = [Math]::Sin(2 * [Math]::PI * $toneFrequency * $toneT) * .18 }
            $toneWriter.Write([int16]($toneValue * $toneEnvelope * 32767))
        }
    } finally { $toneWriter.Dispose(); $toneFile.Dispose() }
}
Write-RadioTone 'ConnectTone.wav' $false
Write-RadioTone 'InterruptTone.wav' $true
$radioResults | ConvertTo-Json -Depth 3
