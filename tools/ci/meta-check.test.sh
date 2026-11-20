#!/usr/bin/env bash
# Test cho meta-check.sh: dựng repo Git tạm với các tình huống đúng/sai.
set -uo pipefail
here="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
check="$here/meta-check.sh"
fails=0
tmp="$(mktemp -d)"; trap 'rm -rf "$tmp"' EXIT

meta()   { printf 'fileFormatVersion: 2\nguid: %s\n' "$(printf '%032x' "$RANDOM$RANDOM")" > "$1.meta"; }
dmeta()  { printf 'fileFormatVersion: 2\nguid: %s\nfolderAsset: yes\n' "$(printf '%032x' "$RANDOM$RANDOM")" > "$1.meta"; }

# new_repo <tên>: tạo repo hợp lệ gồm Assets/Dir/A.cs với đủ .meta
new_repo() {
  local r="$tmp/$1"; mkdir -p "$r/Assets/Dir"; ( cd "$r" && git init -q )
  echo x > "$r/Assets/Dir/A.cs"; meta "$r/Assets/Dir/A.cs"; dmeta "$r/Assets/Dir"
  echo "$r"
}
# expect <tên> <mong đợi: pass|fail> <repo>
expect() {
  ( cd "$3" && git add -A -f && bash "$check" . >"$tmp/out" 2>&1 ); local rc=$?
  if { [[ $2 == pass && $rc -eq 0 ]] || [[ $2 == fail && $rc -ne 0 ]]; }; then echo "ok   - $1"
  else echo "FAIL - $1 (rc=$rc)"; cat "$tmp/out"; fails=$((fails+1)); fi
}

r="$(new_repo valid)";                                   expect "repo hợp lệ" pass "$r"
r="$(new_repo missing-file-meta)"; rm "$r/Assets/Dir/A.cs.meta"; expect "thiếu .meta của file" fail "$r"
r="$(new_repo missing-dir-meta)";  rm "$r/Assets/Dir.meta";      expect "thiếu .meta của thư mục" fail "$r"
r="$(new_repo nested)"; mkdir -p "$r/Assets/Dir/Sub"; echo y > "$r/Assets/Dir/Sub/B.cs"; meta "$r/Assets/Dir/Sub/B.cs"
                                                             expect "thiếu .meta thư mục lồng" fail "$r"
dmeta "$r/Assets/Dir/Sub";                                    expect "đủ .meta thư mục lồng" pass "$r"
r="$(new_repo orphan-file)"; meta "$r/Assets/Dir/Gone.cs";   expect ".meta mồ côi của file" fail "$r"
r="$(new_repo orphan-folder)"; dmeta "$r/Assets/Empty";      expect ".meta thư mục rỗng chỉ cảnh báo" pass "$r"
r="$(new_repo ignored)"; echo z > "$r/Assets/Dir/.hidden"; echo z > "$r/Assets/Dir/note~"; mkdir -p "$r/Assets/Dir/Docs~"; echo z > "$r/Assets/Dir/Docs~/n.txt"
                                                             expect "file ẩn, ~ được bỏ qua" pass "$r"
r="$(new_repo untracked)"; ( cd "$r" && git add -A -f )
echo q > "$r/Assets/Dir/NotTracked.cs";  # chưa git add: không xét
( cd "$r" && bash "$check" . >/dev/null 2>&1 ) && echo "ok   - file chưa theo dõi không bị xét" || { echo "FAIL - file chưa theo dõi"; fails=$((fails+1)); }

[[ $fails -eq 0 ]] && echo "meta-check.test: TẤT CẢ ĐẠT" || echo "meta-check.test: $fails LỖI"
exit "$fails"
