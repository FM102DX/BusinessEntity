import { Editor } from "@tiptap/core";
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

const extensions = [
    StarterKit.configure({
        heading: false,
        paragraph: false
    }),
    Paragraph,
    CustomHeading.configure({ levels: [1, 2, 3] }),
    Underline
];

const registries = new Map();

function getRegistry(viewportElementId) {
    let registry = registries.get(viewportElementId);
    if (!registry) {
        registry = {
            editors: new Map(),
            activeSortOrder: null,
            dotNetReference: null
        };
        registries.set(viewportElementId, registry);
    }

    return registry;
}

function toSortOrder(value) {
    const sortOrder = Number(value);
    return Number.isFinite(sortOrder) ? sortOrder : -1;
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

function syncEditors(viewportElementId, chunks, dotNetReference) {
    const viewport = document.getElementById(viewportElementId);
    if (!viewport) {
        return;
    }

    const registry = getRegistry(viewportElementId);
    registry.dotNetReference = dotNetReference ?? registry.dotNetReference;
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
