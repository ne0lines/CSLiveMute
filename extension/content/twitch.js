(function registerTwitchAdapter() {
  window.CsLiveMuteAdapters = window.CsLiveMuteAdapters || [];

  window.CsLiveMuteAdapters.push({
    platform: "Twitch",
    siteKind: "stream",
    matches(hostname) {
      return hostname.endsWith("twitch.tv");
    },
    getMediaElement() {
      return document.querySelector("video");
    },
    isLive() {
      return true;
    },
    getTitle() {
      return (
        document.querySelector('[data-a-target="stream-title"]')?.textContent?.trim() ||
        document.title.replace(" - Twitch", "").trim()
      );
    }
  });
})();
