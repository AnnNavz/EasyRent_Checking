(function () {
    function elements() {
        return {
            select: document.getElementById('input-Type'),
            custom: document.getElementById('input-CustomType'),
            wrap: document.getElementById('customTypeWrap'),
            button: document.getElementById('btnAddType')
        };
    }

    function findOption(select, value) {
        var match = value.toLowerCase();
        return Array.prototype.find.call(select.options, function (option) {
            return option.value.toLowerCase() === match;
        });
    }

    function addAndSelectType(value) {
        var els = elements();
        if (!els.select) return false;
        var name = (value || '').trim();
        if (!name) return false;

        var existing = findOption(els.select, name);
        if (existing) {
            els.select.value = existing.value;
        } else {
            var option = document.createElement('option');
            option.value = name;
            option.textContent = name;
            option.selected = true;
            els.select.appendChild(option);
            els.select.value = name;
        }
        hideNewType();
        return true;
    }

    function showNewType() {
        var els = elements();
        if (!els.wrap || !els.custom) return;
        els.wrap.classList.remove('d-none');
        els.custom.value = '';
        els.custom.focus();
        if (els.button) els.button.textContent = 'Save';
    }

    function hideNewType() {
        var els = elements();
        if (els.wrap) els.wrap.classList.add('d-none');
        if (els.custom) els.custom.value = '';
        if (els.button) {
            els.button.innerHTML = '<i class="bi bi-plus-lg" aria-hidden="true"></i> Add';
        }
    }

    function isAdding() {
        var wrap = document.getElementById('customTypeWrap');
        return !!(wrap && !wrap.classList.contains('d-none'));
    }

    function syncVehicleType() {
        var els = elements();
        if (isAdding() && els.custom && els.custom.value.trim()) {
            addAndSelectType(els.custom.value);
        }
    }

    window.syncVehicleType = syncVehicleType;
    window.selectedVehicleTypeLabel = function () {
        var select = document.getElementById('input-Type');
        if (!select || select.selectedIndex < 0) return 'N/A';
        return select.options[select.selectedIndex]?.text || select.value || 'N/A';
    };

    document.addEventListener('DOMContentLoaded', function () {
        var els = elements();
        if (!els.select || !els.button) return;

        els.button.addEventListener('click', function () {
            if (!isAdding()) {
                showNewType();
                return;
            }
            if (!addAndSelectType(els.custom && els.custom.value)) {
                hideNewType();
            }
        });

        if (els.custom) {
            els.custom.addEventListener('keydown', function (event) {
                if (event.key === 'Enter') {
                    event.preventDefault();
                    if (!addAndSelectType(els.custom.value)) {
                        hideNewType();
                    }
                }
                if (event.key === 'Escape') {
                    event.preventDefault();
                    hideNewType();
                }
            });
        }

        var form = els.select.closest('form');
        if (form) {
            form.addEventListener('submit', syncVehicleType);
        }
    });
})();
