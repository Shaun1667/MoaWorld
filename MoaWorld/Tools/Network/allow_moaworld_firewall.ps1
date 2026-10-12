# Lets other PCs on the LAN join a MoaWorld room hosted on this PC.
# Turns the Private/Public firewall back on (if it was switched off) and allows only the game's UDP ports
# on Private networks: 7777 (game connection) and 47777 (room list).
# Run in an administrator PowerShell:  powershell -ExecutionPolicy Bypass -File allow_moaworld_firewall.ps1

$ruleName = "MoaWorld (UDP 7777, 47777)"

if (-not ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    Write-Host "관리자 권한 PowerShell에서 실행해 주세요." -ForegroundColor Yellow
    exit 1
}

Set-NetFirewallProfile -Profile Private,Public -Enabled True

if (Get-NetFirewallRule -DisplayName $ruleName -ErrorAction SilentlyContinue) {
    Write-Host "이미 규칙이 있어요: $ruleName"
} else {
    New-NetFirewallRule -DisplayName $ruleName -Direction Inbound -Protocol UDP -LocalPort 7777,47777 -Action Allow -Profile Private | Out-Null
    Write-Host "규칙을 추가했어요: $ruleName"
}

$category = (Get-NetConnectionProfile | Select-Object -First 1).NetworkCategory
if ($category -ne "Private") {
    Write-Host "현재 네트워크가 '$category'예요. 설정 > 네트워크에서 '개인 네트워크'로 바꿔야 접속돼요." -ForegroundColor Yellow
}
