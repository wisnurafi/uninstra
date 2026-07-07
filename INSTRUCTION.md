Kamu adalah senior Windows desktop engineer, software architect, security engineer, QA engineer, dan UI/UX designer.

Buat aplikasi desktop Windows open-source bernama:

UNINSTRA

Tagline:
“Deep Uninstaller & Software Cleanup”

Tujuan aplikasi ini adalah menjadi software manager dan deep uninstaller modern yang pengalaman pengguna serta kelengkapan fiturnya setara dengan aplikasi seperti IObit Uninstaller, tetapi memiliki identitas, desain, source code, logo, aset, copywriting, dan implementasi sendiri.

Jangan menyalin logo, ikon, aset, layout secara pixel-perfect, wording, kode, atau elemen proprietary milik IObit. Ambil inspirasi hanya dari pola pengalaman pengguna berupa sidebar software manager, daftar aplikasi yang padat, batch uninstall, install monitor, forced uninstall, residual scan, software health, browser extension manager, dan Windows Apps manager.

Aplikasi harus gratis, open-source, tanpa iklan, tanpa telemetry, tanpa promosi aplikasi pihak ketiga, dan tanpa fitur yang dikunci berbayar.

Kerjakan aplikasi secara end-to-end di repository ini.

Jangan berhenti pada:
- Rencana
- Arsitektur
- Pseudocode
- Mockup
- Scaffolding
- Placeholder
- Tombol yang tidak berfungsi
- Data dummy
- Komentar TODO pada fitur utama

Setelah memberikan rencana singkat, langsung buat project, implementasikan fitur, jalankan build, jalankan test, perbaiki error, dan siapkan output release.

Ambil keputusan teknis kecil secara mandiri. Tanyakan hanya apabila benar-benar ada informasi penting yang tidak bisa ditentukan dari project atau environment.

==================================================
VISI PRODUK
==================================================

Uninstra adalah aplikasi desktop Windows yang memungkinkan pengguna:

1. Melihat seluruh program yang terpasang.
2. Mengelompokkan program berdasarkan kategori.
3. Menghapus satu atau banyak program sekaligus.
4. Menjalankan uninstaller resmi milik program.
5. Melakukan post-uninstall scan.
6. Mendeteksi file, folder, registry, service, task, shortcut, dan startup entry yang tertinggal.
7. Menampilkan tingkat keyakinan dan alasan mengapa item dianggap leftover.
8. Melakukan forced uninstall ketika uninstaller resmi rusak atau hilang.
9. Memonitor instalasi software baru.
10. Mencatat perubahan yang dibuat installer.
11. Mendeteksi residual file dari software yang sudah lama dihapus.
12. Mengelola aplikasi Microsoft Store.
13. Memeriksa browser extensions.
14. Menampilkan kondisi kesehatan software.
15. Membersihkan junk file yang aman.
16. Menyediakan quarantine dan rollback.
17. Menyimpan history dan report seluruh operasi.

Prioritas utama:

SAFETY > ACCURACY > SPEED > VISUAL APPEARANCE

Lebih baik meninggalkan beberapa file sisa daripada menghapus file shared atau file sistem yang masih diperlukan.

Jangan mengklaim bahwa Uninstra dapat menghapus semua aplikasi 100% tanpa sisa.

Gunakan wording seperti:

“Deep uninstall menggunakan deteksi leftover berbasis bukti dan tingkat keyakinan.”

==================================================
TECH STACK
==================================================

Gunakan:

- C#
- WPF
- MVVM
- .NET 10 LTS jika tersedia
- Fallback ke .NET 8 LTS jika .NET 10 tidak tersedia
- Target win-x64
- Nullable reference types enabled
- Implicit usings enabled
- Async/await
- CancellationToken
- Dependency Injection
- CommunityToolkit.Mvvm
- Microsoft.Extensions.Hosting
- Microsoft.Extensions.DependencyInjection
- Microsoft.Extensions.Logging
- Serilog
- Microsoft.Data.Sqlite
- System.Text.Json
- xUnit
- FluentAssertions
- Windows native API atau COM melalui P/Invoke jika diperlukan

Jangan gunakan:

- Electron
- WebView sebagai UI utama
- Node.js
- Python runtime
- Backend web
- Cloud service
- Telemetry SDK
- Analytics
- Iklan
- Tracking
- Dependency berbayar
- Package prerelease kecuali benar-benar tidak ada alternatif stabil

Target OS:

- Windows 10 64-bit
- Windows 11 64-bit

Uninstra dibuild sebagai aplikasi x64, tetapi harus mampu mendeteksi program 32-bit dan 64-bit.

==================================================
STRUKTUR SOLUTION
==================================================

Buat:

Uninstra.sln

src/
  Uninstra.App/
  Uninstra.Core/
  Uninstra.Application/
  Uninstra.Infrastructure/
  Uninstra.Windows/
  Uninstra.ElevatedHelper/

tests/
  Uninstra.Core.Tests/
  Uninstra.Application.Tests/
  Uninstra.Windows.Tests/
  Uninstra.IntegrationTests/

installer/
  Uninstra.iss

docs/
  architecture.md
  safety-model.md
  uninstall-engine.md
  leftover-scoring.md
  install-monitor.md
  development.md
  release.md

Gunakan tanggung jawab berikut.

Uninstra.App:
- WPF Views
- ViewModels
- Navigation
- Dialog
- Toast
- Theme
- ResourceDictionary
- UI services
- Value converters
- Tidak berisi business logic utama

Uninstra.Core:
- Domain models
- Enums
- Value objects
- Result types
- Safety rules
- Confidence scoring
- Validation rules
- Tidak bergantung pada WPF atau Windows Registry

Uninstra.Application:
- Use cases
- Orchestration
- Commands
- Queries
- Application services
- Batch uninstall coordinator
- Scan coordinator
- Report coordinator
- Tidak berisi implementasi native Windows

Uninstra.Infrastructure:
- SQLite
- Repository
- Settings
- History
- Quarantine manifest
- JSON serialization
- Report generation
- Logging setup

