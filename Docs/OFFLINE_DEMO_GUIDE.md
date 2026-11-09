# Offline Demo 0.1.0 — 9 November 2026

## Artifact and verification
- APK: `Build/OfflineDemo/CardGame-OfflineDemo-0.1.0.apk` (35.52 MiB).
- Share bundle: `Build/OfflineDemo/CardGame-OfflineDemo-0.1.0.zip` (APK, Vietnamese installation/test guide, SHA256).
- Vietnamese guide: `Build/OfflineDemo/HUONG_DAN_CAI_VA_TEST.txt`.
- SHA256: `2B2F05D13CAFCA0AF92E5CFFE12A7DABC579262D7E90C5141B8C62FCC770AA57`.
- Unity build: succeeded, 0 errors, 0 warnings. APK signatures v1/v2 verified.
- Android 6.0+ (API 23), target API 34, ARMv7 + ARM64, IL2CPP, portrait.
- Package: `com.binhb2105696.strategiccardgame.demo`, version 0.1.0/code 1.
- No Internet permission. No Google login, cloud sync or online PvP.
- Local profile/deck/settings/history round-trip, backup and corrupt-file protection passed in Editor scratch data.
- Physical-device install/play/persistence tests are still pending; build success is not device acceptance.

## Build again
1. Use Unity 2022.3.62f3 with Android Build Support, SDK API 34, NDK and OpenJDK.
2. Stop Play Mode, save intended scene changes, and wait for compilation.
3. Run **Card Game > App Shell > Build Offline Demo APK**.
4. The tool validates local storage, configures Android and switches platform if needed. It preserves saved scene layouts and does not rebuild AppShell UI.
5. The APK and `BuildResult.txt` go under `Build/OfflineDemo/`. Recalculate the SHA256 and refresh any ZIP after rebuilding; the checksum above identifies only the first demo artifact.
6. The first share ZIP/guide were packaged separately; the Editor menu does not regenerate them.

The build uses the current Android signing configuration; the first APK uses Android Debug signing. Preserve its signing keystore privately for compatible demo updates. Never commit keystores/passwords. A future production signature may require a different package or explicit data migration.
APK/ZIP outputs are ignored by Git. This source checkpoint does not upload an APK to GitHub Releases or retain the private signing key.

## Install and play
1. Extract the ZIP and open the APK on an Android device.
2. If Android requests it, allow installation only for the browser/file manager opening the APK; disable that permission afterward if desired.
3. If installation is incompatible, reports a signature conflict or is blocked by Play Protect, send the exact message to the developer. Do not disable device protection globally.
4. Enter a local guest name. Use a preset or save a valid Deck, then select **Duel mode > Easy Bot / Normal Bot**.
5. Auto delegates both sides to the selected policy. Settings > Duel Log displays current-match events. Auto/hot-seat practice does not count toward manual PvE wins/losses.

## Local data and test feedback
- Explicitly save Deck edits. Guest identity, saved Decks, settings and completed-match results are local; uninstalling/clearing app data can lose them. The OS may apply its own backup policy.
- Updating with the same package/signature is the intended way to retain data. There is no resume-mid-match feature or persisted Duel Log viewer.
- Before broad sharing, test install/open, a full match, result-to-menu, another duel, and profile/Deck/history after closing/reopening the app.
- Check touch targets, clipped text, device heat/frame rate, Easy/Normal, Auto and Log scrolling.
- Report demo version, phone/Android version, mode/Auto status, reproduction steps, expected/actual result, and screenshot/video with relevant turn/log lines.
- Card expansion, detailed effect/chain review and Duel Log polish are deferred; stronger-bot claims require comparative measurements.
