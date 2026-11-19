<#
.SYNOPSIS
  Cho ban .exe FORGEBORN tu luu khung hinh khi chay that (co Update), roi dung FFmpeg ghep video va tam anh tong quan.

.DESCRIPTION
  Anh chup batchmode (ForgeBornCapture) khong chay Update, nen khong thay duoc chuyen dong, hieu ung, mieng lo doi sang.
  Script nay chay .exe voi -fb-frames (xem PlayerAutomation.cs): 3 giay dung yen, roi mai trong -DurationSeconds giay.
  Game tu doc khung hinh tu render target cua chinh no, khong quay man hinh desktop, nen khong anh huong den cong viec khac.
  Dau ra: build/out/video/<thoi gian>/frames/f_NNNN.png, frames.csv, gameplay.mp4, contact_sheet.png (khong vao git).

.EXAMPLE
  powershell -File tools\record-gameplay.ps1
  powershell -File tools\record-gameplay.ps1 -DurationSeconds 12 -Fps 8 -Size 1920x1080

.NOTES
  Can ban .exe co co -fb-frames (build lai sau khi them che do nay: ForgeBorn > Build Windows).
#>
[CmdletBinding()]
param(
    [string]$Exe = '',
    [int]$DurationSeconds = 8,   # so giay mai sau 3 giay dung yen
    [double]$Fps = 4,            # so khung hinh luu moi giay
    [string]$Size = '1280x720',
    [string]$OutRoot = ''
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
if (-not $Exe) { $Exe = Join-Path $root '..\build\out\win\ForgeBorn.exe' }
if (-not $OutRoot) { $OutRoot = Join-Path $root '..\build\out\video' }
$Exe = (Resolve-Path $Exe).Path
if (-not (Get-Command ffmpeg -ErrorAction SilentlyContinue)) { throw 'Khong tim thay ffmpeg trong PATH.' }
if ($Size -notmatch '^(\d+)x(\d+)$') { throw "Size phai dang 1280x720, nhan duoc '$Size'." }
$w = [int]$Matches[1]; $h = [int]$Matches[2]

$out = Join-Path $OutRoot (Get-Date -Format 'yyyyMMdd-HHmmss')
$frames = Join-Path $out 'frames'
New-Item -ItemType Directory -Force -Path $frames | Out-Null
$out = (Resolve-Path $out).Path
$frames = (Resolve-Path $frames).Path

# 1) Chay game, doi thoat (toi da 3 + Duration + 120 giay vi luu PNG cham hon khung hinh)
$gameArgs = @('-screen-fullscreen', '0', '-screen-width', $w, '-screen-height', $h,
              '-fb-frames', $frames, '-fb-duration', $DurationSeconds, '-fb-fps', $Fps)
$game = Start-Process -FilePath $Exe -ArgumentList $gameArgs -PassThru
$limitMs = (3 + $DurationSeconds + 120) * 1000
if (-not $game.WaitForExit($limitMs)) { Stop-Process -Id $game.Id -Force; throw 'Game khong thoat dung han, da dung.' }
if ($game.ExitCode -ne 0) { throw "Game thoat voi ma $($game.ExitCode)." }

$count = (Get-ChildItem $frames -Filter f_*.png).Count
if ($count -eq 0) { throw 'Khong co khung hinh nao. Ban .exe co the chua co che do -fb-frames (build lai).' }

# 2) Ghep video va tam anh tong quan (FFmpeg ghi canh bao ra stderr nen khong de Stop bat loi)
$ErrorActionPreference = 'Continue'
$video = Join-Path $out 'gameplay.mp4'
& ffmpeg -hide_banner -loglevel error -y -framerate $Fps -i (Join-Path $frames 'f_%04d.png') `
    -c:v libx264 -preset veryfast -crf 18 -pix_fmt yuv420p $video

$sheet = Join-Path $out 'contact_sheet.png'
$cols = 4
$tiles = [math]::Min($count, 16)
$rows = [math]::Max(1, [math]::Ceiling($tiles / $cols))
$step = [math]::Max(1, [math]::Floor($count / $tiles))
& ffmpeg -hide_banner -loglevel error -y -framerate $Fps -i (Join-Path $frames 'f_%04d.png') `
    -vf "select='not(mod(n\,$step))',scale=480:-1,tile=${cols}x${rows}" -frames:v 1 $sheet

Write-Host "Khung hinh:    $frames  ($count anh, $Fps anh/giay)"
Write-Host "Video:         $video"
Write-Host "Tam tong quan: $sheet"
