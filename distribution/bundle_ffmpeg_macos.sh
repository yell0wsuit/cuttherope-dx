#!/bin/bash

# Copies FFmpeg dylibs from Homebrew into a Frameworks directory and rewrites
# install names so the bundle is fully self-contained.
# Usage: ./bundle_ffmpeg_macos.sh <frameworks_dir>
#
# Requires Homebrew FFmpeg: brew install ffmpeg@9

set -e

FRAMEWORKS_DIR="$1"
if [ -z "$FRAMEWORKS_DIR" ]; then
    echo "Usage: bundle_ffmpeg_macos.sh <frameworks_dir>"
    exit 1
fi

# The FFmpeg major this build ships. It has to match the one FFmpeg.AutoGen binds
# against, because the binding resolves every function from a library named for its
# own soname. A mismatch resolves nothing, and it surfaces at the first decode call
# rather than at load, where the player catches it and quietly falls back to skipping
# every cutscene -- so this script refuses to bundle the wrong one.
FFMPEG_MAJOR=9

# ffmpeg@9 is an alias of the current stable today and becomes a keg of its own once
# FFmpeg 10 lands, so the versioned path is preferred and the plain one still works.
FFMPEG_LIB=""
for candidate in \
    "/opt/homebrew/opt/ffmpeg@$FFMPEG_MAJOR/lib" "/usr/local/opt/ffmpeg@$FFMPEG_MAJOR/lib" \
    /opt/homebrew/opt/ffmpeg/lib /usr/local/opt/ffmpeg/lib; do
    if [ -d "$candidate" ]; then
        FFMPEG_LIB="$candidate"
        break
    fi
done

if [ -z "$FFMPEG_LIB" ]; then
    echo "Error: Homebrew FFmpeg not found. Install with: brew install ffmpeg@$FFMPEG_MAJOR" >&2
    exit 1
fi

echo "Using FFmpeg from $FFMPEG_LIB"
mkdir -p "$FRAMEWORKS_DIR"

# The exact sonames FFmpeg.AutoGen 9.0.1.1 resolves against, which is why they are
# named here rather than discovered: taking whichever version happened to be installed
# is what let a mismatched FFmpeg into a bundle unnoticed.
CORE_LIBS="libavcodec.63 libavformat.63 libavutil.61 libswresample.7 libswscale.10"

# --- Pass 1: Copy core FFmpeg dylibs ---
# cp -L resolves each versioned symlink to the real file, keeping the soname that
# otool references (e.g. libavcodec.63.dylib).
echo "Copying core FFmpeg dylibs..."
for lib in $CORE_LIBS; do
    if [ ! -f "$FFMPEG_LIB/$lib.dylib" ]; then
        echo "Error: $FFMPEG_LIB has no $lib.dylib" >&2
        echo "       found instead: $(ls "$FFMPEG_LIB" | grep -E '^lib(avcodec|avformat|avutil|swresample|swscale)\.[0-9]+\.dylib$' | tr '\n' ' ')" >&2
        echo "       FFmpeg $FFMPEG_MAJOR is required; install it with: brew install ffmpeg@$FFMPEG_MAJOR" >&2
        exit 1
    fi

    echo "  $lib.dylib"
    cp -L "$FFMPEG_LIB/$lib.dylib" "$FRAMEWORKS_DIR/$lib.dylib"
    chmod 755 "$FRAMEWORKS_DIR/$lib.dylib"
done

# --- Pass 2: Discover and copy third-party Homebrew dependencies (transitive) ---
# Scan for references to /opt/homebrew/* and pull them into Frameworks. Copied
# deps can themselves link further Homebrew libs (e.g. libmp3lame -> libmpg123),
# so repeat full scans until a pass copies nothing new — otherwise a missing
# transitive dep makes libavformat fail to load and crashes the app at runtime.
echo "Copying third-party dependencies..."

while :; do
    added=0
    for dylib in "$FRAMEWORKS_DIR"/*.dylib; do
        while IFS= read -r dep; do
            dep_name=$(basename "$dep")
            # Skip anything already present in Frameworks.
            [ -e "$FRAMEWORKS_DIR/$dep_name" ] && continue
            real=$(python3 -c "import pathlib; print(pathlib.Path('$dep').resolve())" 2>/dev/null)
            if [ -f "$real" ]; then
                echo "  $dep_name"
                cp -L "$real" "$FRAMEWORKS_DIR/$dep_name"
                chmod 755 "$FRAMEWORKS_DIR/$dep_name"
                added=1
            fi
        done < <(otool -L "$dylib" | tail -n +2 | awk '{print $1}' | grep '^/opt/homebrew/')
    done
    [ "$added" -eq 0 ] && break
done

# --- Pass 3: Rewrite install names for relocatability ---
echo "Rewriting install names..."
for dylib in "$FRAMEWORKS_DIR"/*.dylib; do
    name=$(basename "$dylib")
    install_name_tool -id "@loader_path/$name" "$dylib" 2>/dev/null

    otool -L "$dylib" | tail -n +2 | awk '{print $1}' | grep '^/opt/homebrew/' | while IFS= read -r dep; do
        dep_name=$(basename "$dep")
        install_name_tool -change "$dep" "@loader_path/$dep_name" "$dylib" 2>/dev/null
    done
done

# Copy FFmpeg license for LGPL compliance
FFMPEG_CELLAR=$(python3 -c "import pathlib; print(pathlib.Path('$FFMPEG_LIB/..').resolve())")
if [ -f "$FFMPEG_CELLAR/LICENSE" ]; then
    cp "$FFMPEG_CELLAR/LICENSE" "$FRAMEWORKS_DIR/FFmpeg-LICENSE.txt"
elif [ -f "$FFMPEG_CELLAR/COPYING.LGPLv2.1" ]; then
    cp "$FFMPEG_CELLAR/COPYING.LGPLv2.1" "$FRAMEWORKS_DIR/FFmpeg-LICENSE.txt"
fi

TOTAL=$(ls "$FRAMEWORKS_DIR"/*.dylib 2>/dev/null | wc -l | tr -d ' ')
echo "Done. Bundled $TOTAL dylibs into $FRAMEWORKS_DIR"
