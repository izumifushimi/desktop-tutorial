#!/bin/zsh
set -euo pipefail
TASK_DIR="${0:A:h}"
if [[ ! -x "$TASK_DIR/WhalePet.app/Contents/MacOS/WhalePet" ]]; then
  print '请先运行 build.command。'
  exit 1
fi
mkdir -p "$HOME/Library/LaunchAgents" "$TASK_DIR/Logs"
STARTUP_PLIST="$HOME/Library/LaunchAgents/local.whale.pet.plist"
NEW_PLIST="$TASK_DIR/work/startup.plist"
mkdir -p "$TASK_DIR/work"
plutil -create xml1 "$NEW_PLIST"
plutil -insert Label -string local.whale.pet "$NEW_PLIST"
plutil -insert ProgramArguments -json '[]' "$NEW_PLIST"
plutil -insert ProgramArguments.0 -string "$TASK_DIR/WhalePet.app/Contents/MacOS/WhalePet" "$NEW_PLIST"
plutil -insert RunAtLoad -bool YES "$NEW_PLIST"
plutil -insert ProcessType -string Interactive "$NEW_PLIST"
plutil -insert StandardOutPath -string "$TASK_DIR/Logs/startup.log" "$NEW_PLIST"
plutil -insert StandardErrorPath -string "$TASK_DIR/Logs/errors.log" "$NEW_PLIST"
if launchctl print "gui/$(id -u)/local.whale.pet" >/dev/null 2>&1; then
  launchctl bootout "gui/$(id -u)/local.whale.pet"
fi
cp "$NEW_PLIST" "$STARTUP_PLIST"
launchctl enable "gui/$(id -u)/local.whale.pet"
launchctl bootstrap "gui/$(id -u)" "$STARTUP_PLIST"
print '登录自启动已启用。'
