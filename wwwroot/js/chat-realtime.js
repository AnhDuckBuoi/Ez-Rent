(() => {
    'use strict';
    let state = 'connecting', label = 'Đang kết nối…', retry, suspended = false, stopping = Promise.resolve();

    // A connected socket does not guarantee that the conversation GET succeeded.
    function updateControls() {
        window.EzRentChatStatus = label;
        document.querySelectorAll('[data-chat-connection]').forEach(node => node.textContent = label);
        document.querySelectorAll('[data-chat-retry]').forEach(node => {
            const failed = node.closest('[data-chat-root]')?.dataset.chatSyncError === 'true';
            node.hidden = !failed && ['connecting', 'connected', 'reconnecting'].includes(state);
        });
    }
    window.EzRentChat = { get state() { return state; }, updateControls };
    function setState(next, text) {
        state = next; label = text; updateControls();
        document.dispatchEvent(new CustomEvent('chat:state', { detail: { state } }));
    }
    function connected() {
        setState('connected', 'Đã kết nối · Tin nhắn tự cập nhật');
        // Catch up on the first connection too: a message may arrive between
        // rendering the page and connecting its socket.
        document.dispatchEvent(new Event('chat:connected'));
    }
    if (!window.signalR) {
        setState('disconnected', 'Chưa kết nối · Bạn có thể thử tải tin mới');
        return;
    }
    const connection = new signalR.HubConnectionBuilder().withUrl('/hubs/chat')
        .withAutomaticReconnect([0, 2000, 5000, 10000, 30000])
        .configureLogging(signalR.LogLevel.Warning).build();
    connection.on('ConversationChanged', conversationId => {
        if (Number.isSafeInteger(conversationId) && conversationId > 0)
            document.dispatchEvent(new CustomEvent('chat:changed', {detail:{conversationId}}));
    });
    connection.onreconnecting(() => setState('reconnecting', 'Đang kết nối lại…'));
    connection.onreconnected(connected);
    connection.onclose(() => {
        if (suspended) return;
        setState('disconnected', 'Mất kết nối · Đang thử kết nối lại');
        retry = setTimeout(start, 15000);
    });
    async function start() {
        clearTimeout(retry);
        if (suspended || document.visibilityState === 'hidden' || connection.state !== signalR.HubConnectionState.Disconnected) return;
        setState('connecting', 'Đang kết nối…');
        try { await connection.start(); connected(); }
        catch {
            if (suspended) return;
            setState('disconnected', 'Chưa kết nối · Đang thử kết nối lại');
            retry = setTimeout(start, 15000);
        }
    }
    document.addEventListener('visibilitychange', () => { if (document.visibilityState === 'visible') start(); });
    addEventListener('online', start);
    addEventListener('pageshow', () => { suspended = false; stopping.then(start); });
    addEventListener('pagehide', () => {
        suspended = true; clearTimeout(retry);
        stopping = connection.stop().catch(() => { /* The page is leaving. */ });
    });
    start();
})();
