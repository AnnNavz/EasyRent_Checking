(function () {
    'use strict';

    var addressData = null;
    var pickerInstances = [];

    function $(selector, root) {
        return (root || document).querySelector(selector);
    }

    function $all(selector, root) {
        return Array.prototype.slice.call((root || document).querySelectorAll(selector));
    }

    function createEmptyState() {
        return {
            city: null,
            barangay: null,
            postalCode: '',
            street: ''
        };
    }

    function formatAddress(state, meta) {
        if (!state.city || !state.barangay || !state.postalCode || !state.street.trim()) {
            return '';
        }

        return state.street.trim() + ', ' +
            state.barangay + ', ' +
            state.city.name + ', ' +
            meta.province + ' ' + state.postalCode + ', ' +
            meta.region;
    }

    function formatSummary(state) {
        var parts = [];
        if (state.city) parts.push(state.city.name);
        if (state.barangay) parts.push(state.barangay);
        if (state.postalCode) parts.push(state.postalCode);
        if (state.street.trim()) parts.push(state.street.trim());
        return parts.join(', ');
    }

    function findOutputInput(root) {
        var targetId = root.dataset.hiddenTarget;
        if (targetId) {
            var byId = document.getElementById(targetId);
            if (byId) {
                return byId;
            }
        }

        return root.parentElement ? root.parentElement.querySelector('.ph-address-output') : null;
    }

    function initPicker(root) {
        var outputInput = findOutputInput(root);
        if (!outputInput || !addressData) {
            return null;
        }

        var trigger = $('.ph-address-trigger', root);
        var panel = $('.ph-address-panel', root);
        var triggerText = $('.ph-address-trigger-text', root);
        var tabs = $all('.ph-address-tab', root);
        var panes = $all('.ph-address-pane', root);
        var cityList = $('[data-list="city"]', root);
        var barangayList = $('[data-list="barangay"]', root);
        var postalInput = $('.ph-address-input[id$="-postal"]', root);
        var streetInput = $('.ph-address-input[id$="-street"]', root);
        var confirmBtn = $('.ph-address-confirm-btn', root);
        var postalNextBtn = $('[data-next="street"]', root);

        var state = createEmptyState();
        var isOpen = false;

        function renderCityList(filter) {
            cityList.innerHTML = '';
            var query = (filter || '').toLowerCase();
            addressData.cities.forEach(function (city) {
                if (query && city.name.toLowerCase().indexOf(query) === -1) {
                    return;
                }
                var item = document.createElement('li');
                var btn = document.createElement('button');
                btn.type = 'button';
                btn.className = 'ph-address-option';
                btn.textContent = city.name;
                btn.dataset.value = city.code;
                if (state.city && state.city.code === city.code) {
                    btn.classList.add('is-selected');
                }
                btn.addEventListener('click', function () {
                    selectCity(city);
                });
                item.appendChild(btn);
                cityList.appendChild(item);
            });
        }

        function renderBarangayList() {
            barangayList.innerHTML = '';
            if (!state.city) {
                return;
            }

            state.city.barangays.forEach(function (name) {
                var item = document.createElement('li');
                var btn = document.createElement('button');
                btn.type = 'button';
                btn.className = 'ph-address-option';
                btn.textContent = name;
                if (state.barangay === name) {
                    btn.classList.add('is-selected');
                }
                btn.addEventListener('click', function () {
                    selectBarangay(name);
                });
                item.appendChild(btn);
                barangayList.appendChild(item);
            });
        }

        function setTab(tabName) {
            tabs.forEach(function (tab) {
                var active = tab.dataset.tab === tabName;
                tab.classList.toggle('is-active', active);
                tab.setAttribute('aria-selected', active ? 'true' : 'false');
            });

            panes.forEach(function (pane) {
                var active = pane.dataset.pane === tabName;
                pane.classList.toggle('is-active', active);
                pane.hidden = !active;
            });
        }

        function updateTabAvailability() {
            tabs.forEach(function (tab) {
                var tabName = tab.dataset.tab;
                var enabled = tabName === 'city' ||
                    (tabName === 'barangay' && !!state.city) ||
                    (tabName === 'postal' && !!state.barangay) ||
                    (tabName === 'street' && !!state.postalCode);
                tab.disabled = !enabled;
            });
        }

        function readInputsIntoState() {
            state.postalCode = postalInput.value.replace(/\D/g, '').slice(0, 4);
            postalInput.value = state.postalCode;
            state.street = streetInput.value.trim();
        }

        function syncOutput() {
            readInputsIntoState();
            var formatted = formatAddress(state, addressData);
            if (formatted) {
                outputInput.value = formatted;
            }
            outputInput.dispatchEvent(new Event('change', { bubbles: true }));
            outputInput.dispatchEvent(new Event('input', { bubbles: true }));
        }

        function updateTrigger() {
            readInputsIntoState();
            var summary = formatSummary(state);
            if (summary) {
                triggerText.textContent = summary;
                triggerText.classList.remove('is-placeholder');
            } else if (outputInput.value.trim()) {
                triggerText.textContent = outputInput.value.trim();
                triggerText.classList.remove('is-placeholder');
            } else {
                triggerText.textContent = 'City / Municipality, Barangay, Postal Code, Street Address';
                triggerText.classList.add('is-placeholder');
            }
        }

        function hasValidOutput() {
            return outputInput.value.trim().length > 0;
        }

        function isPickerComplete() {
            readInputsIntoState();
            return !!(state.city && state.barangay && state.postalCode.length === 4 && state.street);
        }

        function selectCity(city) {
            state.city = city;
            state.barangay = null;
            state.postalCode = '';
            state.street = '';
            postalInput.value = '';
            streetInput.value = '';
            renderCityList();
            renderBarangayList();
            updateTabAvailability();
            setTab('barangay');
            updateTrigger();
            syncOutput();
        }

        function selectBarangay(name) {
            state.barangay = name;
            renderBarangayList();
            updateTabAvailability();
            setTab('postal');
            postalInput.focus();
            updateTrigger();
            syncOutput();
        }

        function openPanel() {
            isOpen = true;
            panel.hidden = false;
            trigger.setAttribute('aria-expanded', 'true');
            root.classList.add('is-open');
            renderCityList();
            renderBarangayList();
            if (!state.city) {
                setTab('city');
            } else if (!state.barangay) {
                setTab('barangay');
            } else if (!state.postalCode) {
                setTab('postal');
            } else {
                setTab('street');
            }
        }

        function closePanel() {
            isOpen = false;
            panel.hidden = true;
            trigger.setAttribute('aria-expanded', 'false');
            root.classList.remove('is-open');
        }

        function confirmAddress() {
            readInputsIntoState();
            if (!isPickerComplete()) {
                if (!state.city) setTab('city');
                else if (!state.barangay) setTab('barangay');
                else if (state.postalCode.length !== 4) setTab('postal');
                else setTab('street');
                return false;
            }

            syncOutput();
            updateTrigger();
            closePanel();
            return true;
        }

        function finalize() {
            readInputsIntoState();
            if (isPickerComplete()) {
                syncOutput();
            }
            updateTrigger();
            return hasValidOutput();
        }

        trigger.addEventListener('click', function () {
            if (isOpen) {
                closePanel();
            } else {
                openPanel();
            }
        });

        tabs.forEach(function (tab) {
            tab.addEventListener('click', function () {
                if (tab.disabled) {
                    return;
                }
                setTab(tab.dataset.tab);
            });
        });

        postalInput.addEventListener('input', function () {
            updateTabAvailability();
            updateTrigger();
            syncOutput();
        });

        postalNextBtn.addEventListener('click', function () {
            readInputsIntoState();
            if (state.postalCode.length !== 4) {
                postalInput.focus();
                return;
            }
            updateTabAvailability();
            setTab('street');
            streetInput.focus();
        });

        streetInput.addEventListener('input', function () {
            state.street = streetInput.value;
            updateTrigger();
            syncOutput();
        });

        streetInput.addEventListener('keydown', function (event) {
            if (event.key === 'Enter') {
                event.preventDefault();
                confirmAddress();
            }
        });

        confirmBtn.addEventListener('click', confirmAddress);

        document.addEventListener('click', function (event) {
            if (!isOpen) {
                return;
            }
            if (!root.contains(event.target)) {
                readInputsIntoState();
                syncOutput();
                closePanel();
            }
        });

        document.addEventListener('keydown', function (event) {
            if (event.key === 'Escape' && isOpen) {
                closePanel();
            }
        });

        renderCityList();
        updateTabAvailability();

        if (outputInput.value) {
            restoreFromValue(outputInput.value);
            updateTrigger();
        }

        function restoreFromValue(value) {
            var match = value.match(/^(.+),\s*([^,]+),\s*([^,]+),\s*Cebu\s*(\d{4}),\s*(.+)$/i);
            if (!match) {
                return;
            }

            state.street = match[1].trim();
            state.barangay = match[2].trim();
            var cityName = match[3].trim();
            state.postalCode = match[4].trim();
            state.city = addressData.cities.find(function (c) { return c.name === cityName; }) || null;

            streetInput.value = state.street;
            postalInput.value = state.postalCode;
            renderBarangayList();
            updateTabAvailability();
        }

        return {
            root: root,
            outputInput: outputInput,
            finalize: finalize,
            openPanel: openPanel
        };
    }

    function finalizeAllPickers() {
        pickerInstances.forEach(function (picker) {
            picker.finalize();
        });
    }

    function bindFormSubmit() {
        var form = document.getElementById('reservationForm');
        if (!form) {
            return;
        }

        var submitBtn = form.querySelector('.client-reservation-btn-continue');
        if (submitBtn) {
            submitBtn.addEventListener('mousedown', finalizeAllPickers, true);
            submitBtn.addEventListener('click', finalizeAllPickers, true);
        }

        form.addEventListener('submit', function (event) {
            finalizeAllPickers();

            var firstEmpty = pickerInstances.find(function (picker) {
                return !picker.outputInput.value.trim();
            });

            if (firstEmpty) {
                event.preventDefault();
                event.stopImmediatePropagation();
                firstEmpty.openPanel();
            }
        }, true);
    }

    function enableManualFallback() {
        $all('.ph-address-output').forEach(function (input) {
            input.readOnly = false;
            input.placeholder = 'Enter full pickup/drop-off address manually';
        });

        $all('[data-ph-address-picker]').forEach(function (picker) {
            var note = document.createElement('p');
            note.className = 'text-danger small mt-1';
            note.textContent = 'Address list unavailable â€” please type the address in the field above.';
            picker.appendChild(note);
        });
    }

    async function boot() {
        var pickers = $all('[data-ph-address-picker]');
        if (!pickers.length) {
            return;
        }

        bindFormSubmit();

        try {
            var response = await fetch('/data/cebu-addresses.json');
            if (!response.ok) {
                throw new Error('Could not load address data');
            }
            addressData = await response.json();
            pickers.forEach(function (pickerEl) {
                var instance = initPicker(pickerEl);
                if (instance) {
                    pickerInstances.push(instance);
                }
            });
        } catch (error) {
            console.error(error);
            enableManualFallback();
        }
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', boot);
    } else {
        boot();
    }
})();
