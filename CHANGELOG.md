# Changelog

## [1.1.0] - 2026-08-26

### Fixed
- **Authenticode signature evidence** — `GetSigner` used a certificate-file loader on executables, so the digital-signature signal never fired; now extracts the embedded Authenticode cert (with per-file caching)
- **History timestamps shifted by UTC offset** — SQLite reader parsed stored UTC "O"-format strings as local time, mis-dating late-evening entries by one calendar day in UTC+ locales (`RoundtripKind` fix, Kind preserved)

### Changed
- Package alignment: Microsoft.Extensions.* and Microsoft.Data.Sqlite 10.0.0 → 9.0.19 to match the net9.0 target framework
- Uninstall-string parser hardening: unquoted paths with executable extensions (.exe/.com/.bat/.cmd/.msi/.scr) resolve as full paths even when the file no longer exists; extension-less fallback now splits at the LAST space so trailing arguments never bleed into the path
- Settings saves are atomic (write-to-temp + move) — a crash mid-write can no longer corrupt settings.json

### Added
- Single-instance guard (named mutex per user session) preventing concurrent SQLite/quarantine contention
- MIT LICENSE file (badge in README previously linked nowhere)
- GitHub Actions CI: build (warnings-as-errors) + full test suite on windows-latest
- Test coverage: Application audit-trail tests, SQLite history round-trip integration tests, elevated-helper pipe protocol tests against an in-process fake server (regression guard for disposal-order), new parser edge-case tests — 55 → 66 tests across 4 projects

---

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
