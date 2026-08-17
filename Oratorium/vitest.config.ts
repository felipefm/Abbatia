import { defineConfig } from 'vitest/config'
import react from '@vitejs/plugin-react'
import tailwindcss from '@tailwindcss/vite'

export default defineConfig({
  plugins: [react(), tailwindcss()],
  test: {
    environment: 'jsdom',
    setupFiles: ['./vitest.setup.ts'],
    // Padrão do Vitest (5s) é curto demais para estes testes: eles fazem
    // requisições HTTP reais contra uma API real (sem mocks), e desde que a
    // API ganhou busca sob demanda (ver docs/04-inteligencia-de-codigo.md,
    // seção 7), mesmo o teste de "hoje" pode acabar disparando uma raspagem
    // ao vivo (se rodado contra um banco vazio/recém-criado) em vez de bater
    // no cache — o que pode levar dezenas de segundos.
    testTimeout: 30000,
  },
})