Uninstra.Windows:
- Installed application scanner
- Registry access
- MSI handling
- EXE uninstaller handling
- MSIX/AppX
- Process management
- File system scanner
- Registry leftover scanner
- Services
- Scheduled Tasks
- Startup entries
- Shortcut handling
- Digital signatures
- File metadata
- Icon extraction
- Known Folder API
- Restore point
- Browser extension scanner
- Install monitor
- Junk cleaner

Uninstra.ElevatedHelper:
- Operasi administratif
- Named pipe server
- Request validation
- Path validation
- Registry modification
- Service removal
- Scheduled task removal
- Protected file handling
- Restore point operation

==================================================
IDENTITAS DAN BRANDING
==================================================

Nama aplikasi:
Uninstra

Tagline:
Deep Uninstaller & Software Cleanup

Desain logo:

- Bentuk sederhana
- Huruf U abstrak
- Menggabungkan konsep uninstall, sweep, dan clean
- Warna accent cyan atau electric blue
- Harus tetap terbaca pada ukuran 16x16
- Buat aset logo sederhana sendiri menggunakan vector XAML atau SVG
- Jangan mengambil ikon dari internet
- Jangan menyalin logo software lain

Application ID:
Uninstra.Desktop

Publisher sementara:
Uninstra Open Source

Default executable:
Uninstra.exe

==================================================
PRINSIP UI DAN UX
==================================================

UI harus terasa seperti desktop software manager profesional, bukan dashboard SaaS atau website.

Gunakan:

- Permanent left sidebar
- Compact application table
- Small application icons
- Checkbox pada setiap row
- Search bar di atas daftar
- Category navigation
- Detail panel
- Context menu
- Persistent action bar ketika ada item dipilih
- Fast switching antar kategori
- Compact spacing
- Clear hierarchy
- Native desktop interaction
- Keyboard navigation
- High DPI support
- Multi-monitor support

Hindari:

- Hero section
- Oversized marketing text
- Card terlalu besar
- Grafik dekoratif berlebihan
- Gradient berlebihan
- Tombol raksasa
- Layout seperti website admin
- Semua konten dimasukkan ke rounded card
- Animasi yang memperlambat penggunaan
- Pixel-perfect clone aplikasi lain

Tema:

- Dark mode default
- Light mode tersedia
- System theme mode tersedia
- Accent cyan atau electric blue
- Background dark gray
- Border tipis
- Rounded corner secukupnya
- Shadow sangat ringan
- Typography jelas
- Gunakan font sistem Windows

Minimum window:
1100 x 700

Default window:
1280 x 800

Simpan:
- Window size
- Window position
- Selected theme
- Selected category
- Table column width
- Sort order

Pastikan window tidak terbuka di luar area layar setelah monitor berubah.

==================================================
NAVIGATION UTAMA
==================================================

Sidebar utama:

1. Programs
   - All Programs
   - Recently Installed
   - Large Programs
   - Infrequently Used
   - Bundleware
   - Logged Programs
   - Windows Updates
   - System Components

2. Software Health

3. Install Monitor

4. Force Uninstall

5. Residual Scan

6. Windows Apps

7. Browser Extensions

8. Junk Cleaner

9. Quarantine

10. History

11. Settings

12. About

Sidebar harus dapat collapse.

Tampilkan jumlah item pada kategori yang relevan.

==================================================
HALAMAN PROGRAMS
==================================================

Halaman Programs adalah halaman utama.

Gunakan compact DataGrid atau virtualized list.

Kolom:

- Checkbox
- Icon
- Program Name
- Publisher
- Version
- Size
- Install Date
- Usage
- Installer Type
- Architecture
- Action

Action per row:

- Uninstall
- Deep Uninstall
- Open Install Location
- View Details
- Force Uninstall
- Open Registry Entry dalam Advanced Mode

Tambahkan search berdasarkan:

- Program name
- Publisher
- Version
- Install location

Sorting:

- Name
- Size
- Install date
- Publisher
- Last used
- Version

Filtering:

- Architecture
- Installer type
- Publisher
- User application
- System component
- Runtime
- Update
- Driver-related
- Store application

Ketika satu atau lebih program dipilih, tampilkan bottom action bar:

- Jumlah program dipilih
- Total estimated size
- Uninstall Selected
- Clear Selection

Contoh:

“3 programs selected · 2.41 GB”

Batch uninstall wajib berfungsi.

Gunakan virtualisasi agar list tetap lancar ketika program sangat banyak.

==================================================
KATEGORI PROGRAM
==================================================

All Programs:
Semua program normal yang terpasang.

Recently Installed:
Program yang terinstal dalam periode tertentu.

Default:
30 hari terakhir

Sediakan filter:
- 7 hari
- 30 hari
- 90 hari

Large Programs:
Program di atas batas ukuran tertentu.

Default:
500 MB

Infrequently Used:
Program yang jarang digunakan berdasarkan best-effort evidence seperti:
- Last execution record milik Uninstra
- Recent shortcut usage jika tersedia
- UserAssist jika dapat dibaca secara aman
- Install age
- Jangan mengklaim data penggunaan akurat apabila evidence tidak cukup

Bundleware:
Program yang diduga ikut terinstal dalam installation session yang sama.

Logged Programs:
Program yang instalasinya tercatat oleh Install Monitor.

Windows Updates:
Update entry yang terdaftar di uninstall registry.

System Components:
Runtime, framework, driver utility, dependency, system component, dan program sensitif.

System Components disembunyikan secara default dan harus memiliki warning banner.

==================================================
INSTALLED APPLICATION SCANNER
==================================================

Deteksi program dari:

HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall

HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall

HKEY_CURRENT_USER\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall

Gunakan RegistryView.Registry64 dan RegistryView.Registry32 dengan benar.

Jangan gunakan WMI Win32_Product.

Baca ketika tersedia:

