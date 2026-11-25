const NAME_KEY = 'hth.teamName';
const KEY_KEY = 'hth.uniqueKey';
const SHOWS_OPEN = 'Light shows unlocked: your pattern goes on the big screen';

const form = document.getElementById('credentials-form');
const errorEl = document.getElementById('credentials-error');
const signOutEl = document.getElementById('sign-out');
const teamLabelEl = document.getElementById('team-label');
const nextUpEl = document.getElementById('next-up');
const showsEl = document.getElementById('shows-section');
const showsHeadingEl = document.getElementById('shows-heading');
const showsLockedText = showsHeadingEl.textContent;
const nextUpSignedOut = nextUpEl.textContent;

let events = null;

function read(key) {
  try { return localStorage.getItem(key); } catch { return null; }
}

function write(key, value) {
  try {
    if (value === null) localStorage.removeItem(key);
    else localStorage.setItem(key, value);
  } catch { /* storage unavailable: credentials last for this page only */ }
}

let teamName = read(NAME_KEY);
let uniqueKey = read(KEY_KEY);

function rowFor(id) {
  return document.querySelector(`tr[data-puzzle="${CSS.escape(id)}"]`);
}

function setCells(row, status, percent) {
  const statusEl = row.querySelector('.status');
  statusEl.textContent = status ?? '…';
  statusEl.className = status ? `status ${status}` : 'status';
  row.querySelector('.percent').textContent = percent == null ? '—' : `${percent}%`;
}

function setShows(unlocked) {
  showsEl.classList.toggle('locked', !unlocked);
  showsHeadingEl.textContent = unlocked ? SHOWS_OPEN : showsLockedText;
}

function showForm(message) {
  teamName = uniqueKey = null;
  write(NAME_KEY, null);
  write(KEY_KEY, null);
  events?.close();
  events = null;
  form.hidden = false;
  signOutEl.hidden = true;
  teamLabelEl.textContent = '';
  nextUpEl.textContent = nextUpSignedOut;
  errorEl.textContent = message ?? '';
  errorEl.hidden = !message;
  for (const row of document.querySelectorAll('tr[data-puzzle]')) setCells(row, null, null);
  setShows(false);
}

function setNextUp(progress) {
  const row = rowFor(progress.next);
  if (!row) {
    nextUpEl.textContent = 'Found Santa';
    return;
  }
  const percent = progress.puzzles.find(p => p.id === progress.next)?.percent;
  const detail = document.createElement('span');
  detail.className = 'detail';
  detail.textContent = [
    `${row.querySelector('.size').textContent} rows`,
    percent == null ? 'no attempt yet' : `best ${percent}%`,
    '80% to pass',
  ].join(' · ');
  nextUpEl.replaceChildren(`${progress.next} ${row.querySelector('.name').textContent}`, detail);
}

function render(progress) {
  form.hidden = true;
  errorEl.hidden = true;
  signOutEl.hidden = false;
  teamLabelEl.textContent = progress.teamName;
  for (const puzzle of progress.puzzles) {
    const row = rowFor(puzzle.id);
    if (row) setCells(row, puzzle.status, puzzle.percent);
  }
  setNextUp(progress);
  setShows(progress.showsUnlocked);
}

function listen() {
  if (events) return;
  events = new EventSource('/feed/events');
  events.addEventListener('tile', e => {
    try {
      if (JSON.parse(e.data).teamName === teamName) loadProgress();
    } catch { /* ignore a malformed event */ }
  });
}

async function loadProgress() {
  if (!teamName || !uniqueKey) {
    showForm();
    return;
  }
  let response;
  try {
    response = await fetch('/progress', {
      headers: { 'X-Team-Name': teamName, 'X-Unique-Key': uniqueKey },
    });
  } catch {
    nextUpEl.textContent = 'Server unreachable. Reload the page to try again.';
    return;
  }
  if (response.status === 401) {
    showForm('Team name or key not recognised');
    return;
  }
  if (!response.ok) {
    nextUpEl.textContent = `Could not load progress (HTTP ${response.status}). Reload the page to try again.`;
    return;
  }
  render(await response.json());
  listen();
}

form.addEventListener('submit', e => {
  e.preventDefault();
  const data = new FormData(form);
  teamName = String(data.get('teamName')).trim();
  uniqueKey = String(data.get('uniqueKey')).trim();
  write(NAME_KEY, teamName);
  write(KEY_KEY, uniqueKey);
  loadProgress();
});

signOutEl.addEventListener('click', () => showForm());

loadProgress();
