(() => {
    'use strict';
    const main = document.getElementById('main');
    const header = document.getElementById('header');
    const hour = 3600000;
    let submissionTimer;
    // A fixed demonstration of Vietnam wall time, not a server clock or a real booking source.
    const base = Date.UTC(2026, 9, 16);
    const state = { day: 0, week: 0, slot: null, note: '', clock: 7, scenario: 'normal', failedOnce: false, loadRetried: false, request: null, requests: [], sending: false, feedback: null, saved: false, lostSlots: new Set() };
    const room = { title: 'Phòng có ban công, đón nắng mỗi sáng', address: 'Ngõ 68, phố Cầu Giấy, Cầu Giấy, Hà Nội', price: '3.500.000 đ', area: '28 m²', host: 'Anh Tuấn' };
    const paths = { home: 'm3 11 9-8 9 8M5 10v11h14V10M9 21v-7h6v7', arrow: 'm9 5 7 7-7 7', back: 'm15 5-7 7 7 7', bell: 'M18 8a6 6 0 0 0-12 0c0 7-3 7-3 9h18c0-2-3-2-3-9M10 21h4', user: 'M5 21v-3a7 7 0 0 1 14 0v3M16 6a4 4 0 1 1-8 0 4 4 0 0 1 8 0', pin: 'M19 10c0 5-7 11-7 11S5 15 5 10a7 7 0 0 1 14 0M15 10a3 3 0 1 1-6 0 3 3 0 0 1 6 0', calendar: 'M4 5h16v16H4z M8 3v4m8-4v4M4 10h16', check: 'm5 12 4 4L19 6', heart: 'M20 5c-3-3-6 0-8 2-2-2-5-5-8-2-4 4 0 8 8 15 8-7 12-11 8-15' };
    const icon = name => `<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.6" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="${paths[name] || paths.home}"/></svg>`;
    const esc = value => String(value).replace(/[&<>"']/g, char => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' })[char]);
    const now = () => base + state.clock * hour;
    const start = (day, value) => base + day * 24 * hour + value * hour;
    const time = value => new Date(value).toISOString().slice(11, 16);
    const date = day => new Intl.DateTimeFormat('vi-VN', { timeZone: 'UTC', weekday: 'long', day: '2-digit', month: '2-digit', year: 'numeric' }).format(new Date(base + day * 24 * hour));
    const dayShort = day => ['CN', 'Thứ 2', 'Thứ 3', 'Thứ 4', 'Thứ 5', 'Thứ 6', 'Thứ 7'][new Date(base + day * 24 * hour).getUTCDay()];
    const route = () => {
        const query = new URL(location.href).searchParams;
        const view = query.get('view');
        const id = query.get('id') || '';
        return { view: ['room', 'book', 'appointment', 'appointments'].includes(view) ? view : 'room', id: id === 'existing' || /^demo-[1-9]\d{0,5}$/.test(id) ? id : 'new' };
    };
    const href = (view, id) => `?view=${view}${id ? `&id=${id}` : ''}`;
    const link = (view, text, className = '', id) => `<a href="${href(view, id)}" data-view="${view}" ${id ? `data-id="${id}"` : ''} class="${className}">${text}</a>`;
    function navigate(view, id) {
        history.pushState(null, '', href(view, id));
        state.feedback = null;
        render();
        scrollTo(0, 0);
        main.focus({ preventScroll: true });
    }
    function expire() {
        for (const request of state.requests) if (request.status === 'pending' && now() >= request.deadline) request.status = 'expired';
    }
    function source(day, useScenario = true) {
        const scenario = useScenario ? state.scenario : 'normal';
        if (scenario === 'load-error' && !state.loadRetried) return { kind: 'error', slots: [] };
        if (scenario === 'unopened' || day === 3 || day === 8) return { kind: 'unopened', slots: [] };
        const hours = day === 0 ? [9, 11, 13, 14, 15, 16, 17, 18] : [9, 10, 11, 13, 14, 15, 16, 17];
        const slots = hours.filter(value => start(day, value) - now() >= 6 * hour).map(value => {
            let status = 'free';
            if (scenario === 'full' || day === 2 || (day === 0 && value === 15) || (day === 1 && value === 14)) status = 'busy';
            if (day === 0 && value === 17 && state.clock < 9) status = 'held';
            if (day === 1 && value === 16) status = 'confirmed-own';
            if (state.lostSlots.has(`${day}-${value}`)) status = 'busy';
            if (state.requests.some(request => request.status === 'pending' && request.day === day && request.slot === value)) status = 'own';
            return { value, status };
        });
        return { kind: slots.some(slot => slot.status === 'free') ? 'open' : 'full', slots };
    }
    function validSelection() {
        return state.slot !== null && source(state.day).slots.some(slot => slot.value === state.slot && slot.status === 'free');
    }
    function feedback() {
        if (!state.feedback) return '';
        return `<div class="feedback${state.feedback.error ? ' error' : ''}" id="feedback" role="${state.feedback.error ? 'alert' : 'status'}" tabindex="-1"><span>${esc(state.feedback.message)}</span><button type="button" data-action="close-feedback" aria-label="Đóng thông báo">×</button></div>`;
    }
    function renderHeader(view) {
        const appointments = view === 'appointment' || view === 'appointments';
        header.innerHTML = `<a href="http://127.0.0.1:8770/Tenant/Dashboard" class="brand"><span>${icon('home')}</span><strong>EZ Rent</strong></a>
            <nav class="header-nav" aria-label="Điều hướng người thuê"><a href="http://127.0.0.1:8770/Tenant/Dashboard">Trang chủ</a><a href="http://127.0.0.1:8770/Tenant/RentalPost" ${!appointments ? 'aria-current="page"' : ''}>Tìm phòng</a>${link('appointments', 'Lịch hẹn', '', '')}<a href="http://127.0.0.1:8770/Tenant/Favorite">Đã lưu</a></nav>
            <a href="http://127.0.0.1:8770/Tenant/Notification" class="bell" aria-label="Thông báo">${icon('bell')}</a><a href="http://127.0.0.1:8770/Identity/Account/Manage" class="account" aria-label="Tài khoản của tôi">${icon('user')}</a>`;
        if (appointments) header.querySelector('[data-view="appointments"]').setAttribute('aria-current', 'page');
    }
    const host = () => `<div class="host-row"><span class="host-icon">${icon('user')}</span><div><strong>${room.host}</strong><small>Chủ nhà · Người dẫn xem phòng</small></div></div>`;
    function roomSummary(withSubmit = false) {
        const selected = validSelection();
        return `<aside class="panel sticky-summary room-summary" aria-label="Căn phòng bạn muốn xem"><img class="summary-photo" src="tenant-booking-room.svg" alt="Ảnh thay thế của phòng minh họa"><div class="room-info"><h2 class="summary-title">${room.title}</h2><p class="summary-address">${icon('pin')} ${room.address}</p><p class="room-meta">Phòng trọ · ${room.area}</p><p class="price">${room.price} <small>/ tháng</small></p>${link('room', 'Xem lại thông tin phòng', 'text-link small')}</div>${host()}
            ${withSubmit ? `<div class="summary-choice" aria-live="polite">${selected ? `<strong>${time(start(state.day, state.slot))}</strong><p class="small">${date(state.day)}</p>` : '<p class="no-choice">Chọn ngày và giờ đến xem để tiếp tục.</p>'}<button type="submit" form="booking-form" class="button primary full" ${!selected || state.sending ? 'disabled' : ''}>${state.sending ? 'Đang gửi yêu cầu…' : 'Gửi yêu cầu xem phòng'}</button><p class="summary-footnote">Chủ nhà có tối đa 2 giờ để phản hồi. Vui lòng chờ xác nhận trước khi đến xem.</p></div>` : ''}</aside>`;
    }
    function roomView() {
        return `<a href="http://127.0.0.1:8770/Tenant/RentalPost" class="back-link">${icon('back')} Quay lại Tìm phòng</a>${feedback()}
            <div class="heading"><h1>${room.title}</h1><p>${icon('pin')} ${room.address}</p></div>
            <div class="columns"><div><img src="tenant-booking-room.svg" class="room-image" alt="Ảnh thay thế của căn phòng minh họa"><p class="photo-label">Phòng minh họa cho bản thử đặt lịch</p><section class="panel detail-copy"><h2>Về căn phòng</h2><p>Không gian sáng và thoáng, phù hợp cho 1–2 người. Có khu bếp riêng, cửa sổ lớn và chỗ để xe ở tầng một.</p><ul class="amenities"><li>Điều hòa</li><li>Nóng lạnh</li><li>Chỗ để xe</li><li>Bếp riêng</li></ul><h3>Điện, nước và dịch vụ</h3><p class="small">Điện: 3.800 đ/kWh · Nước: 35.000 đ/m³ · Internet: 100.000 đ/tháng</p><p class="small muted">Xác nhận cách tính các khoản phí với chủ nhà trước khi thuê.</p></section></div>
            <aside class="panel sticky-summary"><p class="room-meta">Phòng trọ · ${room.area}</p><p class="price">${room.price} <small>/ tháng</small></p>${host()}${link('book', `${icon('calendar')} Đặt lịch xem phòng`, 'button primary full')}<p class="summary-footnote">Chọn giờ chủ nhà có thể đón bạn đến xem.</p><button type="button" data-action="save" class="button full" style="margin-top:14px">${icon('heart')} ${state.saved ? 'Đã lưu phòng' : 'Lưu phòng'}</button></aside></div>`;
    }
    function datePicker() {
        const dates = Array.from({ length: 7 }, (_, index) => state.week * 7 + index);
        return `<div class="section-head"><h2>Chọn ngày đến xem</h2><div class="week-controls"><button type="button" class="icon-button" data-action="previous-week" aria-label="7 ngày trước" ${state.week === 0 ? 'disabled' : ''}>${icon('back')}</button><button type="button" class="icon-button" data-action="next-week" aria-label="7 ngày tiếp theo" ${state.week === 1 ? 'disabled' : ''}>${icon('arrow')}</button></div></div><p class="small muted">Tháng 10, 2026 · Giờ Việt Nam</p><div class="date-strip" role="group" aria-label="Ngày đến xem phòng">${dates.map(day => {
            const kind = source(day, false).kind;
            return `<button type="button" class="date-button" data-day="${day}" data-focus="day-${day}" aria-pressed="${day === state.day}" aria-label="${date(day)}${day === 0 ? ', hôm nay' : ''}"><span>${dayShort(day)}</span><strong>${16 + day}</strong><small>${day === 0 ? 'Hôm nay' : kind === 'unopened' ? 'Chưa mở' : kind === 'full' ? 'Đã kín' : 'Có lịch'}</small></button>`;
        }).join('')}</div>`;
    }
    function slotPicker() {
        const data = source(state.day);
        const empty = (title, copy, action, label) => `<div class="empty"><h3>${title}</h3><p>${copy}</p><button type="button" class="button" data-action="${action}">${label}</button></div>`;
        let content;
        if (data.kind === 'error') content = empty('Chưa tải được lịch của chủ nhà', 'Bạn có thể thử tải lại. Nội dung ghi chú sẽ được giữ lại.', 'retry-load', 'Thử tải lại');
        else if (data.kind === 'unopened') content = empty('Chủ nhà chưa mở lịch ngày này', 'Bạn có thể chọn ngày khác đã có lịch đón khách.', 'next-day', 'Tìm ngày có lịch');
        else if (data.kind === 'full') content = empty('Không còn giờ trống phù hợp', 'Các khung còn đủ thời gian báo trước đã có người đặt hoặc đang được giữ. Hãy chọn ngày khác.', 'next-day', 'Chọn ngày khác');
        else content = `<div class="slots" role="group" aria-label="Giờ đến xem">${data.slots.map(slot => `<button type="button" class="slot" data-slot="${slot.value}" data-focus="slot-${slot.value}" aria-pressed="${state.slot === slot.value}" ${slot.status !== 'free' ? 'disabled' : ''}><strong>${time(start(state.day, slot.value))}</strong><small>${slot.status === 'busy' ? 'Đã có lịch' : slot.status === 'held' ? 'Đang được giữ' : slot.status === 'own' ? 'Bạn đang giữ' : slot.status === 'confirmed-own' ? 'Bạn đã có lịch' : state.slot === slot.value ? 'Đã chọn' : 'Có thể chọn'}</small></button>`).join('')}</div><div class="legend"><span><i></i>Có thể chọn</span><span><i class="busy"></i>Đã có lịch / Đang giữ</span></div>`;
        return `<h2 class="arrival-heading">Giờ đến xem · ${date(state.day)}</h2><p class="arrival-helper">Chỉ hiển thị các giờ cách hiện tại ít nhất 6 giờ.</p>${content}`;
    }
    function bookingView() {
        return `${link('room', `${icon('back')} Quay lại thông tin phòng`, 'back-link')}${feedback()}<div class="heading"><h1>Đặt lịch xem phòng</h1><p>Chọn ngày và giờ chủ nhà có thể đón bạn. Thời gian xem bao lâu do hai bên sắp xếp.</p></div>
            <div class="columns"><form id="booking-form" class="panel">${datePicker()}${slotPicker()}<label class="note-label" for="note">Ghi chú cho chủ nhà <span>Không bắt buộc</span></label><textarea id="note" name="note" maxlength="500" placeholder="Ví dụ: Mình đi cùng một người bạn và muốn xem chỗ để xe.">${esc(state.note)}</textarea><p class="note-count"><span id="note-count">${state.note.length}</span>/500 ký tự</p><div class="policy-note"><p>${icon('calendar')} Giờ đã chọn là giờ bạn đến, không phải thời lượng buổi xem.</p><p>Sau khi gửi, khung được giữ riêng trong tối đa 2 giờ để chủ nhà phản hồi. Chỉ đến xem khi lịch đã được xác nhận.</p></div></form>${roomSummary(true)}</div>`;
    }
    function requestView(id) {
        const existing = id === 'existing';
        const request = existing ? { day: 1, slot: 16, note: 'Mình đi cùng một người bạn.', status: 'confirmed' } : id === 'new' ? state.request : state.requests.find(item => item.id === id);
        if (!request) return `${link('appointments', `${icon('back')} Lịch hẹn của tôi`, 'back-link')}<section class="panel empty"><h1>Bạn chưa gửi yêu cầu cho phòng này</h1><p>Chọn ngày và giờ đến xem để tạo yêu cầu mới.</p>${link('book', 'Chọn lịch xem phòng', 'button primary')}</section>`;
        const expired = request.status === 'expired';
        const confirmed = request.status === 'confirmed';
        const status = confirmed ? 'Đã xác nhận' : expired ? 'Hết hạn phản hồi' : 'Chờ xác nhận';
        return `${link('appointments', `${icon('back')} Lịch hẹn của tôi`, 'back-link')}${feedback()}<div class="request-heading"><span class="request-icon">${icon(confirmed ? 'check' : 'calendar')}</span><div><h1>${confirmed ? 'Lịch xem phòng đã xác nhận' : expired ? 'Yêu cầu đã hết hạn phản hồi' : 'Đã gửi yêu cầu xem phòng'}</h1><p>${existing ? 'Yêu cầu minh họa có sẵn' : 'Yêu cầu minh họa vừa tạo'} · ${confirmed ? 'Chủ nhà đã đồng ý đón bạn.' : 'Bạn có thể theo dõi lại trong mục Lịch hẹn.'}</p></div></div>
            <div class="columns"><section class="panel"><div class="section-head"><h2>Thông tin lịch hẹn</h2><span class="status-label ${confirmed ? 'confirmed' : expired ? 'expired' : ''}">${status}</span></div><div class="request-message">${confirmed ? '<p><strong>Hẹn gặp bạn đúng giờ đã xác nhận.</strong></p><p>Hẹn bạn tại cổng. Khi đến bạn nhắn mình nhé.</p>' : expired ? '<p><strong>Chủ nhà chưa phản hồi trong 2 giờ.</strong></p><p>Khung đã được nhả. Bạn có thể chọn lịch mới; chỉ các giờ còn đủ 6 giờ báo trước mới được hiển thị.</p>' : `<p><strong>Chờ chủ nhà xác nhận trước khi đến xem.</strong></p><p>Khung của bạn được giữ đến <strong>${time(request.deadline)}, ${date(0)}</strong>. Sau thời điểm này, yêu cầu chưa được xác nhận sẽ hết hạn.</p>`}</div>
            <dl class="request-facts"><div><dt>Giờ đến xem</dt><dd><strong>${time(start(request.day, request.slot))}</strong><span>${date(request.day)} · Giờ Việt Nam</span></dd></div><div><dt>Địa chỉ</dt><dd>${room.address}</dd></div><div><dt>Người đón bạn</dt><dd>${room.host}</dd></div><div><dt>Ghi chú của bạn</dt><dd>${request.note ? esc(request.note) : 'Không có ghi chú.'}</dd></div>${!existing ? `<div><dt>Đã gửi lúc</dt><dd>${time(request.submitted)}, ${date(0)}</dd></div>` : ''}</dl><div class="actions">${link('appointments', 'Xem tất cả lịch hẹn', 'button primary')}${expired ? link('book', 'Chọn lịch mới', 'button') : link('room', 'Xem lại phòng', 'button')}</div></section>${roomSummary()}</div>`;
    }
    function appointmentCard(request, existing = false) {
        const expired = request.status === 'expired';
        const status = existing ? 'Đã xác nhận' : expired ? 'Hết hạn phản hồi' : 'Chờ xác nhận';
        return `<article class="panel appointment-item"><span class="status-label ${existing ? 'confirmed' : expired ? 'expired' : ''}">${status}</span><h2>${room.title}</h2><p class="small muted">${room.address}</p><div class="time-line">${icon('calendar')}<div><strong>${time(start(request.day, request.slot))}</strong><br><span>${date(request.day)}</span></div></div><p class="small muted">${existing ? 'Chủ nhà đã xác nhận. Hẹn bạn tại cổng.' : expired ? 'Yêu cầu hết hạn; bạn có thể chọn lịch mới.' : `Giữ khung đến ${time(request.deadline)} hôm nay. Chờ xác nhận trước khi đến.`}</p>${link('appointment', `Xem chi tiết ${icon('arrow')}`, 'button', existing ? 'existing' : request.id)}</article>`;
    }
    function appointmentsView() {
        return `<div class="heading list-intro"><div><h1>Lịch hẹn của tôi</h1><p>Theo dõi những yêu cầu bạn đã gửi và lịch được chủ nhà xác nhận.</p></div>${link('room', 'Xem phòng minh họa', 'button')}</div><div class="appointment-grid">${state.requests.slice().reverse().map(request => appointmentCard(request)).join('')}${appointmentCard({ day: 1, slot: 16, status: 'confirmed' }, true)}</div>`;
    }
    function render(focusKey) {
        expire();
        if (state.slot !== null && !validSelection()) state.slot = null;
        const { view, id } = route();
        renderHeader(view);
        main.innerHTML = view === 'room' ? roomView() : view === 'book' ? bookingView() : view === 'appointment' ? requestView(id) : appointmentsView();
        if (state.sending) main.querySelectorAll('button,textarea').forEach(control => control.disabled = true);
        document.title = `EZ Rent · ${view === 'room' ? 'Thông tin phòng' : view === 'book' ? 'Đặt lịch xem phòng' : view === 'appointments' ? 'Lịch hẹn của tôi' : 'Chi tiết lịch hẹn'}`;
        if (focusKey) main.querySelector(`[data-focus="${focusKey}"]`)?.focus({ preventScroll: true });
    }
    document.addEventListener('click', event => {
        const anchor = event.target.closest('[data-view]');
        if (anchor && !event.ctrlKey && !event.metaKey && !event.shiftKey && !event.altKey && event.button === 0) {
            event.preventDefault(); navigate(anchor.dataset.view, anchor.dataset.id); return;
        }
        const day = event.target.closest('[data-day]');
        if (day) { state.day = Number(day.dataset.day); state.slot = null; state.feedback = null; render(`day-${state.day}`); return; }
        const slot = event.target.closest('[data-slot]');
        if (slot && !slot.disabled) { state.slot = Number(slot.dataset.slot); state.feedback = null; render(`slot-${state.slot}`); document.getElementById('live-status').textContent = `Đã chọn ${time(start(state.day, state.slot))}, ${date(state.day)}.`; return; }
        const action = event.target.closest('[data-action]')?.dataset.action;
        if (action === 'previous-week' || action === 'next-week') { state.week += action === 'next-week' ? 1 : -1; state.week = Math.max(0, Math.min(1, state.week)); state.day = state.week * 7; state.slot = null; render(`day-${state.day}`); }
        if (action === 'retry-load') { state.loadRetried = true; render(); document.getElementById('live-status').textContent = 'Đã tải lại lịch.'; }
        if (action === 'next-day') { state.scenario = 'normal'; document.getElementById('scenario').value = 'normal'; const next = Array.from({ length: 14 }, (_, i) => i).find(day => day > state.day && source(day, false).kind === 'open'); if (next !== undefined) { state.day = next; state.week = Math.floor(next / 7); state.slot = null; } render(`day-${state.day}`); }
        if (action === 'close-feedback') { state.feedback = null; main.querySelector('#feedback')?.remove(); main.focus({ preventScroll: true }); }
        if (action === 'save') { state.saved = !state.saved; state.feedback = { message: state.saved ? 'Đã lưu phòng trong bản thử.' : 'Đã bỏ lưu phòng trong bản thử.' }; render(); }
    });
    main.addEventListener('input', event => {
        if (event.target.id === 'note') { state.note = event.target.value.slice(0, 500); document.getElementById('note-count').textContent = state.note.length; }
    });
    main.addEventListener('submit', event => {
        if (event.target.id !== 'booking-form') return;
        event.preventDefault();
        if (state.sending) return;
        expire();
        if (!validSelection()) { state.slot = null; state.feedback = { error: true, message: 'Giờ đã chọn không còn phù hợp. Hãy chọn lại giờ đến xem.' }; render(); main.querySelector('#feedback')?.focus(); return; }
        state.sending = true;
        render();
        submissionTimer = setTimeout(() => {
            state.sending = false;
            if ((state.scenario === 'taken' || state.scenario === 'send-error') && !state.failedOnce) {
                state.failedOnce = true;
                const taken = state.scenario === 'taken';
                if (taken) { state.lostSlots.add(`${state.day}-${state.slot}`); state.slot = null; }
                state.feedback = { error: true, message: taken ? 'Khung giờ vừa có người đặt. Ghi chú của bạn vẫn được giữ; hãy chọn một giờ khác.' : 'Chưa gửi được yêu cầu. Ghi chú và giờ đã chọn vẫn được giữ; bạn có thể thử lại.' };
                render(); main.querySelector('#feedback')?.focus(); return;
            }
            if (!validSelection()) { state.feedback = { error: true, message: 'Khung giờ không còn khả dụng. Hãy chọn lại giờ đến xem.' }; render(); return; }
            state.request = { id: `demo-${state.requests.length + 1}`, day: state.day, slot: state.slot, note: state.note, status: 'pending', submitted: now(), deadline: now() + 2 * hour };
            state.requests.push(state.request);
            state.slot = null;
            navigate('appointment', state.request.id);
        }, 450);
    });
    document.getElementById('scenario').addEventListener('change', event => { state.scenario = event.target.value; state.failedOnce = false; state.loadRetried = false; state.feedback = null; render(); });
    document.getElementById('clock').addEventListener('change', event => { state.clock = Number(event.target.value); render(); });
    document.getElementById('reset').addEventListener('click', () => { clearTimeout(submissionTimer); Object.assign(state, { day: 0, week: 0, slot: null, note: '', clock: 7, scenario: 'normal', failedOnce: false, loadRetried: false, request: null, requests: [], sending: false, feedback: null, saved: false }); state.lostSlots.clear(); document.getElementById('scenario').value = 'normal'; document.getElementById('clock').value = '7'; navigate('room'); });
    addEventListener('popstate', () => { state.feedback = null; render(); main.focus({ preventScroll: true }); });
    render();
})();
