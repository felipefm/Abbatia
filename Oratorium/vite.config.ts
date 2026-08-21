import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'
import tailwindcss from '@tailwindcss/vite'
import { VitePWA } from 'vite-plugin-pwa'

export default defineConfig({
  plugins: [
    react(),
    tailwindcss(),
    VitePWA({
      registerType: 'autoUpdate',
      includeAssets: ['favicon.ico', 'icon-source.svg', 'apple-touch-icon-180x180.png'],
      manifest: {
        name: 'Oratorium — Devocional Diário',
        short_name: 'Oratorium',
        description: 'Liturgia diária, santo do dia e homilias do Papa, para oração e leitura.',
        lang: 'pt-BR',
        start_url: '/',
        display: 'standalone',
        background_color: '#2a0f28',
        theme_color: '#4c1d3d',
        icons: [
          { src: 'pwa-64x64.png', sizes: '64x64', type: 'image/png' },
          { src: 'pwa-192x192.png', sizes: '192x192', type: 'image/png' },
          { src: 'pwa-512x512.png', sizes: '512x512', type: 'image/png' },
          { src: 'maskable-icon-512x512.png', sizes: '512x512', type: 'image/png', purpose: 'maskable' },
        ],
      },
      workbox: {
        // O devocional muda diariamente e vem de uma API própria — nunca
        // servimos essas respostas do cache do service worker (evita
        // mostrar dados de ontem como se fossem de hoje). Só os assets
        // estáticos do próprio app (JS/CSS/ícones) usam cache-first,
        // permitindo o app abrir offline.
        navigateFallbackDenylist: [/^\/api\//],
        runtimeCaching: [
          {
            // Cobre tanto /api/devotional/{date} quanto
            // /api/devotional/calendar/{year}/{month} (o padrão é um
            // "contém", não uma âncora de início) — mesmo comportamento
            // NetworkFirst serve bem os dois, não precisa de uma regra
            // separada só pro calendário.
            urlPattern: /\/api\/devotional\//,
            handler: 'NetworkFirst',
            options: {
              cacheName: 'devotional-api',
              networkTimeoutSeconds: 5,
              expiration: { maxEntries: 16, maxAgeSeconds: 60 * 60 * 24 * 3 },
            },
          },
          {
            // Diário é escrita (PUT/DELETE) — nunca cachear, pra nunca
            // mostrar/aceitar como sucesso algo que não foi de fato salvo
            // no servidor.
            urlPattern: /\/api\/diary\//,
            handler: 'NetworkOnly',
          },
        ],
      },
    }),
  ],
})
