/* Side-by-side PDF comparison viewer.
   The server does the diff and returns highlight rectangles as fractions of each
   page box. The browser renders the PDFs with PDF.js and positions the rectangles
   on top, so page images never have to travel over the wire. */

pdfjsLib.GlobalWorkerOptions.workerSrc =
  'https://cdnjs.cloudflare.com/ajax/libs/pdf.js/3.11.174/pdf.worker.min.js';

var API = 'api/compare/upload';

var state = {
  files: { left: null, right: null },
  buffers: { left: null, right: null },
  result: null,
  zoom: 1,
  currentBlock: -1,
  filters: { removed: true, added: true, changed: true },
  panes: {}
};

var $ = function (id) { return document.getElementById(id); };

/* ---------------- Intake ---------------- */

['left', 'right'].forEach(function (side) {
  var input = $('file' + cap(side));
  var slot = document.querySelector('.slot[data-side="' + side + '"]');

  input.addEventListener('change', function () {
    if (input.files.length) setFile(side, input.files[0]);
  });

  ['dragenter', 'dragover'].forEach(function (evt) {
    slot.addEventListener(evt, function (e) {
      e.preventDefault();
      slot.classList.add('is-over');
    });
  });

  ['dragleave', 'drop'].forEach(function (evt) {
    slot.addEventListener(evt, function (e) {
      e.preventDefault();
      slot.classList.remove('is-over');
    });
  });

  slot.addEventListener('drop', function (e) {
    var file = e.dataTransfer.files[0];
    if (file) setFile(side, file);
  });
});

function setFile(side, file) {
  if (file.type !== 'application/pdf' && !/\.pdf$/i.test(file.name)) {
    showIntakeError(file.name + ' is not a PDF.');
    return;
  }
  showIntakeError(null);
  state.files[side] = file;

  var slot = document.querySelector('.slot[data-side="' + side + '"]');
  slot.classList.add('is-set');
  $('name' + cap(side)).textContent = file.name;

  $('runCompare').disabled = !(state.files.left && state.files.right);
}

function showIntakeError(message) {
  var el = $('intakeError');
  el.hidden = !message;
  el.textContent = message || '';
}

$('runCompare').addEventListener('click', run);

$('startOver').addEventListener('click', function () {
  $('viewer').hidden = true;
  $('intake').hidden = false;
  $('pagesLeft').innerHTML = '';
  $('pagesRight').innerHTML = '';
  state.result = null;
  state.currentBlock = -1;
});

/* ---------------- Compare ---------------- */

function run() {
  $('intake').hidden = true;
  busy('Reading both documents');

  var form = new FormData();
  form.append('left', state.files.left, state.files.left.name);
  form.append('right', state.files.right, state.files.right.name);

  Promise.all([
    readBuffer(state.files.left),
    readBuffer(state.files.right),
    fetch(API, { method: 'POST', body: form }).then(readJson)
  ])
    .then(function (parts) {
      state.buffers.left = parts[0];
      state.buffers.right = parts[1];
      state.result = parts[2];
      busy('Drawing the pages');
      return show(state.result);
    })
    .catch(function (err) {
      $('busy').hidden = true;
      $('intake').hidden = false;
      showIntakeError(err.message || 'The comparison failed. Try again.');
    });
}

function readJson(response) {
  return response.text().then(function (text) {
    var data;
    try { data = JSON.parse(text); } catch (e) { data = null; }
    if (!response.ok) {
      throw new Error((data && data.error) || 'Server error ' + response.status);
    }
    return data;
  });
}

function readBuffer(file) {
  return new Promise(function (resolve, reject) {
    var reader = new FileReader();
    reader.onload = function () { resolve(reader.result); };
    reader.onerror = function () { reject(new Error('Could not read ' + file.name)); };
    reader.readAsArrayBuffer(file);
  });
}

function busy(text) {
  $('busyText').textContent = text;
  $('busy').hidden = false;
}

