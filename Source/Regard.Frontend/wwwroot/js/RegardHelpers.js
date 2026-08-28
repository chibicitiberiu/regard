window.RegardHelpers = {

    clickOutsideHandlers: [],

    _onClick: function (event) {
        for (var i = 0; i < window.RegardHelpers.clickOutsideHandlers.length; i++) {
            var handler = window.RegardHelpers.clickOutsideHandlers[i];
            if (!handler.element.contains(event.target)) {
                handler.dotNetObjectRef.invokeMethodAsync("InvokeClickOutside");
            }
        }
    },

    addClickOutsideHandler: function (element, dotNetObjectRef) {
        this.removeClickOutsideHandler(element);
        this.clickOutsideHandlers.push({
            element: element,
            dotNetObjectRef: dotNetObjectRef
        });
    },

    removeClickOutsideHandler: function (element) {
        if (!(element instanceof Node))
            return;

        for (var i = 0; i < this.clickOutsideHandlers.length; i++) {
            if (this.clickOutsideHandlers[i].element.isSameNode(element)) {
                this.clickOutsideHandlers.splice(i, 1);
            }
        }
    },

    escapeHandlers: [],

    _onKeydown: function (event) {
        if (event.key !== "Escape")
            return;
        // Iterate a copy: a handler may unregister (dispose) itself during the callback.
        var handlers = window.RegardHelpers.escapeHandlers.slice();
        for (var i = 0; i < handlers.length; i++) {
            handlers[i].dotNetObjectRef.invokeMethodAsync("InvokeEscape");
        }
    },

    addEscapeHandler: function (element, dotNetObjectRef) {
        this.removeEscapeHandler(element);
        this.escapeHandlers.push({
            element: element,
            dotNetObjectRef: dotNetObjectRef
        });
    },

    removeEscapeHandler: function (element) {
        if (!(element instanceof Node))
            return;

        for (var i = 0; i < this.escapeHandlers.length; i++) {
            if (this.escapeHandlers[i].element.isSameNode(element)) {
                this.escapeHandlers.splice(i, 1);
            }
        }
    },

    // --- Watch-progress tracking -------------------------------------------------------------
    // Fires a one-shot .NET callback once the playhead crosses `threshold` (0..1) of the video's
    // duration, so a video counts as watched at ~90% rather than only when it plays out to the end.

    watchProgressHandlers: [],

    addWatchProgressHandler: function (videoElement, dotNetObjectRef, threshold) {
        this.removeWatchProgressHandler(videoElement);

        var handler = {
            element: videoElement,
            dotNetObjectRef: dotNetObjectRef,
            threshold: threshold,
            fired: false,
            listener: null
        };
        handler.listener = function () {
            if (handler.fired)
                return;
            var d = videoElement.duration;
            if (!isFinite(d) || d <= 0)          // unknown/live duration -> can't compute a fraction
                return;
            if (videoElement.currentTime / d >= handler.threshold) {
                handler.fired = true;
                handler.dotNetObjectRef.invokeMethodAsync("OnWatchThresholdReached");
            }
        };
        videoElement.addEventListener("timeupdate", handler.listener);
        this.watchProgressHandlers.push(handler);
    },

    removeWatchProgressHandler: function (videoElement) {
        if (!(videoElement instanceof Node))
            return;

        for (var i = this.watchProgressHandlers.length - 1; i >= 0; i--) {
            var h = this.watchProgressHandlers[i];
            if (h.element.isSameNode(videoElement)) {
                if (h.listener)
                    h.element.removeEventListener("timeupdate", h.listener);
                this.watchProgressHandlers.splice(i, 1);
            }
        }
    }
}

window.addEventListener("click", window.RegardHelpers._onClick);
window.addEventListener("keydown", window.RegardHelpers._onKeydown);