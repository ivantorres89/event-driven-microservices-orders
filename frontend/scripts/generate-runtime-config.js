/*
  Generates src/assets/runtime-config.json at container startup.
  This makes infra/local/.env the single source of truth for host ports.

  Notes:
  - These URLs are consumed by the browser, so they must reference HOST ports (localhost).
  - The Docker network DNS names (order-notification, etc.) are NOT usable in the browser.
*/

const fs = require('fs');
const path = require('path');

function bool(v, fallback) {
  if (v === undefined || v === null || v === '') return fallback;
  if (typeof v === 'boolean') return v;
  const s = String(v).trim().toLowerCase();
  if (['1', 'true', 'yes', 'y', 'on'].includes(s)) return true;
  if (['0', 'false', 'no', 'n', 'off'].includes(s)) return false;
  return fallback;
}

const orderNotificationHttpsPort = process.env.ORDER_NOTIFICATION_HTTPS_PORT || '5007';
const orderAcceptHttpPort = process.env.ORDER_ACCEPT_HTTP_PORT || '8081';

const signalRBaseUrl = process.env.SIGNALR_BASE_URL || `https://localhost:${orderNotificationHttpsPort}`;
const orderAcceptApiBaseUrl = process.env.ORDER_ACCEPT_API_BASE_URL || `http://localhost:${orderAcceptHttpPort}`;

const defaultUserId = process.env.DEFAULT_USER_ID || 'CUST-0001';
const signalRForceLongPolling = bool(process.env.SIGNALR_FORCE_LONG_POLLING, false);
const useMocks = bool(process.env.USE_MOCKS, false);

const config = {
  signalRBaseUrl,
  orderAcceptApiBaseUrl,
  defaultUserId,
  signalRForceLongPolling,
  useMocks
};

const outDir = path.join(__dirname, '..', 'src', 'assets');
const outFile = path.join(outDir, 'runtime-config.json');

if (!fs.existsSync(outDir)) {
  fs.mkdirSync(outDir, { recursive: true });
}

fs.writeFileSync(outFile, JSON.stringify(config, null, 2), { encoding: 'utf8' });
console.log(`[runtime-config] wrote ${outFile}`);
console.log(`[runtime-config] signalRBaseUrl=${signalRBaseUrl}`);
