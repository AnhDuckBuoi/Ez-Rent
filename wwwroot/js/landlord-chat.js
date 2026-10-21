(() => {
    'use strict';
    let root = document.getElementById('landlord-messages-root');
    if (!root) return;
    let sending = false, refreshing = false, readBusy = false, timer, refreshTimer;
    const input = () => root.querySelector('#landlord-message-input');
    function status(text) { const node = root.querySelector('#landlord-send-status'); if (node) node.textContent = text; }
    async function refresh() {
        if (document.visibilityState !== 'visible') return;
        if (refreshing || sending) { clearTimeout(refreshTimer); refreshTimer = setTimeout(refresh,250); return; }
        refreshing = true;
        const draft = input()?.value || '', focused = document.activeElement === input(), start = input()?.selectionStart;
        const stream = root.querySelector('#landlord-message-stream');
        const scroll = stream?.scrollTop || 0, bottom = !stream || stream.scrollHeight - stream.scrollTop - stream.clientHeight < 100;
        try {
            const response = await fetch(location.href,{credentials:'same-origin'});
            if (!response.ok || response.redirected) return;
            const next = new DOMParser().parseFromString(await response.text(),'text/html').getElementById('landlord-messages-root');
            if (!next) return;
            // Capture typing that happened during the GET, not only before it started.
            const current = input()?.value ?? draft;
            root.replaceWith(next); root = next;
            const label = root.querySelector('[data-chat-connection]'); if (label && window.EzRentChatStatus) label.textContent = window.EzRentChatStatus;
            if (input()) { input().value = current; if (focused) { input().focus({preventScroll:true}); input().setSelectionRange(start,start); } }
            const nextStream = root.querySelector('#landlord-message-stream');
            if (nextStream) nextStream.scrollTop = bottom ? nextStream.scrollHeight : scroll;
            scheduleRead();
        } catch { status('Chưa cập nhật được tin mới. Nội dung đang soạn vẫn được giữ; bạn có thể tải lại tin.'); } finally { refreshing = false; }
    }
    function scheduleRead() { clearTimeout(timer); timer = setTimeout(markVisible,700); }
    async function markVisible() {
        const form = root.querySelector('#landlord-read-form'), stream = root.querySelector('#landlord-message-stream');
        if (!form || !stream || readBusy || document.visibilityState !== 'visible') return;
        const box = stream.getBoundingClientRect();
        const ids = [...stream.querySelectorAll('[data-unread=true]')].filter(node => {
            const rect = node.getBoundingClientRect();
            return Math.min(rect.bottom,box.bottom,innerHeight)-Math.max(rect.top,box.top,0) >= Math.min(rect.height*.6,box.height*.6);
        }).map(node => node.dataset.messageId);
        if (!ids.length) return;
        const data = new FormData(form); data.delete('MessageIds'); ids.forEach(id=>data.append('MessageIds',id));
        readBusy = true;
        try {
            const response = await fetch(form.action,{method:'POST',body:data,credentials:'same-origin',headers:{'X-Requested-With':'fetch'}});
            if (response.ok && !response.redirected) { ids.forEach(id=>{const node=root.querySelector(`[data-message-id="${id}"]`);if(node)node.dataset.unread='false';}); }
        } catch { /* Explicit read form remains available. */ }
        finally { readBusy = false; }
    }
    document.addEventListener('submit',async event=>{
        if (event.target.id !== 'landlord-message-form') return;
        event.preventDefault(); if(sending || !event.target.reportValidity()) return;
        const form=event.target,data=new FormData(form),button=form.querySelector('button');
        sending=true;button.disabled=true;input().readOnly=true;status('Đang gửi…');
        try {
            const response=await fetch(form.action,{method:'POST',body:data,credentials:'same-origin',headers:{'X-Requested-With':'fetch'}});
            const result=response.headers.get('content-type')?.includes('application/json')?await response.json():null;
            if(!response.ok||response.redirected||!result?.ok)throw new Error(result?.message||'Chưa xác nhận được kết quả gửi. Hãy tải tin mới trước khi gửi lại.');
            input().value='';sending=false;await refresh();status('Đã gửi tin nhắn.');input()?.focus({preventScroll:true});
        }catch(error){status(error.message||'Lỗi kết nối. Nội dung vẫn được giữ.');}
        finally{sending=false;if(input())input().readOnly=false;const current=root.querySelector('#landlord-message-form button');if(current)current.disabled=false;}
    });
    document.addEventListener('chat:changed',()=>{clearTimeout(refreshTimer);refreshTimer=setTimeout(refresh,150);});
    document.addEventListener('chat:reconnected',refresh);
    document.addEventListener('visibilitychange',()=>{if(document.visibilityState==='visible'){refresh();scheduleRead();}});
    document.addEventListener('scroll',scheduleRead,true);addEventListener('resize',scheduleRead);
    const stream=root.querySelector('#landlord-message-stream');if(stream)stream.scrollTop=stream.scrollHeight;scheduleRead();
})();
