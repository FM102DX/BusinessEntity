window.richTextViewport = {
    scrollToHeading: function (viewportElementId, headingId) {
        const viewport = document.getElementById(viewportElementId);
        if (!viewport) {
            return;
        }

        // Работаем только внутри viewport rich-text документа.
        // Внешнюю страницу при клике по содержанию прокручивать не нужно.
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
};
