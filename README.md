# Screen Care

**English** | [Português (Brasil)](README.pt-BR.md)

Screen Care is a Windows tray app developed to help protect monitors from **burn-in, image retention and other effects of prolonged static images**. It reduces static-image exposure by turning off your displays or showing local photos after a period without real user activity.

It sends its own monitor-off request, independently of the Windows power plan's idle timeout. This lets it act when background applications keep the screen awake and normal Windows display sleep does not happen. If a display wakes without user activity, Screen Care retries with progressively longer intervals. User input, suspension and session changes stop the retries. Physical behavior depends on Windows, drivers and monitor hardware; the app cannot guarantee prevention of every wake-up or all burn-in.

Open source, distributed under the [MIT license](LICENSE).

## Download

Get the [installer or portable version from Releases](https://github.com/lucasmas16/screen-care/releases/latest).

- **Installer:** download `ScreenCare-Setup.exe` and `ScreenCare-Setup.exe.config` to the same folder, then run the installer.
- **Portable:** extract `ScreenCare-Portatil.zip` to a permanent folder and run `ScreenCare.exe`.
- Packages include the MIT license and the bilingual [user guide](LEIA-ME.txt). Use `SHA256SUMS.txt` to verify downloads.

Requires Windows 10/11 x64 and .NET Framework 4.8 or later. Executables do not yet have an Authenticode publisher signature.

## What's new in 1.5.1

- **English and Portuguese:** choose the language in settings or the installer. It applies and saves immediately without discarding pending edits. On first use, Portuguese Windows locales select Portuguese; other locales select English.
- **Ctrl+Alt+F11:** turn off displays after two seconds, even when slideshow mode is selected or automation is paused. The tray menu offers the same action. **Ctrl+Alt+F12** still toggles automation and cancels a pending manual countdown.
- **Wake recovery:** monitor state notifications trigger a new power-off request when a display wakes without input. A three-second input grace period and retry delays of 5, 15, 30 and then 60 seconds avoid rapid command loops. Confirmed display-off stops retries.
- **Error handling:** recoverable UI errors pause automation while keeping the tray app available. Errors are logged before cleanup, and a small local event log records starts, shutdowns and monitor state changes.
- The exact cause of a reported disappearing tray icon has not been confirmed. See the [investigation, validation and limits](docs/VALIDACAO-1.5.1.md).

## Features and usage

- Turn off all monitors or show local images across displays.
- Activity from keyboard, mouse, touchpad, DualSense, XInput and compatible WinMM controllers resets the timer. Connected but idle controllers and minor analog drift do not count.
- Configure a timeout from 10 seconds to 24 hours. For slideshows, choose a local folder, a 2–3600 second interval, optional subfolders and shuffle.
- Light/dark themes, saved preferences and optional startup with Windows.
- Lock, unlock, suspend/resume and display topology handling. When Windows is locked, both modes turn off displays; private photos are shown only in the unlocked session.
- Notifications appear at the bottom center of the primary monitor without stealing focus.

Double-click the tray icon to open settings; right-click for actions or Exit. Closing settings keeps the app running. Input after activation restores normal monitoring. The manual shortcut does not change your saved mode or automation preference.

Turning off displays does not shut down the computer. Manual monitor-off (Ctrl+Alt+F11 or its menu action) keeps only the computer awake so background playback can continue, until input or cancellation. Automatic monitor-off releases the app's stay-awake request. Security policies, manual suspension and critical battery behavior still take priority. A slideshow changes visible content but does not provide the same reduction in panel use as turning displays off.

## Privacy and diagnostics

No telemetry, HTTP client, server or internet connection is used by the application. Photos remain in your selected folders. Input is detected in memory without recording keys, typed text or cursor positions. User-selected network folders are accessed through Windows and may cause operating-system network authentication.

Settings stay under `%LOCALAPPDATA%\MonitorPausa`; the legacy directory name preserves upgrades from MonitorPausa. `diagnostics.log` contains timestamps and fixed event codes, including the input category that ended an active action and changes to the app's power requests. It is limited to approximately 64 KB and is never uploaded. Individual keys, pointer coordinates and device identifiers are not logged. `last-error.txt` may contain local paths from exceptions: review it before sharing. See [privacy details](docs/PRIVACIDADE.md).

## Build and test

C#, Windows Forms, .NET Framework 4.8 and native Windows APIs; no NuGet dependencies.

```powershell
powershell -ExecutionPolicy Bypass -File .\build.ps1 -RunTests
.\dist\ScreenCare.exe
```

The installer is generated at `dist/ScreenCare-Setup.exe`. Use `-OutputDirectory` for another build folder.

Before publishing, run `node tools/check-privacy.cjs` and review the diff. After building tests, run `powershell -ExecutionPolicy Bypass -File .\tools\test-security.ps1`. UI and global-shortcut tests require an interactive Windows session. Automated power tests exercise state transitions and cancel native monitor commands; physical display behavior needs testing on the target hardware.

## Source layout

- `src/IdleController.cs`: inactivity state machine.
- `src/MonitorPowerGuard.cs`: wake recovery and delayed manual actions.
- `src/Native.cs`, `src/SessionState.cs`: input, power and session integration.
- `src/Settings.cs`, `src/SettingsForm.cs`: preferences and settings UI.
- `src/Localization.cs`: English/Portuguese catalog and controls.
- `src/Diagnostics.cs`: bounded local event logging.
- `src/Images.cs`, `src/Slideshow.cs`: image catalog and presentation.
- `src/TrayApplication.cs`: tray app lifecycle and orchestration.
- `setup/Setup.cs`, `tests/Tests.cs`: installer and regression tests.

Additional documentation: [architecture](docs/ARQUITETURA.md), [controller support](docs/CONTROLES.md), [contributing](CONTRIBUTING.md), [security reporting](SECURITY.md) and [1.5.1 validation](docs/VALIDACAO-1.5.1.md).

## License

[MIT](LICENSE): use, modify and redistribute, including commercially, while retaining the copyright and license notice. Windows and .NET components retain their own terms. Organization references: [Windows Forms samples](https://github.com/dotnet/samples/tree/main/windowsforms) and [GitHub ignore templates](https://github.com/github/gitignore).
