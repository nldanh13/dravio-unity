#!/usr/bin/env bash
# SIGKILL tiến trình đang ghi save ở thời điểm ngẫu nhiên, rồi kiểm save đọc được và không mất bản đã xác nhận.
set -euo pipefail
ROUNDS="${1:-40}"
HERE="$(cd "$(dirname "$0")" && pwd)"
WORK="$(mktemp -d)"
trap 'rm -rf "$WORK"' EXIT
dotnet build "$HERE/KillTest.csproj" -c Release -o "$WORK/bin" -nologo -v q >/dev/null
BIN="$WORK/bin/KillTest.dll"
SAVES="$WORK/saves"
# Establish a confirmed save before random kills. A cold process may be killed
# before its first write; that is not evidence of losing a confirmed revision.
if ! dotnet "$BIN" seed "$SAVES"; then
  echo "Failed to establish baseline save" >&2
  exit 1
fi
dotnet "$BIN" check "$SAVES" 1
pass=0
confirmed=1
for i in $(seq 1 "$ROUNDS"); do
  dotnet "$BIN" write "$SAVES" > "$WORK/log" 2>&1 &
  pid=$!
  sleep "0.$(( (RANDOM % 9) + 1 ))$(( RANDOM % 10 ))"
  kill -9 "$pid" 2>/dev/null || true
  wait "$pid" 2>/dev/null || true
  last="$(grep -o 'rev [0-9]*' "$WORK/log" | tail -1 | cut -d' ' -f2 || true)"
  if [[ -n "$last" ]] && (( last > confirmed )); then confirmed="$last"; fi
  if dotnet "$BIN" check "$SAVES" "$confirmed"; then pass=$((pass+1)); else echo "ROUND $i FAILED"; fi
done
echo "kill-test: $pass/$ROUNDS rounds passed; files: $(ls "$SAVES" | tr '\n' ' ')"
[ "$pass" -eq "$ROUNDS" ]
