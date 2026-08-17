/// <reference types="@testing-library/jest-dom" />
import { describe, expect, it } from 'vitest'
import { render, screen, waitFor } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import App from './App'

/**
 * Smoke test de integração: renderiza a árvore de componentes real (sem
 * mocks) e faz requisições HTTP reais contra Scriptorium.API, apontado por
 * VITE_API_BASE_URL. Serve para validar que o front consome a resposta
 * real da API corretamente — não substitui testes unitários futuros.
 */
describe('Oratorium — smoke test contra a API real', () => {
  it('renderiza o devocional de hoje com dados reais da API', async () => {
    render(
      <MemoryRouter initialEntries={['/hoje']}>
        <App />
      </MemoryRouter>,
    )

    await waitFor(() => expect(screen.getByText(/Cor litúrgica/)).toBeInTheDocument(), { timeout: 10000 })

    expect(screen.getByText('Santo do Dia')).toBeInTheDocument()
    expect(screen.getAllByText('1ª Leitura').length).toBeGreaterThan(0)
    expect(screen.getAllByText('Evangelho').length).toBeGreaterThan(0)
  })

  it('mostra mensagem amigável para uma data fora do intervalo suportado (404, sem tentar raspar)', async () => {
    // 1899 é anterior a MinScrapableDate (2000-01-01) no backend — retorna
    // 404 IMEDIATO, sem tentar a busca sob demanda (ver DevotionalEndpoints.cs).
    // Usar uma data DENTRO do intervalo suportado aqui dispararia uma
    // raspagem ao vivo de verdade, tornando o teste lento e dependente da
    // disponibilidade dos 4 sites externos — o cenário de raspagem sob
    // demanda tem seu próprio teste, abaixo.
    render(
      <MemoryRouter initialEntries={['/dia/1899-01-01']}>
        <App />
      </MemoryRouter>,
    )

    await waitFor(
      () => expect(screen.getByText(/Não encontramos informações/)).toBeInTheDocument(),
      { timeout: 10000 },
    )
  })

  it(
    'busca sob demanda: uma data fora do cache (mas dentro do intervalo suportado) ainda traz o devocional completo',
    async () => {
      // Uma data bem no futuro, fora da janela de 7 dias que o Worker mantém
      // atualizada — força o cache-miss no Scriptorium.API, que deve raspar
      // as fontes ao vivo e devolver o resultado (não um 404). Timeout maior
      // (no `it`, não só no `waitFor`) porque essa chamada realmente sai
      // para a internet (4 scrapers em paralelo, até 30s cada) antes de
      // responder — o timeout padrão de 5s do Vitest mataria o teste antes
      // da API terminar.
      const farFutureDate = new Date()
      farFutureDate.setUTCDate(farFutureDate.getUTCDate() + 90)
      const iso = farFutureDate.toISOString().slice(0, 10)

      render(
        <MemoryRouter initialEntries={[`/dia/${iso}`]}>
          <App />
        </MemoryRouter>,
      )

      await waitFor(() => expect(screen.getByText(/Cor litúrgica/)).toBeInTheDocument(), { timeout: 45000 })
    },
    50000,
  )

  it('mostra mensagem amigável para formato de data inválido (400)', async () => {
    render(
      <MemoryRouter initialEntries={['/dia/data-invalida']}>
        <App />
      </MemoryRouter>,
    )

    await waitFor(
      () => expect(screen.getByText(/Formato de data inválido/)).toBeInTheDocument(),
      { timeout: 10000 },
    )
  })
})