- DisplayName
- DisplayVersion
- Publisher
- InstallDate
- InstallLocation
- UninstallString
- QuietUninstallString
- ModifyPath
- DisplayIcon
- EstimatedSize
- WindowsInstaller
- SystemComponent
- NoRemove
- NoModify
- NoRepair
- ReleaseType
- ParentKeyName
- ParentDisplayName
- BundleProviderKey
- ProductID
- ProductCode
- URLInfoAbout
- HelpLink
- Comments
- Registry hive
- Registry key
- Registry view

Model InstalledApplication minimal harus mempunyai:

- Id
- DisplayName
- NormalizedName
- DisplayVersion
- Publisher
- InstallDate
- InstallLocation
- UninstallCommand
- QuietUninstallCommand
- ModifyCommand
- DisplayIconPath
- EstimatedSizeBytes
- ProductCode
- InstallerType
- Architecture
- ApplicationCategory
- RegistryHive
- RegistryKeyPath
- RegistryView
- IsSystemComponent
- IsRuntime
- IsUpdate
- IsDriverRelated
- IsStoreApplication
- IsProtected
- ProtectionReason
- DigitalSignaturePublisher
- Icon
- DetectionEvidence

Implementasikan stable ID.

Implementasikan deduplication berdasarkan kombinasi:

- ProductCode
- Registry key
- Install location
- Normalized name
- Publisher
- Uninstall command

Jangan menggabungkan aplikasi berbeda hanya karena namanya mirip.

==================================================
UNINSTALLER TYPE DETECTION
==================================================

Deteksi:

- MSI
- Inno Setup
- NSIS
- InstallShield
- WiX Burn
- Squirrel
- ClickOnce
- Custom EXE
- Microsoft Store/MSIX/AppX
- Per-user application
- Per-machine application
- Broken uninstall entry
- Missing uninstaller
- Unknown

Gunakan:

- Registry metadata
- Executable metadata
- ProductCode
- Command pattern
- File version information
- Digital signature
- Known safe installer signatures

Jangan hanya menggunakan nama executable.

==================================================
NORMAL UNINSTALL
==================================================

Normal uninstall harus:

1. Menampilkan confirmation dialog.
2. Menampilkan program, publisher, version, size, dan install location.
3. Mendeteksi proses terkait yang sedang berjalan.
4. Menawarkan close gracefully.
5. Tidak melakukan force kill tanpa konfirmasi.
6. Menjalankan uninstaller resmi.
7. Memantau proses tanpa memblokir UI.
8. Menampilkan progress state.
9. Mencatat exit code.
10. Mendeteksi cancellation.
11. Menangani UAC cancellation.
12. Menangani child process.
13. Menunggu kondisi uninstall selesai secara best effort.
14. Refresh daftar program.
15. Menjalankan post-uninstall leftover scan.
16. Menampilkan review leftover.
17. Memindahkan item terpilih ke quarantine.
18. Menampilkan result screen.

Status akhir:

- Completed
- CompletedWithWarnings
- Cancelled
- Failed
- UnknownResult

Jangan menyatakan uninstall berhasil hanya karena proses pertama exit dengan code 0. Verifikasi juga registry entry dan install location.

==================================================
UNINSTALL COMMAND PARSING
==================================================

Buat parser command line Windows yang benar.

Harus menangani:

- Quoted executable
- Unquoted executable
- Path dengan spasi
- Arguments
- Escaped quotes
- Environment variable
- Rundll32 entry
- msiexec
- Missing executable
- Malformed command

Jangan menggunakan naive Split(' ').

Jangan menjalankan command melalui:

- cmd.exe /c
- PowerShell
- shell string interpolation

Gunakan ProcessStartInfo dengan executable dan arguments terpisah.

Validasi executable path.

==================================================
MSI UNINSTALL
==================================================

Untuk MSI:

- Kenali ProductCode GUID.
- Validasi GUID.
- Gunakan msiexec.exe dengan path sistem resmi.
- Pisahkan argument.
- Dukung interactive uninstall.
- Dukung quiet uninstall hanya ketika user memilih quiet mode.
- Jangan menjalankan repair.
- Jangan menjalankan modify.
- Jangan menghapus Windows Installer cache.
- Jangan menghapus C:\Windows\Installer.

==================================================
MICROSOFT STORE DAN MSIX
==================================================

Halaman Windows Apps harus menampilkan:

- App icon
- Display name
- Package family name
- Publisher
- Version
- Install size jika tersedia
- Install location jika dapat diakses
- Framework status
- Dependency status
- User scope
- Protected status

Gunakan PackageManager atau Windows API resmi.

Lindungi:

- Windows shell components
- Microsoft Store
- Windows Security
- App Installer
- Runtime framework
- Dependency packages
- Provisioned system packages
- Required inbox apps

Jangan menghapus provisioned package untuk semua user tanpa explicit advanced confirmation.

==================================================
BATCH UNINSTALL
==================================================

Batch uninstall wajib berfungsi.

Workflow:

1. User memilih beberapa program.
2. Tampilkan review list.
3. Hitung total estimated size.
4. Tandai program protected.
5. Program protected tidak dipilih secara default.
6. User mengonfirmasi.
7. Buat uninstall queue.
8. Jalankan satu per satu.
9. Jangan menjalankan beberapa interactive uninstaller secara paralel.
10. Tampilkan queue progress.
11. User dapat skip program berikutnya.
12. User dapat cancel queue setelah operasi aktif selesai.
13. Lakukan leftover scan per program.
14. Simpan hasil per program.
15. Tampilkan batch summary.

Queue item status:

- Waiting
- Preparing
- RunningUninstaller
- ScanningLeftovers
- AwaitingReview
- Cleaning
- Completed
- Skipped
- Failed
- Cancelled

==================================================
DEEP UNINSTALL WORKFLOW
==================================================

Gunakan visual stepper:

1. Selected Program
2. Safety Check
3. Restore Point
4. Running Uninstaller
5. Scanning Leftovers
6. Review Leftovers
7. Cleanup
8. Result

Setiap langkah harus menampilkan:

- Current activity
- Progress
- Warning
- Error
- Cancel availability
- Operation log ringkas

Jangan memblokir UI.

==================================================
LEFTOVER SCANNER
==================================================

Setelah uninstaller selesai, scan:

