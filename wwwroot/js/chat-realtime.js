(() => {
    'use strict';
    if (!window.signalR) return;
    const connection = new signalR.HubConnectionBuilder().withUrl('/hubs/chat')
        .withAutomaticReconnect([0, 2000, 5000, 10000, 30000]).configureLogging(signalR.LogLevel.Warning).build();
    const status = text => { window.EzRentChatStatus = text; document.querySelectorAll('[data-chat-connection]').forEach(node => node.textContent = text); };
    let retry;
    connection.on('ConversationChanged', conversationId => {
        if (Number.isSafeInteger(conversationId) && conversationId > 0)
            document.dispatchEvent(new CustomEvent('chat:changed', {detail:{conversationId}}));
    });
    connection.onreconnecting(() => status('Đang kết nối lại…'));
    connection.onreconnected(() => { status('Đã kết nối'); document.dispatchEvent(new Event('chat:reconnected')); });
    connection.onclose(() => { status('Mất kết nối · Bạn có thể tải tin mới'); retry = setTimeout(start, 15000); });
    async function start() {
        clearTimeout(retry);
        if (document.visibilityState === 'hidden' || connection.state !== signalR.HubConnectionState.Disconnected) return;
        try { await connection.start(); status('Đã kết nối'); }
        catch { status('Chưa kết nối · Bạn vẫn có thể gửi và tải tin mới'); retry = setTimeout(start,15000); }
    }
    document.addEventListener('visibilitychange', () => { if (document.visibilityState === 'visible') start(); });
    addEventListener('pagehide', () => { clearTimeout(retry); connection.stop(); });
    start();
})();
