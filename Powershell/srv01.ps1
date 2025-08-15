# === Add 'where' wrapper to profile + sanity checks (user: Admin) ===
$ErrorActionPreference = 'Stop'

function OK   ($m){ Write-Host "[OK ] $m"   -ForegroundColor Green  }
function WARN ($m){ Write-Host "[WARN] $m"  -ForegroundColor Yellow }
function ERR  ($m){ Write-Host "[ERR] $m"   -ForegroundColor Red    }

# 0) Базовые пути
$npmBin = 'C:\Users\Admin\AppData\Roaming\npm'
$profilePath = $PROFILE.CurrentUserAllHosts
$profileDir  = Split-Path $profilePath

# 1) Папка профиля
if (!(Test-Path $profileDir)) {
  New-Item -ItemType Directory -Path $profileDir -Force | Out-Null
  OK "Создан каталог профиля: $profileDir"
} else { OK "Каталог профиля существует: $profileDir" }

# 2) Бэкап профиля
if (Test-Path $profilePath) {
  $bak = "$profilePath.bak.$(Get-Date -Format 'yyyyMMdd-HHmmss')"
  Copy-Item $profilePath $bak -Force
  OK "Сделан бэкап профиля: $bak"
} else { WARN "Файл профиля ещё не создан: $profilePath" }

# 3) Удалим alias 'where' (который указывает на Where-Object), если есть
if (Test-Path Alias:where) {
  Remove-Item Alias:where -Force
  OK "Удалён конфликтующий alias 'where' → Where-Object"
} else { WARN "Alias 'where' не найден — ок" }

# 4) Вставим функцию 'where' и alias 'which' в профиль (идемпотентно)
$snippet = @"
# --- added by setup: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss') ---
function global:where {
    param([Parameter(ValueFromRemainingArguments=\$true)][string[]]\$Rest)
    & "\$env:SystemRoot\System32\where.exe" @Rest
}
Set-Alias -Name which -Value Get-Command -Option AllScope -Force
# --- end ---
"@

$current = if (Test-Path $profilePath) { Get-Content $profilePath -Raw } else { "" }
if ($current -notmatch 'function\s+global:where') {
  Add-Content -Path $profilePath -Value $snippet
  OK "Добавлены функция 'where' и alias 'which' в профиль: $profilePath"
} else {
  WARN "В профиле уже есть функция 'where' — пропускаю вставку"
}

# 5) На всякий случай — добавим npm-bin в PATH этой сессии
if (Test-Path $npmBin) {
  if (-not (($env:Path -split ';') -contains $npmBin)) {
    $env:Path += ";$npmBin"
    OK "Добавлен в PATH текущей сессии: $npmBin"
  } else { OK "В PATH текущей сессии уже есть: $npmBin" }
} else { WARN "Папка npm-bin не найдена: $npmBin (проверь установку CLI)" }

# 6) Перезагрузим профиль
. $PROFILE
OK "Профиль перезагружен: $PROFILE"

# 7) Тесты
$w1 = & "$env:SystemRoot\System32\where.exe" claude 2>$null
if ($LASTEXITCODE -eq 0 -and $w1) { OK "where.exe claude → `n$w1" } else { WARN "where.exe не нашёл 'claude' (PATH?)" }

$w2 = where claude 2>$null
if ($w2) { OK "новая функция 'where' работает → `n$w2" } else { ERR "функция 'where' ничего не вернула" }
