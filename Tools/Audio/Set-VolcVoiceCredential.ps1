param([ValidateSet('seed-tts')][string]$Provider = 'seed-tts')
$ErrorActionPreference = 'Stop'
$voiceCredentialName = 'VOLC_SPEECH_API_KEY'
Write-Host "Enter $voiceCredentialName. It will be stored in your Windows user environment (not encrypted), never in the project."
$voiceSecret = Read-Host 'API key (input hidden)' -AsSecureString
$voicePointer = [IntPtr]::Zero
try {
    $voicePointer = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($voiceSecret)
    $voicePlain = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($voicePointer)
    if ([string]::IsNullOrWhiteSpace($voicePlain)) { throw 'Empty key; no change made.' }
    [Environment]::SetEnvironmentVariable($voiceCredentialName, $voicePlain.Trim(), 'User')
    Write-Host "$voiceCredentialName saved. volc_voice.py can read it immediately; the key has not been printed."
} finally {
    if ($voicePointer -ne [IntPtr]::Zero) { [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($voicePointer) }
    $voicePlain = $null
    $voiceSecret.Dispose()
}
