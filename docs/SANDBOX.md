# Development sandbox (Linux)

Development happens on a headless Ubuntu machine (`claude-code-sandbox-server`, user
`renotari`), reached over SSH from the Windows PC with the same key used for the
terminal. The app is tested on Windows; see `CLAUDE.md` for building and running it
on Linux.

## Finding the sandbox address

The sandbox is a Hyper-V virtual machine on the Windows PC. Its IP address (Default
Switch network) can change when the PC restarts:

- on the sandbox: `hostname -I`;
- on Windows, PowerShell as administrator:
  `Get-VM | Get-VMNetworkAdapter | Select VMName, IPAddresses` (the `172.` address);
- if `ssh` uses a name, it is defined in `C:\Users\<name>\.ssh\config` (`HostName`).

There is no FTP server: only SSH (port 22) is open, and it also serves **SFTP**, which
is what FileZilla/WinSCP must use.

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

## With a graphical client (drag and drop)

**FileZilla** — File → Site Manager → New site:

- Protocol: **SFTP**; Host: the one used for `ssh`; Port: 22 (or empty)
- Logon type: **Key file**; User: `renotari`
- Key file: the private key, usually `C:\Users\<name>\.ssh\id_ed25519` (or `id_rsa`);
  the `.ssh` folder may be hidden in the file picker, paste the path. If FileZilla
  offers to convert it to `.ppk`, accept and save it next to the original.
- Connect, accept the server key the first time. Left pane: Windows; right pane:
  the sandbox. Go to `/home/renotari/progetti/karaoke-player-samples/kar` on the
  right and drag the files there.

**WinSCP**: same settings (SFTP site, key under Advanced → SSH → Authentication).

**VS Code with Remote - SSH**: if already connected to the sandbox, open the samples
folder and drag files from Explorer into the VS Code file tree.

## Where to put them

- Sample media for manual tests: `~/progetti/karaoke-player-samples/` (outside the
  repository: songs are not committed). `.kar`/`.mid` samples in `kar/`.
- Test runs use a temporary data folder (`KARAOKE_PLAYER_DATA_DIR`), so pointing a GUI
  run at the samples does not touch the real settings.
