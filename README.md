Real Borderless Gaming

I made a fork off "SRWE App" made it more up to date and easy to use.

Keyboard shortcuts

- Apply / Restore: it acts on the game you’re in, and uses the selected profile if the active window has none. If that profile is applied it restores it, otherwise it applies it.
- Hide / show taskbar: it overrides the profile’s taskbar setting until you press it again, or until a fresh Apply, or Restore Desktop. If you hide the taskbar this way and the game closes, it stays hidden until you press the key again.
- Restore Desktop: the existing emergency key, now rebindable.
To bind them, click a box and press the keys you want. Backspace clears it and Esc cancels. Keys need Ctrl, Alt or Shift (F-keys can go alone), and two actions can’t share a shortcut.
- Defaults are Ctrl+Alt+B, Ctrl+Alt+T and Ctrl+Alt+Shift+R. If another program already uses a key, the status line tells you which shortcut to change.
- Pressing a shortcut shows a tray notification when the window is hidden. The Restore Desktop button shows its current key.

Saved resolutions

- The Window area card now has Save resolution and Delete buttons next to the preset dropdown.
- Save asks for a name, pre-filled with the size (for example 5440 × 1440). Leaving the size as the name shows just the size and its ratio.
- Presets are shared by all games. The built-in list is now an editable list, so you can delete any of it.
- Restore default presets... is the last entry in the dropdown if you delete something by accident. I also added a few ultrawide sizes (2560×1080, 3440×1440, 5120×1440).

Process picker

- “Add game > From a running process...” now opens a new window in the same style as the main app, with a search box, app icons and a “Has profile” badge.
- It shows only apps with windows by default. A switch turns that off.
- The Advanced editor’s “Open process” button uses the same picker, so the old dialog is gone.

Right-click a process

- Add game and apply preset lists your saved presets. It creates the profile, centres that size on your primary display, and applies it to the running game right away.
- For a game that already has a profile, the same entry reads “Apply resolution preset”.
- Right-clicking a game in the sidebar also offers Apply, Restore, Apply resolution preset and Delete.

Ignore processes

- Right-click gives “Ignore” and “Un-ignore”. Ignored processes disappear from the picker.
- The “Show ignored” switch brings them back with an “Ignored” badge, and “Un-ignore all (n)” clears the list. The list is remembered between runs.

Dark and light themes

- The Theme button in the bottom bar cycles Dark, Light and System. It switches live and is remembered.
- It covers the main window, the picker, dialogs, menus, the tray menu and the title bar.
- System follows your Windows setting when you click it, but doesn’t update on its own if Windows changes later.

New icon

- A gamepad on a violet-to-blue tile, which also appears in the tray and the sidebar logo.

- On a small screen such as a 768p laptop, the window now shrinks to fit and the editor scrolls instead of being cut off.


EXITSIZEMOVE

Some games don't seem to work with Real Borderless Gaming, even though they passed the manual resize test. This is because their internal windows handling 
code waits for a sign from Windows that the user has finished resizing the window. This sign usually comes when you release the left-mouse
button after manually resizing the windows. These signs are called _messages_. With Real Borderless Gaming we basically trick the window it is attached to
that a user has resized the window manually by sending it these messages, like a size message that the window size has changed. For some
games, this is enough. For other games this isn't enough, they deliberately wait for the 'I'm done!' message. This particularly message is
called `WM_EXITSIZEMOVE`, and you can tell Real Borderless Gaming to send this message as well, by checking the _Force EXITSIZEMOVE after window resize_ 
checkbox. 

Some games, like Dragon Age:Inquisition, it's required to check this checkbox. For others, like Rise of the Tomb Raider, 
checking this checkbox actually causes resizing the window through Real Borderless Gaming to be stretching. It's therefore a trial/error process whether
to check this checkbox or not. But it's easy to find out (just try whether checking the checkbox helps solve it or not), and Real Borderless Gaming will remember 
the state of the checkbox. 

Remember, not all games support hotsampling. Real Borderless Gaming isn't a piece of magic that can enable functionality in a game that's not implemented by its developers:
it simply mimics a user's resize actions on a windowed game. If the game itself doesn't anticipate on resize actions (most games ignore any resize action
on the window, sadly) then Real Borderless Gaming can't add that functionality, only the game developers can.
