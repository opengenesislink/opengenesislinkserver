// SPDX-License-Identifier: MPL-2.0
// Small self-hosted, same-origin interactive web profile UI.
// All authorization and state mutation remain with the World API.
using System;
using System.Net;
using System.Text.Json;

namespace NexVerse.Server.Api
{
    internal static class OglProfileFeedWebApp
    {
        internal static string Controls()
        {
            return """
                <nav class="ogl-controls" aria-label="Feed-Navigation">
                  <button id="ogl-tab-profile" type="button" aria-pressed="true">Profil</button>
                  <button id="ogl-tab-following" type="button" aria-pressed="false">Abonniert</button>
                  <span class="ogl-spacer"></span>
                  <button id="ogl-login" type="button">Mit Avatar anmelden</button>
                  <button id="ogl-logout" type="button" hidden>Abmelden</button>
                </nav>
                <p id="ogl-auth-status" class="sub" role="status"></p>
                <div id="ogl-compose" class="ogl-compose" hidden>
                  <label for="ogl-post-text">Was gibt es Neues?</label>
                  <textarea id="ogl-post-text" maxlength="1500" rows="3" placeholder="Teile einen Moment aus Stadt NexVerse..."></textarea>
                  <div class="ogl-compose-bottom"><span id="ogl-counter" class="sub">0/1500</span>
                  <button id="ogl-publish" type="button">Veroeffentlichen</button></div>
                </div>
                <div id="ogl-follow-controls" hidden>
                  <button id="ogl-follow" type="button">Folgen</button>
                </div>
                <p id="ogl-feedback" role="status" class="sub"></p>
                """;
        }

        internal static string Styles()
        {
            return """
                .ogl-controls{display:flex;flex-wrap:wrap;align-items:center;gap:7px;padding:10px 0}
                .ogl-spacer{flex:1}button{border:1px solid #42647d;background:#1b354d;
                color:#e6f3ff;border-radius:8px;padding:7px 12px;cursor:pointer;font:inherit}
                button:hover{background:#2d4f69}button:disabled{opacity:.5;cursor:default}
                button[aria-pressed=true]{background:#245b79;border-color:#6ab4d3}
                [hidden]{display:none!important}.ogl-compose{background:#10273b;
                border:1px solid #304c64;border-radius:12px;padding:12px;margin:8px 0}
                .ogl-compose label{display:block;margin-bottom:8px;font-weight:650}
                textarea{display:block;width:100%;resize:vertical;min-height:66px;
                background:#0a1c2d;color:#eff7ff;border:1px solid #496880;
                border-radius:8px;padding:10px;font:inherit}
                textarea:focus,button:focus-visible{outline:2px solid #80cdec;outline-offset:2px}
                .ogl-compose-bottom{display:flex;justify-content:space-between;
                align-items:center;margin-top:8px}.ogl-actions{display:flex;gap:6px;
                flex-wrap:wrap;margin:11px 0 0}.ogl-comment-list{margin-top:9px}
                .ogl-comment-compose{display:flex;gap:7px;margin-top:8px}
                .ogl-comment-compose textarea{min-height:46px}
                .ogl-muted{color:#98b7cd}.ogl-user-link{color:#90d8f4}
                .ogl-error{color:#ffbdac}.ogl-empty{padding:24px 6px;text-align:center;
                color:#acc6d8}.ogl-loading{opacity:.7}#ogl-follow-controls{margin:10px 0}
                """;
        }

        internal static string Script(string nonce, string ownerId,
            string ownerName, bool published, string clientId, string redirectUri)
        {
            string json=JsonSerializer.Serialize(new {
                ownerId=ownerId ?? "", ownerName=ownerName ?? "",
                published, clientId=clientId ?? "", redirectUri=redirectUri ?? ""
            });
            // System.Text.Json's default encoder escapes '<' and other
            // HTML-significant characters; the JSON element is not executable.
            return "<script type=\"application/json\" id=\"ogl-cfg\">" + json +
                "</script><script nonce=\"" + WebUtility.HtmlEncode(nonce) + "\">" +
                ScriptBody + "</script>";
        }

        internal static string CallbackPage(string nonce, string clientId,
            string redirectUri)
        {
            return "<!doctype html><html lang=\"de\"><head><meta charset=\"utf-8\">" +
                "<meta name=\"viewport\" content=\"width=device-width,initial-scale=1\">" +
                "<title>Stadt NexVerse - Avatar-Anmeldung</title><style>" + Styles() +
                "body{font:14px system-ui;padding:24px;background:#0b1420;color:#e6f3ff}" +
                "</style></head><body><h1>Stadt NexVerse</h1>" +
                "<p id=\"ogl-feedback\" role=\"status\">Anmeldung wird abgeschlossen...</p>" +
                Script(nonce, "", "", false, clientId, redirectUri) +
                "</body></html>";
        }

