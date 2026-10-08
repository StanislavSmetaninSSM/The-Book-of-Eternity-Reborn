# Linux HTTP coverage gap and authorized loopback reproduction — WIP

Source #1553. Baseline checkpoint3ce1ab52b0a4e41dbe9717b52681236bd368c9b9;
web/core/frontend code matches accepted main6381a9507baae6bb2531e22e9a0ace839f03985f.
No future HTTP fix SHA has been provided. Product source/tests remain read-only;
HOME-PC remains the implementation writer.

Read-only evidence: real host smoke calls await /api/session and /api/game-screen
sequentially; settings endpoint tests each exercise one request. The named
BrowserAudioService_SerializesSharedSettingsUpdates test is a source-string guard,
not concurrent HTTP. Actual Load HTTP early-ACK fixture overlaps Load with ACK for
100ms, without the three contested GET routes. Historical selection/TRX review
and exact source references are being collected; no Windows-only/timing conclusion
is inferred from the gap.

Owner now authorizes a live new isolated ordinary game, original owned persistent
relay, real web entrypoint and bounded concurrent loopback requests on31460.
No model response/provider request, real save, access/security/network change or
product fix is authorized. Use existing prebuilt package with source/binary pins,
ordinary NewGame/cancel before relay launch, real launcher/fence/readiness and
one original scoped shutdown. Record each request duration/status/body and cleanup.

Chromium with sandbox requested refused: installed SUID sandbox helper is not
configured correctly. No sandbox bypass or settings change will be used; direct
localhost HTTP is separate from browser UI qualification. Live baseline remains
pending. Report branch codex/1553-http-linux-audit-20261008.
