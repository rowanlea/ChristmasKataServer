import { loadArt, drawTree } from './tree.js';

const MAX_TILES = 8;
const BANNER_MS = 6000;
const SHOW_FRAME_MS = 100;
const POLL_MS = 5000;
const ENLARGED = new Set(['first-full-marks', 'secret-santa']);

const $ = (id) => document.getElementById(id);
const scoreboardEl = $('scoreboard');
const scoreboardEmptyEl = $('scoreboard-empty');
const feedEl = $('feed');
const feedEmptyEl = $('feed-empty');
const statusEl = $('status');
const bannerEl = $('banner');
const bannerTreeEl = $('banner-tree');
const bannerTextEl = $('banner-text');
const showTreeEl = $('show-tree');
const showTeamEl = $('show-team');
const showProgressEl = $('show-progress');
const showsEmptyEl = $('shows-empty');
const ladderEl = $('ladder');

const artCache = new Map();
function artFor(size) {
  if (!artCache.has(size)) artCache.set(size, loadArt('', size));
  return artCache.get(size);
}

function el(tag, className, text) {
  const node = document.createElement(tag);
  if (className) node.className = className;
  if (text !== undefined) node.textContent = text;
  return node;
}

function fitCanvas(canvas) {
  const px = Math.round(canvas.clientWidth * (window.devicePixelRatio || 1));
  if (px > 0 && canvas.width !== px) canvas.width = canvas.height = px;
}

async function paint(canvas, size, states, time = 0) {
  const art = await artFor(size);
  fitCanvas(canvas);
  drawTree(canvas.getContext('2d'), art, { states, time });
}

async function getJson(url) {
  const res = await fetch(url, { cache: 'no-store' });
  if (!res.ok) throw new Error(`${url} ${res.status}`);
  return res.json();
}

// Leaderboard

function renderLeaderboard(entries) {
  const rows = [];
  entries.forEach((entry) => {
    const li = el('li', entry.rank <= 3 ? `place-${entry.rank}` : '');
    li.append(
      el('span', 'rank', String(entry.rank)),
      el('span', 'team', entry.teamName),
      el('span', 'total', entry.total.toLocaleString('en-GB')),
    );
    rows.push(li);
  });
  scoreboardEl.style.setProperty('--rows', String(Math.max(12, entries.length)));
  scoreboardEl.replaceChildren(...rows);
  scoreboardEmptyEl.hidden = entries.length > 0;
}

// Ladder

// The server places each team; this only draws the rungs it sends, top rung first.
function renderLadder(rungs) {
  ladderEl.replaceChildren(...rungs.map((rung) => {
    const santa = rung.level === 'santa';
    const li = el('li', santa ? 'rung santa' : 'rung');
    li.dataset.round = santa ? '8' : rung.level.split('.')[0];
    li.classList.toggle('occupied', rung.teams.length > 0);
    const label = el('span', 'level', santa ? rung.name : rung.level);
    const teams = el('span', 'teams');
    teams.append(...rung.teams.map((team) => {
      const chip = el('span', 'chip', team.teamName);
      chip.style.setProperty('--percent', String(team.percent));
      chip.title = `${team.teamName}: ${team.percent}%`;
      return chip;
    }));
    if (santa) li.append(label, teams);
    else li.append(label, el('span', 'name', rung.name), teams);
    return li;
  }));
  fitLadder();
}

// Many teams on one rung wrap onto extra lines; shrink the ladder text until
// every rung fits the panel rather than clipping the bottom rungs.
function fitLadder() {
  let fit = 1;
  ladderEl.style.setProperty('--fit', '1');
  while (ladderEl.scrollHeight > ladderEl.clientHeight && fit > 0.6) {
    fit -= 0.05;
    ladderEl.style.setProperty('--fit', fit.toFixed(2));
  }
}

// Feed

const tileKey = (t) => JSON.stringify([t.teamName, t.puzzle, t.correct, t.states, t.banner]);
let shownTiles = [];

function tileEl(tile, isNew) {
  const li = el('li', isNew ? 'tile new' : 'tile');
  const canvas = el('canvas');
  const caption = el('div', 'caption');
  const count = el('span', 'count', `${tile.correct}/${tile.bulbCount}`);
  if (tile.correct === tile.bulbCount) count.classList.add('full');
  count.hidden = tile.correct === null;
  caption.append(el('span', 'team', tile.teamName), el('span', 'puzzle', tile.puzzle), count);
  li.append(canvas, caption);
  li.addEventListener('animationend', () => li.classList.remove('new'), { once: true });
  queueMicrotask(() => paint(canvas, tile.size, tile.states));
  return li;
}

