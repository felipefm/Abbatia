/** Corta um texto longo num trecho curto, preferindo terminar no fim de uma
 * frase; se não achar um ponto final razoavelmente perto do limite, corta
 * no último espaço e adiciona reticências. */
export function getExcerpt(text: string, maxLength = 220): string {
  const trimmed = text.trim()
  if (trimmed.length <= maxLength) return trimmed

  const cut = trimmed.slice(0, maxLength)
  const lastSentenceEnd = Math.max(cut.lastIndexOf('. '), cut.lastIndexOf('.\n'))
  if (lastSentenceEnd > maxLength * 0.4) {
    return cut.slice(0, lastSentenceEnd + 1)
  }

  const lastSpace = cut.lastIndexOf(' ')
  return `${cut.slice(0, lastSpace > 0 ? lastSpace : maxLength)}…`
}
