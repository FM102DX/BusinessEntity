# Rich Document Read Policy

## 1. Purpose

This document defines the read-side policy for rich-text documents stored in `BusinessEntity`.

It describes the concepts and implementation rules used for:

- chunked rich-text storage
- document opening
- table-of-contents loading
- viewport-based chunk reading
- scroll behavior
- chunk cache behavior
- diagnostics and logging
- boundaries between storage infrastructure and rich-document domain logic

The goal is to keep large rich-text documents readable without loading the entire document into the browser DOM at once.

---

## 2. Core Storage Model

A rich-text document is represented by a normal `BusinessEntity` plus a typed `BusinessEntityData` payload.

The document body is not stored as one large text field. It is split into ordered chunks stored as `BusinessEntityDataChunkDto`.

The basic storage structure is:

- `BusinessEntityDto` - document identity and tree object
- `BusinessEntityDataDto` - document manifest and metadata
- `BusinessEntityDataChunkDto` - ordered content chunks
- `BusinessEntityDataChunkPropertyDto` - technical properties for chunks, such as table-of-contents data

`BusinessEntityDataChunkDto` is a technical storage row. It is not a business object and must not be treated as a graph node.

The chunk order is defined by `SortOrder`. All read-side navigation and viewport estimation uses `SortOrder` as the stable ordering key.

---

## 3. Rich Document Manifest

The document-level payload stores the manifest, not the full content.

The manifest describes the document format and storage policy, for example:

- content storage mode
- editor format
- chunk policy
- embedded file storage mode
- image support flags

The manifest is intentionally small. It is safe to read when opening a document and is the only document data that should block the initial page shell render.

---

## 4. Chunk Content

Each rich-text chunk stores the content needed to render that range of the document.

Relevant chunk fields include:

- `BusinessEntityId`
- `SortOrder`
- `Data`
- `PlainText`
- `HtmlCache`
- `BlockCount`
- `CharCount`
- `DataSizeBytes`
- `Version`
- `Checksum`

`HtmlCache` is the primary read-side field for the browser viewport. The viewport renders already prepared HTML and does not rebuild the document from raw blocks during normal reading.

Chunks are cut by configured size. The read side must not depend on semantic boundaries being perfectly aligned with chunk boundaries.

---

## 5. Chunk Properties

Chunk properties are stored in `BusinessEntityDataChunkPropertyDto`.

The table of contents is stored as a chunk property with:

```text
BusinessEntityDataChunkPropertyTypeEnum.RichDocTableOfContents = 100
```

The property belongs to the chunk via `ParentEntityId`.

The property data contains heading entries found inside that chunk. Each entry must contain enough information to navigate back to the exact rendered location:

- heading title
- heading level
- heading anchor
- chunk id
- chunk sort order
- block id or block index when available

The read side builds the full document outline by reading these chunk properties from storage. It must not parse all document HTML in the browser to build the outline.

---

## 6. Document Open Flow

Opening a rich-text document is staged.

The page should synchronously read only the document shell:

1. `BusinessEntity`
2. rich-document manifest

After the shell is available, the page can render the document view immediately.

The following operations must run asynchronously and independently:

- initial chunk window loading
- table-of-contents loading

This prevents a large table of contents from blocking the first visible document screen.

The UI uses separate loading states:

- `IsInitialContentLoading`
- `IsOutlineLoading`

`InitialChunkWindow == null` must not mean "empty document" while `IsInitialContentLoading` is true.

---

## 7. Initial Chunk Window

When a document opens, the viewport loads only the configured initial chunk window.

The initial window size is controlled by system settings.

The default behavior is to show the first document chunks quickly, without waiting for the whole table of contents.

The initial window is passed to `RichTextDocumentViewport` as `InitialWindow`. The viewport applies it to `LoadedChunks`.

---

## 8. Table Of Contents Loading

The outline is loaded from persisted chunk properties.

The read-side outline is a tree of `RichTextDocumentOutlineNode`.

Only heading levels H1-H3 are currently included in table-of-contents properties. The UI can display a configured subset of those levels, currently 1 to 3.

The outline loading must be independent from initial chunk loading.

If the outline is not loaded yet, the document body should still be readable.

---

## 9. Viewport Read Model

The rich-text viewport is a virtualized chunk window.

It renders:

- a top spacer
- currently loaded chunks
- a bottom spacer

The spacers represent unloaded document ranges. This lets the browser scrollbar approximate the full document length while the DOM contains only a limited set of real chunks.

The viewport state is held in:

```text
LoadedChunks
TotalChunkCount
TopSpacerPx
BottomSpacerPx
EstimatedChunkHeight
```

`LoadedChunks` is the source of truth for what is currently rendered as real HTML. Any change to `LoadedChunks` is reflected in the page through normal Blazor rendering.

---

## 10. LoadedChunks Policy

`LoadedChunks` contains the real chunks currently rendered in the viewport.

For adjacent scroll loading, new chunks are merged into the existing loaded set. This prevents already visible content from disappearing when the user scrolls across a chunk boundary.

