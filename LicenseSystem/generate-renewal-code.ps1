<#
    generate-renewal-code.ps1  —  VENDOR-ONLY tool. Keep this file private.

    When a customer's license expires, the app shows them an "Activation ID"
    like  A1B2C3D4-01 . They read it out to you. You run:

        .\generate-renewal-code.ps1 -ActivationId A1B2C3D4-01

    and read the resulting code back to them. They type it into the app and it
    extends their license by one more year.

    IMPORTANT: The $Secret below MUST match RenewalSecret in
    POSApp.Infrastructure/Services/LicenseService.cs exactly. If you change one,
    change both — and only ever change it before shipping to customers.

    Full documentation: see README.md in this same LicenseSystem folder.
#>

param(
    [Parameter(Mandatory = $true)]
    [string]$ActivationId
)

# Must be identical to LicenseService.RenewalSecret
$Secret = "ShahJeePOS::kQ7vN2pR9sT4wX1zA6bC8dE0fG3hJ5kL7mN9pQ2rS4tU6vW::renewal-v1"

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
