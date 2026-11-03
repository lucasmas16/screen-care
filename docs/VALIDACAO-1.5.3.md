# Screen Care 1.5.3 — release validation

Version 1.5.3 includes the overlay and recovery changes documented in [1.5.2 validation](VALIDACAO-1.5.2.md), plus the simplified tray menu. The intermediate 1.5.2 packages were local builds; 1.5.3 is the release prepared for publication after 1.5.1.

Publication was deferred to include the startup-caption layout fix. Version 1.5.3 remains an intermediate local build; see [1.5.4 validation](VALIDACAO-1.5.4.md) for the final release.

## Resulting behavior

- “Start now” was removed. The manual action is “Turn off displays in 2 seconds”, also available through Ctrl+Alt+F11. Automatic slideshow remains available in settings.
- Status alerts reassert topmost placement while visible and pass mouse input through without activating their window. They retain their primary-monitor position and three-second duration.
- Repeated recoverable UI errors pause automation while retaining the tray. Notification creation failures and optional recovery steps are isolated. Fatal UI and startup/message-loop failures receive distinct diagnostic event codes.

## Validation

- .NET Framework 4.8, x64 build with warnings treated as errors; 437 checks passed.
- The fullscreen fixture runs in another process. Tests check foreground preservation, mouse hit-testing, z-order recovery, visible desktop pixels, rapid replacement, expiration and continued animation.
- The manual-action test now checks synchronous state before pumping desktop input, preventing unrelated live mouse/keyboard activity from canceling the test action before its assertions.
- Shortcut dispatch and collision handling passed. The native shortcut ownership lifecycle check was skipped because the installed application owned the shortcuts.
- Installer payload and portable ZIP entries match the final build by SHA-256. The release includes an installer, its runtime configuration, portable ZIP, bilingual guide, MIT license and checksum list.
- Source/privacy review excludes local profiles, logs, test captures, development artifacts and private material from publication.

## Limits

True exclusive fullscreen can block ordinary desktop overlays. The automated borderless fixture does not prove compatibility with every game, video renderer or anti-cheat system. The previous disappearing-process report has no confirmed root cause in the inspected logs. Fatal process failures, failed early initialization and external termination remain possible. Executables are not Authenticode-signed. Publication does not update an installed copy or replace hardware-specific validation.

## Português

Menu simplificado, mantendo somente “Desligar telas em 2 segundos — Ctrl+Alt+F11” como ação manual. A versão inclui os alertas sobre tela cheia sem bordas sem roubar foco e a recuperação de erros repetidos mantendo o aplicativo aberto. Foram aprovadas 437 verificações; conteúdo dos pacotes conferido por SHA-256. Tela cheia exclusiva e falhas fatais continuam sendo limitações. A publicação não instala automaticamente a atualização.
