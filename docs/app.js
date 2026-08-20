(() => {
  "use strict";

  const $ = (selector) => document.querySelector(selector);
  const messages = $("#messages");
  const loginButton = $("#loginButton");
  const loginLabel = $("#loginLabel");
  const previewButton = $("#previewButton");
  const logoutButton = $("#logoutButton");
  const statePill = $("#statePill");
  const stateText = $("#stateText");
  const authFootnote = $("#authFootnote");
  const chatMode = $("#chatMode");
  const chatFootnote = $("#chatFootnote");
  const chatForm = $("#chatForm");
  const chatInput = $("#chatInput");
  const sendButton = $(".send-button");
  const dialog = $("#authDialog");
  const dialogClose = $("#dialogClose");
  const dialogPreview = $("#dialogPreview");

  let authState = "signed-out";
  let connected = false;
  let busy = false;
  let authBusy = false;
  const conversation = [];

  const hostAuthUrl = window.CLAUDE_AUTH_URL || "";
  const hostAuthStatusUrl = window.CLAUDE_AUTH_STATUS_URL || "";
  const hostLogoutUrl = window.CLAUDE_AUTH_LOGOUT_URL || "";
  const hostChatEndpoint = window.CLAUDE_CHAT_ENDPOINT || "";

  function setAuthState(nextState, label) {
    authState = nextState;
    statePill.className = `state-pill ${nextState}`;
    stateText.textContent = label;

    const isConnected = nextState === "connected";
    connected = isConnected;
    loginLabel.textContent = isConnected ? "Claude connected" : nextState === "connecting" ? "Signing in…" : nextState === "error" ? "Try Claude login again" : "Continue with Claude";
    loginButton.disabled = nextState === "connecting";
    previewButton.classList.toggle("hidden", isConnected || Boolean(hostAuthUrl));
    logoutButton.classList.toggle("hidden", !isConnected);
    chatInput.disabled = !isConnected;
    sendButton.disabled = !isConnected;
    chatMode.textContent = isConnected ? (hostChatEndpoint ? "HOST CONNECTED" : "CONNECTED PREVIEW") : "LOCAL PREVIEW";

    if (isConnected) {
      authFootnote.innerHTML = '<span class="footnote-mark">✦</span><span>Connected state is live. Logout returns the control to signed out.</span>';
      chatFootnote.textContent = hostChatEndpoint ? "Host endpoint active. Messages leave this page only through your configured endpoint." : "Connected preview is local until a host chat endpoint is configured.";
      chatInput.placeholder = "Ask Claude something…";
    } else if (nextState === "connecting") {
      authFootnote.innerHTML = '<span class="footnote-mark">◌</span><span>Waiting for the host authorization callback.</span>';
    } else if (nextState === "error") {
      authFootnote.innerHTML = '<span class="footnote-mark">!</span><span>No callback was received. Try again or inspect the host adapter.</span>';
    } else {
      authFootnote.innerHTML = '<span class="footnote-mark">✦</span><span>Demo state is local. A host adapter owns the real callback and session.</span>';
      chatFootnote.textContent = "Connect the button to unlock the mini chat.";
      chatInput.placeholder = "Connect Claude to chat…";
    }
  }

  function openDialog() {
    dialog.classList.remove("hidden");
    dialogClose.focus();
  }

  function closeDialog() {
    dialog.classList.add("hidden");
    loginButton.focus();
  }

  function connectPreview() {
    closeDialog();
    setAuthState("connecting", "Connecting");
    window.setTimeout(() => {
      setAuthState("connected", "Connected");
      appendMessage("assistant", "Connected. The control is ready, and this tiny room is listening.");
      chatInput.focus();
    }, 550);
  }

  async function waitForHostAuth() {
    if (!hostAuthStatusUrl) return;

    const deadline = Date.now() + (5 * 60 * 1000);
    while (Date.now() < deadline) {
      const response = await fetch(hostAuthStatusUrl, { cache: "no-store" });
      const payload = await response.json();
      if (!response.ok || payload.state === "error") {
        throw new Error(payload.message || `Auth status returned ${response.status}`);
      }
      if (payload.state === "connected") {
        setAuthState("connected", payload.label || "Connected");
        appendMessage("assistant", "Connected. The live host is ready for messages.");
        chatInput.focus();
        return;
      }
      await new Promise((resolve) => window.setTimeout(resolve, 1200));
    }

    throw new Error("The browser authorization flow timed out.");
  }

  async function startHostAuth() {
    const response = await fetch(hostAuthUrl, { method: "POST" });
    const payload = await response.json();
    if (!response.ok) throw new Error(payload.message || `Auth start returned ${response.status}`);
    if (payload.redirectUrl) {
      window.location.assign(payload.redirectUrl);
      return;
    }
    await waitForHostAuth();
  }

  async function handleLogin() {
    if (authBusy) return;
    if (authState === "connected") {
      setAuthState("signed-out", "Signed out");
      appendMessage("assistant", "Signed out. The chat is waiting for the next connection.");
      return;
    }

    if (hostAuthUrl) {
      authBusy = true;
      setAuthState("connecting", "Redirecting");
      try {
        await startHostAuth();
      } catch (error) {
        setAuthState("error", "Auth failed");
        appendMessage("assistant", `The host authorization flow failed: ${error.message}`);
      } finally {
        authBusy = false;
      }
      return;
    }

    openDialog();
  }

  function appendMessage(role, text) {
    conversation.push({ role: role === "user" ? "user" : "assistant", content: text });
    const article = document.createElement("article");
    article.className = `message ${role === "user" ? "user-message" : "assistant-message"}`;

    const meta = document.createElement("div");
    meta.className = "message-meta";
    if (role === "user") {
      meta.innerHTML = "<span>You</span>";
    } else {
      meta.innerHTML = '<span class="message-avatar">✦</span><span>Claude</span>';
    }
    const time = document.createElement("time");
    time.textContent = new Intl.DateTimeFormat(undefined, { hour: "2-digit", minute: "2-digit" }).format(new Date());
    meta.appendChild(time);

    const body = document.createElement("p");
    body.textContent = text;
    article.append(meta, body);
    messages.appendChild(article);
    messages.scrollTop = messages.scrollHeight;
  }

  function mockReply(text) {
    const normalized = text.toLowerCase();
    if (normalized.includes("connected") || normalized.includes("button")) {
      return "The button is the welcome surface: focus, loading, connected and error. Authentication stays in the host app.";
    }
    if (normalized.includes("adapter") || normalized.includes("auth")) {
      return "Your adapter starts the provider flow, validates the callback and then calls SetConnected. A click alone is never proof of login.";
    }
    return "This is a local preview reply. Configure CLAUDE_CHAT_ENDPOINT for a server-side Claude Messages API adapter.";
  }

  async function requestReply(text) {
    if (!hostChatEndpoint) {
      await new Promise((resolve) => window.setTimeout(resolve, 450));
      return mockReply(text);
    }

    const response = await fetch(hostChatEndpoint, {
      method: "POST",
      headers: { "content-type": "application/json" },
      body: JSON.stringify({ message: text, messages: conversation.slice(-20) }),
    });
    if (!response.ok) throw new Error(`Chat endpoint returned ${response.status}`);
    const payload = await response.json();
    return payload.text || payload.message || "The host returned an empty response.";
  }

  async function sendMessage(text) {
    const trimmed = text.trim();
    if (!trimmed || busy) return;
    if (!connected) {
      openDialog();
      return;
    }

    busy = true;
    sendButton.disabled = true;
    appendMessage("user", trimmed);
    chatInput.value = "";
    try {
      const reply = await requestReply(trimmed);
      appendMessage("assistant", reply);
    } catch (error) {
      appendMessage("assistant", `The host chat endpoint could not answer: ${error.message}`);
    } finally {
      busy = false;
      sendButton.disabled = false;
      chatInput.focus();
    }
  }

  loginButton.addEventListener("click", () => void handleLogin());
  previewButton.addEventListener("click", connectPreview);
  logoutButton.addEventListener("click", async () => {
    if (hostLogoutUrl) {
      try {
        await fetch(hostLogoutUrl, { method: "POST" });
      } catch (error) {
        appendMessage("assistant", `The host logout flow failed: ${error.message}`);
      }
    }
    setAuthState("signed-out", "Signed out");
    appendMessage("assistant", "Signed out. Your host can now start a fresh authorization attempt.");
  });
  dialogClose.addEventListener("click", closeDialog);
  dialogPreview.addEventListener("click", connectPreview);
  dialog.addEventListener("click", (event) => {
    if (event.target === dialog) closeDialog();
  });
  document.addEventListener("keydown", (event) => {
    if (event.key === "Escape" && !dialog.classList.contains("hidden")) closeDialog();
  });
  chatForm.addEventListener("submit", (event) => {
    event.preventDefault();
    sendMessage(chatInput.value);
  });
  chatInput.addEventListener("keydown", (event) => {
    if (event.key === "Enter" && !event.shiftKey) {
      event.preventDefault();
      sendMessage(chatInput.value);
    }
  });
  document.querySelectorAll("[data-prompt]").forEach((button) => {
    button.addEventListener("click", () => sendMessage(button.dataset.prompt));
  });

  setAuthState("signed-out", "Signed out");
})();
