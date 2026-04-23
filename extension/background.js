const APP_SOCKET_URL = "ws://127.0.0.1:3000/bridge";
const MESSAGE_TYPES = {
  HELLO: "hello",
  STATUS: "status",
  COMBAT_MODE_CHANGED: "combat_mode_changed",
  MEDIA_SNAPSHOT: "media_snapshot",
  HEARTBEAT: "heartbeat"
};

let socket = null;
let reconnectTimer = null;
let heartbeatTimer = null;
let syncChain = Promise.resolve();
let combatModeActive = false;
const tabSnapshots = new Map();

function getBrowserName() {
  const userAgent = navigator.userAgent;
  if (userAgent.includes("Edg/")) {
    return "Edge";
  }

  if (userAgent.includes("Brave/")) {
    return "Brave";
  }

  return "Chrome";
}

function isSupportedUrl(url = "") {
  return /https?:\/\/(?:www\.)?(youtube\.com|twitch\.tv|kick\.com)\//i.test(url);
}

function sendToDesktop(payload) {
  if (!socket || socket.readyState !== WebSocket.OPEN) {
    return;
  }

  socket.send(JSON.stringify(payload));
}

async function querySupportedTabs() {
  const tabs = await chrome.tabs.query({});
  return tabs.filter((tab) => tab.id && isSupportedUrl(tab.url));
}

function queueSync(reason) {
  syncChain = syncChain
    .catch(() => undefined)
    .then(() => syncSupportedTabs(reason));

  return syncChain;
}

async function syncSupportedTabs(reason) {
  const tabs = await querySupportedTabs();
  let connectedTabs = 0;

  for (const tab of tabs) {
    const snapshot = await sendTabMessage(tab.id, {
      type: "cs-live-mute/sync",
      active: combatModeActive,
      reason
    });

    if (snapshot) {
      connectedTabs += 1;
      rememberSnapshot(tab.id, snapshot);
    }
  }

  sendStatus(tabs.length, connectedTabs);
}

function rememberSnapshot(tabId, snapshot) {
  if (!tabId) {
    return;
  }

  const nextSnapshot = {
    ...snapshot,
    tabId
  };

  tabSnapshots.set(tabId, nextSnapshot);
  sendToDesktop({
    type: MESSAGE_TYPES.MEDIA_SNAPSHOT,
    ...nextSnapshot
  });
}

function sendStatus(supportedTabs = null, connectedTabs = null) {
  const totalSupportedTabs = supportedTabs ?? Array.from(tabSnapshots.keys()).length;
  const totalConnectedTabs = connectedTabs ?? Array.from(tabSnapshots.keys()).length;

  sendToDesktop({
    type: MESSAGE_TYPES.STATUS,
    browser: getBrowserName(),
    version: chrome.runtime.getManifest().version,
    connectedTabs: totalConnectedTabs,
    supportedTabs: totalSupportedTabs,
    combatModeActive
  });
}

function scheduleReconnect() {
  clearTimeout(reconnectTimer);
  reconnectTimer = setTimeout(connectSocket, 2000);
}

function startHeartbeat() {
  clearInterval(heartbeatTimer);
  heartbeatTimer = setInterval(() => {
    sendToDesktop({
      type: MESSAGE_TYPES.HEARTBEAT,
      source: "extension",
      sentAt: new Date().toISOString()
    });
  }, 10000);
}

function connectSocket() {
  if (socket && (socket.readyState === WebSocket.OPEN || socket.readyState === WebSocket.CONNECTING)) {
    return;
  }

  socket = new WebSocket(APP_SOCKET_URL);

  socket.addEventListener("open", () => {
    sendToDesktop({
      type: MESSAGE_TYPES.HELLO,
      role: "extension",
      browser: getBrowserName(),
      version: chrome.runtime.getManifest().version
    });

    startHeartbeat();
    sendStatus();
    void queueSync("socket-open");
  });

  socket.addEventListener("message", (event) => {
    try {
      const message = JSON.parse(event.data);
      if (message.type === MESSAGE_TYPES.COMBAT_MODE_CHANGED) {
        combatModeActive = Boolean(message.active);
        void queueSync("combat-mode-changed");
      }
    } catch (error) {
      console.warn("CS Live Mute bridge could not parse desktop message", error);
    }
  });

  socket.addEventListener("close", () => {
    clearInterval(heartbeatTimer);
    scheduleReconnect();
  });

  socket.addEventListener("error", () => {
    clearInterval(heartbeatTimer);
    if (socket) {
      socket.close();
    }
  });
}

async function sendTabMessage(tabId, message) {
  try {
    return await chrome.tabs.sendMessage(tabId, message);
  } catch {
    return null;
  }
}

chrome.runtime.onMessage.addListener((message, sender, sendResponse) => {
  if (message?.type === "mediaSnapshot") {
    const tabId = sender.tab?.id;
    if (tabId) {
      rememberSnapshot(tabId, message.snapshot);
      sendStatus();
    }

    sendResponse({ ok: true });
    return false;
  }

  if (message?.type === "requestSync") {
    queueSync("content-request")
      .then(() => sendResponse({ ok: true }))
      .catch((error) => sendResponse({ ok: false, error: String(error) }));
    return true;
  }

  return false;
});

chrome.tabs.onUpdated.addListener((tabId, changeInfo, tab) => {
  if (!tab.id) {
    return;
  }

  if (changeInfo.status === "complete" && isSupportedUrl(tab.url)) {
    void queueSync("tab-updated");
    return;
  }

  if (tab.url && !isSupportedUrl(tab.url)) {
    tabSnapshots.delete(tabId);
    sendStatus();
  }
});

chrome.tabs.onRemoved.addListener((tabId) => {
  tabSnapshots.delete(tabId);
  sendStatus();
});

chrome.runtime.onStartup.addListener(() => connectSocket());
chrome.runtime.onInstalled.addListener(() => connectSocket());

connectSocket();