function show(result) {
  $('barLeft').textContent = result.leftName;
  $('barRight').textContent = result.rightName;
  $('scoreValue').innerHTML = result.difference + '<span class="score-unit">%</span>';
  $('scoreAlgo').textContent = '(' + result.algorithm + ')';
  $('countRemoved').textContent = result.removedCount;
  $('countAdded').textContent = result.addedCount;
  $('countChanged').textContent = result.changedCount;

  buildChangeList(result.blocks);
  updateNavPosition();

  return Promise.all([
    mountPane('left', state.buffers.left, result.left),
    mountPane('right', state.buffers.right, result.right)
  ]).then(function () {
    $('busy').hidden = true;
    $('viewer').hidden = false;
    // Page boxes are laid out now, so canvases can be sized against real widths.
    layout();
    syncScrolling();
    if (result.blocks.length) goToBlock(0);
  });
}

/* ---------------- Rendering ---------------- */

function mountPane(side, buffer, sideResult) {
  var container = $('pages' + cap(side));
  container.innerHTML = '';

  return pdfjsLib.getDocument({ data: buffer.slice(0) }).promise.then(function (pdf) {
    var pane = {
      pdf: pdf,
      container: container,
      element: $('pane' + cap(side)),
      pages: [],
      highlights: groupBy(sideResult.highlights, 'page')
    };

    for (var n = 1; n <= pdf.numPages; n++) {
      var box = document.createElement('div');
      box.className = 'page';
      box.dataset.page = String(n);

      var canvas = document.createElement('canvas');
      box.appendChild(canvas);

      var label = document.createElement('span');
      label.className = 'page-number';
      label.textContent = 'Page ' + n;
      box.appendChild(label);

      (pane.highlights[n] || []).forEach(function (h) {
        box.appendChild(makeMark(h));
      });

      container.appendChild(box);
      pane.pages.push({ number: n, box: box, canvas: canvas, rendered: false, task: null });
    }

    state.panes[side] = pane;
    observe(pane);
  });
}

function makeMark(h) {
  var mark = document.createElement('div');
  mark.className = 'mark mark-' + h.kind;
  mark.dataset.block = String(h.block);
  mark.style.left = pct(h.x);
  mark.style.top = pct(h.y);
  mark.style.width = pct(h.w);
  mark.style.height = pct(h.h);
  mark.addEventListener('click', function () { goToBlock(Number(h.block)); });
  return mark;
}

function pct(v) { return (v * 100).toFixed(4) + '%'; }

/* Sizes every page box to the current zoom and (re)draws visible canvases. */
function layout() {
  ['left', 'right'].forEach(function (side) {
    var pane = state.panes[side];
    if (!pane) return;

    var available = pane.element.clientWidth - 36;

    pane.pages.forEach(function (page) {
      pane.pdf.getPage(page.number).then(function (pdfPage) {
        var base = pdfPage.getViewport({ scale: 1 });
        var scale = (available / base.width) * state.zoom;
        var viewport = pdfPage.getViewport({ scale: scale });

        page.box.style.width = Math.round(viewport.width) + 'px';
        page.box.style.height = Math.round(viewport.height) + 'px';
        page.rendered = false;
        page.viewport = viewport;
        page.pdfPage = pdfPage;

        if (isNear(pane.element, page.box)) draw(page);
      });
    });
  });
}

function draw(page) {
  if (page.rendered || !page.pdfPage) return;
  page.rendered = true;

  var ratio = Math.min(window.devicePixelRatio || 1, 2);
  var viewport = page.viewport;
  var canvas = page.canvas;

  canvas.width = Math.round(viewport.width * ratio);
  canvas.height = Math.round(viewport.height * ratio);

  var context = canvas.getContext('2d');
  context.setTransform(ratio, 0, 0, ratio, 0, 0);

  if (page.task) page.task.cancel();
  page.task = page.pdfPage.render({ canvasContext: context, viewport: viewport });
  page.task.promise.catch(function () { page.rendered = false; });
}

