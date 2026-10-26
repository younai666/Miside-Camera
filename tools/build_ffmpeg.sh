#!/usr/bin/env bash
set -euo pipefail
# Ubuntu: gcc-mingw-w64-x86-64, make, pkg-config, curl, xz-utils.
# Minimal LGPL build: the existing raw RGBA -> NVENC -> RTSP path only.
output="${1:?output directory required}"
mkdir -p "$output"
output="$(cd "$output" && pwd)"
work="$(mktemp -d)"
trap 'rm -rf "$work"' EXIT
cd "$work"
curl -fL --retry 2 https://github.com/FFmpeg/FFmpeg/archive/refs/tags/n9.0.2.tar.gz -o ffmpeg.tar.gz
curl -fL --retry 2 https://github.com/FFmpeg/nv-codec-headers/archive/refs/tags/n13.0.19.0.tar.gz -o headers.tar.gz
tar xzf ffmpeg.tar.gz
tar xzf headers.tar.gz
make -C nv-codec-headers-n13.0.19.0 PREFIX="$work/prefix" install
export PKG_CONFIG_PATH="$work/prefix/lib/pkgconfig"
cd FFmpeg-n9.0.2
./configure --target-os=mingw32 --arch=x86_64 --cross-prefix=x86_64-w64-mingw32- \
  --cc=x86_64-w64-mingw32-gcc-win32 --pkg-config=pkg-config --extra-ldflags=-static \
  --disable-autodetect --disable-everything --disable-x86asm --disable-debug --disable-doc \
  --disable-programs --enable-ffmpeg --disable-shared --enable-static --enable-small \
  --enable-ffnvcodec --enable-nvenc --enable-network --enable-avfilter --enable-swscale \
  --enable-encoder=h264_nvenc --enable-decoder=rawvideo --enable-demuxer=rawvideo \
  --enable-muxer=rtsp,rtp --enable-protocol=file,pipe,udp,tcp,rtp \
  --enable-parser=h264 --enable-bsf=h264_mp4toannexb,extract_extradata \
  --enable-filter=scale,pad,format,setparams,buffer,buffersink,null
make -j"$(nproc)"
mkdir -p "$output/licenses" "$output/sources"
cp ffmpeg.exe "$output/ffmpeg.exe"
cp COPYING.LGPLv2.1 LICENSE.md "$output/licenses/"
cp ffbuild/config.mak "$output/sources/build-config.txt"
cp ../nv-codec-headers-n13.0.19.0/README "$output/licenses/nv-codec-headers-README.txt"
cp ../ffmpeg.tar.gz "$output/sources/ffmpeg-n9.0.2.tar.gz"
cp ../headers.tar.gz "$output/sources/nv-codec-headers-n13.0.19.0.tar.gz"
cp /usr/share/doc/mingw-w64-common/copyright "$output/licenses/mingw-w64-copyright.txt"
printf '%s\n' 'FFmpeg n9.0.2, LGPL-2.1-or-later; unmodified upstream sources are included in sources/.' \
  'NVENC headers n13.0.19.0: MIT; license is in the headers source archive.' \
  'No NVIDIA driver DLL, GPL library, nonfree library or game code is included.' \
  'Rebuild using build_ffmpeg.sh on Ubuntu with the MinGW toolchain listed in that script.' > "$output/README.txt"
