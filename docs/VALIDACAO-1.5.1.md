# Screen Care 1.5.1 — investigation and validation

[English README](../README.md) | [Português](../README.pt-BR.md)

## Scope

English/Portuguese UI and installer, Ctrl+Alt+F11 delayed monitor-off, unexpected display-wake recovery, local diagnostics and error-handling improvements. No change to the legacy profile location or saved startup identity.

## Findings and changes

The previous implementation issued one power-off request per idle period and did not subscribe its application controller to the existing display-state event. A display waking without input therefore received no follow-up request. The new state machine consumes those events, waits for queued input and retries with backoff. It stops on confirmed display-off, input, pause or session changes. It also retries a request that was never acknowledged. The guard does not reset the inactivity timer or request that Windows keep the screen on.

The exact cause of the reported disappearing tray icon/process has not been established: no matching crash report was obtained. The installed version inspected was older than the published release. Closing settings was tested repeatedly and kept the tray application alive. This investigation does not claim that every possible unexpected exit is fixed.

Previously, the UI exception handler shut down the app before recording the exception. Errors are now recorded first; an additional process-level handler covers unhandled managed background exceptions. Recoverable UI errors pause automation and stop its timer while retaining the tray icon. Fatal errors, or another error within 60 seconds, use normal fatal-error handling. Diagnostics are local, bounded and excluded from releases.

Ctrl+Alt+F11 schedules monitor-off two seconds later even if slideshow is selected or automatic monitoring is paused. It does not save a different mode. Shortcut collisions are independent of Ctrl+Alt+F12 and do not abort startup. The tray menu provides an alternative.

## Follow-up investigation: background video

An installed 1.5.0 build was tested with YouTube in Firefox. Local display-state records showed both immediate external wake-ups (where retries did not keep the display off) and action cancellation after an input event. A temporary, read-only input probe distinguished event categories without recording key codes, typed text, coordinates or device identifiers. Retests generally kept the display off; the user reported one intermittent recurrence and then could not reproduce it in the detailed diagnostic round. Captured returns in that detailed round were preceded by valid keyboard-press events. This does not establish the cause of the earlier uncommanded wake-up.

Version 1.5.1 keeps the system, but not the display, awake during explicit manual monitor-off so background playback can continue. Automatic monitor-off retains the previous sleep policy. Overrun/unmapped keyboard reports are rejected, and the bounded local event log identifies the input category that canceled an active action plus changes to the app's own power requests. These are tested improvements, not proof that every intermittent wake-up is fixed. No Firefox settings, system-wide power overrides or media playback controls were changed.

## Validation

413 checks passed in an interactive Windows session. The security tests also passed; the final source privacy scan reported 43 files and zero findings. The native hotkey ownership lifecycle test was skipped because the installed copy already owned the shortcuts; collision handling and shortcut dispatch were tested.

Regression tests cover language-only persistence, old XML settings, unchanged user-entered paths, invalid draft edits, translating back and forth, English/Portuguese UI rendering, native shortcut dispatch/collisions, delayed actions, display-wake/input races, retry backoff, display-off acknowledgement, repeated settings closure, recoverable errors and bounded local logging. Native monitor-off commands are canceled in the integration test; no physical display is powered down by the tests.

Build: .NET Framework 4.8, x64, compiler warnings as errors. Security tests check native library loading from Windows and reject direct/ancestor junctions. Release packaging compares ZIP entries and installer payload hashes with the tested build. The publication check excludes private files, logs, screenshots with personal paths and development artifacts.

## Limits

Physical behavior across monitors, docks, drivers, apps that continually wake displays, Windows security policy and sleep/resume needs validation on each target. User-assisted physical retests used installed 1.5.0; the final 1.5.1 changes passed automated checks and have not yet been installed or physically retested on this computer. No claim of absolute burn-in prevention or universal override of Windows/driver behavior is made. Executables are not Authenticode-signed.

## Windows API references

- [Display state notifications](https://learn.microsoft.com/en-us/windows/win32/power/power-setting-guids)
- [SC_MONITORPOWER](https://learn.microsoft.com/en-us/windows/win32/menurc/wm-syscommand)
- [SetThreadExecutionState](https://learn.microsoft.com/en-us/windows/win32/api/winbase/nf-winbase-setthreadexecutionstate)
- [Keyboard overrun and unmapped input](https://learn.microsoft.com/en-us/windows/win32/api/winuser/ns-winuser-rawkeyboard)
- [Firefox's display power requests during video playback](https://github.com/mozilla-firefox/firefox/blob/main/widget/windows/nsAppShell.cpp)

## Resumo em português

Foi corrigida a falta de reação ao reacendimento sem atividade, com novas tentativas espaçadas e cancelamento por entrada do usuário. O atalho Ctrl+Alt+F11 desliga as telas em dois segundos e preserva as preferências. A interface e o instalador passam a oferecer inglês e português.

A causa exata do desaparecimento do ícone não foi confirmada. Erros recuperáveis agora pausam a automação mantendo o app aberto, e os registros são gravados antes da limpeza de recursos. Testes verificam fechamento de configurações sem encerrar o app, persistência dos idiomas, atalhos, recuperação de energia e segurança.

Nos testes físicos com a 1.5.0 instalada e vídeo no Firefox, o usuário relatou uma ocorrência intermitente, mas não conseguiu repeti-la na última rodada detalhada. As retomadas capturadas nessa rodada vieram após eventos válidos de pressionar tecla; a causa da ocorrência espontânea anterior continua sem confirmação. A 1.5.1 mantém apenas o sistema acordado no desligamento manual, ignora relatórios inválidos do teclado e melhora o diagnóstico local. Passou por 413 verificações, mas ainda precisa ser instalada e testada fisicamente. Os logs ficam apenas no computador e precisam ser revisados antes de compartilhados.
