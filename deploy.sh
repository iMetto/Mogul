#!/usr/bin/env bash
set -e

PROJECT="Mogul/Mogul.csproj"
DEST="/mnt/c/Users/ahmed/AppData/Roaming/Thunderstore Mod Manager/DataFolder/ScheduleI/profiles/Schedule 1/Mods/imetto-Mogul"

dotnet build "$PROJECT" -c Debug

cp "Mogul/bin/Debug/net6.0/Mogul.dll" "$DEST/Mogul.dll"
cp "docs/property.png"          "$DEST/property.png"
cp "docs/propertyLandscape.png" "$DEST/propertyLandscape.png"
cp "docs/orders.png"            "$DEST/orders.png"
cp "docs/ordersLandscape.png"   "$DEST/ordersLandscape.png"
cp "docs/quests.png"            "$DEST/quests.png"
cp "docs/questsLandscape.png"   "$DEST/questsLandscape.png"
cp "docs/propertyIcon.png" "$DEST/propertyIcon.png"
cp "docs/ordersIcon.png"   "$DEST/ordersIcon.png"
cp "docs/questsIcon.png"   "$DEST/questsIcon.png"

echo "Deployed to $DEST"
