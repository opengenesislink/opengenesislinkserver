"use strict";

const el = id => document.getElementById(id);
const state = { me: null, csrf: "", friends: [], groups: [], tab: "friends",
  selected: null, archive: false, available: false, polling: null };

async function api(path, options = {}) {
  let response;
  try {
    response = await fetch(path, {
      credentials: "same-origin", cache: "no-store",
      headers: {
        Accept: "application/json",
        ...(options.body ? { "Content-Type": "application/json" } : {}),
        ...(state.csrf && options.method && options.method !== "GET"
          ? { "X-NexVerse-CSRF": state.csrf } : {})
      },
      ...options
    });
  } catch {
    return { status: 0, data: { error: "network_unavailable" } };
  }
  let data = {};
  try { data = await response.json(); } catch { /* no content */ }
  return { status: response.status, data };
}

function notice(message = "") { el("notice").textContent = message; }
function humanError(reply) {
  if (reply.status === 0) return "Keine Verbindung. Bitte später erneut versuchen.";
  const errors = {
    messenger_not_configured: "Der Messenger wurde serverseitig noch nicht konfiguriert.",
    portal_im_archive_disabled: "Der Nachrichtenverlauf ist auf dem Server nicht aktiviert.",
    confirmed_friendship_required: "Nachrichten sind nur an bestätigte Freunde möglich.",
    hg_route_unavailable: "Nachrichten an Hypergrid-Freunde werden aktuell noch nicht gesendet.",
    delivery_unconfirmed_retry_not_safe: "Zustellung unklar. Bitte nicht erneut senden, bevor die Zustellung geprüft ist.",
    im_send_rate_limited: "Zu viele Nachrichten. Bitte später erneut senden."
  };
  return errors[reply.data?.error] ||
    (reply.status === 401 ? "Deine Sitzung ist abgelaufen. Bitte erneut anmelden." :
    `Der Dienst antwortete mit HTTP ${reply.status}.`);
}

function node(tag, css, text) {
  const item = document.createElement(tag);
  if (css) item.className = css;
  if (text !== undefined) item.textContent = text;
  return item;
}

async function initialize() {
  const result = await api("/api/v1/auth/me");
  if (result.status !== 200) {
    el("identity").textContent = "Nicht angemeldet";
    el("loginPanel").hidden = false;
    el("navigation").hidden = true;
    el("contacts").replaceChildren();
    if (result.status !== 401) notice(humanError(result));
    return;
  }

  state.me = result.data;
  state.csrf = result.data.csrfToken;
  el("identity").textContent =
    `Angemeldet: ${result.data.displayName || result.data.username}`;
  el("loginPanel").hidden = true;
  el("navigation").hidden = false;
  el("signout").hidden = false;
  el("historySetting").hidden = false;
  await loadSettings();
  await loadContacts();
  state.polling = setInterval(async () => {
    if (document.visibilityState === "visible" && state.selected)
      await loadThread(false);
  }, 5000);
}

async function loadSettings() {
  const r = await api("/api/v1/messages/settings");
  state.available = r.status === 200 && !!r.data.available;
  state.archive = state.available && !!r.data.enabled;
  el("archiveToggle").checked = state.archive;
  el("archiveToggle").disabled = !state.available;
  el("historySetting").title = state.available ?
    "Privater Nachrichtenverlauf (Opt-in)" :
    "Archiv muss erst im World-API-Backend aktiviert werden.";
}

async function loadContacts() {
  state.selected = null;
  el("app").classList.remove("chat-open");
  el("messageForm").hidden = true;
  el("conversationHeader").querySelector("h1").textContent = "Stadt NexVerse";
  el("conversationHeader").querySelector("p").textContent =
    "Wähle einen Freund oder eine Gruppe.";
  el("messageList").replaceChildren(node("div", "intro",
    "Wähle einen Kontakt, um den Nachrichtenverlauf zu öffnen."));
  el("showFriends").classList.toggle("active", state.tab === "friends");
  el("showGroups").classList.toggle("active", state.tab === "groups");

  const endpoint = state.tab === "friends" ?
    "/api/v1/friends" : "/api/v1/groups";
  const r = await api(endpoint);
  const items = state.tab === "friends"
    ? (r.data.relationships || [])
    : (r.data.groups || []);
  if (r.status !== 200) {
    notice(humanError(r));
    return;
  }

  if (state.tab === "friends")
    state.friends = items;
  else
    state.groups = items;

  const list = el("contacts");
  list.replaceChildren();
  if (!items.length) {
    list.append(node("p", "muted", state.tab === "friends" ?
      "Noch keine Kontakte vorhanden." : "Keine Gruppen gefunden."));
    return;
  }

  for (const contact of items) {
    const friend = state.tab === "friends";
    const name = friend
      ? (contact.display_name || contact.username || contact.principal_id)
      : (contact.name || contact.group_id);
    const detail = friend
      ? (contact.state !== "friends" ? "Freundschaftsanfrage" :
        contact.is_hypergrid ? "Hypergrid · Präsenz nur lokal bekannt" :
        contact.online_status === "online" ? "Inworld online" :
        contact.online_status === "offline" ? "Offline" : "Status unbekannt")
      : "Gruppe · Mitteilungen";
    const button = node("button", "contact");
    button.type = "button";
    const avatar = node("span", "avatar", String(name || "?").slice(0,1).toUpperCase());
    const body = node("span", "contact-body");
    body.append(node("span", "contact-name", name),
      node("span", "contact-detail", detail));
    button.append(avatar, body);
    button.addEventListener("click", () => openContact(friend ? "friend" : "group", contact));
    list.append(button);
  }
  notice("");
}

