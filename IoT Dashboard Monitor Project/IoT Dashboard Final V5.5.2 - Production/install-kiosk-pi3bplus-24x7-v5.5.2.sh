#!/usr/bin/env bash
set -Eeuo pipefail

# ==============================================================================
# Raspberry Pi 3 B+ 24x7 Chromium Kiosk Installer V5.5.2
#
# Optimized for Raspberry Pi 3 Model B+ (1 GB RAM). Also adapts to Pi 5 for testing:
#   - systemd directly owns tty1, X11, Openbox and Chromium (no login-shell dependency)
#   - Chromium is restarted inside X without tearing down the display
#   - systemd restarts the complete graphical session if X itself fails
#   - root preflight removes stale X11/Chromium locks before every session
#   - browser health checks X, the visible window, renderer responsiveness and data freshness via CDP
#   - recovery escalates: browser -> low-memory recycle -> X -> guarded reboot
#   - HTTP/application outages are separated from real network transport failures
#   - NetworkManager recovery and guarded reboot occur only for confirmed transport failure
#   - Pi 3 low-memory Chromium tuning, ZRAM and memory-pressure recovery
#   - one SD image auto-selects Pi 3 FKMS or Pi 5 full KMS at boot
#   - successful installation reboots automatically so boot graphics take effect
#   - hardware and software watchdogs are enabled
#   - browser disk cache is stored in RAM to reduce SD-card writes
#   - a daily browser-only recycle prevents long-lived renderer memory growth
#   - no routine full-system reboot is used
#   - Ctrl+Alt+W opens Wi-Fi setup
#   - last-used login credentials can be stored and auto-filled
#
# Usage:
#   chmod +x install-kiosk-pi3bplus-24x7-v5.5.2.sh
#   sudo ./install-kiosk-pi3bplus-24x7-v5.5.2.sh iotdashboard
#
# Optional second argument:
#   sudo ./install-kiosk-pi3bplus-24x7-v5.5.2.sh iotdashboard https://web.itank.io/login/
#
# Environment overrides are also supported, for example:
#   sudo -E KIOSK_DISABLE_GPU=1 ./install-kiosk-pi3bplus-24x7-v5.5.2.sh iotdashboard
# ==============================================================================

PROGRAM_NAME="$(basename "$0")"
CONFIG_FILE="/etc/default/kiosk"
BACKUP_ROOT="/var/backups/kiosk-24x7"
INSTALL_STAMP="$(date +%Y%m%d-%H%M%S)"
BACKUP_DIR="$BACKUP_ROOT/$INSTALL_STAMP"
BOOT_PATH_CHANGED=0

log() {
  printf '[%s] %s\n' "$(date -Is)" "$*"
}

warn() {
  printf '[%s] WARNING: %s\n' "$(date -Is)" "$*" >&2
}

fatal() {
  printf '[%s] ERROR: %s\n' "$(date -Is)" "$*" >&2
  exit 1
}

on_error() {
  local exit_code=$?
  local line_number="${BASH_LINENO[0]:-unknown}"

  trap - ERR
  printf '[%s] ERROR: Installer failed at line %s with exit code %s.\n' \
    "$(date -Is)" "$line_number" "$exit_code" >&2

  if [[ "${BOOT_PATH_CHANGED:-0}" == "1" ]]; then
    printf '[%s] Restoring tty1 console access because kiosk activation failed.\n' "$(date -Is)" >&2
    systemctl disable --now kiosk.service >/dev/null 2>&1 || true
    systemctl unmask getty@tty1.service >/dev/null 2>&1 || true
    systemctl enable --now getty@tty1.service >/dev/null 2>&1 || true
  fi

  printf 'Check the output above. Backups, when created, are in: %s\n' "$BACKUP_DIR" >&2
  exit "$exit_code"
}
trap on_error ERR

if [[ ${EUID} -ne 0 ]]; then
  command -v sudo >/dev/null 2>&1 || fatal "sudo is required."
  exec sudo -E bash "$0" "$@"
fi

[[ -d /run/systemd/system ]] || fatal "This installer requires systemd."
command -v apt-get >/dev/null 2>&1 || fatal "This installer requires Raspberry Pi OS/Debian with apt-get."

# Load an existing configuration so rerunning this installer is idempotent and
# retains deliberate overrides.
if [[ -f "$CONFIG_FILE" ]]; then
  # shellcheck disable=SC1090
  source "$CONFIG_FILE"
fi
PREVIOUS_KIOSK_CONFIG_VERSION="${KIOSK_CONFIG_VERSION:-0}"

find_default_user() {
  local candidate=""

  if [[ -n "${SUDO_USER:-}" && "${SUDO_USER}" != "root" ]]; then
    printf '%s' "$SUDO_USER"
    return 0
  fi

  candidate="$(getent passwd 1000 | cut -d: -f1 || true)"
  if [[ -n "$candidate" && "$candidate" != "root" ]]; then
    printf '%s' "$candidate"
    return 0
  fi

  candidate="$(awk -F: '$3 >= 1000 && $3 < 60000 && $1 != "nobody" { print $1; exit }' /etc/passwd)"
  printf '%s' "$candidate"
}

DEFAULT_USER="$(find_default_user)"
TARGET_USER="${1:-${KIOSK_USER:-$DEFAULT_USER}}"
KIOSK_URL="${2:-${KIOSK_URL:-https://web.itank.io/login/}}"

[[ -n "$TARGET_USER" ]] || fatal "No kiosk user was detected. Run: sudo ./$PROGRAM_NAME iotdashboard"
[[ "$TARGET_USER" != "root" ]] || fatal "Chromium must run as a normal non-root user."
[[ "$KIOSK_URL" =~ ^https?:// ]] || fatal "KIOSK_URL must begin with http:// or https://"

USER_ENTRY="$(getent passwd "$TARGET_USER" || true)"
[[ -n "$USER_ENTRY" ]] || fatal "User '$TARGET_USER' does not exist."

TARGET_UID="$(printf '%s' "$USER_ENTRY" | cut -d: -f3)"
TARGET_GID="$(printf '%s' "$USER_ENTRY" | cut -d: -f4)"
TARGET_HOME="$(printf '%s' "$USER_ENTRY" | cut -d: -f6)"
TARGET_GROUP="$(id -gn "$TARGET_USER")"

[[ -d "$TARGET_HOME" ]] || fatal "Home directory '$TARGET_HOME' does not exist."

# Detect the real board. Production target is Raspberry Pi 3 B+; Pi 5 remains
# supported so the same image can be tested before field deployment.
PI_MODEL="$(tr -d '\0' </proc/device-tree/model 2>/dev/null || printf 'Unknown Raspberry Pi')"
TOTAL_RAM_MB="$(( $(awk '/^MemTotal:/ {print $2}' /proc/meminfo) / 1024 ))"
KIOSK_HARDWARE_PROFILE="generic"
KIOSK_NEEDS_VC4_PRIMARY="0"

case "$PI_MODEL" in
  *"Raspberry Pi 3 Model B Plus"*|*"Raspberry Pi 3 Model B+"*)
    KIOSK_HARDWARE_PROFILE="pi3bplus-1gb"
    ;;
  *"Raspberry Pi 5"*)
    KIOSK_HARDWARE_PROFILE="pi5-test"
    KIOSK_NEEDS_VC4_PRIMARY="1"
    ;;
  *)
    # Some kernels expose separate V3D and VC4 DRM cards even when the model
    # string is unavailable. In that case use the Pi 5-safe Xorg selector.
    if find /sys/class/drm/card*/device/driver -maxdepth 0 -type l -printf '%l\n' \
         2>/dev/null | grep -q '/v3d$' && \
       find /sys/class/drm/card*/device/driver -maxdepth 0 -type l -printf '%l\n' \
         2>/dev/null | grep -q '/vc4$'; then
      KIOSK_HARDWARE_PROFILE="multi-drm"
      KIOSK_NEEDS_VC4_PRIMARY="1"
    fi
    ;;
esac

# ------------------------------- Tunables ------------------------------------
# Keep scheduled refresh disabled unless the dashboard itself needs it.
KIOSK_REFRESH_SECS="${KIOSK_REFRESH_SECS:-0}"
KIOSK_DISABLE_GPU="${KIOSK_DISABLE_GPU:-0}"
KIOSK_DEVTOOLS_PORT="${KIOSK_DEVTOOLS_PORT:-9222}"
KIOSK_BROWSER_RESTART_MIN_SECS="${KIOSK_BROWSER_RESTART_MIN_SECS:-5}"
KIOSK_BROWSER_RESTART_MAX_SECS="${KIOSK_BROWSER_RESTART_MAX_SECS:-60}"
KIOSK_BROWSER_STABLE_SECS="${KIOSK_BROWSER_STABLE_SECS:-300}"
KIOSK_BROWSER_CRASH_WINDOW_SECS="${KIOSK_BROWSER_CRASH_WINDOW_SECS:-600}"
KIOSK_BROWSER_CRASH_RESET_THRESHOLD="${KIOSK_BROWSER_CRASH_RESET_THRESHOLD:-6}"
KIOSK_BROWSER_RECYCLE_HOURS="${KIOSK_BROWSER_RECYCLE_HOURS:-24}"
KIOSK_X_RESTART_MIN_SECS="${KIOSK_X_RESTART_MIN_SECS:-10}"
KIOSK_X_RESTART_MAX_SECS="${KIOSK_X_RESTART_MAX_SECS:-120}"
KIOSK_X_STABLE_SECS="${KIOSK_X_STABLE_SECS:-600}"
KIOSK_HEALTH_INTERVAL_SECS="${KIOSK_HEALTH_INTERVAL_SECS:-30}"
KIOSK_HEALTH_FAIL_THRESHOLD="${KIOSK_HEALTH_FAIL_THRESHOLD:-3}"
KIOSK_HEALTH_RESTART_WAIT_SECS="${KIOSK_HEALTH_RESTART_WAIT_SECS:-30}"
KIOSK_RENDERER_PROBE_TIMEOUT_SECS="${KIOSK_RENDERER_PROBE_TIMEOUT_SECS:-8}"
KIOSK_HTTP_TIMEOUT_SECS="${KIOSK_HTTP_TIMEOUT_SECS:-10}"

# Dashboard freshness monitoring. "auto" observes meaningful DOM text/structure
# changes and completed resource requests. For a site-specific heartbeat, use
# "selector" or "expression" through environment overrides.
KIOSK_DATA_FRESHNESS_MODE="${KIOSK_DATA_FRESHNESS_MODE:-auto}"   # off|auto|selector|expression
KIOSK_DATA_FRESHNESS_MAX_AGE_SECS="${KIOSK_DATA_FRESHNESS_MAX_AGE_SECS:-900}"
KIOSK_DATA_FRESHNESS_SELECTOR="${KIOSK_DATA_FRESHNESS_SELECTOR:-}"
KIOSK_DATA_FRESHNESS_ATTRIBUTE="${KIOSK_DATA_FRESHNESS_ATTRIBUTE:-data-epoch}"
KIOSK_DATA_FRESHNESS_EXPRESSION="${KIOSK_DATA_FRESHNESS_EXPRESSION:-}"
KIOSK_FRESHNESS_EXEMPT_PATH_REGEX="${KIOSK_FRESHNESS_EXEMPT_PATH_REGEX:-^/login(?:/|$)}"

KIOSK_EMERGENCY_REBOOT_AFTER_SECS="${KIOSK_EMERGENCY_REBOOT_AFTER_SECS:-600}"
KIOSK_EMERGENCY_REBOOT_COOLDOWN_SECS="${KIOSK_EMERGENCY_REBOOT_COOLDOWN_SECS:-21600}"

# Raspberry Pi 3 B+ / 1 GB defaults. These keep Chromium useful without using
# unsafe single-process or no-sandbox modes.
KIOSK_RENDERER_PROCESS_LIMIT="${KIOSK_RENDERER_PROCESS_LIMIT:-2}"
KIOSK_DISK_CACHE_BYTES="${KIOSK_DISK_CACHE_BYTES:-16777216}"       # 16 MiB in RAM
KIOSK_MEDIA_CACHE_BYTES="${KIOSK_MEDIA_CACHE_BYTES:-8388608}"      # 8 MiB in RAM
KIOSK_LOW_MEMORY_MB="${KIOSK_LOW_MEMORY_MB:-96}"
KIOSK_LOW_SWAP_MB="${KIOSK_LOW_SWAP_MB:-64}"
KIOSK_MEMORY_FAIL_THRESHOLD="${KIOSK_MEMORY_FAIL_THRESHOLD:-3}"
KIOSK_OOM_SCORE_ADJ="${KIOSK_OOM_SCORE_ADJ:-300}"
KIOSK_BAD_PROFILE_KEEP="${KIOSK_BAD_PROFILE_KEEP:-1}"
ENABLE_PI3_LOW_MEMORY_TUNING="${ENABLE_PI3_LOW_MEMORY_TUNING:-1}"

ENABLE_NETWORK_GUARD="${ENABLE_NETWORK_GUARD:-1}"
KEEPALIVE_INTERVAL_SECS="${KEEPALIVE_INTERVAL_SECS:-20}"
KEEPALIVE_FAIL_THRESHOLD="${KEEPALIVE_FAIL_THRESHOLD:-6}"
KEEPALIVE_ACTION_COOLDOWN_SECS="${KEEPALIVE_ACTION_COOLDOWN_SECS:-300}"
NETWORK_FAILURE_REBOOT_AFTER_SECS="${NETWORK_FAILURE_REBOOT_AFTER_SECS:-3600}"
NETWORK_FAILURE_REBOOT_COOLDOWN_SECS="${NETWORK_FAILURE_REBOOT_COOLDOWN_SECS:-21600}"
NETWORK_RECOVERY_MAX_ATTEMPTS="${NETWORK_RECOVERY_MAX_ATTEMPTS:-4}"

ENABLE_SOFTWARE_WATCHDOG="${ENABLE_SOFTWARE_WATCHDOG:-1}"
SOFTWARE_WATCHDOG_SECS="${SOFTWARE_WATCHDOG_SECS:-90s}"
ENABLE_HARDWARE_WATCHDOG="${ENABLE_HARDWARE_WATCHDOG:-1}"
HARDWARE_WATCHDOG_SECS="${HARDWARE_WATCHDOG_SECS:-15s}"
REBOOT_WATCHDOG_SECS="${REBOOT_WATCHDOG_SECS:-10min}"
ENABLE_EMERGENCY_REBOOT="${ENABLE_EMERGENCY_REBOOT:-1}"
ENABLE_PERIODIC_BROWSER_RECYCLE="${ENABLE_PERIODIC_BROWSER_RECYCLE:-1}"
ENABLE_ZRAM="${ENABLE_ZRAM:-1}"

ENABLE_CREDENTIAL_EXTENSION="${ENABLE_CREDENTIAL_EXTENSION:-1}"
ENABLE_WIFI_HOTKEY="${ENABLE_WIFI_HOTKEY:-1}"
DISABLE_WIFI_POWER_SAVE="${DISABLE_WIFI_POWER_SAVE:-1}"
KIOSK_AUTO_REBOOT_AFTER_INSTALL="${KIOSK_AUTO_REBOOT_AFTER_INSTALL:-1}"
JOURNAL_MAX_USE="${JOURNAL_MAX_USE:-100M}"

# Migrate older V5.x installations to the Pi 3 B+ defaults. Existing dashboard
# URL, user and credentials are preserved, but old Pi 5-oriented resource values
# must not silently override the 1 GB field profile.
if [[ "$PREVIOUS_KIOSK_CONFIG_VERSION" != "552" ]]; then
  KIOSK_BROWSER_RECYCLE_HOURS=24
  HARDWARE_WATCHDOG_SECS=15s
  KIOSK_RENDERER_PROCESS_LIMIT=2
  KIOSK_DISK_CACHE_BYTES=16777216
  KIOSK_MEDIA_CACHE_BYTES=8388608
  KIOSK_LOW_MEMORY_MB=96
  KIOSK_LOW_SWAP_MB=64
  KIOSK_MEMORY_FAIL_THRESHOLD=3
  KIOSK_OOM_SCORE_ADJ=300
  KIOSK_BAD_PROFILE_KEEP=1
fi

shell_bool() {
  case "${1:-}" in
    1|true|TRUE|yes|YES|on|ON) printf '1' ;;
    *) printf '0' ;;
  esac
}

