# ShareX 22 JSON fixtures checked against source

These are synthetic, complete application and uploader JSON objects. They are checked against the public lists of serialized fields in the upstream release tags below. They contain no real profile data. They are checked JObject fixtures, not captures made by running upstream ShareX. Stable helper defaults come from the production serializer. The root, task, region, picker and provider structures that changed in 22 are rebuilt from the tagged sources. Intentional nondefault values exercise migrations and limitations.

The fixtures include every ApplicationConfig field, every TaskSettings root and section field, every RegionCaptureOptions field, every UploadersConfig property, the new picker objects, new provider credentials, complete custom workflows and inherited null sections. UI lists are populated where needed to exercise unsupported values.

Key audit: InputKey names and Windows virtual key values, HotkeyType, AfterCaptureTasks, uploader destination enums, GIFQuality/EImageFormat, RegionCaptureAction and alignment names. Numeric action ordinals are deliberately not reinterpreted.

| Tag | Commit | Application fields | Task fields | Region fields | Uploader properties |
| --- | --- | --- | --- | --- | --- |
| `v22.0.0` | `5a7a1d4002f28aecddb130ec592f6d4d7e224661` | 92 | 36 | 18 | 172 |
| `v22.1.0` | `539703e158ae8187290294731311a847a26d124e` | 92 | 36 | 20 | 172 |
| `v22.2.0` | `3db309c6defa761c00a1e231fd3ca57dfb4bdf70` | 92 | 36 | 20 | 172 |
| `v22.3.0` | `ceafadae3f5d497a14656a859ac4ca4b6cf1fb08` | 92 | 36 | 23 | 172 |
| `v22.4.0` | `08bfff15e748a945944e609aca7faffbe888c7af` | 100 | 36 | 23 | 172 |
