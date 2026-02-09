import { Component } from './component.mjs';

let nextJSComponentId = 1;

const componentsById = new Map();
const componentIdsByKey = new Map();
const componentTypesBySrc = new Map();

const jsElementReferenceScopeKey = "__jsScope";

async function importJSComponent(src) {
    if (src.startsWith("./")) {
        src = new URL(src.slice(2), document.baseURI).toString();
    }

    const mod = await import(src);
    const TComponent = mod?.default;

    return TComponent?.prototype instanceof Component ? TComponent : null;
}

export function beforeStart(options) {
    if (!options?.jsComponents?.disableGlobalProperties) {
        globalThis.BlazorJSComponents = { Component };
    }
}

export function afterStarted(blazor) {

    async function getOrCreateJSComponent(instanceId, src, key) {
        instanceId ??= componentIdsByKey.get(key);

        const existing = componentsById.get(instanceId);
        if (existing) {
            const { key: oldKey, src: oldSrc } = existing;

            if (key && oldKey === key && oldSrc === src) {
                existing.pendingDisposal = false;
                return instanceId;
            }

            disposeJSComponent(instanceId);
        }

        let TComponent = componentTypesBySrc.get(src);

        if (TComponent === undefined) {
            TComponent = await importJSComponent(src);
            componentTypesBySrc.set(src, TComponent);
        }

        if (!TComponent) {
            return 0;
        }

        const instance = new TComponent();
        const newId = nextJSComponentId++;

        if (key != null) {
            componentIdsByKey.set(key, newId);
        }

        componentsById.set(newId, {
            key,
            src,
            TComponent,
            instance
        });

        instance.attach?.(blazor);

        return newId;
    }

    function disposeJSComponent(instanceId) {
        const entry = componentsById.get(instanceId);
        if (!entry) return;

        entry.instance?._dispose?.();

        componentsById.delete(instanceId);

        if (entry.key != null) {
            componentIdsByKey.delete(entry.key);
        }
    }

    function getJSComponentInstance(instanceId) {
        const entry = componentsById.get(instanceId);
        if (!entry) {
            throw new Error(`Could not find JS component with ID ${instanceId}`);
        }
        return entry.instance;
    }

    function setJSComponentParameters(instanceId, args) {
        getJSComponentInstance(instanceId)
            .setParameters?.(...(args ?? []));
    }

    function invokeJSComponentMethod(instanceId, identifier, args) {
        const instance = getJSComponentInstance(instanceId);
        const method = instance?.[identifier];

        if (typeof method !== "function") {
            throw new Error(
                `The JS component does not define method '${identifier}'`
            );
        }

        return method.apply(instance, args ?? []);
    }

    function reviveJSComponentArgs(_key, value) {
        if (
            value &&
            typeof value === "object" &&
            jsElementReferenceScopeKey in value
        ) {
            const collectionId = value[jsElementReferenceScopeKey];
            const elements = document.querySelectorAll(
                `[data-ref|="${collectionId}"]`
            );

            const result = {};
            for (const element of elements) {
                const attr = element.getAttribute("data-ref");
                const idx = attr.indexOf("-") + 1;
                const refId = attr.substring(idx);
                result[refId] = element;
            }

            return result;
        }

        return value;
    }

    globalThis.__blazorScript = {
        getOrCreateJSComponent,
        setJSComponentParameters,
        invokeJSComponentMethod,
        disposeJSComponent,
    };

    globalThis.DotNet.attachReviver(reviveJSComponentArgs);

    let isNavigating = false;

    blazor?.addEventListener?.("enhancednavigationstart", () => {
        isNavigating = true;
    });

    blazor?.addEventListener?.("enhancednavigationend", () => {
        isNavigating = false;
    });

    class BlScriptElement extends HTMLElement {
        static observedAttributes = ["inst"];

        async attributeChangedCallback(name, _oldValue, newValue) {
            if (name !== "inst") return;

            const src = this.getAttribute("src");
            const key = this.getAttribute("key");

            if (!src) {
                throw new Error("Expected the 'src' attribute to be defined.");
            }

            this._instanceId = await getOrCreateJSComponent(
                this._instanceId,
                src,
                key
            );

            if (!this._instanceId) return;

            const argsEl = document.getElementById(`bl-args-${newValue}`);

            const args = argsEl
                ? JSON.parse(argsEl.textContent, reviveJSComponentArgs)
                : [];

            if (argsEl) argsEl.textContent = "";

            setJSComponentParameters(this._instanceId, args);
        }

        disconnectedCallback() {
            if (!this._instanceId) return;

            const key = this.getAttribute("key");
            const mayBeInteractive = this.hasAttribute("int");

            if (!isNavigating && key && mayBeInteractive) {
                const entry = componentsById.get(this._instanceId);
                if (!entry) return;

                entry.pendingDisposal = true;

                setTimeout(() => {
                    const latest = componentsById.get(this._instanceId);
                    if (latest?.pendingDisposal) {
                        disposeJSComponent(this._instanceId);
                    }
                }, 3000);
            } else {
                disposeJSComponent(this._instanceId);
            }
        }
    }

    customElements.define("bl-script", BlScriptElement);
}

export function beforeWebStart(options) {
    beforeStart(options);
}

export function afterWebStarted(blazor) {
    afterStarted(blazor);
}
