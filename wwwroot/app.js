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

const CHAVE = "curtim:recentes";
const MAX_RECENTES = 5;

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
    /* storage indisponível: segue sem histórico */
  }
}

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
  btn.textContent = "Copiou!";
  btn.classList.add("ok");
  setTimeout(() => {
    btn.textContent = original;
    btn.classList.remove("ok");
  }, 1500);
}

function desenharRecentes() {
  const itens = lerRecentes();
  recentes.hidden = itens.length === 0;
  lista.replaceChildren(
    ...itens.map((item) => {
      const li = document.createElement("li");

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

      li.append(info, btn);
      return li;
    })
  );
}

form.addEventListener("submit", async (e) => {
  e.preventDefault();
  erro.hidden = true;

  const longUrl = normalizar(input.value);
  if (!longUrl) {
    mostrarErro("Cola um link aí primeiro 😉");
    return;
  }

  botao.disabled = true;
  botao.firstElementChild.textContent = "Curtinando…";

  try {
    const res = await fetch("/shorten", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ longUrl }),
    });

    if (res.status === 400) {
      mostrarErro("Esse link não parece válido. Confere se começa com http:// ou https://");
      return;
    }
    if (!res.ok) throw new Error(`HTTP ${res.status}`);

    const data = await res.json();
    curto.href = data.shortUrl;
    curto.textContent = data.shortUrl.replace(/^https?:\/\//, "");
    resultado.hidden = false;
    input.value = data.longUrl;

    const itens = [
      { shortUrl: data.shortUrl, longUrl: data.longUrl },
      ...lerRecentes().filter((i) => i.shortUrl !== data.shortUrl),
    ].slice(0, MAX_RECENTES);
    salvarRecentes(itens);
    desenharRecentes();
  } catch {
    mostrarErro("Deu ruim por aqui. Tenta de novo daqui a pouco?");
  } finally {
    botao.disabled = false;
    botao.firstElementChild.textContent = "Curtinar";
  }
});

copiar.addEventListener("click", () => copiarTexto(curto.href, copiar));

$("limpar").addEventListener("click", () => {
  salvarRecentes([]);
  desenharRecentes();
});

desenharRecentes();
