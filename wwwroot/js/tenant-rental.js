// The rental pages use normal server forms; these enhancements do not replace validation.
(() => {
    document.querySelector('[data-contract-print]')?.addEventListener('click', () => window.print());
    document.querySelector('.rental-page .input-validation-error:not([type=hidden])')?.focus();
    const draftForm = document.querySelector('[data-contract-draft-form]');
    if (draftForm) {
        const status = draftForm.querySelector('[data-draft-unsaved]');
        const print = document.querySelector('[data-contract-print]');
        const markUnsaved = () => {
            status.textContent = 'Bạn có thay đổi chưa lưu. Bản giấy vẫn giữ nội dung trước đó; hãy lưu để cập nhật trước khi in.';
            if (print) { print.disabled = true; print.textContent = 'Lưu bản nháp trước khi in'; }
        };
        draftForm.addEventListener('input', markUnsaved);
        draftForm.addEventListener('change', markUnsaved);
        if (draftForm.dataset.draftAttempted === 'true') markUnsaved();
    }
    const description = document.getElementById('Description');
    const count = document.querySelector('[data-repair-count]');
    if (description && count) {
        const update = () => { count.textContent = description.value.length.toLocaleString('vi-VN'); };
        description.addEventListener('input', update);
        update();
    }
    document.querySelectorAll('[data-rental-image]').forEach(image => {
        const fallback = () => {
            if (image.dataset.rentalImage === 'room') {
                image.src = '/images/room-placeholder.svg';
                image.alt = 'Chưa có ảnh phòng khả dụng';
            } else {
                image.hidden = true;
                const message = document.createElement('p');
                message.className = 'rental-note';
                message.textContent = 'Không tải được ảnh đính kèm. Hãy thử lại sau.';
                image.after(message);
            }
        };
        image.addEventListener('error', fallback, { once: true });
        if (image.complete && image.naturalWidth === 0) fallback();
    });

    const zoomLinks = document.querySelectorAll('[data-rental-zoom]');
    if (zoomLinks.length && typeof HTMLDialogElement !== 'undefined') {
        const dialog = document.createElement('dialog');
        dialog.className = 'rental-image-dialog';
        dialog.setAttribute('aria-labelledby', 'rental-image-dialog-title');
        const heading = document.createElement('div');
        heading.className = 'rental-dialog-heading';
        const title = document.createElement('h2');
        title.id = 'rental-image-dialog-title';
        const close = document.createElement('button');
        close.type = 'button';
        close.className = 'rental-button secondary';
        close.textContent = 'Đóng';
        const image = document.createElement('img');
        const error = document.createElement('p');
        error.className = 'rental-note';
        error.hidden = true;
        error.textContent = 'Chưa tải được ảnh. Hãy đóng và thử lại hoặc dùng nút tải ảnh.';
        image.addEventListener('error', () => { image.hidden = true; error.hidden = false; });
        heading.append(title, close);
        dialog.append(heading, image, error);
        document.body.append(dialog);
        close.addEventListener('click', () => dialog.close());
        zoomLinks.forEach(link => link.addEventListener('click', event => {
            event.preventDefault();
            title.textContent = link.dataset.zoomTitle;
            image.alt = link.dataset.zoomTitle;
            error.hidden = true;
            image.hidden = false;
            image.src = link.href;
            dialog.showModal();
        }));
    }
})();
