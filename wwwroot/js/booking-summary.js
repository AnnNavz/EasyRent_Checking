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

    function calcHours(pickupDate, pickupTime, returnDate, returnTime) {
        if (!pickupDate || !returnDate) return 0;
        var start = new Date(pickupDate + 'T' + (pickupTime || '00:00'));
        var end = new Date(returnDate + 'T' + (returnTime || '00:00'));
        if (isNaN(start.getTime()) || isNaN(end.getTime()) || end < start) return 0;
        return Math.max(0, Math.round((end - start) / 3600000));
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
        var hasDiscount = data.discount === 'Yes' || data.discount === true;
        var discountAmount = hasDiscount ? subtotal * 0.10 : 0;
        var total = subtotal - discountAmount;

        text('summaryBookingDateTime', bookingLabel);
        text('summaryCustomerName', data.customerName);
        text('summaryContact', data.contactNumber);
        text('summaryPickupLocation', data.pickupLocation);
        text('summaryDropoffLocation', data.dropoffLocation);
        text('summaryPassengers', data.passengerCount ? (data.passengerCount + (Number(data.passengerCount) === 1 ? ' Passenger' : ' Passengers')) : '—');
        text('summaryDiscount', hasDiscount ? 'Senior Citizen (10% off)' : 'No');
        text('summaryVehicleName', data.vehicleName);
        text('summaryVehicleType', data.vehicleType);
        text('summaryPickupDateTime', (data.pickupDate ? formatDisplayDate(data.pickupDate) : '—') + (data.pickupTime ? ' • ' + formatDisplayTime(data.pickupTime) : ''));
        text('summaryReturnDateTime', (data.returnDate ? formatDisplayDate(data.returnDate) : '—') + (data.returnTime ? ' • ' + formatDisplayTime(data.returnTime) : ''));
        text('summaryDuration', hours === 1 ? '1 hour' : hours + ' hours');
        text('summaryValidId', hasDiscount ? (data.discountFileName || 'ID uploaded') : 'Not required');
        text('summaryNotes', data.notes || 'None');

        text('summaryBaseFee', formatPeso(basePrice));
        text('summarySucceedingLabel', succeedingHours > 0
            ? ('Succeeding Hours (' + succeedingHours + (succeedingHours === 1 ? ' hour' : ' hours') + ')')
            : 'Succeeding Hours');
        text('summarySucceedingFee', formatPeso(succeedingAmount));

        var discountRow = document.getElementById('summaryDiscountRow');
        if (discountRow) {
            discountRow.style.display = hasDiscount ? '' : 'none';
        }
        text('summaryDiscountAmount', '-' + formatPeso(discountAmount));
        text('summaryTotalCost', formatPeso(total));
    }

    function initBookingWizard(options) {
        var form = document.getElementById(options.formId || 'reservationWizardForm');
        var stepNodes = Array.prototype.slice.call(document.querySelectorAll(options.stepSelector || '.admin-booking-stepper .step-node'));
        var step1 = document.getElementById(options.step1Id || 'reservationStep1');
        var step2 = document.getElementById(options.step2Id || 'reservationStep2');
        var continueBtn = document.getElementById(options.continueBtnId || 'continueToSummaryBtn');
        var summaryBackBtn = document.getElementById('summaryBackBtn');
        var currentStep = 1;

        function setStep(step) {
            currentStep = step;
            if (step1) step1.classList.toggle('active', step === 1);
            if (step2) step2.classList.toggle('active', step === 2);

            stepNodes.forEach(function (node, index) {
                var n = index + 1;
                node.classList.toggle('active', n === step);
                node.classList.toggle('completed', n < step);
                var circle = node.querySelector('.step-circle');
                if (!circle) return;
                if (n < step) {
                    circle.innerHTML = '<i class="bi bi-check-lg"></i>';
                } else {
                    circle.textContent = String(n);
                }
            });

            if (typeof options.onStepChange === 'function') {
                options.onStepChange(step);
            }

            window.scrollTo({ top: 0, behavior: 'smooth' });
        }

        function validateStep1() {
            if (typeof options.validateStep1 === 'function') {
                return options.validateStep1();
            }
            return true;
        }

        if (continueBtn) {
            continueBtn.addEventListener('click', function () {
                if (!validateStep1()) return;
                if (typeof options.collectSummaryData === 'function') {
                    populateBookingSummary(options.collectSummaryData());
                }
                setStep(2);
            });
        }

        if (summaryBackBtn) {
            summaryBackBtn.addEventListener('click', function () {
                setStep(1);
            });
        }

        document.querySelectorAll('[data-summary-edit]').forEach(function (btn) {
            btn.addEventListener('click', function () {
                setStep(1);
            });
        });

        if (form) {
            form.addEventListener('submit', function (e) {
                if (currentStep !== 2) {
                    e.preventDefault();
                    if (!validateStep1()) return;
                    if (typeof options.collectSummaryData === 'function') {
                        populateBookingSummary(options.collectSummaryData());
                    }
                    setStep(2);
                }
            });
        }

        setStep(1);
        return { setStep: setStep, populateBookingSummary: populateBookingSummary };
    }

    window.BookingSummary = {
        populate: populateBookingSummary,
        initWizard: initBookingWizard,
        calcHours: calcHours,
        formatPeso: formatPeso
    };
})(window);
