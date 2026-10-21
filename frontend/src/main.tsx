import "./ui/estilos/index.css";

import { StrictMode } from "react";
import { createRoot } from "react-dom/client";

import { App } from "./app/App";
import { prepararAnunciador } from "./ui/utils/anunciar";

const raiz = document.getElementById("root");
if (!raiz) throw new Error("Elemento #root não encontrado em index.html.");

prepararAnunciador();
createRoot(raiz).render(
  <StrictMode>
    <App />
  </StrictMode>,
);
