(function (window) {
    'use strict';

    function text(id, value) {
        var el = document.getElementById(id);
        if (el) el.textContent = value == null || value === '' ? '—' : value;
    }

    function formatPeso(amount) {
        return '₱' + Number(amount || 0).toLocaleString('en-PH', {
            minimumFractionDigits: 2,
            maximumFractionDigits: 2
        });
    }

    function formatDisplayDate(dateStr) {
        if (!dateStr) return '';
        var parts = dateStr.split('-');
        if (parts.length !== 3) return dateStr;
        var months = ['Jan.', 'Feb.', 'Mar.', 'Apr.', 'May', 'Jun.', 'Jul.', 'Aug.', 'Sep.', 'Oct.', 'Nov.', 'Dec.'];
        var monthIndex = parseInt(parts[1], 10) - 1;
        return months[monthIndex] + ' ' + parseInt(parts[2], 10) + ', ' + parts[0];
    }

    function formatDisplayTime(timeStr) {
        if (!timeStr) return '';
        var bits = timeStr.split(':');
        var h = parseInt(bits[0], 10);
        var m = bits[1] || '00';
        var suffix = h >= 12 ? 'PM' : 'AM';
        var hour12 = h % 12;
        if (hour12 === 0) hour12 = 12;
        return hour12 + ':' + m + suffix;
    }

    function formatPaymentDateTime(value) {
        if (!value) return '—';
        var raw = String(value).trim();
        var datePart = '';
        var timePart = '';
        if (raw.indexOf('T') >= 0) {
            var bits = raw.split('T');
            datePart = bits[0] || '';
            timePart = (bits[1] || '').slice(0, 5);
        } else if (raw.indexOf(' ') >= 0) {
            var parts = raw.split(' ');
            datePart = parts[0] || '';
            timePart = (parts[1] || '').slice(0, 5);
        } else {
            datePart = raw;
        }
        var dateLabel = formatDisplayDate(datePart);
        var timeLabel = formatDisplayTime(timePart);
        if (dateLabel && timeLabel) return dateLabel + ' • ' + timeLabel;
        return dateLabel || timeLabel || '—';
    }

    function calcHours(pickupDate, pickupTime, returnDate, returnTime) {
        if (!pickupDate || !returnDate) return 0;
        var start = new Date(pickupDate + 'T' + (pickupTime || '00:00'));
        var end = new Date(returnDate + 'T' + (returnTime || '00:00'));
        if (isNaN(start.getTime()) || isNaN(end.getTime()) || end < start) return 0;
        return Math.max(0, Math.round((end - start) / 3600000));
    }

    function setPreviewImage(wrapId, imgId, linkId, src) {
        var wrap = document.getElementById(wrapId);
        var img = document.getElementById(imgId);
        var link = document.getElementById(linkId);
        if (!wrap || !img) return;

        if (src) {
            img.src = src;
            if (link) link.href = src;
            wrap.classList.remove('d-none');
        } else {
            img.removeAttribute('src');
            if (link) link.href = '#';
            wrap.classList.add('d-none');
        }
    }

    function loadFilePreview(file, wrapId, imgId, linkId) {
        if (!file || !(file.type && file.type.indexOf('image/') === 0)) {
            setPreviewImage(wrapId, imgId, linkId, null);
            return;
        }
        var reader = new FileReader();
        reader.onload = function (e) {
            setPreviewImage(wrapId, imgId, linkId, e.target.result);
        };
        reader.readAsDataURL(file);
    }

    function setIdCardPreview(options) {
        var card = document.getElementById(options.cardId);
        var link = document.getElementById(options.linkId);
        var img = document.getElementById(options.imgId);
        var nameEl = document.getElementById(options.nameId);
        if (!card || !link || !img) return false;

        var src = options.src || '';
        var fileName = options.fileName || '';
        if (!src) {
            card.classList.add('d-none');
            img.removeAttribute('src');
            link.href = '#';
            link.classList.add('d-none');
            if (nameEl) nameEl.textContent = '';
            return false;
        }

        card.classList.remove('d-none');
        img.src = src;
        link.href = src;
        link.classList.remove('d-none');
        if (nameEl) nameEl.textContent = fileName || '';
        return true;
    }

    function loadIdCardFile(file, options) {
        if (!file || !(file.type && file.type.indexOf('image/') === 0)) {
            setIdCardPreview({
                cardId: options.cardId,
                linkId: options.linkId,
                imgId: options.imgId,
                nameId: options.nameId,
                src: null,
                fileName: ''
            });
            return;
        }
        var reader = new FileReader();
        reader.onload = function (e) {
            setIdCardPreview({
                cardId: options.cardId,
                linkId: options.linkId,
                imgId: options.imgId,
                nameId: options.nameId,
                src: e.target.result,
                fileName: file.name || ''
            });
        };
        reader.readAsDataURL(file);
    }

    function populateBookingSummary(data) {
        var now = data.bookingDate || new Date();
        var bookingLabel = formatDisplayDate(
            now.getFullYear() + '-' + String(now.getMonth() + 1).padStart(2, '0') + '-' + String(now.getDate()).padStart(2, '0')
        ) + ' • ' + formatDisplayTime(
            String(now.getHours()).padStart(2, '0') + ':' + String(now.getMinutes()).padStart(2, '0')
        );

        var hours = calcHours(data.pickupDate, data.pickupTime, data.returnDate, data.returnTime);
        var baseHours = data.baseHours || 8;
        var succeedingHours = Math.max(0, hours - baseHours);
        var basePrice = Number(data.basePrice || 0);
        var succeedingFee = Number(data.succeedingFee || 0);
        var succeedingAmount = succeedingHours * succeedingFee;
        var subtotal = basePrice + succeedingAmount;
        var hasDiscount = data.discount === 'Yes' || data.discount === true || data.discount === '1';
        var discountAmount = hasDiscount ? subtotal * 0.10 : 0;
        var total = subtotal - discountAmount;

        text('summaryBookingDateTime', bookingLabel);
        text('summaryCustomerName', data.customerName);
        text('summaryContact', data.contactNumber);
        text('summaryPickupLocation', data.pickupLocation);
        text('summaryDropoffLocation', data.dropoffLocation);
        text('summaryPassengers', data.passengerCount ? (data.passengerCount + (Number(data.passengerCount) === 1 ? ' Passenger' : ' Passengers')) : '—');
        text('summaryDiscount', hasDiscount ? 'Yes' : 'No');
        text('summaryVehicleName', data.vehicleName);
        text('summaryVehicleType', data.vehicleType);
        text('summaryPickupDateTime', (data.pickupDate ? formatDisplayDate(data.pickupDate) : '—') + (data.pickupTime ? ' • ' + formatDisplayTime(data.pickupTime) : ''));
        text('summaryReturnDateTime', (data.returnDate ? formatDisplayDate(data.returnDate) : '—') + (data.returnTime ? ' • ' + formatDisplayTime(data.returnTime) : ''));
        text('summaryDuration', hours === 1 ? '1 hour' : hours + ' hours');
        text('summaryNotes', data.notes || 'None');

        text('summaryBaseFee', formatPeso(basePrice));
        text('summarySucceedingLabel', succeedingHours > 0
            ? ('Succeeding Hours (' + succeedingHours + (succeedingHours === 1 ? ' hour' : ' hours') + ')')
            : 'Succeeding Hours');
        text('summarySucceedingFee', formatPeso(succeedingAmount));

        var discountRow = document.getElementById('summaryDiscountRow');
        var discountSpacer = document.getElementById('summaryDiscountSpacer');
        if (discountRow) discountRow.hidden = !hasDiscount;
        if (discountSpacer) discountSpacer.hidden = !hasDiscount;
        text('summaryDiscountAmount', '-' + formatPeso(discountAmount));
        text('summaryTotalCost', formatPeso(total));

        // Discount / Senior ID previews (front + back)
        var idSection = document.getElementById('summaryDiscountIdSection');
        var idFallback = document.getElementById('summaryValidIdFallback');
        var discountFileInput = document.getElementById('input-DiscountFile');
        var discountBackInput = document.getElementById('input-DiscountBackFile');
        var discountFile = (discountFileInput && discountFileInput.files && discountFileInput.files[0])
            ? discountFileInput.files[0]
            : null;
        var discountBackFile = (discountBackInput && discountBackInput.files && discountBackInput.files[0])
            ? discountBackInput.files[0]
            : null;

        if (hasDiscount) {
            if (idSection) idSection.classList.remove('d-none');
            if (idFallback) idFallback.classList.add('d-none');

            if (discountFile) {
                loadIdCardFile(discountFile, {
                    cardId: 'summaryDiscountFrontCard',
                    linkId: 'summaryDiscountPreviewLink',
                    imgId: 'summaryDiscountPreviewImg',
                    nameId: 'summaryDiscountFrontName'
                });
            } else if (data.discountImageUrl) {
                setIdCardPreview({
                    cardId: 'summaryDiscountFrontCard',
                    linkId: 'summaryDiscountPreviewLink',
                    imgId: 'summaryDiscountPreviewImg',
                    nameId: 'summaryDiscountFrontName',
                    src: data.discountImageUrl,
                    fileName: data.discountFileName || ''
                });
            } else {
                setIdCardPreview({
                    cardId: 'summaryDiscountFrontCard',
                    linkId: 'summaryDiscountPreviewLink',
                    imgId: 'summaryDiscountPreviewImg',
                    nameId: 'summaryDiscountFrontName',
                    src: null
                });
                if (idFallback) {
                    idFallback.textContent = data.discountFileName || 'ID uploaded';
                    idFallback.classList.remove('d-none');
                }
            }

            if (discountBackFile) {
                loadIdCardFile(discountBackFile, {
                    cardId: 'summaryDiscountBackCard',
                    linkId: 'summaryDiscountBackPreviewLink',
                    imgId: 'summaryDiscountBackPreviewImg',
                    nameId: 'summaryDiscountBackName'
                });
            } else if (data.discountBackImageUrl) {
                setIdCardPreview({
                    cardId: 'summaryDiscountBackCard',
                    linkId: 'summaryDiscountBackPreviewLink',
                    imgId: 'summaryDiscountBackPreviewImg',
                    nameId: 'summaryDiscountBackName',
                    src: data.discountBackImageUrl,
                    fileName: data.discountBackFileName || ''
                });
            } else {
                setIdCardPreview({
                    cardId: 'summaryDiscountBackCard',
                    linkId: 'summaryDiscountBackPreviewLink',
                    imgId: 'summaryDiscountBackPreviewImg',
                    nameId: 'summaryDiscountBackName',
                    src: null
                });
            }
        } else {
            if (idSection) idSection.classList.add('d-none');
            setIdCardPreview({
                cardId: 'summaryDiscountFrontCard',
                linkId: 'summaryDiscountPreviewLink',
                imgId: 'summaryDiscountPreviewImg',
                nameId: 'summaryDiscountFrontName',
                src: null
            });
            setIdCardPreview({
                cardId: 'summaryDiscountBackCard',
                linkId: 'summaryDiscountBackPreviewLink',
                imgId: 'summaryDiscountBackPreviewImg',
                nameId: 'summaryDiscountBackName',
                src: null
            });
            if (idFallback) {
                idFallback.textContent = 'Not required';
                idFallback.classList.remove('d-none');
            }
        }

        // Receipt preview (cashless payments)
        var receiptSection = document.getElementById('summaryReceiptSection');
        var receiptFileInput = document.getElementById('input-ReceiptFile');
        var receiptFile = (receiptFileInput && receiptFileInput.files && receiptFileInput.files[0])
            ? receiptFileInput.files[0]
            : null;
        var paymentMethod = data.paymentMethod || '';
        var hasReceipt = !!(receiptFile || data.receiptImageUrl);
        var isWalkIn = paymentMethod === 'Walk-in';

        if (receiptSection) {
            // Keep proof section visible for pay-now; hide image when missing / walk-in.
            receiptSection.hidden = false;
            if (!isWalkIn && receiptFile) {
                text('summaryReceiptName', receiptFile.name);
                loadFilePreview(receiptFile, 'summaryReceiptPreviewWrap', 'summaryReceiptPreviewImg', 'summaryReceiptPreviewLink');
                document.getElementById('summaryReceiptName')?.classList.add('d-none');
            } else if (!isWalkIn && data.receiptImageUrl) {
                text('summaryReceiptName', data.receiptFileName || 'Receipt on file');
                setPreviewImage('summaryReceiptPreviewWrap', 'summaryReceiptPreviewImg', 'summaryReceiptPreviewLink', data.receiptImageUrl);
                document.getElementById('summaryReceiptName')?.classList.add('d-none');
            } else {
                text('summaryReceiptName', isWalkIn ? 'Not required for walk-in' : (hasReceipt ? '—' : 'No screenshot uploaded'));
                setPreviewImage('summaryReceiptPreviewWrap', 'summaryReceiptPreviewImg', 'summaryReceiptPreviewLink', null);
                document.getElementById('summaryReceiptName')?.classList.remove('d-none');
            }
        }

        if (data.paymentType != null) text('summaryPaymentType', data.paymentType);
        if (data.paymentMethod != null) text('summaryPaymentMethod', data.paymentMethod);
        if (data.amountPaid != null) text('summaryAmountPaid', formatPeso(data.amountPaid));
        if (data.accountName != null) text('summaryPaymentAccount', data.accountName || '—');
        if (data.transactionReference != null) text('summaryTransactionReference', data.transactionReference || '—');
        text('summaryPaymentDateTime', formatPaymentDateTime(data.paymentDate));
    }

    function initBookingWizard(options) {
        var form = document.getElementById(options.formId || 'reservationWizardForm');
        var stepNodes = Array.prototype.slice.call(document.querySelectorAll(options.stepSelector || '.admin-booking-stepper .step-node'));
        var stepLines = Array.prototype.slice.call(document.querySelectorAll('.client-reservation-stepper .client-reservation-step-line'));
        var panelIds = options.stepPanelIds || ['reservationStep1', 'reservationStep2'];
        var panels = panelIds.map(function (id) { return document.getElementById(id); }).filter(Boolean);
        var totalPanels = panels.length || 2;
        var submitStep = options.submitStep || totalPanels;
        var visualStepMap = options.visualStepMap || null;
        var currentStep = 1;

        function setStep(step) {
            currentStep = step;
            panels.forEach(function (panel, index) {
                panel.classList.toggle('active', (index + 1) === step);
            });

            // Legacy 2-panel support when stepPanelIds not used fully
            var step1 = document.getElementById(options.step1Id || 'reservationStep1');
            var step2 = document.getElementById(options.step2Id || 'reservationStep2');
            if (!options.stepPanelIds) {
                if (step1) step1.classList.toggle('active', step === 1);
                if (step2) step2.classList.toggle('active', step === 2);
            }

            var visualStep = visualStepMap && visualStepMap[step] != null
                ? Number(visualStepMap[step])
                : step;

            stepNodes.forEach(function (node, index) {
                var n = index + 1;
                var isActive = n === visualStep;
                var isCompleted = n < visualStep;
                node.classList.toggle('active', isActive);
                node.classList.toggle('completed', isCompleted);

                var circle = node.querySelector('.step-circle, .client-reservation-step-dot');
                if (!circle) return;
                if (isCompleted) {
                    circle.innerHTML = '<i class="bi bi-check-lg"></i>';
                } else {
                    circle.textContent = String(n);
                }
            });

            stepLines.forEach(function (line, index) {
                line.classList.toggle('is-complete', index < (visualStep - 1));
            });

            if (typeof options.onStepChange === 'function') {
                options.onStepChange(step);
            }

            window.scrollTo({ top: 0, behavior: 'smooth' });
        }

        function runValidator(name) {
            if (typeof options[name] === 'function') {
                return options[name]();
            }
            return true;
        }

        function goNextFrom(step) {
            if (step === 1 && !runValidator('validateStep1')) return;
            if (step === 2 && !runValidator('validateStep2')) return;
            if (step === 3 && !runValidator('validateStep3')) return;
            if (step === 4 && !runValidator('validateStep4')) return;

            var next = typeof options.resolveNextStep === 'function'
                ? Number(options.resolveNextStep(step))
                : step + 1;
            if (!next || next < 1) next = step + 1;

            var effectiveSubmit = typeof options.getSubmitStep === 'function'
                ? Number(options.getSubmitStep())
                : submitStep;

            if (next >= effectiveSubmit && typeof options.collectSummaryData === 'function') {
                populateBookingSummary(options.collectSummaryData());
            }
            setStep(Math.min(next, totalPanels));
        }

        function goBackFrom(step) {
            var to = typeof options.resolveBackStep === 'function'
                ? Number(options.resolveBackStep(step))
                : step - 1;
            if (!to || to < 1) to = Math.max(1, step - 1);
            setStep(to);
        }

        document.querySelectorAll('[data-wizard-next]').forEach(function (btn) {
            btn.addEventListener('click', function () {
                var from = Number(btn.getAttribute('data-wizard-next') || currentStep);
                goNextFrom(from);
            });
        });

        document.querySelectorAll('[data-wizard-back]').forEach(function (btn) {
            btn.addEventListener('click', function () {
                if (typeof options.resolveBackStep === 'function') {
                    goBackFrom(currentStep);
                    return;
                }
                var to = Number(btn.getAttribute('data-wizard-back') || (currentStep - 1));
                setStep(Math.max(1, to));
            });
        });

        var continueBtn = document.getElementById(options.continueBtnId || 'continueToSummaryBtn');
        if (continueBtn && !continueBtn.hasAttribute('data-wizard-next')) {
            continueBtn.addEventListener('click', function () {
                goNextFrom(1);
            });
        }

        var summaryBackBtn = document.getElementById('summaryBackBtn');
        if (summaryBackBtn) {
            summaryBackBtn.addEventListener('click', function (e) {
                if (typeof options.resolveBackStep === 'function') {
                    e.preventDefault();
                    goBackFrom(currentStep);
                } else if (!summaryBackBtn.hasAttribute('data-wizard-back')) {
                    var effectiveSubmit = typeof options.getSubmitStep === 'function'
                        ? Number(options.getSubmitStep())
                        : submitStep;
                    setStep(Math.max(1, effectiveSubmit - 1));
                }
            });
        }

        document.querySelectorAll('[data-summary-edit]').forEach(function (btn) {
            btn.addEventListener('click', function () {
                var target = Number(btn.getAttribute('data-summary-edit-step') || 1);
                if (typeof options.resolveSummaryEditStep === 'function') {
                    target = Number(options.resolveSummaryEditStep(btn, target));
                }
                setStep(target);
            });
        });

        if (form) {
            form.addEventListener('submit', function (e) {
                var effectiveSubmit = typeof options.getSubmitStep === 'function'
                    ? Number(options.getSubmitStep())
                    : submitStep;
                if (currentStep !== effectiveSubmit) {
                    e.preventDefault();
                    goNextFrom(currentStep);
                }
            });
        }

        setStep(1);
        return { setStep: setStep, populateBookingSummary: populateBookingSummary, getCurrentStep: function () { return currentStep; } };
    }

    function clearImagePreview(imgEl) {
        if (!imgEl) return;
        imgEl.removeAttribute('src');
        imgEl.alt = '';
        imgEl.classList.add('d-none');
    }

    function showImagePreview(file, imgEl) {
        if (!imgEl) return;
        if (!file || !(file.type && file.type.indexOf('image/') === 0)) {
            clearImagePreview(imgEl);
            return;
        }

        var reader = new FileReader();
        reader.onload = function (e) {
            imgEl.src = e.target.result;
            imgEl.alt = file.name;
            imgEl.classList.remove('d-none');
        };
        reader.readAsDataURL(file);
    }

    function initReceiptUpload() {
        var fileInput = document.getElementById('input-ReceiptFile');
        var emptyState = document.getElementById('receiptUploadEmpty');
        var zone = document.getElementById('receiptUploadZone');
        var previewState = document.getElementById('receiptUploadPreview');
        var previewBox = document.getElementById('receiptPreviewBox');
        var previewImg = document.getElementById('receiptPreviewImg');
        var filenameLabel = document.getElementById('receiptFilename');
        var removeBtn = document.getElementById('receiptRemoveBtn');
        if (!fileInput || !previewState) return;

        function clearReceipt() {
            fileInput.value = '';
            clearImagePreview(previewImg);
            if (filenameLabel) filenameLabel.textContent = '';
            if (emptyState) emptyState.classList.remove('d-none');
            if (zone) zone.classList.remove('d-none');
            previewState.classList.remove('is-visible');
        }

        function showReceipt(file) {
            if (!file) return;
            if (filenameLabel) filenameLabel.textContent = file.name;
            if (zone) zone.classList.add('d-none');
            if (emptyState) emptyState.classList.add('d-none');
            previewState.classList.add('is-visible');
            showImagePreview(file, previewImg);
        }

        fileInput.addEventListener('change', function () {
            if (fileInput.files && fileInput.files[0]) showReceipt(fileInput.files[0]);
            else clearReceipt();
        });

        if (previewBox) {
            previewBox.addEventListener('click', function () {
                fileInput.click();
            });
        }

        if (removeBtn) {
            removeBtn.addEventListener('click', function (e) {
                e.preventDefault();
                e.stopPropagation();
                clearReceipt();
            });
        }
    }

    window.BookingSummary = {
        populate: populateBookingSummary,
        initWizard: initBookingWizard,
        initReceiptUpload: initReceiptUpload,
        showImagePreview: showImagePreview,
        clearImagePreview: clearImagePreview,
        calcHours: calcHours,
        formatPeso: formatPeso
    };
})(window);