function observe(pane) {
  var observer = new IntersectionObserver(function (entries) {
    entries.forEach(function (entry) {
      if (!entry.isIntersecting) return;
      var page = pane.pages.filter(function (p) { return p.box === entry.target; })[0];
      if (page) draw(page);
    });
  }, { root: pane.element, rootMargin: '600px 0px' });

  pane.pages.forEach(function (page) { observer.observe(page.box); });
}

function isNear(pane, box) {
  var paneRect = pane.getBoundingClientRect();
  var boxRect = box.getBoundingClientRect();
  return boxRect.bottom > paneRect.top - 600 && boxRect.top < paneRect.bottom + 600;
}

/* ---------------- Zoom ---------------- */

$('zoomIn').addEventListener('click', function () { setZoom(state.zoom + 0.2); });
$('zoomOut').addEventListener('click', function () { setZoom(state.zoom - 0.2); });

function setZoom(value) {
  state.zoom = Math.min(3, Math.max(0.5, Math.round(value * 10) / 10));
  $('zoomLabel').textContent = Math.round(state.zoom * 100) + '%';
  layout();
}

var resizeTimer;
window.addEventListener('resize', function () {
  clearTimeout(resizeTimer);
  resizeTimer = setTimeout(layout, 200);
});

/* ---------------- Scroll sync ---------------- */

function syncScrolling() {
  var locked = false;

  ['left', 'right'].forEach(function (side) {
    var source = state.panes[side].element;
    var target = state.panes[side === 'left' ? 'right' : 'left'].element;

    source.addEventListener('scroll', function () {
      if (locked) return;
      locked = true;

      var sourceRange = source.scrollHeight - source.clientHeight;
      var targetRange = target.scrollHeight - target.clientHeight;
      if (sourceRange > 0 && targetRange > 0) {
        target.scrollTop = (source.scrollTop / sourceRange) * targetRange;
      }

      requestAnimationFrame(function () { locked = false; });
    });
  });
}

/* ---------------- Navigation ---------------- */

$('nextChange').addEventListener('click', function () { step(1); });
$('prevChange').addEventListener('click', function () { step(-1); });

document.addEventListener('keydown', function (e) {
  if ($('viewer').hidden) return;
  if (e.target.tagName === 'INPUT') return;
  if (e.key === 'n' || e.key === 'ArrowDown' && e.altKey) { step(1); e.preventDefault(); }
  if (e.key === 'p' || e.key === 'ArrowUp' && e.altKey) { step(-1); e.preventDefault(); }
});

function visibleBlocks() {
  if (!state.result) return [];
  return state.result.blocks.filter(function (b) { return state.filters[b.kind]; });
}

function step(direction) {
  var blocks = visibleBlocks();
  if (!blocks.length) return;

  var ids = blocks.map(function (b) { return b.id; });
  var position = ids.indexOf(state.currentBlock);
  var next = position === -1
    ? (direction > 0 ? 0 : ids.length - 1)
    : (position + direction + ids.length) % ids.length;

  goToBlock(ids[next]);
}

function goToBlock(id) {
  state.currentBlock = id;

  document.querySelectorAll('.mark.is-current').forEach(function (el) {
    el.classList.remove('is-current');
  });

  var block = state.result.blocks.filter(function (b) { return b.id === id; })[0];
  if (!block) return;

  ['left', 'right'].forEach(function (side) {
    var pane = state.panes[side];
    var mark = pane.container.querySelector('.mark[data-block="' + id + '"]');

    if (mark) {
      mark.classList.add('is-current');
      scrollPaneTo(pane.element, mark);
    } else {
      var pageNumber = side === 'left' ? block.leftPage : block.rightPage;
      var page = pane.container.querySelector('.page[data-page="' + pageNumber + '"]');
      if (page) scrollPaneTo(pane.element, page);
    }
  });

  document.querySelectorAll('.changelist li').forEach(function (li) {
    li.classList.toggle('is-current', Number(li.dataset.block) === id);
  });

  var current = document.querySelector('.changelist li.is-current');
  if (current) current.scrollIntoView({ block: 'nearest' });

  updateNavPosition();
}

