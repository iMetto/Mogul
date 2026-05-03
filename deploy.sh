#!/usr/bin/env bash
set -e

PROJECT="Mogul/Mogul.csproj"
DEST="/mnt/c/Users/ahmed/AppData/Roaming/Thunderstore Mod Manager/DataFolder/ScheduleI/profiles/Schedule 1/Mods/imetto-Mogul"

dotnet build "$PROJECT" -c Debug

cp "Mogul/bin/Debug/net6.0/Mogul.dll" "$DEST/Mogul.dll"

echo "Deployed to $DEST"
