# Real Borderless Gaming

Per-game window profiles: borderless windows, exact custom areas (including spanning several displays),
automatic apply when a game starts, taskbar hiding and a one-key emergency "Restore Desktop"
(Ctrl+Alt+Shift+R).

- Add games from the process picker: double-click, or right-click a process to add it and apply a saved resolution preset.
- "Save resolution" stores the current width x height as a preset; "Delete" removes one. Presets are shared by all games.
- Ignore processes you never want to see in the picker (right-click > Ignore); un-ignore them from the "Show ignored" view.
- Settings button (bottom right): theme (dark / light / system), keyboard shortcuts, and the Advanced editor.
- Global shortcuts you can rebind: Apply / Restore (default Ctrl+Alt+B), hide / show taskbar (Ctrl+Alt+T), Restore Desktop (Ctrl+Alt+Shift+R).

Build: run `build.bat` (no Visual Studio needed). It creates a folder named `Real Borderless Gaming` next to it
containing everything the app needs (the exe, its config file and the optional `Profiles` presets for the Advanced editor).
Copy that folder to any Windows PC (Windows 10/11 already include the required .NET Framework) and delete the rest.
Data lives in `%AppData%\RealBorderlessGaming`.

Based on SRWE (Simple Runtime Window Editor) by _DTG_ and contributors, MIT licensed; see `LICENSE`.
The original SRWE editor is still available inside the app as "Advanced editor".
