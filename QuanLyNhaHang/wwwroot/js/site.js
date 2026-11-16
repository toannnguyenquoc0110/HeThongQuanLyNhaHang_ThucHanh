(function () {
  function toast(msg, err) {
    var t = document.createElement('div'); t.className = 'toast' + (err ? ' err' : ''); t.setAttribute('role', 'status'); t.textContent = msg;
    document.body.appendChild(t); setTimeout(function () { t.remove(); }, 3200);
  }
  window.toast = toast;

  // Gửi POST (không tải lại trang) rồi thay nội dung vùng đích bằng HTML trả về
  function post(url, body, target) {
    return fetch(url, { method: 'POST', headers: { 'Content-Type': 'application/x-www-form-urlencoded' }, body: body })
      .then(function (r) {
        var e = r.headers.get('X-Error'); if (e) toast(decodeURIComponent(e), true);
        if (!r.ok) { toast('Có lỗi xảy ra, hãy thử lại.', true); return null; }
        return r.text();
      })
      .then(function (html) {
        if (html == null) return;
        var t = document.querySelector(target); if (t) { t.innerHTML = html; tick(); }
      })
      .catch(function () { toast('Mất kết nối tới máy chủ.', true); });
  }

  document.addEventListener('click', function (ev) {
    var b = ev.target.closest('[data-post]');
    if (b) {
      ev.preventDefault();
      if (b.dataset.confirm && !confirm(b.dataset.confirm)) return;
      post(b.dataset.post, b.dataset.body || '', b.dataset.target);
      return;
    }
    var o = ev.target.closest('[data-open]');
    if (o) { var m = document.querySelector(o.dataset.open); if (m) { m.classList.add('open'); var f = m.querySelector('input,select'); if (f) setTimeout(function () { f.focus(); }, 50); } return; }
    if (ev.target.closest('[data-close]') || ev.target.classList.contains('modal')) { var md = ev.target.closest('.modal'); if (md) md.classList.remove('open'); }
  });

  document.addEventListener('submit', function (ev) {
    var f = ev.target.closest('form[data-target]');
    if (!f) return;
    ev.preventDefault();
    post(f.action, new URLSearchParams(new FormData(f)).toString(), f.dataset.target);
  });

  document.addEventListener('keydown', function (ev) {
    if (ev.key === 'Escape') document.querySelectorAll('.modal.open').forEach(function (m) { m.classList.remove('open'); });
  });

  // Đồng hồ + thời gian chờ của từng món (data-since = giờ gửi bếp)
  function tick() {
    document.querySelectorAll('[data-since]').forEach(function (el) {
      var m = Math.floor((Date.now() - new Date(el.dataset.since)) / 60000);
      el.textContent = m < 1 ? 'vừa gửi' : m + ' phút';
      var lim = +el.dataset.limit; if (lim && m > lim) el.classList.add('late'); else el.classList.remove('late');
    });
    var c = document.getElementById('clock');
    if (c) c.textContent = new Date().toLocaleTimeString('vi-VN', { hour: '2-digit', minute: '2-digit' });
  }
  setInterval(tick, 15000); document.addEventListener('DOMContentLoaded', tick);

  document.addEventListener('DOMContentLoaded', function () {
    document.querySelectorAll('.toast[data-auto]').forEach(function (t) { setTimeout(function () { t.remove(); }, 3500); });
    var bs = document.getElementById('branchSel');
    if (bs) bs.addEventListener('change', function () { location.href = '/Session/Branch/' + bs.value + '?back=' + encodeURIComponent(location.pathname + location.search); });
  });

  // Chế độ sáng/tối (nhớ lựa chọn nếu trình duyệt cho phép)
  document.addEventListener('click', function (ev) {
    if (!ev.target.closest('#themeBtn')) return;
    var r = document.documentElement, dark = r.getAttribute('data-theme') === 'dark' || (!r.getAttribute('data-theme') && matchMedia('(prefers-color-scheme: dark)').matches);
    var next = dark ? 'light' : 'dark'; r.setAttribute('data-theme', next);
    try { localStorage.setItem('theme', next); } catch (e) { }
  });

  // Bảng lệnh Ctrl/⌘ + K: nhảy nhanh tới trang, vai trò, chi nhánh
  var pal = document.getElementById('pal'), palQ = document.getElementById('palQ');
  function palItems() { return Array.prototype.filter.call(document.querySelectorAll('#palList a'), function (a) { return a.style.display !== 'none'; }); }
  function palMark(i) { var it = palItems(); it.forEach(function (a) { a.classList.remove('sel-i'); }); if (it.length) { i = (i + it.length) % it.length; it[i].classList.add('sel-i'); it[i].scrollIntoView({ block: 'nearest' }); } palMark.i = i; }
  function palOpen() { if (!pal) return; pal.classList.add('open'); palQ.value = ''; palFilter(); palQ.focus(); }
  function palClose() { if (pal) pal.classList.remove('open'); }
  function palFilter() { var q = palQ.value.trim().toLowerCase(); document.querySelectorAll('#palList a').forEach(function (a) { a.style.display = !q || a.dataset.t.indexOf(q) > -1 ? '' : 'none'; }); palMark(0); }
  document.addEventListener('click', function (ev) { if (ev.target.closest('#palBtn')) palOpen(); else if (ev.target === pal) palClose(); });
  document.addEventListener('keydown', function (ev) {
    if ((ev.ctrlKey || ev.metaKey) && ev.key.toLowerCase() === 'k') { ev.preventDefault(); palOpen(); return; }
    if (!pal || !pal.classList.contains('open')) return;
    if (ev.key === 'Escape') palClose();
    else if (ev.key === 'ArrowDown') { ev.preventDefault(); palMark((palMark.i || 0) + 1); }
    else if (ev.key === 'ArrowUp') { ev.preventDefault(); palMark((palMark.i || 0) - 1); }
    else if (ev.key === 'Enter') { var it = palItems()[palMark.i || 0]; if (it) location.href = it.href; }
  });
  if (palQ) palQ.addEventListener('input', palFilter);
})();
