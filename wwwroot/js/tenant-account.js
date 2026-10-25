// Small enhancements only: the forms also work without JavaScript.
(() => {
    const invalid = document.querySelector('.account-page .input-validation-error');
    if (invalid) invalid.focus();
    document.querySelectorAll('[data-password-toggle]').forEach(button => {
        const input = document.getElementById(button.getAttribute('aria-controls'));
        const label = button.getAttribute('aria-label');
        button.addEventListener('click', () => {
            const visible = input.type === 'password';
            input.type = visible ? 'text' : 'password';
            button.textContent = visible ? 'Ẩn' : 'Hiện';
            button.setAttribute('aria-pressed', String(visible));
            button.setAttribute('aria-label', visible ? label.replace('Hiện', 'Ẩn') : label);
        });
    });
    const avatar = document.querySelector('[data-avatar]');
    if (!avatar) return;
    const savedImage = avatar.querySelector('img');
    savedImage?.addEventListener('error', event => event.target.remove());
    if (savedImage?.complete && savedImage.naturalWidth === 0) savedImage.remove();
    const fileInput = document.getElementById('Avatar_File');
    const status = document.querySelector('[data-avatar-status]');
    const original = avatar.querySelector('img')?.getAttribute('src');
    let previewUrl;
    fileInput.addEventListener('change', () => {
        if (previewUrl) URL.revokeObjectURL(previewUrl);
        avatar.querySelector('img')?.remove();
        const file = fileInput.files[0];
        fileInput.setCustomValidity('');
        status.textContent = '';
        let url = original;
        if (file) {
            const valid = ['image/png', 'image/jpeg'].includes(file.type) && file.size <= 2 * 1024 * 1024;
            if (!valid) {
                const message = 'Chọn ảnh PNG hoặc JPEG không quá 2 MB.';
                fileInput.setCustomValidity(message);
                status.textContent = message;
            } else {
                previewUrl = URL.createObjectURL(file);
                url = previewUrl;
                status.textContent = 'Ảnh xem trước. Nhấn Lưu ảnh đại diện để cập nhật.';
            }
        }
        if (url) {
            const image = document.createElement('img');
            image.alt = 'Ảnh đại diện của bạn';
            image.width = image.height = 104;
            image.addEventListener('error', () => image.remove());
            image.src = url;
            avatar.append(image);
        }
    });
    window.addEventListener('pagehide', () => { if (previewUrl) URL.revokeObjectURL(previewUrl); });
})();