ENABLE_NETWORK_GUARD="$(shell_bool "$ENABLE_NETWORK_GUARD")"
ENABLE_SOFTWARE_WATCHDOG="$(shell_bool "$ENABLE_SOFTWARE_WATCHDOG")"
ENABLE_HARDWARE_WATCHDOG="$(shell_bool "$ENABLE_HARDWARE_WATCHDOG")"
ENABLE_EMERGENCY_REBOOT="$(shell_bool "$ENABLE_EMERGENCY_REBOOT")"
ENABLE_PERIODIC_BROWSER_RECYCLE="$(shell_bool "$ENABLE_PERIODIC_BROWSER_RECYCLE")"
ENABLE_ZRAM="$(shell_bool "$ENABLE_ZRAM")"
ENABLE_CREDENTIAL_EXTENSION="$(shell_bool "$ENABLE_CREDENTIAL_EXTENSION")"
ENABLE_WIFI_HOTKEY="$(shell_bool "$ENABLE_WIFI_HOTKEY")"
DISABLE_WIFI_POWER_SAVE="$(shell_bool "$DISABLE_WIFI_POWER_SAVE")"
KIOSK_AUTO_REBOOT_AFTER_INSTALL="$(shell_bool "$KIOSK_AUTO_REBOOT_AFTER_INSTALL")"
ENABLE_PI3_LOW_MEMORY_TUNING="$(shell_bool "$ENABLE_PI3_LOW_MEMORY_TUNING")"
KIOSK_DISABLE_GPU="$(shell_bool "$KIOSK_DISABLE_GPU")"

require_positive_integer() {
  local name="$1"
  local value="$2"
  [[ "$value" =~ ^[0-9]+$ ]] && (( value > 0 )) || fatal "$name must be a positive integer."
}

require_nonnegative_integer() {
  local name="$1"
  local value="$2"
  [[ "$value" =~ ^[0-9]+$ ]] || fatal "$name must be zero or a positive integer."
}

require_positive_integer KIOSK_DEVTOOLS_PORT "$KIOSK_DEVTOOLS_PORT"
(( KIOSK_DEVTOOLS_PORT <= 65535 )) || fatal "KIOSK_DEVTOOLS_PORT must be <= 65535."
require_nonnegative_integer KIOSK_REFRESH_SECS "$KIOSK_REFRESH_SECS"
require_positive_integer KIOSK_BROWSER_RESTART_MIN_SECS "$KIOSK_BROWSER_RESTART_MIN_SECS"
require_positive_integer KIOSK_BROWSER_RESTART_MAX_SECS "$KIOSK_BROWSER_RESTART_MAX_SECS"
require_positive_integer KIOSK_BROWSER_STABLE_SECS "$KIOSK_BROWSER_STABLE_SECS"
require_positive_integer KIOSK_BROWSER_CRASH_WINDOW_SECS "$KIOSK_BROWSER_CRASH_WINDOW_SECS"
require_positive_integer KIOSK_BROWSER_CRASH_RESET_THRESHOLD "$KIOSK_BROWSER_CRASH_RESET_THRESHOLD"
require_positive_integer KIOSK_BROWSER_RECYCLE_HOURS "$KIOSK_BROWSER_RECYCLE_HOURS"
require_positive_integer KIOSK_X_RESTART_MIN_SECS "$KIOSK_X_RESTART_MIN_SECS"
require_positive_integer KIOSK_X_RESTART_MAX_SECS "$KIOSK_X_RESTART_MAX_SECS"
require_positive_integer KIOSK_X_STABLE_SECS "$KIOSK_X_STABLE_SECS"
require_positive_integer KIOSK_HEALTH_INTERVAL_SECS "$KIOSK_HEALTH_INTERVAL_SECS"
require_positive_integer KIOSK_HEALTH_FAIL_THRESHOLD "$KIOSK_HEALTH_FAIL_THRESHOLD"
require_positive_integer KIOSK_HEALTH_RESTART_WAIT_SECS "$KIOSK_HEALTH_RESTART_WAIT_SECS"
require_positive_integer KIOSK_RENDERER_PROBE_TIMEOUT_SECS "$KIOSK_RENDERER_PROBE_TIMEOUT_SECS"
require_positive_integer KIOSK_HTTP_TIMEOUT_SECS "$KIOSK_HTTP_TIMEOUT_SECS"
require_nonnegative_integer KIOSK_DATA_FRESHNESS_MAX_AGE_SECS "$KIOSK_DATA_FRESHNESS_MAX_AGE_SECS"
case "$KIOSK_DATA_FRESHNESS_MODE" in
  off|auto) ;;
  selector)
    [[ -n "$KIOSK_DATA_FRESHNESS_SELECTOR" ]] || fatal "KIOSK_DATA_FRESHNESS_SELECTOR is required when freshness mode is selector."
    ;;
  expression)
    [[ -n "$KIOSK_DATA_FRESHNESS_EXPRESSION" ]] || fatal "KIOSK_DATA_FRESHNESS_EXPRESSION is required when freshness mode is expression."
    ;;
  *) fatal "KIOSK_DATA_FRESHNESS_MODE must be off, auto, selector or expression." ;;
esac
require_positive_integer KIOSK_EMERGENCY_REBOOT_AFTER_SECS "$KIOSK_EMERGENCY_REBOOT_AFTER_SECS"
require_positive_integer KIOSK_EMERGENCY_REBOOT_COOLDOWN_SECS "$KIOSK_EMERGENCY_REBOOT_COOLDOWN_SECS"
require_positive_integer KIOSK_RENDERER_PROCESS_LIMIT "$KIOSK_RENDERER_PROCESS_LIMIT"
require_positive_integer KIOSK_DISK_CACHE_BYTES "$KIOSK_DISK_CACHE_BYTES"
require_positive_integer KIOSK_MEDIA_CACHE_BYTES "$KIOSK_MEDIA_CACHE_BYTES"
require_positive_integer KIOSK_LOW_MEMORY_MB "$KIOSK_LOW_MEMORY_MB"
require_positive_integer KIOSK_LOW_SWAP_MB "$KIOSK_LOW_SWAP_MB"
require_positive_integer KIOSK_MEMORY_FAIL_THRESHOLD "$KIOSK_MEMORY_FAIL_THRESHOLD"
require_nonnegative_integer KIOSK_OOM_SCORE_ADJ "$KIOSK_OOM_SCORE_ADJ"
require_positive_integer KIOSK_BAD_PROFILE_KEEP "$KIOSK_BAD_PROFILE_KEEP"
(( KIOSK_OOM_SCORE_ADJ <= 1000 )) || fatal "KIOSK_OOM_SCORE_ADJ must be <= 1000."
require_positive_integer KEEPALIVE_INTERVAL_SECS "$KEEPALIVE_INTERVAL_SECS"
require_positive_integer KEEPALIVE_FAIL_THRESHOLD "$KEEPALIVE_FAIL_THRESHOLD"
require_positive_integer KEEPALIVE_ACTION_COOLDOWN_SECS "$KEEPALIVE_ACTION_COOLDOWN_SECS"
require_positive_integer NETWORK_FAILURE_REBOOT_AFTER_SECS "$NETWORK_FAILURE_REBOOT_AFTER_SECS"
require_positive_integer NETWORK_FAILURE_REBOOT_COOLDOWN_SECS "$NETWORK_FAILURE_REBOOT_COOLDOWN_SECS"
require_positive_integer NETWORK_RECOVERY_MAX_ATTEMPTS "$NETWORK_RECOVERY_MAX_ATTEMPTS"

if (( KIOSK_BROWSER_RESTART_MIN_SECS > KIOSK_BROWSER_RESTART_MAX_SECS )); then
  fatal "KIOSK_BROWSER_RESTART_MIN_SECS cannot exceed KIOSK_BROWSER_RESTART_MAX_SECS."
fi
if (( KIOSK_X_RESTART_MIN_SECS > KIOSK_X_RESTART_MAX_SECS )); then
  fatal "KIOSK_X_RESTART_MIN_SECS cannot exceed KIOSK_X_RESTART_MAX_SECS."
fi
if (( KIOSK_EMERGENCY_REBOOT_COOLDOWN_SECS < KIOSK_EMERGENCY_REBOOT_AFTER_SECS )); then
  fatal "KIOSK_EMERGENCY_REBOOT_COOLDOWN_SECS must be >= KIOSK_EMERGENCY_REBOOT_AFTER_SECS."
fi
if (( NETWORK_FAILURE_REBOOT_COOLDOWN_SECS < NETWORK_FAILURE_REBOOT_AFTER_SECS )); then
  fatal "NETWORK_FAILURE_REBOOT_COOLDOWN_SECS must be >= NETWORK_FAILURE_REBOOT_AFTER_SECS."
fi

write_root_file() {
  local path="$1"
  local mode="${2:-0644}"
  local temp_file
  temp_file="$(mktemp)"
  cat >"$temp_file"
  install -D -o root -g root -m "$mode" "$temp_file" "$path"
  rm -f "$temp_file"
}

write_user_file() {
  local path="$1"
  local mode="${2:-0644}"
  local temp_file
  temp_file="$(mktemp)"
  cat >"$temp_file"
  install -D -o "$TARGET_USER" -g "$TARGET_GROUP" -m "$mode" "$temp_file" "$path"
  rm -f "$temp_file"
}

backup_path() {
  local path="$1"
  [[ -e "$path" || -L "$path" ]] || return 0
  install -d -m 0700 "$BACKUP_DIR"
  cp -a --parents "$path" "$BACKUP_DIR/"
}

apt_install_required() {
  DEBIAN_FRONTEND=noninteractive apt-get install -y --no-install-recommends "$@"
}

apt_install_optional() {
  DEBIAN_FRONTEND=noninteractive apt-get install -y --no-install-recommends "$@" || true
}

