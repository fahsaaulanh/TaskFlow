const assert = require('node:assert/strict');
const { createRequire } = require('node:module');
const { after, before, test } = require('node:test');
const { setTimeout: delay } = require('node:timers/promises');
const { HubConnectionBuilder, HttpTransportType, LogLevel } = require('../frontend/node_modules/@microsoft/signalr');
const WebSocket = createRequire(require.resolve('../frontend/node_modules/@microsoft/signalr'))('ws');
const host = require('./integration-host.cjs');

let origin;
let password;
before(async () => { ({ origin, password } = await host.start()); });
after(() => host.stop());

function session() {
  const cookies = new Map();
  const cookieHeader = () => [...cookies].map(([key, value]) => `${key}=${value}`).join('; ');

  async function send(url, { method = 'GET', body, csrf = false } = {}) {
    const headers = { Cookie: cookieHeader() };
    if (csrf) headers['X-XSRF-TOKEN'] = cookies.get('XSRF-TOKEN');
    if (body) headers['Content-Type'] = 'application/json';
    const response = await fetch(origin + url, { method, headers, body: body ? JSON.stringify(body) : undefined });
    for (const cookie of response.headers.getSetCookie()) {
      const pair = cookie.split(';')[0];
      const separator = pair.indexOf('=');
      cookies.set(pair.slice(0, separator), pair.slice(separator + 1));
    }
    return response;
  }

  async function login(email, value = password) {
    await check(send('/api/auth/csrf'), 204);
    const response = await send('/api/auth/login', { method: 'POST', body: { email, password: value }, csrf: true });
    if (response.ok) await check(send('/api/auth/csrf'), 204);
    return response;
  }

  return { send, login, cookieHeader };
}

async function check(request, status) {
  const response = await request;
  assert.equal(response.status, status, `${response.url} returned ${response.status}`);
  return response.status === 204 || !response.headers.get('content-type')?.includes('json') ? null : response.json();
}

const write = (client, url, body, expected, method = 'POST') => check(client.send(url, { method, body, csrf: true }), expected);

test('anonymous and invalid credentials are denied, and logout ends a session', async () => {
  const client = session();
  await check(client.send('/api/boards'), 401);
  const wrong = await check(client.login('alex@taskflow.test', 'invalid-test-password'), 401);
  const unknown = await check(client.login('unknown@taskflow.test', 'invalid-test-password'), 401);
  assert.equal(wrong.title, unknown.title);
  const user = await check(client.login('alex@taskflow.test'), 200);
  assert.equal(user.displayName, 'Alex');
  await check(client.send('/api/auth/session'), 200);
  await write(client, '/api/auth/logout', undefined, 204);
  await check(client.send('/api/auth/session'), 401);
});

