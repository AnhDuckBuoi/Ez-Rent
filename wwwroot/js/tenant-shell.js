(() => {
    'use strict';
    const region = document.querySelector('[data-tenant-feedback]');
    window.TenantFeedback = {
        show(message, error = false) {
            if (!region) return;
            const notice = document.createElement('div');
            notice.className = `tenant-feedback${error ? ' is-error' : ''}`;
            notice.setAttribute('role', error ? 'alert' : 'status');
            const copy = document.createElement('span');
            copy.textContent = message;
            const close = document.createElement('button');
            close.type = 'button';
            close.dataset.feedbackClose = '';
            close.setAttribute('aria-label', 'Đóng thông báo');
            close.textContent = '×';
            notice.append(copy, close);
            region.replaceChildren(notice);
            // Keep the same above-content placement as Admin, including for AJAX actions.
            if (region.getBoundingClientRect().bottom < 0) region.scrollIntoView({ block: 'start' });
        }
    };
    region?.addEventListener('click', event => {
        const close = event.target.closest('[data-feedback-close]');
        if (!close) return;
        const notice = close.closest('.tenant-feedback');
        // Move keyboard focus before removing its focused close button.
        if (document.activeElement === close) {
            const main = document.getElementById('main');
            if (main) { main.setAttribute('tabindex', '-1'); main.focus({ preventScroll: true }); }
        }
        notice?.remove();
    });
})();
// Dataset photos can fail before deferred scripts attach their error listeners.
(() => {
    const selector = 'img[data-fallback],img[data-message-image]';
    function repair(image) {
        if (!(image instanceof HTMLImageElement) || image.dataset.fallbackApplied) return;
        const fallback = image.dataset.fallback || (image.hasAttribute('data-message-image') ? '/images/room-placeholder.svg' : null);
        if (!fallback) return;
        image.dataset.fallbackApplied = 'true';
        delete image.dataset.fallback;
        image.src = fallback;
    }
    function scan(node) {
        if (!(node instanceof Element)) return;
        const images = [...node.querySelectorAll(selector)];
        if (node.matches(selector)) images.push(node);
        images.filter(image => image.complete && image.naturalWidth === 0).forEach(repair);
    }
    document.addEventListener('error', event => repair(event.target), true);
    scan(document.body);
    new MutationObserver(records => records.forEach(record => record.addedNodes.forEach(scan)))
        .observe(document.querySelector('.tenant-surface') || document.body, { childList: true, subtree: true });
})();
