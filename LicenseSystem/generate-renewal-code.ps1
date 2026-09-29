<#
    generate-renewal-code.ps1  —  VENDOR-ONLY tool. Keep this file private.

    When a customer's license expires, the app shows them an "Activation ID"
    like  A1B2C3D4-01 . They read it out to you. You run:

        .\generate-renewal-code.ps1 -ActivationId A1B2C3D4-01

    and read the resulting code back to them. They type it into the app and it
    extends their license by one more year.

    IMPORTANT: The secret is NOT stored in this file. It is read from
    LicenseSystem\.secret (git-ignored) or the POSAPP_RENEWAL_SECRET environment
    variable, and must match RenewalSecret in
    POSApp.Infrastructure/Services/LicenseService.cs exactly.

    If you change one, change both — and only ever change it deliberately, because
    renewal codes you have already handed out stop working.

    Full documentation: see README.md in this same LicenseSystem folder.
#>

param(
    [Parameter(Mandatory = $true)]
    [string]$ActivationId
)

# Must be identical to LicenseService.RenewalSecret.
# Kept out of source control: put it in LicenseSystem\.secret next to this script,
# or set POSAPP_RENEWAL_SECRET in your environment.
$Secret = $env:POSAPP_RENEWAL_SECRET
if (-not $Secret) {
    $secretFile = Join-Path $PSScriptRoot ".secret"
    if (Test-Path $secretFile) {
        $Secret = (Get-Content $secretFile -Raw).Trim()
    }
}
if (-not $Secret) {
    Write-Error "No renewal secret found. Create LicenseSystem\.secret containing the secret, or set POSAPP_RENEWAL_SECRET."
    exit 1
}

function Convert-ToBase32([byte[]]$data) {
    $alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567"
    $sb = New-Object System.Text.StringBuilder
    $buffer = 0
    $bitsLeft = 0
    foreach ($b in $data) {
        $buffer = ($buffer -shl 8) -bor $b
        $bitsLeft += 8
        while ($bitsLeft -ge 5) {
            $bitsLeft -= 5
            [void]$sb.Append($alphabet[($buffer -shr $bitsLeft) -band 31])
        }
    }
    if ($bitsLeft -gt 0) {
        [void]$sb.Append($alphabet[($buffer -shl (5 - $bitsLeft)) -band 31])
    }
    return $sb.ToString()
}

$normalized = $ActivationId.ToUpperInvariant()

$hmac = New-Object System.Security.Cryptography.HMACSHA256
$hmac.Key = [System.Text.Encoding]::UTF8.GetBytes($Secret)
$mac = $hmac.ComputeHash([System.Text.Encoding]::UTF8.GetBytes($normalized))

$text = (Convert-ToBase32 $mac).Substring(0, 16)
$code = "{0}-{1}-{2}-{3}" -f $text.Substring(0,4), $text.Substring(4,4), $text.Substring(8,4), $text.Substring(12,4)

Write-Host ""
Write-Host "Activation ID : $ActivationId"
Write-Host "Renewal Code  : $code"
Write-Host ""