- Original InstallLocation
- Relevant parent folder
- LocalAppData
- RoamingAppData
- ProgramData
- Program Files
- Program Files x86
- User Start Menu
- Common Start Menu
- User Desktop
- Public Desktop
- User Startup
- Common Startup
- Registry HKCU
- Registry HKLM
- Services
- Scheduled Tasks
- Startup registry entries
- Shortcuts
- Known application cache locations yang aman

Jangan melakukan pencarian seluruh disk secara agresif hanya berdasarkan satu kata.

Gunakan evidence:

- Exact InstallLocation
- ProductCode
- Exact executable path
- Uninstall registry ownership
- Publisher match
- Digital signature publisher match
- File version metadata
- Service ImagePath
- Scheduled Task action path
- Shortcut target path
- Startup command path
- Exact normalized program name
- Publisher and program name combination
- Install monitor manifest
- Installation timestamp relationship
- Registry value pointing to executable
- File created during monitored installation
- Directory created during monitored installation

Model LeftoverCandidate:

- Id
- ApplicationId
- DisplayName
- Type
- Path
- RegistryHive
- RegistryPath
- RegistryValueName
- SizeBytes
- ConfidenceScore
- ConfidenceLevel
- RiskLevel
- Evidence
- Warnings
- IsSelectedByDefault
- RequiresElevation
- IsProtected
- ProtectionReason
- CanRollback
- LastModified
- SourceScanner

Types:

- File
- Directory
- RegistryKey
- RegistryValue
- Service
- ScheduledTask
- StartupEntry
- Shortcut
- EmptyDirectory
- Unknown

==================================================
CONFIDENCE SCORING
==================================================

Buat scoring engine deterministik dan dapat diuji.

Contoh:

+100 berada tepat di InstallLocation
+60 tercatat oleh Install Monitor session yang sama
+50 executable path menunjuk ke InstallLocation
+45 ProductCode sama
+40 digital signature publisher sama
+35 uninstall registry ownership sama
+30 service ImagePath berada dalam InstallLocation
+30 scheduled task menjalankan executable dalam InstallLocation
+25 shortcut target menunjuk ke InstallLocation
+25 startup entry menunjuk ke InstallLocation
+20 nama folder cocok persis
+20 publisher dan nama program cocok
+15 registry value menunjuk langsung ke executable target
+10 normalized name cocok

Pengurangan:

-30 nama sangat pendek
-40 nama terlalu umum
-50 folder kemungkinan shared
-60 digunakan program lain
-70 Common Files
-80 dependency atau runtime
-100 protected directory
-100 system component
-100 ownership tidak dapat dipastikan

Confidence:

High:
85–100

Medium:
60–84

Low:
0–59

Default selection:

- High confidence dapat dipilih otomatis jika lolos safety policy
- Medium tidak dipilih otomatis
- Low tidak dipilih otomatis
- Protected tidak dapat dipilih

Tampilkan Evidence pada UI.

Contoh:

“High confidence because:
- Folder matches original install location
- Publisher matches executable signature
- Created during monitored installation”

==================================================
CENTRALIZED SAFETY POLICY
==================================================

Safety policy wajib dievaluasi sebelum:

- Menampilkan sebagai auto-selected
- Memindahkan file
- Menghapus registry
- Menghapus service
- Menghapus task
- Menjalankan elevated operation
- Restore
- Permanent delete

Protected path:

- Windows directory
- System32
- SysWOW64
- WinSxS
- Windows Installer
- Servicing
- DriverStore
- Boot
- EFI
- Recovery
- System Volume Information
- Recycle Bin system folder
- Root drive
- User profile root
- Program Files root
- Program Files x86 root
- ProgramData root
- Common Files
- Common Files x86
- Documents
- Downloads
- Pictures
- Videos
- Music
- Desktop kecuali shortcut tervalidasi

Protected concepts:

- Windows
- Microsoft
- System
- Common
- Shared
- Runtime
- Framework
- Driver
- Component Store
- Security
- Defender
- Edge WebView
- Visual C++
- .NET
- Windows App Runtime

Nama saja tidak cukup untuk menentukan ownership.

Aturan wajib:

- Canonicalize path.
- Resolve environment variable.
- Tolak path traversal.
- Jangan mengikuti symbolic link.
- Jangan mengikuti junction.
- Jangan mengikuti mount point.
- Deteksi reparse point.
- Pastikan target tetap berada dalam approved root.
- Elevated helper wajib melakukan validasi ulang.
- Jangan menghapus shared DLL.
- Jangan menghapus file yang dipakai program lain tanpa warning.
- Jangan menghapus user-generated content.
- Jangan menghapus save game otomatis.
- Jangan menghapus browser profile.
- Jangan menghapus password browser.
- Jangan menghapus database shared.
- Jangan menghapus driver package.
- Jangan menghapus antivirus.
- Jangan menghapus anti-cheat.
- Jangan menghapus VPN/network filter.
- Jangan menghapus Visual C++ Redistributable.
- Jangan menghapus .NET Runtime.
- Jangan menghapus Windows App SDK runtime.
- Jangan menghapus Windows Feature.

Buat protected publisher list.

Buat protected application list.

==================================================
FORCE UNINSTALL
==================================================

Buat halaman Force Uninstall.

UI:

- Drag-and-drop zone
- Browse executable
- Browse folder
- Select running process
- Select broken uninstall entry

User dapat memasukkan:

- EXE
- Shortcut
- Program folder
- Running process
- Broken registry entry

Workflow:

1. Identifikasi executable atau folder.
2. Ambil metadata.
3. Cari digital signature.
4. Cari publisher.
5. Cari uninstall registry entry.
6. Cari related processes.
7. Cari services.
8. Cari scheduled tasks.
9. Cari startup entries.
10. Cari shortcuts.
11. Cari AppData dan ProgramData.
12. Hitung confidence.
13. Tampilkan preview.
14. Protected item tidak dapat dipilih.
15. Pindahkan item terpilih ke quarantine.
16. Simpan report.

Force Uninstall tidak boleh berarti “hapus semua file yang namanya mirip”.

