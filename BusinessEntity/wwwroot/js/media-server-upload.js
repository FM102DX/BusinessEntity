(function () {
    const activeUploads = new Map();

    function createJobId() {
        if (window.crypto && typeof window.crypto.randomUUID === "function") {
            return window.crypto.randomUUID();
        }

        return "xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx".replace(/[xy]/g, function (char) {
            const value = Math.random() * 16 | 0;
            const resolved = char === "x" ? value : (value & 0x3 | 0x8);
            return resolved.toString(16);
        });
    }

    function invoke(dotNetReference, methodName) {
        if (!dotNetReference) {
            return;
        }

        dotNetReference.invokeMethodAsync(methodName).catch(function () { });
    }

    function startVideoUpload(file, dotNetReference, options) {
        options = options || {};
        const jobId = createJobId();
        const xhr = new XMLHttpRequest();
        let lastNotifyAt = 0;
        activeUploads.set(jobId, xhr);

        function notifyChanged(force) {
            const now = Date.now();
            if (!force && now - lastNotifyAt < 500) {
                return;
            }

            lastNotifyAt = now;
            invoke(dotNetReference, "OnMediaVideoUploadJobsChanged");
        }

        xhr.open("PUT", `/media-server-files/video-upload-jobs/${jobId}`, true);
        xhr.setRequestHeader("X-File-Name", encodeURIComponent(file.name || "video"));
        xhr.setRequestHeader("X-Content-Type", file.type || "application/octet-stream");
        xhr.setRequestHeader("X-File-Length", String(file.size || 0));
        if (options.clientUploadToken) {
            xhr.setRequestHeader("X-Client-Upload-Token", encodeURIComponent(options.clientUploadToken));
        }

        xhr.upload.onprogress = function () {
            notifyChanged(false);
        };

        xhr.onload = function () {
            activeUploads.delete(jobId);
            notifyChanged(true);
        };

        xhr.onerror = function () {
            activeUploads.delete(jobId);
            notifyChanged(true);
        };

        xhr.onabort = function () {
            activeUploads.delete(jobId);
            notifyChanged(true);
        };

        xhr.send(file);
        notifyChanged(true);
        return jobId;
    }

    window.mediaServerUpload = {
        startVideos: function (inputId, dotNetReference) {
            const input = document.getElementById(inputId);
            if (!input || !input.files || input.files.length === 0) {
                return 0;
            }

            const files = Array.from(input.files);
            files.forEach(function (file) {
                startVideoUpload(file, dotNetReference);
            });

            input.value = "";
            return files.length;
        },

        startFirstVideo: function (inputId, clientUploadToken, dotNetReference) {
            const input = document.getElementById(inputId);
            if (!input || !input.files || input.files.length === 0) {
                return "";
            }

            const file = input.files[0];
            const jobId = startVideoUpload(file, dotNetReference, { clientUploadToken: clientUploadToken || "" });
            input.value = "";
            return jobId;
        },

        cancel: function (jobId) {
            const xhr = activeUploads.get(jobId);
            if (xhr) {
                xhr.abort();
                activeUploads.delete(jobId);
            }

            return fetch(`/media-server-files/video-upload-jobs/${encodeURIComponent(jobId)}/cancel`, {
                method: "POST"
            }).catch(function () { });
        }
    };
})();
