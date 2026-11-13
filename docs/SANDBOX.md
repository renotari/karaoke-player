# Development sandbox (Linux)

Development happens on a headless Ubuntu machine (`claude-code-sandbox-server`, user
`renotari`), reached over SSH from the Windows PC with the same key used for the
terminal. The app is tested on Windows; see `CLAUDE.md` for building and running it
on Linux.

## Copying files from Windows to the sandbox

Use `scp` from a Windows terminal (PowerShell or cmd: OpenSSH is part of Windows 10/11).
`<sandbox>` is the host name or IP address you use for `ssh`
(on the sandbox, `hostname -I` prints the IP; it can change after a restart).

```powershell
# A whole folder (creates karaoke-player-samples\Kar on the sandbox side)
scp -r "D:\Karaoke\Kar" renotari@<sandbox>:~/progetti/karaoke-player-samples/

# Some files
scp "D:\Karaoke\Kar\song1.kar" "D:\Karaoke\Kar\song2.kar" renotari@<sandbox>:~/progetti/karaoke-player-samples/kar/
```

- Quote Windows paths with spaces. Names with accents are kept.
- The other direction (sandbox → Windows) is the same command with the two sides swapped:
  `scp renotari@<sandbox>:~/progetti/karaoke-player/docs/file.md .`
- With a graphical client (WinSCP, FileZilla): protocol SFTP, same host, user and key.

## Where to put them

- Sample media for manual tests: `~/progetti/karaoke-player-samples/` (outside the
  repository: songs are not committed). `.kar`/`.mid` samples in `kar/`.
- Test runs use a temporary data folder (`KARAOKE_PLAYER_DATA_DIR`), so pointing a GUI
  run at the samples does not touch the real settings.
