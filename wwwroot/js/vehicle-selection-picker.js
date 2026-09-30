(function () {
    window.initVehicleSelectionPicker = function () {
        const searchInput = document.getElementById('vehicleSearchInput');
        const vehicleList = document.getElementById('vehicleSelectionList');
        const tabEmptyState = document.getElementById('vehicleTabEmptyState');

        function updateSelectedState() {
            if (!vehicleList) return;
            vehicleList.querySelectorAll('.incident-vehicle-option').forEach(function (option) {
                const input = option.querySelector('input[type="radio"]');
                option.classList.toggle('is-selected', input && input.checked);
            });
        }

        function applyVehicleFilters() {
            if (!vehicleList) return;
            const term = (searchInput?.value || '').trim().toLowerCase();
            let visibleCount = 0;

            vehicleList.querySelectorAll('.incident-vehicle-option').forEach(function (option) {
                const searchKey = option.dataset.vehicleSearch || '';
                const matchesSearch = !term || searchKey.includes(term);
                option.style.display = matchesSearch ? '' : 'none';
                if (matchesSearch) {
                    visibleCount += 1;
                }
            });

            if (tabEmptyState) {
                tabEmptyState.hidden = visibleCount > 0;
            }
        }

        if (vehicleList) {
            vehicleList.addEventListener('change', updateSelectedState);
            updateSelectedState();
        }

        if (searchInput) {
            searchInput.addEventListener('input', applyVehicleFilters);
        }

        applyVehicleFilters();
    };
})();
