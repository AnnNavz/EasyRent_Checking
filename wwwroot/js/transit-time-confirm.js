(function () {
    function formatTimeDisplay(value) {
        if (!value) {
            return '—';
        }

        var parts = value.split(':');
        if (parts.length < 2) {
            return value;
        }

        var hour = parseInt(parts[0], 10);
        var minute = parts[1];
        var suffix = hour >= 12 ? 'PM' : 'AM';
        var hour12 = hour % 12;
        if (hour12 === 0) {
            hour12 = 12;
        }

        return hour12 + ':' + minute + ' ' + suffix;
    }

    window.initTransitTimeConfirm = function (formId, timeInputId, options) {
        var form = document.getElementById(formId);
        var timeInput = document.getElementById(timeInputId);
        if (!form || !timeInput) {
            return;
        }

        options = options || {};
        var scheduledTime = (options.scheduledTime || '').trim();
        var confirmTitle = options.confirmTitle || 'Time is not aligned with the booking schedule.';
        var confirmButton = options.confirmButton || 'Continue';
        var scheduledLabel = options.scheduledLabel || 'Scheduled time';

        form.addEventListener('submit', function (event) {
            if (form.getAttribute('data-confirm-accepted') === '1') {
                return;
            }

            var enteredTime = (timeInput.value || '').trim();
            if (!scheduledTime || !enteredTime || enteredTime === scheduledTime) {
                form.removeAttribute('data-confirm');
                form.removeAttribute('data-confirm-text');
                form.removeAttribute('data-confirm-button');
                form.removeAttribute('data-confirm-tone');
                return;
            }

            event.preventDefault();
            event.stopImmediatePropagation();

            form.setAttribute('data-confirm', confirmTitle);
            form.setAttribute(
                'data-confirm-text',
                scheduledLabel + ' is ' + formatTimeDisplay(scheduledTime)
                    + ', but you entered ' + formatTimeDisplay(enteredTime)
                    + '. Do you want to continue?'
            );
            form.setAttribute('data-confirm-button', confirmButton);
            form.setAttribute('data-confirm-tone', 'warn');

            if (typeof window.openAdminConfirmForForm === 'function') {
                window.openAdminConfirmForForm(form);
                return;
            }

            if (window.confirm(form.getAttribute('data-confirm') + '\n\n' + form.getAttribute('data-confirm-text'))) {
                form.setAttribute('data-confirm-accepted', '1');
                if (typeof form.requestSubmit === 'function') {
                    form.requestSubmit();
                } else {
                    form.submit();
                }
            }
        });
    };
})();
