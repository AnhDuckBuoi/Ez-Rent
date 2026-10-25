(() => {
    'use strict';
    const form = document.getElementById('tenant-support-form');
    if (!form) return;
    const button = document.getElementById('support-submit');
    const status = document.getElementById('support-status');
    const description = document.getElementById('Input_Description');
    let busy = false, dirty = !!(form.elements.namedItem('Input.Subject').value.trim() || description.value.trim()), saved = false;
    function showError(message) { status.hidden = false; status.textContent = message; status.focus({preventScroll:true}); }
    function updateCount() { document.getElementById('support-count').textContent = `${description.value.length.toLocaleString('vi-VN')}/1.000`; }
    form.addEventListener('input', event => {
        dirty = true;
        const field = event.target;
        if (field.name && field.value.trim() && field.checkValidity()) {
            field.removeAttribute('aria-invalid'); field.classList.remove('input-validation-error');
            const error = form.querySelector(`[data-valmsg-for="${field.name}"]`);
            if (error) error.textContent = '';
        }
        updateCount();
    });
    form.addEventListener('change', () => { dirty = true; });
    form.addEventListener('submit', async event => {
        event.preventDefault();
        if (busy || button.disabled || !form.reportValidity()) return;
        const data = new FormData(form);
        busy = true; button.disabled = true; button.textContent = 'Đang gửi…'; form.setAttribute('aria-busy','true');
        // Freeze the submitted fields, so a successful response cannot erase typing made in flight.
        const fields = [...form.querySelectorAll('input:not([type=hidden]),textarea,select')];
        fields.forEach(field => field.disabled = true);
        try {
            const response = await fetch(form.action, {method:'POST',body:data,credentials:'same-origin',headers:{'X-Requested-With':'XMLHttpRequest'}});
            if (response.redirected) throw new Error('Phiên đăng nhập có thể đã hết hạn. Hãy đăng nhập lại; nội dung vẫn được giữ ở đây.');
            const result = response.headers.get('content-type')?.includes('application/json') ? await response.json() : null;
            if (!response.ok) {
                for (const [name,errors] of Object.entries(result?.errors || {})) {
                    const field = form.elements.namedItem(name), error = [...form.querySelectorAll('[data-valmsg-for]')].find(node => node.dataset.valmsgFor === name);
                    field?.setAttribute('aria-invalid','true');
                    if (error) { error.textContent = errors[0]; error.classList.add('field-validation-error'); }
                }
                throw new Error(result?.error || 'Chưa xác nhận được kết quả gửi. Hãy xem Yêu cầu đã gửi trước khi thử lại.');
            }
            const url = new URL(result?.url || '', location.origin);
            if (url.origin !== location.origin || url.pathname !== '/Tenant/Dashboard/Messages' || !url.searchParams.has('ConversationId'))
                throw new Error('Chưa mở được yêu cầu. Hãy xem Yêu cầu đã gửi để kiểm tra trước khi gửi lại.');
            saved = true;
            location.assign(url.href);
        } catch (error) {
            showError(error instanceof TypeError ? 'Chưa xác nhận được kết quả gửi do kết nối. Hãy xem Yêu cầu đã gửi trước khi gửi lại; nội dung vẫn được giữ.' : error.message);
        } finally {
            busy = false; fields.forEach(field => field.disabled = false); button.disabled = false;
            button.textContent = 'Gửi yêu cầu'; form.removeAttribute('aria-busy');
        }
    });
    addEventListener('beforeunload', event => { if (dirty && !saved) { event.preventDefault(); event.returnValue = ''; } });
    updateCount();
})();
