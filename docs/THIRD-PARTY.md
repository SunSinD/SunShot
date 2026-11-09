# Third-party software

SunShot is based on ShareX 21.0.0. Original notices and licenses are in the source files, `LICENSE.txt` and `Licenses`.

The installer and portable ZIP include the self-contained .NET 10 runtime, Windows Desktop runtime, Windows SDK projection and the packages listed in the published `*.deps.json` files. `Licenses/Packages/PACKAGES.txt` lists each shipped package and its license. Packaging stops when a shipped package or native file has no license text.

SQLite (`e_sqlite3.dll`) is public domain. SQLitePCLRaw is Apache-2.0; its license and notice are in `Licenses/Additional/SQLitePCLRaw.txt`.

FFmpeg is downloaded on request, not included in SunShot packages. SunShot pins [ShareX's FFmpeg 8.1 x64 archive](https://github.com/ShareX/FFmpeg/releases/tag/v8.1) and checks this SHA-256:

```
1838932e32f01e0e7b65fde5ca0ebb2ab131cb6a9ff1a84025ee5b784075576c
```

That build (`n8.1.1-7-g3728de467d-20260519`) is GPL-3.0. Its [source](https://github.com/FFmpeg/FFmpeg/tree/3728de467d), [build scripts](https://github.com/ShareX/FFmpeg-Builds) and [upstream build instructions](https://github.com/BtbN/FFmpeg-Builds) belong to their projects. You can use a different ffmpeg.exe through the custom FFmpeg path option.

Release source archives include the source, notices and build scripts for that version. `SHA256SUMS.txt` covers the installer, portable ZIP and source ZIP.
