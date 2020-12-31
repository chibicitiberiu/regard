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
        this.clickOutsideHandlers.push({
            element: element,
            dotNetObjectRef: dotNetObjectRef
        });
    },

    removeClickOutsideHandler: function (element) {
        for (var i = 0; i < this.clickOutsideHandlers.length; i++) {
            if (this.clickOutsideHandlers[i].element.isSameNode(element)) {
                this.clickOutsideHandlers.splice(i, 1);
            }
        }
    }
}

window.addEventListener("click", window.RegardHelpers._onClick);