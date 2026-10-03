param(
    [string]$ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Speech
$ledgerPath = Join-Path $ProjectRoot 'ArtSource/Audio/SeedTTS20260929/ledger.json'
$ledger = [IO.File]::ReadAllText($ledgerPath, [Text.Encoding]::UTF8) | ConvertFrom-Json
$recognizerInfo = [System.Speech.Recognition.SpeechRecognitionEngine]::InstalledRecognizers() |
    Where-Object { $_.Culture.Name -eq 'zh-CN' } | Select-Object -First 1
if ($null -eq $recognizerInfo) { throw 'No installed Chinese recognizer; no installation attempted.' }
$checks = @()
foreach ($entry in $ledger.entries) {
    if ($entry.status -ne 'complete' -or $entry.payload.req_params.speaker -notlike 'zh_*') { continue }
    $sourcePath = Join-Path (Split-Path (Join-Path $ProjectRoot $entry.job) -Parent) 'voice.wav'
    $engine = New-Object System.Speech.Recognition.SpeechRecognitionEngine($recognizerInfo)
    try {
        # Free dictation only: never inject the requested text as a forced grammar.
        $engine.LoadGrammar((New-Object System.Speech.Recognition.DictationGrammar))
        $engine.SetInputToWaveFile($sourcePath)
        $segments = @()
        # Synchronous Recognize consumes one utterance and closes input at EOF.
        # Inspect only that first utterance; a second call can have no input.
        $recognized = $engine.Recognize()
        if ($null -ne $recognized) {
            $segments += [PSCustomObject]@{
                text = $recognized.Text
                confidence = $recognized.Confidence
                positionSeconds = $recognized.Audio.AudioPosition.TotalSeconds
                durationSeconds = $recognized.Audio.Duration.TotalSeconds
            }
        }
        $checks += [PSCustomObject]@{
            id = $entry.id
            audio = $sourcePath
            expectedNonSpeech = $entry.category -ne 'voice_control'
            inputText = $entry.payload.req_params.text
            sha256 = $entry.sha256
            transcript = ($segments | ForEach-Object { $_.text }) -join ''
            segments = $segments
            inference = 'Speech recognition is supporting evidence only; empty transcript does not prove non-speech or acceptable quality.'
        }
    }
    finally { $engine.Dispose() }
}
$evidenceDirectory = Join-Path $ProjectRoot 'docs/verification/SeedTTS-Soundscape-20260929'
[IO.Directory]::CreateDirectory($evidenceDirectory) | Out-Null
$report = [PSCustomObject]@{
    engine = $recognizerInfo.Description
    culture = $recognizerInfo.Culture.Name
    mode = 'offline_first_utterance_free_dictation_no_prompt_grammar'
    generatedAt = [DateTime]::UtcNow.ToString('o')
    checks = $checks
}
$json = $report | ConvertTo-Json -Depth 10
$reportPath = Join-Path $evidenceDirectory 'offline-speech-inspection.json'
if ([IO.File]::Exists($reportPath)) {
    $prior = [IO.File]::ReadAllText($reportPath, [Text.Encoding]::UTF8) | ConvertFrom-Json
    $stamp = ([DateTime]$prior.generatedAt).ToUniversalTime().ToString('yyyyMMddTHHmmssfffZ')
    $archivePath = Join-Path $evidenceDirectory ('speech-inspection-' + $stamp + '.json')
    if (-not [IO.File]::Exists($archivePath)) { [IO.File]::Copy($reportPath, $archivePath) }
}
[IO.File]::WriteAllText($reportPath, $json, (New-Object Text.UTF8Encoding($false)))
[Console]::OutputEncoding = [Text.Encoding]::UTF8
$json
