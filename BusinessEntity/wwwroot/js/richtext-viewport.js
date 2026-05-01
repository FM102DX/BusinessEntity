window.richTextViewport = (() => {
    const registry = new Map();

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
            if (!dotNetReference || scrollFrameId !== 0 || isScrollbarDragging) {
                return;
            }

            scrollFrameId = window.requestAnimationFrame(() => {
                scrollFrameId = 0;
                if (isScrollbarDragging) {
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
            dotNetReference
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

    function scrollToHeading(viewportElementId, headingId) {
        const viewport = document.getElementById(viewportElementId);
        if (!viewport) {
            return false;
        }

        const target = viewport.querySelector(`[id="${headingId}"]`);
        if (!target) {
            return false;
        }

        const viewportRect = viewport.getBoundingClientRect();
        const targetRect = target.getBoundingClientRect();
        const nextScrollTop = viewport.scrollTop + (targetRect.top - viewportRect.top) - 12;

        viewport.scrollTo({
            top: Math.max(nextScrollTop, 0),
            behavior: "smooth",
        });

        return true;
    }

    function measureChunks(viewportElementId) {
        const viewport = document.getElementById(viewportElementId);
        const entry = registry.get(viewportElementId);
        if (!viewport || !entry || !entry.dotNetReference) {
            return;
        }

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
        measureChunks
    };
})();
