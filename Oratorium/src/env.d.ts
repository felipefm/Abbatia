interface ImportMetaEnv {
  /**
   * URL base da API do Scriptorium (ex: "http://192.168.1.10:8110").
   * Sem barra final. Configurável via .env / .env.production / variável
   * de ambiente de build — ver .env.example.
   */
  readonly VITE_API_BASE_URL: string
}

interface ImportMeta {
  readonly env: ImportMetaEnv
}

/**
 * Configuração injetada em TEMPO DE EXECUÇÃO (não em build-time), via
 * public/env-config.js — sobrescrito pelo entrypoint do container Docker a
 * partir da variável de ambiente ORATORIUM_API_BASE_URL. Existe para evitar
 * que trocar o endereço da API exija reconstruir a imagem do Oratorium
 * (mesmo problema que já resolvemos no LmStudio__BaseUrl do backend — ver
 * docs/04-inteligencia-de-codigo.md).
 */
interface Window {
  __ORATORIUM_CONFIG__?: {
    apiBaseUrl?: string
  }
}
