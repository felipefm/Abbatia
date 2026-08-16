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

  it('mostra mensagem amigável para uma data sem devocional (404)', async () => {
    render(
      <MemoryRouter initialEntries={['/dia/2030-01-01']}>
        <App />
      </MemoryRouter>,
    )

    await waitFor(
      () => expect(screen.getByText(/ainda não está disponível/)).toBeInTheDocument(),
      { timeout: 10000 },
    )
  })

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
