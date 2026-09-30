(function () {
    var stack = document.getElementById('clientFlashStack');
    var hideDurationMs = 220;

    function getStack() {
        return document.getElementById('clientFlashStack');
    }

    function clearFlashTimer(node) {
        if (node && node._flashTimeout) {
            window.clearTimeout(node._flashTimeout);
            node._flashTimeout = null;
        }
    }

    function removeFlash(node) {
        if (!node) return;
        clearFlashTimer(node);
        node.remove();
    }

    function clearAllFlashes() {
        var currentStack = getStack();
        if (!currentStack) return;

        currentStack.querySelectorAll('.client-flash').forEach(function (node) {
            removeFlash(node);
        });

        currentStack.remove();
    }

    function dismissFlash(node) {
        if (!node || node.classList.contains('is-hiding')) return;

        clearFlashTimer(node);
        node.classList.add('is-hiding');
        window.setTimeout(function () {
            removeFlash(node);
            var currentStack = getStack();
            if (currentStack && !currentStack.querySelector('.client-flash')) {
                currentStack.remove();
            }
        }, hideDurationMs);
    }

    function bindFlash(node) {
        var close = node.querySelector('.client-flash-close');
        if (close) {
            close.addEventListener('click', function () {
                dismissFlash(node);
            });
        }

        clearFlashTimer(node);
        node._flashTimeout = window.setTimeout(function () {
            dismissFlash(node);
        }, 6000);
    }

    function showClientFlash(message, type, options) {
        type = type || 'success';
        options = options || {};
        if (!message) return;

        clearAllFlashes();

        var currentStack = document.createElement('div');
        currentStack.id = 'clientFlashStack';
        currentStack.className = 'client-flash-stack';
        currentStack.setAttribute('aria-live', 'polite');
        document.body.appendChild(currentStack);

        var flash = document.createElement('div');
        flash.className = 'client-flash client-flash-' + type;
        flash.setAttribute('role', type === 'error' ? 'alert' : 'status');

        var text = document.createElement('span');
        text.className = 'client-flash-text';
        text.textContent = message;
        flash.appendChild(text);

        var closeBtn = document.createElement('button');
        closeBtn.type = 'button';
        closeBtn.className = 'client-flash-close';
        closeBtn.setAttribute('aria-label', 'Dismiss notification');
        closeBtn.innerHTML = '<i class="bi bi-x-lg" aria-hidden="true"></i>';
        flash.appendChild(closeBtn);

        currentStack.appendChild(flash);
        bindFlash(flash);

        if (options.scrollToTop !== false) {
            window.scrollTo({ top: 0, behavior: 'smooth' });
        }
    }

    if (stack) {
        stack.querySelectorAll('.client-flash').forEach(bindFlash);
    }

    window.showClientFlash = showClientFlash;

    window.showClientAlert = function (message, type) {
        showClientFlash(message, type || 'error', { scrollToTop: true });
    };

    window.alert = function (message) {
        var text = message == null ? '' : String(message).trim();
        if (!text) return;
        showClientFlash(text, 'error', { scrollToTop: true });
    };
})();
