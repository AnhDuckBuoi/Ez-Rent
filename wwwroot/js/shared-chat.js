(() => {
    'use strict';
    let root = document.getElementById('shared-chat-root');
    if (!root) return;
    let sending = false, refreshing = false, readBusy = false, timer, refreshTimer;
    let syncRequested = false, syncFailures = 0;
    const input = () => root.querySelector('#shared-chat-input');
    function status(text) { const node = root.querySelector('#shared-send-status'); if (node) node.textContent = text; }
    function queueRefresh(delay = 100) {
        clearTimeout(refreshTimer);
        if (syncRequested) refreshTimer = setTimeout(refresh, delay);
    }
    function requestRefresh() { syncRequested = true; queueRefresh(); }
    function install() {
        window.EzRentChat?.updateControls();
        if (input()) input().readOnly = sending;
        const send = root.querySelector('#shared-chat-form button');
        if (send) send.disabled = sending;
        const form = root.querySelector('#shared-read-form');
        if (form) form.hidden = true;
        scheduleRead();
    }
    async function refresh() {
        if (!syncRequested || document.visibilityState !== 'visible') return;
        if (refreshing || sending || readBusy) { queueRefresh(150); return; }
        refreshing = true; syncRequested = false;
        const request = new AbortController(), timeout = setTimeout(() => request.abort(), 12000);
        const draft = input()?.value || '', focused = document.activeElement === input(), start = input()?.selectionStart;
        const stream = root.querySelector('#shared-chat-stream');
        const scroll = stream?.scrollTop || 0, bottom = !stream || stream.scrollHeight - stream.scrollTop - stream.clientHeight < 100;
        try {
            const response = await fetch(location.href, { credentials: 'same-origin', cache: 'no-store', signal: request.signal });
            if (!response.ok || response.redirected || !response.headers.get('content-type')?.includes('text/html')) throw new Error('refresh');
            const next = new DOMParser().parseFromString(await response.text(), 'text/html').getElementById('shared-chat-root');
            if (!next) throw new Error('refresh');
            const current = input()?.value ?? draft;
            root.replaceWith(next); root = next;
            syncFailures = 0;
            root.dataset.chatSyncError = 'false';
            install();
            if (input()) { input().value = current; if (focused) { input().focus({ preventScroll: true }); input().setSelectionRange(start, start); } }
            const nextStream = root.querySelector('#shared-chat-stream');
            if (nextStream) nextStream.scrollTop = bottom ? nextStream.scrollHeight : scroll;
        } catch {
            syncRequested = true; syncFailures++;
            root.dataset.chatSyncError = 'true';
            window.EzRentChat?.updateControls();
            status('Chưa cập nhật được tin mới. Đang tự thử lại; nội dung đang soạn vẫn được giữ.');
        } finally {
            clearTimeout(timeout); refreshing = false;
            if (syncRequested) queueRefresh(syncFailures ? Math.min(15000, 1000 * 2 ** Math.min(syncFailures, 4)) : 100);
        }
    }
    function scheduleRead() { clearTimeout(timer); timer = setTimeout(markVisible, 200); }
    async function markVisible(allInPage = false) {
        const form = root.querySelector('#shared-read-form'), stream = root.querySelector('#shared-chat-stream');
        if (!form || !stream || readBusy || refreshing || (!allInPage && !form.hidden) ||
            document.visibilityState !== 'visible' || !stream.getClientRects().length) return;
        const box = stream.getBoundingClientRect();
        if (box.height <= 0 || box.width <= 0) return;
        const ids = [...stream.querySelectorAll('[data-unread=true]')].filter(node => {
            const rect = node.getBoundingClientRect();
            const height = Math.min(rect.bottom, box.bottom, innerHeight) - Math.max(rect.top, box.top, 0);
            const width = Math.min(rect.right, box.right, innerWidth) - Math.max(rect.left, box.left, 0);
            return allInPage || (height > 0 && width > 0 && height >= Math.min(rect.height * .6, box.height * .6));
        }).map(node => node.dataset.messageId);
        if (!ids.length) return;
        const data = new FormData(form); data.delete('messageIds'); ids.forEach(id => data.append('messageIds', id));
        const request = new AbortController(), timeout = setTimeout(() => request.abort(), 12000);
        readBusy = true;
        try {
            const response = await fetch(form.action, { method: 'POST', body: data, credentials: 'same-origin', headers: { 'X-Requested-With': 'fetch' }, signal: request.signal });
            if (!response.ok || response.redirected || !response.headers.get('content-type')?.includes('application/json')) throw new Error('read');
            form.hidden = true;
            ids.forEach(id => { const node = root.querySelector(`[data-message-id="${id}"]`); if (node) node.dataset.unread = 'false'; });
        } catch {
            form.hidden = false;
            status('Chưa cập nhật được trạng thái đã xem. Bạn có thể thử lại bằng nút đánh dấu đã xem.');
        } finally { clearTimeout(timeout); readBusy = false; if (syncRequested) queueRefresh(); }
    }
    document.addEventListener('submit', async event => {
        if (event.target.id === 'shared-read-form') { event.preventDefault(); markVisible(true); return; }
        if (event.target.id !== 'shared-chat-form') return;
        event.preventDefault(); if (sending || !event.target.reportValidity()) return;
        const form = event.target, data = new FormData(form), button = form.querySelector('button');
        const request = new AbortController(), timeout = setTimeout(() => request.abort(), 15000);
        sending = true; button.disabled = true; input().readOnly = true; status('Đang gửi…');
        try {
            const response = await fetch(form.action, { method: 'POST', body: data, credentials: 'same-origin', headers: { 'X-Requested-With': 'fetch' }, signal: request.signal });
            const result = response.headers.get('content-type')?.includes('application/json') ? await response.json() : null;
            if (!response.ok || response.redirected || !result?.ok) throw new Error(result?.message || 'Chưa xác nhận được kết quả gửi. Hãy tải tin mới trước khi gửi lại.');
            input().value = ''; sending = false; syncRequested = true; await refresh(); status('Đã gửi tin nhắn.'); input()?.focus({ preventScroll: true });
        } catch (error) { status(error.message || 'Lỗi kết nối. Nội dung vẫn được giữ.'); }
        finally { clearTimeout(timeout); sending = false; if (input()) input().readOnly = false; const current = root.querySelector('#shared-chat-form button'); if (current) current.disabled = false; if (syncRequested) queueRefresh(); }
    });
    document.addEventListener('click', event => {
        const link = event.target.closest('[data-chat-retry]');
        if (link && root.contains(link) && event.button === 0 && !event.ctrlKey && !event.metaKey && !event.shiftKey && !event.altKey) { event.preventDefault(); requestRefresh(); }
    });
    document.addEventListener('chat:changed', requestRefresh);
    document.addEventListener('chat:connected', requestRefresh);
    document.addEventListener('visibilitychange', () => { if (document.visibilityState === 'visible') { requestRefresh(); scheduleRead(); } });
    addEventListener('online', requestRefresh);
    addEventListener('pageshow', event => { if (event.persisted) requestRefresh(); });
    document.addEventListener('scroll', scheduleRead, true); addEventListener('resize', scheduleRead);
    const stream = root.querySelector('#shared-chat-stream'); if (stream) stream.scrollTop = stream.scrollHeight; install();
})();