        private const string ScriptBody = """
            (() => {
              'use strict';
              const config = JSON.parse(document.getElementById('ogl-cfg').textContent);
              const $ = (id) => document.getElementById(id);
              const params = new URL(location.href).searchParams;
              const tokenKey = 'oglFeedAccess';
              const expiresKey = 'oglFeedExpires';
              const pendingKey = 'oglFeedPending';
              const scope = 'profile:read profile:write relationships:read relationships:write';
              const base = '/api/v1/feed/';
              let me = null, tab = 'profile', following = false;
              let liked = new Set(), loadedPosts = [];
              function feedback(message, problem = false) {
                const target = $('ogl-feedback');
                if (!target) return;
                target.textContent = message || '';
                target.classList.toggle('ogl-error', !!problem);
              }
              function authStatus(message) {
                if ($('ogl-auth-status')) $('ogl-auth-status').textContent = message;
              }
              function access() {
                const expires = Number(sessionStorage.getItem(expiresKey) || 0);
                if (expires <= Date.now()) {
                  sessionStorage.removeItem(tokenKey);
                  sessionStorage.removeItem(expiresKey);
                  return '';
                }
                return sessionStorage.getItem(tokenKey) || '';
              }
              function node(tag, className, text, parent) {
                const element = document.createElement(tag);
                if (className) element.className = className;
                if (text !== undefined) element.textContent = String(text);
                if (parent) parent.appendChild(element);
                return element;
              }
              function action(label, parent, handler) {
                const element = node('button', '', label, parent);
                element.type = 'button';
                element.addEventListener('click', () => {
                  Promise.resolve(handler()).catch(e => feedback(e.message, true));
                });
                return element;
              }
              function formatTime(timestamp) {
                const d = new Date(Number(timestamp) * 1000);
                return Number.isFinite(d.getTime()) ? d.toLocaleString('de-DE') : '';
              }
              async function api(path, method = 'GET', body) {
                const headers = { Accept: 'application/json' };
                const token = access();
                if (token) headers.Authorization = 'Bearer ' + token;
                if (body !== undefined) headers['Content-Type'] = 'application/json';
                const response = await fetch(path, {
                  method, headers, credentials: 'omit', cache: 'no-store',
                  body: body === undefined ? undefined : JSON.stringify(body)
                });
                const data = await response.json().catch(() => ({}));
                if (!response.ok) {
                  if (response.status === 401) {
                    sessionStorage.removeItem(tokenKey);
                    sessionStorage.removeItem(expiresKey);
                  }
                  throw Error(data.error || ('HTTP ' + response.status));
                }
                return data;
              }
              function b64url(bytes) {
                return btoa(String.fromCharCode(...bytes))
                  .replaceAll('+', '-').replaceAll('/', '_').replace(/=+$/g, '');
              }
              async function startLogin() {
                if (!config.clientId || !config.redirectUri) {
                  throw Error('Die OAuth-Anmeldung ist auf ROBUST noch nicht konfiguriert.');
                }
                const redirect = new URL(config.redirectUri);
                if (redirect.origin !== location.origin || redirect.pathname !== '/feed' ||
                    redirect.search || redirect.hash || location.protocol !== 'https:') {
                  throw Error('Der OAuth-Ruecksprung muss dieselbe HTTPS-Domain /feed verwenden.');
                }
                if (!crypto || !crypto.subtle) {
                  throw Error('Dieser Viewer-Browser unterstuetzt kein sicheres PKCE.');
                }
                const verifier = b64url(crypto.getRandomValues(new Uint8Array(32)));
                const state = b64url(crypto.getRandomValues(new Uint8Array(24)));
                const digest = await crypto.subtle.digest('SHA-256',
                  new TextEncoder().encode(verifier));
                const challenge = b64url(new Uint8Array(digest));
                sessionStorage.setItem(pendingKey, JSON.stringify({
                  verifier, state, target: config.ownerName, at: Date.now()
                }));
                const url = new URL('/oauth/authorize', location.origin);
                for (const [k, v] of Object.entries({
                  response_type: 'code', client_id: config.clientId,
                  redirect_uri: redirect.href, code_challenge: challenge,
                  code_challenge_method: 'S256', scope, state
                })) url.searchParams.set(k, v);
                location.assign(url.href);
              }
              async function completeLogin() {
                const code = params.get('code');
                const oauthError = params.get('error');
                if (!code && !oauthError) return false;
                // Remove one-time code from browser history before any
                // network request or further UI navigation.
                history.replaceState(null, '', '/feed');
                const raw = sessionStorage.getItem(pendingKey);
                sessionStorage.removeItem(pendingKey);
                if (!raw) throw Error('Anmeldung abgelaufen: bitte erneut starten.');
                const pending = JSON.parse(raw);
                if (Date.now() - pending.at > 10 * 60 * 1000 ||
                    params.get('state') !== pending.state) {
                  throw Error('OAuth-Sicherheitspruefung fehlgeschlagen (state).');
                }
                if (oauthError) throw Error('Anmeldung abgelehnt: ' + oauthError);
                const form = new URLSearchParams({
                  grant_type: 'authorization_code',
                  client_id: config.clientId,
                  redirect_uri: config.redirectUri,
                  code, code_verifier: pending.verifier
                });
                const response = await fetch('/oauth/token', {
                  method: 'POST', credentials: 'omit', cache: 'no-store',
                  headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
                  body: form
                });
                const data = await response.json();
                if (!response.ok || !data.access_token)
                  throw Error(data.error || 'Token-Austausch fehlgeschlagen.');
                sessionStorage.setItem(tokenKey, data.access_token);
                sessionStorage.setItem(expiresKey,
                  String(Date.now() + Math.max(0, Number(data.expires_in || 0) - 30) * 1000));
                const target = pending.target || '';
                location.replace(target ? '/feed?name=' + encodeURIComponent(target) : '/feed');
                return true;
              }
              async function logout() {
                const token = access();
                sessionStorage.removeItem(tokenKey);
                sessionStorage.removeItem(expiresKey);
                me = null;
                renderAccess();
                feedback('Avatar abgemeldet.');
                if (token) {
                  const body = new URLSearchParams({ token });
                  fetch('/oauth/revoke', { method: 'POST', credentials: 'omit',
                    headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
                    body }).catch(() => {});
                }
                await load();
              }
              function renderAccess() {
                const signedIn = !!me;
                if ($('ogl-login')) $('ogl-login').hidden = signedIn || !config.clientId;
                if ($('ogl-logout')) $('ogl-logout').hidden = !signedIn;
                if ($('ogl-compose')) $('ogl-compose').hidden =
                  !signedIn || me.avatar_id !== config.ownerId;
                if ($('ogl-follow-controls')) $('ogl-follow-controls').hidden =
                  !signedIn || !config.ownerId || me.avatar_id === config.ownerId ||
                  !config.published;
                if ($('ogl-tab-following')) $('ogl-tab-following').disabled = !signedIn;
                authStatus(signedIn ? 'Angemeldet als ' + me.name :
                  (config.clientId ? 'Zum Schreiben mit deinem Avatar anmelden.' :
                    'Dieser Feed ist derzeit nur lesbar (OAuth nicht konfiguriert).'));
              }
              function switchTab(next) {
                if (next === 'following' && !me) {
                  feedback('Zum Lesen deiner abonnierten Beitraege zuerst anmelden.', true);
                  return;
                }
                tab = next;
                for (const t of ['profile', 'following']) {
                  if ($('ogl-tab-' + t))
                    $('ogl-tab-' + t).setAttribute('aria-pressed', String(t === next));
                }
                if ($('ogl-compose')) $('ogl-compose').hidden =
                  next !== 'profile' || !me || me.avatar_id !== config.ownerId;
                if ($('ogl-follow-controls')) $('ogl-follow-controls').hidden =
                  next !== 'profile' || !me || me.avatar_id === config.ownerId ||
                  !config.published;
                load().catch(e => feedback(e.message, true));
              }
              async function publish() {
                const box = $('ogl-post-text');
                const text = box.value.trim();
                if (!text || text.length > 1500) return feedback('Bitte Text mit 1-1500 Zeichen eingeben.', true);
                const button = $('ogl-publish');
                button.disabled = true;
                try {
                  await api(base + config.ownerId, 'POST', { text });
                  box.value = '';
                  $('ogl-counter').textContent = '0/1500';
                  feedback('Dein Beitrag wurde veroeffentlicht.');
                  await load();
                } finally { button.disabled = false; }
              }
              async function toggleFollow() {
                const next = !following;
                await api(base + config.ownerId + '/following', next ? 'PUT' : 'DELETE');
                following = next;
                $('ogl-follow').textContent = following ? 'Entfolgen' : 'Folgen';
                feedback(following ? 'Avatar abonniert.' : 'Avatar nicht mehr abonniert.');
                await load();
              }
              async function toggleLike(owner, post) {
                const wasLiked = liked.has(post);
                await api(base + owner + '/' + post + '/likes', wasLiked ? 'DELETE' : 'PUT');
                if (wasLiked) liked.delete(post); else liked.add(post);
                await load();
              }
              async function displayComments(owner, post, target) {
                target.replaceChildren();
                const data = await api(base + owner + '/' + post + '/comments');
                for (const comment of (data.comments || [])) {
                  const line = node('div', 'comment', '', target);
                  node('strong', '', comment.author_name || 'Einwohner', line);
                  node('span', '', ': ' + comment.text, line);
                  if (me && comment.author_id === me.avatar_id) {
                    action('Loeschen', line, async () => {
                      await api(base + owner + '/' + post + '/comments/' + comment.id, 'DELETE');
                      await displayComments(owner, post, target);
                    });
                  }
                }
                if (me) {
                  const row = node('div', 'ogl-comment-compose', '', target);
                  const entry = node('textarea', '', '', row);
                  entry.maxLength = 800;
                  entry.rows = 2;
                  entry.placeholder = 'Kommentar schreiben...';
                  const send = action('Senden', row, async () => {
                    const text = entry.value.trim();
                    if (!text || text.length > 800) return;
                    send.disabled = true;
                    try {
                      await api(base + owner + '/' + post + '/comments', 'POST', { text });
                      await displayComments(owner, post, target);
                      feedback('Kommentar gespeichert.');
                    } finally { send.disabled = false; }
                  });
                }
              }
              function renderPost(data, parent) {
                const owner = data.owner_id || config.ownerId;
                const section = node('article', 'post', '', parent);
                if (data.owner_name) {
                  const link = node('a', 'ogl-user-link', data.owner_name, section);
                  link.href = '/feed?name=' + encodeURIComponent(data.owner_name.replaceAll(' ', '.'));
                } else {
                  node('strong', '', config.ownerName, section);
                }
                node('div', 'meta', formatTime(data.created_at), section);
                node('div', 'content', data.text, section);
                node('div', 'meta', (data.likes || 0) + ' Likes · ' +
                  (data.comments || 0) + ' Kommentare', section);
                const buttons = node('div', 'ogl-actions', '', section);
                if (me) {
                  action(liked.has(data.id) ? 'Like entfernen' : 'Gefällt mir', buttons,
                    () => toggleLike(owner, data.id));
                }
                const commentArea = node('div', 'ogl-comment-list', '', section);
                action('Kommentare anzeigen', buttons,
                  () => displayComments(owner, data.id, commentArea));
                if (me && me.avatar_id === owner) {
                  action('Beitrag loeschen', buttons, async () => {
                    if (!confirm('Diesen Beitrag dauerhaft loeschen?')) return;
                    await api(base + owner + '/' + data.id, 'DELETE');
                    feedback('Beitrag geloescht.');
                    await load();
                  });
                }
              }
              async function load() {
                if (!config.ownerId) return;
                const target = $('ogl-feed-posts');
                if (!target) return;
                const route = tab === 'following' ? base + 'timeline' : base + config.ownerId;
                const data = await api(route);
                following = !!data.following;
                if ($('ogl-follow')) $('ogl-follow').textContent =
                  following ? 'Entfolgen' : 'Folgen';
                liked = new Set(data.liked_post_ids || []);
                loadedPosts = data.posts || [];
                target.replaceChildren();
                if (!loadedPosts.length) {
                  node('div', 'ogl-empty', tab === 'following' ?
                    'Noch keine oeffentlichen Beitraege von abonnierten Avataren.' :
                    'Hier gibt es noch keine Beitraege.', target);
                  return;
                }
                for (const post of loadedPosts) renderPost(post, target);
              }
              async function initialize() {
                // This is the fixed registered OAuth callback, without name.
                if (params.has('code') || params.has('error')) {
                  try { await completeLogin(); }
                  catch (error) { feedback(error.message, true); }
                  return;
                }
                if ($('ogl-tab-profile')) {
                  $('ogl-login').addEventListener('click', () =>
                    startLogin().catch(e => feedback(e.message, true)));
                  $('ogl-logout').addEventListener('click', logout);
                  $('ogl-publish').addEventListener('click', () =>
                    publish().catch(e => feedback(e.message, true)));
                  $('ogl-post-text').addEventListener('input', (event) => {
                    $('ogl-counter').textContent = event.target.value.length + '/1500';
                  });
                  $('ogl-follow').addEventListener('click', () =>
                    toggleFollow().catch(e => feedback(e.message, true)));
                  for (const name of ['profile', 'following']) {
                    $('ogl-tab-' + name).addEventListener('click', () => switchTab(name));
                  }
                }
                if (access()) {
                  try { me = await api(base + 'me'); }
                  catch (error) {
                    sessionStorage.removeItem(tokenKey);
                    sessionStorage.removeItem(expiresKey);
                  }
                }
                renderAccess();
                try { await load(); } catch (error) { feedback(error.message, true); }
              }
              initialize().catch(error => feedback(error.message, true));
            })();
            """;
    }
}
