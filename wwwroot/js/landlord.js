/* EZ-Rent · Chủ trọ. JavaScript thuần, không phụ thuộc thư viện.
   Nguyên tắc: trang vẫn dùng được khi tắt JS (link/form thường); JS chỉ làm nhanh hơn bằng AJAX (fetch):
   - Lọc, đổi tab, chuyển trang số: tải lại đúng vùng danh sách (header X-EZ-Region), cập nhật URL bằng history.
   - Thao tác POST (xác nhận, từ chối, đánh dấu đã thanh toán...): gửi fetch kèm CSRF token có sẵn trong form, nhận JSON.
   - Tính tiền điện nước / hóa đơn: gọi server tính và hiển thị ngay khi nhập.
   - Tin nhắn: gửi không tải lại trang, tự kiểm tra tin mới mỗi 15 giây khi tab đang mở.
   Mọi nội dung từ server chèn vào trang bằng textContent hoặc HTML do Razor đã encode. */
(function () {
  "use strict";
  const live = document.getElementById("ll-live");
  const announce = (text) => { if (live) { live.textContent = ""; setTimeout(() => { live.textContent = text; }, 30); } };

  // ---------- Thông báo ----------
  function flash(message, kind) {
    if (!message) return;
    let box = document.getElementById("ll-flash");
    if (!box) {
      box = document.createElement("div");
      box.id = "ll-flash";
      box.setAttribute("role", "status");
      const main = document.getElementById("ll-main");
      main.insertBefore(box, main.firstChild);
    }
    box.className = "ll-flash" + (kind === "error" ? " is-error" : "");
    box.replaceChildren();
    const span = document.createElement("span");
    span.textContent = message;
    const close = document.createElement("button");
    close.type = "button"; close.setAttribute("aria-label", "Đóng thông báo"); close.setAttribute("data-ll-dismiss", ""); close.textContent = "×";
    box.append(span, close);
    box.hidden = false;
    if (kind === "error") box.scrollIntoView({ block: "nearest" });
  }

  // ---------- Tải lại một vùng ----------
  async function loadRegion(id, url, push) {
    const region = document.getElementById(id);
    if (!region) { location.assign(url); return; }
    region.classList.add("is-loading");
    region.setAttribute("aria-busy", "true");
    try {
      const res = await fetch(url, { headers: { "X-EZ-Region": id }, credentials: "same-origin" });
      if (res.redirected && new URL(res.url).pathname.startsWith("/Identity")) { location.assign(res.url); return; }
      if (!res.ok) throw new Error(String(res.status));
      region.innerHTML = await res.text();
      if (push) history.pushState({ region: id }, "", url);
      const heading = region.querySelector("[data-ll-focus]") || region;
      if (!heading.hasAttribute("tabindex")) heading.setAttribute("tabindex", "-1");
      if (push) heading.focus({ preventScroll: true });
      const summary = region.querySelector("[data-ll-announce]");
      announce(summary ? summary.textContent.trim() : "Đã cập nhật danh sách");
      if (push && region.getBoundingClientRect().top < 0) region.scrollIntoView({ block: "start" });
    } catch (e) {
      location.assign(url);
    } finally {
      region.classList.remove("is-loading");
      region.removeAttribute("aria-busy");
    }
  }

  const firstRegion = document.querySelector("[data-ll-region]");
  if (firstRegion) history.replaceState({ region: firstRegion.id }, "", location.href);
  window.addEventListener("popstate", (e) => {
    if (e.state && e.state.region && document.getElementById(e.state.region)) loadRegion(e.state.region, location.href, false);
    else location.reload();
  });

  // ---------- Link điều hướng trong vùng (tab, phân trang) ----------
  document.addEventListener("click", (e) => {
    const dismiss = e.target.closest("[data-ll-dismiss]");
    if (dismiss) { dismiss.closest(".ll-flash").hidden = true; return; }
    const print = e.target.closest("[data-ll-print]");
    if (print) { e.preventDefault(); window.print(); return; }
    const link = e.target.closest("a[data-ll-nav]");
    if (!link || e.ctrlKey || e.metaKey || e.shiftKey || e.button !== 0) return;
    e.preventDefault();
    loadRegion(link.getAttribute("data-ll-nav"), link.href, true);
  });

  // ---------- Bộ lọc (GET) ----------
  function filterUrl(form) {
    const url = new URL(form.getAttribute("action") || location.pathname, location.origin);
    url.search = "";
    for (const [key, value] of new FormData(form)) {
      if (typeof value === "string" && value.trim() !== "" && key !== "page") url.searchParams.append(key, value.trim());
    }
    return url.toString();
  }
  document.addEventListener("change", (e) => {
    const form = e.target.closest("form[data-ll-filter]");
    if (form && (e.target.matches("select") || e.target.type === "checkbox" || e.target.type === "month")) form.requestSubmit();

    const reload = e.target.closest("select[data-ll-reload]");
    if (reload) {
      const url = new URL(location.href);
      url.searchParams.set(reload.getAttribute("data-ll-reload"), reload.value);
      location.assign(url.toString());
    }

    const defaults = e.target.closest("select[data-ll-defaults]");
    if (defaults && defaults.value) fillDefaults(defaults);

    const file = e.target.closest("input[type=file][data-ll-preview]");
    if (file) previewFiles(file);
  });

  // ---------- Gửi form ----------
  document.addEventListener("submit", async (e) => {
    const form = e.target;
    // jquery.validate (kiểm tra phía client) đã chặn form sai: không làm gì thêm.
    if (e.defaultPrevented) return;
    const question = form.getAttribute("data-ll-confirm");
    if (question && !window.confirm(question)) { e.preventDefault(); return; }

    if (form.hasAttribute("data-ll-filter")) {
      e.preventDefault();
      loadRegion(form.getAttribute("data-ll-filter"), filterUrl(form), true);
      return;
    }
    if (form.hasAttribute("data-ll-chat")) { e.preventDefault(); sendChat(form); return; }
    if (!form.hasAttribute("data-ll-action")) { form.classList.add("is-busy"); return; }

    e.preventDefault();
    if (form.classList.contains("is-busy")) return;
    const error = form.querySelector("[data-ll-error]");
    if (error) error.textContent = "";
    form.classList.add("is-busy");
    try {
      const res = await fetch(form.action, {
        method: "POST", body: new FormData(form), credentials: "same-origin",
        headers: { "X-Requested-With": "fetch", "Accept": "application/json" }
      });
      const type = res.headers.get("content-type") || "";
      if (!type.includes("application/json")) {
        flash(res.status === 400 ? "Phiên làm việc đã hết hạn. Hãy tải lại trang rồi thử lại." : "Không thực hiện được thao tác. Hãy tải lại trang.", "error");
        return;
      }
      const data = await res.json();
      if (!data.ok) {
        if (error) error.textContent = data.message; else flash(data.message, "error");
        announce(data.message);
        return;
      }
      flash(data.message, "ok");
      announce(data.message);
      const refresh = form.getAttribute("data-ll-refresh");
      if (refresh && document.getElementById(refresh)) await loadRegion(refresh, location.href, false);
      else location.reload();
    } catch (err) {
      flash("Mất kết nối tới máy chủ. Kiểm tra mạng rồi thử lại.", "error");
    } finally {
      form.classList.remove("is-busy");
    }
  });

  // ---------- Tính tiền bằng AJAX ----------
  const timers = new WeakMap();
  function calcQuery(form) {
    const params = new URLSearchParams();
    for (const [key, value] of new FormData(form)) {
      if (typeof value !== "string" || key === "__RequestVerificationToken") continue;
      params.append(key, value);
    }
    return params.toString();
  }
  async function runCalc(form) {
    const url = form.getAttribute("data-ll-calc");
    const outs = form.querySelectorAll("[data-ll-out]");
    const box = form.querySelector("[data-ll-calc-box]");
    if (box) box.setAttribute("aria-busy", "true");
    try {
      const res = await fetch(url + (url.includes("?") ? "&" : "?") + calcQuery(form), {
        headers: { "X-Requested-With": "fetch", "Accept": "application/json" }, credentials: "same-origin"
      });
      const data = await res.json();
      outs.forEach((el) => {
        const key = el.getAttribute("data-ll-out");
        if (key === "message") { el.textContent = data.message || ""; el.classList.toggle("is-error", !data.ok); }
        else if (data.ok && key in data) el.textContent = data[key];
        else if (!data.ok) el.textContent = "—";
      });
    } catch (e) { /* Bản xem trước không bắt buộc; server vẫn tính lại khi lưu. */ }
    finally { if (box) box.removeAttribute("aria-busy"); }
  }
  document.addEventListener("input", (e) => {
    const form = e.target.closest("form[data-ll-calc]");
    if (!form || e.target.type === "file") return;
    clearTimeout(timers.get(form));
    timers.set(form, setTimeout(() => runCalc(form), 350));
  });
  document.addEventListener("change", (e) => {
    const form = e.target.closest("form[data-ll-calc]");
    if (form && (e.target.type === "checkbox" || e.target.matches("select"))) runCalc(form);
  });
  document.querySelectorAll("form[data-ll-calc][data-ll-calc-auto]").forEach(runCalc);

  // ---------- Điền sẵn giá theo phòng (form hợp đồng) ----------
  async function fillDefaults(select) {
    try {
      const url = select.getAttribute("data-ll-defaults") + "?roomId=" + encodeURIComponent(select.value);
      const res = await fetch(url, { headers: { "X-Requested-With": "fetch" }, credentials: "same-origin" });
      const data = await res.json();
      if (!data.ok) { flash(data.message, "error"); return; }
      const form = select.form;
      for (const key of ["rent", "deposit", "electricity", "water"]) {
        const input = form.querySelector('[data-ll-fill="' + key + '"]');
        if (input && data[key] !== undefined) input.value = data[key];
      }
      announce("Đã điền giá theo tin đăng của phòng. Bạn vẫn có thể sửa.");
    } catch (e) { /* giữ nguyên giá đang nhập */ }
  }

  // ---------- Xem trước ảnh trước khi tải lên ----------
  function previewFiles(input) {
    const target = document.querySelector(input.getAttribute("data-ll-preview"));
    if (!target) return;
    target.replaceChildren();
    const maxImage = 5 * 1024 * 1024, maxVideo = 25 * 1024 * 1024;
    const problems = [];
    for (const file of input.files) {
      const isVideo = file.type.startsWith("video/");
      if (file.size > (isVideo ? maxVideo : maxImage)) problems.push(file.name + " quá dung lượng cho phép.");
      if (file.type.startsWith("image/")) {
        const img = document.createElement("img");
        img.alt = "Xem trước " + file.name;
        img.src = URL.createObjectURL(file);
        img.onload = () => URL.revokeObjectURL(img.src);
        target.append(img);
      } else {
        const span = document.createElement("span");
        span.textContent = file.name;
        target.append(span);
      }
    }
    input.setCustomValidity(problems.join(" "));
    if (problems.length) { input.reportValidity(); }
    else if (input.files.length) {
      const note = document.createElement("span");
      note.textContent = input.files.length + " file chưa lưu";
      target.append(note);
    }
  }

  // ---------- Danh sách dịch vụ (thêm/bớt dòng) ----------
  function renumber(container) {
    container.querySelectorAll("[data-ll-repeat-row]").forEach((row, index) => {
      row.querySelectorAll("[name],[id],[for],[data-valmsg-for]").forEach((el) => {
        for (const attr of ["name", "id", "for", "data-valmsg-for"]) {
          const value = el.getAttribute(attr);
          if (value) el.setAttribute(attr, value.replace(/\[\d+\]/, "[" + index + "]").replace(/_\d+__/, "_" + index + "__"));
        }
      });
    });
  }
  document.addEventListener("click", (e) => {
    const add = e.target.closest("[data-ll-repeat-add]");
    if (add) {
      const container = document.getElementById(add.getAttribute("data-ll-repeat-add"));
      const rows = container.querySelectorAll("[data-ll-repeat-row]");
      const max = Number(container.getAttribute("data-ll-max") || 10);
      if (rows.length >= max) { flash("Tối đa " + max + " dịch vụ.", "error"); return; }
      const template = container.querySelector("template");
      const html = template.innerHTML.replaceAll("__i__", String(rows.length));
      template.insertAdjacentHTML("beforebegin", html);
      const inserted = container.querySelectorAll("[data-ll-repeat-row]");
      inserted[inserted.length - 1].querySelector("input").focus();
      return;
    }
    const remove = e.target.closest("[data-ll-repeat-remove]");
    if (remove) {
      const container = remove.closest("[data-ll-repeat]");
      remove.closest("[data-ll-repeat-row]").remove();
      renumber(container);
    }
  });

  // ---------- Tin nhắn ----------
  const thread = document.getElementById("ll-thread");
  function bubble(item) {
    const div = document.createElement("div");
    div.className = "ll-msg" + (item.mine ? " mine" : "");
    div.dataset.id = item.id;
    const p = document.createElement("p"); p.textContent = item.content;
    const t = document.createElement("time"); t.textContent = item.time;
    div.append(p, t);
    return div;
  }
  function lastId() {
    const items = thread ? thread.querySelectorAll(".ll-msg[data-id]") : [];
    return items.length ? Number(items[items.length - 1].dataset.id) : Number(thread?.dataset.lastId || 0);
  }
  function appendMessages(items) {
    if (!thread || !items.length) return;
    const empty = thread.querySelector(".ll-empty"); if (empty) empty.remove();
    for (const item of items) if (!thread.querySelector('.ll-msg[data-id="' + item.id + '"]')) thread.append(bubble(item));
    thread.scrollTop = thread.scrollHeight;
  }
  async function sendChat(form) {
    const box = form.querySelector("textarea");
    if (!box.value.trim()) { box.focus(); return; }
    form.classList.add("is-busy");
    try {
      const res = await fetch(form.action, { method: "POST", body: new FormData(form), credentials: "same-origin",
        headers: { "X-Requested-With": "fetch", "Accept": "application/json" } });
      const data = await res.json();
      if (!data.ok) { flash(data.message, "error"); return; }
      appendMessages([data.item]);
      box.value = "";
      box.focus();
    } catch (e) { flash("Chưa gửi được tin nhắn. Kiểm tra mạng rồi thử lại.", "error"); }
    finally { form.classList.remove("is-busy"); }
  }
  if (thread && thread.dataset.since) {
    thread.scrollTop = thread.scrollHeight;
    setInterval(async () => {
      if (document.visibilityState !== "visible") return;
      try {
        const res = await fetch(thread.dataset.since + "?after=" + lastId(), { headers: { "X-Requested-With": "fetch" }, credentials: "same-origin" });
        const data = await res.json();
        if (data.ok && data.items.length) { appendMessages(data.items); announce(data.items.length + " tin nhắn mới"); }
      } catch (e) { /* thử lại ở lượt sau */ }
    }, 15000);
    document.addEventListener("keydown", (e) => {
      if (e.key === "Enter" && (e.ctrlKey || e.metaKey) && e.target.closest("form[data-ll-chat]")) e.target.form.requestSubmit();
    });
  }

  // Mở lại nút khi quay về trang bằng nút Back của trình duyệt.
  window.addEventListener("pageshow", () => document.querySelectorAll("form.is-busy").forEach((f) => f.classList.remove("is-busy")));
})();