Harus tetap evidence-based.

==================================================
INSTALL MONITOR
==================================================

Buat halaman Install Monitor.

Sediakan:

- Start Monitoring Installation
- Drag installer here
- Browse installer
- Logged Programs
- Previous Sessions
- Session Details
- Delete Log
- Export Log

Workflow:

1. User memilih installer.
2. Validasi installer.
3. Ambil snapshot sebelum install.
4. Jalankan installer.
5. Monitor proses installer dan child process.
6. Catat perubahan file secara best effort.
7. Catat perubahan registry secara best effort.
8. Catat service baru.
9. Catat scheduled task baru.
10. Catat startup entry baru.
11. Ambil snapshot setelah install.
12. Bandingkan snapshot.
13. Simpan installation manifest.
14. Hubungkan manifest ke program yang terdeteksi.
15. Tandai program sebagai Logged Program.

Jangan membuat kernel driver.

Gunakan pendekatan aman:

- Pre-install snapshot
- Post-install snapshot
- FileSystemWatcher sebagai data tambahan
- Registry snapshot pada scope relevan
- Process tree monitoring
- Service before/after diff
- Scheduled Task before/after diff
- Startup entry before/after diff

Jangan snapshot seluruh disk byte-per-byte.

Batasi snapshot ke:

- Program Files
- Program Files x86
- ProgramData
- LocalAppData
- RoamingAppData
- Start Menu
- Desktop shortcuts
- Startup folders
- Relevant registry uninstall keys
- Relevant registry software keys
- Service inventory
- Scheduled Task inventory

Manifest:

- SessionId
- InstallerPath
- InstallerHash
- InstallerPublisher
- StartedAt
- CompletedAt
- RootProcessId
- ChildProcesses
- CreatedFiles
- ModifiedFiles
- CreatedDirectories
- RegistryChanges
- NewServices
- NewScheduledTasks
- NewStartupEntries
- DetectedApplications
- Warnings
- IncompleteMonitoringReason

Tampilkan dengan jujur ketika monitoring tidak lengkap.

==================================================
BUNDLEWARE DETECTION
==================================================

Tandai “Possible Bundleware” jika:

- Beberapa program muncul dalam installation session yang sama
- Publisher berbeda
- Child installer berbeda
- Installation timestamp sangat berdekatan
- Program tambahan tidak cocok dengan nama installer utama
- Install Monitor mencatat multiple product registrations

Jangan menyebut malware.

Gunakan label:

- Possible bundle
- Installed in same session
- Additional program
- Unknown relationship

User harus memutuskan sendiri apakah ingin menghapusnya.

==================================================
RESIDUAL SCAN
==================================================

Buat halaman Residual Scan untuk mendeteksi sisa program yang sudah pernah dihapus.

Scan:

- Broken uninstall entries
- Missing uninstall executable
- Folder program tanpa executable utama
- Service dengan missing ImagePath
- Scheduled Task dengan missing target
- Startup entry dengan missing target
- Broken shortcut
- Empty application directory
- Registry entry yang mengarah ke file tidak ada
- Orphaned install monitor manifest
- Old quarantine metadata

Gunakan safety dan confidence scoring yang sama.

Jangan menghapus otomatis.

Tampilkan:

- Item
- Type
- Evidence
- Confidence
- Risk
- Size
- Location
- Recommended action

==================================================
SOFTWARE HEALTH
==================================================

Buat halaman Software Health.

Ini bukan fitur marketing.

Tampilkan hasil yang dapat dibuktikan:

- Broken uninstall entries
- Programs with detected leftovers
- Programs with missing uninstallers
- Possible bundleware
- Logged installations
- High-risk browser extensions
- Broken startup entries
- Broken scheduled tasks
- Expired quarantine
- Large unused programs
- Recently installed programs

Setiap card atau row harus membuka detail yang relevan.

Gunakan istilah:

- Needs review
- Attention recommended
- No action required
- Unable to determine

Jangan menggunakan klaim:

- PC 200% faster
- Boost performance
- Repair all problems
- Dangerous tanpa bukti
- Malware tanpa reputation source

==================================================
BROWSER EXTENSIONS
==================================================

Dukung:

- Google Chrome
- Microsoft Edge
- Mozilla Firefox
- Chromium-based browser yang dapat dideteksi

Tampilkan:

- Browser
- Profile
- Extension name
- Extension ID
- Version
- Description
- Install source
- Extension folder
- Permissions jika dapat dibaca
- Managed by policy
- Developer mode
- Unpacked
- Enabled status jika dapat ditentukan
- Risk indicators

Risk indicator berbasis lokal:

- High permission count
- Installed by policy
- Unpacked extension
- Unknown publisher
- Missing manifest metadata
- Extension folder broken

Jangan menuduh extension sebagai malware tanpa sumber reputasi.

Removal:

- Jangan menghapus managed extension.
- Jangan mengubah browser policy.
- Jangan menghapus extension ketika browser aktif tanpa warning.
- Backup metadata.
- Prefer membuka browser extension management page jika direct removal tidak aman.
- Direct removal hanya untuk user-installed unmanaged extension setelah explicit confirmation.
- Jangan menghapus browser profile, cookies, password, bookmark, atau history.

==================================================
JUNK CLEANER
==================================================

Halaman Junk Cleaner terpisah.

Kategori aman:

- User Temp
- Windows Temp yang dapat diakses
- Application crash dumps
- Old application logs
- Thumbnail cache
- Old installer temporary files
- Empty temporary directories
- Uninstra old logs
- Expired quarantine
- Broken shortcuts
- Temporary report files

Setiap kategori:

- Description
- Item count
- Detected size
- Risk
- Requires elevation
- Preview
- Scan
- Clean

Jangan membuat generic Registry Cleaner.

Jangan membersihkan:

- Browser password
- Browser session
- Browser profile
- User Documents
- Downloads
- Prefetch secara default
- Event Logs secara default
- Restore Points
- WinSxS
- DriverStore
- Windows Installer cache
- Windows Update component store secara manual
- Application database aktif

