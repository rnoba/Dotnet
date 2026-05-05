<script lang="ts">

import * as signalR from '@microsoft/signalr';

import {
  onMount,
  onDestroy
} from 'svelte';

import {
  browser
} from '$app/environment';


interface Vec2 {
  x: number;
  y: number;
}

interface SnakeDto {
  id: string;
  name: string;
  segments: Vec2[];
  color: string;
  score: number;
  radius: number;
}

interface Food {
  id: string;
  x: number;
  y: number;
  color: string;
  radius: number;
}

interface GameStateDto {
  snakes: Record<string, SnakeDto>;
  foods: Food[];
}

let canvas: HTMLCanvasElement | null = $state(null);
let ctx: CanvasRenderingContext2D | null = $state(null);
let conn: signalR.HubConnection | null = $state(null);
let gameState: GameStateDto = $state({ snakes: {}, foods: [] });
let myId   = $state('');
let myName = $state('Snake' + Math.floor(Math.random() * 9000 + 1000));
let phase: 'menu' | 'playing' | 'dead' = $state('menu');
let score  = $state(0);
let raf    = $state(0);

let mouseX = $state(0);
let mouseY = $state(0);
let lastAngleSend = $state(0);

const WORLD  = 3000;
const SERVER = 'http://localhost:5194';

onMount(() => {
  ctx = canvas!.getContext('2d')!;
  resize();
  window.addEventListener('resize', resize);
  canvas!.addEventListener('mousemove', onMouse);
  canvas!.addEventListener('touchmove', onTouch, { passive: true });
  raf = requestAnimationFrame(renderLoop);
});

onDestroy(() => {
  if (browser) {
    cancelAnimationFrame(raf);
    window.removeEventListener('resize', resize);
  }
  conn?.stop();
});

function resize() {
  canvas!.width  = window.innerWidth;
  canvas!.height = window.innerHeight;
}

function onMouse(e: MouseEvent) { mouseX = e.clientX; mouseY = e.clientY; }

function onTouch(e: TouchEvent) {
  if (e.touches.length > 0) { mouseX = e.touches[0].clientX; mouseY = e.touches[0].clientY; }
}

async function connect() {
  conn = new signalR.HubConnectionBuilder()
    .withUrl(`${SERVER}/game`)
    .withAutomaticReconnect()
    .build();

  conn.on('Init', (id: string) => { myId = id; phase = 'playing'; });

  conn.on('GameState', (s: GameStateDto) => {
    gameState.snakes = s.snakes;
    gameState.foods  = s.foods;
    if (myId && s.snakes[myId]) score = s.snakes[myId].score;
  });

  conn.on('Died', () => { phase = 'dead'; });

  await conn.start();
}

async function joinGame() {
  if (!conn || conn.state !== signalR.HubConnectionState.Connected) await connect();
  await conn!.invoke('Join', myName);
}

async function respawn() {
  await conn!.invoke('Respawn');
}

function maybeSendAngle() {
  if (!conn || conn.state !== signalR.HubConnectionState.Connected) return;
  if (phase !== 'playing' || !myId) return;

  const now = performance.now();
  if (now - lastAngleSend < 50) return;
  lastAngleSend = now;

  const angle = Math.atan2(mouseY - canvas!.height / 2, mouseX - canvas!.width / 2);
  conn.invoke('UpdateAngle', angle).catch(() => {});
}

function renderLoop() {
  raf = requestAnimationFrame(renderLoop);
  if (!ctx) return;
  maybeSendAngle();

  const W = canvas!.width, H = canvas!.height;
  ctx.fillStyle = '#080c14';
  ctx.fillRect(0, 0, W, H);

  const me   = myId ? gameState.snakes[myId] : null;
  const camX = me?.segments[0]?.x ?? WORLD / 2;
  const camY = me?.segments[0]?.y ?? WORLD / 2;

  ctx.save();
  ctx.translate(W / 2 - camX, H / 2 - camY);
  drawGrid();
  drawFood();
  drawSnakes();
  ctx.restore();
  drawHUD();
}

function drawGrid() {
  ctx!.strokeStyle = 'rgba(255,255,255,0.035)';
  ctx!.lineWidth = 1;
  for (let x = 0; x <= WORLD; x += 80) {
    ctx!.beginPath(); ctx!.moveTo(x, 0); ctx!.lineTo(x, WORLD); ctx!.stroke();
  }
  for (let y = 0; y <= WORLD; y += 80) {
    ctx!.beginPath(); ctx!.moveTo(0, y); ctx!.lineTo(WORLD, y); ctx!.stroke();
  }
  ctx!.shadowColor = '#ff2244';
  ctx!.shadowBlur  = 20;
  ctx!.strokeStyle = 'rgba(255,34,68,0.7)';
  ctx!.lineWidth   = 5;
  ctx!.strokeRect(1, 1, WORLD - 2, WORLD - 2);
  ctx!.shadowBlur  = 0;
}