test('board writes, SignalR, analytics, and activity agree with SQL Server', async () => {
  const alex = session();
  const dina = session();
  await Promise.all([check(alex.login('alex@taskflow.test'), 200), check(dina.login('dina@taskflow.test'), 200)]);
  const [board] = await check(alex.send('/api/boards'), 200);
  const url = `/api/boards/${board.id}`;
  const tasks = `${url}/tasks`;
  const read = () => check(dina.send(tasks), 200);
  await check(alex.send('/api/boards/2147483647/tasks'), 403);
  await check(alex.send(tasks, { method: 'POST', body: { title: 'Without a token', priority: 'Medium' } }), 400);
  const bad = await write(alex, tasks, { title: 'x', priority: 'Urgent', assigneeId: 2147483647 }, 400);
  assert.deepEqual(Object.keys(bad.errors).sort(), ['assigneeId', 'priority', 'title']);
  const beforeCounts = await check(alex.send(`${url}/analytics`), 200);
  assert.deepEqual(beforeCounts, { total: 3, todo: 1, inProgress: 1, done: 1, overdue: 1 });

  await check(fetch(`${origin}/hubs/board/negotiate?negotiateVersion=1`, { method: 'POST' }), 401);
  let socket;
  class TrackedSocket extends WebSocket {
    constructor(...args) {
      super(...args);
      socket = this;
    }
  }
  const hub = new HubConnectionBuilder().withUrl(`${origin}/hubs/board`, {
    transport: HttpTransportType.WebSockets,
    headers: { Cookie: dina.cookieHeader(), Origin: origin },
    WebSocket: TrackedSocket,
  }).withAutomaticReconnect([2000, 4000]).configureLogging(LogLevel.None).build();

  let eventCount = 0;
  hub.on('BoardChanged', () => { eventCount++; });
  try {
    await hub.start();
    await assert.rejects(hub.invoke('WatchBoard', 2147483647), /do not have access/);
    await hub.invoke('WatchBoard', board.id);
    const createEvent = nextChange(hub, read);
    const created = await write(alex, tasks, { title: 'Integration test task', priority: 'Medium' }, 201);
    assert((await within(createEvent)).some((task) => task.id === created.id));

    const taskUrl = `${tasks}/${created.id}`;
    const edited = await write(alex, taskUrl, { title: 'Updated integration task', priority: 'High', rowVersion: created.rowVersion }, 200, 'PUT');
    assert.notEqual(edited.rowVersion, created.rowVersion);
    const moved = await write(alex, `${taskUrl}/move`, { status: 'InProgress', rowVersion: edited.rowVersion }, 200);
    await write(alex, `${taskUrl}/move`, { status: 'Done', rowVersion: moved.rowVersion }, 200);
    await waitForCount(() => eventCount, 4);
    await write(alex, taskUrl, { title: 'Stale integration edit', priority: 'Low', rowVersion: created.rowVersion }, 409, 'PUT');
    await delay(200);
    assert.equal(eventCount, 4, 'a stale edit must not broadcast');

    const [done] = (await read()).filter((task) => task.id === created.id);
    assert.equal(done.status, 'Done');
    const filtered = await check(alex.send(`${tasks}?status=Done&priority=High`), 200);
    assert.equal(filtered.find((task) => task.id === done.id).rowVersion, done.rowVersion);

    const reconnecting = new Promise((resolve) => hub.onreconnecting(resolve));
    const rejoined = new Promise((resolve, reject) => {
      hub.onreconnected(() => hub.invoke('WatchBoard', board.id).then(read).then(resolve, reject));
    });
    socket.terminate();
    await within(reconnecting);
    const missed = await write(alex, taskUrl, { title: 'Edited during disconnect', priority: 'High', rowVersion: done.rowVersion }, 200, 'PUT');
    assert((await within(rejoined)).some((task) => task.id === done.id && task.rowVersion === missed.rowVersion));

    const archiveEvent = nextChange(hub, read);
    await write(alex, `${taskUrl}/archive`, { rowVersion: missed.rowVersion }, 204);
    assert(!(await within(archiveEvent)).some((task) => task.id === done.id));
    assert.deepEqual(await check(alex.send(`${url}/analytics`), 200), beforeCounts);

    const today = new Date().toISOString().slice(0, 10);
    const nextDay = new Date(Date.now() + 86400000).toISOString().slice(0, 10);
    const newest = await check(alex.send(`${url}/activity?from=${today}&to=${today}&pageSize=1`), 200);
    const oldest = await check(alex.send(`${url}/activity?from=${today}&to=${today}&pageSize=1&page=2`), 200);
    const future = await check(alex.send(`${url}/activity?from=${nextDay}`), 200);
    assert.equal(newest.hasMore, true);
    assert.equal(oldest.hasMore, false);
    assert.deepEqual(future.items, []);
    assert.deepEqual([newest.items[0].taskId, newest.items[0].actorName, newest.items[0].previousStatus, newest.items[0].newStatus],
      [created.id, 'Alex', 'InProgress', 'Done']);
    assert.equal(oldest.items[0].previousStatus, 'Todo');
  } finally {
    await hub.stop();
  }
});

function nextChange(hub, read) {
  return new Promise((resolve, reject) => {
    const receive = () => {
      hub.off('BoardChanged', receive);
      read().then(resolve, reject);
    };
    hub.on('BoardChanged', receive);
  });
}

async function within(promise) {
  const timer = new AbortController();
  try {
    return await Promise.race([promise, delay(8000, undefined, { signal: timer.signal }).then(() => {
      throw new Error('SignalR notification did not arrive.');
    })]);
  } finally { timer.abort(); }
}

async function waitForCount(read, expected) {
  const deadline = Date.now() + 8000;
  while (read() < expected && Date.now() < deadline) await delay(30);
  assert.equal(read(), expected);
}
