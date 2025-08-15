<#  Check-IPv6Leak.ps1
    Проверяет наличие публичного IPv6, дефолтный IPv6-маршрут и пытается
    определить вероятную утечку IPv6 мимо VPN.
    Выход: код 0 — утечки не видно, 2 — вероятна утечка.
#>

[CmdletBinding()]
param([switch]$Quiet)

function Write-Status {
  param([ValidateSet('OK','WARN','FAIL','INFO')]$Level,[string]$Message)
  $color = switch ($Level) { 'OK'{'Green'} 'WARN'{'Yellow'} 'FAIL'{'Red'} default{'Cyan'} }
  if ($Quiet){ Write-Output "[$Level] $Message" } else { Write-Host "[$Level] $Message" -ForegroundColor $color }
}

try { [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12 } catch {}

# 1) Локальные IPv6
$allV6 = Get-NetIPAddress -AddressFamily IPv6 -ErrorAction SilentlyContinue
$localGlobalV6 = $allV6 | ? { $_.IPAddress -match '^(2|3)[0-9a-f]' } | 
                 Select InterfaceAlias, IPAddress, PrefixLength, Type
$localUlaV6    = $allV6 | ? { $_.IPAddress -match '^(fc|fd)' } | 
                 Select InterfaceAlias, IPAddress, PrefixLength, Type

if ($localGlobalV6) {
  Write-Status INFO "Локальные глобальные IPv6 найдены:"
  $localGlobalV6 | Format-Table | Out-String | % { if($Quiet){$_} else {Write-Host $_} }
} else {
  Write-Status OK "Глобальных локальных IPv6 не найдено (только link-local/нет)."
}
if ($localUlaV6) {
  Write-Status INFO "Есть ULA (fc00/fd00) адреса — не публичные:"
  $localUlaV6 | Format-Table | Out-String | % { if($Quiet){$_} else {Write-Host $_} }
}

# 2) Дефолтный маршрут IPv6
$v6Route = Get-NetRoute -AddressFamily IPv6 -ErrorAction SilentlyContinue |
           ? DestinationPrefix -eq '::/0' |
           Sort-Object { $_.RouteMetric ?? $_.Metric } |
           Select -First 1
if ($v6Route) {
  Write-Status INFO ("IPv6 default route → Interface: {0}, NextHop: {1}, Metric: {2}" -f `
    $v6Route.InterfaceAlias, $v6Route.NextHop, ($v6Route.RouteMetric ?? $v6Route.Metric))
} else {
  Write-Status OK "Дефолтного маршрута IPv6 нет — IPv6 в интернет не пойдёт."
}

# 3) Публичные IP по v4 и v6
function Get-PublicIP {
  param([ValidateSet('v4','v6')]$Family)
  $urls = if ($Family -eq 'v6') {
    @('https://api6.ipify.org?format=json','https://ipv6.icanhazip.com')
  } else {
    @('https://api4.ipify.org?format=json','https://ipv4.icanhazip.com')
  }
  foreach ($u in $urls) {
    try {
      if ($u -like '*format=json*') {
        $r = Invoke-RestMethod -Uri $u -TimeoutSec 6 -ErrorAction Stop
        return ($r.ip ?? $r) -as [string]
      } else {
        $r = Invoke-WebRequest -Uri $u -TimeoutSec 6 -ErrorAction Stop
        return ($r.Content.Trim()) -as [string]
      }
    } catch { continue }
  }
  return $null
}

$pubV4 = Get-PublicIP v4
if ($pubV4) { Write-Status OK "Public IPv4: $pubV4" } else { Write-Status WARN "Не удалось определить Public IPv4." }

$pubV6 = Get-PublicIP v6
if ($pubV6) { Write-Status WARN "Public IPv6: $pubV6 (IPv6 доступен в интернет)" }
else        { Write-Status OK   "Запросы по IPv6 не проходят — публичный IPv6 не виден." }

# 4) Эвристика утечки
$vpnKeywords = 'VPN','WireGuard','OpenVPN','TAP','TUN','Nord','Express','Proton','Surfshark','Mullvad','Cloudflare WARP','WARP','ZeroTier','Outline','wg','tun','tap'
$ifaceIsVpn = $false
if ($v6Route) {
  foreach ($k in $vpnKeywords) { if ($v6Route.InterfaceAlias -match [Regex]::Escape($k)) { $ifaceIsVpn = $true; break } }
}

$leakLikely = $false
if ($pubV6) {
  if (-not $v6Route) { $leakLikely = $true }          # есть публичный v6, но нет def route (аномалия)
  elseif (-not $ifaceIsVpn) { $leakLikely = $true }   # дефолтный v6 не через VPN-интерфейс
}

if ($leakLikely) {
  Write-Status FAIL "Вероятная утечка IPv6: публичный IPv6 доступен и дефолтный маршрут не похож на VPN."
  exit 2
} else {
  Write-Status OK "Явных признаков утечки IPv6 не обнаружено."
  exit 0
}