async function openContact(kind, contact) {
  state.selected = { kind, contact };
  el("app").classList.add("chat-open");
  const title = kind === "friend"
    ? (contact.display_name || contact.username || contact.principal_id)
    : (contact.name || contact.group_id);
  el("conversationHeader").querySelector("h1").textContent = title;
  el("conversationHeader").querySelector("p").textContent = kind === "group" ?
    "Gruppenmitteilungen · Live-Gruppenchat ist noch nicht freigeschaltet." :
    (contact.is_hypergrid ? "Hypergrid-Kontakt · Senden derzeit nicht möglich." :
      contact.state !== "friends" ? "Freundschaft noch nicht bestätigt." :
      "Direktnachrichten über Stadt NexVerse");
  el("messageForm").hidden = kind !== "friend" ||
    contact.is_hypergrid || contact.state !== "friends";
  notice("");
  await loadThread(true);
}

async function loadThread(scroll) {
  const target = state.selected;
  if (!target) return;
  const path = target.kind === "friend"
    ? `/api/v1/messages/with/${encodeURIComponent(target.contact.principal_id)}`
    : `/api/v1/groups/${encodeURIComponent(target.contact.group_id)}/notices`;
  const result = await api(path);
  if (state.selected !== target) return;
  const list = el("messageList");
  list.replaceChildren();
  if (result.status !== 200) {
    list.append(node("div", "intro", humanError(result)));
    return;
  }
  const entries = target.kind === "friend"
    ? (result.data.messages || [])
    : (result.data.notices || []);
  if (!entries.length) {
    list.append(node("div", "intro",
      target.kind === "friend" ?
      "Hier erscheinen gespeicherte Direktnachrichten. Für den Verlauf muss die Archivierung aktiviert sein." :
      "Noch keine Mitteilungen in dieser Gruppe."));
    return;
  }

  for (const item of entries) {
    const own = target.kind === "friend" &&
      String(item.from_agent_id || "").toLowerCase() ===
      String(state.me.principalId || "").toLowerCase();
    const bubble = node("article", `bubble${own ? " own" : ""}`);
    const who = target.kind === "friend"
      ? (item.sender_name || (own ? "Du" : "Freund"))
      : (item.from_name || item.subject || "Gruppe");
    const time = Number(item.accepted_at || item.timestamp || 0);
    const when = time > 0 ?
      new Date(time * 1000).toLocaleString("de-DE") : "";
    bubble.append(node("span", "bubble-meta", [who, when].filter(Boolean).join(" · ")),
      node("span", "", item.body || item.message || item.subject || ""));
    list.append(bubble);
  }
  if (scroll) list.scrollTop = list.scrollHeight;
}

async function sendMessage(event) {
  event.preventDefault();
  const current = state.selected;
  if (!current || current.kind !== "friend" ||
      current.contact.is_hypergrid || !state.csrf) return;
  const field = el("messageText");
  const body = field.value.trim();
  if (!body) return;
  if (new TextEncoder().encode(body).length > 1024) {
    notice("Nachrichten dürfen höchstens 1024 UTF-8-Bytes lang sein.");
    return;
  }
  el("send").disabled = true;
  const messageId = crypto.randomUUID();
  const response = await api("/api/v1/im", {
    method: "POST",
    body: JSON.stringify({
      clientMessageId: messageId,
      recipient: { agentId: current.contact.principal_id, homeGridUri: null },
      body
    })
  });
  el("send").disabled = false;
  if (response.status === 202) {
    field.value = "";
    notice("Nachricht von der World API angenommen. Endzustellung ist nicht bestätigt.");
    if (state.archive) await loadThread(true);
  } else {
    notice(humanError(response));
    // Deliberately do not automatically resend on timeouts.
  }
}

async function changeArchive(event) {
  const enabled = !!event.target.checked;
  if (!enabled && !confirm("Archiv ausschalten und den gespeicherten Verlauf löschen?")) {
    event.target.checked = true;
    return;
  }
  const r = await api("/api/v1/messages/settings", {
    method: "PATCH", body: JSON.stringify({ enabled })
  });
  if (r.status !== 200) {
    event.target.checked = !enabled;
    notice(humanError(r));
    return;
  }
  state.archive = enabled;
  notice(enabled ? "IM-Archiv aktiviert." : "IM-Archiv deaktiviert und Verlauf gelöscht.");
  if (state.selected) await loadThread(true);
}

el("showFriends").addEventListener("click", async () => {
  state.tab = "friends"; await loadContacts();
});
el("showGroups").addEventListener("click", async () => {
  state.tab = "groups"; await loadContacts();
});
el("back").addEventListener("click", () =>
  el("app").classList.remove("chat-open"));
el("messageForm").addEventListener("submit", sendMessage);
el("archiveToggle").addEventListener("change", changeArchive);
el("signout").addEventListener("click", async () => {
  const r = await api("/api/v1/auth/logout", { method: "POST" });
  if (r.status !== 200) { notice(humanError(r)); return; }
  window.location.replace("/");
});
if ("serviceWorker" in navigator)
  navigator.serviceWorker.register("/sw.js").catch(() => {});
initialize();
