(function registerYouTubeAdapter() {
  window.CsLiveMuteAdapters = window.CsLiveMuteAdapters || [];

  window.CsLiveMuteAdapters.push({
    platform: "YouTube",
    siteKind: "video",
    matches(hostname) {
      return hostname.includes("youtube.com");
    },
    getMediaElement() {
      return document.querySelector("video");
    },
    isLive() {
      const liveMeta = document.querySelector('meta[itemprop="isLiveBroadcast"][content="True"], meta[itemprop="isLiveBroadcast"][content="true"]');
      return Boolean(
        liveMeta ||
        document.querySelector(".ytp-live-badge, .ytp-live") ||
        window.location.pathname.startsWith("/live/")
      );
    },
    getTitle() {
      return (
        document.querySelector("h1.ytd-watch-metadata yt-formatted-string")?.textContent?.trim() ||
        document.title.replace(" - YouTube", "").trim()
      );
    }
  });
})();
