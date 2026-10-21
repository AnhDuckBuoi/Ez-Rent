(() => {
    'use strict';
    // A full-page filter change should leave the selected tab visible on a narrow screen.
    document.querySelector('.appointment-filters [aria-current="page"]')
        ?.scrollIntoView({ block: 'nearest', inline: 'nearest' });
    const form = document.querySelector('[data-cancel-appointment]');
    if (!form) return;
    const button = form.querySelector('button[type="submit"]');
    form.addEventListener('submit', () => {
        button.disabled = true;
        button.textContent = 'Đang hủy…';
    });
    window.addEventListener('pageshow', () => {
        button.disabled = false;
        button.textContent = 'Xác nhận hủy lịch';
    });
})();
