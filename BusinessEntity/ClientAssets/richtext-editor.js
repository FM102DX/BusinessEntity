import { Editor, Node } from "@tiptap/core";
import StarterKit from "@tiptap/starter-kit";
import Heading from "@tiptap/extension-heading";
import Paragraph from "@tiptap/extension-paragraph";
import Underline from "@tiptap/extension-underline";

const CustomHeading = Heading.extend({
    addAttributes() {
        return {
            ...(this.parent?.() ?? {}),
            id: {
                default: null,
                parseHTML: element => element.getAttribute("id"),
                renderHTML: attributes => attributes.id ? { id: attributes.id } : {}
            },
            "data-chunk-id": {
                default: null,
                parseHTML: element => element.getAttribute("data-chunk-id"),
                renderHTML: attributes => attributes["data-chunk-id"] ? { "data-chunk-id": attributes["data-chunk-id"] } : {}
            },
            "data-block-index": {
                default: null,
                parseHTML: element => element.getAttribute("data-block-index"),
                renderHTML: attributes => attributes["data-block-index"] ? { "data-block-index": attributes["data-block-index"] } : {}
            }
        };
    }
});

const RichTextImage = Node.create({
    name: "richTextImage",
    inline: true,
    group: "inline",
    atom: true,
    selectable: true,
    draggable: true,

    addAttributes() {
        return {
            src: { default: null },
            imageId: { default: null },
            displayVariant: { default: "original" },
            altText: { default: "" },
            width: { default: null },
            height: { default: null }
        };
    },

    parseHTML() {
        return [
            {
                tag: "span.rich-text-inline-image",
                getAttrs: element => readImageAttributes(element)
            },
            {
                tag: "img[data-rich-image-id]",
                getAttrs: element => readImageAttributes(element)
            },
            {
                tag: "img[src*='/rich-document-files/']",
                getAttrs: element => readImageAttributes(element)
            }
        ];
    },

    renderHTML({ node }) {
        const spanAttrs = buildInlineImageSpanAttributes(node.attrs);
        const imgAttrs = buildImageDomAttributes(node.attrs);
        return ["span", spanAttrs, ["img", imgAttrs]];
    }
});

const extensions = [
    StarterKit.configure({
        heading: false,
        paragraph: false
    }),
    Paragraph,
    CustomHeading.configure({ levels: [1, 2, 3] }),
    RichTextImage,
    Underline
];

const registries = new Map();
let activeImageMenu = null;

function getRegistry(viewportElementId) {
    let registry = registries.get(viewportElementId);
    if (!registry) {
        registry = {
            editors: new Map(),
            activeSortOrder: null,
            dotNetReference: null,
            documentId: null
        };
        registries.set(viewportElementId, registry);
    }

    return registry;
}

function toSortOrder(value) {
    const sortOrder = Number(value);
    return Number.isFinite(sortOrder) ? sortOrder : -1;
}

function toPositiveInt(value) {
    const number = Number(value);
    return Number.isFinite(number) && number > 0
        ? Math.round(number)
        : null;
}

function readImageAttributes(element) {
    if (!element) {
        return false;
    }

    const isImage = element.matches?.("img") === true;
    const imageElement = isImage ? element : element.querySelector?.("img");
    const src = imageElement?.getAttribute("src") || element.getAttribute("src") || "";
    const parsed = parseRichDocumentImageUrl(src);
    const imageId =
        element.getAttribute("data-rich-image-id") ||
        imageElement?.getAttribute("data-rich-image-id") ||
        parsed.imageId;
    if (!imageId) {
        return false;
    }

    return {
        src,
        imageId,
        displayVariant:
            element.getAttribute("data-display-variant") ||
            imageElement?.getAttribute("data-display-variant") ||
            parsed.variant ||
            "original",
        altText:
            element.getAttribute("data-alt-text") ||
            imageElement?.getAttribute("alt") ||
            element.getAttribute("alt") ||
            "",
        width:
            toPositiveInt(element.getAttribute("data-width")) ||
            toPositiveInt(element.getAttribute("width")) ||
            toPositiveInt(imageElement?.getAttribute("width")),
        height:
            toPositiveInt(element.getAttribute("data-height")) ||
            toPositiveInt(element.getAttribute("height")) ||
            toPositiveInt(imageElement?.getAttribute("height"))
    };
}