Locked file:

- Skip secara default.
- Tampilkan warning.
- Jangan force delete.
- Delete on reboot hanya dengan explicit confirmation dan safety validation.

==================================================
QUARANTINE
==================================================

Semua file destructive cleanup dipindahkan ke quarantine terlebih dahulu.

Lokasi:

%PROGRAMDATA%\Uninstra\Quarantine

Struktur:

Quarantine/
  {OperationId}/
    manifest.json
    files/
    registry/
    metadata/

Manifest:

- OperationId
- ApplicationId
- ApplicationName
- CreatedAt
- ExpiresAt
- OriginalPath
- QuarantinePath
- Size
- Hash
- ItemType
- RegistryBackup
- CanRestore
- RestoreWarnings

Default retention:
14 hari

Settings:
- 7 hari
- 14 hari
- 30 hari
- Never auto-delete

Halaman Quarantine:

- Group by operation
- Search
- Original location
- Size
- Date
- Expiry
- Restore
- Delete permanently
- Select multiple
- Empty expired items
- Empty all

Permanent delete wajib meminta konfirmasi.

==================================================
REGISTRY BACKUP DAN RESTORE
==================================================

Sebelum registry item dihapus:

- Simpan hive
- Key path
- Value name
- Value kind
- Serialized value
- Subkeys jika relevan
- Timestamp
- Related application
- Operation ID

Restore harus:

- Memvalidasi target
- Meminta elevation jika perlu
- Menangani key yang sudah dibuat ulang
- Meminta konfirmasi sebelum overwrite
- Mencatat hasil

Jangan menyimpan registry value sensitif ke log biasa.

==================================================
SYSTEM RESTORE POINT
==================================================

Sebelum deep cleanup, coba membuat restore point:

“Uninstra - Before removing {ApplicationName}”

Jika System Protection nonaktif:

- Tampilkan warning.
- Jangan crash.
- User dapat melanjutkan.
- Jelaskan bahwa quarantine tetap tersedia.

Restore point bukan satu-satunya rollback.

==================================================
ELEVATED HELPER
==================================================

Jangan menjalankan UI utama sebagai administrator.

Uninstra.App:
standard user

Uninstra.ElevatedHelper:
administrator only when required

Komunikasi menggunakan named pipe.

Request:

- RequestId
- SessionId
- Nonce
- OperationType
- TypedPayload
- Timestamp

Response:

- RequestId
- Success
- ErrorCode
- Message
- TechnicalDetails
- ResultPayload

Allowed operations:

- MoveToQuarantine
- RestoreFromQuarantine
- PermanentlyDeleteQuarantineItem
- DeleteRegistryKey
- DeleteRegistryValue
- RestoreRegistryItem
- StopService
- DeleteService
- DeleteScheduledTask
- RemoveStartupEntry
- CreateRestorePoint

Dilarang:

- Raw shell command
- Arbitrary command text
- PowerShell script
- cmd.exe command
- Arbitrary executable launch
- Unvalidated path
- Arbitrary registry operation

Named pipe:

- Current user ACL
- Session validation
- Timeout
- Cancellation
- Audit logging
- Replay protection sederhana dengan nonce
- Reject stale request

Elevated helper wajib mengevaluasi safety policy ulang.

==================================================
HISTORY
==================================================

Simpan di SQLite.

History fields:

- OperationId
- OperationType
- ApplicationId
- ApplicationName
- Publisher
- StartedAt
- CompletedAt
- Status
- ExitCode
- ItemsDetected
- ItemsCleaned
- ItemsSkipped
- RecoveredBytes
- RestorePointStatus
- QuarantineAvailable
- WarningCount
- ErrorCount
- ReportPath

Halaman History:

- Filter date
- Filter operation
- Search
- Open report
- View details
- Restore related items
- Delete history record
- Export

Menghapus history tidak boleh otomatis menghapus quarantine.

==================================================
REPORT
==================================================

Buat report:

- JSON
- HTML mandiri tanpa dependency eksternal

Report berisi:

- Application
- Publisher
- Version
- Installer type
- Uninstall command sanitized
- Start time
- End time
- Exit code
- Verification result
- Restore point status
- Leftovers detected
- Evidence
- Confidence
- Items cleaned
- Items skipped
- Warnings
- Errors
- Recovered bytes
- Quarantine path
- Rollback status

Lokasi default:

Known Folder Documents\Uninstra Reports

Gunakan Known Folder API atau Environment.GetFolderPath.

Jangan hardcode:

C:\Users\Username\Documents

==================================================
DATABASE DAN STORAGE
==================================================

SQLite digunakan untuk:

- Installed application cache
- History
- Quarantine metadata
- Installation monitor sessions
- Logged programs
- Settings
- Cleanup statistics
- Registry backup metadata
- Residual scan result cache

Lokasi per-user:

%LOCALAPPDATA%\Uninstra

Logs:

%LOCALAPPDATA%\Uninstra\Logs

Settings:

%LOCALAPPDATA%\Uninstra\settings.json

Shared quarantine:

%PROGRAMDATA%\Uninstra\Quarantine

Implementasikan schema initialization dan migration.

Gunakan transaction.

Jangan biarkan database corruption membuat aplikasi gagal dibuka total.

==================================================
SETTINGS
==================================================

Settings page:

General:
- Start page
- Language architecture-ready
- Confirm before uninstall
- Confirm before cleanup
- Refresh after uninstall

Appearance:
- Dark
- Light
- System
- Accent color
- Compact density

Scanning:
- Include medium confidence
- Scan AppData
- Scan ProgramData
- Scan registry
- Scan services
- Scan scheduled tasks
- Scan startup
- Show system components

Safety:
- Create restore point
- Quarantine retention
- Allow permanent deletion
- Advanced mode
- Show protected items

Logging:
- Log level
- Open log folder
- Clear old logs

Privacy:
- Local-only statement
- No telemetry
- No analytics
- No ads

==================================================
LOGGING
==================================================

Gunakan structured logging.

Log:

