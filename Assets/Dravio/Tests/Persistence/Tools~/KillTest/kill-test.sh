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
pass=0
for i in $(seq 1 "$ROUNDS"); do
  dotnet "$BIN" write "$SAVES" > "$WORK/log" 2>&1 &
  pid=$!
  sleep "0.$(( (RANDOM % 9) + 1 ))$(( RANDOM % 10 ))"
  kill -9 "$pid" 2>/dev/null || true
  wait "$pid" 2>/dev/null || true
  last="$(grep -o 'rev [0-9]*' "$WORK/log" | tail -1 | cut -d' ' -f2 || true)"
  if dotnet "$BIN" check "$SAVES" "${last:-0}"; then pass=$((pass+1)); else echo "ROUND $i FAILED"; fi
done
echo "kill-test: $pass/$ROUNDS rounds passed; files: $(ls "$SAVES" | tr '\n' ' ')"
[ "$pass" -eq "$ROUNDS" ]
