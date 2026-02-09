export class Component {
    setEventListener(element, type, listener, options) {
        if (typeof listener === "string") {
            listener = this._resolveMethodListener(listener);
        }

        this._registeredEventListeners ??= new Map();

        let listenersByType = this._registeredEventListeners.get(element);
        if (!listenersByType) {
            listenersByType = new Map();
            this._registeredEventListeners.set(element, listenersByType);
        }

        const existing = listenersByType.get(type);
        if (existing) {
            element.removeEventListener(type, existing.listener, existing.options);
        }

        listenersByType.set(type, { listener, options });
        element.addEventListener(type, listener, options);
    }

    removeEventListener(element, type) {
        const listeners = this._registeredEventListeners;
        if (!listeners) return;

        const listenersByType = listeners.get(element);
        if (!listenersByType) return;

        const entry = listenersByType.get(type);
        if (!entry) return;

        element.removeEventListener(type, entry.listener, entry.options);
        listenersByType.delete(type);

        if (listenersByType.size === 0) {
            listeners.delete(element);
        }
    }

    clearEventListeners() {
        const listeners = this._registeredEventListeners;
        if (!listeners) return;

        for (const [element, listenersByType] of listeners) {
            for (const [type, { listener, options }] of listenersByType) {
                element.removeEventListener(type, listener, options);
            }
        }

        listeners.clear();
        this._registeredEventListeners = null;
    }

    _resolveMethodListener(name) {
        this._resolvedMethodListeners ??= new Map();

        const cached = this._resolvedMethodListeners.get(name);
        if (cached) return cached;

        const method = this[name];
        if (typeof method !== "function") {
            throw new Error(`The JS component has no method '${name}'`);
        }

        const bound = method.bind(this);
        this._resolvedMethodListeners.set(name, bound);
        return bound;
    }

    _dispose() {
        try {
            this.dispose?.();
        } finally {
            this.clearEventListeners();
        }
    }
}
