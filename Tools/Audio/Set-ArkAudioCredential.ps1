param(
    [ValidateSet('ark', 'seed-audio')][string]$Profile = 'ark',
    [string]$StatusPath = ''
)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing
[Windows.Forms.Application]::EnableVisualStyles()
$credentialName = if ($Profile -eq 'seed-audio') { 'VOLC_AUDIO_API_KEY' } else { 'ARK_API_KEY' }
$credentialLabel = if ($Profile -eq 'seed-audio') { 'Seed Audio (Speech platform)' } else { 'Volcengine Ark' }
if (-not $StatusPath) {
    $StatusPath = Join-Path $env:LOCALAPPDATA "DropletPrototype/$Profile-credential-status.json"
}
$script:saved = $false
$statusDirectory = Split-Path -Parent $StatusPath
[IO.Directory]::CreateDirectory($statusDirectory) | Out-Null
function Write-ConfigurationStatus([string]$State) {
    [PSCustomObject]@{
        state = $State
        variable = $credentialName
        helperPid = $PID
        scope = 'WindowsUserEnvironment'
        timestamp = [DateTime]::UtcNow.ToString('o')
        apiTested = $false
        paidRequests = 0
    } | ConvertTo-Json | Set-Content -LiteralPath $StatusPath -Encoding UTF8
}
Write-ConfigurationStatus 'starting_local_prompt'
$form = New-Object Windows.Forms.Form
$form.Text = "$credentialLabel - local API key configuration"
$form.ClientSize = New-Object Drawing.Size(540, 215)
$form.StartPosition = 'CenterScreen'
$form.FormBorderStyle = 'FixedDialog'
$form.MaximizeBox = $false
$form.MinimizeBox = $false
$form.TopMost = $true
$form.ShowInTaskbar = $true
$label = New-Object Windows.Forms.Label
$label.Location = New-Object Drawing.Point(20, 18)
$label.Size = New-Object Drawing.Size(500, 76)
$label.Text = "Paste your $credentialLabel key below (input is hidden).`r`nSave to $credentialName in your Windows user environment.`r`nWindows stores this variable unencrypted, outside the project.`r`nOther keys are preserved. This window makes no API calls."
$box = New-Object Windows.Forms.TextBox
$box.Location = New-Object Drawing.Point(20, 105)
$box.Size = New-Object Drawing.Size(500, 25)
$box.UseSystemPasswordChar = $true
$save = New-Object Windows.Forms.Button
$save.Text = 'Save locally'
$save.Location = New-Object Drawing.Point(294, 158)
$save.Size = New-Object Drawing.Size(110, 32)
$cancel = New-Object Windows.Forms.Button
$cancel.Text = 'Cancel'
$cancel.Location = New-Object Drawing.Point(414, 158)
$cancel.Size = New-Object Drawing.Size(106, 32)
$cancel.DialogResult = [Windows.Forms.DialogResult]::Cancel
$form.Controls.AddRange(@($label, $box, $save, $cancel))
$form.AcceptButton = $save
$form.CancelButton = $cancel
$save.Add_Click({
    $localSecret = $box.Text.Trim()
    try {
        if ([string]::IsNullOrWhiteSpace($localSecret) -or $localSecret -match '\s') {
            [Windows.Forms.MessageBox]::Show('Enter one API key without embedded whitespace.', 'Check input') | Out-Null
            return
        }
        [Environment]::SetEnvironmentVariable($credentialName, $localSecret, 'User')
        if ([Environment]::GetEnvironmentVariable($credentialName, 'User') -cne $localSecret) {
            throw 'Credential persistence check failed.'
        }
        $script:saved = $true
        Write-ConfigurationStatus 'saved_and_readback_verified'
        $box.Clear()
        $form.Close()
    } catch {
        Write-ConfigurationStatus 'configuration_error'
        [Windows.Forms.MessageBox]::Show('Local configuration failed. No key value was logged.', 'Configuration error') | Out-Null
    } finally { $localSecret = $null }
})
$form.Add_Shown({
    Write-ConfigurationStatus 'awaiting_local_input'
    $form.Activate()
    $form.BringToFront()
    [void]$box.Focus()
})
try {
    [void]$form.ShowDialog()
    if (-not $script:saved) { Write-ConfigurationStatus 'cancelled_without_save' }
} finally {
    $box.Clear()
    $form.Dispose()
}
