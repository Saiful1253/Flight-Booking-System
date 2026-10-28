// FlyLocal – small UI helpers
document.addEventListener('DOMContentLoaded', function () {
    // Auto-dismiss alerts
    document.querySelectorAll('.alert-app .btn-close').forEach(function (btn) {
        btn.addEventListener('click', function () {
            var alert = btn.closest('.alert-app');
            if (alert) alert.style.display = 'none';
        });
    });

    // Auto-submit filter dropdowns (admin lists)
    document.querySelectorAll('select[data-autosubmit]').forEach(function (sel) {
        sel.addEventListener('change', function () { sel.form.submit(); });
    });

    /* ---------- Mobile: panel sidebar off-canvas ---------- */
    var sidebarToggle = document.getElementById('sidebarToggle');
    var sidebarBackdrop = document.getElementById('sidebarBackdrop');
    if (sidebarToggle && document.querySelector('.sidebar')) {
        var closeSidebar = function () {
            document.body.classList.remove('sidebar-open');
            sidebarToggle.setAttribute('aria-expanded', 'false');
        };
        sidebarToggle.addEventListener('click', function () {
            var open = document.body.classList.toggle('sidebar-open');
            sidebarToggle.setAttribute('aria-expanded', open ? 'true' : 'false');
        });
        if (sidebarBackdrop) sidebarBackdrop.addEventListener('click', closeSidebar);
        document.querySelectorAll('.sidebar .side-nav a').forEach(function (a) {
            a.addEventListener('click', closeSidebar);
        });
        document.addEventListener('keydown', function (e) {
            if (e.key === 'Escape') closeSidebar();
        });
        window.addEventListener('resize', function () {
            if (window.innerWidth > 991) closeSidebar();
        });
    }

    /* ---------- Mobile: public nav dropdown ---------- */
    var navToggle = document.querySelector('.public-nav-toggle');
    var publicNav = document.querySelector('.public-nav');
    if (navToggle && publicNav) {
        navToggle.addEventListener('click', function () {
            var open = publicNav.classList.toggle('open');
            navToggle.setAttribute('aria-expanded', open ? 'true' : 'false');
            navToggle.innerHTML = open ? '<i class="bi bi-x-lg"></i>' : '<i class="bi bi-list"></i>';
        });
    }

    /* ---------- Auth portal (Traveller / Staff) ---------- */
    var shell = document.querySelector('.auth-shell');
    if (shell) {
        var portalField = document.getElementById('portalField');
        var authTitle = document.getElementById('authTitle');
        var authSub = document.getElementById('authSub');
        var eyebrow = document.getElementById('portalEyebrow');
        var heading = document.getElementById('authHeading');

        var copy = {
            client: {
                title: 'Your journey starts here',
                sub: 'Sign in to search flights, manage bookings and download your e-tickets.',
                eyebrow: 'Traveller portal',
                heading: 'Welcome back'
            },
            admin: {
                title: 'Staff operations portal',
                sub: 'Verify bookings, manage flight inventory and monitor every activity on FlyLocal.',
                eyebrow: 'Staff portal',
                heading: 'Staff sign in'
            }
        };

        function setPortal(role) {
            shell.setAttribute('data-auth-role', role);
            if (portalField) portalField.value = role;
            document.querySelectorAll('.ps-btn').forEach(function (b) {
                var on = b.dataset.portal === role;
                b.classList.toggle('active', on);
                b.setAttribute('aria-selected', on ? 'true' : 'false');
            });
            document.querySelectorAll('.chip').forEach(function (c) {
                c.classList.toggle('on', c.dataset.for === role);
            });
            var c = copy[role] || copy.client;
            if (authTitle) authTitle.textContent = c.title;
            if (authSub) authSub.textContent = c.sub;
            if (eyebrow) eyebrow.textContent = c.eyebrow;
            if (heading) heading.textContent = c.heading;
        }

        document.querySelectorAll('.ps-btn').forEach(function (b) {
            b.addEventListener('click', function () { setPortal(b.dataset.portal); });
        });
        setPortal(shell.getAttribute('data-auth-role') || 'client');

        // Password visibility
        document.querySelectorAll('.pw-toggle').forEach(function (tog) {
            tog.addEventListener('click', function () {
                var input = tog.parentElement.querySelector('input');
                if (!input) return;
                var show = input.type === 'password';
                input.type = show ? 'text' : 'password';
                tog.innerHTML = show ? '<i class="bi bi-eye-slash"></i>' : '<i class="bi bi-eye"></i>';
                tog.setAttribute('aria-label', show ? 'Hide password' : 'Show password');
            });
        });

        // Demo quick-fill chips
        document.querySelectorAll('.chip[data-email]').forEach(function (chip) {
            chip.addEventListener('click', function () {
                setPortal(chip.dataset.for);
                var email = document.getElementById('Email');
                var pass = document.getElementById('Password');
                if (email) email.value = chip.dataset.email;
                if (pass) { pass.value = chip.dataset.pass; pass.type = 'password'; }
                if (email) email.focus();
            });
        });

        // Submit loading state
        var authForm = document.getElementById('authForm');
        if (authForm) {
            authForm.addEventListener('submit', function () {
                var btn = authForm.querySelector('.btn-portal');
                if (btn) btn.classList.add('is-loading');
            });
        }
    }
});

function printTicket() { window.print(); }
