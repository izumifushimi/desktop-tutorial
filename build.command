#!/bin/zsh
set -euo pipefail
TASK_DIR="${0:A:h}"
cd "$TASK_DIR"
mkdir -p work
xcrun swiftc WhalePet.swift -o work/WhalePet -framework Cocoa
work/WhalePet --self-test
work/WhalePet --test-preview
if /usr/bin/pgrep -f "^$TASK_DIR/WhalePet.app/Contents/MacOS/WhalePet$" >/dev/null; then
  print '请先退出此目录中的桌宠，再重新构建。'
  exit 1
fi
mkdir -p WhalePet.app/Contents/MacOS WhalePet.app/Contents/Resources
cp Info.plist WhalePet.app/Contents/Info.plist
cp assets/states.png WhalePet.app/Contents/Resources/states.png
cp work/WhalePet WhalePet.app/Contents/MacOS/WhalePet
codesign --force --deep --sign - WhalePet.app
print '构建完成：WhalePet.app'
