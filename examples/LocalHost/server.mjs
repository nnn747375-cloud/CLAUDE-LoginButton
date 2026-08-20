import { createServer } from "node:http";
import { spawn } from "node:child_process";
import { readFile } from "node:fs/promises";
import { dirname, extname, resolve, sep } from "node:path";
import { fileURLToPath } from "node:url";

const here = dirname(fileURLToPath(import.meta.url));
const docsRoot = resolve(here, "../../docs");
const port = Number(process.env.PORT || 4173);
const model = process.env.ANTHROPIC_MODEL || "claude-sonnet-5";
const antExecutable = process.platform === "win32" ? "ant.cmd" : "ant";

let loginProcess = null;
let lastAuthError = null;

function sendJson(response, status, payload) {
  response.writeHead(status, {
    "content-type": "application/json; charset=utf-8",
    "cache-control": "no-store",
  });
  response.end(JSON.stringify(payload));
}

function runAnt(argumentsList) {
  return new Promise((resolvePromise, rejectPromise) => {
    let child;
    try {
      child = spawn(antExecutable, argumentsList, {
        windowsHide: true,
        stdio: ["ignore", "pipe", "pipe"],
        shell: process.platform === "win32",
      });
    } catch {
      rejectPromise(new Error("The official 'ant' CLI is not installed or not on PATH."));
      return;
    }
    let stdout = "";
    let stderr = "";
    child.stdout.on("data", (chunk) => { stdout += chunk; });
    child.stderr.on("data", (chunk) => { stderr += chunk; });
    child.once("error", () => rejectPromise(new Error("The official 'ant' CLI is not installed or not on PATH.")));
    child.once("close", (code) => {
      if (code === 0) {
        resolvePromise({ stdout: stdout.trim(), stderr: stderr.trim() });
      } else {
        rejectPromise(new Error(`The 'ant' command exited with code ${code ?? "unknown"}.`));
      }
    });
  });
}

async function getCredential() {
  const apiKey = process.env.ANTHROPIC_API_KEY?.trim();
  if (apiKey) return { kind: "api-key", value: apiKey };

  try {
    const result = await runAnt(["auth", "print-credentials", "--access-token"]);
    if (result.stdout) return { kind: "bearer", value: result.stdout };
  } catch {
    // The status endpoint deliberately returns a generic signed-out state.
  }

  return null;
}

function startLogin() {
  if (loginProcess) return;
  lastAuthError = null;
  try {
    loginProcess = spawn(antExecutable, ["auth", "login"], {
      stdio: "inherit",
      windowsHide: false,
      shell: process.platform === "win32",
    });
  } catch {
    lastAuthError = "The official 'ant' CLI is not installed or not on PATH.";
    loginProcess = null;
    return;
  }
  loginProcess.once("error", () => {
    lastAuthError = "The official 'ant' CLI is not installed or not on PATH.";
    loginProcess = null;
  });
  loginProcess.once("close", (code) => {
    if (code !== 0) lastAuthError = "The Claude Console authorization did not complete.";
    loginProcess = null;
  });
}

async function readJson(request) {
  let body = "";
  for await (const chunk of request) {
    body += chunk;
    if (body.length > 1_000_000) throw new Error("Request body is too large.");
  }
  return body ? JSON.parse(body) : {};
}

function normalizeMessages(input, fallback) {
  const source = Array.isArray(input) ? input : [{ role: "user", content: fallback }];
  return source
    .filter((message) => message && (message.role === "user" || message.role === "assistant"))
    .map((message) => ({
      role: message.role,
      content: String(message.content || "").slice(0, 16_000),
    }))
    .filter((message) => message.content.trim())
    .slice(-20);
}

async function handleChat(request, response) {
  const body = await readJson(request);
  const message = String(body.message || "").trim();
  const messages = normalizeMessages(body.messages, message);
  if (!message || messages.length === 0) {
    sendJson(response, 400, { message: "Send a non-empty message." });
    return;
  }

  const credential = await getCredential();
  if (!credential) {
    sendJson(response, 401, { message: "Connect with the Claude button first, or set ANTHROPIC_API_KEY." });
    return;
  }

  const headers = {
    "content-type": "application/json",
    "anthropic-version": "2023-06-01",
  };
  if (credential.kind === "api-key") {
    headers["x-api-key"] = credential.value;
  } else {
    headers.authorization = `Bearer ${credential.value}`;
  }

  const upstream = await fetch("https://api.anthropic.com/v1/messages", {
    method: "POST",
    headers,
    body: JSON.stringify({ model, max_tokens: 1024, messages }),
  });
  const payload = await upstream.json().catch(() => ({}));
  if (!upstream.ok) {
    sendJson(response, upstream.status, {
      message: payload?.error?.message || "Claude returned an error. Check the selected model and account limits.",
    });
    return;
  }

  const text = Array.isArray(payload.content)
    ? payload.content.filter((block) => block.type === "text").map((block) => block.text).join("\n")
    : "";
  sendJson(response, 200, { text: text || "Claude returned no text content." });
}

