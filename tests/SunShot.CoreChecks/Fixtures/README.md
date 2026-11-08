# ShareX 22 source-checked JSON fixtures

These are synthetic full application/uploader JSON objects, checked against the public serialized field inventories in the upstream release tags below. They contain no real profile data. They are checked JObject fixtures, not captures made by running upstream ShareX. Stable helper defaults come from the production serializer; changed 22 root/task/region/picker/provider structures are rebuilt from the tagged sources. Intentional nondefault values exercise migrations and limitations.

The fixtures include every ApplicationConfig field, every TaskSettings root and section field, every RegionCaptureOptions field, every UploadersConfig property, the new picker objects, new provider credentials, complete custom workflows and inherited null sections. UI lists are populated where needed to exercise unsupported values.

Key audit: InputKey names and Windows virtual-key values, HotkeyType, AfterCaptureTasks, uploader destination enums, GIFQuality/EImageFormat, RegionCaptureAction and alignment names. Numeric action ordinals are deliberately not reinterpreted.

- `v22.0.0`: `5a7a1d4002f28aecddb130ec592f6d4d7e224661`; application 92 fields, task 36 fields, region 18 fields, uploaders 172 properties.
- `v22.1.0`: `539703e158ae8187290294731311a847a26d124e`; application 92 fields, task 36 fields, region 20 fields, uploaders 172 properties.
- `v22.2.0`: `3db309c6defa761c00a1e231fd3ca57dfb4bdf70`; application 92 fields, task 36 fields, region 20 fields, uploaders 172 properties.
- `v22.3.0`: `ceafadae3f5d497a14656a859ac4ca4b6cf1fb08`; application 92 fields, task 36 fields, region 23 fields, uploaders 172 properties.
- `v22.4.0`: `08bfff15e748a945944e609aca7faffbe888c7af`; application 100 fields, task 36 fields, region 23 fields, uploaders 172 properties.
