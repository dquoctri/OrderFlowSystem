import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';

const code = await readFile(new URL('../../src/Web/OrderFlow.Web/wwwroot/js/sse.js', import.meta.url), 'utf8');
const { connect } = await import(`data:text/javascript;base64,${Buffer.from(code).toString('base64')}`);
const flush = () => new Promise(resolve => setImmediate(resolve));

class FakeEventSource {
    static latest;
    listeners = new Map();
    closed = false;
    constructor(url) { this.url = url; FakeEventSource.latest = this; }
    addEventListener(name, callback) { this.listeners.set(name, callback); }
    close() { this.closed = true; }
    emit(name, data) { this.listeners.get(name)({ data }); }
}
globalThis.EventSource = FakeEventSource;

test('delivers saga and terminal in order, closing native reconnect on terminal', async () => {
    const received = [];
    const handle = connect('/stream', { async invokeMethodAsync(_, kind, data) { received.push([kind, data]); } });
    const source = FakeEventSource.latest;
    source.emit('saga', '{"seq":1}');
    source.emit('terminal', '{"status":"Confirmed"}');
    assert.equal(source.closed, true);
    await flush();
    assert.deepEqual(received.map(x => x[0]), ['saga', 'terminal']);
    handle.close();
});

test('three consecutive failures switch to fallback and stop EventSource retries', async () => {
    const received = [];
    connect('/stream', { async invokeMethodAsync(_, kind) { received.push(kind); } });
    const source = FakeEventSource.latest;
    source.onerror(); source.onerror(); source.onerror();
    await flush();
    assert.deepEqual(received, ['stalled', 'stalled', 'fallback']);
    assert.equal(source.closed, true);
});

test('disposal suppresses queued callbacks to a disposed .NET receiver', async () => {
    let calls = 0;
    const handle = connect('/stream', { async invokeMethodAsync() { calls++; } });
    FakeEventSource.latest.emit('saga', '{}');
    handle.close();
    await flush();
    assert.equal(calls, 0);
    assert.equal(FakeEventSource.latest.closed, true);
});
