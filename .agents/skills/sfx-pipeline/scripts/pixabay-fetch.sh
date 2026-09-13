#!/usr/bin/env bash
# pixabay-fetch.sh — download the original file behind a Pixabay sound/music page.
#
# Pixabay pages are Cloudflare-protected: a direct curl usually returns a
# challenge page, not the CDN link. This goes through the jina.ai reader proxy
# (HTML mode), retries the challenge, extracts the cdn.pixabay.com download URL
# and downloads it with a browser UA + pixabay referer.
#
# usage: pixabay-fetch.sh <pixabay-page-url> [outdir]
#   outdir defaults to ~/Downloads (where the user keeps raw originals).
#
# prints on stdout: the downloaded file path
# prints on stderr: title / author metadata for the provenance row
set -euo pipefail

url="${1:?usage: pixabay-fetch.sh <pixabay-page-url> [outdir]}"
outdir="${2:-$HOME/Downloads}"
UA="Mozilla/5.0 (X11; Linux x86_64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/126.0 Safari/537.36"

tmp="$(mktemp -d)"
trap 'rm -rf "$tmp"' EXIT
mkdir -p "$outdir"

# 1) Page HTML through the reader proxy; retry if Cloudflare shows its challenge.
page="$tmp/page.html"
for attempt in 1 2 3 4 5; do
    curl -sL -m 90 -H "x-respond-with: html" "https://r.jina.ai/$url" -o "$page" || true
    if [ -s "$page" ] && ! grep -q 'Just a moment' "$page"; then
        break
    fi
    echo "cloudflare challenge (attempt $attempt), retrying..." >&2
    sleep 5
done

# 2) Extract the CDN download link (first .mp3 on the page).
cdn="$(grep -oE 'https://cdn\.pixabay\.com/download/[^"\\ <>()]*\.mp3[^"\\ <>()]*' "$page" | sort -u | head -1 || true)"
if [ -z "$cdn" ]; then
    echo "no cdn.pixabay.com mp3 link found in $url" >&2
    echo "(page kept for debugging: $page — re-run without the trap if needed)" >&2
    exit 1
fi

# 3) Metadata for the docs/ASSET_SOURCES.md row.
title="$(grep -oE '<title>[^<]*</title>' "$page" | head -1 | sed -e 's/<[^>]*>//g' || true)"
name="$(printf '%s' "$cdn" | sed -n 's/.*[?&]filename=\([^&]*\).*/\1/p')"
[ -n "$name" ] || name="pixabay-$(basename "${url%/}")"
echo "title:  ${title:-unknown}" >&2
echo "author: ${name%%-*}  (from the CDN filename slug — verify on the page)" >&2
echo "source: $url" >&2

# 4) Download the original.
out="$outdir/$name"
echo "downloading -> $out" >&2
curl -sL -m 300 -A "$UA" -e "https://pixabay.com/" "$cdn" -o "$out"

if [ ! -s "$out" ]; then
    echo "download produced an empty file" >&2
    exit 1
fi

file "$out" >&2 || true
ffprobe -v error -show_entries format=duration,bit_rate -of default=noprint_wrappers=1 "$out" >&2
printf '%s\n' "$out"
