/* EZ-Rent · Chủ trọ. JavaScript thuần chạy trên _Layout chung (Bootstrap 5 + jQuery có sẵn).
   Trang vẫn dùng được khi tắt JS (link/form thường, Post/Redirect/Get); JS chỉ làm nhanh hơn bằng AJAX (fetch):
   - Lọc, đổi tab, chuyển trang số: tải lại đúng vùng danh sách (header X-EZ-Region), URL cập nhật bằng history.
   - Thao tác POST (xác nhận, đã nhận tiền...): fetch kèm CSRF token có sẵn trong form, nhận ApiResult {success, message}
     giống trang Admin, rồi làm mới vùng liên quan.
   - Tính tiền điện nước / hóa đơn: server tính và trả JSON ngay khi nhập.
   Mọi chuỗi từ server được chèn bằng textContent. */
(function () {
  "use strict";
  const XHR = { "X-Requested-With": "XMLHttpRequest", "Accept": "application/json" };

  function showAlert(message, type) {
    if (!message) return;
    const box = document.getElementById("ll-alerts");
    if (!box) { window.alert(message); return; }
    const alert = document.createElement("div");
    alert.className = "alert alert-" + (type || "success") + " alert-dismissible fade show";
    alert.setAttribute("role", "alert");
    alert.append(document.createTextNode(message));
    const close = document.createElement("button");
    close.type = "button"; close.className = "btn-close"; close.setAttribute("data-bs-dismiss", "alert"); close.setAttribute("aria-label", "Đóng");
    alert.append(close);
    box.prepend(alert);
    if (type === "danger") box.scrollIntoView({ block: "nearest" });
  }

  // ---------- Tải lại một vùng danh sách ----------
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
      const focus = region.querySelector("[data-ll-focus]") || region;
      if (!focus.hasAttribute("tabindex")) focus.setAttribute("tabindex", "-1");
      if (push) focus.focus({ preventScroll: true });
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

  document.addEventListener("click", (e) => {
    const print = e.target.closest("[data-ll-print]");
    if (print) { e.preventDefault(); window.print(); return; }
    if (e.ctrlKey || e.metaKey || e.shiftKey || e.button !== 0) return;
    // Tab trạng thái và phân trang số (partial _Pagination của main) bên trong một vùng danh sách.
    const link = e.target.closest("a[data-ll-nav], [data-ll-region] .pagination a.page-link");
    if (!link || link.closest(".disabled")) return;
    const region = link.getAttribute("data-ll-nav") || link.closest("[data-ll-region]").id;
    e.preventDefault();
    loadRegion(region, link.href, true);
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
    const filter = e.target.closest("form[data-ll-filter]");
    if (filter && (e.target.matches("select") || e.target.type === "month")) filter.requestSubmit();

    const reload = e.target.closest("select[data-ll-reload]");
    if (reload) {
      const url = new URL(location.href);
      url.searchParams.set(reload.getAttribute("data-ll-reload"), reload.value);
      location.assign(url.toString());
    }
    const defaults = e.target.closest("select[data-ll-defaults]");
    if (defaults && defaults.value && defaults.value !== "0") fillDefaults(defaults);

    const file = e.target.closest("input[type=file][data-ll-preview]");
    if (file) previewFiles(file);
  });

  // ---------- Gửi form ----------
  document.addEventListener("submit", async (e) => {
    const form = e.target;
    if (e.defaultPrevented) return; // jquery-validate đã chặn form sai
    const question = form.getAttribute("data-ll-confirm");
    if (question && !window.confirm(question)) { e.preventDefault(); return; }

    if (form.hasAttribute("data-ll-filter")) {
      e.preventDefault();
      loadRegion(form.getAttribute("data-ll-filter"), filterUrl(form), true);
      return;
    }
    if (!form.hasAttribute("data-ll-action")) { form.classList.add("is-busy"); return; }

    e.preventDefault();
    if (form.classList.contains("is-busy")) return;
    const error = form.querySelector("[data-ll-error]");
    if (error) error.textContent = "";
    form.classList.add("is-busy");
    try {
      const res = await fetch(form.action, { method: "POST", body: new FormData(form), credentials: "same-origin", headers: XHR });
      const type = res.headers.get("content-type") || "";
      if (!type.includes("application/json")) {
        showAlert(res.status === 400 ? "Phiên làm việc đã hết hạn. Hãy tải lại trang rồi thử lại." : "Không thực hiện được thao tác. Hãy tải lại trang.", "danger");
        return;
      }
      const data = await res.json();
      if (!data.success) {
        if (error) error.textContent = data.message; else showAlert(data.message, "danger");
        return;
      }
      showAlert(data.message, "success");
      const refresh = form.getAttribute("data-ll-refresh");
      if (refresh && document.getElementById(refresh)) await loadRegion(refresh, location.href, false);
      else location.reload();
    } catch (err) {
      showAlert("Mất kết nối tới máy chủ. Kiểm tra mạng rồi thử lại.", "danger");
    } finally {
      form.classList.remove("is-busy");
    }
  });

  // ---------- Tính tiền bằng AJAX ----------
  const timers = new WeakMap();
  function calcQuery(form) {
    const params = new URLSearchParams();
    for (const [key, value] of new FormData(form)) {
      if (typeof value === "string" && key !== "__RequestVerificationToken") params.append(key, value);
    }
    return params.toString();
  }
  async function runCalc(form) {
    const url = form.getAttribute("data-ll-calc");
    const box = form.querySelector("[data-ll-calc-box]");
    if (box) box.setAttribute("aria-busy", "true");
    try {
      const res = await fetch(url + (url.includes("?") ? "&" : "?") + calcQuery(form), { headers: XHR, credentials: "same-origin" });
      const data = await res.json();
      form.querySelectorAll("[data-ll-out]").forEach((el) => {
        const key = el.getAttribute("data-ll-out");
        if (key === "message") { el.textContent = data.message || ""; el.classList.toggle("text-danger", !data.success); }
        else if (data.success && key in data) el.textContent = data[key];
        else if (!data.success) el.textContent = "—";
      });
    } catch (e) { /* xem trước không bắt buộc; server tính lại khi lưu */ }
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
      const res = await fetch(select.getAttribute("data-ll-defaults") + "?roomId=" + encodeURIComponent(select.value), { headers: XHR, credentials: "same-origin" });
      const data = await res.json();
      if (!data.success) { showAlert(data.message, "danger"); return; }
      for (const key of ["rent", "deposit", "electricity", "water"]) {
        const input = select.form.querySelector('[data-ll-fill="' + key + '"]');
        if (input && data[key] !== undefined) input.value = data[key];
      }
    } catch (e) { /* giữ nguyên giá đang nhập */ }
  }

  // ---------- Xem trước ảnh trước khi tải lên ----------
  function previewFiles(input) {
    const target = document.querySelector(input.getAttribute("data-ll-preview"));
    if (!target) return;
    target.replaceChildren();
    const maxImage = Number(input.getAttribute("data-ll-max-image-mb") || 5) * 1024 * 1024;
    const maxOther = Number(input.getAttribute("data-ll-max-other-mb") || 25) * 1024 * 1024;
    const problems = [];
    for (const file of input.files) {
      const isImage = file.type.startsWith("image/");
      if (file.size > (isImage ? maxImage : maxOther)) problems.push(file.name + " quá dung lượng cho phép.");
      if (isImage) {
        const img = document.createElement("img");
        img.alt = "Xem trước " + file.name;
        img.src = URL.createObjectURL(file);
        img.onload = () => URL.revokeObjectURL(img.src);
        target.append(img);
      } else {
        const span = document.createElement("span");
        span.className = "badge bg-light text-dark";
        span.textContent = file.name;
        target.append(span);
      }
    }
    input.setCustomValidity(problems.join(" "));
    if (problems.length) input.reportValidity();
  }

  // ---------- Danh sách dịch vụ (thêm/bớt dòng) ----------
  function renumber(container) {
    container.querySelectorAll("[data-ll-repeat-row]").forEach((row, index) => {
      row.querySelectorAll("[name],[id],[for]").forEach((el) => {
        for (const attr of ["name", "id", "for"]) {
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
      if (rows.length >= max) { showAlert("Tối đa " + max + " dịch vụ.", "warning"); return; }
      const template = container.querySelector("template");
      template.insertAdjacentHTML("beforebegin", template.innerHTML.replaceAll("__i__", String(rows.length)));
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

  window.addEventListener("pageshow", () => document.querySelectorAll("form.is-busy").forEach((f) => f.classList.remove("is-busy")));
})();
