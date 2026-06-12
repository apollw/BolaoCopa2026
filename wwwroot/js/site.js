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

    const parseDownloadFileName = (contentDisposition) => {
        if (!contentDisposition) {
            return null;
        }

        const utf8Match = contentDisposition.match(/filename\*\s*=\s*UTF-8''([^;]+)/i);
        if (utf8Match?.[1]) {
            return decodeURIComponent(utf8Match[1]);
        }

        const asciiMatch = contentDisposition.match(/filename\s*=\s*"?([^";]+)"?/i);
        return asciiMatch?.[1] ?? null;
    };

    const downloadFile = async (url, customMessage) => {
        show(customMessage || "Preparando arquivo...");

        try {
            const requestUrl = new URL(url, window.location.href);
            requestUrl.searchParams.set("_dl", Date.now().toString());

            const response = await fetch(requestUrl, {
                cache: "no-store",
                credentials: "same-origin",
                headers: {
                    "X-Requested-With": "XMLHttpRequest"
                }
            });

            const contentType = response.headers.get("content-type") || "";
            if (response.redirected || !contentType.includes("image/png")) {
                hide();
                window.location.assign(response.url);
                return;
            }

            if (!response.ok) {
                throw new Error("Nao foi possivel baixar o arquivo agora.");
            }

            const blob = await response.blob();
            if (!blob.size) {
                throw new Error("O arquivo gerado veio vazio.");
            }

            const fileName = parseDownloadFileName(response.headers.get("content-disposition")) || "comprovante.png";
            const blobUrl = window.URL.createObjectURL(blob);
            const anchor = document.createElement("a");
            anchor.href = blobUrl;
            anchor.download = fileName;
            document.body.appendChild(anchor);
            anchor.click();
            anchor.remove();
            window.setTimeout(() => window.URL.revokeObjectURL(blobUrl), 1000);
        } finally {
            hide();
        }
    };

    window.appLoading = { show, hide };
    window.appDownloads = { downloadFile };

    document.addEventListener("click", (event) => {
        const downloadTrigger = event.target.closest("[data-fetch-download-url]");
        if (downloadTrigger) {
            event.preventDefault();
            downloadFile(downloadTrigger.dataset.fetchDownloadUrl, downloadTrigger.dataset.loadingMessage)
                .catch(error => {
                    window.alert(error?.message || "Nao foi possivel baixar o arquivo agora.");
                });
            return;
        }

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
