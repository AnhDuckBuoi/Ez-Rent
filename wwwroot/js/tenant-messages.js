(() => {
    'use strict';
    let root = document.getElementById('messages-root');
    if (!root) return;
    const $ = selector => root.querySelector(selector);
    const phone = matchMedia('(max-width:767px), (max-width:1023px) and (max-height:600px) and (pointer:coarse)');
    const compact = matchMedia('(max-width:1240px)');
    const drafts = new Map();
    const sending = new Set();
    const draftKey = () => root.dataset.draftKey;
    let readTimer, readBusy = false, scanScheduled = false;
    const visibleIncoming = new Set();
    let contextOpener, pending;
    const announce = text => { document.getElementById('messages-live').textContent = text; };
    const visible = node => node?.isConnected && node.getClientRects().length;
    function keepDrafts() {
        const input = $('#message-input');
        if (input) drafts.set(draftKey(), input.value);

    }
    function sizeInput() {
        const input = $('#message-input');
        if (!input) return;
        input.style.height = 'auto';
        input.style.height = `${Math.min(110, Math.max(40, input.scrollHeight))}px`;
        $('#message-char-count').textContent = `${input.value.length.toLocaleString('vi-VN')}/2.000`;
        const busy = sending.has(draftKey());
        input.readOnly = busy;
        $('#message-send-form')?.setAttribute('aria-busy', String(busy));
        const send = $('#message-send-form .send-button');
        if (send) { send.disabled = busy || !input.value.trim(); send.setAttribute('aria-label', busy ? 'Đang gửi tin nhắn' : 'Gửi tin nhắn'); }
        root.querySelectorAll('[data-quick]').forEach(button => button.disabled = busy);
    }
    function arrange() {
        const room = $('#room-context'), dialog = $('#context-dialog'), workspace = $('#workspace');
        if (room && dialog && workspace) {
            if (compact.matches && room.parentElement !== dialog) dialog.append(room);
            if (!compact.matches && room.parentElement !== workspace) { if (dialog.open) dialog.close(); workspace.append(room); }
        }
        const chat = phone.matches && root.dataset.mobileChat === 'true';
        const low = phone.matches && (window.visualViewport?.height || innerHeight) < 560;
        const searching = low && !chat && (document.activeElement.id === 'inbox-search' || document.body.classList.contains('mobile-search'));
        document.body.classList.toggle('mobile-chat', chat);
        document.body.classList.toggle('compact-height', low);
        document.body.classList.toggle('mobile-search', searching);
        if (phone.matches) document.documentElement.style.setProperty('--app-height', `${window.visualViewport?.height || innerHeight}px`);
        else document.documentElement.style.removeProperty('--app-height');
        sizeInput();
        queueReadScan();
    }
    function install() {
        const connectionStatus = root.querySelector('[data-chat-connection]');
        if (connectionStatus && window.EzRentChatStatus) connectionStatus.textContent = window.EzRentChatStatus;
        const input = $('#message-input');
        if (input && drafts.has(draftKey())) input.value = drafts.get(draftKey());
        $('#context-dialog')?.addEventListener('close', () => {
            $('.context-toggle')?.setAttribute('aria-expanded','false');
            if (visible(contextOpener)) contextOpener.focus({preventScroll:true});
        });
        arrange();
        const stream = $('#message-stream');
        if (stream) stream.scrollTop = stream.scrollHeight;
        observeIncoming();
    }
    async function navigate(url, push = true, quiet = false) {
        keepDrafts();
        pending?.abort();
        const request = new AbortController(); pending = request;
        $('#workspace')?.setAttribute('aria-busy','true'); if (!quiet) announce('Đang tải hộp thư…');
        const focused = document.activeElement?.id === 'message-input', selection = $('#message-input')?.selectionStart;
        const oldStream = $('#message-stream'), oldScroll = oldStream?.scrollTop || 0;
        const atBottom = !oldStream || oldStream.scrollHeight - oldStream.scrollTop - oldStream.clientHeight < 100;
        try {
            const response = await fetch(url, {signal:request.signal,headers:{'X-Requested-With':'XMLHttpRequest'},credentials:'same-origin'});
            if (response.redirected || !response.headers.get('content-type')?.includes('text/html')) throw new Error('response');
            const doc = new DOMParser().parseFromString(await response.text(),'text/html');
            const next = doc.getElementById('messages-root');
            if (!next) throw new Error('response');
            // Capture typing that occurred while the read request was in flight.
            keepDrafts();
            root.querySelectorAll('dialog[open]').forEach(dialog => dialog.close());
            if (push) history.pushState(null,'',url);
            const currentFocus = document.activeElement?.id === 'message-input', currentSelection = $('#message-input')?.selectionStart;
            root.replaceWith(next); root = next; install();
            const target = phone.matches && root.dataset.mobileChat !== 'true' ? $('#inbox-title') : $('#chat-person-name') || root.querySelector('h2');
            if (quiet) { const stream = $("#message-stream"); if (stream && !atBottom) stream.scrollTop = oldScroll; if (currentFocus && $("#message-input")) { $("#message-input").focus({preventScroll:true}); $("#message-input").setSelectionRange(currentSelection,currentSelection); } }
            if (target && !quiet) { target.setAttribute('tabindex','-1'); target.focus({preventScroll:true}); }
            if (!quiet) announce(response.ok ? 'Đã tải nội dung hộp thư.' : 'Không mở được nội dung. Xem hướng dẫn trong hộp thư.');
            return true;
        } catch (error) {
            if (error.name !== 'AbortError') {
                window.TenantFeedback?.show('Chưa tải được hộp thư. Hãy thử lại; bản nháp vẫn được giữ trong tab này.',true);
                announce('Chưa tải được hộp thư. Hãy thử lại.');
            }
        } finally { if (pending === request) { pending = null; $('#workspace')?.removeAttribute('aria-busy'); } }
        return false;
    }
    function sendStatus(message, error = false) {
        const status = $('#message-send-status');
        if (status) { status.textContent = message; status.classList.toggle('is-error', error); }
        $('#message-input')?.setAttribute('aria-invalid', String(error));
        announce(message);
    }
    async function sendMessage(form) {
        const key = draftKey(), input = $('#message-input');
        if (!input || sending.has(key)) return;
        if (!input.value.trim()) { sendStatus('Nhập nội dung tin nhắn.', true); input.focus(); return; }
        if (!form.reportValidity()) return;
        const data = new FormData(form);
        keepDrafts(); sending.add(key); sizeInput(); sendStatus('Đang gửi tin nhắn…');
        try {
            const response = await fetch(form.action, {method:'POST',body:data,credentials:'same-origin',headers:{'X-Requested-With':'XMLHttpRequest'}});
            if (response.redirected) throw new Error('Phiên đăng nhập có thể đã hết hạn. Hãy đăng nhập lại; nội dung vẫn được giữ.');
            const result = response.headers.get('content-type')?.includes('application/json') ? await response.json() : null;
            if (!response.ok) throw new Error(result?.error || 'Chưa xác nhận được kết quả gửi. Hãy tải tin mới trước khi gửi lại.');
            const url = new URL(result?.url || '', location.origin);
            if (url.origin !== location.origin || url.pathname !== location.pathname) throw new Error('Đã nhận phản hồi nhưng chưa mở được hội thoại. Hãy tải tin mới để kiểm tra.');
            drafts.delete(key);
            if (draftKey() === key) {
                const currentInput = $('#message-input');
                if (currentInput) currentInput.value = '';
                // navigate captures the current draft; clear the confirmed submission before that capture.
                if (await navigate(url.href, false)) {
                    history.replaceState(null, '', url.href);
                    $('#message-input')?.focus({preventScroll:true});
                }
                sendStatus('Đã gửi tin nhắn.');
            } else window.TenantFeedback?.show('Đã gửi tin nhắn trong cuộc trò chuyện trước.');
        } catch (error) {
            const message = error instanceof TypeError ? 'Chưa xác nhận được kết quả gửi do kết nối. Hãy tải tin mới để kiểm tra trước khi gửi lại; nội dung vẫn được giữ.' : error.message;
            if (draftKey() === key) sendStatus(message, true);
            else window.TenantFeedback?.show(message, true);
        } finally { sending.delete(key); sizeInput(); }
    }
    function observeIncoming() {
        clearTimeout(readTimer); visibleIncoming.clear();
        const form = $('#message-read-form'), stream = $('#message-stream');
        if (!form || !stream) return;
        form.hidden = true;
        stream.addEventListener('scroll', queueReadScan, {passive:true});
        queueReadScan();
    }
    function queueReadScan() {
        if (scanScheduled) return;
        scanScheduled = true;
        requestAnimationFrame(() => {
            scanScheduled = false; clearTimeout(readTimer); visibleIncoming.clear();
            const stream = $('#message-stream'), form = $('#message-read-form');
            if (!stream?.getClientRects().length || !form?.hidden || document.visibilityState !== 'visible') return;
            const area = stream.getBoundingClientRect();
            if (area.height <= 0 || area.width <= 0) return;
            stream.querySelectorAll('.message[data-unread="true"]').forEach(node => {
                const box = node.getBoundingClientRect();
                const height = Math.min(box.bottom, area.bottom, innerHeight) - Math.max(box.top, area.top, 0);
                const width = Math.min(box.right,area.right,innerWidth) - Math.max(box.left,area.left,0);
                // Long messages can be taller than the viewport. Require a substantial visible portion.
                if (height > 0 && width > 0 && height >= Math.min(box.height*.6, area.height*.6)) visibleIncoming.add(node.dataset.messageId);
            });
            if (visibleIncoming.size) readTimer = setTimeout(() => markVisibleRead(), 650);
        });
    }
    async function markVisibleRead(allInPage = false) {
        const form = $('#message-read-form'), previousRoot = root;
        if (!form || readBusy || document.visibilityState !== 'visible' || root.querySelector('dialog[open]')) return;
        const ids = allInPage ? [...form.querySelectorAll('[name="MessageIds"]')].map(node => node.value)
            : [...visibleIncoming].filter(id => $(`[data-message-id="${id}"]`)?.dataset.unread === 'true');
        if (!ids.length) return;
        const data = new FormData(form); data.delete('MessageIds'); ids.forEach(id => data.append('MessageIds',id));
        readBusy = true;
        try {
            const response = await fetch(form.action,{method:'POST',body:data,credentials:'same-origin',headers:{'X-Requested-With':'XMLHttpRequest'}});
            if (!response.ok || response.redirected || !response.headers.get('content-type')?.includes('application/json')) throw new Error('read');
            const result = await response.json();
            if (root !== previousRoot) return;
            for (const id of ids) { const node = $(`[data-message-id="${id}"]`); if (node) node.dataset.unread = 'false'; visibleIncoming.delete(id); }
            updateUnread(result);
        } catch {
            if (root === previousRoot) {
                form.hidden = false;
                window.TenantFeedback?.show('Chưa cập nhật được trạng thái đọc. Bạn vẫn có thể xem tin và thử lại.',true);
            }
        } finally { readBusy = false; queueReadScan(); }
    }
    function updateUnread(result) {
        const total = $('.unread-total');
        if (total) { total.textContent = result.unreadConversations; total.hidden = result.unreadConversations === 0; total.setAttribute('aria-label',`${result.unreadConversations} cuộc trò chuyện chưa đọc`); }
        const filter = $('.inbox-filters a:nth-child(2) span'); if (filter) filter.textContent = result.unreadConversations;
        const thread = $(`[data-thread="${root.dataset.conversationId}"]`);
        if (thread) {
            thread.classList.toggle('unread',result.unreadMessages > 0);
            const badge = thread.querySelector('.thread-unread');
            if (badge) { badge.hidden = !result.unreadMessages; badge.textContent = result.unreadMessages > 99 ? '99+' : result.unreadMessages; badge.setAttribute('aria-label',`${result.unreadMessages} tin chưa đọc`); }
        }
    }
    function openContext(opener) {
        contextOpener = opener;
        const dialog = $('#context-dialog');
        if (dialog && compact.matches && !dialog.open) dialog.showModal();
        $('.context-toggle')?.setAttribute('aria-expanded',String(!!dialog?.open));
        if (dialog?.open) $('.close-context')?.focus({preventScroll:true});
    }
    let realtimeTimer;
    function updateFromSocket() {
        clearTimeout(realtimeTimer);
        realtimeTimer = setTimeout(() => {
            if (document.visibilityState !== 'visible' || root.querySelector('dialog[open]')) return;
            if (pending || sending.size) { realtimeTimer = setTimeout(updateFromSocket, 200); return; }
            navigate(new URL(location.href), false, true);
        }, 180);
    }
    document.addEventListener('chat:changed', updateFromSocket);
    document.addEventListener('chat:reconnected', updateFromSocket);
    document.addEventListener('visibilitychange', () => { if (document.visibilityState === 'visible') updateFromSocket(); });
    document.addEventListener('click',event=>{
        const link=event.target.closest('a');
        if (link?.matches('[data-action="refresh-messages"]') && root.contains(link) && !event.ctrlKey && !event.metaKey && !event.shiftKey && !event.altKey && event.button === 0) {
            event.preventDefault(); navigate(link.href, false); return;
        }
        if (link && root.contains(link) && (link.matches('[data-messages-link]') || link.closest('.tenant-pagination')) && !event.ctrlKey && !event.metaKey && !event.shiftKey && !event.altKey && event.button===0) {
            const url=new URL(link.href); if(url.origin===location.origin && url.pathname===location.pathname){event.preventDefault();navigate(url);return;}
        }
        const button=event.target.closest('[data-action]');
        if(button && root.contains(button)){
            const action=button.dataset.action;
            if(action==='show-context')openContext(button);
            if(action==='close-context')$('#context-dialog')?.close();
            if(action==='exit-search'){document.body.classList.remove('mobile-search');$('#inbox-title').focus({preventScroll:true});arrange();}
        }
        const quick=event.target.closest('[data-quick]');
        if(quick && root.contains(quick) && $('#message-input') && !sending.has(draftKey())){
            const input=$('#message-input');const text=quick.dataset.quick==='fees'?'Anh/chị cho mình hỏi các khoản phí dịch vụ của phòng được tính như thế nào ạ?':'Mình muốn xem phòng. Anh/chị có thể đón mình vào những giờ nào ạ?';
            input.value=`${input.value.trim()?input.value.trim()+'\n':''}${text}`.slice(0,2000);keepDrafts();sizeInput();input.focus();
        }
    });
    document.addEventListener('submit',event=>{
        if(event.target.id === 'message-send-form') { event.preventDefault(); sendMessage(event.target); return; }
        if(event.target.id === 'message-read-form') { event.preventDefault(); markVisibleRead(true); return; }
        if(event.target.matches('[data-message-search]')){
            event.preventDefault();const url=new URL(event.target.action);url.search=new URLSearchParams(new FormData(event.target)).toString();navigate(url);
        }
    });
    document.addEventListener('input',event=>{
        if(event.target.id==='message-input'){drafts.set(draftKey(),event.target.value);sizeInput();}

    });
    document.addEventListener('focusin',arrange);document.addEventListener('focusout',()=>requestAnimationFrame(arrange));
    document.addEventListener('visibilitychange', queueReadScan);
    phone.addEventListener('change',arrange);compact.addEventListener('change',arrange);addEventListener('resize',arrange);window.visualViewport?.addEventListener('resize',arrange);
    addEventListener('popstate',()=>navigate(new URL(location.href),false));
    addEventListener('beforeunload',event=>{keepDrafts();if([...drafts.values()].some(value=>value.trim())){event.preventDefault();event.returnValue='';}});
    document.addEventListener('error',event=>{const image=event.target;if(image.matches?.('[data-message-image]')&&!image.dataset.fallbackApplied){image.dataset.fallbackApplied='true';image.src='/images/room-placeholder.svg';}},true);
    install();document.fonts.ready.then(()=>{const stream=$('#message-stream');if(stream)stream.scrollTop=stream.scrollHeight;});
})();
