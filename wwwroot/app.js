const $ = (id) => document.getElementById(id);

const form = $("form");
const input = $("url");
const botao = $("go");
const erro = $("erro");
const resultado = $("resultado");
const curto = $("curto");
const copiar = $("copiar");
const recentes = $("recentes");
const lista = $("lista");
const painel = $("azulejos");

const CHAVE = "linkzin:recentes";
const MAX_RECENTES = 5;

/* ---------- Azulejos (inspirados nos painéis de Athos Bulcão) ---------- */

const PALETA = ["#1534c8", "#ffc21a", "#e5391b", "#0b1233", "#f6f7f2"];
const FORMAS = [
  "M0 0H100A100 100 0 0 1 0 100Z",         // quarto de círculo
  "M0 0H100L0 100Z",                        // diagonal
  "M50 16A34 34 0 1 1 49.9 16Z",            // círculo
  "M0 100A100 100 0 0 1 100 0A100 100 0 0 1 0 100Z", // folha
  "M0 100A50 50 0 0 1 100 100Z",            // semicírculo
];

function semente(texto) {
  let h = 1779033703 ^ texto.length;
  for (let i = 0; i < texto.length; i++) {
    h = Math.imul(h ^ texto.charCodeAt(i), 3432918353);
    h = (h << 13) | (h >>> 19);
  }
  return h >>> 0;
}

function sorteio(texto) {
  let s = semente(texto);
  return () => {
    s = (s + 0x6d2b79f5) | 0;
    let t = Math.imul(s ^ (s >>> 15), 1 | s);
    t = (t + Math.imul(t ^ (t >>> 7), 61 | t)) ^ t;
    return ((t ^ (t >>> 14)) >>> 0) / 4294967296;
  };
}

function sortearAzulejo(r) {
  const fundo = PALETA[(r() * PALETA.length) | 0];
  let frente = PALETA[(r() * PALETA.length) | 0];
  if (frente === fundo) frente = PALETA[(PALETA.indexOf(fundo) + 1) % PALETA.length];
  return { forma: FORMAS[(r() * FORMAS.length) | 0], fundo, frente };
}

function criarAzulejos(el, quantidade) {
  el.replaceChildren();
  const tiles = Array.from({ length: quantidade }, (_, i) => {
    const d = document.createElement("span");
    d.className = "tile";
    d.style.setProperty("--i", i);
    d.dataset.angulo = "0";
    d.innerHTML = `<svg viewBox="0 0 100 100" preserveAspectRatio="none"></svg>`;
    el.appendChild(d);
    return d;
  });

  return {
    // Forma, cor E rotação saem todas da semente do código — assim cada
    // link encurtado tem mesmo um padrão diferente, não só giradinho.
    girar(codigo) {
      const r = sorteio(codigo);
      tiles.forEach((d) => {
        const t = sortearAzulejo(r);
        d.querySelector("svg").innerHTML =
          `<rect width="100" height="100" fill="${t.fundo}"/><path d="${t.forma}" fill="${t.frente}"/>`;

        const alvo = ((r() * 4) | 0) * 90;
        const atual = Number(d.dataset.angulo);
        let delta = (((alvo - atual) % 360) + 360) % 360;
        if (delta === 0) delta = 360;
        const novo = atual + delta;
        d.dataset.angulo = novo;
        d.style.transform = `rotate(${novo}deg)`;
      });
    },
  };
}

const grande = criarAzulejos(painel, 25);
requestAnimationFrame(() => requestAnimationFrame(() => grande.girar("curtim")));

/* ---------- Histórico ---------- */

function lerRecentes() {
  try {
    return JSON.parse(localStorage.getItem(CHAVE)) ?? [];
  } catch {
    return [];
  }
}

function salvarRecentes(itens) {
  try {
    localStorage.setItem(CHAVE, JSON.stringify(itens));
  } catch {
    /* sem armazenamento: segue sem histórico */
  }
}

function codigoDe(shortUrl) {
  return shortUrl.split("/").pop();
}

function desenharRecentes() {
  const itens = lerRecentes();
  recentes.hidden = itens.length === 0;
  lista.replaceChildren(
    ...itens.map((item) => {
      const li = document.createElement("li");

      const mini = document.createElement("div");
      mini.className = "azulejos mini";
      mini.setAttribute("aria-hidden", "true");
      const tiles = criarAzulejos(mini, 9);
      tiles.girar(codigoDe(item.shortUrl));
      mini.querySelectorAll(".tile").forEach((t) => (t.style.transitionDelay = "0s"));

      const info = document.createElement("div");
      info.className = "info";
      const a = document.createElement("a");
      a.href = item.shortUrl;
      a.target = "_blank";
      a.rel = "noopener";
      a.textContent = item.shortUrl.replace(/^https?:\/\//, "");
      const small = document.createElement("small");
      small.textContent = item.longUrl;
      info.append(a, small);

      const btn = document.createElement("button");
      btn.type = "button";
      btn.textContent = "Copiar";
      btn.addEventListener("click", () => copiarTexto(item.shortUrl, btn));

      li.append(mini, info, btn);
      return li;
    })
  );
}

/* ---------- Ações ---------- */

function normalizar(valor) {
  const v = valor.trim();
  if (!v) return "";
  return /^https?:\/\//i.test(v) ? v : `https://${v}`;
}

function mostrarErro(msg) {
  erro.textContent = msg;
  erro.hidden = false;
  resultado.hidden = true;
}

async function copiarTexto(texto, btn) {
  try {
    await navigator.clipboard.writeText(texto);
  } catch {
    const t = document.createElement("textarea");
    t.value = texto;
    document.body.appendChild(t);
    t.select();
    document.execCommand("copy");
    t.remove();
  }
  const original = btn.textContent;
  btn.textContent = "Copiado";
  btn.classList.add("ok");
  setTimeout(() => {
    btn.textContent = original;
    btn.classList.remove("ok");
  }, 1500);
}

form.addEventListener("submit", async (e) => {
  e.preventDefault();
  erro.hidden = true;

  const longUrl = normalizar(input.value);
  if (!longUrl) {
    mostrarErro("Cole um link para encurtar.");
    return;
  }

  botao.disabled = true;
  botao.textContent = "Encurtando…";

  try {
    const res = await fetch("/shorten", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ longUrl }),
    });

    if (res.status === 400) {
      mostrarErro("Esse endereço não é válido. Use um link que comece com http:// ou https://.");
      return;
    }
    if (!res.ok) throw new Error(`HTTP ${res.status}`);

    const data = await res.json();
    curto.href = data.shortUrl;
    curto.textContent = data.shortUrl.replace(/^https?:\/\//, "");
    resultado.hidden = false;
    input.value = data.longUrl;
    grande.girar(data.code);

    const itens = [
      { shortUrl: data.shortUrl, longUrl: data.longUrl },
      ...lerRecentes().filter((i) => i.shortUrl !== data.shortUrl),
    ].slice(0, MAX_RECENTES);
    salvarRecentes(itens);
    desenharRecentes();
  } catch {
    mostrarErro("Não foi possível encurtar agora. Tente de novo em instantes.");
  } finally {
    botao.disabled = false;
    botao.textContent = "Encurtar";
  }
});

copiar.addEventListener("click", () => copiarTexto(curto.href, copiar));

$("limpar").addEventListener("click", () => {
  salvarRecentes([]);
  desenharRecentes();
});

desenharRecentes();
