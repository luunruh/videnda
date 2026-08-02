#!/usr/bin/env bash
# Baut Videnda und installiert es ins Benutzerverzeichnis (~/.local).
set -euo pipefail

cd "$(dirname "$0")"

PREFIX="${PREFIX:-$HOME/.local}"
RID="${RID:-linux-x64}"

APP_DIR="$PREFIX/share/videnda"
ICON_DIR="$PREFIX/share/icons/hicolor/256x256/apps"
DESKTOP_DIR="$PREFIX/share/applications"

# Eine laufende Instanz lässt sich nicht überschreiben ("Text file busy")
if pgrep -f "$APP_DIR/Videnda" >/dev/null; then
    echo "Videnda läuft gerade — bitte erst schließen." >&2
    exit 1
fi

mkdir -p "$APP_DIR" "$ICON_DIR" "$DESKTOP_DIR"

# Direkt ins Zielverzeichnis publishen, damit keine zweite 91-MB-Kopie entsteht.
# Die .csproj statt der .slnx, sonst warnt das SDK bei -o (NETSDK1194).
#
# IncludeNativeLibrariesForSelfExtract packt libSkiaSharp/libHarfBuzzSharp/
# libe_sqlite3 mit in die Binary. Ohne das liegen sie lose daneben und das SDK
# räumt sie beim nächsten Publish als "stale" aus dem Zielordner — die App
# startet dann nicht mehr ("Unable to load shared library 'libSkiaSharp'").
dotnet publish Videnda.csproj \
    -c Release \
    -r "$RID" \
    --self-contained \
    -p:PublishSingleFile=true \
    -p:IncludeNativeLibrariesForSelfExtract=true \
    -o "$APP_DIR"

install -m 644 Assets/icon.png "$ICON_DIR/videnda.png"

cat > "$DESKTOP_DIR/videnda.desktop" <<EOF
[Desktop Entry]
Type=Application
Name=Videnda
GenericName=Watchlist
Comment=Your watchlist, archived
Exec=$APP_DIR/Videnda
Icon=videnda
Categories=AudioVideo;Video;
Terminal=false
EOF

# Menü-Cache aktualisieren, falls das Tool vorhanden ist
if command -v update-desktop-database >/dev/null; then
    update-desktop-database -q "$DESKTOP_DIR"
fi

echo "Installiert nach $APP_DIR"
