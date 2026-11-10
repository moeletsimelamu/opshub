// Automated accessibility check: signs in as each role, opens every screen and the main
// dialogs in headless Chrome, and runs axe-core (all of its rules, which include WCAG 2.2 A
// and AA). Any violation fails the run. Needs a running OpsHub with the fictional demo data.
//
// Usage:  A11Y_PASSWORD='<demo password>' node scripts/a11y-check.mjs <base-url>
//   e.g.  A11Y_PASSWORD="$DEMO_PASSWORD" node scripts/a11y-check.mjs https://localhost:9443
// Optional: CHROME=<path to Chrome>; AXE=<path to axe.min.js> (default: the web app's dev dependency).
//
// It uses Chrome's own DevTools protocol, so it needs no packages beyond axe-core. Certificate
// errors are ignored because CI's throwaway proxy has its own local certificate authority; TLS
// itself is verified separately by the smoke test.

import { spawn } from 'node:child_process';
import { existsSync, mkdtempSync, readFileSync, rmSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';

const base = process.argv[2]?.replace(/\/$/, '');
const password = process.env.A11Y_PASSWORD;
if (!base || !password) {
  console.error('Usage: A11Y_PASSWORD=<demo password> node scripts/a11y-check.mjs <base-url>');
  process.exit(2);
}

const axeSource = readFileSync(process.env.AXE ?? 'src/OpsHub.Web/node_modules/axe-core/axe.min.js', 'utf8');
const chromePath = process.env.CHROME ?? [
  '/usr/bin/google-chrome', '/usr/bin/chromium', '/usr/bin/chromium-browser',
  'C:/Program Files/Google/Chrome/Application/chrome.exe',
].find((p) => existsSync(p));
if (!chromePath) {
  console.error('Chrome not found; set CHROME to its path.');
  process.exit(2);
}

const sleep = (ms) => new Promise((r) => setTimeout(r, ms));
const port = 9300 + Math.floor(Math.random() * 500);
const profile = mkdtempSync(join(tmpdir(), 'opshub-a11y-'));
const chrome = spawn(chromePath, [
  '--headless=new', `--remote-debugging-port=${port}`, `--user-data-dir=${profile}`,
  '--no-first-run', '--no-default-browser-check', '--ignore-certificate-errors',
  // Chrome's sandbox often can't start on CI's Ubuntu machines. There it only visits our own
  // throwaway app, so it runs without one; elsewhere it keeps it.
  ...(process.env.CI === 'true' ? ['--no-sandbox'] : []),
  'about:blank',
], { stdio: 'ignore' });

let ws;
let nextId = 0;
const pending = new Map();
const send = (method, params = {}) => new Promise((resolve, reject) => {
  const id = ++nextId;
  pending.set(id, { resolve, reject });
  ws.send(JSON.stringify({ id, method, params }));
});

async function connect() {
  for (let attempt = 0; attempt < 50; attempt++) {
    try {
      const target = await (await fetch(`http://127.0.0.1:${port}/json/new?about:blank`, { method: 'PUT' })).json();
      ws = new WebSocket(target.webSocketDebuggerUrl);
      await new Promise((r) => (ws.onopen = r));
      ws.onmessage = (event) => {
        const message = JSON.parse(event.data);
        const call = pending.get(message.id);
        if (call) {
          pending.delete(message.id);
          message.error ? call.reject(new Error(message.error.message)) : call.resolve(message.result);
        }
      };
      return;
    } catch {
      await sleep(200);
    }
  }
  throw new Error('Could not connect to Chrome');
}

async function evaluate(expression) {
  const result = await send('Runtime.evaluate', { expression, awaitPromise: true, returnByValue: true });
  if (result.exceptionDetails) throw new Error(result.exceptionDetails.exception?.description ?? 'evaluation failed');
  return result.result.value;
}

async function waitFor(expression, what, timeout = 20000) {
  const start = Date.now();
  while (Date.now() - start < timeout) {
    try { if (await evaluate(expression)) return; } catch { /* page still loading */ }
    await sleep(200);
  }
  throw new Error(`Timed out waiting for ${what}`);
}

// A page is ready when its heading is there and no loading placeholders remain.
const pageReady = `document.readyState === 'complete' && !!document.querySelector('h1') && !document.querySelector('.skeleton')`;

async function go(path) {
  await send('Page.navigate', { url: base + path });
  await waitFor(pageReady, path);
  await sleep(800); // fonts and transitions settle, as a person would see it
}

async function signIn(email) {
  await send('Network.clearBrowserCookies');
  await go('/login');
  await evaluate(`(() => {
    const set = (id, v) => { const el = document.getElementById(id); el.value = v; el.dispatchEvent(new Event('input', { bubbles: true })); };
    set('email', ${JSON.stringify(email)}); set('password', ${JSON.stringify(password)});
    document.querySelector('form').requestSubmit();
  })()`);
  await waitFor(`!location.pathname.startsWith('/login') && !!document.querySelector('.who')`, `sign-in as ${email}`);
  await waitFor(pageReady, 'home page');
}

const clickButton = (label) => evaluate(`(() => {
  const button = [...document.querySelectorAll('main button')].find(b => b.textContent.trim().startsWith(${JSON.stringify(label)}));
  if (!button) throw new Error('No button labelled ${label}');
  button.click();
})()`);

const results = [];
async function scan(name) {
  // Load axe once per page. DevTools runs its source directly: the page's Content-Security-Policy
  // forbids eval(), and it stays on, as it is for real users.
  if (!(await evaluate('!!window.axe'))) {
    const loaded = await send('Runtime.evaluate', { expression: axeSource });
    if (loaded.exceptionDetails) throw new Error('Could not load axe-core into the page');
  }
  // Each node carries axe's own explanation (for contrast: the colours and the ratio it measured).
  const violations = await evaluate(`axe.run(document).then(r => r.violations.map(v => ({
    id: v.id, impact: v.impact, help: v.help,
    nodes: v.nodes.slice(0, 5).map(n => n.target.join(' ') + ([...n.any, ...n.all][0]?.message ? ': ' + [...n.any, ...n.all][0].message : '')),
  })))`);
  results.push({ name, violations });
  console.log(`${violations.length ? 'FAIL' : ' ok '}  ${name}${violations.length ? `: ${violations.length} violation(s)` : ''}`);
}

const viewport = (width, height, mobile = false) =>
  send('Emulation.setDeviceMetricsOverride', { width, height, deviceScaleFactor: 1, mobile });

const firstTicketLink = `document.querySelector('tbody a.row-link')?.getAttribute('href')`;

try {
  await connect();
  await viewport(1280, 900);
  console.log(`Accessibility check of ${base} (axe-core, all rules)`);

  await send('Network.clearBrowserCookies');
  await go('/login');
  await scan('sign-in page');
  await go('/forgot-password');
  await scan('forgot password');
  await go('/reset-password?user=00000000-0000-0000-0000-000000000000&token=example');
  await scan('reset password');

  // Requester
  await signIn('thandi@marula.example');
  await go('/tickets?view=all');
  await scan('requester: my tickets');
  await go(await evaluate(firstTicketLink));
  await scan('requester: ticket');
  await go('/tickets/new');
  await scan('requester: report a problem');
  await go('/account');
  await scan('requester: account');

  // Support agent
  await signIn('sipho@marula.example');
  await go('/dashboard');
  await scan('agent: dashboard');
  await go('/tickets?view=active');
  await scan('agent: queue');
  await go(await evaluate(firstTicketLink));
  await scan('agent: ticket');
  if (await evaluate(`[...document.querySelectorAll('main button')].some(b => b.textContent.trim() === 'Resolve')`)) {
    await clickButton('Resolve');
    await waitFor(`!!document.querySelector('[role=dialog]')`, 'the resolve dialog');
    await sleep(400);
    await scan('agent: resolve dialog');
  }

  // Administrator
  await signIn('lerato@marula.example');
  await go('/admin/users');
  await scan('admin: users');
  await clickButton('Add user');
  await waitFor(`!!document.querySelector('[role=dialog]')`, 'the add-user dialog');
  await sleep(400);
  await scan('admin: add-user dialog');
  await go('/admin/categories');
  await scan('admin: categories');
  await clickButton('Add category');
  await waitFor(`!!document.querySelector('[role=dialog]')`, 'the add-category dialog');
  await sleep(400);
  await scan('admin: add-category dialog');

  // Phone layout: the queue's stacked rows and the navigation drawer.
  await signIn('sipho@marula.example');
  await viewport(375, 812, true);
  await go('/tickets?view=active');
  await scan('phone: queue');
  await evaluate(`document.querySelector('[aria-label="Menu"]').click()`);
  await sleep(500);
  await scan('phone: menu open');
} catch (error) {
  console.error(`Could not finish the check: ${error.message}`);
  if (process.env.GITHUB_ACTIONS === 'true') console.log(`::error title=Accessibility check::Could not finish: ${error.message.replace(/%/g, '%25').replace(/\r?\n/g, '%0A')}`);
  process.exitCode = 2;
} finally {
  ws?.close();
  chrome.kill();
  await sleep(300);
  try { rmSync(profile, { recursive: true, force: true }); } catch { /* Chrome may still hold files */ }
}

// On GitHub Actions each violation also becomes an annotation: annotations show on the run's
// public page, while the job log needs a signed-in user, so a failure can be read by anyone.
const onGitHub = process.env.GITHUB_ACTIONS === 'true';
const escapeData = (s) => s.replace(/%/g, '%25').replace(/\r/g, '%0D').replace(/\n/g, '%0A');
const escapeProperty = (s) => escapeData(s).replace(/:/g, '%3A').replace(/,/g, '%2C');

const failed = results.filter((r) => r.violations.length);
for (const { name, violations } of failed) {
  console.log(`\n${name}`);
  for (const v of violations) {
    console.log(`  ${v.id} (${v.impact}): ${v.help}`);
    for (const node of v.nodes) console.log(`    at ${node}`);
    if (onGitHub) {
      const details = [`${v.id} (${v.impact}): ${v.help}`, ...v.nodes.map((node) => `at ${node}`)].join('\n');
      console.log(`::error title=${escapeProperty(`Accessibility: ${name}`)}::${escapeData(details)}`);
    }
  }
}
if (process.exitCode !== 2) {
  console.log(`\n${results.length} screens checked, ${failed.length} with violations.`);
  process.exitCode = failed.length ? 1 : 0;
}
