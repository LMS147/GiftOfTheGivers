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
                var duration = 900;
                var start = null;
                function frame(now) {
                    if (!start) start = now;
                    var p = Math.min((now - start) / duration, 1);
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