- Startup
- App version
- Windows version
- Scan start/end
- Scan duration
- Number of apps
- Uninstall operation
- Exit code
- Verification
- Leftover result
- Quarantine
- Restore
- Elevated helper request type
- Error
- Warning

Jangan log:

- Password
- Browser password
- Token
- Cookie
- Personal file content
- Sensitive environment variable
- Full sensitive registry data
- Private document content

Rotate log files.

==================================================
ERROR HANDLING
==================================================

Buat:

OperationResult
OperationResult<T>
ErrorCode
ErrorDetails

Tangani:

- Access denied
- Registry missing
- Uninstaller missing
- Invalid command
- Process launch failure
- UAC cancelled
- Locked file
- Long path
- Invalid path
- Reparse point
- Database error
- Disk full
- Quarantine move failure
- Restore failure
- Service error
- Scheduled Task error
- AppX removal error
- User cancellation
- Helper timeout
- Named pipe failure
- Browser profile locked
- Installer monitor incomplete

Tambahkan global exception handling:

- DispatcherUnhandledException
- AppDomain.UnhandledException
- TaskScheduler.UnobservedTaskException

Destructive operation wajib berhenti jika validasi gagal.

==================================================
PERFORMANCE
==================================================

Gunakan:

- Data virtualization
- Incremental loading jika perlu
- Background scanning
- CancellationToken
- Progress reporting
- Caching
- Batched database writes
- Debounced search
- Lazy icon extraction

Jangan:

- Membaca semua file seluruh disk saat startup
- Menghitung ukuran seluruh Program Files secara langsung
- Memblokir UI thread
- Mengambil icon berulang kali
- Membuka semua registry key berulang kali

Startup harus menampilkan UI lebih dulu lalu melakukan scan asynchronously.

==================================================
ACCESSIBILITY
==================================================

Implementasikan:

- Keyboard navigation
- Focus indicators
- Accessible names
- Screen reader labels
- Sufficient contrast
- Scalable text
- High DPI
- Tab order
- Escape untuk menutup dialog
- Enter untuk primary action
- Space untuk checkbox

==================================================
TESTING
==================================================

Buat unit test untuk:

Installed applications:
- Registry parsing
- Registry32
- Registry64
- EstimatedSize
- Stable ID
- Deduplication
- Categories
- Protected app detection

Command parsing:
- Quoted executable
- Unquoted executable
- Path with spaces
- Arguments
- MSI
- Malformed command
- Missing executable

Scoring:
- Exact install location
- Publisher match
- ProductCode match
- Shared folder penalty
- Protected path
- Confidence classification

Safety:
- Path canonicalization
- Traversal
- Junction abstraction
- Reparse point
- Windows folder
- Common Files
- User document protection
- Root path rejection

Quarantine:
- Manifest serialization
- Restore metadata
- Expiry
- Hash metadata
- Collision handling

Install Monitor:
- Before/after diff
- New file
- New registry
- New service
- New task
- Multiple detected applications

Batch uninstall:
- Queue
- Skip
- Cancel
- Failure continuation
- Summary

Browser extensions:
- Manifest parsing
- Managed detection
- Broken manifest
- Permission reading

Junk cleaner:
- Safe temp detection
- Locked file handling
- Protected path rejection

Gunakan abstraction dan fake untuk:

- Registry
- File system
- Process runner
- Service manager
- Task scheduler
- Package manager
- Restore point
- Browser profile
- Elevated helper
- Clock

Integration test tidak boleh menghapus aplikasi nyata.

Gunakan:

- Temporary directory
- Fake registry
- Fake service list
- Fake scheduled tasks
- Dummy uninstall executable

Semua test harus dapat dijalankan dengan:

dotnet test

==================================================
DUMMY TEST APPLICATION
==================================================

Buat project test utility opsional:

tools/
  Uninstra.DummyApp/
  Uninstra.DummyInstaller/

Dummy app harus dapat membuat:

- Install folder dalam temporary test location
- AppData test folder
- ProgramData test folder jika diizinkan
- Registry test key di HKCU khusus UninstraTests
- Shortcut test
- Startup test entry opsional
- Uninstaller test

Dummy app hanya digunakan untuk testing.

Semua nama harus jelas mengandung:

UninstraTest

Jangan menyentuh key atau folder production.

==================================================
PACKAGING
==================================================

Buat Release publish:

- win-x64
- self-contained
- single-folder
- Jangan gunakan single-file jika mengganggu WPF atau helper
- Include ElevatedHelper
- Include database initialization
- Include icons
- Include license
- Include documentation ringkas

Command:

dotnet publish src/Uninstra.App/Uninstra.App.csproj \
  -c Release \
  -r win-x64 \
  --self-contained true

Buat Inno Setup:

installer/Uninstra.iss

Fitur installer:

- Install per-machine
- Start Menu shortcut
- Desktop shortcut optional
- Application icon
- Version
- Publisher
- Uninstall entry
- Upgrade existing version
- Jangan menghapus quarantine tanpa konfirmasi
- Jangan menghapus reports tanpa konfirmasi
- Jangan menghapus user settings tanpa checkbox

==================================================
README DAN DOKUMENTASI
==================================================

Buat README.md:

- Logo
- Tagline
- Description
- Screenshots placeholder
- Features
- Program categories
- Deep uninstall workflow
- Install Monitor
- Force Uninstall
- Residual Scan
- Software Health
- Browser Extensions
- Junk Cleaner
- Safety model
- Requirements
- Build
- Test
- Publish
- Installer
- Project structure
- Privacy
- Known limitations
- Contributing
- Disclaimer
- License

License:
MIT

Privacy statement:

“Uninstra berjalan secara lokal. Uninstra tidak mengirim daftar aplikasi, file, browser extension, atau aktivitas pengguna ke server.”

Disclaimer:

- User harus meninjau leftover sebelum cleanup.
- Tidak ada uninstaller yang dapat menjamin semua leftover aman dihapus.
- Runtime dan system component harus ditangani dengan hati-hati.
- Backup tetap direkomendasikan.
- Pengguna bertanggung jawab atas item yang dipilih secara manual dalam Advanced Mode.

