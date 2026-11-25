#!/usr/bin/env bash
# Docker smoke test: builds the server image, runs it with in-memory storage and submits
# three answers to puzzle 1.1: a blank image, a half-correct one and a fully correct one.
# Needs docker, the .NET SDK and the hth harness CLI (set HTH if it is not on PATH).
set -euo pipefail

here=$(cd "$(dirname "$0")" && pwd)
server="$here/../ChristmasKataServer"
hth=${HTH:-hth}
port=${PORT:-8080}
url="http://localhost:$port"
work=$(mktemp -d)
failed=0

cleanup() { docker rm -f hth-smoke >/dev/null 2>&1 || true; rm -rf "$work"; }
trap cleanup EXIT

expect() {
  if grep -qF -- "$2" <<<"$3"; then echo "PASS $1"; else echo "FAIL $1: expected '$2' in: $3"; failed=1; fi
}

dotnet build "$here/solver" -nologo -v q -o "$work/solver" >/dev/null
solver="$work/solver/solver"
[ -x "$solver" ] || solver="$solver.exe"

docker build -q -t hth-smoke -f "$server/WebAPI/Dockerfile" "$server" >/dev/null
docker rm -f hth-smoke >/dev/null 2>&1 || true
docker run -d --name hth-smoke -p "$port:8080" -e Storage__Provider=InMemory hth-smoke >/dev/null
for _ in $(seq 1 30); do curl -sf -o /dev/null "$url/team" && break; sleep 1; done

key=$(curl -sf "$url/register/smoke-blank" | tr -d '"')
challenge=$(curl -sf -X POST -H "X-Team-Name: smoke-blank" -H "X-Unique-Key: $key" "$url/puzzles/1.1/challenges" \
  | grep -oE '"challengeId": *"[^"]+"' | sed 's/.*"\([^"]*\)"$/\1/')
blank=$(curl -sf -X POST -H "X-Team-Name: smoke-blank" -H "X-Unique-Key: $key" -H 'Content-Type: image/png' \
  --data-binary @"$server/Tests/Fixtures/render/blank-16.png" "$url/challenges/$challenge/submission" | tr -d ' \n')
expect "blank scores nothing" '"correct":0,"bulbCount":136,"points":0' "$blank"

run() {
  HTH_CONFIG="$work/$1.json" "$hth" login "$1" --server "$url" >/dev/null
  (cd "$work" && HTH_CONFIG="$work/$1.json" "$hth" run 1.1 -- "$solver" "$2" 2>&1)
}
half=$(run smoke-half 68)
expect "half correct fails the 80% mark" "68/136 correct (50%), 0 points" "$half"
full=$(run smoke-full 0)
expect "fully correct scores full marks" "136/136 correct (100%), 100 points" "$full"
expect "fully correct unlocks 1.2" "Unlocked 1.2" "$full"

if docker logs hth-smoke 2>&1 | grep -qiE 'skia|DllNotFound|unhandled exception'; then
  echo "FAIL server log shows a native-load or unhandled error"; docker logs hth-smoke 2>&1 | tail -20; failed=1
else
  echo "PASS no native-load errors in the server log"
fi

exit $failed
