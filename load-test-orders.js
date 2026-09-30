import http from "k6/http";
import { check, sleep } from "k6";

// Configuração dos estágios de carga (poucos -> médios -> muitos usuários)
export const options = {
  stages: [
    { duration: "10s", target: 5 }, // Poucos usuários: 5 VUs (Virtual Users)
    { duration: "20s", target: 50 }, // Carga moderada: 50 VUs
    { duration: "20s", target: 400 }, // Carga pesada / pico: 150 VUs
    { duration: "10s", target: 0 }, // Desaceleração até 0 VUs
  ],
  thresholds: {
    // 95% das requisições devem responder em menos de 200ms
    http_req_duration: ["p(95)<200"],
    // Taxa de falhas deve ser menor que 1%
    http_req_failed: ["rate<0.01"],
  },
};

export default function () {
  // Ajuste a porta se sua aplicação estiver rodando em outra (ex: 3000 ou 5000)
  const baseUrl = __ENV.API_URL || "http://localhost:3000";
  const url = `${baseUrl}/api/orders`;

  const params = {
    headers: {
      Accept: "application/json",
    },
  };

  const res = http.get(url, params);

  // Verificações de resposta
  check(res, {
    "status é 200": (r) => r.status === 200,
    "resposta não está vazia": (r) => r.body && r.body.length > 0,
  });

  // Pausa curta de 100ms entre requisições de cada usuário virtual
  sleep(0.1);
}
