(function () {
    'use strict';

    var addressData = null;

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

    function initPicker(root) {
        var hiddenInput = document.getElementById(root.dataset.hiddenTarget);
        if (!hiddenInput || !addressData) {
            return;
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

        function syncHiddenInput() {
            hiddenInput.value = formatAddress(state, addressData);
            hiddenInput.dispatchEvent(new Event('change', { bubbles: true }));
        }

        function updateTrigger() {
            var summary = formatSummary(state);
            if (summary) {
                triggerText.textContent = summary;
                triggerText.classList.remove('is-placeholder');
            } else {
                triggerText.textContent = 'City / Municipality, Barangay, Postal Code, Street Address';
                triggerText.classList.add('is-placeholder');
            }
        }

        function selectCity(city) {
            state.city = city;
            state.barangay = null;
            state.postalCode = city.postalCode || '';
            state.street = '';
            postalInput.value = state.postalCode;
            streetInput.value = '';
            renderCityList();
            renderBarangayList();
            updateTabAvailability();
            setTab('barangay');
            updateTrigger();
            syncHiddenInput();
        }

        function selectBarangay(name) {
            state.barangay = name;
            renderBarangayList();
            updateTabAvailability();
            setTab('postal');
            updateTrigger();
            syncHiddenInput();
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
            state.street = streetInput.value.trim();
            state.postalCode = postalInput.value.trim();
            if (!state.city || !state.barangay || !state.postalCode || !state.street) {
                return;
            }
            updateTrigger();
            syncHiddenInput();
            closePanel();
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
            state.postalCode = postalInput.value.replace(/\D/g, '').slice(0, 4);
            postalInput.value = state.postalCode;
            updateTabAvailability();
            updateTrigger();
            syncHiddenInput();
        });

        postalNextBtn.addEventListener('click', function () {
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
        });

        confirmBtn.addEventListener('click', confirmAddress);

        document.addEventListener('click', function (event) {
            if (!isOpen) {
                return;
            }
            if (!root.contains(event.target)) {
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

        if (hiddenInput.value) {
            restoreFromValue(hiddenInput.value);
        }

        function restoreFromValue(value) {
            var match = value.match(/^(.+),\s*([^,]+),\s*([^,]+),\s*Cebu\s*(\d{4}),\s*(.+)$/i);
            if (!match) {
                triggerText.textContent = value;
                triggerText.classList.remove('is-placeholder');
                return;
            }

            state.street = match[1].trim();
            state.barangay = match[2].trim();
            var cityName = match[3].trim();
            state.postalCode = match[4].trim();
            state.city = addressData.cities.find(function (c) { return c.name === cityName; }) || null;

            streetInput.value = state.street;
            postalInput.value = state.postalCode;
            updateTabAvailability();
            updateTrigger();
        }
    }

    async function boot() {
        var pickers = $all('[data-ph-address-picker]');
        if (!pickers.length) {
            return;
        }

        try {
            var response = await fetch('/data/cebu-addresses.json');
            if (!response.ok) {
                throw new Error('Could not load address data');
            }
            addressData = await response.json();
            pickers.forEach(initPicker);
        } catch (error) {
            console.error(error);
            pickers.forEach(function (picker) {
                var note = document.createElement('p');
                note.className = 'text-danger small mt-1';
                note.textContent = 'Address options could not be loaded. Please refresh the page.';
                picker.appendChild(note);
            });
        }
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', boot);
    } else {
        boot();
    }
})();
