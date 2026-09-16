# Changelog

## 1.0.0

First public release.

- Captures files dragged out of New Outlook, Outlook on the web, Teams, Gmail,
  SharePoint and OneDrive by completing the `IDataObjectAsyncCapability`
  handshake those apps require.
- Re-serves captured files as a plain `CF_HDROP` drag, so any Windows app,
  Explorer window or browser upload box accepts them normally.
- Falls back to plain `CF_HDROP` for ordinary file drags and to
  `FileGroupDescriptorW` + `FileContents` for classic virtual-file sources.
- Ctrl+C puts captured files on the clipboard for pasting into upload dialogs.
- Per-user installer, no administrator rights required.
