// Lê as preferências salvas antes do React montar, para não piscar o tema errado.
// Mantenha as chaves e valores em sincronia com src/ui/preferencias.ts.
(function () {
  var raiz = document.documentElement;
  try {
    var tema = window.localStorage.getItem("apolice.tema");
    if (tema === "claro") raiz.setAttribute("data-theme", "light");
    if (tema === "escuro") raiz.setAttribute("data-theme", "dark");
    var densidade = window.localStorage.getItem("apolice.densidade");
    raiz.setAttribute("data-density", densidade === "compacta" ? "compact" : "comfortable");
  } catch {
    // Storage bloqueado (modo privado, política do navegador): segue o tema do sistema.
    raiz.setAttribute("data-density", "comfortable");
  }
})();
