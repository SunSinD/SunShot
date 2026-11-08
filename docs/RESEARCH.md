# Release scope

SunShot starts from [ShareX 21.0.0](https://github.com/ShareX/ShareX/tree/v21.0.0), rather than trying to undo the 22.x main-window rewrite. Existing capture menus, task workflows, history views, and settings remain recognizable.

The initial work responds to these reports:

- [#9044](https://github.com/ShareX/ShareX/issues/9044): frequent update prompts. SunShot checks its own release feed, and automatic checking starts off. This does not promise a release schedule.
- [#8986](https://github.com/ShareX/ShareX/issues/8986) and [#8952](https://github.com/ShareX/ShareX/issues/8952): development and UI concerns. SunShot retains the 21.0 main window and makes limited menu and toolbar changes. These reports are not claimed as resolved upstream.
- [#8911](https://github.com/ShareX/ShareX/issues/8911): unexpected uploading. New profiles use local capture tasks; the toolbar shows automatic-upload state, and selected-file uploads from the main window ask for confirmation. Other explicit upload entry points and user-enabled automatic workflows retain their existing behavior.
- [#8909](https://github.com/ShareX/ShareX/issues/8909): watched files taking longer than five seconds. The watcher now waits while file size progresses, logs idle skips, and rejects callbacks from disabled or replaced watchers. Locked preallocated files with no size changes can still time out after five idle seconds.
- [#9031](https://github.com/ShareX/ShareX/issues/9031): editor selection. A specific right-click selection notification bug was reproduced and fixed. This is not a claim that every possible selection problem in that report is resolved.

Multipart boundary and cursor regressions investigated in 22.x were not copied into this release: they were introduced after the chosen 21.0 baseline.

The provided GitHub issue bodies and discussion threads informed the investigation. Reddit requests were blocked with HTTP 403, so no claim is made that all subreddit complaints were reviewed. Every inherited feature and external upload provider still needs broader beta coverage.
