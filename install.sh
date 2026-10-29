#!/bin/sh
# Installs or updates lazytsql from its GitHub releases on Linux and macOS:
#
#   curl -fsSL https://raw.githubusercontent.com/carandev/lazytsql/main/install.sh | sh
#
# Options, as environment variables:
#   LAZYTSQL_INSTALL_DIR   where to put the binary (default: ~/.local/bin)
#   LAZYTSQL_VERSION       a version such as 0.1.3 (default: the latest release)
#   LAZYTSQL_DOWNLOAD_URL  a mirror of the release downloads (default: GitHub)
set -eu

repo="carandev/lazytsql"
install_dir="${LAZYTSQL_INSTALL_DIR:-$HOME/.local/bin}"
version="${LAZYTSQL_VERSION:-latest}"

fail() {
  echo "lazytsql install: $*" >&2
  exit 1
}

case "$(uname -s)" in
  Linux) os=linux ;;
  Darwin) os=osx ;;
  *) fail "unsupported system $(uname -s). On Windows, use install.ps1." ;;
esac

case "$(uname -m)" in
  x86_64 | amd64) arch=x64 ;;
  aarch64 | arm64) arch=arm64 ;;
  *) fail "unsupported processor $(uname -m). Try 'dotnet tool install -g lazytsql'." ;;
esac

if [ "$os-$arch" = "osx-x64" ]; then
  fail "there is no build for Intel Macs. Try 'dotnet tool install -g lazytsql'."
fi

asset="lazytsql-$os-$arch.tar.gz"
if [ -n "${LAZYTSQL_DOWNLOAD_URL:-}" ]; then
  base="$LAZYTSQL_DOWNLOAD_URL"
elif [ "$version" = "latest" ]; then
  base="https://github.com/$repo/releases/latest/download"
else
  base="https://github.com/$repo/releases/download/v${version#v}"
fi

download() {
  if command -v curl >/dev/null 2>&1; then
    curl -fsSL "$1" -o "$2"
  elif command -v wget >/dev/null 2>&1; then
    wget -q "$1" -O "$2"
  else
    fail "curl or wget is needed to download lazytsql."
  fi
}

sha256() {
  if command -v sha256sum >/dev/null 2>&1; then
    sha256sum "$1" | cut -d ' ' -f 1
  else
    shasum -a 256 "$1" | cut -d ' ' -f 1
  fi
}

tmp="$(mktemp -d)"
trap 'rm -rf "$tmp"' EXIT INT TERM

echo "Downloading $asset ($version)..."
download "$base/$asset" "$tmp/$asset" || fail "could not download $base/$asset"

# Releases from 0.1.3 on publish SHA256SUMS; an older release installs without the check.
if download "$base/SHA256SUMS" "$tmp/SHA256SUMS" 2>/dev/null; then
  expected="$(grep " $asset\$" "$tmp/SHA256SUMS" | cut -d ' ' -f 1)"
  [ -n "$expected" ] || fail "$asset is not listed in SHA256SUMS."
  actual="$(sha256 "$tmp/$asset")"
  [ "$expected" = "$actual" ] || fail "checksum mismatch for $asset: expected $expected, got $actual."
  echo "Checksum verified."
else
  echo "This release has no SHA256SUMS; skipping the checksum check."
fi

tar -xzf "$tmp/$asset" -C "$tmp"
mkdir -p "$install_dir"
# Replace through a rename, so a running lazytsql keeps working until it exits.
cp "$tmp/lazytsql" "$install_dir/.lazytsql.new"
chmod 755 "$install_dir/.lazytsql.new"
mv -f "$install_dir/.lazytsql.new" "$install_dir/lazytsql"

echo "Installed lazytsql $("$install_dir/lazytsql" --version) to $install_dir/lazytsql"

case ":$PATH:" in
  *":$install_dir:"*) ;;
  *)
    echo
    echo "$install_dir is not on your PATH. Add it, then open a new terminal:"
    echo "  echo 'export PATH=\"$install_dir:\$PATH\"' >> ~/.bashrc   # or ~/.zshrc"
    ;;
esac
