#!/usr/bin/env bash
# Henter de nyeste landeopdelte IP-intervaller (ip-location-db/geo-whois-asn-country, data fra NRO, CC BY 4.0)
# og skriver de danske til src/Tumlegaarden.Web/GeoData/dk-ip-ranges.csv.
# Bruges af GitHub Actions ved hvert byg; kan også køres manuelt. Kræver npm.
set -euo pipefail

root="$(cd "$(dirname "$0")/.." && pwd)"
out="$root/src/Tumlegaarden.Web/GeoData/dk-ip-ranges.csv"
work="$(mktemp -d)"
trap 'rm -rf "$work"' EXIT

cd "$work"
npm pack --silent @ip-location-db/geo-whois-asn-country > /dev/null
tar xzf ./*.tgz
version="$(sed -n 's/.*"version": *"\([^"]*\)".*/\1/p' package/package.json)"

{
  echo "# Danske IP-intervaller (start,slut). Genereret $(date -u +%Y-%m-%d) fra @ip-location-db/geo-whois-asn-country $version."
  echo "# Data: Number Resource Organization (nro.net), CC BY 4.0. Se LICENSE-NRO.txt."
  awk -F, '$3 == "DK" { print $1 "," $2 }' package/geo-whois-asn-country-ipv4.csv package/geo-whois-asn-country-ipv6.csv
} > "$work/dk.csv"

lines="$(grep -vc '^#' "$work/dk.csv")"
if [ "$lines" -lt 1000 ]; then
  echo "For få danske intervaller ($lines) – beholder den eksisterende fil." >&2
  exit 1
fi
mv "$work/dk.csv" "$out"
echo "Skrev $lines danske intervaller til $out"
