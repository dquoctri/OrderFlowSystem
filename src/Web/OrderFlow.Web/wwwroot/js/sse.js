export function connect(url, receiver) {
    const source = new EventSource(url);
    let closed = false;
    let errors = 0;
    let pending = Promise.resolve();
    const send = (kind, data) => {
        pending = pending.then(() => !closed && receiver.invokeMethodAsync("Receive", kind, data)).catch(() => {});
    };
    for (const kind of ["saga", "terminal", "stalled"]) {
        source.addEventListener(kind, event => {
            errors = 0;
            if (kind === "terminal") source.close();
            send(kind, event.data);
        });
    }
    source.onerror = () => {
        if (++errors >= 3) {
            source.close();
            send("fallback", "{}");
        } else send("stalled", '{"reason":"Connection interrupted; reconnecting."}');
    };
    return { close() { closed = true; source.close(); } };
}