function addTile(tile) {
  const first = feedEl.firstElementChild;
  feedEl.prepend(tileEl(tile, true));
  shownTiles = [tile, ...shownTiles].slice(0, MAX_TILES);
  while (feedEl.children.length > MAX_TILES) feedEl.lastElementChild.remove();
  feedEmptyEl.hidden = true;
  if (first) first.classList.remove('new');
  if (tile.banner) queueBanner({ ...tile.banner, tile });
}

// Tiles have no id, so a fetched feed counts a tile as new when nothing on
// screen has the same content.
function mergeFeed(tiles, announce) {
  tiles = tiles.slice(0, MAX_TILES);
  const keys = tiles.map(tileKey);
  const oldKeys = shownTiles.map(tileKey);
  if (keys.join('\n') === oldKeys.join('\n')) return;
  const old = new Set(oldKeys);
  const fresh = keys.map((k) => announce && !old.has(k));
  shownTiles = tiles;
  feedEl.replaceChildren(...tiles.map((t, i) => tileEl(t, fresh[i])));
  feedEmptyEl.hidden = tiles.length > 0;
  for (let i = tiles.length - 1; i >= 0; i--) {
    if (fresh[i] && tiles[i].banner) queueBanner({ ...tiles[i].banner, tile: tiles[i] });
  }
}

// Banners

const banners = [];
let bannerTimer = null;
let bannerTwinkle = 0;

function queueBanner(banner) {
  banners.push(banner);
  if (bannerTimer === null) nextBanner();
}

function nextBanner() {
  cancelAnimationFrame(bannerTwinkle);
  const banner = banners.shift();
  if (!banner) {
    bannerTimer = null;
    bannerEl.hidden = true;
    return;
  }
  bannerEl.className = `banner ${banner.kind}`;
  bannerTextEl.textContent = banner.text;
  const enlarge = banner.tile && ENLARGED.has(banner.kind);
  bannerTreeEl.hidden = !enlarge;
  bannerEl.hidden = false;
  if (enlarge) {
    const { size, states } = banner.tile;
    const twinkle = (time) => {
      paint(bannerTreeEl, size, states, time);
      bannerTwinkle = requestAnimationFrame(twinkle);
    };
    bannerTwinkle = requestAnimationFrame(twinkle);
  }
  bannerTimer = setTimeout(nextBanner, BANNER_MS);
}

// Light shows

const SHOW_SIZE = 48;
let shows = [];
let showTeam = null;
let showFrame = 0;

function setShows(list) {
  shows = list;
  showsEmptyEl.hidden = shows.length > 0;
  if (shows.length === 0) {
    showTeam = null;
    showTeamEl.textContent = '';
    showProgressEl.textContent = '';
    paint(showTreeEl, SHOW_SIZE, '');
  }
}

function currentShow() {
  return shows.find((s) => s.teamName === showTeam);
}

function tickShow() {
  if (shows.length === 0) return;
  let show = currentShow();
  if (!show || showFrame >= show.frames.length) {
    const index = show ? shows.indexOf(show) : -1;
    show = shows[(index + 1) % shows.length];
    showTeam = show.teamName;
    showFrame = 0;
    showTeamEl.textContent = show.teamName;
  }
  showProgressEl.textContent = `frame ${showFrame + 1} of ${show.frames.length}`;
  paint(showTreeEl, SHOW_SIZE, show.frames[showFrame]);
  showFrame++;
}

async function loadShows() {
  setShows(await getJson('/shows'));
}

// Data

function renderBoard(board) {
  renderLeaderboard(board.teams);
  renderLadder(board.ladder);
}

async function loadAll(announce) {
  const [feed, board] = await Promise.all([getJson('/feed'), getJson('/leaderboard'), loadShows()]);
  mergeFeed(feed.tiles, announce);
  renderBoard(board);
}

let pollTimer = null;

function startPolling() {
  if (pollTimer !== null) return;
  pollTimer = setInterval(() => loadAll(true).catch(() => {}), POLL_MS);
}

function stopPolling() {
  clearInterval(pollTimer);
  pollTimer = null;
}

function listen() {
  const events = new EventSource('/feed/events');
  events.onopen = () => {
    statusEl.textContent = 'live';
    stopPolling();
    loadAll(true).catch(() => {});
  };
  events.onerror = () => {
    statusEl.textContent = 'reconnecting';
    startPolling();
  };
  events.addEventListener('tile', (e) => addTile(JSON.parse(e.data)));
  events.addEventListener('leaderboard', (e) => renderBoard(JSON.parse(e.data)));
  events.addEventListener('show', (e) => {
    const show = JSON.parse(e.data);
    if (show.first) queueBanner({ kind: 'show', text: `${show.teamName} has a light show!` });
    loadShows().catch(() => {});
  });
}

async function start() {
  try {
    await loadAll(false);
  } catch {
    statusEl.textContent = 'server unreachable';
  }
  setInterval(tickShow, SHOW_FRAME_MS);
  listen();
}

window.addEventListener('resize', fitLadder);

start();
