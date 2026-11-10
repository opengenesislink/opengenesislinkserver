"use strict";
const CACHE = "nexverse-messenger-shell-v1";
const ASSETS = ["/", "/index.html", "/style.css", "/app.js", "/icon.svg",
  "/manifest.webmanifest"];
self.addEventListener("install", event => {
  event.waitUntil(caches.open(CACHE).then(cache => cache.addAll(ASSETS)));
  self.skipWaiting();
});
self.addEventListener("activate", event => {
  event.waitUntil(caches.keys().then(keys => Promise.all(
    keys.filter(k => k !== CACHE).map(k => caches.delete(k)))));
  self.clients.claim();
});
self.addEventListener("fetch", event => {
  const request = event.request;
  if (request.method !== "GET" || new URL(request.url).origin !== self.location.origin)
    return;
  const path = new URL(request.url).pathname;
  // Never cache avatar sessions, World API traffic or personal messages.
  if (path.startsWith("/auth") || path.startsWith("/api") || path === "/healthz")
    return;
  if (!ASSETS.includes(path)) return;
  event.respondWith(caches.match(request).then(cached =>
    cached || fetch(request)));
});
