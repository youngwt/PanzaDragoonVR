# Where the Godot / .NET / JDK 17 / Android SDK toolchain lives.
#
# Source this file; do not run it:    . tools/toolchain-env.sh
#
# It only sets variables, so it is safe to source before anything is installed.
# tools/setup-toolchain.sh installs the toolchain; --check reports what is missing.
# Plain POSIX sh, so it works from sh, bash, the VS Code sandbox and the host.

TOOLCHAIN_ROOT="${TOOLCHAIN_ROOT:-$HOME/.local/opt}"

GODOT_VERSION="4.7.2"
GODOT_RELEASE="${GODOT_VERSION}-stable"
GODOT_DIR="$TOOLCHAIN_ROOT/godot-${GODOT_RELEASE}-mono"
GODOT="$GODOT_DIR/Godot_v${GODOT_RELEASE}_mono_linux.x86_64"
GODOT_EXPORT_TEMPLATES="$GODOT_DIR/editor_data/export_templates/${GODOT_VERSION}.stable.mono"

DOTNET_ROOT="$TOOLCHAIN_ROOT/dotnet-9"
JAVA_HOME="$TOOLCHAIN_ROOT/jdk-17"
ANDROID_HOME="$TOOLCHAIN_ROOT/android-sdk"

DOTNET_CLI_TELEMETRY_OPTOUT=1
DOTNET_NOLOGO=1

export TOOLCHAIN_ROOT GODOT_VERSION GODOT_RELEASE GODOT_DIR GODOT GODOT_EXPORT_TEMPLATES
export DOTNET_ROOT JAVA_HOME ANDROID_HOME DOTNET_CLI_TELEMETRY_OPTOUT DOTNET_NOLOGO

# Put the tools first on PATH, once, however many times this file is sourced.
for _toolchain_dir in \
    "$ANDROID_HOME/platform-tools" \
    "$ANDROID_HOME/cmdline-tools/latest/bin" \
    "$JAVA_HOME/bin" \
    "$DOTNET_ROOT"
do
    case ":$PATH:" in
        *":$_toolchain_dir:"*) ;;
        *) PATH="$_toolchain_dir:$PATH" ;;
    esac
done
unset _toolchain_dir
export PATH
