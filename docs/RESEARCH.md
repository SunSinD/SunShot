# Release scope

SunShot starts from [ShareX 21.0.0](https://github.com/ShareX/ShareX/tree/v21.0.0) and keeps its capture menus, workflows, history and settings.

It addresses these ShareX reports:

- [#9044](https://github.com/ShareX/ShareX/issues/9044): too many update prompts. SunShot uses its own releases, and automatic checks start off.
- [#8986](https://github.com/ShareX/ShareX/issues/8986) and [#8952](https://github.com/ShareX/ShareX/issues/8952): UI changes. SunShot keeps the 21.0 main window with a smaller toolbar and fewer menu items.
- [#8911](https://github.com/ShareX/ShareX/issues/8911): unexpected uploads. New profiles save locally, and uploading selected files asks first.
- [#8909](https://github.com/ShareX/ShareX/issues/8909): watched files that take more than five seconds. SunShot waits until the file stops growing or is unlocked.
- [#9031](https://github.com/ShareX/ShareX/issues/9031): editor selection. A right-click selection bug is fixed.

Multipart upload and cursor bugs from ShareX 22 aren't in SunShot, since they came after 21.0.
