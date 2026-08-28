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
    },

    // --- SponsorBlock in-player skip ---------------------------------------------------------
    // Jumps the playhead past any SponsorBlock segment it enters (segments are [{start,end}] seconds
    // on the original timeline). Only wired for files that were NOT cut at download time.

    skipSegmentHandlers: [],

    addSkipSegmentsHandler: function (videoElement, segments) {
        this.removeSkipSegmentsHandler(videoElement);
        if (!segments || !segments.length)
            return;

        var handler = { element: videoElement, segments: segments, listener: null };
        handler.listener = function () {
            var t = videoElement.currentTime;
            for (var i = 0; i < segments.length; i++) {
                var s = segments[i];
                // epsilon so we don't re-trigger right at the end / fight a manual seek to the start
                if (t >= s.start && t < s.end - 0.3) {
                    videoElement.currentTime = s.end;
                    break;
                }
            }
        };
        videoElement.addEventListener("timeupdate", handler.listener);
        this.skipSegmentHandlers.push(handler);
    },

    removeSkipSegmentsHandler: function (videoElement) {
        if (!(videoElement instanceof Node))
            return;

        for (var i = this.skipSegmentHandlers.length - 1; i >= 0; i--) {
            var h = this.skipSegmentHandlers[i];
            if (h.element.isSameNode(videoElement)) {
                if (h.listener)
                    h.element.removeEventListener("timeupdate", h.listener);
                this.skipSegmentHandlers.splice(i, 1);
            }
        }
    },

    // --- Seeking ------------------------------------------------------------------------------
    // Seek the player to an absolute time in seconds (chapters click-to-seek).

    seekTo: function (videoElement, seconds) {
        if (!(videoElement instanceof Node) || !isFinite(seconds))
            return;
        try { videoElement.currentTime = Math.max(0, seconds); } catch (e) { /* not ready yet */ }
    }
}

window.addEventListener("click", window.RegardHelpers._onClick);
window.addEventListener("keydown", window.RegardHelpers._onKeydown);