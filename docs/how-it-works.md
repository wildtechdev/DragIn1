# Why dragging attachments out of New Outlook does nothing

A short technical write-up, with the evidence, for anyone who has hit this and
wants to know what is actually happening.

---

## The symptom

Drag an attachment out of New Outlook onto a folder, an upload box, or any
desktop app. The cursor shows "no drop", or the drop is accepted and nothing
arrives. No error appears anywhere.

The same thing happens from Teams, Gmail, SharePoint and OneDrive. Classic
Outlook works fine. That pattern is the first clue.

## The cause

Classic Outlook is a native Windows application. When you drag an attachment it
puts a real file on the drag, in the format Windows has used since the nineties:
`CF_HDROP`, a list of paths on disk.

New Outlook is Chromium in a window. So are Teams, Gmail in a browser tab,
SharePoint and OneDrive. Chromium does not put a file on the drag, because at
the moment you start dragging there is no file. The attachment lives on a
server. Writing it to disk takes time, and a drag-and-drop operation is not
allowed to block while that happens.

So Chromium uses **delayed rendering**. It advertises the formats it *could*
produce, then waits to be asked properly before producing anything.

"Properly" means a specific, documented handshake:
[`IDataObjectAsyncCapability`](https://learn.microsoft.com/en-us/windows/win32/api/shobjidl_core/nn-shobjidl_core-idataobjectasynccapability).

```
target: GetAsyncMode()        -> source says "yes, I work asynchronously"
target: StartOperation()      -> "I am going to fetch on a background thread"
target: GetData(CF_HDROP)     -> now the source produces the file
target: EndOperation()        -> "done"
```

A drop target that skips this and simply calls `GetData(CF_HDROP)` on the UI
thread gets `DV_E_FORMATETC` and nothing else.

**Almost nothing implements the handshake.** Not File Explorer for these
sources, not the upload box on most websites, not the average desktop app. So
the drop silently fails, everywhere, all at once, and it looks like the email
client is broken.

## The evidence

This was verified by registering a real `IDropTarget`, dropping one attachment
out of New Outlook, and trying both approaches against the same drag.

**What the drag advertised:**

```
--- RAW FORMATETC ENUMERATION ---
    1. id=49327  tymed=TYMED_ISTREAM   DragContext
    2. id=49917  tymed=TYMED_HGLOBAL   DragImageBits
    3. id=50088  tymed=TYMED_HGLOBAL   chromium/x-renderer-taint
    4. id=15     tymed=TYMED_HGLOBAL   CF_HDROP
    5. id=49856  tymed=TYMED_HGLOBAL   Chromium Web Custom MIME Data Format

IDataObjectAsyncCapability: PRESENT  GetAsyncMode hr=0x00000000 asyncMode=True
```

`CF_HDROP` is right there on the list. The source is openly saying it can
produce a file, and openly saying it works asynchronously.

**Strategy A, ask the ordinary way:**

```
  [A] CF_HDROP GetData threw: DV_E_FORMATETC (0x80040064)
  [A] FileGroupDescriptorW threw: DV_E_FORMATETC (0x80040064)
```

Refused. This is what every failing app sees.

**Strategy B, complete the handshake and ask from a background MTA thread:**

```
  StartOperation hr=0x00000000
  [B/try1] CF_HDROP SUCCESS, 1 path(s):
        C:\Users\...\AppData\Local\Temp\chrome_drag19360_810947597\DOC081826.pdf
        (413387 bytes on disk)
```

A real 413 KB PDF, on the first attempt, no retries.

Note the path. `chrome_drag19360_...` is **Chromium's own** temp directory, and
19360 is the process ID of the WebView2 host running Outlook. Chromium wrote
that file itself, the instant it was asked correctly. Nothing was fetched from
Microsoft, no API was called, no credentials were involved. The file was always
available. It just required the right question.

## What DragIn1 does

1. Registers a real `IDropTarget` with `RegisterDragDrop`, rather than relying
   on a framework's simplified drop handling.
2. On drop, queries the data object for `IDataObjectAsyncCapability`.
3. If async mode is on, calls `StartOperation`, marshals the data object to a
   background MTA thread with `CoMarshalInterThreadInterfaceInStream`, and polls
   there while Chromium writes the file.
4. **Copies the result out of Chromium's temp directory immediately**, because
   Chromium deletes that directory when the drag operation ends.
5. Calls `EndOperation`.
6. Serves the saved file back out as a plain `CF_HDROP` drag.

There are fallbacks for other source types: plain synchronous `CF_HDROP` for
ordinary Explorer drags, and `FileGroupDescriptorW` + `FileContents` over
`TYMED_ISTREAM` for classic virtual-file sources such as attachments in old
Outlook.

Step 6 is the part that makes it universally useful. Once the file is on disk
and a normal Windows app is offering it, every destination in Windows accepts
it, because there is nothing unusual left to accept.

## Why this is two gestures

Making it one gesture requires fixing the problem at the source: getting inside
the Chromium process, intercepting `DoDragDrop`, and performing the handshake on
the application's behalf before the drag reaches any destination. That is
technically achievable and it is what commercial tools in this space do.

It also means injecting unsigned code into Outlook, Teams and Chrome, which
trips antivirus, requires a conversation with IT on a managed machine, and
breaks whenever those applications update.

DragIn1 never enters another process. The cost is one extra gesture. The benefit
is that it cannot break your email client, and it will still work after the next
Outlook update.

## If you are implementing this yourself

Three things that are easy to get wrong:

1. **Do the extraction off the UI thread.** The whole point of async mode is
   that the source may need time. Marshal the data object across apartments with
   `CoMarshalInterThreadInterfaceInStream` and `CoGetInterfaceAndReleaseStream`;
   do not just call from the drop thread and hope.

2. **Copy the file immediately.** The path you get back points inside
   Chromium's temp directory, which is deleted when the drag ends. A path you
   stored and read later will be gone.

3. **Call `EndOperation`.** Even on failure. Skipping it leaves the source
   believing an operation is still in flight.

The implementation is in
[`DragIn1.cs`](../DragIn1.cs) — see the `Grab` and `ShelfTarget` classes. It is
plain C# against the Win32 interfaces, with no dependencies.

## References

- [IDataObjectAsyncCapability](https://learn.microsoft.com/en-us/windows/win32/api/shobjidl_core/nn-shobjidl_core-idataobjectasynccapability)
- [Transferring Shell Objects with Drag-and-Drop and the Clipboard](https://learn.microsoft.com/en-us/windows/win32/shell/datascenarios)
- [Handling Shell Data Transfer Scenarios](https://learn.microsoft.com/en-us/windows/win32/shell/datascenarios)