function buildInlineImageSpanAttributes(attrs) {
    const imageId = attrs.imageId || "";
    const displayVariant = attrs.displayVariant || "original";
    const domAttrs = {
        class: "rich-text-inline-image",
        "data-rich-image-id": imageId,
        "data-display-variant": displayVariant,
        "data-alt-text": attrs.altText || "",
        contenteditable: "false"
    };

    const width = toPositiveInt(attrs.width);
    const height = toPositiveInt(attrs.height);
    if (width) {
        domAttrs["data-width"] = String(width);
    }

    if (height) {
        domAttrs["data-height"] = String(height);
    }

    return domAttrs;
}

function buildImageDomAttributes(attrs) {
    const imageId = attrs.imageId || "";
    const displayVariant = attrs.displayVariant || "original";
    const domAttrs = {
        src: attrs.src || "",
        alt: attrs.altText || "",
        "data-rich-image-id": imageId,
        "data-display-variant": displayVariant,
        loading: "lazy"
    };

    const width = toPositiveInt(attrs.width);
    const height = toPositiveInt(attrs.height);
    if (width) {
        domAttrs.width = String(width);
    }

    if (height) {
        domAttrs.height = String(height);
    }

    const styles = [];
    if (width) {
        styles.push(`width: ${width}px`);
    }

    if (height) {
        styles.push(`height: ${height}px`);
    }

    if (styles.length > 0) {
        domAttrs.style = styles.join("; ");
    }

    return domAttrs;
}

