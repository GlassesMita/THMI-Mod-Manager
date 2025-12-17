<#
.SYNOPSIS
Install .cer format certificate to the trusted root certification authorities of the local computer
.REQUIREMENTS
- Run PowerShell as administrator
- Certificate file in .cer format
#>

# Check administrator privileges
$currentPrincipal = New-Object Security.Principal.WindowsPrincipal([Security.Principal.WindowsIdentity]::GetCurrent())
if (-not $currentPrincipal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    Write-Host "Error: Please run this script as [Administrator]!" -ForegroundColor Red
    Read-Host -Prompt "Press any key to exit"
    exit 1
}

# Interactive input for certificate path
$certPath = Read-Host -Prompt "Please enter the full path of the certificate file (.cer):"

# Verify if file exists
if (-not (Test-Path -Path $certPath -PathType Leaf)) {
    Write-Host "Error: Certificate file '$certPath' does not exist!" -ForegroundColor Red
    Read-Host -Prompt "Press any key to exit"
    exit 1
}

# Install certificate to LocalMachine\Root (Trusted Root Certification Authorities)
try {
    Write-Host "Installing certificate to trusted root certificate store..." -ForegroundColor Cyan
    Import-Certificate -FilePath $certPath -CertStoreLocation "Cert:\LocalMachine\Root" -ErrorAction Stop
    Write-Host "Certificate installed successfully!" -ForegroundColor Green
}
catch {
    Write-Host "Certificate installation failed: $($_.Exception.Message)" -ForegroundColor Red
}

Read-Host -Prompt "Press any key to exit"