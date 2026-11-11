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
                  <div class="ogl-navtabs" role="group" aria-label="Ansicht auswaehlen">
                    <button id="ogl-tab-profile" type="button" aria-pressed="true">
                      <span aria-hidden="true">▦</span> Profilfeed</button>
                    <button id="ogl-tab-following" type="button" aria-pressed="false">
                      <span aria-hidden="true">◈</span> Abonniert</button>
                  </div>
                  <span class="ogl-spacer"></span>
                  <button id="ogl-login" class="ogl-login" type="button">
                    <span aria-hidden="true">↗</span> Mit Avatar anmelden</button>
                  <button id="ogl-logout" class="ogl-outline" type="button" hidden>Abmelden</button>
                </nav>
                <p id="ogl-auth-status" class="ogl-auth-status" role="status"></p>
                <section id="ogl-compose" class="ogl-compose" aria-label="Neuen Beitrag erstellen" hidden>
                  <div class="ogl-compose-heading">
                    <span class="ogl-portrait ogl-portrait-small" aria-hidden="true">NV</span>
                    <div><strong>Teile deinen NexVerse-Moment</strong>
                    <span class="sub">Was passiert heute in deiner Welt?</span></div>
                  </div>
                  <label for="ogl-post-text" class="ogl-visually-hidden">Neuen Beitrag verfassen</label>
                  <textarea id="ogl-post-text" maxlength="1500" rows="3"
                    placeholder="Was gibt es Neues in deinem NexVerse?"></textarea>
                  <div class="ogl-compose-bottom">
                    <span class="ogl-compose-hint">Deine Geschichte. Deine Welt.</span>
                    <span id="ogl-counter" class="sub" aria-live="polite">0/1500</span>
                    <button id="ogl-publish" class="ogl-primary" type="button">
                      <span aria-hidden="true">↗</span> Veroeffentlichen</button>
                  </div>
                </section>
                <div id="ogl-follow-controls" class="ogl-follow-controls" hidden>
                  <span class="sub">Halte dich ueber diesen Einwohner auf dem Laufenden.</span>
                  <button id="ogl-follow" class="ogl-primary" type="button">Folgen</button>
                </div>
                <p id="ogl-feedback" class="ogl-feedback" role="status" aria-live="polite"></p>
                <div class="ogl-stream-heading">
                  <div><div class="ogl-eyebrow">DEIN NEXVERSE</div>
                  <h2 id="ogl-stream-title">Aktuelles aus dem Profil</h2></div>
                  <span class="ogl-live-dot" aria-hidden="true"></span>
                </div>
                """;
        }

        internal static string Styles()
        {
            return """
                :root{color-scheme:dark;--nv-bg:#070e1e;--nv-panel:#121d34;
                  --nv-border:rgba(161,192,245,.17);--nv-txt:#f0f6ff;--nv-muted:#95a9c8;
                  --nv-cyan:#70e2ef;--nv-blue:#598af9;--nv-purple:#a27af4;
                  --nv-glow:rgba(86,146,240,.22);--nv-radius:18px}
                *{box-sizing:border-box}html{scroll-behavior:smooth}
                body{min-height:100vh;margin:0;padding:0;background:var(--nv-bg);
                  color:var(--nv-txt);font:14px/1.65 system-ui,-apple-system,"Segoe UI",sans-serif}
                body::before{content:"";position:fixed;inset:0;z-index:0;pointer-events:none;
                  background:radial-gradient(ellipse 65% 36% at 90% 0%,rgba(103,81,211,.22),transparent),
                  radial-gradient(ellipse 55% 36% at 0% 15%,rgba(44,131,206,.20),transparent)}
                [hidden]{display:none!important}button,textarea{font:inherit}
                button{display:inline-flex;align-items:center;justify-content:center;gap:7px;
                  min-height:38px;padding:7px 13px;color:#dfeeff;cursor:pointer;
                  border:1px solid rgba(142,169,213,.27);border-radius:11px;
                  background:rgba(92,119,160,.14);transition:background .15s,border-color .15s,transform .15s}
                button:hover{border-color:rgba(113,223,243,.67);background:rgba(80,120,175,.25)}
                button:active{transform:translateY(1px)}
                button:disabled{cursor:not-allowed;opacity:.45}
                button:focus-visible,textarea:focus-visible,a:focus-visible{
                  outline:2px solid var(--nv-cyan);outline-offset:3px}
                button.ogl-primary{background:linear-gradient(115deg,#497af0,#7555dd);
                  color:white;border-color:rgba(179,187,255,.47);font-weight:650;
                  box-shadow:0 6px 18px rgba(58,90,189,.20)}
                button.ogl-primary:hover{background:linear-gradient(115deg,#5893ff,#8b68f3)}
                button.ogl-outline{background:transparent}
                a{color:#88ddec;text-decoration:none}a:hover{text-decoration:underline}
                .ogl-shell{position:relative;z-index:1;max-width:870px;margin:0 auto;padding:0 18px 30px}
                .ogl-brandbar{display:flex;align-items:center;gap:12px;padding:19px 1px 15px}
                .ogl-emblem{display:inline-flex;align-items:center;justify-content:center;
                  flex:0 0 43px;width:43px;height:43px;border-radius:14px;
                  border:1px solid rgba(167,222,255,.48);background:linear-gradient(140deg,#123d6f,#6657d6);
                  font-weight:850;font-size:15px;letter-spacing:-1.5px;color:white;
                  box-shadow:0 0 26px rgba(88,133,241,.24)}
                .ogl-brandname{font-size:18px;font-weight:830;letter-spacing:.1px;line-height:1.1;
                  background:linear-gradient(100deg,#f2fcff,#8fe8ff 65%,#b3a4ff);
                  -webkit-background-clip:text;background-clip:text;color:transparent}
                .ogl-brandtag{display:block;color:var(--nv-muted);font-size:10px;
                  letter-spacing:1.5px;text-transform:uppercase;font-weight:650}
                .ogl-brandbar .ogl-spacer{flex:1}
                .ogl-network-chip{font-size:11px;color:#9ddce1;border:1px solid rgba(84,199,197,.28);
                  background:rgba(22,117,136,.16);padding:5px 10px;border-radius:999px;
                  white-space:nowrap}
                .ogl-hero{position:relative;overflow:hidden;border-radius:22px;
                  border:1px solid rgba(148,181,249,.27);
                  background:linear-gradient(119deg,#172e54 0%,#15213f 45%,#32235e 100%);
                  box-shadow:0 18px 45px rgba(3,8,22,.25);margin:0 0 15px}
                .ogl-hero::before{content:"";pointer-events:none;position:absolute;inset:0;
                  background:radial-gradient(circle at 87% 28%,rgba(167,117,254,.27),transparent 32%),
                  linear-gradient(135deg,transparent 60%,rgba(101,207,254,.12) 61%,
                  transparent 62%);opacity:.9}
                .ogl-cover{height:76px;border-bottom:1px solid rgba(168,191,245,.13);
                  background:linear-gradient(115deg,rgba(26,167,208,.14),transparent 45%),
                  radial-gradient(circle at 78% 60%,rgba(125,106,228,.25),transparent 35%)}
                .ogl-profile{position:relative;display:flex;align-items:center;gap:16px;
                  padding:0 20px 22px;margin-top:-23px}
                .ogl-portrait{display:inline-flex;align-items:center;justify-content:center;
                  flex:none;width:66px;height:66px;border:3px solid #17274a;border-radius:20px;
                  color:white;font-weight:800;font-size:21px;letter-spacing:-1px;
                  background:linear-gradient(145deg,#087b9d,#516dda 55%,#986fe7);
                  box-shadow:0 9px 22px rgba(10,13,37,.38)}
                .ogl-portrait-small{width:42px;height:42px;font-size:13px;
                  border-radius:13px;border-width:2px}
                .ogl-profile-copy{padding-top:24px;min-width:0}
                .ogl-kicker{color:#89eaf2;font-size:10px;letter-spacing:1.6px;
                  font-weight:800;text-transform:uppercase}
                .ogl-profile h1{font-size:clamp(19px,4vw,26px);line-height:1.25;
                  margin:3px 0 2px;overflow-wrap:anywhere;letter-spacing:-.5px}
                .ogl-subtitle{color:#adc4e1;font-size:12px}
                .ogl-controls{display:flex;align-items:center;gap:8px;flex-wrap:wrap;
                  margin:14px 0 6px;padding:10px;background:rgba(20,33,59,.75);
                  border:1px solid var(--nv-border);border-radius:15px}
                .ogl-navtabs{display:flex;align-items:center;gap:5px;flex-wrap:wrap}
                .ogl-controls button{border-color:transparent;background:transparent}
                .ogl-controls button[aria-pressed=true]{background:rgba(73,127,205,.28);
                  border-color:rgba(124,187,246,.38);color:#effdff}
                .ogl-controls .ogl-login{background:linear-gradient(110deg,#387bb3,#5b55bb);
                  font-weight:650}
                .ogl-spacer{flex:1}
                .ogl-auth-status{margin:5px 4px 14px;font-size:11px;color:var(--nv-muted)}
                .ogl-compose{margin:12px 0 17px;padding:18px;border-radius:var(--nv-radius);
                  border:1px solid rgba(105,173,238,.29);
                  background:linear-gradient(145deg,rgba(26,48,78,.96),rgba(20,28,56,.97));
                  box-shadow:0 12px 32px rgba(5,9,25,.20)}
                .ogl-compose-heading{display:flex;align-items:center;gap:12px;margin-bottom:13px}
                .ogl-compose-heading strong{display:block;font-size:14px}
                .ogl-compose-heading .sub{display:block;font-size:11px}
                textarea{resize:vertical;display:block;width:100%;min-height:90px;
                  border:1px solid rgba(137,171,219,.3);border-radius:12px;
                  color:var(--nv-txt);background:rgba(6,15,32,.65);padding:12px 14px;
                  font-size:13px;line-height:1.6}
                textarea::placeholder{color:#7f98b8}
                .ogl-compose-bottom{display:flex;align-items:center;justify-content:flex-end;
                  gap:12px;margin-top:12px;flex-wrap:wrap}
                .ogl-compose-hint{margin-right:auto;color:#829dbb;font-size:11px}
                .ogl-follow-controls{display:flex;align-items:center;gap:14px;
                  justify-content:space-between;margin:12px 0;padding:10px 13px;
                  background:rgba(31,49,76,.55);border:1px solid var(--nv-border);border-radius:12px}
                .ogl-feedback{margin:4px 2px;color:var(--nv-cyan);font-size:12px}
                .ogl-error{color:#ffb6ae!important}
                .ogl-stream-heading{display:flex;align-items:center;justify-content:space-between;
                  margin:18px 2px 10px;padding:0 3px}
                .ogl-stream-heading h2{font-size:16px;margin:0;font-weight:750;letter-spacing:-.2px}
                .ogl-eyebrow{font-size:10px;letter-spacing:1.5px;color:#87b9e1;font-weight:750}
                .ogl-live-dot{width:8px;height:8px;border-radius:50%;background:#76dfcf;
                  box-shadow:0 0 0 5px rgba(75,208,191,.1)}
                .post{position:relative;overflow:hidden;margin:11px 0;padding:18px 19px 15px;
                  background:linear-gradient(135deg,rgba(24,40,69,.96),rgba(18,28,52,.96));
                  border:1px solid var(--nv-border);border-radius:var(--nv-radius);
                  box-shadow:0 6px 19px rgba(0,0,0,.12)}
                .post:hover{border-color:rgba(125,178,241,.38)}
                .ogl-post-header{display:flex;gap:11px;align-items:center;margin-bottom:12px}
                .ogl-post-avatar{width:38px;height:38px;flex:none;display:flex;align-items:center;
                  justify-content:center;border-radius:12px;color:#ddf9ff;font-size:13px;
                  font-weight:850;background:linear-gradient(135deg,#206a8b,#6654b9)}
                .ogl-post-who{min-width:0;flex:1}
                .ogl-post-name{font-weight:750;font-size:13px;color:#f3f7ff;
                  overflow-wrap:anywhere}
                .ogl-post-name a{color:inherit}
                .meta{color:#94adce;font-size:11px}
                .content{white-space:pre-wrap;overflow-wrap:anywhere;font-size:14px;
                  line-height:1.75;color:#e5f1ff;margin:0 0 17px}
                .ogl-reactions{border-top:1px solid rgba(155,185,236,.13);
                  padding-top:11px;color:#9cb5d3;font-size:11px}
                .ogl-actions{display:flex;gap:7px;flex-wrap:wrap;margin-top:9px}
                .ogl-actions button{min-height:32px;font-size:11px;padding:5px 9px}
                .ogl-actions .ogl-liked{color:#f9a2d8;border-color:rgba(245,137,206,.36);
                  background:rgba(185,56,129,.13)}
                .ogl-comment-list{margin-top:11px}
                .comment{margin:7px 0 0;padding:10px 12px;background:rgba(9,22,43,.65);
                  border-left:3px solid #5586da;border-radius:4px 11px 11px 4px;
                  color:#dceafe;white-space:pre-wrap;overflow-wrap:anywhere}
                .comment strong{color:#90d8f4}
                .ogl-comment-compose{display:flex;gap:7px;margin-top:9px;align-items:flex-end}
                .ogl-comment-compose textarea{min-height:45px;flex:1}
                .sub,.ogl-muted{color:var(--nv-muted);font-size:12px}
                .ogl-user-link{color:#9de6f5}
                .ogl-empty{padding:38px 18px;text-align:center;border:1px dashed
                  rgba(124,171,227,.28);border-radius:17px;margin:10px 0;
                  color:#b5cce4;background:rgba(23,43,70,.35)}
                .ogl-loading{opacity:.65}
                footer.ogl-footer{display:flex;align-items:center;justify-content:space-between;
                  gap:10px;flex-wrap:wrap;padding:22px 4px 10px;color:#859bb9;
                  font-size:11px;border-top:1px solid rgba(146,177,227,.11);margin-top:24px}
                .ogl-powered{color:#91a5bd;letter-spacing:.2px}
                .ogl-powered strong{color:#cad6ea;font-weight:650}
                .ogl-visually-hidden{position:absolute;width:1px;height:1px;padding:0;
                  margin:-1px;overflow:hidden;clip:rect(0,0,0,0);white-space:nowrap;border:0}
                @media(max-width:560px){
                  .ogl-shell{padding:0 12px 24px}.ogl-profile{padding:0 14px 17px;gap:11px}
                  .ogl-portrait{width:58px;height:58px;border-radius:17px}
                  .ogl-brandbar{padding-top:13px}.ogl-network-chip{font-size:10px}
                  .ogl-controls{padding:7px}.ogl-controls button{font-size:12px;padding:6px 9px}
                  .ogl-navtabs{flex:1}.ogl-compose,.post{padding:14px;border-radius:14px}
                  .ogl-compose-hint{display:none}.ogl-profile-copy{padding-top:18px}
                  .ogl-comment-compose{flex-direction:column;align-items:stretch}
                  .ogl-comment-compose button{align-self:flex-end}
                }
                @media(prefers-reduced-motion:reduce){
                  *,*::before,*::after{transition:none!important;scroll-behavior:auto!important}
                }
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
              function initials(displayName) {
                const words = String(displayName || 'NexVerse').trim().split(/[.\s]+/).filter(Boolean);
                const letters = words.slice(0, 2).map(w => w.slice(0, 1).toUpperCase()).join('');
                return letters || 'NV';
              }
              function renderPost(data, parent) {
                const owner = data.owner_id || config.ownerId;
                const name = data.owner_name || config.ownerName || 'Einwohner';
                const section = node('article', 'post', '', parent);
                const header = node('div', 'ogl-post-header', '', section);
                node('span', 'ogl-post-avatar', initials(name), header)
                  .setAttribute('aria-hidden', 'true');
                const author = node('div', 'ogl-post-who', '', header);
                const authorName = node('div', 'ogl-post-name', '', author);
                if (data.owner_name) {
                  const link = node('a', 'ogl-user-link', name, authorName);
                  link.href = '/feed?name=' + encodeURIComponent(name.replaceAll(' ', '.'));
                } else {
                  node('strong', '', name, authorName);
                }
                node('div', 'meta', formatTime(data.created_at), author);
                node('div', 'content', data.text, section);
                const likes = Number(data.likes || 0);
                const comments = Number(data.comments || 0);
                node('div', 'ogl-reactions', likes + ' Gefaellt mir  ·  ' +
                  comments + ' Kommentare', section);
                const buttons = node('div', 'ogl-actions', '', section);
                if (me) {
                  const likeBtn = action(liked.has(data.id) ? '♥ Gefaellt mir' : '♡ Gefaellt mir',
                    buttons, () => toggleLike(owner, data.id));
                  if (liked.has(data.id)) likeBtn.classList.add('ogl-liked');
                  likeBtn.setAttribute('aria-pressed', String(liked.has(data.id)));
                }
                const commentArea = node('div', 'ogl-comment-list', '', section);
                action('◌ Kommentare', buttons,
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
                if ($('ogl-stream-title')) $('ogl-stream-title').textContent =
                  tab === 'following' ? 'Aus deinem Netzwerk' : 'Aktuelles aus dem Profil';
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
                    'Dein Feed ist noch ruhig. Folge anderen Einwohnern, um ihre Geschichten zu sehen.' :
                    'Noch keine Beitraege. Der naechste NexVerse-Moment wartet schon.', target);
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