function scrollPaneTo(pane, element) {
  var paneRect = pane.getBoundingClientRect();
  var rect = element.getBoundingClientRect();
  pane.scrollTop += (rect.top - paneRect.top) - pane.clientHeight * 0.32;
}

function updateNavPosition() {
  var blocks = visibleBlocks();
  var label = $('navPosition');

  if (!blocks.length) {
    label.textContent = state.result && state.result.blocks.length
      ? 'nothing shown'
      : 'no changes';
    return;
  }

  var index = blocks.map(function (b) { return b.id; }).indexOf(state.currentBlock);
  label.textContent = (index === -1 ? '–' : index + 1) + ' of ' + blocks.length;
}

/* ---------------- Filters ---------------- */

document.querySelectorAll('.key').forEach(function (key) {
  key.setAttribute('aria-pressed', 'true');

  key.addEventListener('click', function () {
    var kind = key.dataset.filter;
    state.filters[kind] = !state.filters[kind];

    key.setAttribute('aria-pressed', String(state.filters[kind]));
    key.classList.toggle('is-dim', !state.filters[kind]);

    document.querySelectorAll('.mark-' + kind).forEach(function (mark) {
      mark.classList.toggle('is-hidden', !state.filters[kind]);
    });

    document.querySelectorAll('.changelist li[data-kind="' + kind + '"]').forEach(function (li) {
      li.hidden = !state.filters[kind];
    });

    updateNavPosition();
  });
});

/* ---------------- Change list ---------------- */

$('toggleList').addEventListener('click', function () {
  var list = $('changeList');
  list.hidden = !list.hidden;
  this.setAttribute('aria-pressed', String(!list.hidden));
});

function buildChangeList(blocks) {
  var list = $('changeItems');
  list.innerHTML = '';

  if (!blocks.length) {
    var empty = document.createElement('li');
    empty.className = 'empty-state';
    empty.textContent = 'The two documents contain the same words.';
    list.appendChild(empty);
    return;
  }

  blocks.forEach(function (block) {
    var li = document.createElement('li');
    li.dataset.block = String(block.id);
    li.dataset.kind = block.kind;

    var head = document.createElement('div');
    head.className = 'change-head';

    var kind = document.createElement('span');
    kind.className = 'change-kind ' + block.kind;
    kind.textContent = block.kind === 'changed' ? 'rewritten' : block.kind;

    var where = document.createElement('span');
    where.textContent = 'p. ' + (block.kind === 'added' ? block.rightPage : block.leftPage);

    head.appendChild(kind);
    head.appendChild(where);

    var body = document.createElement('div');
    body.className = 'change-text';

    if (block.leftText) {
      var del = document.createElement('del');
      del.textContent = block.leftText;
      body.appendChild(del);
    }

    if (block.leftText && block.rightText) body.appendChild(document.createTextNode(' '));

    if (block.rightText) {
      var ins = document.createElement('ins');
      ins.textContent = block.rightText;
      body.appendChild(ins);
    }

    li.appendChild(head);
    li.appendChild(body);
    li.addEventListener('click', function () { goToBlock(block.id); });
    list.appendChild(li);
  });
}

/* ---------------- Helpers ---------------- */

function groupBy(items, key) {
  var map = {};
  (items || []).forEach(function (item) {
    var k = item[key];
    (map[k] = map[k] || []).push(item);
  });
  return map;
}

function cap(s) { return s.charAt(0).toUpperCase() + s.slice(1); }