function drawFood() {
  for (const f of gameState.foods) {
    ctx!.beginPath();
    ctx!.arc(f.x, f.y, f.radius, 0, Math.PI * 2);
    ctx!.fillStyle   = f.color;
    ctx!.shadowColor = f.color;
    ctx!.shadowBlur  = 10;
    ctx!.fill();
  }
  ctx!.shadowBlur = 0;
}

function drawSnakes() {
  for (const snake of Object.values(gameState.snakes)) {
    const segs = snake.segments;
    if (segs.length === 0) continue;
    const r    = snake.radius;
    const isMe = snake.id === myId;

    console.log(segs);
    for (let i = segs.length - 1; i >= 0; i--) {
      const s  = segs[i];
      const t  = i / segs.length;
      const sr = i === 0 ? r * 1.3 : r * 1.3;

      ctx!.beginPath();
      ctx!.arc(s.x, s.y, sr, 0, Math.PI * 2);

      if (i === 0) {
        ctx!.fillStyle   = lighten(snake.color, isMe ? 55 : 30);
        ctx!.shadowColor = isMe ? snake.color : 'transparent';
        ctx!.shadowBlur  = isMe ? 18 : 0;
      } else {
        ctx!.fillStyle  = darken(snake.color, Math.floor(t * 30));
        ctx!.shadowBlur = 0;
      }
      ctx!.fill();
    }
    ctx!.shadowBlur = 0;

    if (segs.length >= 2) {
      const angle = Math.atan2(segs[0].y - segs[1].y, segs[0].x - segs[1].x);
      drawEyes(segs[0].x, segs[0].y, r, angle);
    }

    ctx!.font         = `bold ${Math.max(11, Math.round(r * 1.1))}px 'Courier New', monospace`;
    ctx!.textAlign    = 'center';
    ctx!.textBaseline = 'bottom';
    ctx!.fillStyle    = isMe ? '#FFD700' : 'rgba(255,255,255,0.85)';
    if (isMe) { ctx!.shadowColor = 'rgba(255,215,0,0.6)'; ctx!.shadowBlur = 6; }
    ctx!.fillText(snake.name, segs[0].x, segs[0].y - r - 4);
    ctx!.shadowBlur   = 0;
    ctx!.textBaseline = 'alphabetic';
  }
}

function drawEyes(x: number, y: number, r: number, angle: number) {
  const perp    = angle + Math.PI / 2;
  const forward = r * 0.5;
  const side    = r * 0.42;
  for (const sign of [-1, 1]) {
    const ex = x + Math.cos(angle) * forward + Math.cos(perp) * sign * side;
    const ey = y + Math.sin(angle) * forward + Math.sin(perp) * sign * side;
    const er = r * 0.28;
    ctx!.beginPath(); ctx!.arc(ex, ey, er, 0, Math.PI * 2);
    ctx!.fillStyle = '#fff'; ctx!.fill();
    ctx!.beginPath();
    ctx!.arc(ex + Math.cos(angle) * er * 0.4, ey + Math.sin(angle) * er * 0.4, er * 0.55, 0, Math.PI * 2);
    ctx!.fillStyle = '#111'; ctx!.fill();
  }
}

