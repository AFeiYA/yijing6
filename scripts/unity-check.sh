#!/bin/bash
set -euo pipefail
project_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
unity_editor="${YIJING_UNITY_EDITOR:-/Applications/Unity/Hub/Editor/6000.6.0f1/Unity.app/Contents/MacOS/Unity}"
test_mode="${1:-editmode}"
mkdir -p "$project_dir/Artifacts"
if [[ ! -x "$unity_editor" ]]; then
  echo "Unity 6000.6.0f1 not found; set YIJING_UNITY_EDITOR to its executable." >&2
  exit 1
fi
case "$test_mode" in
  validate)
    "$unity_editor" -batchmode -nographics -quit -projectPath "$project_dir" \
      -executeMethod Yijing.Editor.ProjectSetup.ValidateConfiguration \
      -logFile "$project_dir/Artifacts/validate.log"
    ;;
  editmode|playmode)
    "$unity_editor" -batchmode -nographics -projectPath "$project_dir" -runTests \
      -testPlatform "$test_mode" -testResults "$project_dir/Artifacts/$test_mode-results.xml" \
      -logFile "$project_dir/Artifacts/$test_mode.log"
    ;;
  *) echo "Usage: $0 [validate|editmode|playmode]" >&2; exit 2 ;;
esac
