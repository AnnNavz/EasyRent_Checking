(function () {
    var stack = document.getElementById('clientFlashStack');
    var hideDurationMs = 220;

    function getStack() {
        return document.getElementById('clientFlashStack');
    }

    function dismissFlash(node) {
        if (!node || node.classList.contains('is-hiding')) return;

        node.classList.add('is-hiding');
        window.setTimeout(function () {
            node.remove();
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

        window.setTimeout(function () {
            dismissFlash(node);
        }, 6000);
    }

    if (stack) {
        stack.querySelectorAll('.client-flash').forEach(bindFlash);
    }

    window.showClientFlash = function (message, type) {
        type = type || 'success';
        if (!message) return;

        var currentStack = getStack();
        if (!currentStack) {
            currentStack = document.createElement('div');
            currentStack.id = 'clientFlashStack';
            currentStack.className = 'client-flash-stack';
            currentStack.setAttribute('aria-live', 'polite');
            document.body.appendChild(currentStack);
        }

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
    };
})();