For table-of-contents navigation or far jumps, the viewport replaces the loaded set with a new window around the target chunk. This avoids unbounded DOM growth during direct navigation.

The merge policy is:

- merge when the requested window is adjacent to the currently loaded window
- replace when the requested window is a far jump

Duplicate chunks are resolved by `SortOrder`; the latest loaded instance wins.

---

## 11. Chunk Read Cache

Before reading from storage, the viewport checks whether requested chunks already exist in `LoadedChunks`.

If all requested chunks are already loaded, no database read is needed.

If only part of the requested range is missing, only missing contiguous ranges should be fetched.

This prevents unnecessary database reads when the user scrolls back into a range that is still displayed.

---

## 12. Scroll Down Behavior

When the user scrolls down normally, the viewport estimates the target chunk based on scroll offset and estimated chunk height.

If the estimated chunk is already loaded, no read is performed.

If the estimated chunk is not loaded, the viewport loads a configured scroll window around that target.

For normal adjacent scrolling, the new window is merged into `LoadedChunks`.

---

## 13. Scroll Up Behavior

When the user scrolls up, the viewport checks whether the scroll position approaches the top boundary of the first loaded chunk.

If the user reaches that boundary and previous chunks exist, the viewport loads the previous window and merges it into `LoadedChunks`.

The number of previous chunks kept or loaded during scroll is controlled by rich-document settings.

This behavior must work after direct table-of-contents navigation as well as after normal scrolling.

---

## 14. PageUp And PageDown

`PageUp` and `PageDown` are treated like normal viewport scrolling.

They should trigger the same boundary checks and chunk-window loading as mouse-wheel or trackpad scrolling.

They must not use table-of-contents jump semantics.

---

## 15. Scrollbar Drag Behavior

Dragging the rich-document scrollbar has special semantics.

While the user holds and drags the scrollbar thumb, the viewport must not read chunks.

On mouse release, the viewport:

1. reads the final scrollbar position
2. estimates the approximate document position
3. maps that position to an approximate chunk `SortOrder`
4. finds the nearest table-of-contents node
5. loads that node as if the user clicked the outline item

This prevents repeated reads during rapid scrollbar movement and gives the scrollbar a whole-document navigation meaning.

If the table of contents is not available, the viewport may load a window around the estimated chunk.

---

## 16. Table-Of-Contents Navigation

Clicking an outline item navigates to a stable heading anchor.

If the target chunk is already loaded, the viewport scrolls to the anchor in the current DOM.

If the target chunk is not loaded, the viewport loads a configured window around the target chunk, then scrolls to the heading anchor after render.

The configured table-of-contents window includes:

- chunks before the target
- the target chunk
- chunks after the target

The before and after counts are system settings.

---

## 17. Settings

Rich-document read behavior is controlled by system parameters.

Current read-side settings include:

- rich-text chunk size
- initial chunk count on document open
- table-of-contents before buffer
- table-of-contents after buffer
- scroll previous chunk count
- table-of-contents scrollbar visibility

Settings are read through `RichTextDocumentSettingsService`.

The storage provider should not contain rich-document domain decisions. Domain-specific reading behavior belongs in rich-document services and components.

---

## 18. Logging

Chunk reads are logged to the web logger with a dedicated tag:

```text
[rich-doc-chunk-read]
```

Loaded chunk state can be logged with:

```text
[rich-doc-loaded-chunks]
```

Diagnostic logs must use stable tags so they can be filtered in the web logger.

Diagnostic tags should not include dynamic chunk values as separate logger tags. Dynamic values belong in the message text.

---

## 19. Rebuilding The Table Of Contents

The table of contents is rebuilt explicitly.

It should run:

- after import
- after pressing the rebuild table-of-contents button

Opening a document must not rebuild the table of contents.

Opening a document only reads persisted table-of-contents properties.

---

## 20. Import Policy

Import creates or appends chunks and creates table-of-contents properties for those chunks.

Chunk cutting is controlled by size settings.

The read side assumes imported chunks already have:

- stable `SortOrder`
- rendered `HtmlCache`
- table-of-contents properties when headings exist

If a chunk has no H1-H3 headings, no table-of-contents property is required.

---

## 21. Ownership Boundaries

Rich-document logic belongs to the rich-document layer.

The data provider may expose generic storage operations and converters, but it must not own rich-document read policy.

The correct responsibility split is:

- data provider: store and retrieve DTOs
- converters: translate persisted DTO payloads
- rich-document helper: rich-document domain operations
- rich-document viewport: UI windowing and navigation
- rich-document outline: table-of-contents UI

This keeps the storage layer reusable and prevents domain-specific behavior from leaking into generic infrastructure.

---

## 22. Failure And Cancellation Policy

Opening a document can start multiple asynchronous reads.

If the user navigates to another document before those reads complete, old reads must not overwrite the new page state.

The page uses:

- cancellation tokens
- a load version

Every asynchronous result must check that it still belongs to the active document before applying UI state.

