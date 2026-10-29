// SPDX-License-Identifier: MPL-2.0
// Browser widget for Stadt-NexVerse. Embed in an authenticated portal page.
// The host MUST supply getAccessToken() backed by the portal's OAuth flow.
// Never write a bearer token to localStorage, a URL or an HTML attribute.
export function mountOglIarSelfService(root, {
  getAccessToken, baseUrl = "", regionId
}) {
  if (!root || typeof getAccessToken !== "function")
    throw new Error("A container and the portal's OAuth token provider are required.");
  if (!/^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(regionId))
    throw new Error("A valid region UUID is required.");
  root.replaceChildren();
  const node = (tag, text, parent = root) => {
    const n = document.createElement(tag);
    if (text !== undefined) n.textContent = text;
    parent.append(n);
    return n;
  };
  node("h2", "Stadt-NexVerse · IAR Import / Export");
  node("p", "Nur dein angemeldeter Avatar. Große IAR-Archive werden blockweise übertragen.");
  const pathLabel = node("label", "Inventarpfad");
  const invPath = node("input", undefined, pathLabel);
  invPath.value = "/*";
  const actions = node("div");
  const exportButton = node("button", "Eigenes Inventar exportieren", actions);
  const importButton = node("button", "IAR importieren", actions);
  const fileInput = node("input", undefined, actions);
  fileInput.type = "file";
  fileInput.accept = ".iar";
  importButton.disabled = true;
  fileInput.addEventListener("change", () => {
    importButton.disabled = !fileInput.files?.[0];
  });
  const progress = node("progress");
  progress.max = 100;
  progress.value = 0;
  progress.style.width = "100%";
  const state = node("p", "Keine laufende Operation");
  state.setAttribute("role", "status");
  const detail = node("p", "");
  const downloads = node("div");
  let timer = null;
  let operationId = null;
  let busy = false;
  let stopped = false;
  const CHUNK = 4 * 1024 * 1024;

  async function api(path, init = {}) {
    const token = await getAccessToken();
    if (!token) throw new Error("Bitte erneut im Stadtportal anmelden.");
    const headers = new Headers(init.headers || {});
    headers.set("Authorization", "Bearer " + token);
    const res = await fetch(baseUrl + path, {
      ...init, headers, credentials: "omit", cache: "no-store"
    });
    if (!res.ok) {
      let reason = "HTTP " + res.status;
      try { reason += ": " + (await res.json()).error; } catch {}
      throw new Error(reason);
    }
    return res;
  }
  const base = "/api/v1/iar/me";
  const op = id => base + "/operations/" + encodeURIComponent(id);
  function stopPolling() { if (timer) clearInterval(timer); timer = null; }
  function setBusy(value) {
    busy = value; exportButton.disabled = value;
    importButton.disabled = value || !fileInput.files?.[0];
  }
  function fail(err) {
    stopPolling(); setBusy(false);
    state.textContent = "Fehler: " + (err?.message || String(err));
  }
  const formatSeconds = value => value == null ? "Noch nicht berechenbar"
    : value >= 60 ? Math.ceil(value / 60) + " Min."
    : value + " Sek.";
  function localDownloadText(data, name) {
    const blob = new Blob([data], { type: "text/plain;charset=utf-8" });
    const url = URL.createObjectURL(blob);
    const a = document.createElement("a");
    a.href = url; a.download = name; root.append(a); a.click(); a.remove();
    setTimeout(() => URL.revokeObjectURL(url), 3000);
  }

  async function exportFile(id, total) {
    if (!window.showSaveFilePicker) {
      state.textContent =
        "Für den großen IAR-Download bitte einen Browser mit Dateispeicher-API nutzen.";
      return;
    }
    const handle = await window.showSaveFilePicker({
      suggestedName: id + ".iar",
      types: [{description: "OpenSim Inventory Archive",
        accept: {"application/octet-stream": [".iar"]}}]
    });
    const writable = await handle.createWritable();
    try {
      let offset = 0;
      while (offset < total) {
        const take = Math.min(CHUNK, total - offset);
        const res = await api(op(id) + "/file?offset=" +
          offset + "&length=" + take);
        if (res.status !== 206) throw new Error("HTTP 206 erwartet");
        const bytes = await res.arrayBuffer();
        if (!bytes.byteLength || bytes.byteLength > take)
          throw new Error("Ungueltiger IAR-Block");
        await writable.write(bytes);
        offset += bytes.byteLength;
        progress.value = Math.floor(offset * 100 / total);
        detail.textContent = "Download " + offset + " / " + total + " Bytes";
      }
      await writable.close();
    } catch (error) { await writable.abort(); throw error; }
    const manifest = await api(op(id) + "/md5");
    localDownloadText(await manifest.text(), id + ".iar.md5");
    state.textContent = "Download abgeschlossen – MD5 und SHA-256 gespeichert";
  }

  async function renderStatus(id) {
    const job = await (await api(op(id))).json();
    if (stopped || id !== operationId) return;
    progress.value = job.progress_percent || 0;
    const eta = formatSeconds(job.eta_seconds);
    state.textContent = (job.action === "import" ? "Import" : "Export") +
      " · " + job.state + " · " + job.phase + " · " +
      progress.value + "%";
    detail.textContent = "Geschaetzte Restzeit: " + eta +
      (job.next_offset == null ? "" :
        " · Upload " + job.next_offset + " / " + job.expected_bytes + " Bytes");
    if (["completed", "failed", "cancelled"].includes(job.state)) {
      stopPolling(); setBusy(false);
      if (job.state === "failed") detail.textContent +=
        " · Fehler: " + (job.message || "Unbekannter Fehler");
      if (job.download_ready && !downloads.childElementCount) {
        const b = node("button", "IAR und MD5 herunterladen", downloads);
        b.addEventListener("click", async () => {
          b.disabled = true;
          try { await exportFile(id, job.file_size_bytes); }
          catch (e) { fail(e); }
          finally { b.disabled = false; }
        });
      }
    }
  }
  function watch(id) {
    operationId = id;
    downloads.replaceChildren();
    stopPolling();
    timer = setInterval(() => {
      renderStatus(id).catch(fail);
    }, 2000);
    renderStatus(id).catch(fail);
  }

  exportButton.addEventListener("click", async () => {
    setBusy(true);
    try {
      const res = await api(base + "/export", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ region_id: regionId,
          inventory_path: invPath.value || "/*" })
      });
      watch((await res.json()).operation_id);
    } catch (err) { fail(err); }
  });

  importButton.addEventListener("click", async () => {
    const file = fileInput.files?.[0];
    if (!file) return;
    setBusy(true);
    stopPolling();
    try {
      if (file.size <= 0) throw new Error("Leere IAR-Datei");
      // SHA-256 verification is required before the server imports.
      // Browser crypto.subtle.digest is not a streaming API. For >100MiB,
      // require a checksum calculated out of process instead of allocating
      // the entire archive into the JavaScript heap.
      const checksum = window.prompt(
        "Bitte die SHA-256-Pruefsumme der IAR-Datei eingeben (64 Hexzeichen):");
      if (!/^[0-9a-f]{64}$/i.test(checksum || ""))
        throw new Error("Gueltige SHA-256-Pruefsumme erforderlich");
      const res = await api(base + "/import", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({
          region_id: regionId,
          inventory_path: invPath.value || "/",
          archive_size_bytes: file.size,
          sha256: checksum.toLowerCase(),
          merge: true,
          dry_run: false
        })
      });
      const job = await res.json();
      const id = job.operation_id;
      operationId = id;
      downloads.replaceChildren();
      let offset = 0;
      while (offset < file.size) {
        const chunk = file.slice(offset, offset + CHUNK);
        try {
          const result = await api(op(id) + "/chunks?offset=" + offset, {
            method: "PUT",
            headers: { "Content-Type": "application/octet-stream" },
            body: chunk
          });
          offset = (await result.json()).next_offset;
        } catch (err) {
          // Resolve the actual persisted offset before retrying.
          const status = await (await api(op(id))).json();
          if (!Number.isSafeInteger(status.next_offset)) throw err;
          offset = status.next_offset;
        }
        progress.value = Math.floor(offset * 100 / file.size);
        state.textContent = "IAR Upload: " + progress.value + "%";
        detail.textContent = offset + " / " + file.size + " Bytes";
      }
      await api(op(id) + "/start", {method: "POST"});
      watch(id);
    } catch (err) { fail(err); }
  });

  return {
    destroy() {
      stopped = true;
      stopPolling();
      root.replaceChildren();
    }
  };
}