async function handleAuthStatus(response) {
  if (lastAuthError) {
    sendJson(response, 200, { state: "error", message: lastAuthError });
    return;
  }
  if (loginProcess) {
    sendJson(response, 200, { state: "pending", label: "Waiting for Claude Console" });
    return;
  }
  if (await getCredential()) {
    sendJson(response, 200, { state: "connected", label: "Claude connected" });
    return;
  }
  sendJson(response, 200, { state: "signed-out", label: "Signed out" });
}

function safeDocumentPath(requestUrl) {
  const pathname = decodeURIComponent(new URL(requestUrl, "http://127.0.0.1").pathname);
  const relative = pathname === "/" ? "index.html" : pathname.replace(/^\/+/, "");
  const candidate = resolve(docsRoot, relative);
  const rootWithSeparator = docsRoot.endsWith(sep) ? docsRoot : `${docsRoot}${sep}`;
  if (candidate !== docsRoot && !candidate.toLowerCase().startsWith(rootWithSeparator.toLowerCase())) {
    return null;
  }
  return candidate;
}

function contentType(filePath) {
  return {
    ".html": "text/html; charset=utf-8",
    ".css": "text/css; charset=utf-8",
    ".js": "text/javascript; charset=utf-8",
    ".svg": "image/svg+xml",
    ".jpg": "image/jpeg",
    ".jpeg": "image/jpeg",
  }[extname(filePath).toLowerCase()] || "application/octet-stream";
}

async function serveDocument(request, response) {
  const filePath = safeDocumentPath(request.url);
  if (!filePath) {
    response.writeHead(403);
    response.end("Forbidden");
    return;
  }

  try {
    let content = await readFile(filePath);
    if (filePath.toLowerCase() === resolve(docsRoot, "index.html").toLowerCase()) {
      const injection = `<script>window.CLAUDE_AUTH_URL="/api/auth/start";window.CLAUDE_AUTH_STATUS_URL="/api/auth/status";window.CLAUDE_AUTH_LOGOUT_URL="/api/auth/logout";window.CLAUDE_CHAT_ENDPOINT="/api/chat";</script>`;
      content = Buffer.from(content.toString("utf8").replace("</head>", `${injection}</head>`));
    }
    response.writeHead(200, { "content-type": contentType(filePath), "cache-control": "no-store" });
    response.end(content);
  } catch {
    response.writeHead(404);
    response.end("Not found");
  }
}

const server = createServer(async (request, response) => {
  try {
    if (request.method === "POST" && request.url === "/api/auth/start") {
      if (process.env.ANTHROPIC_API_KEY?.trim()) {
        sendJson(response, 200, { state: "connected" });
      } else {
        try {
          await runAnt(["--version"]);
        } catch {
          sendJson(response, 503, { message: "Install Anthropic's official 'ant' CLI and put it on PATH first." });
          return;
        }
        startLogin();
        sendJson(response, 200, { state: "pending" });
      }
      return;
    }
    if (request.method === "GET" && request.url === "/api/auth/status") {
      await handleAuthStatus(response);
      return;
    }
    if (request.method === "POST" && request.url === "/api/auth/logout") {
      if (!process.env.ANTHROPIC_API_KEY?.trim()) await runAnt(["auth", "logout"]);
      lastAuthError = null;
      sendJson(response, 200, { state: "signed-out" });
      return;
    }
    if (request.method === "POST" && request.url === "/api/chat") {
      await handleChat(request, response);
      return;
    }
    if (request.method === "GET") {
      await serveDocument(request, response);
      return;
    }
    response.writeHead(405);
    response.end("Method not allowed");
  } catch (error) {
    sendJson(response, 500, { message: error instanceof Error ? error.message : "Local host error." });
  }
});

server.listen(port, "127.0.0.1", () => {
  console.log(`CLAUDE-LoginButton live demo: http://127.0.0.1:${port}`);
  console.log(`Model: ${model}`);
  console.log("Click the button in the browser to start the official Claude Console login.");
});
