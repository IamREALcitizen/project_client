#!/usr/bin/env bash
# storyboard.html의 보드 4장을 PNG로 저장한다.
# 사용: bash render.sh [출력 폴더=Docs/UI] [배율=1.5]
#       bash render.sh <출력 폴더> <배율> <보드#컷> ...   (예: kill/5 → 그 컷 하나만)
set -e
cd "$(dirname "$0")"
OUT="${1:-../../Docs/UI}"
SCALE="${2:-1.5}"
shift 2 2>/dev/null || shift $#
CH="/c/Program Files/Google/Chrome/Application/chrome.exe"
[ -x "$CH" ] || CH="/c/Program Files (x86)/Microsoft/Edge/Application/msedge.exe"
URL="file:///$(cygpath -m "$PWD")/storyboard.html"
mkdir -p "$OUT"
PROFILE="$(mktemp -d)"
trap 'rm -rf "$PROFILE"' EXIT
render() { # 해시 너비 높이 파일명 [쿼리]
  # 크롬이 앞 창을 아직 닫는 중이면 새 실행이 그 창에 붙고 그냥 끝나므로 매번 프로필을 따로 쓴다
  local dir="$PROFILE/$4"
  "$CH" --headless=new --disable-gpu --hide-scrollbars --allow-file-access-from-files \
    --user-data-dir="$(cygpath -w "$dir")" --force-device-scale-factor="$SCALE" --window-size="$2,$3" \
    --screenshot="$(cygpath -w "$OUT/$4")" "$URL${5:-}#$1" >/dev/null 2>&1
  [ -s "$OUT/$4" ] && echo "$OUT/$4" || { echo "실패: $4" >&2; return 1; }
}
if [ $# -gt 0 ]; then
  for cut in "$@"; do render "$cut" 360 640 "cut_${cut//\//_}.png" "?z1"; done
  exit 0
fi
render kill  1992 1100 DayTransition_A_Killed.png
render save  1992 1100 DayTransition_B_Protected.png
render quiet 1604 1100 DayTransition_C_Quiet.png
render exec  1992 1100 DayTransition_D_Executed.png