==================================================
CODING RULES
==================================================

- Jangan taruh business logic di code-behind.
- Jangan membuat god class.
- Jangan menggunakan global mutable state.
- Jangan menggunakan service locator.
- Gunakan constructor injection.
- Gunakan interface pada system boundary.
- Gunakan immutable model jika cocok.
- Gunakan record jika cocok.
- Gunakan file-scoped namespace.
- Gunakan async/await.
- Jangan gunakan .Result atau .Wait().
- Jangan gunakan Thread.Sleep.
- Async void hanya untuk UI event handler.
- Gunakan CancellationToken.
- Gunakan IProgress atau typed progress event.
- Jangan shell command interpolation.
- Jangan gunakan cmd.exe untuk file operation.
- Jangan gunakan PowerShell jika ada API resmi.
- Jangan hardcode username.
- Jangan hardcode user folder.
- Jangan menelan exception.
- Jangan meninggalkan tombol placeholder.
- Jangan meninggalkan TODO pada fitur utama.
- Jangan menggunakan production dummy data.
- Semua destructive method wajib memiliki safety validation.
- Semua elevated operation wajib divalidasi ulang.
- Gunakan dotnet format.
- Perbaiki compiler warning penting.
- Tambahkan XML documentation untuk public API penting.

==================================================
URUTAN PENGERJAAN
==================================================

Kerjakan langsung.

1. Periksa environment.
2. Periksa .NET SDK.
3. Buat solution.
4. Buat semua project.
5. Tambahkan references.
6. Tambahkan packages.
7. Implementasikan domain model.
8. Implementasikan result type.
9. Implementasikan safety policy.
10. Implementasikan confidence scoring.
11. Implementasikan registry application scanner.
12. Implementasikan command parser.
13. Implementasikan normal uninstall.
14. Implementasikan MSI uninstall.
15. Implementasikan AppX/MSIX support.
16. Implementasikan batch uninstall queue.
17. Implementasikan leftover scanner.
18. Implementasikan quarantine.
19. Implementasikan registry backup.
20. Implementasikan elevated helper.
21. Implementasikan restore point.
22. Implementasikan force uninstall.
23. Implementasikan install monitor.
24. Implementasikan bundleware detection.
25. Implementasikan residual scan.
26. Implementasikan software health.
27. Implementasikan Windows Apps.
28. Implementasikan browser extensions.
29. Implementasikan junk cleaner.
30. Implementasikan SQLite storage.
31. Implementasikan history.
32. Implementasikan report.
33. Implementasikan settings.
34. Implementasikan WPF shell.
35. Implementasikan sidebar.
36. Implementasikan Programs table.
37. Implementasikan detail panel.
38. Implementasikan batch action bar.
39. Implementasikan deep uninstall stepper.
40. Implementasikan semua halaman.
41. Hubungkan dependency injection.
42. Buat test.
43. Jalankan dotnet restore.
44. Jalankan dotnet build.
45. Jalankan dotnet test.
46. Jalankan dotnet format.
47. Build ulang.
48. Perbaiki error dan test failure.
49. Buat Release publish.
50. Buat Inno Setup script.
51. Buat README.
52. Buat dokumentasi.
53. Tampilkan hasil akhir.

Jangan berhenti hanya karena satu fitur Windows memerlukan privilege atau API yang sulit.

Jika ada API yang tidak tersedia:
- Buat fallback yang aman.
- Dokumentasikan limitation.
- Jangan membuat implementasi palsu.
- Jangan menampilkan tombol seolah-olah fitur bekerja.

==================================================
DEFINITION OF DONE
==================================================

Project dianggap selesai ketika:

- Solution dapat direstore.
- Solution dapat dibuild.
- Test lulus.
- UI dapat dibuka.
- Sidebar berfungsi.
- Programs table menampilkan program nyata.
- Search berfungsi.
- Filter berfungsi.
- Sorting berfungsi.
- Categories berfungsi.
- App details berfungsi.
- Normal uninstall berfungsi.
- Batch uninstall queue berfungsi.
- Deep uninstall workflow berfungsi.
- Post-uninstall scan berfungsi.
- Leftover evidence ditampilkan.
- Confidence score ditampilkan.
- Protected path tidak dapat dibersihkan.
- Quarantine berfungsi.
- Restore berfungsi.
- Registry backup berfungsi.
- Force Uninstall dapat menerima executable atau folder.
- Install Monitor dapat membuat session dan manifest.
- Logged Programs ditampilkan.
- Residual Scan menghasilkan preview.
- Software Health membuka detail relevan.
- Windows Apps ditampilkan.
- Browser Extensions ditampilkan.
- Junk Cleaner dapat scan dan preview.
- History tersimpan.
- JSON report dibuat.
- HTML report dibuat.
- Settings tersimpan.
- Elevated helper tidak menerima arbitrary shell command.
- Release publish berhasil.
- Installer script tersedia.
- Tidak ada fitur utama berupa tombol kosong.
- Tidak ada destructive operation tanpa preview.
- Tidak ada destructive operation tanpa confirmation.
- Tidak ada destructive operation yang melewati safety policy.

==================================================
OUTPUT AKHIR
==================================================

Setelah implementasi selesai, tampilkan:

1. Ringkasan arsitektur.
2. Struktur directory final.
3. Fitur yang benar-benar bekerja.
4. Fitur yang hanya memiliki fallback.
5. Limitation yang masih ada.
6. Safety mechanism.
7. Hasil dotnet build.
8. Hasil dotnet test.
9. Jumlah test yang lulus.
10. Lokasi output publish.
11. Command menjalankan aplikasi.
12. Command membuat installer.
13. Lokasi database.
14. Lokasi logs.
15. Lokasi quarantine.
16. File penting yang perlu direview.
17. Cara menguji dengan Dummy App.
18. Risiko yang perlu diperhatikan sebelum membagikan aplikasi ke teman.

Mulai sekarang.

Berikan rencana implementasi singkat maksimal 20 baris, lalu langsung buat seluruh solution dan lanjutkan sampai build serta test berhasil.