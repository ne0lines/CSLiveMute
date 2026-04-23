(function bootstrapShared() {
  if (window.CsLiveMuteShared) {
    return;
  }

  function ensureState(video) {
    if (!video.__csLiveMuteState) {
      video.__csLiveMuteState = {
        mute: {
          active: false,
          previousMuted: false,
          userOverrode: false
        },
        pause: {
          active: false,
          previousPaused: true,
          userOverrode: false
        },
        listenersAttached: false
      };
    }

    return video.__csLiveMuteState;
  }

  function attachListeners(video, state) {
    if (state.listenersAttached) {
      return;
    }

    video.addEventListener("play", () => {
      if (state.pause.active) {
        state.pause.userOverrode = true;
      }
    });

    video.addEventListener("volumechange", () => {
      if (state.mute.active && !video.muted) {
        state.mute.userOverrode = true;
      }
    });

    state.listenersAttached = true;
  }

  function releaseInactiveState(video, state, action) {
    if (action === "mute" && state.pause.active) {
      if (!state.pause.userOverrode && state.pause.previousPaused === false) {
        Promise.resolve(video.play()).catch(() => undefined);
      }

      state.pause.active = false;
      state.pause.userOverrode = false;
    }

    if (action === "pause" && state.mute.active) {
      if (!state.mute.userOverrode) {
        video.muted = state.mute.previousMuted;
      }

      state.mute.active = false;
      state.mute.userOverrode = false;
    }
  }

  function buildSnapshot(adapter, video, action) {
    return {
      platform: adapter.platform,
      title: adapter.getTitle(),
      siteKind: adapter.siteKind,
      isLive: adapter.isLive(),
      isPlaying: Boolean(video && !video.paused && !video.ended),
      action
    };
  }

  function createController(adapter) {
    function snapshot() {
      const video = adapter.getMediaElement();
      const action = adapter.isLive() ? "mute" : "pause";
      return buildSnapshot(adapter, video, action);
    }

    function sync(active) {
      const video = adapter.getMediaElement();
      const action = adapter.isLive() ? "mute" : "pause";

      if (!video) {
        return buildSnapshot(adapter, null, action);
      }

      const state = ensureState(video);
      attachListeners(video, state);
      releaseInactiveState(video, state, action);

      if (active) {
        if (action === "mute") {
          if (!state.mute.active) {
            state.mute.previousMuted = video.muted;
            state.mute.userOverrode = false;
          }

          video.muted = true;
          state.mute.active = true;
        } else {
          if (!state.pause.active) {
            state.pause.previousPaused = video.paused;
            state.pause.userOverrode = false;
          }

          if (!video.paused) {
            video.pause();
          }

          state.pause.active = true;
        }
      } else {
        if (state.mute.active && !state.mute.userOverrode) {
          video.muted = state.mute.previousMuted;
        }

        if (state.pause.active && !state.pause.userOverrode && state.pause.previousPaused === false) {
          Promise.resolve(video.play()).catch(() => undefined);
        }

        state.mute.active = false;
        state.mute.userOverrode = false;
        state.pause.active = false;
        state.pause.userOverrode = false;
      }

      return buildSnapshot(adapter, video, action);
    }

    return {
      snapshot,
      sync
    };
  }

  window.CsLiveMuteShared = {
    createController
  };
})();
