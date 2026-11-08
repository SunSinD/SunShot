# Third-party software

SunShot is based on ShareX 21.0.0. Its original notices and licenses are preserved in the source files, `LICENSE.txt`, and `Licenses`.

The installer and portable ZIP include the self-contained .NET 10 runtime, the Windows Desktop runtime, the Windows SDK projection, and the NuGet packages listed in the published `*.deps.json` files. `Licenses/Packages/PACKAGES.txt` lists each shipped package with its version and the license and notice files that cover it. Texts shipped inside a package are copied to `Licenses/Packages` with its `.nuspec`. When a package only names its license, the full text is in `Licenses` or `Licenses/Additional`, taken from the project's repository at the commit noted in the file. The packaging script stops if a shipped package or native file has no license text.

SQLite (`e_sqlite3.dll`) is in the public domain. SQLitePCLRaw is Apache-2.0; its license and `NOTICE` are in `Licenses/Additional/SQLitePCLRaw.txt`.

FFmpeg is downloaded separately on request, not redistributed in SunShot's packages. SunShot pins [ShareX's FFmpeg 8.1 x64 archive](https://github.com/ShareX/FFmpeg/releases/tag/v8.1) and verifies its SHA-256:

```
1838932e32f01e0e7b65fde5ca0ebb2ab131cb6a9ff1a84025ee5b784075576c
```

The executable identifies itself as `n8.1.1-7-g3728de467d-20260519`. FFmpeg is GPL-3.0 in that build. Its [source](https://github.com/FFmpeg/FFmpeg/tree/3728de467d), [build scripts](https://github.com/ShareX/FFmpeg-Builds), and [upstream build instructions](https://github.com/BtbN/FFmpeg-Builds) are maintained by their respective projects. The downloaded tool remains separate from SunShot and can be replaced through the existing custom FFmpeg path option.

`SunShot-<version>-source.zip`, built by `build/Package.ps1` next to the installer, contains SunShot's source, notices, and build scripts for that exact version. `SHA256SUMS.txt` covers the installer, portable ZIP, and source ZIP. The repository retains the upstream history and credit.
