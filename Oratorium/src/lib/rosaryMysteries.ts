export interface RosaryMystery {
  name: string
  items: string[]
}

const MYSTERIES = {
  Gozosos: {
    name: 'Mistérios Gozosos',
    items: ['Anunciação', 'Visitação', 'Natividade', 'Apresentação no Templo', 'Encontro no Templo'],
  },
  Dolorosos: {
    name: 'Mistérios Dolorosos',
    items: ['Agonia no Horto', 'Flagelação', 'Coroação de Espinhos', 'Carregamento da Cruz', 'Crucificação'],
  },
  Gloriosos: {
    name: 'Mistérios Gloriosos',
    items: ['Ressurreição', 'Ascensão', 'Descida do Espírito Santo', 'Assunção de Maria', 'Coroação de Maria'],
  },
  Luminosos: {
    name: 'Mistérios Luminosos',
    items: ['Batismo no Jordão', 'Bodas de Caná', 'Anúncio do Reino', 'Transfiguração', 'Instituição da Eucaristia'],
  },
} as const satisfies Record<string, RosaryMystery>

/** Mistério do Rosário tradicionalmente associado a cada dia da semana
 * (Gozosos seg/sáb, Dolorosos ter/sex, Gloriosos qua/dom, Luminosos qui). */
export function getMysteryForDate(iso: string): RosaryMystery {
  const [year, month, day] = iso.split('-').map(Number)
  const dayOfWeek = new Date(Date.UTC(year, month - 1, day)).getUTCDay() // 0=domingo..6=sábado

  if (dayOfWeek === 0 || dayOfWeek === 3) return MYSTERIES.Gloriosos
  if (dayOfWeek === 1 || dayOfWeek === 6) return MYSTERIES.Gozosos
  if (dayOfWeek === 2 || dayOfWeek === 5) return MYSTERIES.Dolorosos
  return MYSTERIES.Luminosos // quinta-feira
}