---

## 23. Current Tradeoffs

The current model limits initial DOM size and avoids full-document reads during normal opening.

However, adjacent scrolling can still grow the DOM over time because merged chunks remain visible. This is acceptable for the current implementation phase.

A future stricter virtualization model may evict distant chunks and keep only the visible range plus buffer.

---

## 24. Non-Goals

The current read policy does not require:

- loading the whole rich document into the DOM
- rebuilding the table of contents on every open
- parsing browser DOM to discover headings
- treating chunks as business entities
- putting rich-document navigation rules into the data provider

These behaviors should be avoided unless this policy is intentionally revised.

---

## 25. Principal Solution Approaches

This section defines the preferred engineering approaches for recurring rich-document read problems.

### 25.1. Opening a large document

Problem:

- the user needs to see the document quickly
- the document can contain many chunks
- the table of contents can be large

Principled approach:

- read only the shell synchronously
- render the page frame as soon as the shell is available
- load the initial chunk window asynchronously
- load the table of contents asynchronously
- keep separate loading states for body and outline

Avoid:

- waiting for all chunks before rendering
- waiting for the table of contents before showing the first text
- using one global loading flag for all read stages

### 25.2. Reading document body content

Problem:

- the document body can be too large for a single DOM render
- the user usually needs only a local range

Principled approach:

- read chunks by ordered windows
- render only loaded chunks as real DOM
- represent unloaded ranges with spacers
- estimate total scroll height from chunk count and measured chunk heights

Avoid:

- concatenating all chunk HTML into one giant string
- loading all chunks on open
- making the scrollbar represent only the currently loaded window

### 25.3. Navigating by table of contents

Problem:

- the user clicks a semantic heading, not a raw chunk number
- the target chunk may not be loaded

Principled approach:

- store stable heading anchors in chunk properties
- resolve the clicked outline node to `ChunkSortOrder` and anchor
- if the chunk is loaded, scroll to the anchor
- if the chunk is not loaded, load a configured window around it, then scroll after render

Avoid:

- reparsing all document HTML to find headings
- loading the entire document to make anchor navigation possible
- relying on visual text matching instead of stable anchors

### 25.4. Scrolling down and up

Problem:

- the user can move through chunk boundaries gradually
- content must not disappear at the boundary

Principled approach:

- detect that the user is approaching the loaded range boundary
- fetch only the missing adjacent window
- merge adjacent windows into `LoadedChunks`
- reuse chunks already present in `LoadedChunks`

Avoid:

- replacing the whole loaded range during adjacent scrolling
- rereading chunks that are already loaded
- treating upward scrolling as a different navigation model from downward scrolling

### 25.5. Dragging the scrollbar thumb

Problem:

- scrollbar dragging can emit many scroll events
- reading during drag causes repeated database reads and flicker

Principled approach:

- suppress chunk reads while the mouse holds the scrollbar thumb
- wait until mouse release
- estimate the final document position
- map that position to the nearest outline node when possible
- load that location using table-of-contents jump semantics

Avoid:

- reading chunks continuously during thumb drag
- treating every intermediate scroll event as a real navigation intent
- merging every drag-produced window into the current DOM

### 25.6. Rebuilding the table of contents

Problem:

- rebuilding scans chunks and writes technical properties
- this is heavier than reading persisted outline data

Principled approach:

- rebuild only on explicit events
- run rebuild after import
- run rebuild when the user presses the rebuild button
- on document open, read persisted properties only

Avoid:

- rebuilding on every open
- rebuilding as a side effect of reading
- hiding rebuild cost inside generic data-provider reads

### 25.7. Choosing merge versus replace

Problem:

- adjacent scroll should feel continuous
- far jumps should not grow the DOM unnecessarily

Principled approach:

- merge when the new window touches or overlaps the existing loaded window
- replace when the new window is a far jump
- use table-of-contents jumps and scrollbar-release jumps as replace operations

Avoid:

- always replacing, because it causes visible disappearance near boundaries
- always merging, because it can grow the DOM too aggressively after many jumps

### 25.8. Placing domain behavior

Problem:

- chunk storage is generic infrastructure
- rich-document reading is domain-specific behavior

Principled approach:

- keep DTO persistence in the data provider
- keep rich-document decisions in `RichTextDocumentHelper` and UI components
- keep converters limited to storage payload conversion

Avoid:

- putting table-of-contents navigation policy into the data provider
- making storage APIs understand UI scroll behavior
- coupling generic repository code to rich-document semantics

### 25.9. Diagnostics

Problem:

- chunked reading is hard to reason about without visibility
- logs can become noisy and hard to filter

Principled approach:

- log actual chunk reads with a stable tag
- log loaded-window state with a separate stable tag
- keep dynamic values in message text, not in logger tags
- remove or reduce diagnostic logging when it starts to interfere with normal analysis

Avoid:

- creating dynamic tags for chunk ids or chunk sizes
- logging every internal UI estimate as a separate tag
- mixing read diagnostics with unrelated application logs
