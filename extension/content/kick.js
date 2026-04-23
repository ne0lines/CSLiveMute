(function registerKickAdapter() {
  window.CsLiveMuteAdapters = window.CsLiveMuteAdapters || [];

  window.CsLiveMuteAdapters.push({
    platform: "Kick",
    siteKind: "stream",
    matches(hostname) {
      return hostname.endsWith("kick.com");
    },
    getMediaElement() {
      return document.querySelector("video");
    },
    isLive() {
      return true;
    },
    getTitle() {
      return (
        document.querySelector("h1")?.textContent?.trim() ||
        document.title.replace(" | Kick", "").trim()
      );
    }
  });
})();