add_user_to_existing_groups() {
  local group
  local groups=()
  local joined=""

  for group in video render input audio plugdev netdev; do
    if getent group "$group" >/dev/null 2>&1; then
      groups+=("$group")
    fi
  done

  if (( ${#groups[@]} > 0 )); then
    joined="$(IFS=,; printf '%s' "${groups[*]}")"
    usermod -aG "$joined" "$TARGET_USER"
  fi
}

log "Installing 24x7 kiosk for user '$TARGET_USER'"
log "Dashboard URL: $KIOSK_URL"
log "Detected model: $PI_MODEL"
log "Hardware profile: $KIOSK_HARDWARE_PROFILE, RAM: ${TOTAL_RAM_MB}MB"
if [[ "$KIOSK_HARDWARE_PROFILE" != "pi3bplus-1gb" ]]; then
  warn "This build is optimized for Pi 3 B+; current board is used as a compatibility/test target."
fi
log "Backups: $BACKUP_DIR"

install -d -m 0700 "$BACKUP_DIR"
backup_path "$CONFIG_FILE"
backup_path /etc/systemd/system/kiosk.service
backup_path /etc/X11/xorg.conf.d/99-vc4.conf
backup_path /etc/systemd/system/kiosk-healthcheck.service
backup_path /etc/systemd/system/kiosk-healthcheck.timer
backup_path /etc/systemd/system/kiosk-guard.service
backup_path /etc/systemd/system/kiosk-browser-recycle.service
backup_path /etc/systemd/system/kiosk-browser-recycle.timer
backup_path /etc/systemd/zram-generator.conf
backup_path /etc/default/zramswap
backup_path /etc/ztab
backup_path "$TARGET_HOME/.bash_profile"
backup_path "$TARGET_HOME/.config/openbox/autostart"

log "Installing required packages"
apt-get update
apt_install_required \
  ca-certificates curl dbus-x11 iproute2 iputils-ping network-manager procps sudo util-linux \
  python3 python3-websocket python3-xdg xserver-xorg xinit openbox unclutter \
  xbindkeys xdotool xterm x11-xserver-utils x11-utils
apt_install_optional xserver-xorg-legacy
if [[ "$ENABLE_ZRAM" == "1" ]]; then
  apt_install_optional systemd-zram-generator
fi

if ! command -v chromium >/dev/null 2>&1 && ! command -v chromium-browser >/dev/null 2>&1; then
  apt_install_optional chromium
fi
if ! command -v chromium >/dev/null 2>&1 && ! command -v chromium-browser >/dev/null 2>&1; then
  apt_install_optional chromium-browser
fi

CHROMIUM_BIN="$(command -v chromium || command -v chromium-browser || true)"
[[ -n "$CHROMIUM_BIN" ]] || fatal "Chromium was not found after package installation."
DBUS_RUN_SESSION_BIN="$(command -v dbus-run-session || true)"
[[ -n "$DBUS_RUN_SESSION_BIN" ]] || fatal "dbus-run-session is not installed."
NMTUI_BIN="$(command -v nmtui || true)"

add_user_to_existing_groups

# Repair ownership commonly broken by previous root-run scripts or abrupt power
# removal. Chromium exits immediately when its profile is not writable.
install -d -o "$TARGET_USER" -g "$TARGET_GROUP" -m 0700 "$TARGET_HOME/.kiosk-chrome"
chown -R "$TARGET_USER:$TARGET_GROUP" "$TARGET_HOME/.kiosk-chrome"

if [[ -e "$TARGET_HOME/.Xauthority" ]]; then
  chown "$TARGET_USER:$TARGET_GROUP" "$TARGET_HOME/.Xauthority"
  chmod 0600 "$TARGET_HOME/.Xauthority"
fi

# Xorg.wrap must allow the dedicated non-root kiosk service to start X on tty1.
write_root_file /etc/X11/Xwrapper.config 0644 <<'XWRAPPER_CONFIG'
allowed_users=anybody
needs_root_rights=yes
XWRAPPER_CONFIG

# Pi 3 B+ normally exposes one VC4 display DRM card and needs no forced BusID.
# Pi 5 exposes separate V3D and VC4 cards, so install the selector only there.
if [[ "$KIOSK_NEEDS_VC4_PRIMARY" == "1" ]]; then
  write_root_file /etc/X11/xorg.conf.d/99-vc4.conf 0644 <<'VC4_XORG_CONFIG'
Section "OutputClass"
    Identifier "Raspberry Pi VC4 display"
    MatchDriver "vc4"
    Driver "modesetting"
    Option "PrimaryGPU" "true"
EndSection
VC4_XORG_CONFIG
else
  rm -f /etc/X11/xorg.conf.d/99-vc4.conf
fi

# Shared configuration, written with shell-safe escaping.
{
  printf 'KIOSK_CONFIG_VERSION=%q\n' '552'
  printf 'KIOSK_USER=%q\n' "$TARGET_USER"
  printf 'KIOSK_UID=%q\n' "$TARGET_UID"
  printf 'KIOSK_GROUP=%q\n' "$TARGET_GROUP"
  printf 'KIOSK_HOME=%q\n' "$TARGET_HOME"
  printf 'KIOSK_RUNTIME_DIR=%q\n' "/run/kiosk-runtime"
  printf 'KIOSK_URL=%q\n' "$KIOSK_URL"
  printf 'KIOSK_HARDWARE_PROFILE=%q\n' "$KIOSK_HARDWARE_PROFILE"
  printf 'KIOSK_NEEDS_VC4_PRIMARY=%q\n' "$KIOSK_NEEDS_VC4_PRIMARY"
  printf 'KIOSK_TOTAL_RAM_MB=%q\n' "$TOTAL_RAM_MB"
  printf 'KIOSK_REFRESH_SECS=%q\n' "$KIOSK_REFRESH_SECS"
  printf 'KIOSK_DISABLE_GPU=%q\n' "$KIOSK_DISABLE_GPU"
  printf 'KIOSK_DEVTOOLS_PORT=%q\n' "$KIOSK_DEVTOOLS_PORT"
  printf 'KIOSK_BROWSER_RESTART_MIN_SECS=%q\n' "$KIOSK_BROWSER_RESTART_MIN_SECS"
  printf 'KIOSK_BROWSER_RESTART_MAX_SECS=%q\n' "$KIOSK_BROWSER_RESTART_MAX_SECS"
  printf 'KIOSK_BROWSER_STABLE_SECS=%q\n' "$KIOSK_BROWSER_STABLE_SECS"
  printf 'KIOSK_BROWSER_CRASH_WINDOW_SECS=%q\n' "$KIOSK_BROWSER_CRASH_WINDOW_SECS"
  printf 'KIOSK_BROWSER_CRASH_RESET_THRESHOLD=%q\n' "$KIOSK_BROWSER_CRASH_RESET_THRESHOLD"
  printf 'KIOSK_BROWSER_RECYCLE_HOURS=%q\n' "$KIOSK_BROWSER_RECYCLE_HOURS"
  printf 'KIOSK_X_RESTART_MIN_SECS=%q\n' "$KIOSK_X_RESTART_MIN_SECS"
  printf 'KIOSK_X_RESTART_MAX_SECS=%q\n' "$KIOSK_X_RESTART_MAX_SECS"
  printf 'KIOSK_X_STABLE_SECS=%q\n' "$KIOSK_X_STABLE_SECS"
  printf 'KIOSK_HEALTH_INTERVAL_SECS=%q\n' "$KIOSK_HEALTH_INTERVAL_SECS"
  printf 'KIOSK_HEALTH_FAIL_THRESHOLD=%q\n' "$KIOSK_HEALTH_FAIL_THRESHOLD"
  printf 'KIOSK_HEALTH_RESTART_WAIT_SECS=%q\n' "$KIOSK_HEALTH_RESTART_WAIT_SECS"
  printf 'KIOSK_RENDERER_PROBE_TIMEOUT_SECS=%q\n' "$KIOSK_RENDERER_PROBE_TIMEOUT_SECS"
  printf 'KIOSK_HTTP_TIMEOUT_SECS=%q\n' "$KIOSK_HTTP_TIMEOUT_SECS"
  printf 'KIOSK_DATA_FRESHNESS_MODE=%q\n' "$KIOSK_DATA_FRESHNESS_MODE"
  printf 'KIOSK_DATA_FRESHNESS_MAX_AGE_SECS=%q\n' "$KIOSK_DATA_FRESHNESS_MAX_AGE_SECS"
  printf 'KIOSK_DATA_FRESHNESS_SELECTOR=%q\n' "$KIOSK_DATA_FRESHNESS_SELECTOR"
  printf 'KIOSK_DATA_FRESHNESS_ATTRIBUTE=%q\n' "$KIOSK_DATA_FRESHNESS_ATTRIBUTE"
  printf 'KIOSK_DATA_FRESHNESS_EXPRESSION=%q\n' "$KIOSK_DATA_FRESHNESS_EXPRESSION"
  printf 'KIOSK_FRESHNESS_EXEMPT_PATH_REGEX=%q\n' "$KIOSK_FRESHNESS_EXEMPT_PATH_REGEX"
  printf 'KIOSK_EMERGENCY_REBOOT_AFTER_SECS=%q\n' "$KIOSK_EMERGENCY_REBOOT_AFTER_SECS"
  printf 'KIOSK_EMERGENCY_REBOOT_COOLDOWN_SECS=%q\n' "$KIOSK_EMERGENCY_REBOOT_COOLDOWN_SECS"
  printf 'KIOSK_RENDERER_PROCESS_LIMIT=%q\n' "$KIOSK_RENDERER_PROCESS_LIMIT"
  printf 'KIOSK_DISK_CACHE_BYTES=%q\n' "$KIOSK_DISK_CACHE_BYTES"
  printf 'KIOSK_MEDIA_CACHE_BYTES=%q\n' "$KIOSK_MEDIA_CACHE_BYTES"
  printf 'KIOSK_LOW_MEMORY_MB=%q\n' "$KIOSK_LOW_MEMORY_MB"
  printf 'KIOSK_LOW_SWAP_MB=%q\n' "$KIOSK_LOW_SWAP_MB"
  printf 'KIOSK_MEMORY_FAIL_THRESHOLD=%q\n' "$KIOSK_MEMORY_FAIL_THRESHOLD"
  printf 'KIOSK_OOM_SCORE_ADJ=%q\n' "$KIOSK_OOM_SCORE_ADJ"
  printf 'KIOSK_BAD_PROFILE_KEEP=%q\n' "$KIOSK_BAD_PROFILE_KEEP"
  printf 'ENABLE_PI3_LOW_MEMORY_TUNING=%q\n' "$ENABLE_PI3_LOW_MEMORY_TUNING"
  printf 'ENABLE_NETWORK_GUARD=%q\n' "$ENABLE_NETWORK_GUARD"
  printf 'KEEPALIVE_INTERVAL_SECS=%q\n' "$KEEPALIVE_INTERVAL_SECS"
  printf 'KEEPALIVE_FAIL_THRESHOLD=%q\n' "$KEEPALIVE_FAIL_THRESHOLD"
  printf 'KEEPALIVE_ACTION_COOLDOWN_SECS=%q\n' "$KEEPALIVE_ACTION_COOLDOWN_SECS"
  printf 'NETWORK_FAILURE_REBOOT_AFTER_SECS=%q\n' "$NETWORK_FAILURE_REBOOT_AFTER_SECS"
  printf 'NETWORK_FAILURE_REBOOT_COOLDOWN_SECS=%q\n' "$NETWORK_FAILURE_REBOOT_COOLDOWN_SECS"
  printf 'NETWORK_RECOVERY_MAX_ATTEMPTS=%q\n' "$NETWORK_RECOVERY_MAX_ATTEMPTS"
  printf 'ENABLE_SOFTWARE_WATCHDOG=%q\n' "$ENABLE_SOFTWARE_WATCHDOG"
  printf 'SOFTWARE_WATCHDOG_SECS=%q\n' "$SOFTWARE_WATCHDOG_SECS"
  printf 'ENABLE_HARDWARE_WATCHDOG=%q\n' "$ENABLE_HARDWARE_WATCHDOG"
  printf 'HARDWARE_WATCHDOG_SECS=%q\n' "$HARDWARE_WATCHDOG_SECS"
  printf 'REBOOT_WATCHDOG_SECS=%q\n' "$REBOOT_WATCHDOG_SECS"
  printf 'ENABLE_EMERGENCY_REBOOT=%q\n' "$ENABLE_EMERGENCY_REBOOT"
  printf 'ENABLE_PERIODIC_BROWSER_RECYCLE=%q\n' "$ENABLE_PERIODIC_BROWSER_RECYCLE"
  printf 'ENABLE_ZRAM=%q\n' "$ENABLE_ZRAM"
  printf 'ENABLE_CREDENTIAL_EXTENSION=%q\n' "$ENABLE_CREDENTIAL_EXTENSION"
  printf 'ENABLE_WIFI_HOTKEY=%q\n' "$ENABLE_WIFI_HOTKEY"
  printf 'DISABLE_WIFI_POWER_SAVE=%q\n' "$DISABLE_WIFI_POWER_SAVE"
  printf 'KIOSK_AUTO_REBOOT_AFTER_INSTALL=%q\n' "$KIOSK_AUTO_REBOOT_AFTER_INSTALL"
} >"$CONFIG_FILE"
chmod 0644 "$CONFIG_FILE"

# Remove all older login-shell launchers. V5.5.2 starts the kiosk directly from
# systemd, so the user's shell and profile contents cannot block kiosk startup.
PROFILE="$TARGET_HOME/.bash_profile"
if [[ -f "$PROFILE" ]]; then
  sed -i '/# BEGIN KIOSK AUTOSTART/,/# END KIOSK AUTOSTART/d' "$PROFILE"
  sed -i '/# BEGIN KIOSK 24X7 TTY1/,/# END KIOSK 24X7 TTY1/d' "$PROFILE"
  chown "$TARGET_USER:$TARGET_GROUP" "$PROFILE"
fi

# V4 launched unclutter from Openbox autostart while V5.x launches it directly.
# Remove only the exact legacy line so upgraded units do not run two copies.
OPENBOX_AUTOSTART="$TARGET_HOME/.config/openbox/autostart"
if [[ -f "$OPENBOX_AUTOSTART" ]]; then
  backup_path "$OPENBOX_AUTOSTART"
  sed -i -E '/^[[:space:]]*unclutter[[:space:]]+-idle[[:space:]]+1[[:space:]]+-root[[:space:]]*&?[[:space:]]*$/d' "$OPENBOX_AUTOSTART"
  chown "$TARGET_USER:$TARGET_GROUP" "$OPENBOX_AUTOSTART"
fi

# Prevent console blanking before or during kiosk startup.
CMDLINE_FILE=""
for candidate in /boot/firmware/cmdline.txt /boot/cmdline.txt; do
  if [[ -f "$candidate" ]]; then
    CMDLINE_FILE="$candidate"
    break
  fi
done
if [[ -n "$CMDLINE_FILE" ]]; then
  backup_path "$CMDLINE_FILE"
  if ! grep -qw 'consoleblank=0' "$CMDLINE_FILE"; then
    sed -i '1 s/[[:space:]]*$//' "$CMDLINE_FILE"
    sed -i '1 s/$/ consoleblank=0/' "$CMDLINE_FILE"
  fi
fi

# Portable Raspberry Pi 3 B+ / Raspberry Pi 5 graphics configuration.
#
# Proven field behavior on Raspberry Pi OS 13 (Trixie) 32-bit / armhf:
#   - Pi 3 B+ with full vc4-kms-v3d can leave HDMI black even while Xorg,
#     Openbox and Chromium are all alive.
#   - Pi 3 B+ works with the vc4-fkms-v3d compatibility path.
#   - Pi 5 continues to use the normal vc4-kms-v3d full-KMS path.
#
# Raspberry Pi firmware evaluates [pi3] / [pi5] conditionals at boot, so the
# same SD card can safely move between both boards without manual editing.
# This managed block is rebuilt on every installer run to remain idempotent.
BOOT_CONFIG_FILE=""
for candidate in /boot/firmware/config.txt /boot/config.txt; do
  if [[ -f "$candidate" ]]; then
    BOOT_CONFIG_FILE="$candidate"
    break
  fi
done

if [[ -n "$BOOT_CONFIG_FILE" ]]; then
  log "Configuring portable Pi 3 B+ / Pi 5 boot graphics in $BOOT_CONFIG_FILE"
  backup_path "$BOOT_CONFIG_FILE"

  # Remove the block created by an earlier V5.5.2 run.
  sed -i \
    '/# BEGIN KIOSK BOARD GRAPHICS/,/# END KIOSK BOARD GRAPHICS/d' \
    "$BOOT_CONFIG_FILE"

  # Disable any previously active VC4 KMS/FKMS overlay line before adding the
  # board-specific block. This also safely absorbs a manual fix made before
  # installing V5.5.2 and prevents duplicate overlay loading.
  sed -i -E \
    's@^([[:space:]]*dtoverlay=vc4-(f?kms)-v3d([,[:space:]].*)?)$@# KIOSK disabled previous graphics overlay: \1@' \
    "$BOOT_CONFIG_FILE"

  cat >>"$BOOT_CONFIG_FILE" <<'KIOSK_BOARD_GRAPHICS'

# BEGIN KIOSK BOARD GRAPHICS
# Portable kiosk graphics selection: one SD card for Pi 3 B+ and Pi 5.
[pi3]
dtoverlay=vc4-fkms-v3d

[pi5]
dtoverlay=vc4-kms-v3d

[all]
# END KIOSK BOARD GRAPHICS
KIOSK_BOARD_GRAPHICS

  log "Boot graphics configured: Pi 3 B+ -> FKMS; Pi 5 -> full KMS"
  log "A reboot is required before a newly changed graphics overlay takes effect"
else
  warn "No Raspberry Pi config.txt was found; portable Pi 3/Pi 5 graphics setup was skipped."
fi

# Disable Wi-Fi power saving because it can cause long-running kiosk dropouts.
if [[ "$DISABLE_WIFI_POWER_SAVE" == "1" ]]; then
  write_root_file /etc/NetworkManager/conf.d/20-kiosk-wifi-powersave.conf 0644 <<'NM_POWERSAVE'
[connection]
wifi.powersave=2
NM_POWERSAVE
else
  rm -f /etc/NetworkManager/conf.d/20-kiosk-wifi-powersave.conf
fi

# Avoid switching the active network manager during an SSH installation. The
# service changes take effect safely on reboot.
systemctl enable NetworkManager.service >/dev/null 2>&1 || true
if systemctl list-unit-files dhcpcd.service >/dev/null 2>&1; then
  systemctl disable dhcpcd.service >/dev/null 2>&1 || true
fi

# Optional compressed RAM swap. This gives Chromium breathing room during
# temporary memory spikes without writing swap traffic to the SD card. Prefer
# systemd-zram-generator; fall back to either modern or legacy zram-tools.
if [[ "$ENABLE_ZRAM" == "1" ]]; then
  ZRAM_BACKEND=""
  ZRAM_GENERATOR=""

  # Debian/Raspberry Pi OS package name is systemd-zram-generator, but the
  # installed executable is named zram-generator. Keep legacy candidates too
  # so the same installer remains compatible with older/custom images.
  for generator in \
    /usr/lib/systemd/system-generators/zram-generator \
    /lib/systemd/system-generators/zram-generator \
    /usr/lib/systemd/system-generators/systemd-zram-generator \
    /lib/systemd/system-generators/systemd-zram-generator; do
    if [[ -x "$generator" ]]; then
      ZRAM_GENERATOR="$generator"
      break
    fi
  done

  if [[ -n "$ZRAM_GENERATOR" ]]; then
    ZRAM_BACKEND="systemd-zram-generator"
    backup_path /etc/systemd/zram-generator.conf
    write_root_file /etc/systemd/zram-generator.conf 0644 <<'ZRAM_CONFIG'
[zram0]
zram-size = ram / 2
compression-algorithm = lz4
swap-priority = 100
fs-type = swap
ZRAM_CONFIG

    # Prevent an older zram-tools service from creating a second conflicting
    # device after reboot. Do not stop an active swap device during installation.
    systemctl disable zram-config.service zramswap.service >/dev/null 2>&1 || true
    log "Configured ZRAM with systemd-zram-generator (50% of RAM)"
  else
    apt_install_optional zram-tools

    # Package installation can occur while systemd has not reloaded its unit
    # cache yet. Reload first, then detect both through systemctl and directly
    # from standard unit-file locations.
    systemctl daemon-reload >/dev/null 2>&1 || true

    ZRAMSWAP_UNIT_AVAILABLE=0
    ZRAM_CONFIG_UNIT_AVAILABLE=0

    if systemctl cat zramswap.service >/dev/null 2>&1 ||
       [[ -f /etc/systemd/system/zramswap.service ]] ||
       [[ -f /run/systemd/system/zramswap.service ]] ||
       [[ -f /usr/lib/systemd/system/zramswap.service ]] ||
       [[ -f /lib/systemd/system/zramswap.service ]]; then
      ZRAMSWAP_UNIT_AVAILABLE=1
    fi

    if systemctl cat zram-config.service >/dev/null 2>&1 ||
       [[ -f /etc/systemd/system/zram-config.service ]] ||
       [[ -f /run/systemd/system/zram-config.service ]] ||
       [[ -f /usr/lib/systemd/system/zram-config.service ]] ||
       [[ -f /lib/systemd/system/zram-config.service ]]; then
      ZRAM_CONFIG_UNIT_AVAILABLE=1
    fi

    if [[ "$ZRAMSWAP_UNIT_AVAILABLE" == "1" ]]; then
      ZRAM_BACKEND="zram-tools-zramswap"
      backup_path /etc/default/zramswap
      write_root_file /etc/default/zramswap 0644 <<'ZRAMSWAP_CONFIG'
ALGO=lz4
PERCENT=50
PRIORITY=100
ZRAMSWAP_CONFIG
      systemctl enable zramswap.service >/dev/null 2>&1 || true
      systemctl disable zram-config.service >/dev/null 2>&1 || true
      log "Configured ZRAM with zramswap.service (50% of RAM)"
    elif [[ "$ZRAM_CONFIG_UNIT_AVAILABLE" == "1" ]]; then
      ZRAM_BACKEND="zram-tools-zram-config"
      backup_path /etc/ztab
      write_root_file /etc/ztab 0644 <<'ZTAB_CONFIG'
# mode  swap  pri  disksize  mem_limit  lz_algo  stream  mountpoint  fs  options
swap    swap  100  ram/2     0          lz4      1
ZTAB_CONFIG
      systemctl enable zram-config.service >/dev/null 2>&1 || true
      systemctl disable zramswap.service >/dev/null 2>&1 || true
      log "Configured ZRAM with zram-config.service (50% of RAM)"
    else
      fatal "ENABLE_ZRAM=1, but no usable ZRAM generator or zram-tools service was found after installation."
    fi
  fi

  # A running old ZRAM device cannot be resized safely while Chromium or SSH may
  # be using it. The desired configuration is guaranteed on the requested reboot.
  if swapon --noheadings --raw --show=NAME 2>/dev/null | grep -q '^/dev/zram'; then
    log "An active ZRAM device exists; the V5.5.2 50% configuration will apply after reboot"
  fi
else
  rm -f /etc/systemd/zram-generator.conf
  systemctl disable zram-config.service zramswap.service >/dev/null 2>&1 || true
fi

if [[ "$ENABLE_ZRAM" == "1" && "$ENABLE_PI3_LOW_MEMORY_TUNING" == "1" ]]; then
  write_root_file /etc/sysctl.d/90-kiosk-pi3-memory.conf 0644 <<'SYSCTL_MEMORY'
# Prefer compressed RAM swap over killing Chromium during temporary peaks.
vm.swappiness=100
vm.page-cluster=0
# Keep dirty write bursts small on microSD storage.
vm.dirty_background_ratio=5
vm.dirty_ratio=15
SYSCTL_MEMORY
  sysctl --system >/dev/null 2>&1 || true
else
  rm -f /etc/sysctl.d/90-kiosk-pi3-memory.conf
fi

# Limit journal growth so a verbose browser cannot fill the SD card.
write_root_file /etc/systemd/journald.conf.d/20-kiosk-limits.conf 0644 <<EOF
[Journal]
SystemMaxUse=${JOURNAL_MAX_USE}
RuntimeMaxUse=64M
MaxRetentionSec=14day
Compress=yes
EOF

# ------------------------- Auto-login extension -------------------------------
if [[ "$ENABLE_CREDENTIAL_EXTENSION" == "1" ]]; then
  install -d -o "$TARGET_USER" -g "$TARGET_GROUP" -m 0755 "$TARGET_HOME/kiosk-ext"

  write_user_file "$TARGET_HOME/kiosk-ext/manifest.json" 0644 <<'EXT_MANIFEST'
{
  "manifest_version": 3,
  "name": "IoT Dashboard Kiosk Login",
  "version": "2.1.0",
  "description": "Stores the last submitted dashboard credentials and performs one controlled auto-login attempt per tab session.",
  "permissions": ["storage"],
  "host_permissions": ["https://web.itank.io/*"],
  "content_scripts": [
    {
      "matches": ["https://web.itank.io/*"],
      "js": ["content.js"],
      "run_at": "document_idle",
      "all_frames": true
    }
  ]
}
EXT_MANIFEST

  write_user_file "$TARGET_HOME/kiosk-ext/content.js" 0644 <<'EXT_CONTENT'
(() => {
  "use strict";

  if (!location.pathname.toLowerCase().startsWith("/login")) return;

  const STORAGE_KEY = "itankLastCredentialsV2";
  const LEGACY_STORAGE_KEY = "itankLastCreds";
  const SESSION_ATTEMPT_KEY = "itankAutoLoginAttemptedV2";
  const AUTO_LOGIN_DELAY_MS = 1500;
  const SCAN_INTERVAL_MS = 1000;
  const MAX_SCAN_TIME_MS = 60000;

  const wiredForms = new WeakSet();
  let bannerElement = null;
  let autoLoginTimer = null;
  let cancelled = false;
  let autoFlowHandled = false;

  const loadCredentials = () => new Promise((resolve) => {
    chrome.storage.local.get([STORAGE_KEY, LEGACY_STORAGE_KEY], (data) => {
      const current = data[STORAGE_KEY];
      if (current?.username && current?.password) {
        resolve(current);
        return;
      }

      const legacy = data[LEGACY_STORAGE_KEY];
      if (legacy?.username && legacy?.password) {
        chrome.storage.local.set({
          [STORAGE_KEY]: {
            username: legacy.username,
            password: legacy.password,
            savedAt: legacy.ts || Date.now()
          }
        }, () => {
          chrome.storage.local.remove(LEGACY_STORAGE_KEY, () => resolve(legacy));
        });
        return;
      }

      resolve(null);
    });
  });

  const saveCredentials = (username, password) => new Promise((resolve) => {
    chrome.storage.local.set({
      [STORAGE_KEY]: {
        username,
        password,
        savedAt: Date.now()
      }
    }, resolve);
  });

  const clearCredentials = () => new Promise((resolve) => {
    chrome.storage.local.remove(STORAGE_KEY, resolve);
  });

  function setNativeValue(element, value) {
    if (!element) return;

    const prototype = Object.getPrototypeOf(element);
    const descriptor = Object.getOwnPropertyDescriptor(prototype, "value");
    if (descriptor?.set) descriptor.set.call(element, value);
    else element.value = value;

    element.dispatchEvent(new Event("input", { bubbles: true }));
    element.dispatchEvent(new Event("change", { bubbles: true }));
  }

  function looksLikeUsername(element) {
    const haystack = [
      element.name,
      element.id,
      element.placeholder,
      element.autocomplete,
      element.type
    ].join(" ").toLowerCase();

    return /user|email|login|account|mobile|phone|text/.test(haystack);
  }

  function findLoginControls() {
    const password = document.querySelector('input[type="password"]');
    if (!password) return null;

    const form = password.closest("form") || password.closest('[role="form"]');
    const scope = form || document;
    const inputs = [...scope.querySelectorAll("input")];
    const username = inputs.find((input) => input !== password && looksLikeUsername(input)) || null;
    const submit = scope.querySelector('button[type="submit"], input[type="submit"]') ||
      [...scope.querySelectorAll("button")].find((button) => /login|sign in|submit/i.test(button.textContent || "")) ||
      null;

    return { form, username, password, submit };
  }

  function showBanner(message, includeReset = false) {
    bannerElement?.remove();

    const banner = document.createElement("div");
    bannerElement = banner;
    Object.assign(banner.style, {
      position: "fixed",
      right: "10px",
      bottom: "10px",
      zIndex: "2147483647",
      maxWidth: "420px",
      padding: "9px 12px",
      borderRadius: "7px",
      background: "rgba(0, 0, 0, 0.82)",
      color: "white",
      font: "13px/1.35 sans-serif",
      boxShadow: "0 2px 12px rgba(0, 0, 0, 0.35)"
    });

    const messageNode = document.createElement("span");
    messageNode.textContent = message;
    banner.appendChild(messageNode);

    if (includeReset) {
      const reset = document.createElement("button");
      reset.type = "button";
      reset.textContent = "Reset saved login";
      Object.assign(reset.style, {
        marginLeft: "10px",
        padding: "3px 7px",
        cursor: "pointer"
      });
      reset.addEventListener("click", async () => {
        cancelled = true;
        clearTimeout(autoLoginTimer);
        await clearCredentials();
        sessionStorage.removeItem(SESSION_ATTEMPT_KEY);
        showBanner("Saved credentials cleared.", false);
      });
      banner.appendChild(reset);
    }

    document.body?.appendChild(banner);
    setTimeout(() => {
      if (bannerElement === banner) banner.remove();
    }, 5000);
  }

  function submitForm(controls) {
    if (controls.form?.requestSubmit) {
      controls.form.requestSubmit();
    } else if (controls.submit?.click) {
      controls.submit.click();
    } else if (controls.form?.submit) {
      controls.form.submit();
    } else {
      controls.password.dispatchEvent(new KeyboardEvent("keydown", {
        key: "Enter",
        code: "Enter",
        bubbles: true
      }));
    }
  }

  function wireCredentialSaving(controls) {
    const key = controls.form || controls.password;
    if (!key || wiredForms.has(key)) return;
    wiredForms.add(key);

    const persist = () => {
      const username = controls.username?.value.trim() || "";
      const password = controls.password?.value || "";
      if (username && password) {
        saveCredentials(username, password).catch(() => {});
      }
    };

    controls.form?.addEventListener("submit", persist, { capture: true });
    controls.submit?.addEventListener("click", persist, { capture: true });
  }

  async function scan() {
    const controls = findLoginControls();
    if (!controls) return;

    wireCredentialSaving(controls);

    const credentials = await loadCredentials();
    if (!credentials?.username || !credentials?.password) return;

    setNativeValue(controls.username, credentials.username);
    setNativeValue(controls.password, credentials.password);

    if (autoFlowHandled) return;
    autoFlowHandled = true;

    const alreadyAttempted = sessionStorage.getItem(SESSION_ATTEMPT_KEY) === "1";
    if (alreadyAttempted) {
      showBanner("Saved login filled. Automatic retry is disabled to prevent a login loop.", true);
      return;
    }

    sessionStorage.setItem(SESSION_ATTEMPT_KEY, "1");
    showBanner("Automatic login in 1.5 seconds. Press Esc to cancel.", true);

    autoLoginTimer = setTimeout(() => {
      if (!cancelled) submitForm(controls);
    }, AUTO_LOGIN_DELAY_MS);
  }

  window.addEventListener("keydown", (event) => {
    if (event.key !== "Escape") return;
    cancelled = true;
    clearTimeout(autoLoginTimer);
    showBanner("Automatic login cancelled.", true);
  });

  scan().catch(() => {});
  const scanTimer = setInterval(() => scan().catch(() => {}), SCAN_INTERVAL_MS);
  setTimeout(() => clearInterval(scanTimer), MAX_SCAN_TIME_MS);
})();
EXT_CONTENT
else
  rm -rf "$TARGET_HOME/kiosk-ext"
fi

# ---------------------------- Wi-Fi hotkey ------------------------------------
write_root_file /usr/local/bin/kiosk-wifi.sh 0755 <<'WIFI_SCRIPT'
#!/usr/bin/env bash
set -Eeuo pipefail

if [[ -f /etc/default/kiosk ]]; then
  # shellcheck disable=SC1091
  source /etc/default/kiosk
fi

export DISPLAY="${DISPLAY:-:0}"
export XAUTHORITY="${XAUTHORITY:-${KIOSK_HOME:-$HOME}/.Xauthority}"

NMTUI_BIN="$(command -v nmtui || true)"
[[ -n "$NMTUI_BIN" ]] || {
  xterm -fullscreen -title "Wi-Fi setup" -e bash -lc \
    'echo "nmtui is not installed."; echo "Press Enter to close."; read -r'
  exit 1
}

# Avoid opening multiple Wi-Fi windows if the hotkey is pressed repeatedly.
if pgrep -u "$(id -u)" -f 'xterm.*Kiosk Wi-Fi Setup' >/dev/null 2>&1; then
  exit 0
fi

exec xterm \
  -fa Monospace \
  -fs 13 \
  -fullscreen \
  -title "Kiosk Wi-Fi Setup" \
  -e bash -lc '
    sudo -n "'"$NMTUI_BIN"'" || true
    sleep 2
    xdotool search --onlyvisible --class chromium windowactivate --sync key ctrl+r >/dev/null 2>&1 || \
      xdotool key ctrl+r >/dev/null 2>&1 || true
  '
WIFI_SCRIPT

if [[ "$ENABLE_WIFI_HOTKEY" == "1" && -n "$NMTUI_BIN" ]]; then
  write_user_file "$TARGET_HOME/.xbindkeysrc" 0644 <<'XBINDKEYS_CONFIG'
# Ctrl+Alt+W opens fullscreen NetworkManager Wi-Fi configuration.
"/usr/local/bin/kiosk-wifi.sh"
  control+alt + w
XBINDKEYS_CONFIG

  # Grant only the single terminal NetworkManager utility needed by the hotkey.
  write_root_file /etc/sudoers.d/kiosk-wifi 0440 <<EOF
${TARGET_USER} ALL=(root) NOPASSWD: ${NMTUI_BIN}
EOF
  visudo -cf /etc/sudoers.d/kiosk-wifi >/dev/null
else
  rm -f "$TARGET_HOME/.xbindkeysrc" /etc/sudoers.d/kiosk-wifi
fi

# --------------------------- Runtime scripts ----------------------------------
write_root_file /usr/local/bin/kiosk-preflight.sh 0755 <<'PREFLIGHT_SCRIPT'
#!/usr/bin/env bash
set -Eeuo pipefail

if [[ -f /etc/default/kiosk ]]; then
  # shellcheck disable=SC1091
  source /etc/default/kiosk
fi

KIOSK_USER="${KIOSK_USER:?KIOSK_USER is not configured}"
KIOSK_GROUP="${KIOSK_GROUP:?KIOSK_GROUP is not configured}"
KIOSK_HOME="${KIOSK_HOME:?KIOSK_HOME is not configured}"
KIOSK_RUNTIME_DIR="${KIOSK_RUNTIME_DIR:-/run/kiosk-runtime}"

log() {
  echo "[kiosk-preflight] $*"
}

# Wait for DRM enumeration on fast boots.
udevadm settle --timeout=15 >/dev/null 2>&1 || true
for _ in $(seq 1 20); do
  if find /sys/class/drm/card*/device/driver -maxdepth 0 -type l -printf '%l\n' 2>/dev/null | grep -q '/vc4$'; then
    break
  fi
  sleep 1
done

install -d -m 0755 /etc/X11/xorg.conf.d

# Detect the board again at every boot. This allows one tested image to be
# cloned from a Pi 5 test unit onto a Pi 3 B+ field unit without preserving the
# Pi 5-only Xorg workaround.
CURRENT_MODEL="$(tr -d '\0' </proc/device-tree/model 2>/dev/null || printf unknown)"
CURRENT_NEEDS_VC4_PRIMARY=0
case "$CURRENT_MODEL" in
  *"Raspberry Pi 5"*) CURRENT_NEEDS_VC4_PRIMARY=1 ;;
  *)
    if find /sys/class/drm/card*/device/driver -maxdepth 0 -type l -printf '%l\n' \
         2>/dev/null | grep -q '/v3d$' && \
       find /sys/class/drm/card*/device/driver -maxdepth 0 -type l -printf '%l\n' \
         2>/dev/null | grep -q '/vc4$'; then
      CURRENT_NEEDS_VC4_PRIMARY=1
    fi
    ;;
esac

if [[ "$CURRENT_NEEDS_VC4_PRIMARY" == "1" ]]; then
  expected_xorg="$(mktemp)"
  cat >"$expected_xorg" <<'EOF'
Section "OutputClass"
    Identifier "Raspberry Pi VC4 display"
    MatchDriver "vc4"
    Driver "modesetting"
    Option "PrimaryGPU" "true"
EndSection
EOF
  if ! cmp -s "$expected_xorg" /etc/X11/xorg.conf.d/99-vc4.conf; then
    log "Repairing Pi 5/multi-DRM VC4 primary-GPU selection"
    install -o root -g root -m 0644 "$expected_xorg" /etc/X11/xorg.conf.d/99-vc4.conf
  fi
  rm -f "$expected_xorg"
else
  # Pi 3 B+ uses the single VC4 display card directly. A forced primary-GPU
  # stanza is unnecessary and may preserve assumptions copied from Pi 5 tests.
  rm -f /etc/X11/xorg.conf.d/99-vc4.conf
fi

install -d -o "$KIOSK_USER" -g "$KIOSK_GROUP" -m 0700 \
  "$KIOSK_RUNTIME_DIR" "$KIOSK_RUNTIME_DIR/chrome-cache" "$KIOSK_HOME/.kiosk-chrome"
install -d -m 1777 /tmp/.X11-unix
chown -R "$KIOSK_USER:$KIOSK_GROUP" "$KIOSK_RUNTIME_DIR" "$KIOSK_HOME/.kiosk-chrome"

if [[ -e "$KIOSK_HOME/.Xauthority" ]]; then
  chown "$KIOSK_USER:$KIOSK_GROUP" "$KIOSK_HOME/.Xauthority"
  chmod 0600 "$KIOSK_HOME/.Xauthority"
fi

pid_cmdline() {
  local pid="$1"
  [[ -r "/proc/$pid/cmdline" ]] || return 1
  tr '\0' ' ' <"/proc/$pid/cmdline"
}

pid_is_xorg0() {
  local command_line
  command_line="$(pid_cmdline "$1" 2>/dev/null || true)"
  [[ "$command_line" =~ (^|/)(Xorg|X)[[:space:]]+:0([[:space:]]|$) ]]
}

pid_is_kiosk_chromium() {
  local command_line
  command_line="$(pid_cmdline "$1" 2>/dev/null || true)"
  [[ "$command_line" == *chromium*"--user-data-dir=$KIOSK_HOME/.kiosk-chrome"* ]]
}

# This is a dedicated kiosk on display :0. Any process still present before a
# new service start is an orphan from the previous cgroup and must be removed.
mapfile -t x_pids < <(pgrep -f '(^|/)(Xorg|X)[[:space:]]+:0([[:space:]]|$)' || true)
if (( ${#x_pids[@]} > 0 )); then
  log "Stopping orphaned Xorg process(es): ${x_pids[*]}"
  kill -TERM "${x_pids[@]}" 2>/dev/null || true
  remaining=("${x_pids[@]}")
  for _ in $(seq 1 10); do
    sleep 1
    remaining=()
    for pid in "${x_pids[@]}"; do
      pid_is_xorg0 "$pid" && remaining+=("$pid")
    done
    (( ${#remaining[@]} == 0 )) && break
  done
  (( ${#remaining[@]} == 0 )) || kill -KILL "${remaining[@]}" 2>/dev/null || true
fi

mapfile -t chromium_pids < <(
  pgrep -u "$KIOSK_USER" -f "(^|/)(chromium|chromium-browser).*--user-data-dir=${KIOSK_HOME}/\.kiosk-chrome" || true
)
if (( ${#chromium_pids[@]} > 0 )); then
  log "Stopping orphaned Chromium process(es): ${chromium_pids[*]}"
  kill -TERM "${chromium_pids[@]}" 2>/dev/null || true
  remaining=("${chromium_pids[@]}")
  for _ in $(seq 1 5); do
    sleep 1
    remaining=()
    for pid in "${chromium_pids[@]}"; do
      pid_is_kiosk_chromium "$pid" && remaining+=("$pid")
    done
    (( ${#remaining[@]} == 0 )) && break
  done
  (( ${#remaining[@]} == 0 )) || kill -KILL "${remaining[@]}" 2>/dev/null || true
fi

rm -f /tmp/.X0-lock /tmp/.X11-unix/X0
rm -f \
  "$KIOSK_HOME/.kiosk-chrome/SingletonCookie" \
  "$KIOSK_HOME/.kiosk-chrome/SingletonLock" \
  "$KIOSK_HOME/.kiosk-chrome/SingletonSocket" \
  "$KIOSK_RUNTIME_DIR/chromium.pid"
PREFLIGHT_SCRIPT

write_root_file /usr/local/bin/kiosk-session.sh 0755 <<'SESSION_SCRIPT'
#!/usr/bin/env bash
set -Eeuo pipefail

if [[ -f /etc/default/kiosk ]]; then
  # shellcheck disable=SC1091
  source /etc/default/kiosk
fi

KIOSK_USER="${KIOSK_USER:?KIOSK_USER is not configured}"
KIOSK_HOME="${KIOSK_HOME:?KIOSK_HOME is not configured}"
KIOSK_RUNTIME_DIR="${KIOSK_RUNTIME_DIR:-/run/kiosk-runtime}"
MIN_BACKOFF="${KIOSK_X_RESTART_MIN_SECS:-10}"
MAX_BACKOFF="${KIOSK_X_RESTART_MAX_SECS:-120}"
STABLE_SECS="${KIOSK_X_STABLE_SECS:-600}"

export HOME="$KIOSK_HOME"
export USER="$KIOSK_USER"
export LOGNAME="$KIOSK_USER"
export XDG_RUNTIME_DIR="$KIOSK_RUNTIME_DIR"
export XAUTHORITY="$KIOSK_HOME/.Xauthority"

install -d -m 0700 "$KIOSK_RUNTIME_DIR" "$KIOSK_RUNTIME_DIR/chrome-cache" "$KIOSK_HOME/.kiosk-chrome"

# Only one tty1 kiosk session is allowed. The lock disappears automatically
# when login/systemd kills the session.
exec 9>"$KIOSK_RUNTIME_DIR/session.lock"
if ! flock -n 9; then
  echo "[kiosk-session] Another kiosk session already owns the runtime lock" >&2
  exit 75
fi

cleanup_stale_state() {
  if pgrep -af '(^|/)(Xorg|X)[[:space:]]+:0([[:space:]]|$)' >/dev/null 2>&1; then
    echo "[kiosk-session] A live Xorg :0 process remains; requesting a full service restart" >&2
    return 1
  fi

  rm -f /tmp/.X0-lock /tmp/.X11-unix/X0 "$KIOSK_RUNTIME_DIR/chromium.pid"

  if ! pgrep -u "$(id -u)" -f '(^|/)(chromium|chromium-browser)([[:space:]]|$)' >/dev/null 2>&1; then
    rm -f \
      "$KIOSK_HOME/.kiosk-chrome/SingletonCookie" \
      "$KIOSK_HOME/.kiosk-chrome/SingletonLock" \
      "$KIOSK_HOME/.kiosk-chrome/SingletonSocket"
  fi
}

backoff="$MIN_BACKOFF"

while true; do
  if ! cleanup_stale_state; then
    exit 75
  fi

  start_epoch="$(date +%s)"
  echo "[kiosk-session] Starting X11 on tty1"

  set +e
  /usr/bin/startx /usr/local/bin/kiosk-xinit.sh -- :0 vt1 -keeptty -nolisten tcp
  exit_code=$?
  set -e

  end_epoch="$(date +%s)"
  runtime=$((end_epoch - start_epoch))
  echo "[kiosk-session] X session exited with status $exit_code after ${runtime}s"

  # If Xorg survived xinit, exiting the login session lets systemd kill the
  # entire cgroup and run root preflight before trying again.
  if pgrep -af '(^|/)(Xorg|X)[[:space:]]+:0([[:space:]]|$)' >/dev/null 2>&1; then
    echo "[kiosk-session] Xorg survived xinit; escalating to systemd" >&2
    exit 75
  fi

  cleanup_stale_state || exit 75

  if (( runtime >= STABLE_SECS )); then
    backoff="$MIN_BACKOFF"
  else
    next_backoff=$((backoff * 2))
    (( next_backoff > MAX_BACKOFF )) && next_backoff="$MAX_BACKOFF"
  fi

  echo "[kiosk-session] Retrying X in ${backoff}s"
  sleep "$backoff"

  if (( runtime < STABLE_SECS )); then
    backoff="$next_backoff"
  fi
done
SESSION_SCRIPT

write_root_file /usr/local/bin/kiosk-xinit.sh 0755 <<'XINIT_SCRIPT'
#!/usr/bin/env bash
set -Eeuo pipefail

if [[ -f /etc/default/kiosk ]]; then
  # shellcheck disable=SC1091
  source /etc/default/kiosk
fi

# Give Openbox and Chromium a private D-Bus session even though the kiosk no
# longer depends on a login shell. Re-exec only once inside dbus-run-session.
if [[ -z "${DBUS_SESSION_BUS_ADDRESS:-}" && "${KIOSK_DBUS_WRAPPED:-0}" != "1" ]]; then
  export KIOSK_DBUS_WRAPPED=1
  exec dbus-run-session -- "$0" "$@"
fi

URL="${KIOSK_URL:-https://web.itank.io/login/}"
REFRESH_SECS="${KIOSK_REFRESH_SECS:-0}"
USER_HOME="${KIOSK_HOME:-$HOME}"
RUNTIME_DIR="${KIOSK_RUNTIME_DIR:-/run/kiosk-runtime}"
DISABLE_GPU="${KIOSK_DISABLE_GPU:-0}"
DEVTOOLS_PORT="${KIOSK_DEVTOOLS_PORT:-9222}"
MIN_BACKOFF="${KIOSK_BROWSER_RESTART_MIN_SECS:-5}"
MAX_BACKOFF="${KIOSK_BROWSER_RESTART_MAX_SECS:-60}"
STABLE_SECS="${KIOSK_BROWSER_STABLE_SECS:-300}"
CRASH_WINDOW="${KIOSK_BROWSER_CRASH_WINDOW_SECS:-600}"
CRASH_RESET_THRESHOLD="${KIOSK_BROWSER_CRASH_RESET_THRESHOLD:-6}"
RENDERER_LIMIT="${KIOSK_RENDERER_PROCESS_LIMIT:-2}"
DISK_CACHE_BYTES="${KIOSK_DISK_CACHE_BYTES:-16777216}"
MEDIA_CACHE_BYTES="${KIOSK_MEDIA_CACHE_BYTES:-8388608}"
OOM_SCORE_ADJ="${KIOSK_OOM_SCORE_ADJ:-300}"
BAD_PROFILE_KEEP="${KIOSK_BAD_PROFILE_KEEP:-1}"
LOW_MEMORY_TUNING="${ENABLE_PI3_LOW_MEMORY_TUNING:-1}"
ENABLE_EXTENSION="${ENABLE_CREDENTIAL_EXTENSION:-1}"
ENABLE_HOTKEY="${ENABLE_WIFI_HOTKEY:-1}"
PROFILE_DIR="$USER_HOME/.kiosk-chrome"
CRASH_HISTORY="$RUNTIME_DIR/browser-crash-times"

export HOME="$USER_HOME"
export DISPLAY="${DISPLAY:-:0}"
export XAUTHORITY="${XAUTHORITY:-$USER_HOME/.Xauthority}"
export XDG_RUNTIME_DIR="$RUNTIME_DIR"

install -d -m 0700 "$RUNTIME_DIR" "$RUNTIME_DIR/chrome-cache" "$PROFILE_DIR"
rm -f "$RUNTIME_DIR/chromium.pid"

xset s off || true
xset -dpms || true
xset s noblank || true

OPENBOX_PID=""
UNCLUTTER_PID=""
XBINDKEYS_PID=""
REFRESH_PID=""
BROWSER_PID=""
STOP_REQUESTED=0

openbox-session &
OPENBOX_PID=$!

unclutter -idle 0.5 -root &
UNCLUTTER_PID=$!

if [[ "$ENABLE_HOTKEY" == "1" && -f "$USER_HOME/.xbindkeysrc" ]]; then
  xbindkeys -f "$USER_HOME/.xbindkeysrc" &
  XBINDKEYS_PID=$!
fi

if [[ "$REFRESH_SECS" =~ ^[0-9]+$ ]] && (( REFRESH_SECS > 0 )); then
  (
    while sleep "$REFRESH_SECS"; do
      xdotool search --onlyvisible --class chromium windowactivate --sync key ctrl+r >/dev/null 2>&1 || \
        xdotool key ctrl+r >/dev/null 2>&1 || true
    done
  ) &
  REFRESH_PID=$!
fi

terminate_pid() {
  local pid="${1:-}"
  [[ -n "$pid" ]] || return 0
  kill -TERM "$pid" 2>/dev/null || true
}

pid_is_profile_chromium() {
  local pid="$1" command_line
  [[ -r "/proc/$pid/cmdline" ]] || return 1
  command_line="$(tr '\0' ' ' <"/proc/$pid/cmdline")"
  [[ "$command_line" == *chromium*"--user-data-dir=$PROFILE_DIR"* ]]
}

terminate_orphaned_chromium() {
  mapfile -t pids < <(
    pgrep -u "$(id -u)" -f "(^|/)(chromium|chromium-browser).*--user-data-dir=${PROFILE_DIR}" || true
  )
  (( ${#pids[@]} == 0 )) && return 0

  echo "[kiosk-xinit] Stopping orphaned Chromium process(es): ${pids[*]}"
  kill -TERM "${pids[@]}" 2>/dev/null || true
  remaining=("${pids[@]}")
  for _ in $(seq 1 5); do
    sleep 1
    remaining=()
    for pid in "${pids[@]}"; do
      pid_is_profile_chromium "$pid" && remaining+=("$pid")
    done
    (( ${#remaining[@]} == 0 )) && break
  done
  (( ${#remaining[@]} == 0 )) || kill -KILL "${remaining[@]}" 2>/dev/null || true
}

shutdown_session() {
  STOP_REQUESTED=1
  terminate_pid "$BROWSER_PID"
  terminate_pid "$REFRESH_PID"
  terminate_pid "$XBINDKEYS_PID"
  terminate_pid "$UNCLUTTER_PID"
  terminate_pid "$OPENBOX_PID"
}
trap shutdown_session INT TERM HUP

cleanup_session() {
  shutdown_session
  rm -f "$RUNTIME_DIR/chromium.pid"
}
trap cleanup_session EXIT

CHROMIUM_BIN="$(command -v chromium || command -v chromium-browser || true)"
[[ -n "$CHROMIUM_BIN" ]] || {
  echo "[kiosk-xinit] Chromium executable not found" >&2
  exit 127
}

BASE_ARGS=(
  --kiosk
  --start-fullscreen
  --ozone-platform=x11
  --no-first-run
  --no-default-browser-check
  --noerrdialogs
  --disable-session-crashed-bubble
  --disable-translate
  --disable-breakpad
  --password-store=basic
  --autoplay-policy=no-user-gesture-required
  --overscroll-history-navigation=0
  --process-per-site
  "--renderer-process-limit=$RENDERER_LIMIT"
  --disable-backgrounding-occluded-windows
  --disable-component-update
  --disable-domain-reliability
  --disable-sync
  --remote-debugging-address=127.0.0.1
  "--remote-debugging-port=$DEVTOOLS_PORT"
  "--user-data-dir=$PROFILE_DIR"
  "--disk-cache-dir=$RUNTIME_DIR/chrome-cache"
  "--disk-cache-size=$DISK_CACHE_BYTES"
  "--media-cache-size=$MEDIA_CACHE_BYTES"
  --enable-logging=stderr
  --log-level=1
)

if [[ "$DISABLE_GPU" == "1" ]]; then
  BASE_ARGS+=(--disable-gpu --disable-gpu-compositing)
fi

if [[ "$ENABLE_EXTENSION" == "1" && -f "$USER_HOME/kiosk-ext/manifest.json" ]]; then
  BASE_ARGS+=("--load-extension=$USER_HOME/kiosk-ext")
fi

remove_chromium_singleton_locks() {
  if ! pgrep -u "$(id -u)" -f '(^|/)(chromium|chromium-browser)([[:space:]]|$)' >/dev/null 2>&1; then
    rm -f \
      "$PROFILE_DIR/SingletonCookie" \
      "$PROFILE_DIR/SingletonLock" \
      "$PROFILE_DIR/SingletonSocket"
  fi
}

record_rapid_crash() {
  local now cutoff count temp
  now="$(date +%s)"
  cutoff=$((now - CRASH_WINDOW))
  temp="$(mktemp)"

  if [[ -r "$CRASH_HISTORY" ]]; then
    awk -v cutoff="$cutoff" '$1 ~ /^[0-9]+$/ && $1 >= cutoff { print $1 }' \
      "$CRASH_HISTORY" >"$temp" || true
  fi
  printf '%s\n' "$now" >>"$temp"
  mv "$temp" "$CRASH_HISTORY"
  count="$(wc -l <"$CRASH_HISTORY" | tr -d '[:space:]')"
  printf '%s' "$count"
}

quarantine_broken_profile() {
  local timestamp backup
  timestamp="$(date +%Y%m%d-%H%M%S)"
  backup="${PROFILE_DIR}.bad.${timestamp}"

  terminate_orphaned_chromium
  remove_chromium_singleton_locks

  if [[ -d "$PROFILE_DIR" ]]; then
    echo "[kiosk-xinit] Repeated rapid crashes; moving profile to $backup"
    mv "$PROFILE_DIR" "$backup"
  fi
  install -d -m 0700 "$PROFILE_DIR"
  rm -f "$CRASH_HISTORY"

  # Prevent repeated corrupt-profile recovery from filling the SD card.
  mapfile -t old_profiles < <(
    find "$USER_HOME" -maxdepth 1 -type d -name '.kiosk-chrome.bad.*' -printf '%T@ %p\n' \
      2>/dev/null | sort -nr | awk -v keep="$BAD_PROFILE_KEEP" 'NR > keep {sub(/^[^ ]+ /, ""); print}'
  )
  for old_profile in "${old_profiles[@]}"; do
    rm -rf -- "$old_profile"
  done
}

backoff="$MIN_BACKOFF"

while (( STOP_REQUESTED == 0 )); do
  terminate_orphaned_chromium
  remove_chromium_singleton_locks
  rm -rf "$RUNTIME_DIR/chrome-cache"
  install -d -m 0700 "$RUNTIME_DIR/chrome-cache"

  start_epoch="$(date +%s)"
  echo "[kiosk-xinit] Starting Chromium: $URL"

  set +e
  "$CHROMIUM_BIN" "${BASE_ARGS[@]}" "$URL" &
  BROWSER_PID=$!
  printf '%s\n' "$BROWSER_PID" >"$RUNTIME_DIR/chromium.pid"
  # Under extreme memory pressure, prefer restarting Chromium over losing
  # systemd, SSH or NetworkManager. Children inherit this score.
  if [[ "$LOW_MEMORY_TUNING" == "1" && -w "/proc/$BROWSER_PID/oom_score_adj" ]]; then
    printf '%s\n' "$OOM_SCORE_ADJ" >"/proc/$BROWSER_PID/oom_score_adj" 2>/dev/null || true
  fi
  wait "$BROWSER_PID"
  browser_exit=$?
  set -e

  end_epoch="$(date +%s)"
  runtime=$((end_epoch - start_epoch))
  rm -f "$RUNTIME_DIR/chromium.pid"
  BROWSER_PID=""

  terminate_orphaned_chromium

  if (( STOP_REQUESTED != 0 )); then
    break
  fi

  echo "[kiosk-xinit] Chromium exited with status $browser_exit after ${runtime}s"

  if (( runtime >= STABLE_SECS )); then
    backoff="$MIN_BACKOFF"
    rm -f "$CRASH_HISTORY"
  else
    crash_count="$(record_rapid_crash)"
    echo "[kiosk-xinit] Rapid browser crash count: ${crash_count}/${CRASH_RESET_THRESHOLD}"
    if (( crash_count >= CRASH_RESET_THRESHOLD )); then
      quarantine_broken_profile
      backoff="$MIN_BACKOFF"
    fi

    next_backoff=$((backoff * 2))
    (( next_backoff > MAX_BACKOFF )) && next_backoff="$MAX_BACKOFF"
  fi

  echo "[kiosk-xinit] Restarting Chromium in ${backoff}s"
  sleep "$backoff"

  if (( runtime < STABLE_SECS )); then
    backoff="$next_backoff"
  fi
done

exit 0
XINIT_SCRIPT

write_root_file /usr/local/bin/kiosk-cdp-probe.py 0755 <<'CDP_PROBE'
#!/usr/bin/env python3
"""Verify Chromium renderer health and optional dashboard-data freshness."""

from __future__ import annotations

import json
import os
import socket
import sys
import time
import urllib.request

import websocket


def fail(message: str) -> int:
    print(message, file=sys.stderr)
    return 1


def build_expression() -> str:
    config = {
        "mode": os.environ.get("KIOSK_DATA_FRESHNESS_MODE", "auto"),
        "maxAgeMs": int(os.environ.get("KIOSK_DATA_FRESHNESS_MAX_AGE_SECS", "900")) * 1000,
        "selector": os.environ.get("KIOSK_DATA_FRESHNESS_SELECTOR", ""),
        "attribute": os.environ.get("KIOSK_DATA_FRESHNESS_ATTRIBUTE", "data-epoch"),
        "customExpression": os.environ.get("KIOSK_DATA_FRESHNESS_EXPRESSION", ""),
        "exemptPathRegex": os.environ.get("KIOSK_FRESHNESS_EXEMPT_PATH_REGEX", r"^/login(?:/|$)"),
    }
    encoded = json.dumps(config, separators=(",", ":"))

    return f"""
(() => {{
  const cfg = {encoded};
  const now = Date.now();

  const result = {{
    now,
    ready: document.readyState,
    url: location.href,
    title: document.title,
    visibility: document.visibilityState,
    freshnessMode: cfg.mode,
    freshnessChecked: false,
    fresh: true,
    freshnessAgeMs: 0,
    freshnessSource: "disabled"
  }};

  let exempt = false;
  try {{
    exempt = new RegExp(cfg.exemptPathRegex).test(location.pathname);
  }} catch (_error) {{
    exempt = location.pathname.toLowerCase().startsWith("/login");
  }}

  const navigationEntry = performance.getEntriesByType("navigation")[0];
  const navigationStatus = Number(navigationEntry?.responseStatus || 0);
  result.navigationHttpStatus = navigationStatus;

  if (cfg.mode === "off" || cfg.maxAgeMs <= 0 || exempt || navigationStatus >= 400) {{
    result.freshnessSource = exempt
      ? "exempt-path"
      : navigationStatus >= 400
        ? "http-error-page"
        : "disabled";
    return JSON.stringify(result);
  }}

  const normalizeTimestamp = (value) => {{
    if (typeof value === "number" && Number.isFinite(value)) {{
      return value > 100000000000 ? value : value * 1000;
    }}
    if (typeof value === "string") {{
      const trimmed = value.trim();
      if (/^[0-9]+(?:\\.[0-9]+)?$/.test(trimmed)) {{
        const numeric = Number(trimmed);
        return numeric > 100000000000 ? numeric : numeric * 1000;
      }}
      const parsed = Date.parse(trimmed);
      return Number.isFinite(parsed) ? parsed : null;
    }}
    return null;
  }};

  let lastUpdateMs = null;
  let explicitFresh = null;

  if (cfg.mode === "selector") {{
    result.freshnessChecked = true;
    result.freshnessSource = `selector:${{cfg.selector}}`;
    const element = document.querySelector(cfg.selector);
    if (!element) {{
      result.fresh = false;
      result.freshnessError = "configured freshness selector was not found";
      return JSON.stringify(result);
    }}
    const raw = cfg.attribute
      ? element.getAttribute(cfg.attribute)
      : (element.textContent || "");
    lastUpdateMs = normalizeTimestamp(raw);
    if (lastUpdateMs === null) {{
      result.fresh = false;
      result.freshnessError = "freshness selector did not contain a parseable timestamp";
      return JSON.stringify(result);
    }}
  }} else if (cfg.mode === "expression") {{
    result.freshnessChecked = true;
    result.freshnessSource = "custom-expression";
    let value;
    try {{
      value = Function(`"use strict"; return (${{cfg.customExpression}});`)();
    }} catch (error) {{
      result.fresh = false;
      result.freshnessError = `custom freshness expression failed: ${{error}}`;
      return JSON.stringify(result);
    }}

    if (typeof value === "boolean") {{
      explicitFresh = value;
    }} else if (value && typeof value === "object") {{
      if (typeof value.fresh === "boolean") explicitFresh = value.fresh;
      lastUpdateMs = normalizeTimestamp(
        value.lastUpdateEpochMs ?? value.lastUpdateEpoch ?? value.timestamp ?? value.value
      );
    }} else {{
      lastUpdateMs = normalizeTimestamp(value);
    }}
  }} else {{
    // Generic mode: preserve a heartbeat inside the page. Meaningful DOM text or
    // structure changes and new resource entries update the activity timestamp.
    result.freshnessChecked = true;
    result.freshnessSource = "auto-dom-resource-activity";
    const stateKey = "__kioskFreshnessStateV55";
    let state = window[stateKey];

    if (!state || state.url !== location.href) {{
      try {{ state?.observer?.disconnect(); }} catch (_error) {{}}
      state = {{
        url: location.href,
        lastActivityMs: now,
        resourceCount: performance.getEntriesByType("resource").length,
        observer: null,
        resourceObserver: null
      }};
      const root = document.documentElement;
      if (root && typeof MutationObserver !== "undefined") {{
        state.observer = new MutationObserver((records) => {{
          if (records.some((record) =>
            record.type === "characterData" ||
            (record.type === "childList" && (record.addedNodes.length || record.removedNodes.length))
          )) {{
            state.lastActivityMs = Date.now();
          }}
        }});
        state.observer.observe(root, {{ subtree: true, childList: true, characterData: true }});
      }}
      if (typeof PerformanceObserver !== "undefined") {{
        try {{
          state.resourceObserver = new PerformanceObserver(() => {{
            state.lastActivityMs = Date.now();
          }});
          state.resourceObserver.observe({{ type: "resource", buffered: false }});
        }} catch (_error) {{}}
      }}
      window[stateKey] = state;
    }}

    const resourceCount = performance.getEntriesByType("resource").length;
    if (resourceCount > state.resourceCount) {{
      state.resourceCount = resourceCount;
      state.lastActivityMs = now;
    }}
    lastUpdateMs = state.lastActivityMs;
  }}

  if (explicitFresh !== null) {{
    result.fresh = explicitFresh;
    result.freshnessAgeMs = lastUpdateMs === null ? 0 : Math.max(0, now - lastUpdateMs);
    return JSON.stringify(result);
  }}

  if (lastUpdateMs === null || !Number.isFinite(lastUpdateMs)) {{
    result.fresh = false;
    result.freshnessError = "no valid dashboard update timestamp was available";
    return JSON.stringify(result);
  }}

  result.freshnessAgeMs = Math.max(0, now - lastUpdateMs);
  result.fresh = result.freshnessAgeMs <= cfg.maxAgeMs;
  return JSON.stringify(result);
}})()
""".strip()


def main() -> int:
    port = int(sys.argv[1]) if len(sys.argv) > 1 else 9222
    timeout = float(sys.argv[2]) if len(sys.argv) > 2 else 8.0
    endpoint = f"http://127.0.0.1:{port}/json/list"

    try:
        with urllib.request.urlopen(endpoint, timeout=timeout) as response:
            targets = json.load(response)
    except Exception as exc:  # noqa: BLE001 - a health probe must report all failures
        return fail(f"CDP target query failed: {exc}")

    pages = [
        target
        for target in targets
        if target.get("type") == "page" and target.get("webSocketDebuggerUrl")
    ]
    if not pages:
        return fail("CDP has no page target")

    pages.sort(
        key=lambda item: str(item.get("url", "")).startswith(
            ("chrome-extension://", "devtools://")
        )
    )
    target = pages[0]
    ws = None

    try:
        ws = websocket.create_connection(
            target["webSocketDebuggerUrl"],
            timeout=timeout,
            suppress_origin=True,
        )
        ws.settimeout(timeout)
        request_id = int(time.time() * 1000) & 0x7FFFFFFF
        ws.send(
            json.dumps(
                {
                    "id": request_id,
                    "method": "Runtime.evaluate",
                    "params": {
                        "expression": build_expression(),
                        "returnByValue": True,
                    },
                }
            )
        )

        deadline = time.monotonic() + timeout
        while time.monotonic() < deadline:
            payload = json.loads(ws.recv())
            if payload.get("id") != request_id:
                continue
            if "error" in payload:
                return fail(f"CDP Runtime.evaluate error: {payload['error']}")
            result = payload.get("result", {})
            if result.get("exceptionDetails"):
                return fail(f"Renderer evaluation exception: {result['exceptionDetails']}")
            value = result.get("result", {}).get("value")
            if not isinstance(value, str) or not value:
                return fail("Renderer returned no value")
            decoded = json.loads(value)
            if not isinstance(decoded.get("now"), (int, float)):
                return fail("Renderer heartbeat is invalid")
            if decoded.get("fresh") is False:
                age_ms = decoded.get("freshnessAgeMs", "unknown")
                reason = decoded.get("freshnessError", "dashboard data/activity is stale")
                return fail(f"Dashboard freshness failed: {reason}; age_ms={age_ms}")
            print(json.dumps(decoded, separators=(",", ":")))
            return 0
        return fail("Renderer did not answer before timeout")
    except (OSError, socket.timeout, websocket.WebSocketException, ValueError, json.JSONDecodeError) as exc:
        return fail(f"CDP renderer probe failed: {exc}")
    finally:
        if ws is not None:
            try:
                ws.close()
            except Exception:
                pass


if __name__ == "__main__":
    raise SystemExit(main())
CDP_PROBE

write_root_file /usr/local/bin/kiosk-healthcheck.sh 0755 <<'HEALTH_SCRIPT'
#!/usr/bin/env bash
set -Eeuo pipefail

if [[ -f /etc/default/kiosk ]]; then
  # shellcheck disable=SC1091
  source /etc/default/kiosk
fi

KIOSK_USER="${KIOSK_USER:?KIOSK_USER is not configured}"
KIOSK_GROUP="${KIOSK_GROUP:?KIOSK_GROUP is not configured}"
KIOSK_HOME="${KIOSK_HOME:?KIOSK_HOME is not configured}"
RUNTIME_DIR="${KIOSK_RUNTIME_DIR:-/run/kiosk-runtime}"
DEVTOOLS_PORT="${KIOSK_DEVTOOLS_PORT:-9222}"
FAIL_THRESHOLD="${KIOSK_HEALTH_FAIL_THRESHOLD:-3}"
RESTART_WAIT="${KIOSK_HEALTH_RESTART_WAIT_SECS:-30}"
RENDERER_TIMEOUT="${KIOSK_RENDERER_PROBE_TIMEOUT_SECS:-8}"
EMERGENCY_AFTER="${KIOSK_EMERGENCY_REBOOT_AFTER_SECS:-600}"
REBOOT_COOLDOWN="${KIOSK_EMERGENCY_REBOOT_COOLDOWN_SECS:-21600}"
ENABLE_EMERGENCY_REBOOT="${ENABLE_EMERGENCY_REBOOT:-1}"
LOW_MEMORY_MB="${KIOSK_LOW_MEMORY_MB:-96}"
LOW_SWAP_MB="${KIOSK_LOW_SWAP_MB:-64}"
MEMORY_FAIL_THRESHOLD="${KIOSK_MEMORY_FAIL_THRESHOLD:-3}"
LOW_MEMORY_TUNING="${ENABLE_PI3_LOW_MEMORY_TUNING:-1}"
export KIOSK_DATA_FRESHNESS_MODE="${KIOSK_DATA_FRESHNESS_MODE:-auto}"
export KIOSK_DATA_FRESHNESS_MAX_AGE_SECS="${KIOSK_DATA_FRESHNESS_MAX_AGE_SECS:-900}"
export KIOSK_DATA_FRESHNESS_SELECTOR="${KIOSK_DATA_FRESHNESS_SELECTOR:-}"
export KIOSK_DATA_FRESHNESS_ATTRIBUTE="${KIOSK_DATA_FRESHNESS_ATTRIBUTE:-data-epoch}"
export KIOSK_DATA_FRESHNESS_EXPRESSION="${KIOSK_DATA_FRESHNESS_EXPRESSION:-}"
export KIOSK_FRESHNESS_EXEMPT_PATH_REGEX="${KIOSK_FRESHNESS_EXEMPT_PATH_REGEX:-^/login(?:/|$)}"
STATE_DIR="/run/kiosk-health"
PERSIST_DIR="/var/lib/kiosk-watchdog"
BROWSER_FAIL_FILE="$STATE_DIR/browser-failures"
LOCAL_FAIL_FILE="$STATE_DIR/local-failures"
MEMORY_FAIL_FILE="$STATE_DIR/memory-failures"
PID_FILE="$RUNTIME_DIR/chromium.pid"
RENDERER_ERROR_FILE="$STATE_DIR/renderer-last-error"
FIRST_LOCAL_FAILURE_FILE="$PERSIST_DIR/first-local-failure"
LAST_REBOOT_FILE="$PERSIST_DIR/last-emergency-reboot"

install -d -m 0755 "$STATE_DIR" "$PERSIST_DIR"
exec 9>"$STATE_DIR/healthcheck.lock"
flock -n 9 || exit 0

log() {
  echo "[kiosk-health] $*"
}

read_counter() {
  local file="$1" value="0"
  if [[ -r "$file" ]]; then
    value="$(tr -d '[:space:]' <"$file" 2>/dev/null || printf '0')"
  fi
  [[ "$value" =~ ^[0-9]+$ ]] || value="0"
  printf '%s' "$value"
}

write_counter() {
  printf '%s\n' "$2" >"$1"
}

increment_counter() {
  local file="$1" value
  value=$(( $(read_counter "$file") + 1 ))
  write_counter "$file" "$value"
  printf '%s' "$value"
}

browser_pid() {
  local pid=""
  [[ -r "$PID_FILE" ]] || return 1
  pid="$(tr -d '[:space:]' <"$PID_FILE" 2>/dev/null || true)"
  [[ "$pid" =~ ^[0-9]+$ ]] || return 1
  [[ -d "/proc/$pid" ]] || return 1
  [[ "$(stat -c '%U' "/proc/$pid" 2>/dev/null || true)" == "$KIOSK_USER" ]] || return 1
  tr '\0' ' ' <"/proc/$pid/cmdline" | grep -q -- "--user-data-dir=$KIOSK_HOME/.kiosk-chrome" || return 1
  printf '%s' "$pid"
}

xorg_pid() {
  pgrep -o -f '(^|/)(Xorg|X)[[:space:]]+:0([[:space:]]|$)'
}

x_healthy() {
  local pid
  pid="$(xorg_pid 2>/dev/null || true)"
  [[ -n "$pid" && -S /tmp/.X11-unix/X0 ]] || return 1

  runuser -u "$KIOSK_USER" -- env \
    HOME="$KIOSK_HOME" DISPLAY=:0 XAUTHORITY="$KIOSK_HOME/.Xauthority" \
    timeout 6s xdpyinfo >/dev/null 2>&1
}

visible_browser_window() {
  runuser -u "$KIOSK_USER" -- env \
    HOME="$KIOSK_HOME" DISPLAY=:0 XAUTHORITY="$KIOSK_HOME/.Xauthority" \
    timeout 6s xdotool search --onlyvisible --class chromium \
    >/dev/null 2>&1
}

renderer_healthy() {
  local output=""
  if output="$(timeout "$((RENDERER_TIMEOUT + 3))"s \
      /usr/local/bin/kiosk-cdp-probe.py "$DEVTOOLS_PORT" "$RENDERER_TIMEOUT" 2>&1)"; then
    rm -f "$RENDERER_ERROR_FILE"
    return 0
  fi
  printf '%s\n' "${output:-renderer probe timed out or failed}" >"$RENDERER_ERROR_FILE"
  return 1
}

browser_healthy() {
  browser_pid >/dev/null && visible_browser_window && renderer_healthy
}

memory_pressure_high() {
  local mem_available_kb swap_total_kb swap_free_kb
  [[ "$LOW_MEMORY_TUNING" == "1" ]] || return 1

  mem_available_kb="$(awk '/^MemAvailable:/ {print $2}' /proc/meminfo)"
  swap_total_kb="$(awk '/^SwapTotal:/ {print $2}' /proc/meminfo)"
  swap_free_kb="$(awk '/^SwapFree:/ {print $2}' /proc/meminfo)"

  [[ "$mem_available_kb" =~ ^[0-9]+$ ]] || return 1
  [[ "$swap_total_kb" =~ ^[0-9]+$ ]] || swap_total_kb=0
  [[ "$swap_free_kb" =~ ^[0-9]+$ ]] || swap_free_kb=0

  (( mem_available_kb < LOW_MEMORY_MB * 1024 )) || return 1
  (( swap_total_kb == 0 || swap_free_kb < LOW_SWAP_MB * 1024 ))
}

restore_display_settings() {
  runuser -u "$KIOSK_USER" -- env \
    HOME="$KIOSK_HOME" DISPLAY=:0 XAUTHORITY="$KIOSK_HOME/.Xauthority" \
    sh -c 'xset s off; xset -dpms; xset s noblank' >/dev/null 2>&1 || true
}

mark_local_failure() {
  local now
  now="$(date +%s)"
  if [[ ! -s "$FIRST_LOCAL_FAILURE_FILE" ]]; then
    printf '%s\n' "$now" >"$FIRST_LOCAL_FAILURE_FILE"
  fi
}

clear_local_failure() {
  rm -f "$FIRST_LOCAL_FAILURE_FILE"
  write_counter "$LOCAL_FAIL_FILE" 0
}

maybe_emergency_reboot() {
  local now first last=0
  [[ "$ENABLE_EMERGENCY_REBOOT" == "1" ]] || return 0
  [[ -r "$FIRST_LOCAL_FAILURE_FILE" ]] || return 0

  now="$(date +%s)"
  first="$(read_counter "$FIRST_LOCAL_FAILURE_FILE")"
  [[ -r "$LAST_REBOOT_FILE" ]] && last="$(read_counter "$LAST_REBOOT_FILE")"

  (( first > 0 )) || return 0
  (( now - first >= EMERGENCY_AFTER )) || return 0
  (( now - last >= REBOOT_COOLDOWN )) || {
    log "Emergency reboot suppressed by cooldown"
    return 0
  }

  printf '%s\n' "$now" >"$LAST_REBOOT_FILE"
  log "Local kiosk remained unhealthy for at least ${EMERGENCY_AFTER}s; rebooting Pi"
  sync
  systemctl reboot --no-wall
  exit 0
}

wait_for_local_health() {
  local max_wait="$1"
  for ((second = 1; second <= max_wait; second++)); do
    sleep 1
    if x_healthy && browser_healthy; then
      return 0
    fi
  done
  return 1
}

restart_whole_session() {
  local reason="$1"
  log "Restarting complete kiosk session: $reason"
  mark_local_failure
  write_counter "$BROWSER_FAIL_FILE" 0
  write_counter "$LOCAL_FAIL_FILE" 0
  systemctl restart kiosk.service

  if wait_for_local_health 120; then
    log "Complete kiosk session recovered"
    clear_local_failure
    return 0
  fi

  log "Complete kiosk session did not recover"
  maybe_emergency_reboot
  return 1
}

# Board-specific Xorg selection is repaired by kiosk-preflight.sh before every
# kiosk.service start, including after moving an image between Pi 5 and Pi 3.

if ! systemctl is-active --quiet kiosk.service; then
  log "kiosk.service is not active; starting it"
  mark_local_failure
  systemctl start kiosk.service
  if wait_for_local_health 120; then
    clear_local_failure
  else
    maybe_emergency_reboot
  fi
  exit 0
fi

if ! x_healthy; then
  failures="$(increment_counter "$LOCAL_FAIL_FILE")"
  mark_local_failure
  log "Xorg/display health check failed ${failures}/${FAIL_THRESHOLD}"
  if (( failures >= FAIL_THRESHOLD )); then
    restart_whole_session "Xorg :0 is absent or not answering" || true
  else
    maybe_emergency_reboot
  fi
  exit 0
fi

pid="$(browser_pid || true)"
if [[ -z "$pid" ]] || ! browser_healthy; then
  failures="$(increment_counter "$BROWSER_FAIL_FILE")"
  renderer_reason="$(tail -n 1 "$RENDERER_ERROR_FILE" 2>/dev/null || true)"
  log "Chromium window/renderer/freshness health check failed ${failures}/${FAIL_THRESHOLD}${renderer_reason:+: $renderer_reason}"

  if (( failures < FAIL_THRESHOLD )); then
    exit 0
  fi

  if [[ -n "$pid" ]]; then
    log "Terminating unhealthy Chromium PID $pid"
    kill -TERM "$pid" 2>/dev/null || true
  else
    log "Chromium supervisor PID is missing"
  fi

  write_counter "$BROWSER_FAIL_FILE" 0
  for ((second = 1; second <= RESTART_WAIT; second++)); do
    sleep 1
    if browser_healthy; then
      log "Chromium recovered without restarting X"
      clear_local_failure
      exit 0
    fi
  done

  restart_whole_session "Chromium did not recover after browser-only restart" || true
  exit 0
fi

write_counter "$BROWSER_FAIL_FILE" 0

if memory_pressure_high; then
  memory_failures="$(increment_counter "$MEMORY_FAIL_FILE")"
  available_mb="$(( $(awk '/^MemAvailable:/ {print $2}' /proc/meminfo) / 1024 ))"
  log "Sustained low memory: available=${available_mb}MB (${memory_failures}/${MEMORY_FAIL_THRESHOLD})"

  if (( memory_failures >= MEMORY_FAIL_THRESHOLD )); then
    log "Recycling Chromium before the kernel OOM killer is required"
    write_counter "$MEMORY_FAIL_FILE" 0
    kill -TERM "$pid" 2>/dev/null || true
    for ((second = 1; second <= RESTART_WAIT + 30; second++)); do
      sleep 1
      if browser_healthy; then
        log "Chromium recovered after low-memory recycle"
        clear_local_failure
        exit 0
      fi
    done
    restart_whole_session "Chromium did not recover after low-memory recycle" || true
  fi
  exit 0
fi

write_counter "$MEMORY_FAIL_FILE" 0
clear_local_failure
restore_display_settings
log "Xorg, Chromium window, renderer, dashboard freshness and memory are healthy (PID $pid)"
HEALTH_SCRIPT

write_root_file /usr/local/bin/kiosk-guard.sh 0755 <<'GUARD_SCRIPT'
#!/usr/bin/env bash
set -u -o pipefail

if [[ -f /etc/default/kiosk ]]; then
  # shellcheck disable=SC1091
  source /etc/default/kiosk
fi

KIOSK_USER="${KIOSK_USER:-pi}"
KIOSK_HOME="${KIOSK_HOME:-/home/$KIOSK_USER}"
KIOSK_URL="${KIOSK_URL:-https://web.itank.io/login/}"
ENABLE_NETWORK_GUARD="${ENABLE_NETWORK_GUARD:-1}"
INTERVAL="${KEEPALIVE_INTERVAL_SECS:-20}"
FAIL_THRESHOLD="${KEEPALIVE_FAIL_THRESHOLD:-6}"
COOLDOWN="${KEEPALIVE_ACTION_COOLDOWN_SECS:-300}"
HTTP_TIMEOUT="${KIOSK_HTTP_TIMEOUT_SECS:-10}"
NETWORK_REBOOT_AFTER="${NETWORK_FAILURE_REBOOT_AFTER_SECS:-3600}"
NETWORK_REBOOT_COOLDOWN="${NETWORK_FAILURE_REBOOT_COOLDOWN_SECS:-21600}"
MAX_RECOVERY_ATTEMPTS="${NETWORK_RECOVERY_MAX_ATTEMPTS:-4}"
STATE_DIR="/var/lib/kiosk-watchdog"
APP_OUTAGE_FILE="$STATE_DIR/dashboard-outage"
FIRST_TRANSPORT_FAILURE_FILE="$STATE_DIR/first-network-transport-failure"
RECOVERY_ATTEMPTS_FILE="$STATE_DIR/network-recovery-attempts"
LAST_NETWORK_REBOOT_FILE="$STATE_DIR/last-network-reboot"
LAST_HTTP_CODE="000"
LAST_CURL_EXIT=0

install -d -m 0755 "$STATE_DIR"

log() {
  echo "[$(date -Is)] $*"
}

notify_systemd() {
  if command -v systemd-notify >/dev/null 2>&1; then
    systemd-notify "$@" || true
  fi
}

read_uint_file() {
  local file="$1" value="0"
  if [[ -r "$file" ]]; then
    value="$(tr -d '[:space:]' <"$file" 2>/dev/null || printf '0')"
  fi
  [[ "$value" =~ ^[0-9]+$ ]] || value=0
  printf '%s' "$value"
}

write_uint_file() {
  printf '%s\n' "$2" >"$1"
}

probe_dashboard() {
  local output=""
  output="$(curl --silent --show-error --location --output /dev/null \
    --connect-timeout 4 --max-time "$HTTP_TIMEOUT" \
    --write-out '%{http_code}' "$KIOSK_URL" 2>/dev/null)"
  LAST_CURL_EXIT=$?
  LAST_HTTP_CODE="${output:-000}"

  # 2xx/3xx are normal. 401/403 also prove that the intended application server
  # is alive even when it requires authentication outside the browser session.
  [[ "$LAST_HTTP_CODE" =~ ^[23][0-9][0-9]$ || "$LAST_HTTP_CODE" == "401" || "$LAST_HTTP_CODE" == "403" ]]
}

http_response_proves_transport() {
  [[ "$LAST_HTTP_CODE" =~ ^[1-5][0-9][0-9]$ ]]
}

network_manager_full_connectivity() {
  local state=""
  state="$(nmcli -t -f CONNECTIVITY general 2>/dev/null || true)"
  [[ "$state" == "full" ]]
}

basic_network_transport_ok() {
  local host=""
  host="$(printf '%s' "$KIOSK_URL" | sed -E 's#^[a-zA-Z]+://([^/:]+).*#\1#')"
  ip route get 1.1.1.1 >/dev/null 2>&1 || return 1

  # Either public IP reachability or successful DNS resolution is enough to
  # show that restarting NetworkManager/rebooting the Pi is not justified.
  timeout 5s ping -c 1 -W 3 1.1.1.1 >/dev/null 2>&1 ||
    timeout 8s getent ahosts "$host" >/dev/null 2>&1
}

transport_healthy() {
  http_response_proves_transport || network_manager_full_connectivity || basic_network_transport_ok
}

recover_network() {
  log "Recovering NetworkManager"
  systemctl restart NetworkManager.service || true
  sleep 6
  nmcli networking on >/dev/null 2>&1 || true
  nmcli radio wifi on >/dev/null 2>&1 || true
}

refresh_browser() {
  runuser -u "$KIOSK_USER" -- env \
    HOME="$KIOSK_HOME" \
    DISPLAY=:0 \
    XAUTHORITY="$KIOSK_HOME/.Xauthority" \
    sh -c 'xdotool search --onlyvisible --class chromium windowactivate --sync key ctrl+r >/dev/null 2>&1 || xdotool key ctrl+r >/dev/null 2>&1 || true'
}

mark_application_outage() {
  [[ -e "$APP_OUTAGE_FILE" ]] || date +%s >"$APP_OUTAGE_FILE"
}

clear_transport_failure() {
  rm -f "$FIRST_TRANSPORT_FAILURE_FILE" "$RECOVERY_ATTEMPTS_FILE"
}

mark_transport_failure() {
  local now="$1"
  [[ -s "$FIRST_TRANSPORT_FAILURE_FILE" ]] || write_uint_file "$FIRST_TRANSPORT_FAILURE_FILE" "$now"
}

increment_recovery_attempts() {
  local attempts
  attempts=$(( $(read_uint_file "$RECOVERY_ATTEMPTS_FILE") + 1 ))
  write_uint_file "$RECOVERY_ATTEMPTS_FILE" "$attempts"
  printf '%s' "$attempts"
}

maybe_reboot_for_persistent_transport_failure() {
  local now="$1" first attempts last_reboot
  first="$(read_uint_file "$FIRST_TRANSPORT_FAILURE_FILE")"
  attempts="$(read_uint_file "$RECOVERY_ATTEMPTS_FILE")"
  last_reboot="$(read_uint_file "$LAST_NETWORK_REBOOT_FILE")"

  (( first > 0 )) || return 0
  (( attempts >= MAX_RECOVERY_ATTEMPTS )) || return 0
  (( now - first >= NETWORK_REBOOT_AFTER )) || return 0
  if (( now - last_reboot < NETWORK_REBOOT_COOLDOWN )); then
    log "Persistent-network reboot suppressed by cooldown"
    return 0
  fi

  write_uint_file "$LAST_NETWORK_REBOOT_FILE" "$now"
  log "Confirmed network transport failure persisted for at least ${NETWORK_REBOOT_AFTER}s after ${attempts} NetworkManager recoveries; rebooting Pi"
  notify_systemd WATCHDOG=1 STATUS="Rebooting after persistent local network transport failure"
  sync
  systemctl reboot --no-wall
  exit 0
}

notify_systemd --ready --status="Kiosk network guard started"
log "Network guard started: URL=$KIOSK_URL interval=${INTERVAL}s"

failures=0
last_recovery_epoch=0

while true; do
  now="$(date +%s)"

  if [[ "$ENABLE_NETWORK_GUARD" != "1" ]]; then
    notify_systemd WATCHDOG=1 STATUS="Network guard disabled; process alive"
    sleep "$INTERVAL"
    continue
  fi

  if probe_dashboard; then
    if [[ -e "$APP_OUTAGE_FILE" ]]; then
      log "Dashboard recovered (HTTP $LAST_HTTP_CODE); refreshing Chromium"
      refresh_browser
      rm -f "$APP_OUTAGE_FILE"
    fi
    clear_transport_failure
    failures=0
    notify_systemd WATCHDOG=1 STATUS="Dashboard reachable (HTTP $LAST_HTTP_CODE)"
    sleep "$INTERVAL"
    continue
  fi

  failures=$((failures + 1))
  mark_application_outage
  log "Dashboard check failed ${failures}/${FAIL_THRESHOLD}: HTTP=${LAST_HTTP_CODE} curl_exit=${LAST_CURL_EXIT}"
  notify_systemd WATCHDOG=1 STATUS="Dashboard check failed $failures/$FAIL_THRESHOLD (HTTP $LAST_HTTP_CODE)"

  if (( failures >= FAIL_THRESHOLD )); then
    if transport_healthy; then
      # Includes HTTP 4xx/5xx: the network path is alive, but the application is
      # unavailable or misconfigured. Never restart networking for this case.
      clear_transport_failure
      log "General network transport is healthy; dashboard application is unavailable (HTTP $LAST_HTTP_CODE). NetworkManager will not be restarted."
    else
      mark_transport_failure "$now"
      if (( now - last_recovery_epoch >= COOLDOWN )); then
        last_recovery_epoch="$now"
        attempts="$(increment_recovery_attempts)"
        log "Confirmed local transport failure; NetworkManager recovery attempt ${attempts}/${MAX_RECOVERY_ATTEMPTS}"
        recover_network

        # Re-probe after recovery. Never reboot when NetworkManager has already
        # restored transport but the remote dashboard is still returning an
        # application error such as HTTP 503.
        if probe_dashboard; then
          log "Network and dashboard recovered after NetworkManager restart (HTTP $LAST_HTTP_CODE)"
          refresh_browser
          rm -f "$APP_OUTAGE_FILE"
          clear_transport_failure
        elif transport_healthy; then
          log "Network transport recovered; dashboard application remains unavailable (HTTP $LAST_HTTP_CODE)"
          clear_transport_failure
        else
          maybe_reboot_for_persistent_transport_failure "$(date +%s)"
        fi
      else
        log "Network recovery skipped because cooldown is active"
        maybe_reboot_for_persistent_transport_failure "$now"
      fi
    fi
    failures=0
  fi

  sleep "$INTERVAL"
done
GUARD_SCRIPT

write_root_file /usr/local/bin/kiosk-maintenance.sh 0755 <<'MAINTENANCE_SCRIPT'
#!/usr/bin/env bash
set -Eeuo pipefail

if [[ -f /etc/default/kiosk ]]; then
  # shellcheck disable=SC1091
  source /etc/default/kiosk
fi

KIOSK_HOME="${KIOSK_HOME:-/home/pi}"

root_usage="$(df -P / | awk 'NR==2 {gsub(/%/, "", $5); print $5}')"
echo "[kiosk-maintenance] Root filesystem usage: ${root_usage}%"
if command -v vcgencmd >/dev/null 2>&1; then
  echo "[kiosk-maintenance] $(vcgencmd get_throttled 2>/dev/null || true)"
  echo "[kiosk-maintenance] $(vcgencmd measure_temp 2>/dev/null || true)"
fi

echo "[kiosk-maintenance] Memory: $(free -m | awk '/^Mem:/ {printf "available=%sMB total=%sMB", $7, $2}')"
echo "[kiosk-maintenance] Swap: $(free -m | awk '/^Swap:/ {printf "used=%sMB total=%sMB", $3, $2}')"

# Remove only old crash-report artifacts, never active profile databases.
if [[ -d "$KIOSK_HOME/.kiosk-chrome/Crash Reports" ]]; then
  find "$KIOSK_HOME/.kiosk-chrome/Crash Reports" -type f -mtime +7 -delete || true
fi

# A profile is quarantined only after repeated rapid Chromium crashes. Keep the
# newest configured copy for diagnosis and delete older copies after seven days.
find "$KIOSK_HOME" -maxdepth 1 -type d -name '.kiosk-chrome.bad.*' -mtime +7 \
  -exec rm -rf -- {} + 2>/dev/null || true
mapfile -t old_profiles < <(
  find "$KIOSK_HOME" -maxdepth 1 -type d -name '.kiosk-chrome.bad.*' -printf '%T@ %p\n' \
    2>/dev/null | sort -nr | awk -v keep="${KIOSK_BAD_PROFILE_KEEP:-1}" 'NR > keep {sub(/^[^ ]+ /, ""); print}'
)
for profile in "${old_profiles[@]}"; do
  rm -rf -- "$profile"
done

journalctl --vacuum-size="${JOURNAL_MAX_USE:-100M}" >/dev/null 2>&1 || true

if (( root_usage >= 90 )); then
  echo "[kiosk-maintenance] WARNING: root filesystem is at least 90% full" >&2
fi
MAINTENANCE_SCRIPT

write_root_file /usr/local/bin/kiosk-browser-recycle.sh 0755 <<'RECYCLE_SCRIPT'
#!/usr/bin/env bash
set -Eeuo pipefail

if [[ -f /etc/default/kiosk ]]; then
  # shellcheck disable=SC1091
  source /etc/default/kiosk
fi

KIOSK_USER="${KIOSK_USER:?KIOSK_USER is not configured}"
KIOSK_UID="${KIOSK_UID:?KIOSK_UID is not configured}"
KIOSK_HOME="${KIOSK_HOME:?KIOSK_HOME is not configured}"
RUNTIME_DIR="${KIOSK_RUNTIME_DIR:-/run/kiosk-runtime}"
PID_FILE="$RUNTIME_DIR/chromium.pid"
pid=""

pid_is_kiosk_chromium() {
  local candidate="$1" owner_uid command_line
  [[ "$candidate" =~ ^[0-9]+$ && -r "/proc/$candidate/cmdline" ]] || return 1
  owner_uid="$(stat -c '%u' "/proc/$candidate" 2>/dev/null || true)"
  [[ "$owner_uid" == "$KIOSK_UID" ]] || return 1
  command_line="$(tr '\0' ' ' <"/proc/$candidate/cmdline")"
  [[ "$command_line" == *chromium*"--user-data-dir=$KIOSK_HOME/.kiosk-chrome"* ]]
}

if [[ -r "$PID_FILE" ]]; then
  pid="$(tr -d '[:space:]' <"$PID_FILE" 2>/dev/null || true)"
fi

if pid_is_kiosk_chromium "$pid"; then
  echo "[kiosk-recycle] Performing controlled browser-only recycle for validated PID $pid"
  kill -TERM "$pid" 2>/dev/null || true
else
  echo "[kiosk-recycle] Chromium PID is missing, stale or does not belong to the kiosk; restarting complete kiosk session"
  systemctl restart kiosk.service
fi
RECYCLE_SCRIPT

# Syntax-check every generated shell program before changing the boot path.
for generated_script in \
  /usr/local/bin/kiosk-wifi.sh \
  /usr/local/bin/kiosk-preflight.sh \
  /usr/local/bin/kiosk-session.sh \
  /usr/local/bin/kiosk-xinit.sh \
  /usr/local/bin/kiosk-healthcheck.sh \
  /usr/local/bin/kiosk-guard.sh \
  /usr/local/bin/kiosk-maintenance.sh \
  /usr/local/bin/kiosk-browser-recycle.sh; do
  bash -n "$generated_script"
done
python3 -m py_compile /usr/local/bin/kiosk-cdp-probe.py
rm -rf /usr/local/bin/__pycache__

# V5.5.2 intentionally creates no login-shell launcher. kiosk.service runs the
# session executable directly as the configured non-root user.

# ----------------------------- systemd units ----------------------------------
write_root_file /etc/systemd/system/kiosk.service 0644 <<EOF
[Unit]
Description=24x7 Chromium kiosk directly managed on tty1
After=systemd-user-sessions.service systemd-logind.service NetworkManager.service
Wants=NetworkManager.service
Conflicts=getty@tty1.service display-manager.service
StartLimitIntervalSec=0

[Service]
Type=simple
User=${TARGET_USER}
Group=${TARGET_GROUP}
WorkingDirectory=${TARGET_HOME}
Environment=HOME=${TARGET_HOME}
Environment=USER=${TARGET_USER}
Environment=LOGNAME=${TARGET_USER}
Environment=SHELL=/bin/bash
Environment=TERM=linux
ExecStartPre=+/usr/local/bin/kiosk-preflight.sh
ExecStart=/usr/local/bin/kiosk-session.sh
Restart=always
RestartSec=10s
TTYPath=/dev/tty1
TTYReset=yes
TTYVHangup=yes
TTYVTDisallocate=yes
StandardInput=tty-force
StandardOutput=tty
StandardError=journal
UtmpIdentifier=tty1
UtmpMode=user
KillMode=control-group
SendSIGHUP=yes
TimeoutStopSec=45s
TasksMax=512
LimitNOFILE=16384

[Install]
WantedBy=multi-user.target
EOF

write_root_file /etc/systemd/system/kiosk-healthcheck.service 0644 <<'EOF'
[Unit]
Description=Chromium kiosk X11/window/renderer health check
After=kiosk.service

[Service]
Type=oneshot
ExecStart=/usr/local/bin/kiosk-healthcheck.sh
TimeoutStartSec=240s
Nice=10
EOF

write_root_file /etc/systemd/system/kiosk-healthcheck.timer 0644 <<EOF
[Unit]
Description=Run kiosk local health check every ${KIOSK_HEALTH_INTERVAL_SECS} seconds

[Timer]
OnBootSec=90s
OnUnitInactiveSec=${KIOSK_HEALTH_INTERVAL_SECS}s
AccuracySec=5s
Unit=kiosk-healthcheck.service

[Install]
WantedBy=timers.target
EOF

if [[ "$ENABLE_SOFTWARE_WATCHDOG" == "1" ]]; then
  SOFTWARE_WATCHDOG_LINE="WatchdogSec=${SOFTWARE_WATCHDOG_SECS}"
else
  SOFTWARE_WATCHDOG_LINE=""
fi

write_root_file /etc/systemd/system/kiosk-guard.service 0644 <<EOF
[Unit]
Description=Kiosk dashboard connectivity guard
After=NetworkManager.service multi-user.target
Wants=NetworkManager.service
StartLimitIntervalSec=0

[Service]
Type=notify
NotifyAccess=all
ExecStart=/usr/local/bin/kiosk-guard.sh
Restart=always
RestartSec=10s
TimeoutStartSec=30s
${SOFTWARE_WATCHDOG_LINE}

[Install]
WantedBy=multi-user.target
EOF

write_root_file /etc/systemd/system/kiosk-maintenance.service 0644 <<'EOF'
[Unit]
Description=Low-write kiosk maintenance

[Service]
Type=oneshot
ExecStart=/usr/local/bin/kiosk-maintenance.sh
Nice=15
EOF

write_root_file /etc/systemd/system/kiosk-maintenance.timer 0644 <<'EOF'
[Unit]
Description=Run kiosk maintenance daily

[Timer]
OnBootSec=20min
OnUnitInactiveSec=1d
RandomizedDelaySec=20min
Persistent=true
Unit=kiosk-maintenance.service

[Install]
WantedBy=timers.target
EOF

write_root_file /etc/systemd/system/kiosk-browser-recycle.service 0644 <<'EOF'
[Unit]
Description=Controlled Chromium kiosk browser recycle
After=kiosk.service

[Service]
Type=oneshot
ExecStart=/usr/local/bin/kiosk-browser-recycle.sh
TimeoutStartSec=30s
EOF

write_root_file /etc/systemd/system/kiosk-browser-recycle.timer 0644 <<EOF
[Unit]
Description=Recycle only Chromium every ${KIOSK_BROWSER_RECYCLE_HOURS} hours

[Timer]
OnBootSec=${KIOSK_BROWSER_RECYCLE_HOURS}h
OnUnitInactiveSec=${KIOSK_BROWSER_RECYCLE_HOURS}h
AccuracySec=10min
Persistent=false
Unit=kiosk-browser-recycle.service

[Install]
WantedBy=timers.target
EOF

# Validate unit-file syntax before taking over tty1.
systemd-analyze verify \
  /etc/systemd/system/kiosk.service \
  /etc/systemd/system/kiosk-healthcheck.service \
  /etc/systemd/system/kiosk-healthcheck.timer \
  /etc/systemd/system/kiosk-guard.service \
  /etc/systemd/system/kiosk-maintenance.service \
  /etc/systemd/system/kiosk-maintenance.timer \
  /etc/systemd/system/kiosk-browser-recycle.service \
  /etc/systemd/system/kiosk-browser-recycle.timer

# Hardware watchdog for complete kernel/system lockups. It is separate from the
# browser health monitor and does not reboot merely because the website is down.
if [[ "$ENABLE_HARDWARE_WATCHDOG" == "1" ]]; then
  write_root_file /etc/modules-load.d/kiosk-watchdog.conf 0644 <<'WATCHDOG_MODULE'
bcm2835_wdt
WATCHDOG_MODULE

  write_root_file /etc/systemd/system.conf.d/20-kiosk-watchdog.conf 0644 <<EOF
[Manager]
RuntimeWatchdogSec=${HARDWARE_WATCHDOG_SECS}
RebootWatchdogSec=${REBOOT_WATCHDOG_SECS}
EOF

  # Use the kernel/systemd runtime watchdog on all boards. Avoid writing a
  # Pi 4/5 bootloader handoff option so a Pi 5 test image remains safe to clone
  # directly onto a Pi 3 B+ field unit.

  modprobe bcm2835_wdt >/dev/null 2>&1 || true
else
  rm -f /etc/modules-load.d/kiosk-watchdog.conf
  rm -f /etc/systemd/system.conf.d/20-kiosk-watchdog.conf
fi

# Disable the old forced-reboot design. A healthy kiosk should continue running;
# targeted supervisors handle browser, X, network and kernel failures separately.
systemctl disable --now kiosk-auto-reboot.timer >/dev/null 2>&1 || true
systemctl disable --now kiosk-auto-reboot.service >/dev/null 2>&1 || true

# Dedicated kiosk boot: console target, no desktop display manager or login shell
# competing for tty1. systemd starts the non-root kiosk session directly.
BOOT_PATH_CHANGED=1
systemctl set-default multi-user.target >/dev/null
for display_service in lightdm.service gdm3.service sddm.service display-manager.service; do
  systemctl disable "$display_service" >/dev/null 2>&1 || true
done
systemctl disable getty@tty1.service >/dev/null 2>&1 || true
systemctl mask getty@tty1.service >/dev/null 2>&1 || true

systemctl daemon-reload
systemctl enable kiosk.service >/dev/null
systemctl enable kiosk-healthcheck.timer >/dev/null
systemctl enable kiosk-maintenance.timer >/dev/null
if [[ "$ENABLE_PERIODIC_BROWSER_RECYCLE" == "1" ]]; then
  systemctl enable kiosk-browser-recycle.timer >/dev/null
else
  systemctl disable --now kiosk-browser-recycle.timer >/dev/null 2>&1 || true
fi

if [[ "$ENABLE_NETWORK_GUARD" == "1" || "$ENABLE_SOFTWARE_WATCHDOG" == "1" ]]; then
  systemctl enable kiosk-guard.service >/dev/null
else
  systemctl disable kiosk-guard.service >/dev/null 2>&1 || true
fi

systemctl restart systemd-journald.service >/dev/null 2>&1 || true
systemctl daemon-reexec >/dev/null 2>&1 || true

# Stop any old graphical session now, then start the new units. SSH remains up.
systemctl stop display-manager.service >/dev/null 2>&1 || true
systemctl stop getty@tty1.service >/dev/null 2>&1 || true
systemctl restart kiosk.service

# A running service can still contain a repeatedly failing X supervisor, so checking
# only kiosk.service is not enough. Verify the actual X server and Chromium's
# local DevTools endpoint before reporting success.
systemctl is-active --quiet kiosk.service || fatal "kiosk.service did not remain active after direct tty1 startup."

xorg_ready=0
for _ in $(seq 1 75); do
  if pgrep -af '(^|/)(Xorg|X)[[:space:]].*:0([[:space:]]|$)' >/dev/null 2>&1; then
    xorg_ready=1
    break
  fi
  sleep 1
done

if [[ "$xorg_ready" != "1" ]]; then
  grep -nE '\(EE\)|Fatal server error|no screens|framebuffer|Permission denied|Cannot|failed' \
    /var/log/Xorg.0.log 2>/dev/null | tail -n 100 >&2 || true
  journalctl -u kiosk.service -b -n 150 --no-pager >&2 || true
  fatal "Xorg display :0 did not start."
fi

browser_ready=0
for _ in $(seq 1 180); do
  if env \
      KIOSK_DATA_FRESHNESS_MODE="$KIOSK_DATA_FRESHNESS_MODE" \
      KIOSK_DATA_FRESHNESS_MAX_AGE_SECS="$KIOSK_DATA_FRESHNESS_MAX_AGE_SECS" \
      KIOSK_DATA_FRESHNESS_SELECTOR="$KIOSK_DATA_FRESHNESS_SELECTOR" \
      KIOSK_DATA_FRESHNESS_ATTRIBUTE="$KIOSK_DATA_FRESHNESS_ATTRIBUTE" \
      KIOSK_DATA_FRESHNESS_EXPRESSION="$KIOSK_DATA_FRESHNESS_EXPRESSION" \
      KIOSK_FRESHNESS_EXEMPT_PATH_REGEX="$KIOSK_FRESHNESS_EXEMPT_PATH_REGEX" \
      timeout "$((KIOSK_RENDERER_PROBE_TIMEOUT_SECS + 3))"s \
      /usr/local/bin/kiosk-cdp-probe.py \
      "$KIOSK_DEVTOOLS_PORT" "$KIOSK_RENDERER_PROBE_TIMEOUT_SECS" \
      >/dev/null 2>&1; then
    browser_ready=1
    break
  fi
  sleep 1
done

if [[ "$browser_ready" != "1" ]]; then
  journalctl -u kiosk.service -b -n 200 --no-pager >&2 || true
  fatal "Chromium did not produce a responsive page renderer."
fi

systemctl restart kiosk-healthcheck.timer
systemctl restart kiosk-maintenance.timer
if systemctl is-enabled --quiet kiosk-browser-recycle.timer; then
  systemctl restart kiosk-browser-recycle.timer
fi
if systemctl is-enabled --quiet kiosk-guard.service; then
  systemctl restart kiosk-guard.service
fi

BOOT_PATH_CHANGED=0
log "Installation completed successfully"
printf '\n'
printf 'Useful status commands:\n'
printf '  systemctl status kiosk.service --no-pager\n'
printf '  journalctl -u kiosk.service -b -n 200 --no-pager\n'
printf '  systemctl status kiosk-healthcheck.timer --no-pager\n'
printf '  journalctl -u kiosk-healthcheck.service -b -n 100 --no-pager\n'
printf '  journalctl -u kiosk-guard.service -b -n 100 --no-pager\n'
printf '  systemctl list-timers kiosk-healthcheck.timer kiosk-maintenance.timer kiosk-browser-recycle.timer\n\n'
printf 'Recovery order: Chromium restart -> low-memory recycle -> full X session restart -> guarded reboot after persistent local or confirmed transport failure.\n'
printf 'Pi 3 low-memory browser recycle: %s every %sh.\n\n' "$ENABLE_PERIODIC_BROWSER_RECYCLE" "$KIOSK_BROWSER_RECYCLE_HOURS"
printf 'Emergency restart commands:\n'
printf '  sudo systemctl restart kiosk.service\n'
printf '  sudo systemctl restart NetworkManager.service\n\n'
printf 'GPU fallback, only when Chromium logs show GPU/GL/EGL crashes:\n'
printf "  sudo sed -i 's/^KIOSK_DISABLE_GPU=.*/KIOSK_DISABLE_GPU=1/' /etc/default/kiosk\n"
printf '  sudo systemctl restart kiosk.service\n\n'

if [[ "$KIOSK_AUTO_REBOOT_AFTER_INSTALL" == "1" ]]; then
  log "Rebooting automatically so board-specific graphics, groups, ZRAM and watchdog settings take effect"
  sync
  systemctl reboot
else
  printf 'Automatic reboot is disabled. Reboot once manually to apply all boot changes:\n'
  printf '  sudo reboot\n'
fi
