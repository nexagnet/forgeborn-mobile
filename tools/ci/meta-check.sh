#!/usr/bin/env bash
# Kiểm tra .meta của Unity cho các file được Git theo dõi dưới Assets/.
#  - Mỗi file/thư mục (trừ những thứ Unity bỏ qua) phải có file .meta đi kèm.
#  - Mỗi file .meta phải có file/thư mục tương ứng (không mồ côi).
# Dùng: tools/ci/meta-check.sh [thư_mục_gốc_repo]   (mặc định: thư mục hiện tại)
set -euo pipefail
root="${1:-.}"
cd "$root"
git rev-parse --is-inside-work-tree >/dev/null

# Unity bỏ qua: tên bắt đầu bằng '.', kết thúc bằng '~', hoặc 'cvs', '*.tmp'.
ignored() {
  local part
  IFS=/ read -ra parts <<< "$1"
  for part in "${parts[@]}"; do
    case "$part" in .*|*~|cvs|*.tmp) return 0 ;; esac
  done
  return 1
}

declare -A tracked=() needed=()
while IFS= read -r -d '' f; do
  tracked["$f"]=1
  ignored "$f" && continue
  [[ "$f" == *.meta ]] && continue
  # Chính file đó và mọi thư mục cha bên dưới Assets/ đều cần .meta.
  needed["$f.meta"]=1
  d="$f"
  while d="$(dirname "$d")"; [[ "$d" != "Assets" && "$d" != "." ]]; do
    needed["$d.meta"]=1
  done
done < <(git ls-files -z -- Assets)

status=0
for m in "${!needed[@]}"; do
  if [[ -z "${tracked[$m]:-}" ]]; then
    echo "THIẾU .meta: $m"; status=1
  fi
done
for f in "${!tracked[@]}"; do
  [[ "$f" == *.meta ]] || continue
  ignored "$f" && continue
  [[ -n "${needed[$f]:-}" ]] && continue
  # Git không lưu được thư mục rỗng, nên .meta của thư mục rỗng chỉ là cảnh báo.
  if grep -q '^folderAsset: yes' "$f" 2>/dev/null; then
    echo "CẢNH BÁO .meta của thư mục rỗng/không có file theo dõi: $f"
  else
    echo ".meta MỒ CÔI (không có asset): $f"; status=1
  fi
done
if [[ "$status" -eq 0 ]]; then
  echo "meta-check: OK (${#needed[@]} file .meta cần thiết đều có, không có .meta mồ côi)"
else
  echo "meta-check: LỖI"
fi
exit "$status"
