# FaChordable

An accessible Windows desktop reader for the textual guitar-chord descriptions published by FaChords.

## Accessibility and keyboard use

- `Alt+K`, `Alt+T`, and `Alt+S` move to the key, chord type, and subtype lists.
- `Alt+P` or Enter opens the positions window.
- The position window opens directly in a standard MSAA read-only rich edit control, with its caret at the start. Use normal JAWS reading and edit-navigation commands or copy the text.
- `Alt+N` or Page Down moves to the next position and reads it. `Alt+P` or Page Up moves to the previous position.
- `Alt+A` opens that position's audio in the default media application.
- Escape or `Alt+C` closes the positions window and restores focus to the selected chord.
- `Alt+U` updates the complete offline catalogue from FaChords. The existing catalogue remains untouched unless the download passes validation.
- On startup, the app performs a lightweight update check at most once every seven days. It never delays or disables offline use when the site is unavailable.

## Build

```powershell
dotnet build
```

To produce a self-contained 64-bit Windows application:

```powershell
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

Chord descriptions remain credited to and sourced from <https://www.fachords.com/guitar-chord-textual-description/>.
