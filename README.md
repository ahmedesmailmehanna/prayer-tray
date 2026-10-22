# PrayerTray

A tiny Windows app that keeps the Islamic prayer times in your taskbar.

- **Live countdown on the taskbar**, right next to the clock:
  ```
    Asr 15:21
  in 1:23:45
  ```
- **A small square in the tray** showing the minutes left (or hours, e.g. `2h`). It turns orange in the last 15 minutes.
- **Click either one** for today's full list: Fajr, Sunrise, Dhuhr, Asr, Maghrib, Isha, plus the Hijri date.
- **A notification** when each prayer time arrives (optional).
- **Works offline.** Whenever you're online it saves at least the next 7 days.
- Any city in the world, 22 calculation methods, Standard or Hanafi Asr.
- Follows your light/dark taskbar and your 12h/24h clock setting, and hides during full-screen games and videos.
- Light: one ~70 KB exe and about 15-25 MB of memory. No installer, nothing else to install.

Prayer times come from the free [AlAdhan API](https://aladhan.com/prayer-times-api).

## Install

1. Download **`PrayerTray.exe`** from the [latest release](../../releases/latest).
2. Put it somewhere permanent, e.g. `C:\Tools\PrayerTray\PrayerTray.exe`.
3. Double-click it. Enter your city and country, pick your calculation method, and click **Start**.

> **"Windows protected your PC"?** The exe isn't code-signed, so SmartScreen warns about new downloads.
> Click **More info → Run anyway**. You can also [build it yourself](#build-from-source) from the source.

By default PrayerTray starts automatically when you sign in. You can change that in Settings.

### Keep the icon visible

Windows 11 hides new tray icons behind the **^** arrow next to the clock. PrayerTray tries to pin itself on first run. If it's still hidden:

- **Windows 11:** Settings → Personalization → Taskbar → Other system tray icons → turn on **PrayerTray**
- **Windows 10:** Settings → Personalization → Taskbar → Select which icons appear on the taskbar → turn on **PrayerTray**
- Or drag the icon from the **^** menu onto the taskbar.

## Using it

| Action | What happens |
| --- | --- |
| Click the countdown or the square | Opens today's prayer times. Click elsewhere or press Esc to close. |
| Drag the countdown left/right | Moves it, if it covers something. The position is remembered. |
| Hover the square | Tooltip, e.g. `Asr in 1h 23m (15:21)` |
| Right-click either one | Show times, Refresh, Settings, show/hide the countdown, reset its position, Uninstall, Exit |

## Uninstall

Right-click the icon → **Uninstall…**. This removes it from startup, deletes its settings and saved times, and closes it. Then delete `PrayerTray.exe`.

## Privacy

PrayerTray only talks to `api.aladhan.com`, sending the city, country and method you chose. It doesn't use your location and collects nothing. Settings and saved times live in `%APPDATA%\PrayerTray`.

## Build from source

No Visual Studio needed. Every Windows 10/11 PC has the C# compiler built in:

Clone this repo, then run:

```
build.bat
```

The exe ends up in `dist\PrayerTray.exe`.

The code is plain C# 5 / WinForms on .NET Framework 4.x, which is preinstalled on Windows. Everything is in `src/`:

| File | Purpose |
| --- | --- |
| `Program.cs` | Entry point, single-instance check, first-run settings |
| `TrayApp.cs` | Tray icon, menu, 1-second timer, notifications |
| `Overlay.cs` | The countdown text drawn on the taskbar |
| `PopupForm.cs` | The prayer-times panel |
| `SettingsForm.cs` | Settings window |
| `PrayerData.cs` | AlAdhan API, 7-day offline cache, next-prayer logic |
| `AppSettings.cs` | Settings file, start-with-Windows, calculation methods |
| `Native.cs` | Win32 calls and DPI helpers |

### Releases

`.github/workflows/build.yml` builds the exe on every push. To publish a release with the exe attached, push a version tag:

```
git tag v1.0.0
git push origin v1.0.0
```

## Notes

- Times are shown in the city's local time, so your PC clock should be set to the same time zone.
- Windows has no official way to put text in the taskbar. The countdown is a small transparent window kept on top of it. If you click the taskbar, it can drop behind for up to a second.

## License

[MIT](LICENSE)
