# Changelog

## [1.0.0] - 2026-07-29

### Added
- **Software Health Detail View** - Click on any health category (Broken Uninstall Entries, Missing Uninstallers, etc.) to see detailed list of affected programs with actions:
  - View program name, publisher, size, and install date
  - "Open Location" button to open install folder
  - "Registry" button to open registry key in regedit
- **Settings Save Button** - Save button in Settings page footer to persist configuration changes
- **GitHub Repository Link** - Clickable GitHub button in About page linking to https://github.com/wisnurafi/uninstra

### Technical
- Added `HealthIssueDetail` model for software health detail items
- Added `StringToVisibilityConverter` for conditional UI visibility
- Registered `BytesToSizeConverter` and `StringToVisibilityConverter` in App.xaml

---

## [0.9.0] - 2026-07-07

### Initial Release
- Deep uninstaller with leftover scanning
- Software health monitoring
- Browser extensions management
- Windows apps management
- Junk cleaner
- Quarantine system
- Install monitor
- Force uninstall
- History tracking
