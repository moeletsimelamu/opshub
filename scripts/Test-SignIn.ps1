<#
.SYNOPSIS
    Signs in to a running OpsHub API the way the Angular app will, then checks what
    that user is allowed to do.

.DESCRIPTION
    1. Fetches an XSRF token (GET /api/auth/antiforgery).
    2. Signs in (POST /api/auth/login) with the token in the X-XSRF-TOKEN header.
    3. Calls the admin-only endpoint GET /api/users (200 for admins, 403 for others).
    4. Signs out and confirms the session no longer works (401).

    Works in Windows PowerShell 5.1 and PowerShell 7. The password is read without
    being shown on screen or stored in your command history.

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File .\scripts\Test-SignIn.ps1 -Email lerato@marula.example

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File .\scripts\Test-SignIn.ps1 -Email thandi@marula.example
#>
param(
    [Parameter(Mandatory = $true)]
    [string] $Email,

    [System.Security.SecureString] $Password,

    [string] $BaseUrl = "https://localhost:7003"
)

$ErrorActionPreference = "Stop"

if (-not $Password) {
    $Password = Read-Host "Password for $Email" -AsSecureString
}
$plainPassword = [System.Net.NetworkCredential]::new("", $Password).Password
$session = New-Object Microsoft.PowerShell.Commands.WebRequestSession

function Get-XsrfToken {
    ($session.Cookies.GetCookies($BaseUrl) | Where-Object Name -eq "XSRF-TOKEN").Value
}

function Get-StatusCode($errorRecord) {
    if ($errorRecord.Exception.Response) { return [int]$errorRecord.Exception.Response.StatusCode }
    return $null
}

# ---------- 1. XSRF token ----------
Write-Host "1. Getting an XSRF token..."
try {
    Invoke-WebRequest "$BaseUrl/api/auth/antiforgery" -WebSession $session -UseBasicParsing | Out-Null
}
catch {
    Write-Host "   Could not reach $BaseUrl. Is the API running with --launch-profile https?" -ForegroundColor Red
    Write-Host "   $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}
Write-Host "   OK: received the XSRF-TOKEN cookie" -ForegroundColor Green

# ---------- 2. Sign in ----------
Write-Host "2. Signing in as $Email..."
$body = @{ email = $Email; password = $plainPassword } | ConvertTo-Json
try {
    $me = Invoke-RestMethod "$BaseUrl/api/auth/login" -Method Post -WebSession $session `
        -ContentType "application/json" -Headers @{ "X-XSRF-TOKEN" = (Get-XsrfToken) } -Body $body
}
catch {
    $status = Get-StatusCode $_
    Write-Host "   Sign-in failed: HTTP $status" -ForegroundColor Red
    if ($status -eq 401) {
        Write-Host "   Wrong email or password, or the account is locked/deactivated." -ForegroundColor Red
        Write-Host "   Your demo password: dotnet user-secrets list --project src/OpsHub.Api" -ForegroundColor Red
    }
    exit 1
}
Write-Host "   OK: signed in as $($me.displayName) ($($me.role))" -ForegroundColor Green

# ---------- 3. Admin-only endpoint ----------
Write-Host "3. Calling the admin-only endpoint GET /api/users..."
try {
    $page = Invoke-RestMethod "$BaseUrl/api/users?pageSize=5" -WebSession $session
    Write-Host "   OK: HTTP 200, $($page.totalCount) users in total. First $($page.items.Count):" -ForegroundColor Green
    $page.items | Format-Table displayName, email, role, isActive -AutoSize | Out-Host
}
catch {
    $status = Get-StatusCode $_
    if ($status -eq 403) {
        Write-Host "   HTTP 403 Forbidden: expected, because $($me.role) is not an Administrator." -ForegroundColor Yellow
    }
    else {
        Write-Host "   Unexpected result: HTTP $status" -ForegroundColor Red
        exit 1
    }
}

# ---------- 4. Sign out ----------
Write-Host "4. Signing out..."
Invoke-RestMethod "$BaseUrl/api/auth/logout" -Method Post -WebSession $session `
    -Headers @{ "X-XSRF-TOKEN" = (Get-XsrfToken) } | Out-Null
try {
    Invoke-RestMethod "$BaseUrl/api/auth/me" -WebSession $session | Out-Null
    Write-Host "   Unexpected: still signed in after logout" -ForegroundColor Red
    exit 1
}
catch {
    $status = Get-StatusCode $_
    if ($status -eq 401) {
        Write-Host "   OK: signed out; /api/auth/me now returns 401" -ForegroundColor Green
    }
    else {
        Write-Host "   Unexpected result after logout: HTTP $status" -ForegroundColor Red
        exit 1
    }
}
