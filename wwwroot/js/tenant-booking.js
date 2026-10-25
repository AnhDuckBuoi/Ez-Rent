(() => {
    'use strict';
    let root = document.getElementById('tenant-booking-root');
    if (!root) return;
    let draft = '';
    let request = 0;
    let controller;
    function selected() { return root.querySelector('input[name="SlotId"]:checked:not(:disabled)'); }
    function updateChoice() {
        const input = selected();
        root.querySelector('[data-selected-time]').textContent = input ? input.closest('label').querySelector('strong').textContent : 'Chưa chọn giờ đến xem';
        root.querySelector('[data-booking-review]').disabled = !input || root.getAttribute('aria-busy') === 'true';
    }
    async function loadDate(url, push = true) {
        const current = ++request;
        controller?.abort(); controller = new AbortController();
        root.setAttribute('aria-busy', 'true'); updateChoice();
        try {
            const response = await fetch(url, { signal: controller.signal });
            if (!response.ok || response.redirected) throw new Error('Chưa tải được lịch. Vui lòng thử lại hoặc tải lại trang để đăng nhập.');
            const document = new DOMParser().parseFromString(await response.text(), 'text/html');
            const next = document.getElementById('tenant-booking-root');
            if (!next || next.dataset.roomId !== root.dataset.roomId) throw new Error('Chưa tải được lịch của phòng này. Vui lòng thử lại.');
            if (current !== request) return;
            next.querySelector('#booking-note').value = draft;
            next.querySelector('[data-note-count]').textContent = draft.length;
            root.replaceWith(next); root = next;
            if (push) history.pushState(null, '', url);
            updateChoice();
            root.querySelector('.booking-day[aria-current=date]')?.focus({ preventScroll: true });
        } catch (error) {
            if (error.name !== 'AbortError' && current === request) window.TenantFeedback?.show(error instanceof TypeError ? 'Kết nối bị gián đoạn. Ghi chú của bạn vẫn được giữ; hãy thử lại.' : error.message, true);
        } finally {
            if (current === request) { root.removeAttribute('aria-busy'); updateChoice(); }
        }
    }
    document.addEventListener('click', event => {
        const date = event.target.closest('[data-booking-date]');
        if (date && !event.ctrlKey && !event.metaKey && !event.shiftKey && !event.altKey && event.button === 0) {
            event.preventDefault(); loadDate(date.href); return;
        }
        if (!event.target.closest('[data-booking-review]')) return;
        const input = selected();
        if (!input || root.getAttribute('aria-busy') === 'true') return;
        root.querySelector('[data-review-room]').textContent = root.querySelector('[data-booking-room-title]').textContent;
        root.querySelector('[data-review-time]').textContent = input.closest('label').querySelector('strong').textContent;
        root.querySelector('[data-review-note]').textContent = draft.trim() ? draft : 'Không có ghi chú.';
        root.querySelector('#booking-preview-dialog').showModal();
    });
    document.addEventListener('input', event => {
        if (event.target.id !== 'booking-note') return;
        draft = event.target.value.slice(0, 500);
        root.querySelector('[data-note-count]').textContent = draft.length;
    });
    document.addEventListener('change', event => { if (event.target.matches('input[name="SlotId"]')) updateChoice(); });
    addEventListener('popstate', () => loadDate(location.href, false));
    updateChoice();
})();
