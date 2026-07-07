<div align="center">

# 🛡️ Uninstra

### Open-Source Deep Uninstaller & Software Cleanup Tool

A modern, privacy-first alternative to IObit Uninstaller — built with WPF & .NET 9.

[![License: MIT](https://img.shields.io/badge/license-MIT-blue?style=flat-square)](LICENSE)
[![.NET](https://img.shields.io/badge/.NET-9.0-512BD4?style=flat-square&logo=dotnet)](https://dotnet.microsoft.com/)
[![Platform](https://img.shields.io/badge/platform-Windows-0078D4?style=flat-square&logo=windows)](https://github.com/)
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
├── Uninstra.Core.Tests         # 55 unit tests — domain logic, scoring, normalization
├── Uninstra.Application.Tests  # Business logic tests
├── Uninstra.Windows.Tests      # Windows-specific scanner tests
└── Uninstra.IntegrationTests   # End-to-end integration tests

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

### Build from Source

```bash
# Clone the repository
git clone https://github.com/YOUR_USERNAME/Uninstra.git
cd Uninstra

# Restore and build
dotnet build Uninstra.sln

# Run tests
dotnet test tests/Uninstra.Core.Tests

# Run the application
dotnet run --project src/Uninstra.App
```

### Publish Self-Contained Release

```bash
dotnet publish src/Uninstra.App/Uninstra.App.csproj \
  -c Release \
  -r win-x64 \
  --self-contained true \
  -p:PublishSingleFile=true \
  -p:IncludeNativeLibrariesForSelfExtract=true \
  -p:EnableCompressionInSingleFile=true \
  -o ./publish
```

This produces a single `Uninstra.exe` (~61 MB) that requires no .NET runtime installation.

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

- Install Monitor (currently uses placeholder snapshot logic)
- Browser Extension removal (scan-only, no inline removal yet)
- Quarantine restore flow
- Integration & Windows-specific tests
- DummyApp / DummyInstaller tooling

**Contributions, bug reports, and feature requests are welcome.** Feel free to fork, modify, and submit pull requests. If you encounter issues, please [open an issue](https://github.com/YOUR_USERNAME/Uninstra/issues).

---

## 📄 License

This project is licensed under the **MIT License** — see the [LICENSE](LICENSE) file for details.

---

<div align="center">

**Built with ❤️ by [Wisnu](https://github.com/YOUR_USERNAME)**

If you find Uninstra useful, consider giving it a ⭐!

</div>
