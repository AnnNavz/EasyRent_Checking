(function () {
    function formatTimestamp(date) {
        return date.toLocaleString(undefined, {
            year: 'numeric',
            month: 'short',
            day: 'numeric',
            hour: 'numeric',
            minute: '2-digit',
            hour12: true
        });
    }

    function initPhotoCard(card, options) {
        const dropzone = card.querySelector('[data-photo-dropzone]');
        const fileInput = card.querySelector('.transit-photo-file-input');
        const fileBar = card.querySelector('[data-photo-file-bar]');
        const fileNameEl = card.querySelector('[data-photo-file-name]');
        const removeBtn = card.querySelector('[data-photo-remove-btn]');
        const uploadEmpty = card.querySelector('[data-photo-upload-empty]');
        const preview = card.querySelector('[data-photo-preview]');
        const previewImg = card.querySelector('[data-photo-preview-img]');
        const timestampEl = card.querySelector('[data-photo-timestamp]');
        const isPrimary = fileInput?.hasAttribute('data-primary-photo-input') === true;
        if (!dropzone || !fileInput || !fileBar || !fileNameEl || !removeBtn) return;

        function showFile(file) {
            if (!file || !file.type || file.type.indexOf('image/') !== 0) return;

            fileNameEl.textContent = file.name;
            fileBar.hidden = false;

            const reader = new FileReader();
            reader.onload = function (event) {
                if (previewImg) {
                    previewImg.src = event.target.result;
                    previewImg.alt = 'Vehicle photo preview';
                }

                if (timestampEl) {
                    timestampEl.textContent = formatTimestamp(new Date());
                }

                if (uploadEmpty) {
                    uploadEmpty.hidden = true;
                }

                if (preview) {
                    preview.hidden = false;
                }

                dropzone.classList.add('has-preview');
            };
            reader.readAsDataURL(file);
        }

        function clearFile() {
            fileInput.value = '';
            fileNameEl.textContent = '';
            fileBar.hidden = true;

            if (previewImg) {
                previewImg.removeAttribute('src');
                previewImg.alt = '';
            }

            if (timestampEl) {
                timestampEl.textContent = '';
            }

            if (uploadEmpty) {
                uploadEmpty.hidden = false;
            }

            if (preview) {
                preview.hidden = true;
            }

            dropzone.classList.remove('has-preview');
        }

        dropzone.addEventListener('click', function (event) {
            if (event.target.closest('[data-photo-remove-btn]')) return;
            fileInput.click();
        });
        dropzone.addEventListener('dragover', function (event) {
            event.preventDefault();
            dropzone.classList.add('is-dragover');
        });
        dropzone.addEventListener('dragleave', function () {
            dropzone.classList.remove('is-dragover');
        });
        dropzone.addEventListener('drop', function (event) {
            event.preventDefault();
            dropzone.classList.remove('is-dragover');
            const file = event.dataTransfer?.files?.[0];
            if (!file) return;
            fileInput.files = event.dataTransfer.files;
            showFile(file);
        });
        fileInput.addEventListener('change', function () {
            const file = fileInput.files?.[0];
            if (file) showFile(file);
            else clearFile();
        });
        removeBtn.addEventListener('click', function (event) {
            event.stopPropagation();

            if (!isPrimary) {
                card.remove();
                options.onCardsChanged?.();
                return;
            }

            clearFile();
        });
    }

    window.initTimestampedVehiclePhotos = function (gridId, addButtonId, templateId) {
        const photoGrid = document.getElementById(gridId);
        const photoAddBtn = document.getElementById(addButtonId);
        const photoTemplate = document.getElementById(templateId);
        const maxPhotos = parseInt(photoGrid?.dataset.maxPhotos || '4', 10);

        function getPhotoCardCount() {
            return photoGrid?.querySelectorAll('[data-photo-card]').length || 0;
        }

        function updateAddButtonVisibility() {
            if (!photoAddBtn) return;
            const atLimit = getPhotoCardCount() >= maxPhotos;
            photoAddBtn.hidden = atLimit;
            photoAddBtn.disabled = atLimit;
        }

        const cardOptions = {
            onCardsChanged: updateAddButtonVisibility
        };

        photoGrid?.querySelectorAll('[data-photo-card]').forEach(function (card) {
            initPhotoCard(card, cardOptions);
        });

        updateAddButtonVisibility();

        if (!photoAddBtn || !photoGrid || !photoTemplate) return;

        photoAddBtn.addEventListener('click', function () {
            if (getPhotoCardCount() >= maxPhotos) return;

            const fragment = photoTemplate.content.cloneNode(true);
            photoGrid.insertBefore(fragment, photoAddBtn);
            const insertedCard = photoAddBtn.previousElementSibling;
            if (insertedCard) {
                initPhotoCard(insertedCard, cardOptions);
            }

            updateAddButtonVisibility();
        });
    };
})();