function parseRichDocumentImageUrl(src) {
    const empty = { imageId: "", variant: "original" };
    if (!src) {
        return empty;
    }

    const marker = "/rich-document-files/";
    const markerIndex = src.toLowerCase().indexOf(marker);
    if (markerIndex < 0) {
        return empty;
    }

    const tail = src.slice(markerIndex + marker.length).split(/[?#]/)[0];
    const parts = tail.split("/").filter(Boolean);
    if (parts.length < 4 || parts[1].toLowerCase() !== "images") {
        return empty;
    }

    return {
        imageId: decodeURIComponent(parts[2]),
        variant: decodeURIComponent(parts[3] || "original")
    };
}

function getClipboardImageFile(event) {
    const items = Array.from(event?.clipboardData?.items || []);
    for (const item of items) {
        if (item.kind === "file" && item.type && item.type.startsWith("image/")) {
            const file = item.getAsFile();
            if (file) {
                return file;
            }
        }
    }

    const files = Array.from(event?.clipboardData?.files || []);
    return files.find(file => file.type && file.type.startsWith("image/")) || null;
}

async function uploadRichTextImage(documentId, file) {
    const formData = new FormData();
    formData.append("file", file, file.name || "clipboard-image.png");

    const response = await fetch(`/rich-document-files/${encodeURIComponent(documentId)}/images`, {
        method: "POST",
        body: formData
    });

    if (!response.ok) {
        const message = await response.text();
        throw new Error(message || `Image upload failed (${response.status})`);
    }

    return response.json();
}

async function insertPastedImage(editor, documentId, file) {
    const result = await uploadRichTextImage(documentId, file);
    const imageId = result.imageId ?? result.ImageId ?? "";
    const variant = result.variant ?? result.Variant ?? "original";
    const url = result.url ?? result.Url ?? `/rich-document-files/${documentId}/images/${encodeURIComponent(imageId)}/${encodeURIComponent(variant)}`;
    const fileName = result.fileName ?? result.FileName ?? file.name ?? "";

    if (!imageId) {
        throw new Error("Image upload response does not contain imageId.");
    }

    editor.chain().focus().insertContent({
        type: "richTextImage",
        attrs: {
            src: url,
            imageId,
            displayVariant: variant,
            altText: fileName,
            width: 220
        }
    }).run();
}

function handleImagePaste(viewportElementId, registry, sortOrder, event) {
    const file = getClipboardImageFile(event);
    if (!file || !registry.documentId) {
        return false;
    }

    event.preventDefault();
    const state = registry.editors.get(sortOrder);
    if (!state) {
        return true;
    }

    insertPastedImage(state.editor, registry.documentId, file).catch(error => {
        console.error("[rich-text-image-paste]", error);
    });
    return true;
}

function hideImageSizeMenu() {
    if (!activeImageMenu) {
        return;
    }

    const menu = activeImageMenu;
    activeImageMenu = null;
    document.removeEventListener("mousedown", menu.onDocumentMouseDown, true);
    document.removeEventListener("keydown", menu.onDocumentKeyDown, true);
    window.removeEventListener("resize", menu.onWindowChange, true);
    window.removeEventListener("scroll", menu.onWindowChange, true);
    menu.element.remove();
}

function handleImageContextMenu(registry, sortOrder, event) {
    const target = event.target instanceof Element ? event.target : null;
    const image = target?.closest("span.rich-text-inline-image, span.rich-text-inline-image img, img[data-rich-image-id], p.rich-text-image img");
    const state = registry.editors.get(sortOrder);
    if (!image || !state || !state.editor?.view?.dom?.contains(image)) {
        return false;
    }

    const position = findImageNodePosition(state.editor, image);
    if (position == null) {
        return false;
    }

    event.preventDefault();
    event.stopPropagation();
    showImageSizeMenu(state.editor, position, event);
    return true;
}

function findImageNodePosition(editor, imageElement) {
    const imageAttrs = readImageAttributes(imageElement);
    if (!imageAttrs || !imageAttrs.imageId) {
        return null;
    }

    const markerElement = imageElement.closest?.("span.rich-text-inline-image") || imageElement;
    try {
        const domPosition = editor.view.posAtDOM(markerElement, 0);
        for (const offset of [0, -1, 1]) {
            const position = domPosition + offset;
            const node = position >= 0 ? editor.state.doc.nodeAt(position) : null;
            if (node?.type?.name === "richTextImage") {
                return position;
            }
        }
    } catch {
        // Fallback below handles legacy DOM or browser-specific posAtDOM quirks.
    }

    let match = null;
    editor.state.doc.descendants((node, position) => {
        if (node.type.name !== "richTextImage") {
            return true;
        }

        if (node.attrs.imageId === imageAttrs.imageId) {
            match = position;
            return false;
        }

        return true;
    });

    return match;
}

function showImageSizeMenu(editor, position, event) {
    hideImageSizeMenu();

    const menu = document.createElement("div");
    menu.style.position = "fixed";
    menu.style.zIndex = "10000";
    menu.style.minWidth = "0";
    menu.style.padding = "6px";
    menu.style.border = "1px solid #b8c4d4";
    menu.style.borderRadius = "4px";
    menu.style.background = "#ffffff";
    menu.style.boxShadow = "0 8px 24px rgba(15, 23, 42, 0.18)";
    menu.style.font = "13px/1.3 Arial, sans-serif";
    menu.style.color = "#102033";
    menu.style.userSelect = "none";

    const optionRow = document.createElement("div");
    optionRow.style.display = "flex";
    optionRow.style.alignItems = "center";
    optionRow.style.gap = "4px";

    for (const option of [
        { label: "100px", width: 100 },
        { label: "200px", width: 200 },
        { label: "300px", width: 300 },
        { label: "500px", width: 500 },
        { label: "[orig]", width: null }
    ]) {
        const button = document.createElement("button");
        button.type = "button";
        button.textContent = option.label;
        button.style.border = "1px solid #c9d4e2";
        button.style.borderRadius = "3px";
        button.style.background = "#f8fafc";
        button.style.padding = "4px 6px";
        button.style.cursor = "pointer";
        button.style.color = "#102033";
        button.addEventListener("mousedown", e => e.preventDefault());
        button.addEventListener("click", () => setImageWidth(editor, position, option.width));
        optionRow.appendChild(button);
    }

    const input = document.createElement("input");
    input.type = "number";
    input.min = "1";
    input.step = "1";
    input.placeholder = "custom px";
    input.style.boxSizing = "border-box";
    input.style.width = "92px";
    input.style.marginLeft = "4px";
    input.style.border = "1px solid #c9d4e2";
    input.style.borderRadius = "3px";
    input.style.padding = "5px 6px";
    input.addEventListener("mousedown", e => e.stopPropagation());
    input.addEventListener("keydown", e => {
        if (e.key === "Enter") {
            e.preventDefault();
            const width = toPositiveInt(input.value);
            if (width) {
                setImageWidth(editor, position, width);
            }
        }
    });

    optionRow.appendChild(input);
    menu.appendChild(optionRow);
    document.body.appendChild(menu);

    positionImageMenu(menu, event.clientX, event.clientY);

    const menuState = {
        element: menu,
        onDocumentMouseDown: e => {
            if (!menu.contains(e.target)) {
                hideImageSizeMenu();
            }
        },
        onDocumentKeyDown: e => {
            if (e.key === "Escape") {
                hideImageSizeMenu();
            }
        },
        onWindowChange: () => hideImageSizeMenu()
    };

    activeImageMenu = menuState;
    document.addEventListener("mousedown", menuState.onDocumentMouseDown, true);
    document.addEventListener("keydown", menuState.onDocumentKeyDown, true);
    window.addEventListener("resize", menuState.onWindowChange, true);
    window.addEventListener("scroll", menuState.onWindowChange, true);
    input.focus();
}

function positionImageMenu(menu, clientX, clientY) {
    const margin = 8;
    const rect = menu.getBoundingClientRect();
    const left = Math.min(clientX, window.innerWidth - rect.width - margin);
    const top = Math.min(clientY, window.innerHeight - rect.height - margin);
    menu.style.left = `${Math.max(margin, left)}px`;
    menu.style.top = `${Math.max(margin, top)}px`;
}

function setImageWidth(editor, position, width) {
    const node = editor.state.doc.nodeAt(position);
    if (!node || node.type.name !== "richTextImage") {
        hideImageSizeMenu();
        return;
    }

    const nextAttrs = {
        ...node.attrs,
        width: width ?? null,
        height: null
    };

    const transaction = editor.state.tr.setNodeMarkup(position, undefined, nextAttrs);
    editor.view.dispatch(transaction);
    editor.view.focus();
    hideImageSizeMenu();
}

function createEditor(viewportElementId, registry, host, item) {
    const sortOrder = toSortOrder(item.sortOrder ?? item.SortOrder);
    const chunkId = item.chunkId ?? item.ChunkId ?? "";
    const html = item.html ?? item.Html ?? "";
    const providedOriginalHtml = item.originalHtml ?? item.OriginalHtml ?? null;
    const isDraft = Boolean(item.isDraft ?? item.IsDraft ?? false);

    const editor = new Editor({
        element: host,
        extensions,
        content: html || "<p></p>",
        editorProps: {
            attributes: {
                class: "rich-text-tiptap-content"
            },
            handlePaste: (view, event) => {
                return handleImagePaste(viewportElementId, registry, sortOrder, event);
            },
            handleDOMEvents: {
                contextmenu: (view, event) => {
                    return handleImageContextMenu(registry, sortOrder, event);
                }
            }
        },
        onFocus: () => {
            registry.activeSortOrder = sortOrder;
        },
        onUpdate: () => {
            const state = registry.editors.get(sortOrder);
            if (state) {
                const wasDirty = state.isDirty === true;
                state.isDirty = true;
                if (registry.dotNetReference) {
                    registry.dotNetReference.invokeMethodAsync(
                        "OnEditorChunkEdited",
                        {
                            chunkId: state.chunkId,
                            sortOrder: state.sortOrder,
                            originalHtml: state.originalHtml,
                            html: state.editor.getHTML(),
                            isDirty: true
                        },
                        !wasDirty).catch(() => {});
                }
            }
        }
    });

    const state = {
        editor,
        chunkId,
        sortOrder,
        originalHtml: providedOriginalHtml == null
            ? editor.getHTML()
            : providedOriginalHtml,
        isDirty: isDraft
    };

    registry.editors.set(sortOrder, state);
    host.dataset.richTextEditorInitialized = "true";
}

function notifyEditorDisposed(registry, state, reason) {
    if (!registry.dotNetReference || !state) {
        return;
    }

    registry.dotNetReference
        .invokeMethodAsync(
            "OnEditorChunkDisposed",
            String(state.chunkId ?? ""),
            state.sortOrder,
            state.isDirty === true,
            reason)
        .catch(() => {});
}

function syncEditors(viewportElementId, chunks, dotNetReference, documentId) {
    const viewport = document.getElementById(viewportElementId);
    if (!viewport) {
        return;
    }

    const registry = getRegistry(viewportElementId);
    registry.dotNetReference = dotNetReference ?? registry.dotNetReference;
    registry.documentId = documentId ?? registry.documentId;
    const visibleSortOrders = new Set();
    const items = Array.isArray(chunks) ? chunks : [];
    const itemsBySortOrder = new Map();

    for (const item of items) {
        const sortOrder = toSortOrder(item.sortOrder ?? item.SortOrder);
        if (sortOrder < 0) {
            continue;
        }

        visibleSortOrders.add(sortOrder);
        itemsBySortOrder.set(sortOrder, item);
    }

    for (const [sortOrder, state] of Array.from(registry.editors.entries())) {
        const hostStillExists = viewport.querySelector(`[data-rich-text-editor-host][data-chunk-sort-order="${sortOrder}"]`);
        const editorDomStillAttached = hostStillExists && state.editor?.view?.dom && hostStillExists.contains(state.editor.view.dom);
        if (visibleSortOrders.has(sortOrder) && hostStillExists && editorDomStillAttached) {
            continue;
        }

        if (visibleSortOrders.has(sortOrder) && hostStillExists && state.isDirty === true) {
            itemsBySortOrder.set(sortOrder, {
                chunkId: state.chunkId,
                sortOrder: state.sortOrder,
                html: state.editor.getHTML(),
                originalHtml: state.originalHtml,
                isDraft: true
            });
        }

        notifyEditorDisposed(registry, state, "sync-window");
        state.editor.destroy();
        registry.editors.delete(sortOrder);
    }

    const hosts = viewport.querySelectorAll("[data-rich-text-editor-host]");
    hosts.forEach(host => {
        const sortOrder = toSortOrder(host.getAttribute("data-chunk-sort-order"));
        if (sortOrder < 0 || registry.editors.has(sortOrder)) {
            return;
        }

        const item = itemsBySortOrder.get(sortOrder);
        if (!item) {
            return;
        }

        createEditor(viewportElementId, registry, host, item);
    });
}

function collectEditors(viewportElementId) {
    const registry = registries.get(viewportElementId);
    if (!registry) {
        return [];
    }

    return Array.from(registry.editors.values()).map(state => {
        const isDirty = state.isDirty === true;
        const html = isDirty ? state.editor.getHTML() : "";
        return {
            chunkId: state.chunkId,
            sortOrder: state.sortOrder,
            originalHtml: isDirty ? state.originalHtml : "",
            html: isDirty ? html : "",
            isDirty
        };
    });
}

function destroyEditors(viewportElementId) {
    hideImageSizeMenu();

    const registry = registries.get(viewportElementId);
    if (!registry) {
        return;
    }

    for (const state of registry.editors.values()) {
        notifyEditorDisposed(registry, state, "destroy-viewport");
        state.editor.destroy();
    }

    registries.delete(viewportElementId);
}

function markClean(viewportElementId, sortOrders) {
    const registry = registries.get(viewportElementId);
    if (!registry) {
        return;
    }

    const normalizedSortOrders = new Set((Array.isArray(sortOrders) ? sortOrders : [])
        .map(toSortOrder)
        .filter(sortOrder => sortOrder >= 0));

    for (const [sortOrder, state] of registry.editors.entries()) {
        if (normalizedSortOrders.size > 0 && !normalizedSortOrders.has(sortOrder)) {
            continue;
        }

        state.originalHtml = state.editor.getHTML();
        state.isDirty = false;
    }
}

function getActiveEditor(registry) {
    if (registry.activeSortOrder != null && registry.editors.has(registry.activeSortOrder)) {
        return registry.editors.get(registry.activeSortOrder).editor;
    }

    const first = registry.editors.values().next();
    return first.done ? null : first.value.editor;
}

function runCommand(viewportElementId, command) {
    const registry = registries.get(viewportElementId);
    if (!registry) {
        return;
    }

    const editor = getActiveEditor(registry);
    if (!editor) {
        return;
    }

    switch (command) {
        case "toggleBold":
            editor.chain().focus().toggleBold().run();
            break;
        case "toggleItalic":
            editor.chain().focus().toggleItalic().run();
            break;
        case "toggleUnderline":
            editor.chain().focus().toggleUnderline().run();
            break;
        case "setParagraph":
            editor.chain().focus().setParagraph().run();
            break;
        case "toggleHeading1":
            editor.chain().focus().toggleHeading({ level: 1 }).run();
            break;
        case "toggleHeading2":
            editor.chain().focus().toggleHeading({ level: 2 }).run();
            break;
        case "toggleHeading3":
            editor.chain().focus().toggleHeading({ level: 3 }).run();
            break;
    }
}

window.richTextEditor = {
    syncEditors,
    collectEditors,
    destroyEditors,
    markClean,
    runCommand
};
