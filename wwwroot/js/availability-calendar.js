(function (window) {
    'use strict';

    var MONTHS = ['January', 'February', 'March', 'April', 'May', 'June',
        'July', 'August', 'September', 'October', 'November', 'December'];

    function byId(id) {
        return id ? document.getElementById(id) : null;
    }

    function resolveEl(value, fallbackId) {
        if (value && value.nodeType) return value;
        if (typeof value === 'string') return byId(value);
        return byId(fallbackId);
    }

    function parseDate(value) {
        if (!value) return null;
        var s = String(value).trim();
        var parts = s.split('-');
        // Prefer ISO yyyy-MM-dd (calendar + invariant posts).
        if (parts.length === 3 && parts[0].length === 4) {
            var isoYear = +parts[0];
            var isoMonth = +parts[1];
            var isoDay = +parts[2];
            var iso = new Date(isoYear, isoMonth - 1, isoDay);
            if (isNaN(iso.getTime()) || isoYear < 2000) return null;
            // Reject impossible dates like 02/31 that JS rolls forward.
            if (iso.getFullYear() !== isoYear || iso.getMonth() !== isoMonth - 1 || iso.getDate() !== isoDay) {
                return null;
            }
            return iso;
        }
        // Fallback for typed/display values (M/d/yyyy or mm/dd/yyyy).
        parts = s.split('/');
        if (parts.length === 3) {
            var yearToken = String(parts[2] || '');
            var year = +yearToken;
            var month = +parts[0];
            var day = +parts[1];
            // Only expand real 2-digit years (e.g. 26 → 2026). Do NOT turn 0001 into 2001.
            if (yearToken.length <= 2 && year >= 0 && year < 100) year += 2000;
            if (year < 2000 || month < 1 || month > 12 || day < 1 || day > 31) return null;
            var local = new Date(year, month - 1, day);
            if (isNaN(local.getTime())) return null;
            if (local.getFullYear() !== year || local.getMonth() !== month - 1 || local.getDate() !== day) {
                return null;
            }
            return local;
        }
        return null;
    }

    function formatDate(date) {
        return date.getFullYear() + '-' +
            String(date.getMonth() + 1).padStart(2, '0') + '-' +
            String(date.getDate()).padStart(2, '0');
    }

    function formatDisplayDate(date) {
        if (!date) return '';
        return String(date.getMonth() + 1).padStart(2, '0') + '/' +
            String(date.getDate()).padStart(2, '0') + '/' +
            date.getFullYear();
    }

    function startOfDay(date) {
        return new Date(date.getFullYear(), date.getMonth(), date.getDate());
    }

    function sameDay(a, b) {
        return a && b && startOfDay(a).getTime() === startOfDay(b).getTime();
    }

    function todayKey() {
        return formatDate(new Date());
    }

    function isUnsetDate(date) {
        return !date || date.getFullYear() < 2000;
    }

    function init(options) {
        options = options || {};

        var pickupDateInput = resolveEl(options.pickupDateInput, 'input-PickupDate');
        var returnDateInput = resolveEl(options.returnDateInput, 'input-ReturnDate');
        var pickupDisplay = resolveEl(options.pickupDisplay, 'pickupDisplay');
        var returnDisplay = resolveEl(options.returnDisplay, 'returnDisplay');
        var calGrid = resolveEl(options.calGrid, 'calGrid');
        var calMonthLabel = resolveEl(options.calMonthLabel, 'calMonthLabel');
        var calPrev = resolveEl(options.calPrev, 'calPrev');
        var calNext = resolveEl(options.calNext, 'calNext');
        var vehicleInput = resolveEl(options.vehicleInput, 'input-VehicleId');
        var availabilityUrl = options.availabilityUrl || '/Rentals/Availability';

        if (!pickupDateInput || !returnDateInput || !calGrid || !calMonthLabel) {
            return null;
        }

        var viewYear;
        var viewMonth;
        var hoverDate = null;
        var selectionPhase = 'start';
        var unavailableDates = new Set();

        function isPast(date) {
            return formatDate(startOfDay(date)) < todayKey();
        }

        function isRented(date) {
            return !isPast(date) && unavailableDates.has(formatDate(startOfDay(date)));
        }

        function isBlocked(date) {
            return isPast(date) || unavailableDates.has(formatDate(startOfDay(date)));
        }

        function rangeHasRented(start, end) {
            if (!start || !end) return false;
            var cursor = startOfDay(start);
            var last = startOfDay(end);
            if (cursor > last) {
                var tmp = cursor;
                cursor = last;
                last = tmp;
            }
            while (cursor <= last) {
                if (isRented(cursor)) return true;
                cursor = new Date(cursor.getFullYear(), cursor.getMonth(), cursor.getDate() + 1);
            }
            return false;
        }

        function applyUnavailableDates(dates) {
            unavailableDates = new Set();
            (dates || []).forEach(function (d) {
                var key = String(d).substring(0, 10);
                if (/^\d{4}-\d{2}-\d{2}$/.test(key)) {
                    unavailableDates.add(key);
                }
            });
        }

        function getVehicleId() {
            if (typeof options.getVehicleId === 'function') {
                return String(options.getVehicleId() || '').trim();
            }
            if (!vehicleInput) return '';
            var id = String(vehicleInput.value || '').trim();
            return id === '0' ? '' : id;
        }

        function syncDisplays() {
            if (pickupDisplay) {
                var pickup = parseDate(pickupDateInput.value);
                pickupDisplay.value = pickup && !isUnsetDate(pickup) ? formatDate(pickup) : '';
            }
            if (returnDisplay) {
                var ret = parseDate(returnDateInput.value);
                returnDisplay.value = ret && !isUnsetDate(ret) ? formatDate(ret) : '';
            }
            if (typeof options.onDatesChange === 'function') {
                options.onDatesChange();
            }
        }

        function getPreviewRange() {
            var pickup = parseDate(pickupDateInput.value);
            var ret = parseDate(returnDateInput.value);
            if (selectionPhase === 'end' && pickup && hoverDate && !isBlocked(hoverDate)) {
                var start = hoverDate < pickup ? hoverDate : pickup;
                var end = hoverDate < pickup ? pickup : hoverDate;
                if (rangeHasRented(start, end)) return { start: pickup, end: pickup };
                return { start: start, end: end };
            }
            if (pickup && ret) return { start: pickup, end: ret };
            if (pickup) return { start: pickup, end: pickup };
            return { start: null, end: null };
        }

        function renderCalendar() {
            calMonthLabel.textContent = MONTHS[viewMonth] + ' ' + viewYear;
            calGrid.innerHTML = '';
            var startWeekday = new Date(viewYear, viewMonth, 1).getDay();
            var daysInMonth = new Date(viewYear, viewMonth + 1, 0).getDate();
            var range = getPreviewRange();
            var i;
            var day;

            for (i = 0; i < startWeekday; i++) {
                var empty = document.createElement('div');
                empty.className = 'airbnb-cal-cell is-empty';
                calGrid.appendChild(empty);
            }

            for (day = 1; day <= daysInMonth; day++) {
                (function (date) {
                    var cell = document.createElement('button');
                    cell.type = 'button';
                    cell.className = 'airbnb-cal-cell';
                    cell.innerHTML = '<span class="airbnb-cal-day">' + date.getDate() + '</span>';

                    if (isPast(date)) {
                        cell.classList.add('is-past');
                        cell.disabled = true;
                        cell.setAttribute('aria-disabled', 'true');
                    } else if (isRented(date)) {
                        cell.classList.add('is-rented');
                        cell.disabled = true;
                        cell.setAttribute('aria-disabled', 'true');
                        cell.title = 'Vehicle already rented';
                    } else {
                        cell.classList.add('is-available');
                    }

                    if (!cell.disabled && range.start && range.end) {
                        var t = startOfDay(date).getTime();
                        var startT = startOfDay(range.start).getTime();
                        var endT = startOfDay(range.end).getTime();
                        if (t >= startT && t <= endT) {
                            cell.classList.add('in-range');
                            if (t === startT) cell.classList.add('is-selected', 'is-pickup');
                            if (t === endT) cell.classList.add('is-selected', 'is-return');
                            if (t === startT && t === endT) cell.classList.add('is-single');
                        }
                    }

                    cell.addEventListener('click', function () {
                        if (!cell.disabled) onDayClick(date);
                    });
                    cell.addEventListener('mouseenter', function () {
                        if (cell.disabled || selectionPhase !== 'end') return;
                        if (hoverDate && sameDay(hoverDate, date)) return;
                        hoverDate = date;
                        renderCalendar();
                    });
                    calGrid.appendChild(cell);
                })(new Date(viewYear, viewMonth, day));
            }
        }

        function onDayClick(date) {
            var clicked = startOfDay(date);
            if (isBlocked(clicked)) return;
            var pickup = parseDate(pickupDateInput.value);

            if (selectionPhase === 'start' || !pickup) {
                pickupDateInput.value = formatDate(clicked);
                returnDateInput.value = '';
                selectionPhase = 'end';
                hoverDate = null;
            } else {
                var start = clicked < startOfDay(pickup) ? clicked : startOfDay(pickup);
                var end = clicked < startOfDay(pickup) ? startOfDay(pickup) : clicked;
                if (rangeHasRented(start, end)) {
                    alert('Selected range includes dates when this vehicle is already rented.');
                    return;
                }
                pickupDateInput.value = formatDate(start);
                returnDateInput.value = formatDate(end);
                selectionPhase = 'start';
                hoverDate = null;
            }
            syncDisplays();
            renderCalendar();
        }

        function bindChrome() {
            calGrid.addEventListener('mouseleave', function () {
                if (selectionPhase === 'end' && hoverDate) {
                    hoverDate = null;
                    renderCalendar();
                }
            });

            if (calPrev) {
                calPrev.addEventListener('click', function () {
                    viewMonth -= 1;
                    if (viewMonth < 0) { viewMonth = 11; viewYear -= 1; }
                    renderCalendar();
                });
            }
            if (calNext) {
                calNext.addEventListener('click', function () {
                    viewMonth += 1;
                    if (viewMonth > 11) { viewMonth = 0; viewYear += 1; }
                    renderCalendar();
                });
            }
            if (pickupDisplay) {
                pickupDisplay.type = 'date';
                pickupDisplay.min = todayKey();
                pickupDisplay.addEventListener('focus', function () { selectionPhase = 'start'; });
                pickupDisplay.addEventListener('change', function () { commitTypedDate('pickup'); });
            }
            if (returnDisplay) {
                returnDisplay.type = 'date';
                returnDisplay.min = todayKey();
                returnDisplay.addEventListener('focus', function () {
                    if (pickupDateInput.value) selectionPhase = 'end';
                });
                returnDisplay.addEventListener('change', function () { commitTypedDate('return'); });
            }
        }

        function commitTypedDate(which) {
            var display = which === 'pickup' ? pickupDisplay : returnDisplay;
            if (!display) return;

            var raw = String(display.value || '').trim();
            if (!raw) {
                if (which === 'pickup') {
                    pickupDateInput.value = '';
                    returnDateInput.value = '';
                    selectionPhase = 'start';
                } else {
                    returnDateInput.value = '';
                    selectionPhase = pickupDateInput.value ? 'end' : 'start';
                }
                syncDisplays();
                renderCalendar();
                return;
            }

            var typed = parseDate(raw);
            if (!typed) {
                alert('Please choose a valid date.');
                syncDisplays();
                return;
            }

            var day = startOfDay(typed);
            if (isPast(day)) {
                alert('Please choose a date that is today or later.');
                syncDisplays();
                return;
            }
            if (isRented(day)) {
                alert('That date is unavailable because the vehicle is already rented.');
                syncDisplays();
                return;
            }

            if (which === 'pickup') {
                var existingReturn = parseDate(returnDateInput.value);
                pickupDateInput.value = formatDate(day);
                if (existingReturn && startOfDay(existingReturn) < day) {
                    returnDateInput.value = '';
                    selectionPhase = 'end';
                } else if (existingReturn && rangeHasRented(day, startOfDay(existingReturn))) {
                    alert('Selected range includes dates when this vehicle is already rented.');
                    returnDateInput.value = '';
                    selectionPhase = 'end';
                } else if (existingReturn) {
                    selectionPhase = 'start';
                } else {
                    selectionPhase = 'end';
                }
                viewYear = day.getFullYear();
                viewMonth = day.getMonth();
                if (returnDisplay) {
                    returnDisplay.min = formatDate(day);
                }
            } else {
                var pickup = parseDate(pickupDateInput.value);
                if (!pickup) {
                    alert('Select a pick-up date first.');
                    syncDisplays();
                    return;
                }
                var start = day < startOfDay(pickup) ? day : startOfDay(pickup);
                var end = day < startOfDay(pickup) ? startOfDay(pickup) : day;
                if (rangeHasRented(start, end)) {
                    alert('Selected range includes dates when this vehicle is already rented.');
                    syncDisplays();
                    return;
                }
                pickupDateInput.value = formatDate(start);
                returnDateInput.value = formatDate(end);
                selectionPhase = 'start';
                viewYear = end.getFullYear();
                viewMonth = end.getMonth();
            }

            hoverDate = null;
            syncDisplays();
            renderCalendar();
        }

        function start() {
            applyUnavailableDates(options.initialUnavailable || []);
            var pickup = parseDate(pickupDateInput.value);
            var ret = parseDate(returnDateInput.value);
            var today = new Date();

            if (isUnsetDate(pickup)) {
                pickupDateInput.value = '';
                returnDateInput.value = '';
                selectionPhase = 'start';
            } else {
                // Normalize any culture-formatted values back to yyyy-MM-dd so they survive step changes.
                pickupDateInput.value = formatDate(pickup);
                if (isUnsetDate(ret)) {
                    returnDateInput.value = '';
                    selectionPhase = 'end';
                } else {
                    returnDateInput.value = formatDate(ret);
                    selectionPhase = 'start';
                }
            }

            // Open on selected pickup month, otherwise the current month (never DateOnly default / 2001).
            var base = parseDate(pickupDateInput.value);
            if (!base || isUnsetDate(base)) {
                base = today;
            }
            viewYear = base.getFullYear();
            viewMonth = base.getMonth();
            bindChrome();
            if (returnDisplay) {
                var pickupForMin = parseDate(pickupDateInput.value);
                returnDisplay.min = pickupForMin && !isUnsetDate(pickupForMin)
                    ? formatDate(pickupForMin)
                    : todayKey();
            }
            syncDisplays();
            renderCalendar();
            loadAvailability();
        }

        function loadAvailability() {
            var vehicleId = getVehicleId();
            if (!vehicleId) {
                unavailableDates = new Set();
                renderCalendar();
                return Promise.resolve();
            }

            return fetch(availabilityUrl + '?vehicleId=' + encodeURIComponent(vehicleId), {
                headers: { 'Accept': 'application/json' },
                credentials: 'same-origin'
            }).then(function (response) {
                if (!response.ok) throw new Error('Failed to load availability');
                var contentType = response.headers.get('content-type') || '';
                if (contentType.indexOf('application/json') === -1) {
                    throw new Error('Availability response was not JSON');
                }
                return response.json();
            }).then(function (data) {
                applyUnavailableDates(data.unavailableDates || data.UnavailableDates || []);
            }).catch(function (err) {
                console.warn('Availability load failed:', err);
            }).then(function () {
                renderCalendar();
            });
        }

        function resetSelection() {
            pickupDateInput.value = '';
            returnDateInput.value = '';
            selectionPhase = 'start';
            hoverDate = null;
            syncDisplays();
            renderCalendar();
        }

        start();

        return {
            parseDate: parseDate,
            rangeHasRented: rangeHasRented,
            loadAvailability: loadAvailability,
            resetSelection: resetSelection,
            render: renderCalendar
        };
    }

    window.AvailabilityCalendar = {
        init: init,
        parseDate: parseDate
    };
})(window);