function drawHUD() {
  const W = canvas!.width, H = canvas!.height;

  ctx!.fillStyle = 'rgba(8,12,20,0.75)';
  pill(12, 12, 170, 46, 10); ctx!.fill();
  ctx!.font      = "bold 20px 'Courier New', monospace";
  ctx!.textAlign = 'left';
  ctx!.fillStyle = '#FFD700';
  ctx!.fillText(`⬡ ${score}`, 28, 44);

  const playerCount = Object.keys(gameState.snakes).length;
  ctx!.fillStyle = 'rgba(8,12,20,0.75)';
  pill(12, 66, 170, 34, 8); ctx!.fill();
  ctx!.font      = "13px 'Courier New', monospace";
  ctx!.fillStyle = '#88aacc';
  ctx!.fillText(`🐍 ${playerCount} online`, 28, 89);

  const sorted = Object.values(gameState.snakes).sort((a, b) => b.score - a.score).slice(0, 8);
  const lbW = 200, lbH = 36 + sorted.length * 24 + 8;
  ctx!.fillStyle = 'rgba(8,12,20,0.75)';
  pill(W - lbW - 12, 12, lbW, lbH, 10); ctx!.fill();
  ctx!.font      = "bold 11px 'Courier New', monospace";
  ctx!.textAlign = 'left';
  ctx!.fillStyle = '#446688';
  ctx!.fillText('LEADERBOARD', W - lbW + 4, 33);

  sorted.forEach((s, i) => {
    const y    = 55 + i * 24;
    const isMe = s.id === myId;
    ctx!.fillStyle = isMe ? '#FFD700' : '#99bbdd';
    ctx!.font      = isMe ? "bold 13px 'Courier New', monospace" : "13px 'Courier New', monospace";
    const name = s.name.length > 11 ? s.name.slice(0, 10) + '…' : s.name;
    ctx!.fillText(`${i + 1}. ${name}`, W - lbW + 4, y);
    ctx!.textAlign = 'right';
    ctx!.fillText(`${s.score}`, W - 18, y);
    ctx!.textAlign = 'left';
  });

  drawMinimap(W, H);

  if (phase === 'playing') {
    const cx = canvas!.width / 2, cy = canvas!.height / 2;
    ctx!.strokeStyle = 'rgba(255,255,255,0.08)';
    ctx!.lineWidth   = 1;
    ctx!.beginPath(); ctx!.moveTo(cx - 10, cy); ctx!.lineTo(cx + 10, cy); ctx!.stroke();
    ctx!.beginPath(); ctx!.moveTo(cx, cy - 10); ctx!.lineTo(cx, cy + 10); ctx!.stroke();
  }
}

function drawMinimap(W: number, H: number) {
  const size = 130, pad = 14;
  const scale = size / WORLD;
  const x0 = W - size - pad, y0 = H - size - pad;

  ctx!.fillStyle = 'rgba(8,12,20,0.8)';
  ctx!.fillRect(x0 - 2, y0 - 2, size + 4, size + 4);
  ctx!.strokeStyle = 'rgba(255,34,68,0.4)';
  ctx!.lineWidth   = 1;
  ctx!.strokeRect(x0, y0, size, size);

  for (const f of gameState.foods) {
    ctx!.fillStyle = f.color;
    ctx!.fillRect(x0 + f.x * scale - 0.5, y0 + f.y * scale - 0.5, 1.5, 1.5);
  }

  for (const snake of Object.values(gameState.snakes)) {
    if (snake.segments.length === 0) continue;
    const isMe = snake.id === myId;
    const head = snake.segments[0];
    ctx!.fillStyle = isMe ? '#FFD700' : snake.color;
    ctx!.beginPath();
    ctx!.arc(x0 + head.x * scale, y0 + head.y * scale, isMe ? 3 : 2, 0, Math.PI * 2);
    ctx!.fill();
  }
}

function pill(x: number, y: number, w: number, h: number, r: number) {
  ctx!.beginPath();
  ctx!.moveTo(x + r, y);
  ctx!.lineTo(x + w - r, y);
  ctx!.quadraticCurveTo(x + w, y,     x + w, y + r);
  ctx!.lineTo(x + w, y + h - r);
  ctx!.quadraticCurveTo(x + w, y + h, x + w - r, y + h);
  ctx!.lineTo(x + r, y + h);
  ctx!.quadraticCurveTo(x, y + h,     x, y + h - r);
  ctx!.lineTo(x, y + r);
  ctx!.quadraticCurveTo(x, y,         x + r, y);
  ctx!.closePath();
}

function lighten(hex: string, amt: number): string {
  const n = parseInt(hex.slice(1), 16);
  return `rgb(${Math.min(255,(n>>16)+amt)},${Math.min(255,((n>>8)&0xff)+amt)},${Math.min(255,(n&0xff)+amt)})`;
}
function darken(hex: string, amt: number): string {
  const n = parseInt(hex.slice(1), 16);
  return `rgb(${Math.max(0,(n>>16)-amt)},${Math.max(0,((n>>8)&0xff)-amt)},${Math.max(0,(n&0xff)-amt)})`;
}
</script>

