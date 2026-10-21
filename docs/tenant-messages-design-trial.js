(() => {
    'use strict';
    const paths = { back: 'm15 5-7 7 7 7M8 12h13', heart: 'M20 5c-3-3-7-1-8 1-1-2-5-4-8-1-4 4 0 9 8 15 8-6 12-11 8-15', home: 'm3 11 9-8 9 8M5 10v11h14V10M9 21v-7h6v7', search: 'M21 21l-5-5M18 10a8 8 0 1 1-16 0 8 8 0 0 1 16 0', bell: 'M18 8a6 6 0 0 0-12 0c0 7-3 7-3 9h18c0-2-3-2-3-9M10 21h4', chevron: 'm9 5 7 7-7 7', arrow: 'M4 12h16m-6-6 6 6-6 6', external: 'M14 3h7v7m0-7L10 14M10 3H3v18h18v-7', support: 'M4 13v-2a8 8 0 0 1 16 0v2M4 12H2v6h4v-6zm16 0h2v6h-4v-6zm0 6c0 3-3 4-7 4', close: 'm6 6 12 12M6 18 18 6', info: 'M12 11v6m0-10v1M22 12a10 10 0 1 1-20 0 10 10 0 0 1 20 0', calendar: 'M4 5h16v16H4z M8 3v4m8-4v4M4 10h16', send: 'm3 11 18-8-8 18-2-8-8-2zm8 2L21 3', check: 'm5 12 4 4L19 6', doublecheck: 'm2 12 4 4L16 6m-4 9 2 2L24 7', location: 'M19 10c0 5-7 11-7 11S5 15 5 10a7 7 0 0 1 14 0M15 10a3 3 0 1 1-6 0 3 3 0 0 1 6 0', area: 'M3 3h18v18H3zM3 9h5m-5 6h5M9 3v5m6-5v5', bed: 'M3 18v3m18-3v3M3 18h18v-7H3zm2-7V5h14v6M8 8h3m3 0h3', chat: 'M3 4h18v13H9l-6 4z', cloud: 'M6 17a5 5 0 0 1-1-10 7 7 0 0 1 13-1 5 5 0 0 1 0 10M12 9v4m0 3v1' };
    const icon = name => `<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.65" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="${paths[name] || paths.chat}"/></svg>`;
    const esc = value => String(value).replace(/[&<>"']/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' })[c]);
    document.querySelectorAll('[data-icon]').forEach(node => node.innerHTML = icon(node.dataset.icon));
    const $ = selector => document.querySelector(selector);
    const workspace = $('#workspace'), chat = $('#conversation'), context = $('#room-context'), list = $('#thread-list'), supportDialog = $('#support-dialog'), supportForm = $('#support-form');
    const seeds = [
        { id: 'tuan', name: 'Anh Tuấn', initials: 'AT', color: '', unread: 0, day: 'Hôm nay, 16/10/2026', room: { title: 'Phòng có ban công, đón nắng mỗi sáng', short: 'Phòng ban công · Cầu Giấy', address: 'Ngõ 68, phố Cầu Giấy, Hà Nội', price: '3.500.000 đ', area: '28 m²', type: 'Phòng trọ' }, visit: true, messages: [
            { id: 'a1', text: 'Chào Minh Anh, phòng hiện vẫn còn nhé. Bạn muốn hỏi thêm thông tin gì?', time: '09:10' },
            { id: 'a2', self: true, text: 'Em chào anh. Phòng có ban công riêng như trong ảnh đúng không ạ? Em muốn xem phòng vào cuối tuần này.', time: '09:12' },
            { id: 'a3', text: 'Đúng rồi bạn, ban công riêng nhé. Chiều Chủ Nhật mình có thể đón bạn lúc 14:00.', time: '09:14' },
            { id: 'a4', self: true, text: 'Dạ được ạ. Anh cho em hỏi thêm về tiền điện, nước và gửi xe nhé.', time: '09:16' },
            { id: 'a5', text: 'Điện 3.800 đ/kWh, nước 35.000 đ/m³. Gửi xe 100.000 đ/tháng. Bạn cứ nhắn mình khi đến cổng nhé.', time: '09:18' }
        ] },
        { id: 'ha', name: 'Chị Hà', initials: 'CH', color: 'sand', unread: 1, day: 'Hôm nay, 16/10/2026', room: { title: 'Studio yên tĩnh, có khu bếp riêng', short: 'Studio · Thanh Xuân', address: 'Ngõ 21, Thanh Xuân, Hà Nội', price: '4.200.000 đ', area: '32 m²', type: 'Studio' }, visit: false, messages: [
            { id: 'b1', self: true, text: 'Em chào chị, studio này có thể ở hai người được không ạ?', time: '10:30' },
            { id: 'b2', text: 'Được em nhé, phòng phù hợp cho hai người. Có khu bếp và nhà vệ sinh riêng.', time: '10:35' },
            { id: 'b3', text: 'Chị gửi em thêm thông tin: phí dịch vụ 150.000 đ/người/tháng, đã gồm internet và vệ sinh khu chung.', time: '10:40' }
        ] },
        { id: 'minh', name: 'Anh Minh', initials: 'AM', color: 'sage', unread: 1, day: 'Hôm qua, 15/10/2026', room: { title: 'Phòng có gác, thuận tiện đi lại', short: 'Phòng có gác · Bình Thạnh', address: 'Khu dân cư, Bình Thạnh, Hồ Chí Minh', price: '2.800.000 đ', area: '24 m²', type: 'Phòng trọ' }, visit: false, messages: [
            { id: 'c1', self: true, text: 'Em chào anh, phòng có gác này còn trống không ạ?', time: '16:02' },
            { id: 'c2', text: 'Phòng vẫn còn nhé. Bạn muốn xem vào ngày nào để mình sắp xếp?', time: '16:10' }
        ] }
    ];
    const state = { threads: structuredClone(seeds), selected: 'tuan', filter: 'all', mode: 'normal', query: '', failedOnce: false, drafts: new Map(), receipts: [], nextMessage: 1 };
    let dialogOpener = null, formCompleted = false, contextOpener = null;
    const phone = matchMedia('(max-width: 767px), (max-width: 1023px) and (max-height: 600px) and (pointer: coarse)'), compact = matchMedia('(max-width: 1240px)');
    const contextDialog = $('#context-dialog');
    state.view = 'inbox';
    // One room pane, inline on desktop and inside a native modal on smaller screens.
    function adaptLayout() {
        if (compact.matches && context.parentElement !== contextDialog) contextDialog.append(context);
        if (!compact.matches && context.parentElement !== workspace) {
            if (contextDialog.open) contextDialog.close();
            workspace.append(context);
        }
        document.body.classList.toggle('mobile-chat', phone.matches && state.view === 'chat');
        document.body.dataset.view = state.view;
        const lowViewport = phone.matches && (visualViewport?.height || innerHeight) < 560;
        const searching = lowViewport && state.view === 'inbox' && (document.activeElement.id === 'inbox-search' || document.body.classList.contains('mobile-search'));
        document.body.classList.toggle('mobile-search', searching);
        document.body.classList.toggle('compact-height', phone.matches && (visualViewport?.height || innerHeight) < 560);
        const hint = $('#keyboard-hint');
        if (hint) hint.textContent = phone.matches ? 'Xuống dòng bằng Enter · Bấm mũi tên để gửi' : 'Enter để gửi · Shift + Enter để xuống dòng';
        $('.skip-link').href = phone.matches && state.view === 'inbox' ? '#inbox-title' : '#conversation';
        if (phone.matches) document.documentElement.style.setProperty('--app-height', `${visualViewport?.height || innerHeight}px`);
        else document.documentElement.style.removeProperty('--app-height');
        updateComposer();
    }
    function openContext(opener) {
        contextOpener = opener;
        if (compact.matches && !contextDialog.open) contextDialog.showModal();
        $('.context-toggle')?.setAttribute('aria-expanded', String(contextDialog.open));
        if (contextDialog.open) context.querySelector('.close-context')?.focus({ preventScroll: true });
    }
    function closeContext() { if (contextDialog.open) contextDialog.close(); }
    function showInbox() {
        state.view = 'inbox'; syncUrl(); closeContext(); adaptLayout();
        (list.querySelector(`[data-thread="${state.selected}"]`) || $('#inbox-search')).focus({ preventScroll: true });
    }
    contextDialog.addEventListener('close', () => {
        $('.context-toggle')?.setAttribute('aria-expanded', 'false');
        const target = contextOpener?.isConnected && contextOpener.getClientRects().length ? contextOpener : $('.context-toggle');
        if (target?.getClientRects().length) target.focus({ preventScroll: true });
    });
    phone.addEventListener('change', adaptLayout);
    compact.addEventListener('change', adaptLayout);
    visualViewport?.addEventListener('resize', adaptLayout);
    document.addEventListener('focusin', adaptLayout);
    document.addEventListener('focusout', () => requestAnimationFrame(adaptLayout));
    addEventListener('resize', adaptLayout);
    document.addEventListener('focusin', adaptLayout);
    document.addEventListener('focusout', () => requestAnimationFrame(adaptLayout));
    const current = () => state.threads.find(thread => thread.id === state.selected);
    const normal = () => state.mode === 'normal' || state.mode === 'send-error';
    const avatar = thread => `<span class="avatar ${thread.color}" aria-hidden="true">${thread.initials}</span>`;
    const normalize = value => value.toLocaleLowerCase('vi').normalize('NFD').replace(/[\u0300-\u036f]/g, '').replaceAll('đ', 'd');
    function readUrl() {
        const query = new URL(location.href).searchParams;
        const validChat = state.threads.some(thread => thread.id === query.get('chat'));
        state.selected = validChat ? query.get('chat') : state.selected;
        state.view = query.get('view') === 'inbox' || !validChat ? 'inbox' : 'chat';
        state.filter = query.get('filter') === 'unread' ? 'unread' : 'all';
        state.mode = ['normal', 'send-error', 'empty', 'loading', 'load-error'].includes(query.get('mode')) ? query.get('mode') : 'normal';
        $('#demo-mode').value = state.mode;
        if (state.view === 'chat' && normal()) current().unread = 0;
    }
    function syncUrl() {
        const url = new URL(location.href);
        url.searchParams.set('chat', state.selected);
        state.view === 'inbox' ? url.searchParams.set('view', 'inbox') : url.searchParams.delete('view');
        state.filter === 'all' ? url.searchParams.delete('filter') : url.searchParams.set('filter', state.filter);
        state.mode === 'normal' ? url.searchParams.delete('mode') : url.searchParams.set('mode', state.mode);
        history.pushState(null, '', url);
    }
    function announce(text) { $('#live-status').textContent = text; }
    function renderList() {
        const unread = normal() ? state.threads.filter(thread => thread.unread).length : 0;
        $('#unread-count').textContent = unread; $('#unread-count').hidden = unread === 0;
        $('#unread-count').setAttribute('aria-label', `${unread} cuộc trò chuyện chưa đọc`);
        $('#filter-unread-count').textContent = unread;
        document.querySelectorAll('[data-filter]').forEach(button => button.setAttribute('aria-pressed', String(button.dataset.filter === state.filter)));
        $('#clear-search').hidden = !state.query;
        if (state.mode === 'loading') { list.innerHTML = '<div class="skeleton"></div><div class="skeleton"></div><div class="skeleton"></div>'; return; }
        if (!normal()) { list.innerHTML = `<div class="list-empty"><strong>${state.mode === 'empty' ? 'Chưa có cuộc trò chuyện' : 'Danh sách chưa tải được'}</strong><span>${state.mode === 'empty' ? 'Cuộc trò chuyện của bạn sẽ xuất hiện ở đây.' : 'Thử tải lại để tiếp tục trao đổi.'}</span>${state.mode === 'load-error' ? '<button type="button" data-action="retry-load">Thử tải lại</button>' : ''}${state.mode === 'empty' ? '<a class="button" href="http://127.0.0.1:8770/Tenant/RentalPost">Tìm phòng</a>' : ''}</div>`; return; }
        const threads = state.threads.filter(thread => (state.filter !== 'unread' || thread.unread) && normalize(`${thread.name} ${thread.room.title} ${thread.room.short} ${thread.room.address}`).includes(normalize(state.query.trim())));
        list.innerHTML = threads.length ? threads.map(thread => {
            const last = thread.messages.at(-1);
            return `<button type="button" class="thread ${thread.unread ? 'unread' : ''}" data-thread="${thread.id}" aria-current="${state.selected === thread.id}" aria-label="${thread.name}, ${esc(thread.room.short)}${thread.unread ? ', có tin chưa đọc' : ''}">${avatar(thread)}<span class="thread-copy"><span class="thread-title"><strong>${thread.name}</strong><time>${last.time}</time></span><span class="thread-room">${thread.room.short}</span><span class="thread-last"><span>${last.failed ? 'Chưa gửi: ' : last.self ? 'Bạn: ' : ''}${esc(last.text)}</span>${thread.unread ? '<span class="thread-unread" aria-hidden="true">1</span>' : ''}</span></span></button>`;
        }).join('') : `<div class="list-empty"><strong>${state.query ? 'Chưa tìm thấy cuộc trò chuyện' : 'Bạn đã đọc hết tin nhắn'}</strong><span>${state.query ? 'Thử tìm bằng tên chủ nhà hoặc tên phòng.' : 'Các cuộc trò chuyện đã đọc vẫn có trong Tất cả.'}</span><button type="button" data-action="clear-filters">${state.query ? 'Xóa tìm kiếm' : 'Xem tất cả'}</button></div>`;
    }
    function renderChat(focusComposer = false) {
        if (!normal()) {
            const loading = state.mode === 'loading';
            chat.innerHTML = loading ? '<div class="chat-header"><h2>Đang tải cuộc trò chuyện…</h2></div><div class="chat-skeleton" aria-busy="true"><div class="skeleton-line"></div><div class="skeleton-line short"></div><div class="skeleton-bubble"></div><div class="skeleton-bubble self"></div><div class="skeleton-bubble"></div></div>' : `<div class="state-panel">${icon(state.mode === 'empty' ? 'chat' : 'cloud')}<h2>${state.mode === 'empty' ? 'Một cuộc trò chuyện, gần hơn với nơi ở mới' : 'Chưa tải được tin nhắn'}</h2><p>${state.mode === 'empty' ? 'Chọn một căn phòng bạn quan tâm để bắt đầu trao đổi với chủ nhà.' : 'Kết nối có thể đang gián đoạn. Hãy thử tải lại để xem cuộc trò chuyện.'}</p>${state.mode === 'empty' ? '<a href="http://127.0.0.1:8770/Tenant/RentalPost" class="button primary">Tìm phòng</a>' : '<button type="button" class="button primary" data-action="retry-load">Thử tải lại</button>'}</div>`;
            chat.insertAdjacentHTML('afterbegin', `<button type="button" class="mobile-back state-back" data-action="back-inbox">${icon('back')} Trò chuyện</button>`);
            return;
        }
        const thread = current();
        chat.innerHTML = `<div class="chat-header"><button type="button" class="icon-button mobile-back" data-action="back-inbox" aria-label="Về danh sách trò chuyện">${icon('back')}</button>${avatar(thread)}<div class="chat-person"><h2 id="chat-person-name" tabindex="-1">${thread.name}</h2><p>Chủ nhà · ${thread.room.short}</p></div><button type="button" class="icon-button context-toggle" data-action="show-context" aria-expanded="false" aria-controls="context-dialog" aria-haspopup="dialog" aria-label="Thông tin phòng">${icon('info')}</button></div>
            <button type="button" class="room-link" data-action="show-context" aria-haspopup="dialog" aria-controls="context-dialog"><img src="tenant-v4-assets/living.jpg" width="44" height="44" alt=""><span><strong>${thread.room.short}</strong><small>${thread.room.price} / tháng · ${thread.room.area}</small></span>${icon('chevron')}</button>
            ${thread.visit ? `<div class="visit-strip">${icon('calendar')}<span>Lịch xem phòng <strong>14:00, Chủ Nhật 18/10</strong></span><a href="#room-visit" data-action="show-visit">Xem lại</a></div>` : ''}
            <div class="message-stream" id="message-stream" role="log" aria-live="off" aria-label="Tin nhắn với ${thread.name}"><div class="date-divider">${thread.day}</div>${thread.messages.map((message, index) => `${message.demo && thread.day.startsWith('Hôm qua') && !thread.messages.slice(0, index).some(previous => previous.demo) ? '<div class="date-divider">Hôm nay, 16/10/2026</div>' : ''}<div class="message ${message.self ? 'self' : ''} ${message.failed ? 'failed' : ''}" data-message-id="${message.id}"><p class="bubble">${esc(message.text)}</p><span class="message-meta"><time>${message.time}</time>${message.self ? `${icon(message.failed ? 'info' : 'check')}<span>${message.failed ? 'Chưa gửi được' : message.demo ? 'Đã gửi trong bản thử' : 'Đã gửi'}</span>` : ''}${message.failed ? `<button type="button" class="retry-message" data-retry="${message.id}">Thử lại</button>` : ''}</span></div>`).join('')}</div>
            <form class="composer" id="message-form"><div class="quick-replies" aria-label="Gợi ý nội dung"><button type="button" data-quick="fees">Hỏi về phí dịch vụ</button><button type="button" data-quick="visit">${thread.visit ? 'Xác nhận giờ đến' : 'Hỏi lịch xem phòng'}</button></div><label for="message-input" class="composer-label">Tin nhắn của bạn</label><div class="compose-box"><textarea id="message-input" name="message" rows="1" maxlength="2000" placeholder="Viết cho ${thread.name.toLocaleLowerCase('vi')}…" autocomplete="off"></textarea><button type="submit" class="send-button" aria-label="Gửi tin nhắn mẫu" disabled>${icon('send')}</button></div><div class="composer-bottom"><span id="keyboard-hint">Enter để gửi · Shift + Enter để xuống dòng</span><span id="message-char-count">0/2.000</span></div></form>`;
        $('#message-input').value = state.drafts.get(thread.id) || '';
        updateComposer();
        $('#message-stream').scrollTop = $('#message-stream').scrollHeight;
        if (focusComposer) $('#message-input').focus({ preventScroll: true });
    }
    function renderContext() {
        if (!normal()) { context.innerHTML = '<div class="context-heading"><h2>Thông tin phòng</h2></div><p class="context-footnote">Thông tin liên quan sẽ xuất hiện khi bạn mở một cuộc trò chuyện.</p>'; return; }
        const thread = current(), room = thread.room;
        context.innerHTML = `<div class="context-heading"><h2 id="context-title">Phòng đang trao đổi</h2><button class="icon-button close-context" type="button" data-action="close-context" aria-label="Đóng thông tin phòng">${icon('close')}</button></div><figure class="room-photo"><img src="tenant-v4-assets/living.jpg" width="256" height="150" alt="Ảnh nội thất minh họa cho căn phòng trong bản thử"><figcaption>Ảnh minh họa</figcaption></figure><h3>${room.title}</h3><p class="room-location">${icon('location')}<span>${room.address}</span></p><p class="room-price">${room.price}<small> / tháng</small></p><div class="room-facts"><span>${icon('area')}${room.area}</span><span>${icon('bed')}${room.type}</span></div><details class="room-more"><summary>Thông tin căn phòng ${icon('chevron')}</summary><p>Phòng có cửa sổ thoáng, khu vệ sinh riêng và chỗ để xe. Thông tin và ảnh chỉ minh họa cho bản thử thiết kế.</p></details>
            <section class="context-visit" id="room-visit"><h3>Lịch xem phòng</h3>${thread.visit ? `<span class="visit-status">${icon('check')} Đã xác nhận</span><div class="visit-date"><div class="calendar-tile"><strong>18</strong><span>Tháng 10</span></div><div><strong>14:00</strong><span>Chủ Nhật, 18/10/2026</span></div></div><details class="visit-detail"><summary>Chi tiết lịch hẹn ${icon('chevron')}</summary><p>Chủ nhà sẽ đón bạn tại cổng. Lịch này là dữ liệu minh họa; gửi tin nhắn không làm thay đổi giờ hẹn.</p></details>` : '<p class="context-footnote" style="border:0;padding:0;margin:0">Cuộc trò chuyện này chưa có lịch xem phòng được xác nhận.</p>'}</section>
            ${state.receipts.length ? `<section class="support-receipts"><h3>Yêu cầu hỗ trợ mẫu</h3>${state.receipts.map(receipt => `<button type="button" class="request-row" data-request="${receipt.id}"><strong>${esc(receipt.subject)}</strong><span>${receipt.id} · Chưa gửi thật</span></button>`).join('')}</section>` : ''}<button type="button" class="button context-support" data-action="open-support">${icon('support')} Hỗ trợ &amp; phản ánh</button><p class="context-footnote">Thông tin căn phòng luôn đi cùng cuộc trò chuyện, để bạn dễ đối chiếu khi trao đổi.</p>`;
    }
    function renderAll() { adaptLayout(); renderList(); renderChat(); renderContext(); adaptLayout(); }
    function updateComposer() {
        const input = $('#message-input'); if (!input) return;
        $('.send-button').disabled = !input.value.trim();
        $('#message-char-count').textContent = `${input.value.length.toLocaleString('vi-VN')}/2.000`;
        input.style.height = 'auto'; input.style.height = `${Math.min(130, Math.max(40, input.scrollHeight))}px`;
    }
    function sendMessage() {
        const input = $('#message-input');
        if (!input || !input.value.trim() || !normal()) return;
        const thread = current();
        const failed = state.mode === 'send-error' && !state.failedOnce;
        if (failed) state.failedOnce = true;
        const number = state.nextMessage++;
        thread.messages.push({ id: `demo-${number}`, text: input.value.trim().slice(0, 2000), self: true, time: `11:${String(10 + number % 50).padStart(2, '0')}`, demo: true, failed });
        state.drafts.set(thread.id, '');
        renderList(); renderChat(true); adaptLayout();
        announce(failed ? 'Chưa gửi được tin nhắn mẫu. Bạn có thể chọn Thử lại.' : 'Tin nhắn đã được thêm trong bản thử, không gửi đến chủ nhà thật.');
    }
    function setMode(mode) {
        state.mode = mode; state.failedOnce = false; $('#demo-mode').value = mode; syncUrl();
        closeContext(); renderAll();
    }
    function openSupport(opener) {
        dialogOpener = opener;
        if (formCompleted) { supportForm.reset(); formCompleted = false; clearErrors(); }
        $('#support-form').hidden = false; $('#support-result').hidden = true;
        const selectedRoom = $('#support-room').value;
        $('#support-room').innerHTML = state.threads.map(thread => `<option value="${thread.id}">${thread.room.short}</option>`).join('') + '<option value="none">Không liên quan đến phòng</option>';
        $('#support-room').value = selectedRoom || state.selected;
        $('#support-char-count').textContent = `${$('#support-description').value.length.toLocaleString('vi-VN')}/1.000`;
        if (!supportDialog.open) supportDialog.showModal(); else $('#support-category').focus();
    }
    function clearErrors() {
        ['category', 'subject', 'description'].forEach(key => { $(`#support-${key}`).removeAttribute('aria-invalid'); $(`#support-${key}-error`).textContent = ''; });
    }
    function clearCorrectedField(event) {
        const field = event.target;
        if (!['support-category', 'support-subject', 'support-description'].includes(field.id)) return;
        const limit = field.id === 'support-subject' ? 100 : 1000;
        if (field.value.trim() && field.value.length <= limit) {
            field.removeAttribute('aria-invalid'); $(`#${field.id}-error`).textContent = '';
        }
    }
    supportForm.addEventListener('input', clearCorrectedField);
    supportForm.addEventListener('change', clearCorrectedField);
    function showReceipt(receipt) {
        $('#support-form').hidden = true; $('#support-result').hidden = false;
        $('#support-result').className = 'support-result';
        $('#support-result').innerHTML = `<div class="result-check">${icon('check')}</div><h3>Đã tạo yêu cầu trong bản thử</h3><p>Nội dung được giữ để bạn xem lại trong tab này. Bộ phận hỗ trợ chưa nhận được yêu cầu thật.</p><dl><div><dt>Mã minh họa</dt><dd>${receipt.id}</dd></div><div><dt>Vấn đề</dt><dd>${esc(receipt.category)}</dd></div><div><dt>Phòng liên quan</dt><dd>${esc(receipt.room)}</dd></div><div><dt>Tiêu đề</dt><dd>${esc(receipt.subject)}</dd></div><div><dt>Mô tả</dt><dd>${esc(receipt.description)}</dd></div></dl><div class="dialog-actions"><button type="button" class="button" data-action="new-support">Tạo yêu cầu khác</button><button type="button" class="button primary" data-action="close-support">Đóng</button></div>`;
    }
    document.addEventListener('click', event => {
        const threadButton = event.target.closest('[data-thread]');
        if (threadButton) { state.selected = threadButton.dataset.thread; state.view = 'chat'; current().unread = 0; syncUrl(); renderAll(); $('#chat-person-name')?.focus({ preventScroll: true }); announce(`Đã mở cuộc trò chuyện với ${current().name}.`); return; }
        const filter = event.target.closest('[data-filter]');
        if (filter) { state.filter = filter.dataset.filter; syncUrl(); renderList(); return; }
        const quick = event.target.closest('[data-quick]');
        if (quick && $('#message-input')) { const suggestion = quick.dataset.quick === 'fees' ? 'Anh/chị cho mình hỏi các khoản phí dịch vụ của phòng được tính như thế nào ạ?' : current().visit ? 'Mình xác nhận sẽ đến xem phòng lúc 14:00 Chủ Nhật nhé. Cảm ơn anh!' : 'Mình muốn đặt lịch xem phòng. Anh/chị có thể đón mình vào những giờ nào ạ?'; const input = $('#message-input'); input.value = `${input.value.trim() ? `${input.value.trim()}\n` : ''}${suggestion}`.slice(0, 2000); state.drafts.set(state.selected, input.value); updateComposer(); input.focus(); return; }
        const retry = event.target.closest('[data-retry]');
        if (retry) { const message = current().messages.find(item => item.id === retry.dataset.retry); if (message) { message.failed = false; renderList(); renderChat(true); adaptLayout(); announce('Đã thử gửi lại tin nhắn trong bản thử.'); } return; }
        const request = event.target.closest('[data-request]');
        if (request) { const receipt = state.receipts.find(item => item.id === request.dataset.request); if (receipt) { dialogOpener = request; showReceipt(receipt); supportDialog.showModal(); } return; }
        const action = event.target.closest('[data-action]')?.dataset.action;
        if (action === 'exit-search') { document.body.classList.remove('mobile-search'); $('#inbox-title').focus({ preventScroll: true }); adaptLayout(); }
        if (action === 'back-inbox') showInbox();
        if (action === 'open-support') openSupport(event.target.closest('button'));
        if (action === 'close-support') supportDialog.close();
        if (action === 'new-support') { formCompleted = true; openSupport($('#open-support')); }
        if (action === 'retry-load') { setMode('normal'); announce('Đã tải lại dữ liệu minh họa.'); }
        if (action === 'clear-filters') { state.query = ''; state.filter = 'all'; $('#inbox-search').value = ''; syncUrl(); renderList(); $('#inbox-search').focus(); }
        if (action === 'show-context') openContext(event.target.closest('button'));
        if (action === 'close-context') closeContext();
        if (action === 'show-visit') {
            event.preventDefault(); openContext(event.target.closest('a'));
            const detail = context.querySelector('.visit-detail');
            if (detail) { detail.open = true; detail.querySelector('summary').focus({ preventScroll: true }); detail.scrollIntoView({ block: 'nearest' }); }
        }
    });
    $('#inbox-search').addEventListener('input', event => { state.query = event.target.value; renderList(); });
    $('#clear-search').addEventListener('click', () => { state.query = ''; $('#inbox-search').value = ''; syncUrl(); renderList(); $('#inbox-search').focus(); });
    $('#open-support').addEventListener('click', event => openSupport(event.currentTarget));
    $('#toggle-demo-controls').addEventListener('click', event => { const open = $('.demo-bar').classList.toggle('tools-open'); event.currentTarget.setAttribute('aria-expanded', String(open)); });
    $('#demo-mode').addEventListener('change', event => setMode(event.target.value));
    $('#reset-demo').addEventListener('click', () => { supportDialog.close(); state.threads = structuredClone(seeds); state.selected = 'tuan'; state.view = 'inbox'; state.filter = 'all'; state.query = ''; state.drafts.clear(); state.receipts = []; state.nextMessage = 1; $('#inbox-search').value = ''; supportForm.reset(); formCompleted = false; clearErrors(); setMode('normal'); announce('Đã khôi phục bản thử ban đầu.'); });
    document.addEventListener('input', event => {
        if (event.target.id === 'message-input') { state.drafts.set(state.selected, event.target.value); updateComposer(); }
        if (event.target.id === 'support-description') $('#support-char-count').textContent = `${event.target.value.length.toLocaleString('vi-VN')}/1.000`;
    });
    document.addEventListener('keydown', event => {
        if (!phone.matches && event.target.id === 'message-input' && event.key === 'Enter' && !event.shiftKey && !event.ctrlKey && !event.metaKey && !event.altKey && !event.isComposing && event.keyCode !== 229) { event.preventDefault(); if (!event.repeat) sendMessage(); }
    });
    document.addEventListener('submit', event => { if (event.target.id === 'message-form') { event.preventDefault(); sendMessage(); } });
    supportForm.addEventListener('submit', event => {
        event.preventDefault(); clearErrors(); let firstInvalid = null;
        for (const key of ['category', 'subject', 'description']) {
            const field = $(`#support-${key}`), value = field.value.trim();
            if (!value || (key === 'subject' && value.length > 100) || (key === 'description' && value.length > 1000)) {
                field.setAttribute('aria-invalid', 'true'); $(`#support-${key}-error`).textContent = key === 'category' ? 'Chọn vấn đề bạn cần hỗ trợ.' : key === 'subject' ? 'Nhập tiêu đề từ 1 đến 100 ký tự.' : 'Nhập mô tả từ 1 đến 1.000 ký tự.'; firstInvalid ||= field;
            }
        }
        if (firstInvalid) { firstInvalid.focus(); announce('Vui lòng kiểm tra các mục được đánh dấu trong yêu cầu hỗ trợ.'); return; }
        const receipt = { id: `HT-${String(state.receipts.length + 1).padStart(3, '0')}`, category: $('#support-category').value, room: $('#support-room').selectedOptions[0].textContent, subject: $('#support-subject').value.trim(), description: $('#support-description').value.trim() };
        state.receipts.push(receipt); formCompleted = true; showReceipt(receipt); renderContext(); $('#support-result h3').setAttribute('tabindex', '-1'); $('#support-result h3').focus({ preventScroll: true }); announce('Đã tạo yêu cầu minh họa. Chưa gửi đến bộ phận hỗ trợ thật.');
    });
    supportDialog.addEventListener('close', () => { const target = dialogOpener?.isConnected && dialogOpener.getClientRects().length ? dialogOpener : contextDialog.open ? context.querySelector('.context-support') : phone.matches && state.view === 'chat' ? $('.context-toggle') : $('#open-support'); target?.focus({ preventScroll: true }); });
    addEventListener('popstate', () => { readUrl(); closeContext(); renderAll(); });
    addEventListener('beforeunload', event => {
        const hasDraft = [...state.drafts.values()].some(value => value.trim()) || (!formCompleted && ($('#support-subject').value.trim() || $('#support-description').value.trim()));
        if (hasDraft) { event.preventDefault(); event.returnValue = ''; }
    });
    readUrl(); renderAll();
    document.fonts.ready.then(() => { const stream = $('#message-stream'); if (stream) stream.scrollTop = stream.scrollHeight; });
})();
