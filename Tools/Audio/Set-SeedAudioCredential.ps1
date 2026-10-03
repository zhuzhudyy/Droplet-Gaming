# Local password entry only. Never performs API calls or prints the key.
& (Join-Path $PSScriptRoot 'Set-ArkAudioCredential.ps1') -Profile 'seed-audio'
