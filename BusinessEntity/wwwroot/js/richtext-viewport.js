function createRichTextViewportRuntime() {
    const registry = new Map();
    const blockSelector = "h1,h2,h3,h4,h5,h6,p,blockquote,pre,ul,ol,table,hr";

    function syncViewportSize(viewportElementId) {
        const viewport = document.getElementById(viewportElementId);
        if (!viewport) {
            return;
        }

        // На узких экранах viewport разворачивается в обычный поток страницы.
        if (window.innerWidth <= 1100) {
            viewport.style.maxHeight = "";
            return;
        }

        const viewportRect = viewport.getBoundingClientRect();
        const bottomGap = 16;
        const minHeight = 240;
        const availableHeight = Math.max(window.innerHeight - viewportRect.top - bottomGap, minHeight);
        viewport.style.maxHeight = `${availableHeight}px`;
    }

    function registerViewport(viewportElementId) {
        registerVirtualViewport(viewportElementId, null);
    }

    function isScrollNotificationSuppressed(viewportElementId) {
        const entry = registry.get(viewportElementId);
        return !!entry &&
            Number.isFinite(entry.suppressScrollNotificationsUntil) &&
            performance.now() < entry.suppressScrollNotificationsUntil;
    }

    function suppressScrollNotifications(viewportElementId, milliseconds) {
        const entry = registry.get(viewportElementId);
        if (!entry || !Number.isFinite(milliseconds) || milliseconds <= 0) {
            return;
        }

        entry.suppressScrollNotificationsUntil = Math.max(
            entry.suppressScrollNotificationsUntil || 0,
            performance.now() + milliseconds);
    }

    function registerVirtualViewport(viewportElementId, dotNetReference) {
        unregisterViewport(viewportElementId);

        const sync = () => syncViewportSize(viewportElementId);
        let frameId = 0;
        let scrollFrameId = 0;
        let isScrollbarDragging = false;

        const requestSync = () => {
            if (frameId !== 0) {
                return;
            }

            frameId = window.requestAnimationFrame(() => {
                frameId = 0;
                sync();
            });
        };

        const notifyScroll = () => {
            const viewport = document.getElementById(viewportElementId);
            if (!viewport || !dotNetReference) {
                return;
            }

            dotNetReference.invokeMethodAsync(
                "OnVirtualViewportScrolled",
                viewport.scrollTop,
                viewport.clientHeight,
                viewport.scrollHeight);
        };

        const notifyScrollbarReleased = () => {
            const viewport = document.getElementById(viewportElementId);
            if (!viewport || !dotNetReference) {
                return;
            }

            dotNetReference.invokeMethodAsync(
                "OnVirtualViewportScrollbarReleased",
                viewport.scrollTop,
                viewport.clientHeight,
                viewport.scrollHeight);
        };

        const requestScrollNotification = () => {
            if (!dotNetReference ||
                scrollFrameId !== 0 ||
                isScrollbarDragging ||
                isScrollNotificationSuppressed(viewportElementId)) {
                return;
            }

            scrollFrameId = window.requestAnimationFrame(() => {
                scrollFrameId = 0;
                if (isScrollbarDragging || isScrollNotificationSuppressed(viewportElementId)) {
                    return;
                }

                notifyScroll();
            });
        };

        const isVerticalScrollbarPointerEvent = (event) => {
            const viewport = document.getElementById(viewportElementId);
            if (!viewport) {
                return false;
            }

            const rect = viewport.getBoundingClientRect();
            const scrollbarWidth = viewport.offsetWidth - viewport.clientWidth;
            if (scrollbarWidth <= 0) {
                return false;
            }

            return event.clientX >= rect.left + viewport.clientWidth &&
                event.clientX <= rect.right &&
                event.clientY >= rect.top &&
                event.clientY <= rect.bottom;
        };

        const handleMouseDown = (event) => {
            if (!dotNetReference || !isVerticalScrollbarPointerEvent(event)) {
                return;
            }

            isScrollbarDragging = true;
        };

        const handleMouseUp = () => {
            if (!isScrollbarDragging) {
                return;
            }

            isScrollbarDragging = false;
            if (scrollFrameId !== 0) {
                window.cancelAnimationFrame(scrollFrameId);
                scrollFrameId = 0;
            }

            notifyScrollbarReleased();
        };

        const handleKeyDown = (event) => {
            if (event.key !== "PageDown" && event.key !== "PageUp") {
                return;
            }

            window.setTimeout(requestScrollNotification, 0);
        };

        const viewport = document.getElementById(viewportElementId);
        if (viewport) {
            viewport.addEventListener("scroll", requestScrollNotification, { passive: true });
            viewport.addEventListener("keydown", handleKeyDown);
            viewport.addEventListener("mousedown", handleMouseDown);
        }

        registry.set(viewportElementId, {
            requestSync,
            requestScrollNotification,
            handleKeyDown,
            handleMouseDown,
            handleMouseUp,
            dotNetReference,
            suppressScrollNotificationsUntil: 0
        });
        window.addEventListener("resize", requestSync, { passive: true });
        window.addEventListener("scroll", requestSync, { passive: true });
        window.addEventListener("mouseup", handleMouseUp);
        sync();
        measureChunks(viewportElementId);
    }

    function unregisterViewport(viewportElementId) {
        const entry = registry.get(viewportElementId);
        if (!entry) {
            return;
        }

        const viewport = document.getElementById(viewportElementId);
        if (viewport) {
            viewport.removeEventListener("scroll", entry.requestScrollNotification);
            viewport.removeEventListener("keydown", entry.handleKeyDown);
            viewport.removeEventListener("mousedown", entry.handleMouseDown);
        }

        window.removeEventListener("resize", entry.requestSync);
        window.removeEventListener("scroll", entry.requestSync);
        window.removeEventListener("mouseup", entry.handleMouseUp);
        registry.delete(viewportElementId);
    }

    function getChunkBlocks(chunk) {
        const editorHost = chunk.querySelector("[data-rich-text-editor-host]");
        if (editorHost) {
            const editorRoot = editorHost.querySelector(".ProseMirror");
            return Array.from((editorRoot || editorHost).children)
                .filter(element => element.matches(blockSelector));
        }

        return Array.from(chunk.children)
            .filter(element => element.matches(blockSelector));
    }

    function annotateChunkBlocks(viewportElementId) {
        const viewport = document.getElementById(viewportElementId);
        if (!viewport) {
            return;
        }

        viewport.querySelectorAll("[data-rich-text-chunk]").forEach(chunk => {
            getChunkBlocks(chunk).forEach((block, index) => {
                block.setAttribute("data-rich-text-block-index", String(index));
            });
        });
    }

    function getCurrentViewportPosition(viewportElementId) {
        annotateChunkBlocks(viewportElementId);

        const viewport = document.getElementById(viewportElementId);
        if (!viewport) {
            return null;
        }

        const viewportRect = viewport.getBoundingClientRect();
        const anchorY = viewportRect.top + viewportRect.height * 0.30;
        const blocks = [];

        viewport.querySelectorAll("[data-rich-text-chunk]").forEach(chunk => {
            const sortOrder = Number(chunk.getAttribute("data-chunk-sort-order"));
            if (!Number.isFinite(sortOrder)) {
                return;
            }

            getChunkBlocks(chunk).forEach((block, index) => {
                const rect = block.getBoundingClientRect();
                blocks.push({
                    chunkSortOrder: sortOrder,
                    blockIndex: index,
                    rect
                });
            });
        });

        if (blocks.length === 0) {
            return null;
        }

        const visible = blocks
            .filter(item => item.rect.bottom >= viewportRect.top && item.rect.top <= viewportRect.bottom)
            .sort((left, right) =>
                Math.abs((left.rect.top + left.rect.bottom) / 2 - anchorY) -
                Math.abs((right.rect.top + right.rect.bottom) / 2 - anchorY));

        const selected = visible.length > 0 ? visible[0] : null;
        if (!selected) {
            return null;
        }

        return {
            chunkSortOrder: selected.chunkSortOrder,
            blockIndex: selected.blockIndex
        };
    }

    function scrollToBlock(viewportElementId, chunkSortOrder, blockIndex, behavior = "auto", suppressScrollMs = 0) {
        annotateChunkBlocks(viewportElementId);

        const viewport = document.getElementById(viewportElementId);
        if (!viewport) {
            return false;
        }

        const sortOrder = Number(chunkSortOrder);
        const normalizedBlockIndex = Number(blockIndex);
        if (!Number.isFinite(sortOrder) || !Number.isFinite(normalizedBlockIndex)) {
            return false;
        }

        const chunk = viewport.querySelector(`[data-rich-text-chunk][data-chunk-sort-order="${sortOrder}"]`);
        if (!chunk) {
            return false;
        }

        const target = getChunkBlocks(chunk)[normalizedBlockIndex];
        if (!target) {
            return false;
        }

        const viewportRect = viewport.getBoundingClientRect();
        const targetRect = target.getBoundingClientRect();
        const nextScrollTop = viewport.scrollTop + (targetRect.top - viewportRect.top) - 12;

        suppressScrollNotifications(viewportElementId, suppressScrollMs);
        viewport.scrollTo({
            top: Math.max(nextScrollTop, 0),
            behavior: behavior || "auto",
        });

        return true;
    }

    function scrollToHeading(viewportElementId, headingId, behavior = "smooth", suppressScrollMs = 0) {
        const viewport = document.getElementById(viewportElementId);
        if (!viewport) {
            return false;
        }

        const target = document.getElementById(headingId);
        if (!target) {
            return false;
        }

        if (!viewport.contains(target)) {
            return false;
        }

        const viewportRect = viewport.getBoundingClientRect();
        const targetRect = target.getBoundingClientRect();
        const nextScrollTop = viewport.scrollTop + (targetRect.top - viewportRect.top) - 12;

        suppressScrollNotifications(viewportElementId, suppressScrollMs);
        viewport.scrollTo({
            top: Math.max(nextScrollTop, 0),
            behavior: behavior || "auto",
        });

        return true;
    }

    function scrollToChunk(viewportElementId, sortOrder, behavior = "auto", suppressScrollMs = 0) {
        const viewport = document.getElementById(viewportElementId);
        if (!viewport) {
            return false;
        }

        const target = viewport.querySelector(`[data-rich-text-chunk][data-chunk-sort-order="${sortOrder}"]`);
        if (!target) {
            return false;
        }

        const viewportRect = viewport.getBoundingClientRect();
        const targetRect = target.getBoundingClientRect();
        const nextScrollTop = viewport.scrollTop + (targetRect.top - viewportRect.top) - 12;

        suppressScrollNotifications(viewportElementId, suppressScrollMs);
        viewport.scrollTo({
            top: Math.max(nextScrollTop, 0),
            behavior: behavior || "auto",
        });

        return true;
    }

    function ensureChunkVisible(viewportElementId, sortOrder, behavior = "auto", suppressScrollMs = 0) {
        const viewport = document.getElementById(viewportElementId);
        if (!viewport) {
            return false;
        }

        const target = viewport.querySelector(`[data-rich-text-chunk][data-chunk-sort-order="${sortOrder}"]`);
        if (!target) {
            return false;
        }

        const viewportRect = viewport.getBoundingClientRect();
        const targetRect = target.getBoundingClientRect();
        const topLimit = viewportRect.top + 8;
        const bottomLimit = viewportRect.bottom - 8;

        if (targetRect.bottom < topLimit || targetRect.top > bottomLimit) {
            return scrollToChunk(viewportElementId, sortOrder, behavior, suppressScrollMs);
        }

        return true;
    }

    function getCurrentChunkSortOrder(viewportElementId) {
        const viewport = document.getElementById(viewportElementId);
        if (!viewport) {
            return null;
        }

        const chunks = Array.from(viewport.querySelectorAll("[data-rich-text-chunk]"))
            .map((element) => {
                const sortOrder = Number(element.getAttribute("data-chunk-sort-order"));
                return {
                    element,
                    sortOrder,
                    rect: element.getBoundingClientRect()
                };
            })
            .filter((item) => Number.isFinite(item.sortOrder));

        if (chunks.length === 0) {
            return null;
        }

        const viewportRect = viewport.getBoundingClientRect();
        const anchorY = viewportRect.top + viewportRect.height * 0.35;

        const intersecting = chunks
            .filter((item) => item.rect.bottom >= viewportRect.top && item.rect.top <= viewportRect.bottom)
            .sort((left, right) =>
                Math.abs((left.rect.top + left.rect.bottom) / 2 - anchorY) -
                Math.abs((right.rect.top + right.rect.bottom) / 2 - anchorY));

        if (intersecting.length > 0) {
            return intersecting[0].sortOrder;
        }

        chunks.sort((left, right) =>
            Math.abs((left.rect.top + left.rect.bottom) / 2 - anchorY) -
            Math.abs((right.rect.top + right.rect.bottom) / 2 - anchorY));

        return chunks[0].sortOrder;
    }

    function measureChunks(viewportElementId) {
        const viewport = document.getElementById(viewportElementId);
        const entry = registry.get(viewportElementId);
        if (!viewport || !entry || !entry.dotNetReference) {
            return;
        }

        annotateChunkBlocks(viewportElementId);

        const measurements = Array.from(viewport.querySelectorAll("[data-rich-text-chunk]"))
            .map((element) => ({
                sortOrder: Number(element.getAttribute("data-chunk-sort-order")),
                height: element.getBoundingClientRect().height
            }))
            .filter((item) => Number.isFinite(item.sortOrder) && Number.isFinite(item.height) && item.height > 0);

        if (measurements.length === 0) {
            return;
        }

        entry.dotNetReference.invokeMethodAsync("OnChunkHeightsMeasured", measurements);
    }

    return {
        syncViewportSize,
        registerViewport,
        registerVirtualViewport,
        unregisterViewport,
        scrollToHeading,
        scrollToChunk,
        ensureChunkVisible,
        suppressScrollNotifications,
        getCurrentViewportPosition,
        scrollToBlock,
        getCurrentChunkSortOrder,
        measureChunks
    };
}

window.richTextReadViewport = createRichTextViewportRuntime();
window.richTextEditViewport = createRichTextViewportRuntime();
window.richTextOutlineViewport = createRichTextViewportRuntime();

// Backward-compatible alias for any old markup still loaded in the browser.
window.richTextViewport = window.richTextReadViewport;
