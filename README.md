<div align="center">

# 🛡️ Uninstra

### Open-Source Deep Uninstaller & Software Cleanup Tool

A modern, privacy-first alternative to IObit Uninstaller — built with WPF & .NET 9.

[![CI](https://github.com/wisnurafi/Uninstra/actions/workflows/ci.yml/badge.svg)](https://github.com/wisnurafi/Uninstra/actions/workflows/ci.yml)
[![Release](https://img.shields.io/github/v/release/wisnurafi/Uninstra?style=flat-square&logo=github)](https://github.com/wisnurafi/Uninstra/releases/latest)
[![License: MIT](https://img.shields.io/badge/license-MIT-blue?style=flat-square)](LICENSE)
[![.NET](https://img.shields.io/badge/.NET-9.0-512BD4?style=flat-square&logo=dotnet)](https://dotnet.microsoft.com/)
[![Platform](https://img.shields.io/badge/platform-Windows-0078D4?style=flat-square&logo=windows)](https://github.com/wisnurafi/Uninstra)
[![WPF](https://img.shields.io/badge/UI-WPF-512BD4?style=flat-square)](https://github.com/dotnet/wpf)

</div>

---

## 📖 Overview

**Uninstra** is a Windows desktop application designed to help users thoroughly uninstall software, clean up residual files, manage browser extensions, and maintain overall system hygiene. Unlike commercial uninstallers that bundle ads, telemetry, or background services, Uninstra runs entirely offline with zero network access and zero tracking.

Every uninstallation leaves behind files, registry keys, and orphaned services. Uninstra tackles this through **evidence-based leftover detection** — a multi-signal scanning engine that assigns confidence scores to each candidate, ensuring safe and informed cleanup decisions.

---

## ✨ Features

| Feature | Description |
|---------|-------------|
| 📋 **Program Manager** | Browse, search, filter, and uninstall installed applications with deep leftover scanning |
| 💚 **Software Health** | Detect broken uninstall entries, missing installers, recently installed programs, and large applications |
| 📡 **Install Monitor** | Track file and registry changes during software installation via system snapshot comparison |
| 🔨 **Force Uninstall** | Remove stubborn programs that resist normal uninstallation by targeting executables or folders directly |
| 🔍 **Residual Scan** | Find and clean orphaned registry entries, broken uninstall paths, and missing install locations |
| 🪟 **Windows Apps** | Manage MSIX/AppX packages (UWP/Store apps) with search and filtering |
| 🧩 **Browser Extensions** | View extensions across Chrome, Firefox, and Edge browser profiles |
| 🧹 **Junk Cleaner** | Clean temporary files, logs, caches, crash dumps, and other reclaimable disk space |
| 🗃 **Quarantine** | Safely quarantine items before permanent deletion with restore capability |
| 📜 **History** | Full audit trail of all uninstall and cleanup operations stored in local SQLite database |

---

## 🏗️ Architecture

Uninstra follows a **clean architecture** pattern with clear separation of concerns:

```
src/
├── Uninstra.Core           # Domain models, enums, scoring, safety policies
├── Uninstra.Application    # Interfaces, coordinators, business logic
├── Uninstra.Windows        # Windows-specific scanners (registry, filesystem, browser)
├── Uninstra.Infrastructure # SQLite persistence, JSON settings, file quarantine
├── Uninstra.App            # WPF UI (MVVM, dark theme, sidebar navigation)
└── Uninstra.ElevatedHelper # Admin helper process (named pipe, restore points)

tests/
├── Uninstra.Core.Tests         # Domain logic: command parsing, scoring, safety policies
├── Uninstra.Application.Tests  # Audit-trail / business logic tests
├── Uninstra.Windows.Tests      # Elevated-helper pipe protocol tests (fake server)
└── Uninstra.IntegrationTests   # SQLite round trips, settings persistence

tools/
├── Uninstra.DummyApp       # Test target for uninstall testing
└── Uninstra.DummyInstaller # Test installer for monitor testing
```

### Design Principles

- **Evidence-based detection** — leftover candidates are scored with confidence levels (High/Medium/Low) and risk assessment, not blindly deleted
- **Safety-first** — protected paths, protected applications, path traversal prevention, and quarantine-before-delete
- **No network access** — runs entirely locally, zero telemetry, zero analytics
- **Elevation on demand** — admin privileges requested only when needed via named pipe helper process
- **MVVM + Dependency Injection** — clean separation, fully testable architecture

---

## 🛠️ Tech Stack

| Layer | Technology |
|-------|-----------|
| Runtime | .NET 9 (LTS) |
| UI Framework | WPF (Windows Presentation Foundation) |
| MVVM | CommunityToolkit.Mvvm (source-generated) |
| Database | Microsoft.Data.Sqlite |
| Logging | Serilog |
| Testing | xUnit + FluentAssertions |
| DI Container | Microsoft.Extensions.DependencyInjection |

---

## 🚀 Getting Started

### Prerequisites

- **Windows 10** (64-bit) or later
- **.NET 9 SDK** — [Download](https://dotnet.microsoft.com/download/dotnet/9.0)

### Install (Recommended)

Grab the latest pre-built release — no .NET runtime or SDK needed:

**[⬇️ Download Uninstra v3.0.0](https://github.com/wisnurafi/Uninstra/releases/latest)**

Unzip and run `Uninstra.exe`. The elevated helper (`Uninstra.ElevatedHelper.exe`)
must sit next to the main executable — it already does inside the zip.

### Build from Source

```bash
# Clone the repository
git clone https://github.com/wisnurafi/Uninstra.git
cd Uninstra

# Restore and build
dotnet build Uninstra.sln

# Run the full test suite (71 tests across 4 projects)
dotnet test Uninstra.sln

# Run the application
dotnet run --project src/Uninstra.App
```

### Publish Self-Contained Release

The main app ships self-contained (bundles the .NET runtime). The elevated
helper must be published **separately** as a framework-dependent single file
and placed next to `Uninstra.exe` — it is launched via UAC elevation by name.

```bash
# 1) Main WPF app — self-contained single file
dotnet publish src/Uninstra.App/Uninstra.App.csproj \
  -c Release \
  -r win-x64 \
  --self-contained true \
  -p:PublishSingleFile=true \
  -p:IncludeNativeLibrariesForSelfExtract=true \
  -p:EnableCompressionInSingleFile=true \
  -o ./publish/Uninstra-win-x64

# 2) Elevated helper — framework-dependent single file, same folder
dotnet publish src/Uninstra.ElevatedHelper/Uninstra.ElevatedHelper.csproj \
  -c Release \
  -r win-x64 \
  --self-contained false \
  -p:PublishSingleFile=true \
  -o ./publish/Uninstra-win-x64

# 3) Drop debug symbols for distribution
rm ./publish/Uninstra-win-x64/*.pdb

# 4) Zip it up
cd publish && python -c "import zipfile,os; [zipfile.ZipFile('Uninstra-v<VERSION>-win-x64.zip','w',zipfile.ZIP_DEFLATED,9).write(os.path.join('Uninstra-win-x64',f),'Uninstra-win-x64/'+f) for f in os.listdir('Uninstra-win-x64')]"
```

This produces `Uninstra.exe` (~62 MB, no runtime install required) plus its
elevation companion. **Shipping without step 2 breaks every admin operation**
(HKLM cleanup, restore points, protected-path quarantine).

---

## 🔒 Privacy

Uninstra runs **entirely on your machine**.

- ❌ No telemetry
- ❌ No analytics
- ❌ No background services
- ❌ No network requests
- ✅ All data stays local (SQLite database, log files, quarantine manifests)

**Data storage locations:**
- Database: `%LOCALAPPDATA%\Uninstra\uninstra.db`
- Logs: `%LOCALAPPDATA%\Uninstra\Logs\`
- Quarantine: `%LOCALAPPDATA%\Uninstra\Quarantine\`

---

## 📸 Screenshots

> *Screenshots coming soon — the UI features a modern dark theme with gradient accents, sidebar navigation, and a multi-phase deep uninstall dialog.*

---

## ⚠️ Disclaimer

Uninstra is an open-source project developed for educational and personal use. While it implements safety measures such as confidence scoring, protected paths, and quarantine functionality, **it is not guaranteed to be perfect**. False positives, missed entries, or unexpected behavior may still occur.

**You use Uninstra at your own risk.** The authors and contributors are not responsible for any data loss, system instability, or damage that may result from using this software.

### Status

This project is actively developed but may contain bugs, incomplete features, or rough edges. Areas that may need further improvement include:

- Browser Extension removal (scan-only, no inline removal yet)
- Light theme (`Themes/LightTheme.xaml` — currently Dark only)
- Window position/size persistence (settings fields exist, not yet wired to the UI)
- DummyApp / DummyInstaller tooling

**Contributions, bug reports, and feature requests are welcome.** Feel free to fork, modify, and submit pull requests. If you encounter issues, please [open an issue](https://github.com/wisnurafi/Uninstra/issues).

---

## 📄 License

This project is licensed under the **MIT License** — see the [LICENSE](LICENSE) file for details.

---

<div align="center">

**Built with ❤️ by [Wisnu](https://github.com/wisnurafi)**

If you find Uninstra useful, consider giving it a ⭐!

</div>
