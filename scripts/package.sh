#!/bin/zsh
set -euo pipefail

project_dir=${0:A:h:h}
sdk="$project_dir/../.toolchain/dotnet/dotnet"
output_dir="$project_dir/dist"
staging_dir="$output_dir/staging"
version=$(/usr/bin/sed -n 's/.*"version_number": "\([^"]*\)".*/\1/p' "$project_dir/manifest.json")
package_name="maizz-UnifiedAutoRepair-$version.zip"
game_bepinex="$HOME/Library/Application Support/Steam/steamapps/common/Valheim/BepInEx/core"
bepinex_dir="${BEPINEX_DIR:-$game_bepinex}"

# r2modmac can keep BepInEx inside the active profile rather than the game
# folder. Use that copy for compilation only.
if [[ ! -f "$bepinex_dir/BepInEx.dll" ]]; then
  profile_root="$HOME/Library/Application Support/com.r2modmac/profiles"
  profile_bepinex=$(/usr/bin/find "$profile_root" -path '*/BepInEx/core/BepInEx.dll' -print -quit 2>/dev/null || true)
  if [[ -n "$profile_bepinex" ]]; then
    bepinex_dir="${profile_bepinex:h}"
  fi
fi
if [[ ! -f "$bepinex_dir/BepInEx.dll" ]]; then
  echo "BepInEx build reference not found. Apply a BepInEx profile once, or set BEPINEX_DIR." >&2
  exit 1
fi

if [[ ! -x "$sdk" ]]; then
  sdk=$(command -v dotnet)
fi
export DOTNET_ROOT="${sdk:h}"

"$sdk" build "$project_dir/UnifiedAutoRepair.csproj" --configuration Release -p:BepInExDir="$bepinex_dir"

# Thunderstore never accepts the same version twice; refuse to overwrite.
if [[ -e "$output_dir/$package_name" && "${1:-}" != "--force" ]]; then
  echo "Refusing to overwrite existing $package_name. Bump manifest.json, or rerun with --force." >&2
  exit 1
fi

rm -rf "$staging_dir"
mkdir -p "$staging_dir/plugins/UnifiedAutoRepair"
cp "$project_dir/bin/Release/net472/UnifiedAutoRepair.dll" "$staging_dir/plugins/UnifiedAutoRepair/"
cp "$project_dir/manifest.json" "$project_dir/README.md" "$project_dir/CHANGELOG.md" "$project_dir/LICENSE" "$project_dir/icon.png" "$staging_dir/"
rm -f "$output_dir/$package_name"
(cd "$staging_dir" && /usr/bin/zip -qr "$output_dir/$package_name" .)
rm -rf "$staging_dir"
echo "Created $output_dir/$package_name"
