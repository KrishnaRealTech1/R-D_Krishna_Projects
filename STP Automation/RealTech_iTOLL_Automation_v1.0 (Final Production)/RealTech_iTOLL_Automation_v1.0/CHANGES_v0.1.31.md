# Changes in v0.1.31

## Standalone build hotfix

- Fixed the WPF compile error in `WindowsBrandingService.ApplyTo`.
- Replaced the invalid `Window.IsSourceInitialized` check with the supported `PresentationSource.FromVisual(window)` check.
- Preserved the taskbar icon behavior: the icon is assigned immediately and is assigned again after `SourceInitialized` when the native window handle does not yet exist.
- Retains all v0.1.30 IN/OUT auto-approval functionality and Admin Control Panel settings.
- Updated application version to `0.1.31`.
