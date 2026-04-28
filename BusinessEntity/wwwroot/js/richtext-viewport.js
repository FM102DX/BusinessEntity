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
        unregisterViewport(viewportElementId);

        const sync = () => syncViewportSize(viewportElementId);
        let frameId = 0;

        const requestSync = () => {
            if (frameId !== 0) {
                return;
            }

            frameId = window.requestAnimationFrame(() => {
                frameId = 0;
                sync();
            });
        };

        registry.set(viewportElementId, requestSync);
        window.addEventListener("resize", requestSync, { passive: true });
        window.addEventListener("scroll", requestSync, { passive: true });
        sync();
    }

    function unregisterViewport(viewportElementId) {
        const requestSync = registry.get(viewportElementId);
        if (!requestSync) {
            return;
        }

        window.removeEventListener("resize", requestSync);
        window.removeEventListener("scroll", requestSync);
        registry.delete(viewportElementId);
    }

    function scrollToHeading(viewportElementId, headingId) {
        const viewport = document.getElementById(viewportElementId);
        if (!viewport) {
            return;
        }

        const target = viewport.querySelector(`[id="${headingId}"]`);
        if (!target) {
            return;
        }

        const viewportRect = viewport.getBoundingClientRect();
        const targetRect = target.getBoundingClientRect();
        const nextScrollTop = viewport.scrollTop + (targetRect.top - viewportRect.top) - 12;

        viewport.scrollTo({
            top: Math.max(nextScrollTop, 0),
            behavior: "smooth",
        });
    }

    return {
        registerViewport,
        unregisterViewport,
        scrollToHeading
    };
})();
