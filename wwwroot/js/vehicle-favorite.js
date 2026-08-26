(function () {
    function token() {
        var input = document.querySelector('input[name="__RequestVerificationToken"]');
        return input ? input.value : '';
    }

    function setFavoriteUi(btn, isFavorite) {
        btn.classList.toggle('is-favorite', isFavorite);
        btn.setAttribute('aria-pressed', isFavorite ? 'true' : 'false');
        btn.setAttribute('aria-label', isFavorite ? 'Remove from favorites' : 'Save to favorites');
        var icon = btn.querySelector('i');
        if (icon) {
            icon.className = isFavorite ? 'bi bi-heart-fill' : 'bi bi-heart';
        }
    }

    document.querySelectorAll('[data-favorite-toggle]').forEach(function (btn) {
        btn.addEventListener('click', function () {
            if (btn.disabled) return;

            var loginUrl = btn.getAttribute('data-login-url');
            if (loginUrl) {
                window.location.href = loginUrl;
                return;
            }

            var url = btn.getAttribute('data-toggle-url');
            if (!url) return;

            btn.disabled = true;
            fetch(url, {
                method: 'POST',
                headers: {
                    'RequestVerificationToken': token(),
                    'Accept': 'application/json'
                }
            })
                .then(function (res) { return res.json().then(function (data) { return { ok: res.ok, data: data }; }); })
                .then(function (result) {
                    var data = result.data || {};
                    if (data.loginRequired && data.loginUrl) {
                        window.location.href = data.loginUrl;
                        return;
                    }
                    if (!result.ok || !data.ok) return;
                    setFavoriteUi(btn, !!data.isFavorite);
                    if (!data.isFavorite && btn.hasAttribute('data-remove-on-unfavorite')) {
                        var card = btn.closest('[data-favorite-card]');
                        if (card) card.remove();
                        if (!document.querySelector('[data-favorite-card]')) {
                            var empty = document.querySelector('[data-favorites-empty]');
                            var grid = document.querySelector('[data-favorites-grid]');
                            if (empty) empty.classList.remove('d-none');
                            if (grid) grid.classList.add('d-none');
                        }
                    }
                })
                .catch(function () {})
                .finally(function () { btn.disabled = false; });
        });
    });
})();
