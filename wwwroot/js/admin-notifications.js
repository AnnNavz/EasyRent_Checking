(function () {
    var panel = document.getElementById('adminNotifyPanel');
    var overlay = document.getElementById('adminNotifyOverlay');
    var toggle = document.getElementById('adminNotifyToggle');
    var closeBtn = document.getElementById('adminNotifyClose');
    var bodyEl = document.getElementById('adminNotifyBody');
    var badge = document.getElementById('adminNotifyBadge');
    if (!panel || !overlay || !toggle || !bodyEl) return;

    var storageKey = 'easyrent-admin-notif-read';
    var feedUrl = toggle.getAttribute('data-feed-url') || '/Notifications/Feed';
    var feed = { critical: [], items: [] };
    var activeTab = 'all';
    var loaded = false;

    function readIds() {
        try {
            var raw = localStorage.getItem(storageKey);
            var parsed = raw ? JSON.parse(raw) : [];
            return Array.isArray(parsed) ? parsed : [];
        } catch (e) {
            return [];
        }
    }

    function writeIds(ids) {
        try { localStorage.setItem(storageKey, JSON.stringify(ids)); } catch (e) { }
    }

    function allIds() {
        return feed.critical.concat(feed.items).map(function (item) { return item.id; });
    }

    function isUnread(id) {
        return readIds().indexOf(id) === -1;
    }

    function unreadCount() {
        return allIds().filter(isUnread).length;
    }

    function updateBadge() {
        if (!badge) return;
        var hasUnread = unreadCount() > 0;
        badge.hidden = !hasUnread;
        badge.textContent = '';
    }

    function markRead(id) {
        var ids = readIds();
        if (ids.indexOf(id) === -1) {
            ids.push(id);
            writeIds(ids);
        }
        updateBadge();
        render();
    }

    function markAllRead() {
        var ids = readIds();
        allIds().forEach(function (id) {
            if (ids.indexOf(id) === -1) ids.push(id);
        });
        writeIds(ids);
        updateBadge();
        render();
    }

    function iconClass(item) {
        return 'admin-notify-icon is-' + (item.tone || 'system');
    }

    function itemHtml(item) {
        var unread = isUnread(item.id);
        var iconWrap = item.tone === 'danger'
            ? '<span class="' + iconClass(item) + '" aria-hidden="true"><i class="bi ' + item.icon + '"></i></span>'
            : '<span class="' + iconClass(item) + '" aria-hidden="true"><i class="bi ' + item.icon + '"></i></span>';
        return (
            '<a class="admin-notify-item" href="' + item.url + '" data-notify-id="' + item.id + '">' +
                iconWrap +
                '<span class="admin-notify-copy">' +
                    '<span class="admin-notify-title"></span>' +
                    '<span class="admin-notify-detail"></span>' +
                '</span>' +
                '<span class="admin-notify-meta">' +
                    '<span class="admin-notify-time"></span>' +
                    (unread ? '<span class="admin-notify-unread" aria-label="Unread"></span>' : '') +
                '</span>' +
                '<i class="bi bi-chevron-right admin-notify-chevron" aria-hidden="true"></i>' +
            '</a>'
        );
    }

    function fillText(root) {
        root.querySelectorAll('.admin-notify-item').forEach(function (el) {
            var id = el.getAttribute('data-notify-id');
            var item = feed.critical.concat(feed.items).filter(function (x) { return x.id === id; })[0];
            if (!item) return;
            el.querySelector('.admin-notify-title').textContent = item.title;
            el.querySelector('.admin-notify-detail').textContent = item.detail;
            el.querySelector('.admin-notify-time').textContent = item.timeAgo;
        });
    }

    function render() {
        var critical = activeTab === 'all' ? feed.critical : [];
        var items = feed.items.filter(function (item) {
            return activeTab === 'all' || item.category === activeTab;
        });

        if (critical.length === 0 && items.length === 0) {
            bodyEl.innerHTML = '<div class="admin-notify-empty">No notifications right now.</div>';
            return;
        }

        var anyUnread = critical.some(function (item) { return isUnread(item.id); })
            || items.some(function (item) { return isUnread(item.id); });
        var markBtn = anyUnread
            ? '<button type="button" class="admin-notify-mark" id="adminNotifyMarkAll">Mark all as read</button>'
            : '';

        var html = '';
        if (critical.length > 0) {
            html += '<section class="admin-notify-section">' +
                '<div class="admin-notify-section-head">' +
                    '<h3 class="admin-notify-section-title is-critical">Critical Alerts <span class="admin-notify-count">' + critical.length + '</span></h3>' +
                    (items.length === 0 ? markBtn : '') +
                '</div>' +
                '<div class="admin-notify-critical">' +
                    critical.map(itemHtml).join('') +
                '</div>' +
            '</section>';
        }

        if (items.length > 0) {
            html += '<section class="admin-notify-section">' +
                '<div class="admin-notify-section-head">' +
                    '<h3 class="admin-notify-section-title">Recent Activities</h3>' +
                    markBtn +
                '</div>' +
                '<div class="admin-notify-list">' +
                    items.map(itemHtml).join('') +
                '</div>' +
            '</section>';
        }

        bodyEl.innerHTML = html;
        fillText(bodyEl);
    }

    function setOpen(open) {
        if (open) {
            panel.hidden = false;
            overlay.hidden = false;
            void panel.offsetWidth;
            panel.classList.add('is-open');
            if (!loaded) loadFeed();
            else render();
        } else {
            panel.classList.remove('is-open');
            setTimeout(function () {
                if (!panel.classList.contains('is-open')) {
                    panel.hidden = true;
                    overlay.hidden = true;
                }
            }, 220);
        }
        panel.setAttribute('aria-hidden', open ? 'false' : 'true');
        toggle.setAttribute('aria-expanded', open ? 'true' : 'false');
        document.body.classList.toggle('admin-notify-open', open);
    }

    function loadFeed() {
        bodyEl.innerHTML = '<div class="admin-notify-empty">Loading notifications…</div>';
        fetch(feedUrl, { credentials: 'same-origin', headers: { 'Accept': 'application/json' } })
            .then(function (res) { return res.ok ? res.json() : Promise.reject(); })
            .then(function (data) {
                feed.critical = data.critical || [];
                feed.items = data.items || [];
                loaded = true;
                updateBadge();
                if (panel.classList.contains('is-open') || !panel.hidden) render();
            })
            .catch(function () {
                bodyEl.innerHTML = '<div class="admin-notify-empty">Could not load notifications.</div>';
            });
    }

    toggle.addEventListener('click', function (e) {
        e.preventDefault();
        e.stopPropagation();
        setOpen(!panel.classList.contains('is-open'));
    });

    if (closeBtn) closeBtn.addEventListener('click', function () { setOpen(false); });
    overlay.addEventListener('click', function () { setOpen(false); });
    document.addEventListener('keydown', function (e) {
        if (e.key === 'Escape' && panel.classList.contains('is-open')) setOpen(false);
    });

    panel.addEventListener('click', function (e) {
        var mark = e.target.closest('#adminNotifyMarkAll');
        if (mark) {
            e.preventDefault();
            markAllRead();
            return;
        }
        var link = e.target.closest('a.admin-notify-item');
        if (link) markRead(link.getAttribute('data-notify-id'));
    });

    document.querySelectorAll('.admin-notify-tab').forEach(function (tab) {
        tab.addEventListener('click', function () {
            activeTab = tab.getAttribute('data-notify-tab') || 'all';
            document.querySelectorAll('.admin-notify-tab').forEach(function (btn) {
                var on = btn === tab;
                btn.classList.toggle('is-active', on);
                btn.setAttribute('aria-selected', on ? 'true' : 'false');
            });
            render();
        });
    });

    loadFeed();
})();
