window.RegardHelpers = {

    clickOutsideHandlers: [],

    _onClick: function (event) {
        var handlers = window.RegardHelpers.clickOutsideHandlers;
        // Iterate backwards so we can prune stale entries in place.
        for (var i = handlers.length - 1; i >= 0; i--) {
            var handler = handlers[i];
            // Self-heal: if a panel's element is gone from the DOM, its .NET object may already be
            // disposed and invoking it throws "no tracked object with id". Drop it and skip.
            if (!handler.element || !document.contains(handler.element)) {
                handlers.splice(i, 1);
                continue;
            }
            // A click inside the popup OR on its trigger button is NOT "outside". Excluding the trigger
            // is what makes this robust: the very click that opens the menu targets the trigger, so it can
            // never dismiss the menu it just opened (a slow/debug build could otherwise beat the old
            // time-based guard and close the menu instantly — the popup then never appears).
            var insidePopup = handler.element.contains(event.target);
            var onTrigger = handler.trigger && handler.trigger.contains && handler.trigger.contains(event.target);
            if (!insidePopup && !onTrigger) {
                var p = handler.dotNetObjectRef.invokeMethodAsync("InvokeClickOutside");
                // Swallow a rejection if the ref was disposed between the DOM check and the call.
                if (p && typeof p.catch === "function")
                    p.catch(function () { });
            }
        }
    },

    // Keyed by a stable id (not the DOM element): a panel's @ref is nulled before its dispose runs, so
    // removing by element would leak the handler (and its disposed .NET ref). `trigger` is the element
    // that toggles the panel (so clicking it doesn't count as an outside click).
    addClickOutsideHandler: function (element, dotNetObjectRef, id, trigger) {
        if (!(element instanceof Node))
            return;
        this.removeClickOutsideHandler(id);
        this.clickOutsideHandlers.push({
            id: id,
            element: element,
            trigger: (trigger instanceof Node) ? trigger : null,
            dotNetObjectRef: dotNetObjectRef
        });
    },

    removeClickOutsideHandler: function (id) {
        for (var i = this.clickOutsideHandlers.length - 1; i >= 0; i--) {
            if (this.clickOutsideHandlers[i].id === id) {
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
    },

    // Seek once, as soon as metadata is available, to resume where playback was left off.
    seekOnLoad: function (videoElement, seconds) {
        if (!(videoElement instanceof Node) || !isFinite(seconds) || seconds <= 0)
            return;
        var seek = function () {
            try { videoElement.currentTime = seconds; } catch (e) { /* ignore */ }
        };
        if (videoElement.readyState >= 1)            // metadata already loaded
            seek();
        else
            videoElement.addEventListener("loadedmetadata", seek, { once: true });
    },

    // --- Playback position reporting ---------------------------------------------------------
    // Reports currentTime back to .NET (OnPositionReport) at most every `minDelta` seconds while
    // playing, and once on pause/ended, so the resume point survives leaving the page. Also flushed
    // synchronously from the component's DisposeAsync (in-app navigation, where no unload fires).

    positionReportHandlers: [],

    addPositionReportHandler: function (videoElement, dotNetObjectRef, minDelta) {
        this.removePositionReportHandler(videoElement);
        var delta = (minDelta && minDelta > 0) ? minDelta : 5;

        var handler = {
            element: videoElement,
            dotNetObjectRef: dotNetObjectRef,
            minDelta: delta,
            lastReported: -1e9,
            report: null,
            onTime: null,
            onPause: null
        };
        handler.report = function () {
            var d = videoElement.duration;
            if (!isFinite(d) || d <= 0)              // live/unknown -> nothing meaningful to resume
                return;
            var t = videoElement.currentTime;
            handler.lastReported = t;
            // Report the duration too so the backend can backfill Video.Duration (needed to draw the bar).
            try { handler.dotNetObjectRef.invokeMethodAsync("OnPositionReport", t, d); } catch (e) { }
        };
        handler.onTime = function () {
            var t = videoElement.currentTime;
            if (Math.abs(t - handler.lastReported) >= handler.minDelta)
                handler.report();
        };
        handler.onPause = function () { handler.report(); };

        videoElement.addEventListener("timeupdate", handler.onTime);
        videoElement.addEventListener("pause", handler.onPause);
        videoElement.addEventListener("ended", handler.onPause);
        this.positionReportHandlers.push(handler);
    },

    // Report the current position immediately (used by the component's dispose-time flush).
    flushPositionReport: function (videoElement) {
        if (!(videoElement instanceof Node))
            return;
        for (var i = 0; i < this.positionReportHandlers.length; i++) {
            var h = this.positionReportHandlers[i];
            if (h.element.isSameNode(videoElement)) {
                h.report();
                return;
            }
        }
    },

    removePositionReportHandler: function (videoElement) {
        if (!(videoElement instanceof Node))
            return;
        for (var i = this.positionReportHandlers.length - 1; i >= 0; i--) {
            var h = this.positionReportHandlers[i];
            if (h.element.isSameNode(videoElement)) {
                h.element.removeEventListener("timeupdate", h.onTime);
                h.element.removeEventListener("pause", h.onPause);
                h.element.removeEventListener("ended", h.onPause);
                this.positionReportHandlers.splice(i, 1);
            }
        }
    },

    // Resets the scroll position of the element matching the given selector (e.g. the video grid on
    // page/filter change). No-op if it isn't in the DOM.
    scrollToTop: function (selector) {
        var el = document.querySelector(selector);
        if (el)
            el.scrollTop = 0;
    }
}

window.addEventListener("click", window.RegardHelpers._onClick);
window.addEventListener("keydown", window.RegardHelpers._onKeydown);