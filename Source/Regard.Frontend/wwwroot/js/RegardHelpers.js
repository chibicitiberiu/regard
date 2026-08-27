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
    }
}

window.addEventListener("click", window.RegardHelpers._onClick);
window.addEventListener("keydown", window.RegardHelpers._onKeydown);