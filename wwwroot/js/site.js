(() => {
    const overlay = document.getElementById("global-loading-overlay");
    const message = document.getElementById("global-loading-message");

    if (!overlay || !message) {
        return;
    }

    let visible = false;

    const show = (customMessage) => {
        if (visible) {
            if (customMessage) {
                message.textContent = customMessage;
            }

            return;
        }

        message.textContent = customMessage || "Aguarde um instante...";
        overlay.classList.add("visible");
        overlay.setAttribute("aria-hidden", "false");
        document.body.classList.add("global-loading-active");
        visible = true;
    };

    const hide = () => {
        overlay.classList.remove("visible");
        overlay.setAttribute("aria-hidden", "true");
        document.body.classList.remove("global-loading-active");
        visible = false;
    };

    const isSameOriginLink = (anchor) => {
        try {
            const url = new URL(anchor.href, window.location.href);
            return url.origin === window.location.origin;
        } catch {
            return false;
        }
    };

    window.appLoading = { show, hide };

    document.addEventListener("click", (event) => {
        const anchor = event.target.closest("a[href]");
        if (!anchor) {
            return;
        }

        if (anchor.dataset.skipGlobalLoading === "true") {
            return;
        }

        if (anchor.target === "_blank" || anchor.hasAttribute("download")) {
            return;
        }

        if (event.metaKey || event.ctrlKey || event.shiftKey || event.altKey || event.button !== 0) {
            return;
        }

        if (!isSameOriginLink(anchor)) {
            return;
        }

        const href = anchor.getAttribute("href");
        if (!href || href.startsWith("#")) {
            return;
        }

        show(anchor.dataset.loadingMessage);
    });

    document.addEventListener("submit", (event) => {
        const form = event.target;
        if (!(form instanceof HTMLFormElement)) {
            return;
        }

        if (form.dataset.skipGlobalLoading === "true") {
            return;
        }

        show(form.dataset.loadingMessage);
    });

    window.addEventListener("pageshow", hide);
})();
