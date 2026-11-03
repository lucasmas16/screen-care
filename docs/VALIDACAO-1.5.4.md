# Screen Care 1.5.4 — settings caption and release validation

This release includes the alert, error recovery and menu changes from the local 1.5.2/1.5.3 builds, plus the startup-caption layout correction. The prior public release was 1.5.1.

## Caption fix

The text under “Start with Windows” is “Restore your last settings” (Portuguese: “Retomar as últimas configurações”). Fixed-height nested containers could clip that caption. Setting rows and their text containers now measure their content, preserving minimum spacing. The settings card and its parent can grow so increased text height is reachable through the existing scroll area rather than clipped at the card edge.

## Checks

- 489 checks passed on Windows, .NET Framework 4.8 x64, with compiler warnings treated as errors.
- Added caption checks for both languages and themes, normal text and enlarged title/caption fonts, and minimum window size. Checks measure the full preferred text height and containment through each ancestor up to the scroll viewport.
- Rendered previews were inspected at the active 150% display scale. The caption is complete with normal and enlarged text. This does not claim physical validation on every DPI configuration or across monitor transitions.
- Existing checks cover native input, power-state logic, settings, images, alert replacement/expiration, cross-process borderless fullscreen overlays, foreground preservation and repeated error recovery.
- The basic rapid-toggle focus check now takes its baseline immediately before the toggle sequence, after the expiration waits, so user app-switching during those waits is not misattributed to the alert.
- Native shortcut ownership lifecycle was skipped because the installed application owns the shortcuts. Dispatch and collision handling remain tested.

## Package and scope

The installer and portable package contain the application, runtime configuration, bilingual guide and MIT license. SHA-256 comparisons verify the installer payload and ZIP against the final build. Only source and public documentation are committed; profiles, local logs, screenshots and development artifacts remain excluded.

The publication does not update the installed copy. True exclusive fullscreen, fatal process failures, external termination and device-specific monitor behavior retain the limits described in [1.5.2](VALIDACAO-1.5.2.md) and [1.5.3](VALIDACAO-1.5.3.md).

## Português

Corrigida a legenda cortada abaixo de “Iniciar com o Windows”. As linhas de configurações e o painel ajustam a altura ao conteúdo. A versão passou por 489 verificações, incluindo inglês/português, claro/escuro, texto ampliado e janela mínima. Inclui o alerta sem roubar foco, a recuperação de falhas repetidas e o menu com somente “Desligar telas em 2 segundos — Ctrl+Alt+F11” como ação manual. A publicação não instala automaticamente a atualização.