<div class="wrap">
  <canvas bind:this={canvas}></canvas>

  {#if phase === 'menu'}
    <div class="overlay">
      <div class="card">
        <div class="logo">
          <span class="logo-icon">🐍</span>
          <span class="logo-text">SLITHER</span>
        </div>
        <p class="tagline">eat · grow · dominate</p>
        <input
          bind:value={myName}
          placeholder="enter your name"
          maxlength="16"
          spellcheck="false"
          onkeydown={e => e.key === 'Enter' && joinGame()}
        />
        <button class="btn-play" onclick={joinGame}>PLAY</button>
        <p class="hint">Move mouse to steer</p>
      </div>
    </div>

  {:else if phase === 'dead'}
    <div class="overlay">
      <div class="card dead">
        <div class="skull">💀</div>
        <h2>YOU DIED</h2>
        <div class="final-score">
          <span class="label">SCORE</span>
          <span class="value">{score}</span>
        </div>
        <button class="btn-play" onclick={respawn}>RESPAWN</button>
        <button class="btn-ghost" onclick={() => { phase = 'menu'; myId = ''; score = 0; }}>
          MAIN MENU
        </button>
      </div>
    </div>
  {/if}
</div>

<style>
  :global(*, *::before, *::after) { box-sizing: border-box; }
  :global(body) {
    margin: 0; overflow: hidden;
    background: #080c14;
    font-family: 'Courier New', Courier, monospace;
  }
  .wrap { position: relative; width: 100vw; height: 100vh; }
  canvas { display: block; cursor: none; }
  .overlay {
    position: absolute; inset: 0;
    display: flex; align-items: center; justify-content: center;
    background: rgba(4,7,14,0.72);
    backdrop-filter: blur(6px);
  }
  .card {
    background: linear-gradient(145deg, rgba(10,16,28,0.98), rgba(6,11,22,0.98));
    border: 1px solid rgba(100,160,255,0.12);
    border-radius: 4px;
    padding: 48px 44px 40px;
    display: flex; flex-direction: column; align-items: center; gap: 18px;
    min-width: 300px;
    box-shadow: 0 0 0 1px rgba(100,160,255,0.06), 0 30px 80px rgba(0,0,0,0.6), inset 0 1px 0 rgba(255,255,255,0.05);
  }
  .card.dead { border-color: rgba(255,50,70,0.2); }
  .logo { display: flex; align-items: center; gap: 10px; letter-spacing: 6px; }
  .logo-icon { font-size: 2rem; }
  .logo-text { font-size: 2.2rem; font-weight: 900; color: #4cff80; text-shadow: 0 0 20px rgba(76,255,128,0.4); }
  .tagline { margin: 0; color: rgba(100,160,255,0.5); font-size: 0.75rem; letter-spacing: 4px; text-transform: uppercase; }
  input {
    width: 100%; padding: 12px 16px;
    background: rgba(255,255,255,0.04);
    border: 1px solid rgba(100,160,255,0.2);
    border-radius: 3px; color: #d0e8ff; font-size: 15px;
    font-family: 'Courier New', monospace; letter-spacing: 1px;
    outline: none; transition: border-color 0.2s;
  }
  input::placeholder { color: rgba(100,160,255,0.25); }
  input:focus { border-color: rgba(76,255,128,0.5); }
  .btn-play {
    width: 100%; padding: 14px; background: #4cff80; color: #050b10;
    border: none; border-radius: 3px; font-size: 15px; font-weight: 900;
    letter-spacing: 3px; font-family: 'Courier New', monospace;
    cursor: pointer; transition: all 0.15s; box-shadow: 0 0 20px rgba(76,255,128,0.25);
  }
  .btn-play:hover { background: #6dffa0; box-shadow: 0 0 30px rgba(76,255,128,0.45); transform: translateY(-1px); }
  .btn-play:active { transform: translateY(0); }
  .btn-ghost {
    width: 100%; padding: 12px; background: transparent;
    border: 1px solid rgba(100,160,255,0.2); border-radius: 3px;
    color: rgba(100,160,255,0.5); font-size: 13px; letter-spacing: 2px;
    font-family: 'Courier New', monospace; cursor: pointer; transition: all 0.15s;
  }
  .btn-ghost:hover { border-color: rgba(100,160,255,0.45); color: #88aaee; }
  .hint { margin: 0; font-size: 0.7rem; color: rgba(100,160,255,0.25); letter-spacing: 2px; }
  .skull { font-size: 3rem; }
  h2 { margin: 0; color: #ff3244; font-size: 1.8rem; letter-spacing: 5px; }
  .final-score {
    display: flex; flex-direction: column; align-items: center; gap: 4px;
    padding: 16px 40px; border: 1px solid rgba(255,50,70,0.2);
    border-radius: 3px; background: rgba(255,50,70,0.05);
  }
  .label { font-size: 0.65rem; color: rgba(255,80,100,0.5); letter-spacing: 4px; }
  .value { font-size: 2.4rem; font-weight: 900; color: #FFD700; text-shadow: 0 0 20px rgba(255,215,0,0.3); }
</style>
