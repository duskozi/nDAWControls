(function () {
  var root = window.NDAW_ROOT || '';
  var SKINS = { graphite: 'Graphite', studio: 'Studio', lightclassic: 'Light classic', retrohardware: 'Retro hardware', highcontrast: 'High contrast' };
  function store(k, v) { try { if (v === undefined) return localStorage.getItem(k); localStorage.setItem(k, v); } catch (e) { return null; } }

  // ---- skin of the screenshots (shared by every page)
  function applySkin(skin, animate) {
    if (!SKINS[skin]) skin = 'graphite';
    document.querySelectorAll('img[data-shot]').forEach(function (img) {
      var src = root + 'img/' + skin + '/' + img.dataset.shot + '.png';
      if (img.getAttribute('src') === src) return;
      if (animate) {
        img.classList.add('swapping');
        var pre = new Image();
        pre.onload = pre.onerror = function () { img.src = src; img.classList.remove('swapping'); };
        pre.src = src;
      } else img.src = src;
    });
    document.querySelectorAll('[data-skin]').forEach(function (b) { b.setAttribute('aria-pressed', b.dataset.skin === skin ? 'true' : 'false'); });
    document.querySelectorAll('.skin-name').forEach(function (s) { s.textContent = SKINS[skin]; });
  }
  var skin = store('ndaw-skin') || 'graphite';
  applySkin(skin, false);
  document.addEventListener('click', function (e) {
    var b = e.target.closest('[data-skin]');
    if (!b) return;
    skin = b.dataset.skin; store('ndaw-skin', skin); applySkin(skin, true);
  });

  // ---- light / dark
  var tb = document.querySelector('.theme-btn');
  if (tb) tb.addEventListener('click', function () {
    var cur = document.documentElement.dataset.theme ||
      (window.matchMedia('(prefers-color-scheme: light)').matches ? 'light' : 'dark');
    var next = cur === 'light' ? 'dark' : 'light';
    document.documentElement.dataset.theme = next; store('ndaw-theme', next);
  });

  // ---- mobile menu
  var mb = document.querySelector('.menu-btn');
  if (mb) mb.addEventListener('click', function () {
    var open = document.body.classList.toggle('nav-open');
    mb.setAttribute('aria-expanded', open ? 'true' : 'false');
  });
  document.addEventListener('click', function (e) {
    if (document.body.classList.contains('nav-open') && !e.target.closest('.sidebar') && !e.target.closest('.menu-btn')) {
      document.body.classList.remove('nav-open'); if (mb) mb.setAttribute('aria-expanded', 'false');
    }
  });
  var cur = document.querySelector('.nav-sec a[aria-current]');
  if (cur) cur.scrollIntoView({ block: 'center' });

  // ---- copy buttons
  document.addEventListener('click', function (e) {
    var b = e.target.closest('.copy');
    if (!b) return;
    var text = b.closest('.code, .install').querySelector('code').innerText;
    var done = function () { b.textContent = 'Copied'; b.classList.add('done'); setTimeout(function () { b.textContent = 'Copy'; b.classList.remove('done'); }, 1400); };
    if (navigator.clipboard && navigator.clipboard.writeText) navigator.clipboard.writeText(text).then(done, function () { fallback(text); done(); });
    else { fallback(text); done(); }
  });
  function fallback(text) {
    var t = document.createElement('textarea'); t.value = text; t.style.position = 'fixed'; t.style.opacity = '0';
    document.body.appendChild(t); t.select(); try { document.execCommand('copy'); } catch (e) { } t.remove();
  }

  // ---- table of contents highlight
  var links = Array.prototype.slice.call(document.querySelectorAll('.toc a'));
  if (links.length && 'IntersectionObserver' in window) {
    var map = {};
    links.forEach(function (a) { map[a.getAttribute('href').slice(1)] = a; });
    var visible = {};
    var io = new IntersectionObserver(function (entries) {
      entries.forEach(function (en) { visible[en.target.id] = en.isIntersecting; });
      var first = links.find(function (a) { return visible[a.getAttribute('href').slice(1)]; });
      if (first) { links.forEach(function (a) { a.classList.remove('active'); }); first.classList.add('active'); }
    }, { rootMargin: '-70px 0px -65% 0px' });
    Object.keys(map).forEach(function (id) { var el = document.getElementById(id); if (el) io.observe(el); });
  }

  // ---- search
  var input = document.getElementById('search');
  var box = document.querySelector('.search-results');
  var data = window.NDAW_SEARCH || [];
  var sel = -1, results = [];
  function render() {
    var q = input.value.trim().toLowerCase();
    if (!q) { box.hidden = true; return; }
    var words = q.split(/\s+/);
    results = data.map(function (d) {
      var t = d.t.toLowerCase(), score = 0;
      for (var i = 0; i < words.length; i++) { var k = t.indexOf(words[i]); if (k < 0) return null; score += k === 0 ? 0 : (t[k - 1] === '.' ? 1 : 3); }
      if (d.s !== 'Property' && d.s !== 'Event' && d.s !== 'Method') score -= 2;
      return { d: d, score: score + t.length / 100 };
    }).filter(Boolean).sort(function (a, b) { return a.score - b.score; }).slice(0, 14);
    box.innerHTML = results.length ? results.map(function (r, i) {
      return '<a href="' + root + r.d.p + '"' + (i === sel ? ' class="sel"' : '') + '>' + escapeHtml(r.d.t) + '<span>' + escapeHtml(r.d.s) + '</span></a>';
    }).join('') : '<div class="none">Nothing found</div>';
    box.hidden = false;
  }
  function escapeHtml(s) { return s.replace(/[&<>"]/g, function (c) { return { '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;' }[c]; }); }
  if (input) {
    input.addEventListener('input', function () { sel = -1; render(); });
    input.addEventListener('keydown', function (e) {
      if (e.key === 'ArrowDown') { sel = Math.min(sel + 1, results.length - 1); render(); e.preventDefault(); }
      else if (e.key === 'ArrowUp') { sel = Math.max(sel - 1, 0); render(); e.preventDefault(); }
      else if (e.key === 'Enter') { var r = results[sel < 0 ? 0 : sel]; if (r) location.href = root + r.d.p; }
      else if (e.key === 'Escape') { input.value = ''; box.hidden = true; input.blur(); }
    });
    input.addEventListener('focus', render);
    document.addEventListener('click', function (e) { if (!e.target.closest('.search') && !e.target.closest('.search-results')) box.hidden = true; });
    document.addEventListener('keydown', function (e) {
      if ((e.key === '/' || (e.key === 'k' && (e.ctrlKey || e.metaKey))) && document.activeElement !== input &&
          !/INPUT|TEXTAREA/.test(document.activeElement.tagName)) { e.preventDefault(); input.focus(); }
    });
  }
})();
