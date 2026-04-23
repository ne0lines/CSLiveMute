(function bootstrapContent() {
  const adapters = window.CsLiveMuteAdapters || [];
  const adapter = adapters.find((candidate) => candidate.matches(window.location.hostname));
  if (!adapter || !window.CsLiveMuteShared) {
    return;
  }

  const controller = window.CsLiveMuteShared.createController(adapter);

  async function emitSnapshot() {
    try {
      await chrome.runtime.sendMessage({
        type: "mediaSnapshot",
        snapshot: controller.snapshot()
      });
    } catch {
    }
  }

  chrome.runtime.onMessage.addListener((message, _sender, sendResponse) => {
    if (message?.type === "cs-live-mute/sync") {
      const snapshot = controller.sync(Boolean(message.active));
      void emitSnapshot();
      sendResponse(snapshot);
      return false;
    }

    if (message?.type === "cs-live-mute/snapshot") {
      sendResponse(controller.snapshot());
      return false;
    }

    return false;
  });

  window.addEventListener("load", () => {
    void emitSnapshot();
    window.setTimeout(() => void emitSnapshot(), 1000);
  });

  document.addEventListener("visibilitychange", () => void emitSnapshot());
  window.setInterval(() => void emitSnapshot(), 15000);
  void emitSnapshot();
})();
