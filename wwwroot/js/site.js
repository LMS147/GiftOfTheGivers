// Gift of the Givers — site-wide scripts

(function () {
    'use strict';

    /* ----- Navbar shadow + back-to-top on scroll -------------------------- */
    var nav = document.querySelector('.navbar-gotg');
    var topBtn = document.getElementById('backToTop');

    function onScroll() {
        var y = window.scrollY || window.pageYOffset;
        if (nav) nav.classList.toggle('navbar-scrolled', y > 24);
        if (topBtn) topBtn.classList.toggle('show', y > 480);
    }
    window.addEventListener('scroll', onScroll, { passive: true });
    onScroll();

    if (topBtn) {
        topBtn.addEventListener('click', function () {
            window.scrollTo({ top: 0, behavior: 'smooth' });
        });
    }

    /* ----- Animated counters (elements with data-count) ------------------- */
    var counters = document.querySelectorAll('[data-count]');
    if (counters.length && 'IntersectionObserver' in window) {
        var io = new IntersectionObserver(function (entries) {
            entries.forEach(function (entry) {
                if (!entry.isIntersecting) return;
                io.unobserve(entry.target);
                var el = entry.target;
                var target = parseFloat(el.getAttribute('data-count') || '0');
                var decimals = parseInt(el.getAttribute('data-decimals') || '0', 10);
                var duration = window.matchMedia('(prefers-reduced-motion: reduce)').matches ? 0 : 900;
                var start = null;
                function frame(now) {
                    if (!start) start = now;
                    var p = duration === 0 ? 1 : Math.min((now - start) / duration, 1);
                    var eased = 1 - Math.pow(1 - p, 3);
                    el.textContent = (target * eased).toLocaleString('en-ZA', {
                        minimumFractionDigits: decimals,
                        maximumFractionDigits: decimals
                    });
                    if (p < 1) requestAnimationFrame(frame);
                }
                requestAnimationFrame(frame);
            });
        }, { threshold: 0.35 });
        counters.forEach(function (el) { io.observe(el); });
    }

    /* ----- Apply Bootstrap's validation styling where requested ----------- */
    var forms = document.querySelectorAll('.needs-validation');
    Array.prototype.forEach.call(forms, function (form) {
        form.addEventListener('submit', function (event) {
            if (!form.checkValidity()) {
                event.preventDefault();
                event.stopPropagation();
            }
            form.classList.add('was-validated');
        }, false);
    });
})();

(function () {
    'use strict';
    document.querySelectorAll('[data-set-amount]').forEach(function (button) {
        button.addEventListener('click', function () {
            var input = document.getElementById('Amount');
            if (input) { input.value = button.dataset.setAmount; input.dispatchEvent(new Event('input', { bubbles: true })); }
        });
    });
    document.querySelectorAll('[data-back-to-details]').forEach(function (button) {
        button.addEventListener('click', function () { window.history.back(); });
    });
    document.querySelectorAll('form[method="post"], form[method="POST"]').forEach(function (form) {
        form.addEventListener('submit', function (event) {
            if (event.defaultPrevented) return;
            if (!form.checkValidity() || (window.jQuery && window.jQuery.fn.valid && !window.jQuery(form).valid())) {
                event.preventDefault();
                var field = form.querySelector(':invalid, .input-validation-error');
                if (field) field.focus();
                return;
            }
            if (form.dataset.confirm && !window.confirm(form.dataset.confirm)) { event.preventDefault(); return; }
            if (form.dataset.submitting) { event.preventDefault(); return; }
            form.dataset.submitting = 'true';
            form.setAttribute('aria-busy', 'true');
            form.querySelectorAll('button[type="submit"],button:not([type])').forEach(function (button) {
                button.dataset.originalHtml = button.innerHTML;
                button.disabled = true;
                button.innerHTML = '<span class="spinner-border spinner-border-sm me-2" aria-hidden="true"></span> Please wait';
            });
        });
    });
    window.addEventListener('pageshow', function () {
        document.querySelectorAll('form[data-submitting]').forEach(function (form) {
            delete form.dataset.submitting;
            form.removeAttribute('aria-busy');
            form.querySelectorAll('button[data-original-html]').forEach(function (button) {
                button.innerHTML = button.dataset.originalHtml; button.disabled = false; delete button.dataset.originalHtml;
            });
        });
    });
    document.querySelectorAll('.project-image, .project-detail-image').forEach(function (img) {
        img.addEventListener('error', function () { if (!img.dataset.fallbackUsed) { img.dataset.fallbackUsed = 'true'; img.src = '/images/project-food.svg'; } });
    });
})();
