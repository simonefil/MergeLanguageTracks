#!/bin/bash
# MergeLanguageTracks release script
# Usage: ./release.sh <tag> <notes>

set -e

if [ $# -lt 2 ]; then
    echo "Usage: ./release.sh <tag> <notes>"
    exit 1
fi

TAG="$1"
NOTES="$2"

PROJECT="MergeLanguageTracks.csproj"
ARTIFACTS_DIR="release-artifacts"
PUBLISH_DIR="publish"

RIDS=("win-x64" "linux-x64" "linux-arm64" "osx-x64" "osx-arm64")

confirm_step() {
    read -r -p "$1 [Y/n] " choice
    if [ "$choice" = "n" ] || [ "$choice" = "N" ]; then
        echo "Aborted."
        exit 0
    fi
}

# Clean previous builds
rm -rf "$PUBLISH_DIR" "$ARTIFACTS_DIR"
mkdir -p "$ARTIFACTS_DIR"

# Build all targets
confirm_step "Build binaries for ${#RIDS[@]} platforms?"
for rid in "${RIDS[@]}"; do
    echo "Building $rid..."

    dotnet publish "$PROJECT" -c Release -r "$rid" --self-contained true \
        -p:PublishSingleFile=true \
        -p:PublishTrimmed=false \
        -p:EnableCompressionInSingleFile=true \
        -o "$PUBLISH_DIR/$rid"

    # Zip the binary
    cd "$PUBLISH_DIR/$rid"
    zip -r "../../$ARTIFACTS_DIR/MergeLanguageTracks-$rid.zip" .
    cd ../..

    echo "$rid done."
done

# Create and push tag
confirm_step "Create git tag $TAG and push?"
echo "Creating tag $TAG..."
git tag "$TAG"
git push origin "$TAG"

# Create GitHub release
confirm_step "Create GitHub release with artifacts?"
echo "Creating GitHub release..."
gh release create "$TAG" "$ARTIFACTS_DIR"/*.zip --title "$TAG" --notes "$NOTES"

# Cleanup
rm -rf "$PUBLISH_DIR" "$ARTIFACTS_DIR"

echo "Release $TAG published successfully."
