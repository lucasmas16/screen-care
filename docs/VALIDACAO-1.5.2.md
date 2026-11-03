# Screen Care 1.5.2 — overlay and application lifetime

## Changes

The status alert remains at the bottom center of the primary monitor, replaces the previous alert immediately and expires after three seconds. While visible, it reasserts topmost z-order every 100 ms using `SWP_NOACTIVATE`. Its layered, transparent and no-activate window styles let mouse input reach the underlying process without changing foreground focus. The existing fully opaque appearance is retained. The timer is disposed with the alert.

True exclusive fullscreen is outside this guarantee: an ordinary desktop window cannot universally overlay an exclusive presentation surface. Borderless fullscreen is supported. The application does not inject graphics into games, steal focus, minimize them, change their display mode or bypass protected desktops.

## Exit-path review

The former recovery handler deliberately returned failure for a second operational error within 60 seconds; the global handler then shut down the process. That threshold now limits recovery notifications instead of forcing termination. File access, permission, GDI/Win32 and recoverable UI argument/state errors pause automation, cancel queued power actions, release power requests and remove failing UI while retaining the tray and shortcuts. Restarting automation requires an explicit user action. Recovery does not overwrite the saved monitoring preference.

Notification construction/show failures are isolated from monitoring. Optional UI failures during recovery cannot prevent the remaining cleanup steps. Recoverable errors while opening the initial settings window also retain the tray. Explicit shutdown attempts each resource cleanup independently and always exits the message loop. New diagnostic event codes distinguish tray-menu exits, fatal UI errors and startup/message-loop failures.

The local logs inspected did not provide a recorded exception establishing the cause of the previously reported disappearing application. These are fixes to identified failure paths, not a retrospective diagnosis of that incident.

Fatal resource exhaustion, native process corruption, unhandled worker exceptions, initialization failures before the tray is ready, Windows shutdown/logoff and external process termination can still end the application. Such conditions cannot safely be solved by swallowing every exception or restarting after every exit. The explicit `--quit` command and installer shutdown remain supported. Closing settings is not an application exit.

## Validation

Final source build: 437 checks passed with compiler warnings treated as errors. Security tests passed (direct/ancestor junction rejection and system-only native DLL loading). Privacy scan: 45 files, zero findings. The installed copy already owned global shortcuts, so the ownership lifecycle test was skipped; native dispatch and collision handling were tested.

The 15-second native tray smoke passed. Portable ZIP contents and installer payload hashes matched the final build, with assembly identity `ScreenCare, Version=1.5.2.0`. Packages were prepared locally under `release/1.5.2`; the installed application was not updated and nothing was published remotely.

The regression suite tests a separate animated process whose window covers the primary display and is topmost. It checks real desktop pixels, native z-order after the target reasserts topmost, cross-process mouse hit-testing, unchanged foreground during display/replacement/expiration and continued animation. Foreground activation and input-thread attachment are confined to test setup; production alert code never uses them.

Additional checks cover repeated operational failures, disposal of failing settings UI, notification throttling, retained tray visibility, reopening settings, fatal exception exclusions, settings-close lifetime, power-request cancellation and existing controls/settings/image tests. Native monitor-off commands are canceled during integration tests.

This is an automated borderless Windows test, not a test of a particular game, anti-cheat system, exclusive fullscreen renderer or video player. No installed profile is modified by the test suite.

## API references

- [Windows window features: layered window mouse pass-through](https://learn.microsoft.com/en-us/windows/win32/winmsg/window-features)
- [Extended window styles](https://learn.microsoft.com/en-us/windows/win32/winmsg/extended-window-styles)
- [DXGI flip model and borderless fullscreen composition](https://learn.microsoft.com/en-us/windows/win32/direct3ddxgi/for-best-performance--use-dxgi-flip-model)

## Resumo em português

Alerta reforçado sobre tela cheia sem bordas, com passagem dos cliques e preservação do foco. Tela cheia exclusiva real pode impedir a sobreposição. A recuperação não encerra mais o programa só porque uma segunda falha operacional ocorreu em menos de um minuto; pausa a automação e mantém o ícone disponível. A origem do desaparecimento anterior não foi confirmada nos registros consultados. Falhas fatais e encerramentos externos continuam possíveis.
